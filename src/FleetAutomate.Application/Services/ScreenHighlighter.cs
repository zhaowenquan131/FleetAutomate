using System;
using System.Drawing;
using FlaUI.Core.Overlay;
using FlaUI.Core.WindowsAPI;

namespace FleetAutomate.Services
{
    /// <summary>
    /// Helper class to highlight UI elements on screen with a colored border.
    /// </summary>
    public sealed class ScreenHighlighter : IDisposable
    {
        private readonly int _thickness;
        private readonly int _margin;
        private OverlayRectangleForm[]? _forms;
        private Color _currentColor = Color.Red;

        public ScreenHighlighter(int thickness = 3, int margin = 0)
        {
            _thickness = thickness;
            _margin = margin;
        }

        public void Show(Rectangle rect, Color color)
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

        private void EnsureForms(Color color)
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

        private static OverlayRectangleForm CreateForm(Color color)
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
