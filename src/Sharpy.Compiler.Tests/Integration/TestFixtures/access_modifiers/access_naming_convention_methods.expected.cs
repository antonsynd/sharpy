#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AccessNamingConventionMethods
{
    public class Base
    {
        public override string ToString()
#line 9 "access_naming_convention_methods.spy"
        {
#line (10, 9) - (10, 23) 12 "access_naming_convention_methods.spy"
            return "Base";
#line hidden
        }

        private int __PrivateMethod()
#line 12 "access_naming_convention_methods.spy"
        {
#line (13, 9) - (13, 19) 12 "access_naming_convention_methods.spy"
            return 42;
#line hidden
        }

        protected string _ProtectedMethod()
#line 15 "access_naming_convention_methods.spy"
        {
#line (16, 9) - (16, 28) 12 "access_naming_convention_methods.spy"
            return "protected";
#line hidden
        }

        public bool PublicMethod()
#line 18 "access_naming_convention_methods.spy"
        {
#line (19, 9) - (19, 21) 12 "access_naming_convention_methods.spy"
            return true;
#line hidden
        }

        public int CallPrivate()
#line 21 "access_naming_convention_methods.spy"
        {
#line (22, 9) - (22, 40) 12 "access_naming_convention_methods.spy"
            return this.__PrivateMethod();
#line hidden
        }

        public Base()
#line 6 "access_naming_convention_methods.spy"
        {
#line (7, 9) - (7, 14) 12 "access_naming_convention_methods.spy"
            ;
#line hidden
        }
    }

    public class Child : Base
    {
        public string CallProtected()
#line 28 "access_naming_convention_methods.spy"
        {
#line (29, 9) - (29, 41) 12 "access_naming_convention_methods.spy"
            return this._ProtectedMethod();
#line hidden
        }

        public Child() : base()
#line 25 "access_naming_convention_methods.spy"
        {
        }
    }

    public static void Main()
    {
#line (32, 5) - (32, 22) 8 "access_naming_convention_methods.spy"
        Base b = new Base();
#line (34, 5) - (34, 18) 8 "access_naming_convention_methods.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Str(b));
#line (36, 5) - (36, 29) 8 "access_naming_convention_methods.spy"
        global::Sharpy.Builtins.Print(b.PublicMethod());
#line (38, 5) - (38, 28) 8 "access_naming_convention_methods.spy"
        global::Sharpy.Builtins.Print(b.CallPrivate());
#line (40, 5) - (40, 24) 8 "access_naming_convention_methods.spy"
        Child c = new Child();
#line (42, 5) - (42, 30) 8 "access_naming_convention_methods.spy"
        global::Sharpy.Builtins.Print(c.CallProtected());
#line hidden
    }
}
#line default
