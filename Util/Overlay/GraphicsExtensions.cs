using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.Overlay
{
    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics graphics, Brush brush, float x, float y, float width, float height, float radius)
        {
            RectangleF rectangle = new RectangleF(x, y, width, height);
            GraphicsPath path = GetRoundedRectPath(rectangle, radius);
            graphics.FillPath(brush, path);
            path.Dispose();
        }

        private static GraphicsPath GetRoundedRectPath(RectangleF rectangle, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2;

            RectangleF arcRect = new RectangleF(rectangle.X, rectangle.Y, diameter, diameter);
            path.AddArc(arcRect, 180, 90); // top left

            arcRect.X = rectangle.Right - diameter;
            path.AddArc(arcRect, 270, 90); // top right

            arcRect.Y = rectangle.Bottom - diameter;
            path.AddArc(arcRect, 0, 90); // bottom right

            arcRect.X = rectangle.X;
            path.AddArc(arcRect, 90, 90); // bottom left

            path.CloseFigure();
            return path;
        }
    }
}
