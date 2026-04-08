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
        {
#line 10 "access_naming_convention_methods.spy"
            return ((Sharpy.Str)"Base");
        }

        private int __PrivateMethod()
        {
#line 13 "access_naming_convention_methods.spy"
            return 42;
        }

        protected Sharpy.Str _ProtectedMethod()
        {
#line 16 "access_naming_convention_methods.spy"
            return ((Sharpy.Str)"protected");
        }

        public bool PublicMethod()
        {
#line 19 "access_naming_convention_methods.spy"
            return true;
        }

        public int CallPrivate()
        {
#line 22 "access_naming_convention_methods.spy"
            return this.__PrivateMethod();
        }

        public Base()
        {
#line 7 "access_naming_convention_methods.spy"
            ;
        }
    }

    public class Child : Base
    {
        public Sharpy.Str CallProtected()
        {
#line 29 "access_naming_convention_methods.spy"
            return this._ProtectedMethod();
        }

        public Child() : base()
        {
        }
    }

    public static void Main()
    {
#line 32 "access_naming_convention_methods.spy"
        Base b = new Base();
#line 34 "access_naming_convention_methods.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Str(b));
#line 36 "access_naming_convention_methods.spy"
        global::Sharpy.Builtins.Print(b.PublicMethod());
#line 38 "access_naming_convention_methods.spy"
        global::Sharpy.Builtins.Print(b.CallPrivate());
#line 40 "access_naming_convention_methods.spy"
        Child c = new Child();
#line 42 "access_naming_convention_methods.spy"
        global::Sharpy.Builtins.Print(c.CallProtected());
    }
}
