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
#line 9 "dunder_bool.spy"
                return this.Value != 0;
            }
        }

        public static bool operator true(Truthy value)
        {
            return value.IsTrue;
        }

        public Truthy(int value)
        {
#line 6 "dunder_bool.spy"
            this.Value = value;
        }

        public static bool operator false(Truthy value)
        {
            return !value.IsTrue;
        }
    }

    public static void Main()
    {
#line 12 "dunder_bool.spy"
        var t = new Truthy(1);
#line 13 "dunder_bool.spy"
        var f = new Truthy(0);
#line 14 "dunder_bool.spy"
        if (t)
        {
#line 15 "dunder_bool.spy"
            global::Sharpy.Builtins.Print(((Sharpy.Str)"truthy"));
        }

#line 16 "dunder_bool.spy"
        if (f)
        {
#line 17 "dunder_bool.spy"
            global::Sharpy.Builtins.Print(((Sharpy.Str)"should not print"));
        }
        else
        {
#line 19 "dunder_bool.spy"
            global::Sharpy.Builtins.Print(((Sharpy.Str)"falsy"));
        }
    }
}
