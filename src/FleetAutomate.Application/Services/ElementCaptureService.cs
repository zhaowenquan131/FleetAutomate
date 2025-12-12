using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA2;
using FlaUI.UIA3;

namespace Canvas.TestRunner.Services
{
    /// <summary>
    /// Service for capturing UI elements by mouse position and Ctrl key press.
    /// Supports both UIA3 and UIA2 with automatic fallback on access denied errors.
    /// </summary>
    public class ElementCaptureService
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_CONTROL = 0x11;

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _hookProc;
        private UIA3Automation _uia3Automation;
        private UIA2Automation _uia2Automation;
        private bool _isCapturing = false;
        private DispatcherTimer _hoverCheckTimer;
        private AutomationElement _previousHoveredElement;
        private DateTime _lastHoverTime = DateTime.MinValue;

        // Throttling for inaccessible regions
        private System.Drawing.Point _lastFailedPoint = System.Drawing.Point.Empty;
        private DateTime _lastFailureTime = DateTime.MinValue;
        private const int FAILURE_THROTTLE_MS = 500; // Don't retry same region for 500ms

        // Position tracking to prevent redundant attempts
        private System.Drawing.Point _lastAttemptedPoint = System.Drawing.Point.Empty;
        private AutomationElement _lastSuccessfulElement = null;

        // Ctrl key debounce
        private bool _ctrlWasPressed = false;

        // Access denied tracking
        private int _consecutiveAccessDenied = 0;
        private bool _accessDeniedWarningShown = false;

        // Performance optimization: Cache window handle and its element
        private IntPtr _lastWindowHandle = IntPtr.Zero;
        private AutomationElement _lastWindowElement = null;
        private DateTime _lastWindowElementTime = DateTime.MinValue;
        private const int WINDOW_CACHE_MS = 2000; // Cache window element for 2 seconds

        public event Action<AutomationElement>? OnElementCaptured;
        public event Action? OnCaptureModeEntered;
        public event Action? OnCaptureModeExited;
        public event Action<AutomationElement>? OnElementHovered;
        public event Action? OnAccessDeniedWarning;
        public event Action? OnHoverCleared;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern void GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr ChildWindowFromPointEx(IntPtr hwndParent, POINT pt, uint uFlags);

        private const uint CWP_SKIPINVISIBLE = 0x0001;
        private const uint CWP_SKIPDISABLED = 0x0002;
        private const uint CWP_SKIPTRANSPARENT = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        public ElementCaptureService()
        {
            _hookProc = HookCallback;
        }

        public void StartCapture()
        {
            if (_isCapturing)
                return;

            System.Diagnostics.Debug.WriteLine("[ElementCaptureService] StartCapture called");

            try
            {
                System.Diagnostics.Debug.WriteLine("[ElementCaptureService] Starting hover detection timer");
                _isCapturing = true;

                // Start timer to check hovered element continuously
                _hoverCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                _hoverCheckTimer.Tick += HoverCheckTimer_Tick;
                _hoverCheckTimer.Start();

                System.Diagnostics.Debug.WriteLine("[ElementCaptureService] Hover detection timer started, capture mode active");
                OnCaptureModeEntered?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Exception in StartCapture: {ex.Message}\n{ex.StackTrace}");
                _isCapturing = false;
                throw new InvalidOperationException("Failed to start element capture", ex);
            }
        }

