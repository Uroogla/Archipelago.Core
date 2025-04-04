using GameOverlay.Drawing;
using GameOverlay.Windows;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.Overlay
{
    public class WindowsOverlayService : IOverlayService
    {
        private readonly StickyWindow _window;
        private readonly Dictionary<string, SolidBrush> _brushes = new Dictionary<string, SolidBrush>();
        private readonly Dictionary<float, Font> _fonts = new Dictionary<float, Font>();
        private readonly ConcurrentDictionary<Guid, TextPopup> _popups = new ConcurrentDictionary<Guid, TextPopup>();

        private Graphics _gfx;
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        private IntPtr _targetWindowHandle;

        public WindowsOverlayService()
        {
            // Create overlay window (initially hidden)
            _window = new StickyWindow(0, 0, 800, 600);

            // Configure window properties
            _window.IsTopmost = true;
            _window.IsVisible = false;
            _window.FPS = 30; // Update frequency

            // Setup rendering callback
            _window.DrawGraphics += OnDrawGraphics;
            _window.SetupGraphics += OnSetupGraphics;
            _window.DestroyGraphics += OnDestroyGraphics;

            // Create the window (but keep it hidden)
            _window.Create();
        }

        public bool AttachToWindow(IntPtr targetWindowHandle)
        {
            if (targetWindowHandle == IntPtr.Zero)
                return false;

            _targetWindowHandle = targetWindowHandle;
            _window.PlaceAbove(targetWindowHandle);
            return true;
        }

        public void Show()
        {
            if (_isDisposed) return;
            _window.Show();
        }

        public void Hide()
        {
            if (_isDisposed) return;
            _window.Hide();
        }

        public void AddTextPopup(string text, IColor textColor, double durationSeconds = 3.0, float fontSize = 14)
        {
            if (_isDisposed) return;

            // Create a unique ID for this popup
            var id = Guid.NewGuid();

            // Generate color from IColor interface
            var color = new GameOverlay.Drawing.Color(
                textColor.R,
                textColor.G,
                textColor.B,
                textColor.A
            );

            // Check if we need to create a font of this size
            if (!_fonts.ContainsKey(fontSize))
            {
                if (_isInitialized)
                {
                    _fonts[fontSize] = _gfx.CreateFont("Arial", fontSize);
                }
                else
                {
                    // We'll delay creation until graphics are initialized
                }
            }

            // Create a brush key for this color
            string brushKey = $"{color.R}:{color.G}:{color.B}:{color.A}";
            if (!_brushes.ContainsKey(brushKey))
            {
                if (_isInitialized)
                {
                    _brushes[brushKey] = _gfx.CreateSolidBrush(color);
                }
                else
                {
                    // We'll delay creation until graphics are initialized
                }
            }

            // Create the popup
            var popup = new TextPopup
            {
                Text = text,
                ExpireTime = DateTime.Now.AddSeconds(durationSeconds),
                Opacity = 1.0f
            };

            // Add to active popups
            _popups[id] = popup;

            // Set up fade out and removal
            double fadeStartTime = durationSeconds * 0.75; // Start fading after 75% of duration

            // Start fade after delay
            Task.Delay(TimeSpan.FromMilliseconds(fadeStartTime * 1000))
                .ContinueWith(_ =>
                {
                    // Calculate how long to fade
                    double fadeTime = durationSeconds - fadeStartTime;

                    // Fade steps - update 20 times during fade
                    int steps = 20;
                    double stepTime = fadeTime / steps;
                    float stepOpacity = 1.0f / steps;

                    for (int i = 1; i <= steps; i++)
                    {
                        Task.Delay(TimeSpan.FromMilliseconds(i * stepTime * 1000))
                            .ContinueWith(__ =>
                            {
                                if (_isDisposed) return;

                                if (_popups.TryGetValue(id, out var p))
                                {
                                    p.Opacity = Math.Max(0, 1.0f - (i * stepOpacity));
                                }
                            });
                    }
                });

            // Remove after expiration
            Task.Delay(TimeSpan.FromMilliseconds(durationSeconds * 1000))
                .ContinueWith(_ =>
                {
                    if (_isDisposed) return;
                    _popups.TryRemove(id, out var ignore);
                });
        }

        private void OnSetupGraphics(object sender, SetupGraphicsEventArgs e)
        {
            _gfx = e.Graphics;

            // Initialize resources once we have graphics
            if (!_isInitialized)
            {
                _isInitialized = true;
                InitializeResources();
            }
        }

        private void InitializeResources()
        {
            // Clear existing resources (if any)
            foreach (var brush in _brushes.Values)
            {
                brush.Dispose();
            }
            _brushes.Clear();

            foreach (var font in _fonts.Values)
            {
                font.Dispose();
            }
            _fonts.Clear();

            // Create default font for initial popups
            _fonts[14] = _gfx.CreateFont("Arial", 14);
        }

        private void OnDrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            // Update window position in case target window moved
            if (_targetWindowHandle != IntPtr.Zero)
            {
                try
                {
                    GameOverlay.Windows.WindowHelper.GetWindowBounds(_targetWindowHandle, out var rect);
                    _window.X = rect.Left;
                    _window.Y = rect.Top;
                    _window.Width = rect.Right - rect.Left;
                    _window.Height = rect.Bottom - rect.Top;
                }
                catch { /* Ignore errors if window is closed */ }
            }

            // Clear the scene with transparency
            e.Graphics.ClearScene();

            // Get the current time once for all popups
            var now = DateTime.Now;
            var index = 0;
            // Draw active popups
            foreach (var popup in _popups.OrderByDescending(x => x.Value.ExpireTime).ToDictionary<Guid, TextPopup>().Values)
            {
                if (popup.ExpireTime < now)
                    continue;
                // Get or create font
                if (!_fonts.TryGetValue(popup.Font?.FontSize ?? 14.0f, out var font))
                {
                    font = _gfx.CreateFont("Arial", popup.Font?.FontSize ?? 14.0f);
                    _fonts[popup.Font?.FontSize ?? 14.0f] = font;
                }
                popup.Font = font;

                // Create a brush key based on popup color
                string brushKey = "1:1:1:1"; // Default white

                if (popup.Brush != null)
                {
                    var color = popup.Brush.Color;
                    brushKey = $"{color.R}:{color.G}:{color.B}:{color.A}";
                }

                // Get or create brush (adjust for opacity)
                if (!_brushes.TryGetValue(brushKey, out var brush))
                {
                    // Default to white if we don't have a brush
                    brush = _gfx.CreateSolidBrush(new GameOverlay.Drawing.Color(1, 1, 1, popup.Opacity));
                    _brushes[brushKey] = brush;
                }
                popup.Brush = brush;

                // Draw the text with current opacity
                GameOverlay.Drawing.Color originalColor = popup.Brush.Color;
                //popup.Brush.Color = new GameOverlay.Drawing.Color(
                //    originalColor.R,
                //    originalColor.G,
                //    originalColor.B,
                //    originalColor.A * popup.Opacity
                //);
                popup.Brush.Color = new GameOverlay.Drawing.Color(1f,1f,1f,1f);
                e.Graphics.DrawText(popup.Font, popup.Brush, 100, 100 - (index * (popup.Font.FontSize + 3)), popup.Text);

                // Restore original color
                popup.Brush.Color = originalColor;
                index++;
            }
        }

        private void OnDestroyGraphics(object sender, DestroyGraphicsEventArgs e)
        {
            foreach (var brush in _brushes.Values)
            {
                brush.Dispose();
            }
            _brushes.Clear();

            foreach (var font in _fonts.Values)
            {
                font.Dispose();
            }
            _fonts.Clear();

            _isInitialized = false;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _popups.Clear();
            _window.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
