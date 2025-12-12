using System;
using System.Drawing;
using System.Windows;
using FlaUI.Core.AutomationElements;
using Wpf.Ui.Controls;
using FleetAutomate.Services;
using WpfMessageBox = Wpf.Ui.Controls.MessageBox;
using System.Windows.Media;
using WinForms = System.Windows.Forms;
using FlaUI.Core.Overlay;
using FlaUI.Core.WindowsAPI;

namespace FleetAutomate.View.Dialog
{
    /// <summary>
    /// Interaction logic for ElementCaptureDialog.xaml
    /// </summary>
    public partial class ElementCaptureDialog : FluentWindow
    {
        private ElementCaptureService _captureService;
        private string _capturedXPath = string.Empty;
        private bool _isCapturing = false;
        private Rectangle _currentHighlightRect = Rectangle.Empty;
        private readonly ScreenHighlighter _screenHighlighter = new ScreenHighlighter();

        public string CapturedXPath { get; private set; } = string.Empty;
        public bool UseInIfCondition { get; private set; } = false;

        public ElementCaptureDialog()
        {
            System.Diagnostics.Debug.WriteLine("[ElementCaptureDialog] Constructor called");
            InitializeComponent();
            _captureService = new ElementCaptureService();
            System.Diagnostics.Debug.WriteLine("[ElementCaptureDialog] ElementCaptureService created");

            _captureService.OnElementCaptured += OnElementCaptured;
            _captureService.OnCaptureModeExited += OnCaptureModeExited;
            _captureService.OnElementHovered += OnElementHovered;
            _captureService.OnAccessDeniedWarning += OnAccessDeniedWarning;
            _captureService.OnHoverCleared += OnHoverCleared;

            // Don't auto-start capture, wait for user to click button
            System.Diagnostics.Debug.WriteLine("[ElementCaptureDialog] Constructor completed");
        }

