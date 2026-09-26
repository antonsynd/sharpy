#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace AccessDecoratorOverridesConvention
{
    public static partial class AccessDecoratorOverridesConventionModule
    {
        public static void Main()
        {
#line (16, 5) - (16, 26) 12 "access_decorator_overrides_convention.spy"
            global::AccessDecoratorOverridesConvention.Config c = new global::AccessDecoratorOverridesConvention.Config();
#line (18, 5) - (18, 32) 12 "access_decorator_overrides_convention.spy"
            global::Sharpy.Builtins.Print(c.__ShouldBePublic);
#line (19, 5) - (19, 33) 12 "access_decorator_overrides_convention.spy"
            global::Sharpy.Builtins.Print(c._ShouldBePublic());
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Config")]
    public class Config
    {
        public int __ShouldBePublic;
        public string _ShouldBePublic()
#line 9 "access_decorator_overrides_convention.spy"
        {
#line (10, 9) - (10, 29) 12 "access_decorator_overrides_convention.spy"
            return "overridden";
#line hidden
        }

        public Config()
#line 12 "access_decorator_overrides_convention.spy"
        {
#line (13, 9) - (13, 37) 12 "access_decorator_overrides_convention.spy"
            this.__ShouldBePublic = 99;
#line hidden
        }
    }
}
#line default
