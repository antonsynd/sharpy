#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AccessorTypeGenericControl
{
    public class Holder<T>
    {
        protected T _V;
        public T Item
        {
            get
            {
#line (11, 9) - (11, 24) 16 "accessor_type_generic_control.spy"
                return this._V;
#line hidden
            }

            set
            {
#line (14, 9) - (14, 24) 16 "accessor_type_generic_control.spy"
                this._V = value;
#line hidden
            }
        }

        public Holder(T v)
#line 7 "accessor_type_generic_control.spy"
        {
#line (8, 9) - (8, 20) 12 "accessor_type_generic_control.spy"
            this._V = v;
#line hidden
        }
    }

    public static void Main()
    {
#line (18, 5) - (18, 32) 8 "accessor_type_generic_control.spy"
        Holder<int> h = new Holder<int>(3);
#line (19, 5) - (19, 15) 8 "accessor_type_generic_control.spy"
        h.Item = 9;
#line (20, 5) - (20, 18) 8 "accessor_type_generic_control.spy"
        global::Sharpy.Builtins.Print(h.Item);
#line hidden
    }
}
#line default
