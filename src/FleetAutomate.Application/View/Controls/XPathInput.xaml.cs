using System;
using System.Drawing;
using System.Windows;
using FleetAutomate.Services;
using Wpf.Ui.Controls;
using FlaUI.Core.AutomationElements;

namespace FleetAutomate.View.Controls
{
    /// <summary>
    /// Reusable XPath input control with element capture functionality.
    /// </summary>
    public partial class XPathInput : System.Windows.Controls.UserControl
    {
        private ElementCaptureService? _captureService;
        private bool _isCapturing = false;
        private Rectangle _currentHighlightRect = Rectangle.Empty;
        private readonly ScreenHighlighter _screenHighlighter = new ScreenHighlighter();

        /// <summary>
        /// Dependency property for XPath value.
        /// </summary>
        public static readonly DependencyProperty XPathProperty =
            DependencyProperty.Register(
                nameof(XPath),
                typeof(string),
                typeof(XPathInput),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnXPathChanged));

        /// <summary>
        /// Gets or sets the XPath value.
        /// </summary>
        public string XPath
        {
            get => (string)GetValue(XPathProperty);
            set => SetValue(XPathProperty, value);
        }

        private static void OnXPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // XPath was changed externally, update TextBox if needed
            var control = (XPathInput)d;
            if (control.XPathTextBox.Text != (string)e.NewValue)
            {
                control.XPathTextBox.Text = (string)e.NewValue;
            }
        }

        public XPathInput()
        {
            InitializeComponent();

            // Subscribe to Unloaded event to clean up resources
            Unloaded += XPathInput_Unloaded;
        }

        private void XPathInput_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop capture and clean up service
            if (_isCapturing)
            {
                StopCapture();
            }

            if (_captureService != null)
            {
                _captureService.OnElementCaptured -= OnElementCaptured;
                _captureService.OnElementHovered -= OnElementHovered;
                _captureService.OnHoverCleared -= OnHoverCleared;
                _captureService = null;
            }

            // Clean up highlighter
            _screenHighlighter?.Dispose();
        }

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
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
                // Initialize service if needed
                if (_captureService == null)
                {
                    _captureService = new ElementCaptureService();
                    _captureService.OnElementCaptured += OnElementCaptured;
                    _captureService.OnElementHovered += OnElementHovered;
                    _captureService.OnHoverCleared += OnHoverCleared;
                }

                _captureService.StartCapture();
                _isCapturing = true;

                // Update button appearance
                CaptureButton.Content = "Stop Capturing";
                CaptureButton.Icon = new SymbolIcon(SymbolRegular.Stop20);
                CaptureButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Danger;

                System.Diagnostics.Debug.WriteLine("[XPathInput] Capture started");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[XPathInput] Error starting capture: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to start element capture: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void StopCapture()
        {
            try
            {
                if (_captureService != null)
                {
                    _captureService.StopCapture();
                }

                _isCapturing = false;

                // Clear highlight
                ClearHighlight();

                // Restore button appearance
                CaptureButton.Content = "Capture Element";
                CaptureButton.Icon = new SymbolIcon(SymbolRegular.Target20);
                CaptureButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;

                System.Diagnostics.Debug.WriteLine("[XPathInput] Capture stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[XPathInput] Error stopping capture: {ex.Message}");
            }
        }

        private void OnElementCaptured(AutomationElement element)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Generate XPath for the captured element
                    string xpath = XPathGenerator.GenerateXPath(element);

                    // Set the XPath value (will update both property and TextBox)
                    XPath = xpath;

                    // Stop capture after successful capture
                    StopCapture();

                    System.Diagnostics.Debug.WriteLine($"[XPathInput] Element captured, XPath: {xpath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[XPathInput] Error processing captured element: {ex.Message}");
                    System.Windows.MessageBox.Show($"Error processing captured element: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            });
        }

        private void OnElementHovered(AutomationElement element)
        {
            // Draw a red border around the hovered element
            System.Diagnostics.Debug.WriteLine($"[XPathInput] OnElementHovered called");

            // Update highlight in real-time
            Dispatcher.Invoke(() =>
            {
                try
                {
                    UpdateHighlight(element);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[XPathInput] Exception updating highlight: {ex.Message}");
                }
            });
        }

        private void OnHoverCleared()
        {
            // Clear the highlight when hover is cleared
            Dispatcher.Invoke(() =>
            {
                ClearHighlight();
            });
        }

        private void UpdateHighlight(AutomationElement element)
        {
            try
            {
                var bounds = XPathGenerator.GetElementBounds(element);
                System.Diagnostics.Debug.WriteLine($"[XPathInput] Element bounds returned: {bounds}");

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
                        System.Diagnostics.Debug.WriteLine($"[XPathInput] Exception drawing highlight: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[XPathInput] Error in UpdateHighlight: {ex.Message}");
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
                    _currentHighlightRect = Rectangle.Empty;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[XPathInput] Error in ClearHighlight: {ex.Message}");
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
    }
}
