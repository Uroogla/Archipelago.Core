using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.Overlay
{
    public class FontManager
    {
        private PrivateFontCollection privateFonts = new PrivateFontCollection();

        public void LoadFontFromFile(string fontPath)
        {
            privateFonts.AddFontFile(fontPath);
        }

        public void LoadFontFromMemory(byte[] fontData)
        {
            IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
            Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
            privateFonts.AddMemoryFont(fontPtr, fontData.Length);
            Marshal.FreeCoTaskMem(fontPtr);
        }

        public FontFamily GetFontFamily(int index = 0)
        {
            return privateFonts.Families[index];
        }
    }
}
