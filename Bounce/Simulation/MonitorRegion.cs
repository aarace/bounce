using System;
using System.Collections.Generic;
using System.Drawing;

namespace Bounce.Simulation
{
    /// <summary>
    /// The "walkable" area for the bounce simulation, built from the real
    /// geometry of every attached monitor (as arranged in System &gt; Display,
    /// which only makes sense with monitors set to "Extend"). This is what
    /// lets the ball bounce correctly off the seam where two monitors of
    /// different resolution/position meet, not just off one big bounding
    /// rectangle.
    /// </summary>
    internal sealed class MonitorRegion : IDisposable
    {
        // Two monitors that Display Settings shows snapped edge-to-edge can
        // still end up with a real gap between their physical-pixel bounds
        // - that arrangement UI works in DPI-normalized units, not raw
        // pixels, and dragging isn't pixel-perfect either. Without
        // compensating, the ball correctly (but unhelpfully) treats that
        // gap as a real wall and never crosses. Rather than guess a fixed
        // tolerance, this measures the actual gap between every pair of
        // monitors that face each other and bridges exactly that much -
        // anything up to this sanity cap, so two monitors that are
        // deliberately far apart don't get bridged.
        private const int MaxBridgeableGap = 150;

        private readonly Region _region;
        private readonly Bitmap _measurementSurface;
        private readonly Graphics _measurementGraphics;

        public Rectangle Bounds { get; }

        public MonitorRegion(Rectangle[] screenBounds, Rectangle overallBounds)
        {
            Bounds = overallBounds;

            _region = new Region();
            _region.MakeEmpty();
            foreach (var rect in screenBounds)
            {
                _region.Union(rect);
            }
            foreach (var bridge in ComputeBridges(screenBounds))
            {
                _region.Union(bridge);
            }

            // A 1x1 bitmap purely to obtain a Graphics context for Region
            // math (Region.IsEmpty requires one) - nothing is ever drawn to it.
            _measurementSurface = new Bitmap(1, 1);
            _measurementGraphics = Graphics.FromImage(_measurementSurface);
        }

        /// <summary>
        /// Finds every pair of monitors that face each other horizontally
        /// or vertically with a small real gap between them, and returns a
        /// filler rectangle exactly covering each such gap.
        /// </summary>
        private static IEnumerable<Rectangle> ComputeBridges(Rectangle[] screens)
        {
            var bridges = new List<Rectangle>();

            for (int i = 0; i < screens.Length; i++)
            {
                for (int j = 0; j < screens.Length; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    Rectangle a = screens[i];
                    Rectangle b = screens[j];

                    // a's right edge facing b's left edge.
                    int horizontalGap = b.Left - a.Right;
                    if (horizontalGap > 0 && horizontalGap <= MaxBridgeableGap)
                    {
                        int top = Math.Max(a.Top, b.Top);
                        int bottom = Math.Min(a.Bottom, b.Bottom);
                        if (bottom > top)
                        {
                            bridges.Add(Rectangle.FromLTRB(a.Right, top, b.Left, bottom));
                        }
                    }

                    // a's bottom edge facing b's top edge.
                    int verticalGap = b.Top - a.Bottom;
                    if (verticalGap > 0 && verticalGap <= MaxBridgeableGap)
                    {
                        int left = Math.Max(a.Left, b.Left);
                        int right = Math.Min(a.Right, b.Right);
                        if (right > left)
                        {
                            bridges.Add(Rectangle.FromLTRB(left, a.Bottom, right, b.Top));
                        }
                    }
                }
            }

            return bridges;
        }

        /// <summary>True when every point of <paramref name="rect"/> lies on some monitor.</summary>
        public bool Contains(RectangleF rect)
        {
            using (var probe = new Region(rect))
            {
                probe.Exclude(_region);
                return probe.IsEmpty(_measurementGraphics);
            }
        }

        public void Dispose()
        {
            _measurementGraphics.Dispose();
            _measurementSurface.Dispose();
            _region.Dispose();
        }
    }
}
