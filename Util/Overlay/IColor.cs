using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.Overlay
{
    public interface IColor
    {
        byte R { get; }
        byte G { get; }
        byte B { get; }
        byte A { get; }
    }
}
