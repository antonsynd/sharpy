#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace BracketAttrKwargs
{
    public static partial class BracketAttrKwargsModule
    {
        public static void Main()
        {
#line (8, 5) - (8, 19) 12 "bracket_attr_kwargs.spy"
            var s = new global::BracketAttrKwargs.Settings();
#line (9, 5) - (9, 37) 12 "bracket_attr_kwargs.spy"
            global::Sharpy.Builtins.Print(s.GetDefaultThreshold());
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Settings")]
    public class Settings
    {
        [System.ComponentModel.DefaultValue(-42)]
        public int GetDefaultThreshold()
#line 4 "bracket_attr_kwargs.spy"
        {
#line (5, 9) - (5, 20) 12 "bracket_attr_kwargs.spy"
            return -42;
#line hidden
        }
    }
}
#line default