        private void OnAccessDeniedWarning()
        {
            Dispatcher.Invoke(() =>
            {
                CaptureStatusText.Text = "Warning: Access Denied. Cannot inspect elevated/admin windows. Try running Canvas.TestRunner as Administrator, or hover over non-elevated applications.";
                CaptureStatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD3, 0x2F, 0x2F)); // Red warning
            });
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCapturing)
            {
                StopCapture();
            }
            else
            {
                StartCapture();
            }
        }

        private void StartCapture()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ElementCaptureDialog] Starting capture");
                _captureService.StartCapture();
                _isCapturing = true;

                // Update UI
                StartStopButton.Content = "Stop Capture";
                StartStopButton.Icon = new SymbolIcon(SymbolRegular.Stop20);
                StartStopButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Danger;
                CaptureStatusText.Text = "Hover over UI elements to see their information. Press Ctrl to capture, or click 'Stop Capture' to exit capture mode.";
                CaptureStatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x5E, 0x20)); // Green

                System.Diagnostics.Debug.WriteLine("[ElementCaptureDialog] Capture started successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] Exception during StartCapture: {ex.Message}\n{ex.StackTrace}");
                var msgBox = new WpfMessageBox
                {
                    Title = "Error",
                    Content = $"Failed to start element capture: {ex.Message}",
                    PrimaryButtonText = "OK"
                };
                msgBox.ShowDialog();
            }
        }

        private void StopCapture()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ElementCaptureDialog] Stopping capture");
                _captureService.StopCapture();
                _isCapturing = false;

                // Update UI
                StartStopButton.Content = "Start Capture";
                StartStopButton.Icon = new SymbolIcon(SymbolRegular.Play20);
                StartStopButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                CaptureStatusText.Text = "Capture stopped. Click 'Start Capture' to resume.";
                CaptureStatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x15, 0x65, 0xC0)); // Blue

                ClearHighlight();

                System.Diagnostics.Debug.WriteLine("[ElementCaptureDialog] Capture stopped successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] Exception during StopCapture: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnElementHovered(AutomationElement element)
        {
            // Draw a red border around the hovered element
            System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] OnElementHovered called");

            // Update UI with element information in real-time
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var (type, name, automationId, className) = Services.XPathGenerator.GetElementInfo(element);

                    ControlTypeTextBlock.Text = type;
                    NameTextBlock.Text = name;
                    AutomationIdTextBlock.Text = automationId;
                    ClassNameTextBlock.Text = className;

                    var xpath = Services.XPathGenerator.GenerateXPath(element);
                    XPathTextBox.Text = xpath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] Exception updating info: {ex.Message}");
                }
            });

            UpdateHighlight(element);
        }

        private void OnElementCaptured(AutomationElement element)
        {
            // Update UI with element information
            System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] OnElementCaptured called with element: {element?.Name ?? "(no name)"}");

            Dispatcher.Invoke(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[ElementCaptureDialog] In Dispatcher.Invoke");
                    var (type, name, automationId, className) = XPathGenerator.GetElementInfo(element);
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] Element info - Type: {type}, Name: {name}");

                    ControlTypeTextBlock.Text = type;
                    NameTextBlock.Text = name;
                    AutomationIdTextBlock.Text = automationId;
                    ClassNameTextBlock.Text = className;

                    _capturedXPath = XPathGenerator.GenerateXPath(element);
                    XPathTextBox.Text = _capturedXPath;
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] XPath generated: {_capturedXPath}");

                    // Save the captured element for later use
                    CapturedXPath = _capturedXPath;

                    // Stop capture after successful capture
                    StopCapture();

                    System.Diagnostics.Debug.WriteLine("[ElementCaptureDialog] Capture stopped, element info preserved");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] Exception in OnElementCaptured: {ex}");
                    var msgBox = new WpfMessageBox
                    {
                        Title = "Error",
                        Content = $"Error processing captured element: {ex.Message}",
                        PrimaryButtonText = "OK"
                    };
                    msgBox.ShowDialog();
                }
            });
        }

        private void OnCaptureModeExited()
        {
            // Capture mode has exited
        }

        private void OnHoverCleared()
        {
            ClearHighlight();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_capturedXPath))
            {
                try
                {
                    WinForms.Clipboard.SetText(_capturedXPath);
                    var msgBox = new WpfMessageBox
                    {
                        Title = "Success",
                        Content = "XPath copied to clipboard!",
                        PrimaryButtonText = "OK"
                    };
                    msgBox.ShowDialog();
                }
                catch (Exception ex)
                {
                    var msgBox = new WpfMessageBox
                    {
                        Title = "Error",
                        Content = $"Failed to copy to clipboard: {ex.Message}",
                        PrimaryButtonText = "OK"
                    };
                    msgBox.ShowDialog();
                }
            }
            else
            {
                var msgBox = new WpfMessageBox
                {
                    Title = "Info",
                    Content = "No XPath captured yet.",
                    PrimaryButtonText = "OK"
                };
                msgBox.ShowDialog();
            }
        }

        private void UseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_capturedXPath))
            {
                CapturedXPath = _capturedXPath;
                UseInIfCondition = true;
                DialogResult = true;
                Close();
            }
            else
            {
                var msgBox = new WpfMessageBox
                {
                    Title = "Info",
                    Content = "No XPath captured yet.",
                    PrimaryButtonText = "OK"
                };
                msgBox.ShowDialog();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            ClearHighlight();

            // Clean up capture service
            if (_captureService != null)
            {
                _captureService.StopCapture();
            }

            _screenHighlighter.Dispose();
        }

        private void UpdateHighlight(AutomationElement element)
        {
            try
            {
                var bounds = Services.XPathGenerator.GetElementBounds(element);
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] Element bounds returned: {bounds}");

                if (bounds == System.Windows.Rect.Empty)
                {
                    ClearHighlight();
                    return;
                }

                var rectangle = ToScreenRectangle(bounds);
                if (rectangle.Width <= 0 || rectangle.Height <= 0)
                {
                    ClearHighlight();
                    return;
                }

                RunOnUiThread(() =>
                {
                    try
                    {
                        if (_currentHighlightRect == rectangle)
                        {
                            return;
                        }

                        _screenHighlighter.Show(rectangle, System.Drawing.Color.Red);
                        _currentHighlightRect = rectangle;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] Exception drawing highlight: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] Exception updating highlight: {ex.Message}\n{ex.StackTrace}");
                ClearHighlight();
            }
        }

        private void ClearHighlight()
        {
            RunOnUiThread(() =>
            {
                if (_currentHighlightRect.IsEmpty)
                {
                    _screenHighlighter.Hide();
                    return;
                }

                try
                {
                    _screenHighlighter.Hide();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ElementCaptureDialog] Exception clearing highlight: {ex.Message}");
                }
                finally
                {
                    _currentHighlightRect = Rectangle.Empty;
                }
            });
        }

        private static Rectangle ToScreenRectangle(System.Windows.Rect bounds)
        {
            return new Rectangle(
                (int)Math.Round(bounds.Left),
                (int)Math.Round(bounds.Top),
                (int)Math.Round(bounds.Width),
                (int)Math.Round(bounds.Height));
        }

        private void RunOnUiThread(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.Invoke(action);
            }
        }

        private sealed class ScreenHighlighter : IDisposable
        {
            private readonly int _thickness;
            private readonly int _margin;
            private OverlayRectangleForm[]? _forms;
            private System.Drawing.Color _currentColor = System.Drawing.Color.Red;

            public ScreenHighlighter(int thickness = 3, int margin = 0)
            {
                _thickness = thickness;
                _margin = margin;
            }

            public void Show(Rectangle rect, System.Drawing.Color color)
            {
                if (rect.IsEmpty)
                {
                    return;
                }

                EnsureForms(color);

                var borders = GetBorderRectangles(rect);
                for (int i = 0; i < borders.Length; i++)
                {
                    UpdateForm(_forms![i], borders[i]);
                }

                // Force all forms to be topmost and visible by re-asserting z-order
                // This helps with windows that may have complex z-order hierarchies
                foreach (var form in _forms!)
                {
                    User32.SetWindowPos(
                        form.Handle,
                        new IntPtr(-1), // HWND_TOPMOST
                        0, 0, 0, 0,
                        SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE |
                        SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_SHOWWINDOW);
                }
            }

            public void Hide()
            {
                if (_forms == null)
                {
                    return;
                }

                foreach (var form in _forms)
                {
                    form?.Hide();
                }
            }

            public void Dispose()
            {
                if (_forms == null)
                {
                    return;
                }

                foreach (var form in _forms)
                {
                    form?.Close();
                    form?.Dispose();
                }

                _forms = null;
            }

            private void EnsureForms(System.Drawing.Color color)
            {
                if (_forms != null)
                {
                    if (_currentColor != color)
                    {
                        foreach (var form in _forms)
                        {
                            if (form != null)
                            {
                                form.BackColor = color;
                            }
                        }
                        _currentColor = color;
                    }

                    return;
                }

                _forms = new[]
                {
                    CreateForm(color),
                    CreateForm(color),
                    CreateForm(color),
                    CreateForm(color)
                };

                _currentColor = color;
            }

            private static OverlayRectangleForm CreateForm(System.Drawing.Color color)
            {
                var form = new OverlayRectangleForm
                {
                    BackColor = color
                };
                form.Hide();
                return form;
            }

            private void UpdateForm(OverlayRectangleForm form, Rectangle bounds)
            {
                if (!form.Visible)
                {
                    form.Show();
                }

                // Use SWP_SHOWWINDOW and SWP_NOOWNERZORDER to ensure visibility on all window types
                // HWND_TOPMOST (-1) ensures the overlay stays on top of target windows
                User32.SetWindowPos(
                    form.Handle,
                    new IntPtr(-1), // HWND_TOPMOST
                    bounds.X,
                    bounds.Y,
                    bounds.Width,
                    bounds.Height,
                    SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_SHOWWINDOW | SetWindowPosFlags.SWP_NOOWNERZORDER);

                // Force a complete redraw to ensure visibility
                form.Invalidate();
                form.Update();
            }

            private Rectangle[] GetBorderRectangles(Rectangle rect)
            {
                var left = new Rectangle(rect.X - _margin, rect.Y - _margin, _thickness, rect.Height + 2 * _margin);
                var top = new Rectangle(rect.X - _margin, rect.Y - _margin, rect.Width + 2 * _margin, _thickness);
                var right = new Rectangle(rect.Right - _thickness + _margin, rect.Y - _margin, _thickness, rect.Height + 2 * _margin);
                var bottom = new Rectangle(rect.X - _margin, rect.Bottom - _thickness + _margin, rect.Width + 2 * _margin, _thickness);

                return new[] { left, top, right, bottom };
            }
        }
    }
}
