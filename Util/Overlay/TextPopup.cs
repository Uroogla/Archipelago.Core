using System;
using System.Collections.Generic;
using GameOverlay.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.Overlay
{
    public class TextPopup
    {
        public string Text { get; set; }
        public Font Font { get; set; }
        public SolidBrush Brush { get; set; }
        public DateTime ExpireTime { get; set; }
        public float Opacity { get; set; } = 1.0f;
    }
}
