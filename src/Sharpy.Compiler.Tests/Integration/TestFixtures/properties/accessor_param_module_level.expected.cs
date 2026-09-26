#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace AccessorParamModuleLevel
{
    public static partial class AccessorParamModuleLevelModule
    {
        public static int _Backing = 0;
        public static int Level
        {
            set
            {
#line (11, 5) - (11, 21) 16 "accessor_param_module_level.spy"
                _Backing = value + 1;
#line hidden
            }
        }

        public static int ReadLevel()
        {
#line (15, 5) - (15, 21) 12 "accessor_param_module_level.spy"
            return global::AccessorParamModuleLevel.AccessorParamModuleLevelModule._Backing;
#line hidden
        }

        public static void Main()
        {
#line (19, 5) - (19, 14) 12 "accessor_param_module_level.spy"
            Level = 6;
#line (20, 5) - (20, 24) 12 "accessor_param_module_level.spy"
            global::Sharpy.Builtins.Print(global::AccessorParamModuleLevel.AccessorParamModuleLevelModule.ReadLevel());
#line hidden
        }
    }
}
#line default
