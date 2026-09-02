using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Bounce
{
    /// <summary>
    /// Draws trail segments as a soft, airbrushed streak: a wide, very
    /// transparent "glow" pass under a narrower, more opaque "core" pass,
    /// both round-capped and anti-aliased, rather than a hard line.
    /// </summary>
    internal static class TrailRenderer
    {
        // Reused across every segment/window/frame instead of allocating a
        // new Pen (a native GDI+ object) per segment - with a long trail at
        // high speed that was thousands of allocations per second. Only
        // .Width (once per call) and .Color (per segment) change.
        private static readonly Pen GlowPen = new Pen(Color.Black, 1f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        private static readonly Pen CorePen = new Pen(Color.Black, 1f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        /// <summary>Batch path: redraws every given segment. Used for the finite-fade trail, whose segment count is naturally bounded by its max age.</summary>
        public static void Draw(
            Graphics g,
            IEnumerable<(PointF From, PointF To, float Alpha)> segments,
            PointF offset,
            RectangleF clip,
            Color baseColor,
            float coreWidth)
        {
            float glowWidth = coreWidth * 2.2f;
            GlowPen.Width = glowWidth;
            CorePen.Width = coreWidth;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            foreach (var (from, to, alpha) in segments)
            {
                var localFrom = new PointF(from.X - offset.X, from.Y - offset.Y);
                var localTo = new PointF(to.X - offset.X, to.Y - offset.Y);
                DrawLocalSegment(g, localFrom, localTo, alpha, baseColor, clip, glowWidth, coreWidth);
            }
        }

        /// <summary>
        /// Single-segment path: draws just one already-local-coordinate
        /// segment. Used to paint a "forever" trail's segments once each,
        /// onto a persistent bitmap, as they're recorded - see
        /// <see cref="MonitorWindow"/> - instead of redrawing an
        /// ever-growing trail from scratch every frame.
        /// </summary>
        public static void DrawSegment(Graphics g, PointF localFrom, PointF localTo, float alpha, Color baseColor, RectangleF clip, float coreWidth)
        {
            float glowWidth = coreWidth * 2.2f;
            GlowPen.Width = glowWidth;
            CorePen.Width = coreWidth;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawLocalSegment(g, localFrom, localTo, alpha, baseColor, clip, glowWidth, coreWidth);
        }

        private static void DrawLocalSegment(Graphics g, PointF localFrom, PointF localTo, float alpha, Color baseColor, RectangleF clip, float glowWidth, float coreWidth)
        {
            var bounds = RectangleF.FromLTRB(
                Math.Min(localFrom.X, localTo.X) - glowWidth,
                Math.Min(localFrom.Y, localTo.Y) - glowWidth,
                Math.Max(localFrom.X, localTo.X) + glowWidth,
                Math.Max(localFrom.Y, localTo.Y) + glowWidth);

            if (!bounds.IntersectsWith(clip))
            {
                return;
            }

            GlowPen.Color = Color.FromArgb((int)(alpha * 60), baseColor);
            g.DrawLine(GlowPen, localFrom, localTo);

            CorePen.Color = Color.FromArgb((int)(alpha * 140), baseColor);
            g.DrawLine(CorePen, localFrom, localTo);
        }
    }
}
