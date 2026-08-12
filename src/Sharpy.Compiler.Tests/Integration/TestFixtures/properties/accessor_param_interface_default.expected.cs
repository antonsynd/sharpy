#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AccessorParamInterfaceDefault
{
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

    public class Speaker : IVolume
    {
    }

    public static void Main()
    {
#line (15, 5) - (15, 28) 8 "accessor_param_interface_default.spy"
        IVolume s = new Speaker();
#line (16, 5) - (16, 17) 8 "accessor_param_interface_default.spy"
        s.Volume = 4;
#line hidden
    }
}
#line default
