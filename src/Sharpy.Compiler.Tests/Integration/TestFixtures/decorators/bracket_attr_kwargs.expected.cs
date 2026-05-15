#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class BracketAttrKwargs
{
    public class Settings
    {
        [System.ComponentModel.DefaultValue(-42)]
        public int GetDefaultThreshold()
#line 4 "bracket_attr_kwargs.spy"
        {
#line (5, 9) - (5, 20) 1 "bracket_attr_kwargs.spy"
            return -42;
        }
    }

    public static void Main()
    {
#line (8, 5) - (8, 19) 1 "bracket_attr_kwargs.spy"
        var s = new Settings();
#line (9, 5) - (9, 37) 1 "bracket_attr_kwargs.spy"
        global::Sharpy.Builtins.Print(s.GetDefaultThreshold());
    }
}
