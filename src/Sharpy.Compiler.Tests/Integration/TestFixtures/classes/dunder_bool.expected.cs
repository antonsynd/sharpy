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
#line (9, 9) - (9, 32) 1 "dunder_bool.spy"
                return this.Value != 0;
            }
        }

        public static bool operator true(Truthy value)
        {
            return value.IsTrue;
        }

        public Truthy(int value)
#line 5 "dunder_bool.spy"
        {
#line (6, 9) - (6, 27) 1 "dunder_bool.spy"
            this.Value = value;
        }

        public static bool operator false(Truthy value)
        {
            return !value.IsTrue;
        }
    }

    public static void Main()
    {
#line (12, 5) - (12, 18) 1 "dunder_bool.spy"
        var t = new Truthy(1);
#line (13, 5) - (13, 18) 1 "dunder_bool.spy"
        var f = new Truthy(0);
#line (14, 5) - (16, 1) 1 "dunder_bool.spy"
        if (t)
        {
#line (15, 9) - (15, 24) 1 "dunder_bool.spy"
            global::Sharpy.Builtins.Print("truthy");
        }

#line (16, 5) - (20, 1) 1 "dunder_bool.spy"
        if (f)
        {
#line (17, 9) - (17, 34) 1 "dunder_bool.spy"
            global::Sharpy.Builtins.Print("should not print");
        }
        else
        {
#line (19, 9) - (19, 23) 1 "dunder_bool.spy"
            global::Sharpy.Builtins.Print("falsy");
        }
    }
}
