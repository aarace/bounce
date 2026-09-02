using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Bounce.Simulation;

namespace Bounce
{
    /// <summary>
    /// Renders into the small thumbnail Windows shows in the Screensaver
    /// Settings dialog ("/p &lt;hwnd&gt;"). Shows a single representative
    /// screen - the thumbnail is too small to usefully depict real
    /// multi-monitor geometry.
    /// </summary>
    public sealed class PreviewForm : Form
    {
        private const int WS_CHILD = 0x40000000;
        private const int WS_POPUP = unchecked((int)0x80000000);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        private readonly IntPtr _hostHandle;
        private readonly Timer _timer = new Timer();
        private readonly MonitorRegion _region;
        private readonly BouncingBall _ball;
        private readonly Image _ballImage;
        private readonly Brush _ballBrush;

        public PreviewForm(IntPtr hostHandle)
        {
            _hostHandle = hostHandle;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AutoScaleMode = AutoScaleMode.None;

            RECT rect;
            GetClientRect(_hostHandle, out rect);
            int width = Math.Max(rect.Right - rect.Left, 1);
            int height = Math.Max(rect.Bottom - rect.Top, 1);
            var bounds = new Rectangle(0, 0, width, height);
            Bounds = bounds;

            _region = new MonitorRegion(new[] { bounds }, bounds);

            var settings = Settings.Load();
            _ballBrush = new SolidBrush(settings.BallColor);
            if (!string.IsNullOrWhiteSpace(settings.ImagePath) && File.Exists(settings.ImagePath))
            {
                try { _ballImage = Image.FromFile(settings.ImagePath); }
                catch { _ballImage = null; }
            }

            var random = new Random();
            float size = Math.Min(settings.BallSize, Math.Min(width, height));
            var start = new PointF(width / 2f - size / 2f, height / 2f - size / 2f);
            double angle = (random.NextDouble() * 60 + 15) * Math.PI / 180.0;
            float speed = Math.Max(settings.Speed, 1);
            float vx = (float)(speed * Math.Cos(angle)) * (random.Next(2) == 0 ? 1 : -1);
            float vy = (float)(speed * Math.Sin(angle)) * (random.Next(2) == 0 ? 1 : -1);
            _ball = new BouncingBall(_region, start, new PointF(vx, vy), size);

            _timer.Interval = 15;
            _timer.Tick += (s, e) => { _ball.Step(); Invalidate(); };
            _timer.Start();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // Born as a child of the host window (rather than a
                // top-level popup reparented after the fact) so the
                // preview never flickers a separate window into view first.
                cp.Style = (cp.Style & ~WS_POPUP) | WS_CHILD;
                cp.Parent = _hostHandle;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Black);
            BallRenderer.Draw(e.Graphics, _ball.Bounds, _ballImage, _ballBrush);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            _region.Dispose();
            _ballBrush.Dispose();
            _ballImage?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
