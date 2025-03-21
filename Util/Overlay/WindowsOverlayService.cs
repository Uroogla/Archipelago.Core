using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Archipelago.Core.Util.Overlay.Structs;
namespace Archipelago.Core.Util.Overlay
{
    public class WindowsOverlayService : IOverlayService
    {
        // Window styles
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const uint WS_POPUP = 0x80000000;
        private const int GWL_EXSTYLE = -20;

        // Layered window attributes
        private const int LWA_ALPHA = 0x2;
        private const int LWA_COLORKEY = 0x1;

        // SetWindowPos flags
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;

        // Window positions
        private static readonly IntPtr HWND_TOPMOST = new(-1);

        // Window messages
        private const int WM_PAINT = 0x000F;
        private const int WM_ERASEBKGND = 0x0014;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
            IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
        ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
        uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        private IntPtr _overlayWindow;
        private IntPtr _targetWindow;
        private Thread _renderThread;
        private volatile bool _isRunning;
        private readonly List<TextPopup> _popups = [];
        private readonly object _popupsLock = new();
        private readonly string _className;

        public WindowsOverlayService()
        {
            _className = "OverlayWindowClass_" + Guid.NewGuid().ToString("N");
            RegisterWindowClass();
        }
        private void RegisterWindowClass()
        {
            var wndClass = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate<WndProcDelegate>(WndProc),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = GetModuleHandle(null),
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = _className,
                hIconSm = IntPtr.Zero
            };

            RegisterClassEx(ref wndClass);
        }
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            return msg switch
            {
                WM_PAINT => IntPtr.Zero,// Handled by our render thread
                WM_ERASEBKGND => new IntPtr(1),// Prevent erasing to reduce flicker
                WM_NCHITTEST => new IntPtr(HTTRANSPARENT),// Make the window click-through
                _ => DefWindowProc(hWnd, msg, wParam, lParam),
            };
        }
        public bool AttachToWindow(IntPtr targetWindowHandle)
        {
            if (targetWindowHandle == IntPtr.Zero)
                return false;

            _targetWindow = targetWindowHandle;

            // Get target window dimensions
            if (!GetWindowRect(_targetWindow, out RECT rect))
                return false;

            // Create overlay window
            _overlayWindow = CreateWindowEx(
                WS_EX_LAYERED | WS_EX_TRANSPARENT,
                _className,
                "Overlay Window",
                WS_POPUP,
                rect.Left, rect.Top,
                rect.Width, rect.Height,
                IntPtr.Zero,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero);

            if (_overlayWindow == IntPtr.Zero)
                return false;

            // Make window topmost
            SetWindowPos(_overlayWindow, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            return true;
        }

        public void Show()
        {
            if (_overlayWindow == IntPtr.Zero)
                return;

            // Make window visible
            SetWindowPos(_overlayWindow, IntPtr.Zero, 0, 0, 0, 0,
                SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            _isRunning = true;
            _renderThread = new Thread(RenderLoop)
            {
                IsBackground = true
            };
            _renderThread.Start();
        }
        public void Hide()
        {
            _isRunning = false;

            if (_renderThread != null && _renderThread.IsAlive)
            {
                _renderThread.Join(1000);
            }

            // Hide window
            if (_overlayWindow != IntPtr.Zero)
            {
                SetWindowPos(_overlayWindow, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }

        public void AddTextPopup(string text, int x, int y, Color textColor, double durationSeconds = 3.0, int fontSize = 14)
        {
            lock (_popupsLock)
            {
                _popups.Add(new TextPopup
                {
                    Text = text,
                    Position = new Point(x, y),
                    TextColor = textColor,
                    Font = new Font("Arial", fontSize),
                    ExpirationTime = DateTime.Now.AddSeconds(durationSeconds),
                    BackgroundColor = Color.FromArgb(180, 0, 0, 0),
                    Padding = 8
                });
            }
        }

        private void RenderLoop()
        {
            while (_isRunning)
            {
                try
                {
                    UpdateOverlayPosition();
                    RenderOverlay();
                    Thread.Sleep(16); // ~60 FPS
                }
                catch
                {
                    // Handle exceptions
                    Thread.Sleep(100);
                }
            }
        }

        private void UpdateOverlayPosition()
        {
            if (!GetWindowRect(_targetWindow, out RECT rect))
                return;

            SetWindowPos(_overlayWindow, HWND_TOPMOST,
                rect.Left, rect.Top, rect.Width, rect.Height,
                SWP_NOACTIVATE);
        }

        private void RenderOverlay()
        {
            if (_overlayWindow == IntPtr.Zero || !GetWindowRect(_overlayWindow, out RECT rect))
                return;

            int width = rect.Width;
            int height = rect.Height;

            // Get device context
            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(screenDC);
            IntPtr hBitmap = CreateCompatibleBitmap(screenDC, width, height);
            IntPtr oldBitmap = SelectObject(memDC, hBitmap);

            try
            {
                using (Graphics g = Graphics.FromHdc(memDC))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    // Remove expired popups
                    lock (_popupsLock)
                    {
                        _popups.RemoveAll(p => p.IsExpired);

                        // Draw popups
                        foreach (var popup in _popups)
                        {
                            // Measure text
                            SizeF textSize = g.MeasureString(popup.Text, popup.Font);

                            // Draw background
                            int padding = popup.Padding;
                            using (SolidBrush backBrush = new(popup.BackgroundColor))
                            {
                                g.FillRoundedRectangle(
                                    backBrush,
                                    popup.Position.X,
                                    popup.Position.Y,
                                    textSize.Width + (padding * 2),
                                    textSize.Height + (padding * 2),
                                    8);
                            }

                            // Draw text
                            using SolidBrush textBrush = new(popup.TextColor);
                            g.DrawString(popup.Text,
                                         popup.Font,
                                         textBrush,
                                         popup.Position.X + padding,
                                         popup.Position.Y + padding);
                        }
                    }
                }

                // Update layered window
                POINT sourceLocation = new(0, 0);
                POINT destLocation = new(rect.Left, rect.Top);
                SIZE size = new(width, height);

                BLENDFUNCTION blend = new()
                {
                    BlendOp = 0, // AC_SRC_OVER
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = 1 // AC_SRC_ALPHA
                };

                UpdateLayeredWindow(
                    _overlayWindow,
                    screenDC,
                    ref destLocation,
                    ref size,
                    memDC,
                    ref sourceLocation,
                    0,
                    ref blend,
                    2); // ULW_ALPHA
            }
            finally
            {
                // Clean up
                SelectObject(memDC, oldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(memDC);
                ReleaseDC(IntPtr.Zero, screenDC);
            }
        }

        public void Dispose()
        {
            Hide();

            if (_overlayWindow != IntPtr.Zero)
            {
                DestroyWindow(_overlayWindow);
                _overlayWindow = IntPtr.Zero;
            }
        }
    }
}
