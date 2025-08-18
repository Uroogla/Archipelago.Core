using SharpDX.DirectWrite;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.Overlay
{
    public class FontManager
    {
        private SharpDX.DirectWrite.Factory _directWriteFactory;
        private ResourceFontLoader _resourceFontLoader;
        private FontCollection _currentFontCollection;

        public FontManager()
        {
           _directWriteFactory = new SharpDX.DirectWrite.Factory();
           _resourceFontLoader = new ResourceFontLoader(_directWriteFactory);
           _currentFontCollection = new FontCollection(_directWriteFactory, _resourceFontLoader, _resourceFontLoader.Key);
        }
        public ResourceFontLoader GetFontLoader()
        {
            return _resourceFontLoader;
        }
        public TextFormat CreateFont(string fontName, float size)
        {
            return new TextFormat(_directWriteFactory, fontName, _currentFontCollection, FontWeight.Normal, SharpDX.DirectWrite.FontStyle.Normal, FontStretch.Normal, size);
        }

    }
}