        public void StopCapture()
        {
            if (!_isCapturing)
                return;

            System.Diagnostics.Debug.WriteLine("[ElementCaptureService] StopCapture called");
            _isCapturing = false;
            _ctrlWasPressed = false; // Reset Ctrl state
            _lastAttemptedPoint = System.Drawing.Point.Empty; // Reset position tracking
            _lastSuccessfulElement = null; // Clear cached element

            // Clear window cache for fresh start on next capture
            _lastWindowHandle = IntPtr.Zero;
            _lastWindowElement = null;
            _lastWindowElementTime = DateTime.MinValue;

            if (_hoverCheckTimer != null)
            {
                System.Diagnostics.Debug.WriteLine("[ElementCaptureService] Stopping hover detection timer");
                _hoverCheckTimer.Stop();
                _hoverCheckTimer = null;
            }

            if (_hookId != IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("[ElementCaptureService] Unhooking keyboard hook");
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            try
            {
                if (_uia3Automation != null)
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureService] Disposing UIA3 automation");
                    _uia3Automation.Dispose();
                    _uia3Automation = null;
                }

                if (_uia2Automation != null)
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureService] Disposing UIA2 automation");
                    _uia2Automation.Dispose();
                    _uia2Automation = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Exception disposing automation: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("[ElementCaptureService] Capture mode stopped");
            OnCaptureModeExited?.Invoke();
        }

        private void HoverCheckTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (!_isCapturing)
                    return;

                // Get mouse position
                GetCursorPos(out POINT point);

                // Skip if position hasn't changed (no movement)
                if (point.X == _lastAttemptedPoint.X && point.Y == _lastAttemptedPoint.Y)
                {
                    // Mouse hasn't moved, check for Ctrl key but don't re-query element
                    bool ctrlIsPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

                    if (ctrlIsPressed && !_ctrlWasPressed)
                    {
                        // Ctrl just pressed - use last successful element if available
                        if (_lastSuccessfulElement != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ElementCapture] Ctrl key detected with cached element!");
                            OnElementCaptured?.Invoke(_lastSuccessfulElement);
                        }
                        else
                        {
                            // No element cached, stop capture
                            System.Diagnostics.Debug.WriteLine($"[ElementCapture] Ctrl key detected without element - stopping capture");
                            StopCapture();
                        }
                    }

                    _ctrlWasPressed = ctrlIsPressed;
                    return; // Skip element query, position unchanged
                }

                // Position changed, update tracking and query element
                _lastAttemptedPoint = new System.Drawing.Point(point.X, point.Y);

                System.Diagnostics.Debug.WriteLine($"[ElementCapture] Mouse moved to: {point.X}, {point.Y}");

                // Get element at position
                var element = GetElementAtPoint(point.X, point.Y);

                if (element != null)
                {
                    // Safely log element info
                    try
                    {
                        string elementName = "(no name)";
                        string elementType = "Unknown";
                        try { elementName = element.Properties.Name.ValueOrDefault ?? "(no name)"; } catch { }
                        try { elementType = element.Properties.ControlType.ValueOrDefault.ToString(); } catch { }
                        System.Diagnostics.Debug.WriteLine($"[ElementCapture] Found element: {elementName}, Type: {elementType}");
                    }
                    catch { }

                    // Reset access denied counter
                    _consecutiveAccessDenied = 0;
                    _accessDeniedWarningShown = false;

                    // Cache successful element for reuse when Ctrl is pressed without movement
                    _lastSuccessfulElement = element;

                    // Only fire event if the element has changed
                    bool elementChanged = _previousHoveredElement == null;

                    if (!elementChanged)
                    {
                        try
                        {
                            string newName = "";
                            string oldName = "";
                            try { newName = element.Properties.Name.ValueOrDefault ?? ""; } catch { }
                            try { oldName = _previousHoveredElement.Properties.Name.ValueOrDefault ?? ""; } catch { }
                            elementChanged = newName != oldName;
                        }
                        catch { }

                        if (!elementChanged)
                        {
                            try
                            {
                                string newId = "";
                                string oldId = "";
                                try { newId = element.Properties.AutomationId.ValueOrDefault ?? ""; } catch { }
                                try { oldId = _previousHoveredElement.Properties.AutomationId.ValueOrDefault ?? ""; } catch { }
                                elementChanged = newId != oldId;
                            }
                            catch { }
                        }
                    }

                    if (elementChanged)
                    {
                        string changedName = "(no name)";
                        try { changedName = element.Properties.Name.ValueOrDefault ?? "(no name)"; } catch { }
                        System.Diagnostics.Debug.WriteLine($"[ElementCapture] Hovered element changed to: {changedName}");
                        _previousHoveredElement = element;
                        _lastHoverTime = DateTime.Now;
                    }

                    // Always notify so UI can refresh highlights even when hovering same element
                    OnElementHovered?.Invoke(element);

                    // If Ctrl is pressed, capture this element (if hovering) or stop capture
                    bool ctrlIsPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

                    if (ctrlIsPressed && !_ctrlWasPressed)
                    {
                        // Ctrl just pressed (rising edge)
                        System.Diagnostics.Debug.WriteLine($"[ElementCapture] Ctrl key detected with hovered element!");
                        OnElementCaptured?.Invoke(element);
                        // Note: Dialog will call StopCapture after successful capture
                    }

                    _ctrlWasPressed = ctrlIsPressed;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ElementCapture] No element found at position (access denied or no element)");

                    // Track consecutive access denied
                    _consecutiveAccessDenied++;

                    // Warn user if consistently getting access denied
                    if (_consecutiveAccessDenied >= 10 && !_accessDeniedWarningShown)
                    {
                        _accessDeniedWarningShown = true;
                        OnAccessDeniedWarning?.Invoke();
                    }

                    OnHoverCleared?.Invoke();

                    // No element at cursor, clear cache
                    _lastSuccessfulElement = null;

                    // Check if Ctrl is pressed to stop capture
                    bool ctrlIsPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

                    if (ctrlIsPressed && !_ctrlWasPressed)
                    {
                        // Ctrl just pressed (rising edge) without element - stop capture
                        System.Diagnostics.Debug.WriteLine($"[ElementCapture] Ctrl key detected without element - stopping capture");
                        StopCapture();
                    }

                    _ctrlWasPressed = ctrlIsPressed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ElementCapture] Exception in HoverCheckTimer_Tick: {ex.Message}");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            System.Diagnostics.Debug.WriteLine($"[ElementCapture] HookCallback called - nCode: {nCode}, wParam: {wParam}, WM_KEYDOWN: {(IntPtr)WM_KEYDOWN}");

            if (nCode >= 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ElementCapture] nCode >= 0, checking if KEYDOWN...");
                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    System.Diagnostics.Debug.WriteLine($"[ElementCapture] WM_KEYDOWN detected");
                    KBDLLHOOKSTRUCT kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    System.Diagnostics.Debug.WriteLine($"[ElementCapture] vkCode: {kbd.vkCode}, VK_CONTROL: {VK_CONTROL}, _isCapturing: {_isCapturing}");

                    if (kbd.vkCode == VK_CONTROL)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ElementCapture] CONTROL key detected!");
                        if (_isCapturing)
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"[ElementCapture] Ctrl key detected while capturing");
                                GetCursorPos(out POINT point);
                                System.Diagnostics.Debug.WriteLine($"[ElementCapture] Cursor position: X={point.X}, Y={point.Y}");

                                var element = GetElementAtPoint(point.X, point.Y);

                                string elementDebugInfo = "null";
                                if (element != null)
                                {
                                    try { elementDebugInfo = element.Properties.Name.ValueOrDefault ?? "(no name)"; } catch { elementDebugInfo = "(no name)"; }
                                }
                                System.Diagnostics.Debug.WriteLine($"[ElementCapture] Element found: {elementDebugInfo}");

                                if (element != null)
                                {
                                    OnElementCaptured?.Invoke(element);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ElementCapture] Exception: {ex.Message}");
                            }
                        }
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private AutomationElement? GetElementAtPoint(int x, int y)
        {
            var point = new System.Drawing.Point(x, y);

            // Throttling: if we recently failed at a nearby position, skip this attempt
            if (!_lastFailedPoint.IsEmpty &&
                (DateTime.Now - _lastFailureTime).TotalMilliseconds < FAILURE_THROTTLE_MS)
            {
                // Check if we're within 50 pixels of the last failed point
                int dx = Math.Abs(point.X - _lastFailedPoint.X);
                int dy = Math.Abs(point.Y - _lastFailedPoint.Y);
                if (dx < 50 && dy < 50)
                {
                    return null; // Skip this attempt
                }
            }

            // Try UIA3 first (preferred, more modern API)
            var element = TryGetElementWithUIA3(point);
            if (element != null)
            {
                // Success - clear failure tracking
                _lastFailedPoint = System.Drawing.Point.Empty;
                return element;
            }

            // Fallback to UIA2 if UIA3 failed
            element = TryGetElementWithUIA2(point);
            if (element != null)
            {
                // Success - clear failure tracking
                _lastFailedPoint = System.Drawing.Point.Empty;
                return element;
            }

            // Both FromPoint methods failed - try using WindowFromPoint as last resort
            // This helps with applications that have transparent overlays
            element = TryGetElementUsingWindowFromPoint(point);
            if (element != null)
            {
                // Success - clear failure tracking
                _lastFailedPoint = System.Drawing.Point.Empty;
                return element;
            }

            // All methods failed - record this location and time
            _lastFailedPoint = point;
            _lastFailureTime = DateTime.Now;
            return null;
        }

        private AutomationElement? TryGetElementUsingWindowFromPoint(System.Drawing.Point point)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ElementCaptureService] Trying WindowFromPoint as fallback");

                // Get the window handle at the point
                var nativePoint = new POINT { X = point.X, Y = point.Y };
                IntPtr hwnd = WindowFromPoint(nativePoint);

                if (hwnd == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureService] WindowFromPoint returned null");
                    return null;
                }

                // Use UIA3 to get element from the window handle
                if (_uia3Automation == null)
                {
                    _uia3Automation = new UIA3Automation();
                }

                AutomationElement? windowElement = null;

                // Check if we can reuse cached window element (performance optimization)
                bool cacheValid = _lastWindowHandle == hwnd &&
                                  _lastWindowElement != null &&
                                  (DateTime.Now - _lastWindowElementTime).TotalMilliseconds < WINDOW_CACHE_MS;

                if (cacheValid)
                {
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Using cached window element (handle: {hwnd})");
                    windowElement = _lastWindowElement;
                }
                else
                {
                    // Cache miss or expired - fetch fresh window element
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Cache miss - fetching window element for handle: {hwnd}");
                    windowElement = _uia3Automation.FromHandle(hwnd);

                    if (windowElement == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[ElementCaptureService] FromHandle returned null for hwnd");
                        // Clear cache on failure
                        _lastWindowHandle = IntPtr.Zero;
                        _lastWindowElement = null;
                        return null;
                    }

                    // Update cache
                    _lastWindowHandle = hwnd;
                    _lastWindowElement = windowElement;
                    _lastWindowElementTime = DateTime.Now;
                }

                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Got window from handle, finding deepest element");

                // Now find the deepest element at this point within the window
                var deepestElement = FindDeepestElementAtPoint(windowElement, point);
                if (deepestElement != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] WindowFromPoint approach succeeded");
                    return deepestElement;
                }

                System.Diagnostics.Debug.WriteLine("[ElementCaptureService] WindowFromPoint approach found no suitable element");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] WindowFromPoint approach failed: {ex.Message}");
                // Clear cache on exception
                _lastWindowHandle = IntPtr.Zero;
                _lastWindowElement = null;
                return null;
            }
        }

        private AutomationElement? TryGetElementWithUIA3(System.Drawing.Point point)
        {
            try
            {
                // Lazy initialize UIA3 automation only when needed
                if (_uia3Automation == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureService] Initializing UIA3Automation");
                    _uia3Automation = new UIA3Automation();
                }

                var element = _uia3Automation.FromPoint(point);

                if (element == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureService] UIA3 FromPoint returned null - no element at this location");
                    return null;
                }

                // Validate the element before processing
                try
                {
                    // Access a property to ensure the element is accessible
                    var _ = element.Properties.ControlType.ValueOrDefault;
                }
                catch (UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureService] UIA3 element access denied, falling back to UIA2");
                    return null;
                }
                catch (System.Runtime.InteropServices.COMException ex) when (
                    ex.HResult == unchecked((int)0x80070005))  // E_ACCESSDENIED
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureService] UIA3 element access denied (COM), falling back to UIA2");
                    return null;
                }

                // Try to find the smallest/deepest child element at this point
                // This is important for Group elements which often have no visual bounds
                var deepestElement = FindDeepestElementAtPoint(element, point);
                if (deepestElement != null)
                {
                    // Verify the deepest element has valid bounds
                    try
                    {
                        var bounds = deepestElement.Properties.BoundingRectangle.ValueOrDefault;
                        if (bounds.Width > 0 && bounds.Height > 0)
                        {
                            return deepestElement;
                        }
                    }
                    catch { }
                }

                // If deepest element has no bounds, check if original element has bounds
                // But also reject if it's too large (full-screen overlay)
                try
                {
                    var bounds = element.Properties.BoundingRectangle.ValueOrDefault;
                    if (bounds.Width > 0 && bounds.Height > 0 &&
                        bounds.Width < 2000 && bounds.Height < 2000)
                    {
                        return element;
                    }
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] UIA3 original element rejected - too large ({bounds.Width}x{bounds.Height})");
                }
                catch { }

                // Element has no visible bounds or is too large, skip it
                System.Diagnostics.Debug.WriteLine("[ElementCaptureService] UIA3 element has no valid bounds");
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied - silently fall back to UIA2
                System.Diagnostics.Debug.WriteLine("[ElementCaptureService] UIA3 access denied exception, falling back to UIA2");
                return null;
            }
            catch (System.Runtime.InteropServices.COMException ex) when (
                ex.HResult == unchecked((int)0x80070005) || // E_ACCESSDENIED
                ex.HResult == unchecked((int)0x80004005))    // E_FAIL
            {
                // Access denied or generic failure - silently fall back to UIA2
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] UIA3 COM exception {ex.HResult:X}, falling back to UIA2");
                return null;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Win32 exception (typically access denied) - silently fall back to UIA2
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] UIA3 Win32 exception {ex.NativeErrorCode}, falling back to UIA2");
                return null;
            }
            catch (Exception ex)
            {
                // Any other exception - silently fail
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] UIA3 unexpected exception: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        private AutomationElement? TryGetElementWithUIA2(System.Drawing.Point point)
        {
            try
            {
                // Lazy initialize UIA2 automation only when needed
                if (_uia2Automation == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureService] Initializing UIA2Automation as fallback");
                    _uia2Automation = new UIA2Automation();
                }

                var element = _uia2Automation.FromPoint(point);

                if (element == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureService] UIA2 FromPoint returned null - no element at this location");
                    return null;
                }

                // Validate the element before processing
                try
                {
                    // Access a property to ensure the element is accessible
                    var _ = element.Properties.ControlType.ValueOrDefault;
                }
                catch (UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureService] UIA2 element access denied");
                    return null;
                }
                catch (System.Runtime.InteropServices.COMException ex) when (
                    ex.HResult == unchecked((int)0x80070005))  // E_ACCESSDENIED
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureService] UIA2 element access denied (COM)");
                    return null;
                }

                // Try to find the smallest/deepest child element at this point
                // This is important for Group elements which often have no visual bounds
                var deepestElement = FindDeepestElementAtPoint(element, point);
                if (deepestElement != null)
                {
                    // Verify the deepest element has valid bounds
                    try
                    {
                        var bounds = deepestElement.Properties.BoundingRectangle.ValueOrDefault;
                        if (bounds.Width > 0 && bounds.Height > 0)
                        {
                            return deepestElement;
                        }
                    }
                    catch { }
                }

                // If deepest element has no bounds, check if original element has bounds
                // But also reject if it's too large (full-screen overlay)
                try
                {
                    var bounds = element.Properties.BoundingRectangle.ValueOrDefault;
                    if (bounds.Width > 0 && bounds.Height > 0 &&
                        bounds.Width < 2000 && bounds.Height < 2000)
                    {
                        return element;
                    }
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] UIA2 original element rejected - too large ({bounds.Width}x{bounds.Height})");
                }
                catch { }

                // Element has no visible bounds or is too large, skip it
                System.Diagnostics.Debug.WriteLine("[ElementCaptureService] UIA2 element has no valid bounds");
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                // Still access denied even with UIA2 - silently fail
                System.Diagnostics.Debug.WriteLine("[ElementCaptureService] UIA2 access denied exception");
                return null;
            }
            catch (System.Runtime.InteropServices.COMException ex) when (
                ex.HResult == unchecked((int)0x80070005) || // E_ACCESSDENIED
                ex.HResult == unchecked((int)0x80004005))    // E_FAIL
            {
                // Access denied or generic failure even with UIA2 - silently fail
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] UIA2 COM exception {ex.HResult:X}");
                return null;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Win32 exception (typically access denied) - silently fail
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] UIA2 Win32 exception {ex.NativeErrorCode}");
                return null;
            }
            catch (Exception ex)
            {
                // Any other exception - silently fail
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] UIA2 unexpected exception: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        private AutomationElement? FindDeepestElementAtPoint(AutomationElement element, System.Drawing.Point point)
{
            try
            {
                // Use FindAllDescendants instead of FindAllChildren to search deeper in the tree
                // This is critical for finding actual controls inside complex window hierarchies
                AutomationElement[]? descendants = null;

                try
                {
                    descendants = element.FindAllDescendants();
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] FindAllDescendants returned {descendants?.Length ?? 0} descendants");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] FindAllDescendants failed: {ex.Message}");
                }

                if (descendants == null || descendants.Length == 0)
                {
                    // No descendants, check if this element itself has valid bounds
                    try
                    {
                        var elementBounds = element.Properties.BoundingRectangle.ValueOrDefault;
                        System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] No descendants, checking element bounds: {elementBounds.Width}x{elementBounds.Height}");

                        // Reject elements that are too large (likely full-screen or window elements)
                        // Typical controls are much smaller than 1000x1000 pixels
                        if (elementBounds.Width > 0 && elementBounds.Height > 0 &&
                            elementBounds.Contains(point) &&
                            elementBounds.Width < 2000 && elementBounds.Height < 2000)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Accepting element with reasonable bounds");
                            return element;
                        }

                        System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Rejecting element - too large or invalid bounds");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Exception checking element bounds: {ex.Message}");
                    }
                    return null;
                }

                // Find the smallest element that contains the point AND has valid bounds
                AutomationElement? smallestElement = null;
                double smallestArea = double.MaxValue;
                int consideredCount = 0;
                int skippedCount = 0;

                foreach (var descendant in descendants)
                {
                    try
                    {
                        var bounds = descendant.Properties.BoundingRectangle.ValueOrDefault;

                        // Only consider elements with valid bounds that contain the point
                        // Also reject very large elements (likely containers)
                        if (bounds.Width > 0 && bounds.Height > 0 && bounds.Contains(point))
                        {
                            double area = bounds.Width * bounds.Height;

                            // Prefer smaller elements, but reject anything too large (> 2000x2000)
                            if (area < smallestArea && bounds.Width < 2000 && bounds.Height < 2000)
                            {
                                smallestArea = area;
                                smallestElement = descendant;
                                consideredCount++;
                            }
                            else if (bounds.Width >= 2000 || bounds.Height >= 2000)
                            {
                                skippedCount++;
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip this descendant - access denied
                        continue;
                    }
                    catch (System.Runtime.InteropServices.COMException ex) when (
                        ex.HResult == unchecked((int)0x80070005) || // E_ACCESSDENIED
                        ex.HResult == unchecked((int)0x80040201))   // UIA_E_ELEMENTNOTAVAILABLE
                    {
                        // Skip this descendant - access denied or element no longer available
                        continue;
                    }
                    catch
                    {
                        // Skip problematic descendants
                        continue;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Considered {consideredCount} elements, skipped {skippedCount} large elements, smallest area: {smallestArea}");

                // Return the smallest element found, or null if none found
                if (smallestElement != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Returning smallest element with area {smallestArea}");
                    return smallestElement;
                }

                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] No suitable descendant found");
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Access denied in FindDeepestElementAtPoint: {ex.Message}");
                return null;
            }
            catch (System.Runtime.InteropServices.COMException ex) when (
                ex.HResult == unchecked((int)0x80070005))  // E_ACCESSDENIED
            {
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] COM access denied in FindDeepestElementAtPoint: {ex.HResult:X}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureService] Exception in FindDeepestElementAtPoint: {ex.Message}");
                return null;
            }
        }
    }
}
