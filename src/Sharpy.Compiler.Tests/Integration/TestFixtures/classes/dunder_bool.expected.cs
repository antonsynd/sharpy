#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DunderBool
{
    public class Truthy : Sharpy.IBoolConvertible
    {
        public int Value;
        public virtual bool IsTrue
        {
            get
            {
#line (9, 9) - (9, 32) 16 "dunder_bool.spy"
                return this.Value != 0;
#line hidden
            }
        }

        public static bool operator true(Truthy value)
        {
            return value.IsTrue;
        }

        public Truthy(int value)
#line 5 "dunder_bool.spy"
        {
#line (6, 9) - (6, 27) 12 "dunder_bool.spy"
            this.Value = value;
#line hidden
        }

        public static bool operator false(Truthy value)
        {
            return !value.IsTrue;
        }
    }

    public static void Main()
    {
#line (12, 5) - (12, 18) 8 "dunder_bool.spy"
        var t = new Truthy(1);
#line (13, 5) - (13, 18) 8 "dunder_bool.spy"
        var f = new Truthy(0);
#line (14, 5) - (16, 1) 8 "dunder_bool.spy"
        if (t.IsTrue)
#line hidden
        {
#line (15, 9) - (15, 24) 12 "dunder_bool.spy"
            global::Sharpy.Builtins.Print("truthy");
#line hidden
        }

#line (16, 5) - (20, 1) 8 "dunder_bool.spy"
        if (f.IsTrue)
#line hidden
        {
#line (17, 9) - (17, 34) 12 "dunder_bool.spy"
            global::Sharpy.Builtins.Print("should not print");
#line hidden
        }
        else
        {
#line (19, 9) - (19, 23) 12 "dunder_bool.spy"
            global::Sharpy.Builtins.Print("falsy");
#line hidden
        }
    }
}
#line default
