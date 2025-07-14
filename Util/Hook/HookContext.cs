using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.Hook
{
    public class HookContext
    {
        public IntPtr[] Parameters { get; set; }
        public IntPtr ReturnValue { get; set; }
        public bool SuppressOriginal { get; set; } = false;
        public Dictionary<string, object> UserData { get; set; } = new();
    }
}
