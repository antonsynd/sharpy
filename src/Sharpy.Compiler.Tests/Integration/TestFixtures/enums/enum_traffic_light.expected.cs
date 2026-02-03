#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.EnumTrafficLight
{
    public static class Program
    {
        public static TrafficLight Current = TrafficLight.Red;
        public static void Main()
        {
#line 11 "enum_traffic_light.spy"
            global::Sharpy.Core.Exports.Print(Current == TrafficLight.Red);
#line 12 "enum_traffic_light.spy"
            global::Sharpy.Core.Exports.Print(Current == TrafficLight.Green);
#line 15 "enum_traffic_light.spy"
            Current = TrafficLight.Yellow;
#line 16 "enum_traffic_light.spy"
            global::Sharpy.Core.Exports.Print(Current == TrafficLight.Yellow);
        }
    }

    public enum TrafficLight
    {
        Red = 0,
        Yellow = 1,
        Green = 2
    }
}
