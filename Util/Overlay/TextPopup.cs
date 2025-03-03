using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.Overlay
{
    public class TextPopup
    {
        public string Text { get; set; }
        public Point Position { get; set; }
        public Color TextColor { get; set; }
        public Font Font { get; set; }
        public DateTime ExpirationTime { get; set; }
        public Color BackgroundColor { get; set; }
        public int Padding { get; set; }

        public bool IsExpired => DateTime.Now > ExpirationTime;
    }
}
