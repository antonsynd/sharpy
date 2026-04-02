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
        {
#line 15 "access_naming_convention_fields.spy"
            return this.__PrivateField;
        }

        public MyClass(int a, int b, int c)
        {
#line 10 "access_naming_convention_fields.spy"
            this.__PrivateField = a;
#line 11 "access_naming_convention_fields.spy"
            this._ProtectedField = b;
#line 12 "access_naming_convention_fields.spy"
            this.PublicField = c;
        }
    }

    public class Child : MyClass
    {
        public int GetProtected()
        {
#line 22 "access_naming_convention_fields.spy"
            return this._ProtectedField;
        }

        public Child() : base(1, 2, 3)
        {
        }
    }

    public static void Main()
    {
#line 25 "access_naming_convention_fields.spy"
        MyClass obj = new MyClass(10, 20, 30);
#line 26 "access_naming_convention_fields.spy"
        global::Sharpy.Builtins.Print(obj.GetPrivate());
#line 27 "access_naming_convention_fields.spy"
        global::Sharpy.Builtins.Print(obj.PublicField);
#line 29 "access_naming_convention_fields.spy"
        Child child = new Child();
#line 30 "access_naming_convention_fields.spy"
        global::Sharpy.Builtins.Print(child.GetProtected());
#line 31 "access_naming_convention_fields.spy"
        global::Sharpy.Builtins.Print(child.PublicField);
    }
}
