using System;
using System.Drawing;

namespace Bounce.Simulation
{
    /// <summary>
    /// A single bouncing body. Collision is tested against a
    /// <see cref="MonitorRegion"/> rather than a plain rectangle, so it
    /// reflects correctly off real screen edges - including the inside
    /// ("concave") corner formed where two monitors of different
    /// resolution or offset meet.
    /// </summary>
    internal sealed class BouncingBall
    {
        // How fast FlashIntensity decays back to 0 after a corner hit - at
        // this rate a flash fades out over about 20 ticks (~300ms at the
        // screensaver's 15ms tick interval).
        private const float FlashDecayPerTick = 1f / 20f;

        private readonly MonitorRegion _region;

        public PointF Position; // top-left of the bounding box
        public PointF Velocity;
        public float Size { get; }

        /// <summary>
        /// 1 immediately after a true corner hit (both axes reflected at
        /// once), decaying to 0. Always tracked regardless of whether
        /// anything renders it - rendering it as a flash is opt-in
        /// (<see cref="Settings.CornerFlash"/>) at the drawing layer.
        /// </summary>
        public float FlashIntensity { get; private set; }

        public BouncingBall(MonitorRegion region, PointF startPosition, PointF startVelocity, float size)
        {
            _region = region;
            Position = startPosition;
            Velocity = startVelocity;
            Size = size;
        }

        public RectangleF Bounds => new RectangleF(Position.X, Position.Y, Size, Size);

        public void Step()
        {
            if (FlashIntensity > 0f)
            {
                FlashIntensity = Math.Max(0f, FlashIntensity - FlashDecayPerTick);
            }

            float nx = Position.X + Velocity.X;
            float ny = Position.Y + Velocity.Y;

            bool xOk = _region.Contains(new RectangleF(nx, Position.Y, Size, Size));
            bool yOk = _region.Contains(new RectangleF(Position.X, ny, Size, Size));
            bool bothOk = _region.Contains(new RectangleF(nx, ny, Size, Size));

            if (xOk && yOk && bothOk)
            {
                Position = new PointF(nx, ny);
                return;
            }

            if (xOk && !yOk)
            {
                // Free to slide horizontally; the vertical step hit a screen edge.
                Position = new PointF(nx, Position.Y);
                Velocity = new PointF(Velocity.X, -Velocity.Y);
                return;
            }

            if (yOk && !xOk)
            {
                // Free to slide vertically; the horizontal step hit a screen edge.
                Position = new PointF(Position.X, ny);
                Velocity = new PointF(-Velocity.X, Velocity.Y);
                return;
            }

            if (xOk && yOk)
            {
                // Both individual axis moves are fine, but the diagonal step
                // would cut across a concave corner - e.g. the notch where
                // two differently sized/offset monitors meet, where no
                // physical screen exists in that diagonal sliver. Slide
                // along whichever axis has the larger displacement and
                // bounce the other, like sliding along a wall.
                if (Math.Abs(Velocity.X) >= Math.Abs(Velocity.Y))
                {
                    Position = new PointF(nx, Position.Y);
                    Velocity = new PointF(Velocity.X, -Velocity.Y);
                }
                else
                {
                    Position = new PointF(Position.X, ny);
                    Velocity = new PointF(-Velocity.X, Velocity.Y);
                }
                return;
            }

            // Neither axis alone works either (a corner hit dead-on) - reflect both.
            Velocity = new PointF(-Velocity.X, -Velocity.Y);
            FlashIntensity = 1f;
        }
    }
}
