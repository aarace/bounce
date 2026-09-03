using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Bounce.Simulation;

namespace Bounce
{
    /// <summary>
    /// Owns the whole running screensaver: the shared simulation (one or
    /// more balls, per <see cref="Settings.BallCount"/>), one
    /// <see cref="MonitorWindow"/> per physical monitor, the animation
    /// timer, and exit-on-input handling shared across all of them.
    /// </summary>
    internal sealed class ScreensaverSession : IDisposable
    {
        private readonly Timer _timer = new Timer();
        private readonly MonitorRegion _region;
        private readonly List<BouncingBall> _balls = new List<BouncingBall>();
        private readonly List<MonitorWindow> _windows = new List<MonitorWindow>();
        private readonly Image _ballImage;
        private readonly SolidBrush _ballBrush;
        private readonly Color _ballColor;
        private readonly BallTrail _trail;

        private Point? _startMousePosition;
        private bool _exited;

        public ScreensaverSession()
        {
            var screenBounds = Array.ConvertAll(Screen.AllScreens, s => s.Bounds);
            var overallBounds = SystemInformation.VirtualScreen;
            _region = new MonitorRegion(screenBounds, overallBounds);

            var settings = Settings.Load();
            _ballColor = settings.BallColor;
            _ballBrush = new SolidBrush(settings.BallColor);
            if (!string.IsNullOrWhiteSpace(settings.ImagePath) && File.Exists(settings.ImagePath))
            {
                try { _ballImage = Image.FromFile(settings.ImagePath); }
                catch { _ballImage = null; }
            }

            if (settings.ShowTrail)
            {
                _trail = new BallTrail
                {
                    MaxAgeSeconds = settings.TrailMaxAgeSeconds == Settings.ForeverTrailAge
                        ? (double?)null
                        : settings.TrailMaxAgeSeconds
                };
            }

            int ballCount = Math.Max(1, Math.Min(Settings.MaxBallCount, settings.BallCount));
            var random = new Random();
            for (int i = 0; i < ballCount; i++)
            {
                _balls.Add(CreateBall(_region, settings, random));
            }

            foreach (var bounds in screenBounds)
            {
                var window = new MonitorWindow(bounds, _balls, settings.BallSize, _ballImage, _ballBrush, _ballColor, settings.CornerFlash, _trail);
                window.KeyDown += (s, e) => RequestExit();
                window.MouseClick += (s, e) => RequestExit();
                window.MouseMove += (s, e) => HandleMouseMove();
                _windows.Add(window);
            }

            _timer.Interval = 15;
            _timer.Tick += OnTick;
        }

        private static BouncingBall CreateBall(MonitorRegion region, Settings settings, Random random)
        {
            // Spread multiple balls across different monitors to start,
            // rather than all stacked at the same spot.
            var screens = Screen.AllScreens;
            var spawnScreen = screens.Length > 0 ? screens[random.Next(screens.Length)].Bounds : region.Bounds;

            float size = settings.BallSize;
            var start = new PointF(
                spawnScreen.Left + spawnScreen.Width / 2f - size / 2f,
                spawnScreen.Top + spawnScreen.Height / 2f - size / 2f);

            // 15-75 degrees off horizontal in a random quadrant, so it never
            // starts out moving in a perfectly flat/vertical (boring) line.
            double angle = (random.NextDouble() * 60 + 15) * Math.PI / 180.0;
            float speed = Math.Max(settings.Speed, 1);
            float vx = (float)(speed * Math.Cos(angle)) * (random.Next(2) == 0 ? 1 : -1);
            float vy = (float)(speed * Math.Sin(angle)) * (random.Next(2) == 0 ? 1 : -1);

            return new BouncingBall(region, start, new PointF(vx, vy), size);
        }

        public void Start()
        {
            Cursor.Hide();
            foreach (var window in _windows)
            {
                window.Show();
            }
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            for (int i = 0; i < _balls.Count; i++)
            {
                var ball = _balls[i];
                ball.Step();

                if (_trail != null)
                {
                    var center = new PointF(
                        ball.Bounds.X + ball.Bounds.Width / 2f,
                        ball.Bounds.Y + ball.Bounds.Height / 2f);
                    _trail.Record(i, center);
                }
            }

            if (_trail != null)
            {
                // Once per tick, after every ball above has had its chance
                // to Record() a point for this tick - not once per ball.
                _trail.RefreshSegments();

                var pending = _trail.GetAndClearPendingForeverSegments();
                if (pending.Count > 0)
                {
                    foreach (var segment in pending)
                    {
                        foreach (var window in _windows)
                        {
                            window.DrawTrailSegmentToLayer(segment.From, segment.To, segment.Alpha);
                        }
                    }
                }
            }

            foreach (var window in _windows)
            {
                window.Invalidate();
            }
        }

        private void HandleMouseMove()
        {
            if (_startMousePosition == null)
            {
                _startMousePosition = Cursor.Position;
                return;
            }

            int dx = Cursor.Position.X - _startMousePosition.Value.X;
            int dy = Cursor.Position.Y - _startMousePosition.Value.Y;
            if (Math.Abs(dx) > 8 || Math.Abs(dy) > 8)
            {
                RequestExit();
            }
        }

        private void RequestExit()
        {
            if (_exited)
            {
                return;
            }
            _exited = true;
            Application.Exit();
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            foreach (var window in _windows)
            {
                window.Dispose();
            }
            _region.Dispose();
            _ballBrush.Dispose();
            _ballImage?.Dispose();
            Cursor.Show();
        }
    }
}
