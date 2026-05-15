#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class BracketAttrOnField
{
    public class Settings
    {
        [System.ComponentModel.DefaultValue(100)]
        public int Threshold;
        public Settings()
#line 6 "bracket_attr_on_field.spy"
        {
#line (7, 9) - (7, 29) 1 "bracket_attr_on_field.spy"
            this.Threshold = 100;
        }
    }

    public static void Main()
    {
#line (10, 5) - (10, 19) 1 "bracket_attr_on_field.spy"
        var s = new Settings();
#line (11, 5) - (11, 23) 1 "bracket_attr_on_field.spy"
        global::Sharpy.Builtins.Print(s.Threshold);
    }
}
