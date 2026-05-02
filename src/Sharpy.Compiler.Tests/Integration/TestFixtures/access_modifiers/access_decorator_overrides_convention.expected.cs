#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AccessDecoratorOverridesConvention
{
    public class Config
    {
        public int __ShouldBePublic;
        public string _ShouldBePublic()
#line 9 "access_decorator_overrides_convention.spy"
        {
#line (10, 9) - (10, 29) 1 "access_decorator_overrides_convention.spy"
            return "overridden";
        }

        public Config()
#line 12 "access_decorator_overrides_convention.spy"
        {
#line (13, 9) - (13, 37) 1 "access_decorator_overrides_convention.spy"
            this.__ShouldBePublic = 99;
        }
    }

    public static void Main()
    {
#line (16, 5) - (16, 26) 1 "access_decorator_overrides_convention.spy"
        Config c = new Config();
#line (18, 5) - (18, 32) 1 "access_decorator_overrides_convention.spy"
        global::Sharpy.Builtins.Print(c.__ShouldBePublic);
#line (19, 5) - (19, 33) 1 "access_decorator_overrides_convention.spy"
        global::Sharpy.Builtins.Print(c._ShouldBePublic());
    }
}
