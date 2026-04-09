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
        {
#line 10 "access_decorator_overrides_convention.spy"
            return "overridden";
        }

        public Config()
        {
#line 13 "access_decorator_overrides_convention.spy"
            this.__ShouldBePublic = 99;
        }
    }

    public static void Main()
    {
#line 16 "access_decorator_overrides_convention.spy"
        Config c = new Config();
#line 18 "access_decorator_overrides_convention.spy"
        global::Sharpy.Builtins.Print(c.__ShouldBePublic);
#line 19 "access_decorator_overrides_convention.spy"
        global::Sharpy.Builtins.Print(c._ShouldBePublic());
    }
}
