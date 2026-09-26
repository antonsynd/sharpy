#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace AccessorParamInterfaceDefault
{
    public static partial class AccessorParamInterfaceDefaultModule
    {
        public static void Main()
        {
#line (15, 5) - (15, 28) 12 "accessor_param_interface_default.spy"
            global::AccessorParamInterfaceDefault.IVolume s = new global::AccessorParamInterfaceDefault.Speaker();
#line (16, 5) - (16, 17) 12 "accessor_param_interface_default.spy"
            s.Volume = 4;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "IVolume")]
    public interface IVolume
    {
        int Volume
        {
            set
            {
#line (7, 9) - (7, 21) 16 "accessor_param_interface_default.spy"
                global::Sharpy.Builtins.Print(value * 2);
#line hidden
            }
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Speaker")]
    public class Speaker : global::AccessorParamInterfaceDefault.IVolume
    {
    }
}
#line default
