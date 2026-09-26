// Snapshot: Enum definition and member access
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace EnumTrafficLight
{
    public static partial class EnumTrafficLightModule
    {
        public static global::EnumTrafficLight.TrafficLight Current = global::EnumTrafficLight.TrafficLight.RED;
        public static void Main()
        {
#line (11, 5) - (11, 39) 12 "enum_traffic_light.spy"
            global::Sharpy.Builtins.Print(global::EnumTrafficLight.EnumTrafficLightModule.Current == global::EnumTrafficLight.TrafficLight.RED);
#line (12, 5) - (12, 41) 12 "enum_traffic_light.spy"
            global::Sharpy.Builtins.Print(global::EnumTrafficLight.EnumTrafficLightModule.Current == global::EnumTrafficLight.TrafficLight.GREEN);
#line (15, 5) - (15, 34) 12 "enum_traffic_light.spy"
            Current = global::EnumTrafficLight.TrafficLight.YELLOW;
#line (16, 5) - (16, 42) 12 "enum_traffic_light.spy"
            global::Sharpy.Builtins.Print(global::EnumTrafficLight.EnumTrafficLightModule.Current == global::EnumTrafficLight.TrafficLight.YELLOW);
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "TrafficLight")]
    public enum TrafficLight
    {
        RED = 0,
        YELLOW = 1,
        GREEN = 2
    }
}
#line default
