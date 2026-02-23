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
        Red = 0,
        Yellow = 1,
        Green = 2
    }

    public static TrafficLight Current = TrafficLight.Red;
    public static void Main()
    {
#line 11 "enum_traffic_light.spy"
        global::Sharpy.Builtins.Print(Current == TrafficLight.Red);
#line 12 "enum_traffic_light.spy"
        global::Sharpy.Builtins.Print(Current == TrafficLight.Green);
#line 15 "enum_traffic_light.spy"
        Current = TrafficLight.Yellow;
#line 16 "enum_traffic_light.spy"
        global::Sharpy.Builtins.Print(Current == TrafficLight.Yellow);
    }
}
