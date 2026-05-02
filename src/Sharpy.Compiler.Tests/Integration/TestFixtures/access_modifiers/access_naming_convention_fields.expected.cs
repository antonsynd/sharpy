#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AccessNamingConventionFields
{
    public class MyClass
    {
        private int __PrivateField;
        protected int _ProtectedField;
        public int PublicField;
        public int GetPrivate()
#line 14 "access_naming_convention_fields.spy"
        {
#line (15, 9) - (15, 37) 1 "access_naming_convention_fields.spy"
            return this.__PrivateField;
        }

        public MyClass(int a, int b, int c)
#line 9 "access_naming_convention_fields.spy"
        {
#line (10, 9) - (10, 33) 1 "access_naming_convention_fields.spy"
            this.__PrivateField = a;
#line (11, 9) - (11, 34) 1 "access_naming_convention_fields.spy"
            this._ProtectedField = b;
#line (12, 9) - (12, 30) 1 "access_naming_convention_fields.spy"
            this.PublicField = c;
        }
    }

    public class Child : MyClass
    {
        public int GetProtected()
#line 21 "access_naming_convention_fields.spy"
        {
#line (22, 9) - (22, 38) 1 "access_naming_convention_fields.spy"
            return this._ProtectedField;
        }

        public Child() : base(1, 2, 3)
#line 18 "access_naming_convention_fields.spy"
        {
        }
    }

    public static void Main()
    {
#line (25, 5) - (25, 40) 1 "access_naming_convention_fields.spy"
        MyClass obj = new MyClass(10, 20, 30);
#line (26, 5) - (26, 29) 1 "access_naming_convention_fields.spy"
        global::Sharpy.Builtins.Print(obj.GetPrivate());
#line (27, 5) - (27, 28) 1 "access_naming_convention_fields.spy"
        global::Sharpy.Builtins.Print(obj.PublicField);
#line (29, 5) - (29, 28) 1 "access_naming_convention_fields.spy"
        Child child = new Child();
#line (30, 5) - (30, 33) 1 "access_naming_convention_fields.spy"
        global::Sharpy.Builtins.Print(child.GetProtected());
#line (31, 5) - (31, 30) 1 "access_naming_convention_fields.spy"
        global::Sharpy.Builtins.Print(child.PublicField);
    }
}
