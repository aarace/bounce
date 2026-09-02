using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Bounce.Simulation;

namespace Bounce
{
    /// <summary>
    /// One borderless, topmost window per physical monitor, sized and
    /// positioned to exactly match that monitor's own bounds.
    ///
    /// Windows applies a single DPI scale factor to a top-level window,
    /// based on whichever monitor it's on. A window spanning monitors that
    /// have different DPI/scaling gets the portions outside its "home"
    /// monitor bitmap-stretched by the OS rather than drawn natively, which
    /// is what caused the squashed ball and the wrongly-sized black area.
    /// One native window per monitor avoids that entirely.
    /// </summary>
    internal sealed class MonitorWindow : Form
    {
        private const int WM_DPICHANGED = 0x02E0;

        private readonly Rectangle _screenBounds;
        private readonly IReadOnlyList<BouncingBall> _balls;
        private readonly float _ballSize;
        private readonly Image _ballImage;
        private readonly SolidBrush _ballBrush;
        private readonly Color _ballColor;
        private readonly bool _cornerFlashEnabled;
        private readonly BallTrail _trail;

        // Persistent per-monitor canvas for a "forever" trail: each new
        // segment is painted onto it once (DrawTrailSegmentToLayer, called
        // from ScreensaverSession as segments are recorded) rather than
        // every historical segment being redrawn from scratch every frame,
        // which is what made a long-running forever trail get progressively
        // choppier. Stays null (and OnPaint falls back to redrawing
        // BallTrail.GetSegments() each frame) for a finite/fading trail,
        // whose segment count is naturally bounded by its max age anyway.
        private Bitmap _trailLayer;
        private Graphics _trailLayerGraphics;

        public MonitorWindow(
            Rectangle screenBounds,
            IReadOnlyList<BouncingBall> balls,
            float ballSize,
            Image ballImage,
            SolidBrush ballBrush,
            Color ballColor,
            bool cornerFlashEnabled,
            BallTrail trail)
        {
            _screenBounds = screenBounds;
            _balls = balls;
            _ballSize = ballSize;
            _ballImage = ballImage;
            _ballBrush = ballBrush;
            _ballColor = ballColor;
            _cornerFlashEnabled = cornerFlashEnabled;
            _trail = trail;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.Black;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            // Without this, WinForms' default font-based auto-scaling can
            // quietly rescale the exact pixel Bounds set below when the
            // window is created on a non-96-DPI monitor, leaving a sliver
            // of the monitor uncovered. Bounds is already exact, real
            // per-monitor pixel geometry (from Screen.Bounds) - nothing
            // should rescale it.
            AutoScaleMode = AutoScaleMode.None;
            Bounds = screenBounds;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // The window's real per-monitor DPI context is only established
            // once the handle exists. Re-assert our exact bounds afterward
            // in case anything nudged them during creation.
            Bounds = _screenBounds;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DPICHANGED)
            {
                // We lay every window out ourselves in real per-monitor
                // pixel coordinates (from Screen.Bounds), which is already
                // correct for whatever DPI that monitor is running. Ignore
                // the OS's suggested rescaled rect - on a mixed-DPI setup
                // (e.g. one monitor at 175%, others at 100%) accepting it
                // is what left this window mis-sized/mis-positioned.
                Bounds = _screenBounds;
                return;
            }

            base.WndProc(ref m);
        }

        /// <summary>Paints one already-recorded "forever" trail segment onto this monitor's persistent layer. See <see cref="_trailLayer"/>.</summary>
        public void DrawTrailSegmentToLayer(PointF from, PointF to, float alpha)
        {
            if (_trailLayer == null)
            {
                _trailLayer = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height), PixelFormat.Format32bppArgb);
                _trailLayerGraphics = Graphics.FromImage(_trailLayer);
            }

            var localFrom = new PointF(from.X - _screenBounds.X, from.Y - _screenBounds.Y);
            var localTo = new PointF(to.X - _screenBounds.X, to.Y - _screenBounds.Y);
            float trailWidth = Math.Max(2f, _ballSize * 0.35f);

            TrailRenderer.DrawSegment(_trailLayerGraphics, localFrom, localTo, alpha, _ballColor, ClientRectangle, trailWidth);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Always fully clear-and-redraw. Each window only covers one
            // monitor's worth of pixels, so this is cheap, and it avoids the
            // partial-invalidate edge cases that produced a clipped ball.
            e.Graphics.Clear(Color.Black);

            if (_trailLayer != null)
            {
                e.Graphics.DrawImageUnscaled(_trailLayer, Point.Empty);
            }
            else if (_trail != null)
            {
                var offset = new PointF(_screenBounds.X, _screenBounds.Y);
                float trailWidth = Math.Max(2f, _ballSize * 0.35f);
                TrailRenderer.Draw(e.Graphics, _trail.GetSegments(), offset, ClientRectangle, _ballColor, trailWidth);
            }

            foreach (var ball in _balls)
            {
                var relative = new RectangleF(
                    ball.Bounds.X - _screenBounds.X,
                    ball.Bounds.Y - _screenBounds.Y,
                    ball.Bounds.Width,
                    ball.Bounds.Height);

                if (!relative.IntersectsWith(ClientRectangle))
                {
                    continue;
                }

                if (_ballImage != null)
                {
                    // Tinting an arbitrary image for the flash effect would
                    // need a whole ImageAttributes/ColorMatrix pass; a logo
                    // just always draws as-is.
                    BallRenderer.Draw(e.Graphics, relative, _ballImage, _ballBrush);
                    continue;
                }

                if (_cornerFlashEnabled && ball.FlashIntensity > 0f)
                {
                    _ballBrush.Color = Lerp(_ballColor, Color.White, ball.FlashIntensity);
                    BallRenderer.Draw(e.Graphics, relative, null, _ballBrush);
                    _ballBrush.Color = _ballColor;
                }
                else
                {
                    BallRenderer.Draw(e.Graphics, relative, null, _ballBrush);
                }
            }
        }

        private static Color Lerp(Color from, Color to, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                from.A + (int)((to.A - from.A) * t),
                from.R + (int)((to.R - from.R) * t),
                from.G + (int)((to.G - from.G) * t),
                from.B + (int)((to.B - from.B) * t));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _trailLayerGraphics?.Dispose();
                _trailLayer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
