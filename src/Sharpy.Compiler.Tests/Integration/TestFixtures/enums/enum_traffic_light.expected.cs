// Snapshot: Enum definition and member access
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class EnumTrafficLight
{
    public enum TrafficLight
    {
        RED = 0,
        YELLOW = 1,
        GREEN = 2
    }

    public static TrafficLight Current = TrafficLight.RED;
    public static void Main()
    {
#line (11, 5) - (11, 39) 1 "enum_traffic_light.spy"
        global::Sharpy.Builtins.Print(Current == TrafficLight.RED);
#line (12, 5) - (12, 41) 1 "enum_traffic_light.spy"
        global::Sharpy.Builtins.Print(Current == TrafficLight.GREEN);
#line (15, 5) - (15, 34) 1 "enum_traffic_light.spy"
        Current = TrafficLight.YELLOW;
#line (16, 5) - (16, 42) 1 "enum_traffic_light.spy"
        global::Sharpy.Builtins.Print(Current == TrafficLight.YELLOW);
    }
}
