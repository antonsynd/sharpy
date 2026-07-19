#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class BracketAttrSimple
{
    [Serializable]
    public class Config
    {
        public int Value;
        public Config(int v)
#line 6 "bracket_attr_simple.spy"
        {
#line (7, 9) - (7, 23) 12 "bracket_attr_simple.spy"
            this.Value = v;
#line hidden
        }
    }

    public static void Main()
    {
#line (10, 5) - (10, 19) 8 "bracket_attr_simple.spy"
        var c = new Config(42);
#line (11, 5) - (11, 19) 8 "bracket_attr_simple.spy"
        global::Sharpy.Builtins.Print(c.Value);
#line hidden
    }
}
#line default
