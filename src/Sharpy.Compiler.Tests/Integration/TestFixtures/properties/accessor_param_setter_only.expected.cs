#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AccessorParamSetterOnly
{
    public class Box
    {
        protected int _Value = 0;
        public int GetValue()
#line 11 "accessor_param_setter_only.spy"
        {
#line (12, 9) - (12, 28) 12 "accessor_param_setter_only.spy"
            return this._Value;
#line hidden
        }

        public int Doubled
        {
            set
            {
#line (9, 9) - (9, 28) 16 "accessor_param_setter_only.spy"
                this._Value = value * 2;
#line hidden
            }
        }
    }

    public static void Main()
    {
#line (16, 5) - (16, 20) 8 "accessor_param_setter_only.spy"
        Box b = new Box();
#line (17, 5) - (17, 18) 8 "accessor_param_setter_only.spy"
        b.Doubled = 5;
#line (18, 5) - (18, 25) 8 "accessor_param_setter_only.spy"
        global::Sharpy.Builtins.Print(b.GetValue());
#line hidden
    }
}
#line default
