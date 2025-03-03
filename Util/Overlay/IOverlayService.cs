using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.Overlay
{
    internal interface IOverlayService
    {
        bool AttachToWindow(IntPtr targetWindowHandle);
        void AddTextPopup(string text, int x, int y, Color textColor, double durationSeconds = 3.0, int fontSize = 14);
    }
}
