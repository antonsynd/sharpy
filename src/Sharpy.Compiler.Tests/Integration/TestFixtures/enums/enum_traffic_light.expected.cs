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
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::EnumTrafficLight.TrafficLight Current__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Current__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Current__init()
        {
            Current__field = global::EnumTrafficLight.TrafficLight.RED;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Current__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Current__state, "current", Current__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref global::EnumTrafficLight.TrafficLight Current
        {
            get
            {
                if (!__Swept.Done)
                    Current__slow();
                return ref Current__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Current__ensure()
        {
            if (!__Swept.Done)
                Current__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Current__storeSlow(global::EnumTrafficLight.TrafficLight value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Current__state, ref Current__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::EnumTrafficLight.TrafficLight Current__set(global::EnumTrafficLight.TrafficLight value)
        {
            if (__Swept.Done)
                Current__field = value;
            else
                Current__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<global::EnumTrafficLight.TrafficLight>? Current__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Current__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<global::EnumTrafficLight.TrafficLight>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref global::EnumTrafficLight.TrafficLight Current__out(out global::System.Runtime.CompilerServices.StrongBox<global::EnumTrafficLight.TrafficLight>? spill)
        {
            spill = Current__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Current__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Current__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<global::EnumTrafficLight.TrafficLight>? spill)
        {
            if (spill != null)
                Current__set(spill.Value!);
            return result;
        }

        public static void Main()
        {
            __Load();
#line (11, 5) - (11, 39) 12 "enum_traffic_light.spy"
            global::Sharpy.Builtins.Print(global::EnumTrafficLight.EnumTrafficLightModule.Current == global::EnumTrafficLight.TrafficLight.RED);
#line (12, 5) - (12, 41) 12 "enum_traffic_light.spy"
            global::Sharpy.Builtins.Print(global::EnumTrafficLight.EnumTrafficLightModule.Current == global::EnumTrafficLight.TrafficLight.GREEN);
#line (15, 5) - (15, 34) 12 "enum_traffic_light.spy"
            Current__set(global::EnumTrafficLight.TrafficLight.YELLOW);
#line (16, 5) - (16, 42) 12 "enum_traffic_light.spy"
            global::Sharpy.Builtins.Print(global::EnumTrafficLight.EnumTrafficLightModule.Current == global::EnumTrafficLight.TrafficLight.YELLOW);
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref Current__state, "current", Current__init);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static class __Swept
        {
            internal static readonly bool Done;
            static __Swept()
            {
                Done = global::Sharpy.StaticInit.TryLoad(ref __host, __Sweep);
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        [global::Sharpy.SharpyLoadEntry(true)]
        public static void __Load()
        {
            if (!__Swept.Done)
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
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
