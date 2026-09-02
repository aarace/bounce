using System.Drawing;
using System.Drawing.Drawing2D;

namespace Bounce
{
    /// <summary>Shared drawing logic for the ball, used by every window.</summary>
    internal static class BallRenderer
    {
        public static void Draw(Graphics g, RectangleF bounds, Image image, Brush brush)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (image != null)
            {
                g.DrawImage(image, Rectangle.Round(bounds));
            }
            else
            {
                g.FillEllipse(brush, bounds);
            }
        }
    }
}
