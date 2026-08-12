#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AccessorTypeAliasControl
{
    public class Thermostat
    {
        protected double _C = 0.0d;
        public double Target
        {
            get
            {
#line (14, 9) - (14, 24) 16 "accessor_type_alias_control.spy"
                return this._C;
#line hidden
            }

            set
            {
#line (17, 9) - (17, 24) 16 "accessor_type_alias_control.spy"
                this._C = value;
#line hidden
            }
        }
    }

    public static void Main()
    {
#line (21, 5) - (21, 34) 8 "accessor_type_alias_control.spy"
        Thermostat t = new Thermostat();
#line (22, 5) - (22, 20) 8 "accessor_type_alias_control.spy"
        t.Target = 21.5d;
#line (23, 5) - (23, 20) 8 "accessor_type_alias_control.spy"
        global::Sharpy.Builtins.Print(t.Target);
#line hidden
    }
}
#line default
