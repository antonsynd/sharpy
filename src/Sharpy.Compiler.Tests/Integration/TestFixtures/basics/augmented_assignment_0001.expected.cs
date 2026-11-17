// Snapshot: Augmented assignment operators (+=, -=, *=)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace AugmentedAssignment0001
{
    public static partial class AugmentedAssignment0001Module
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int CurrentTemp__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte CurrentTemp__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void CurrentTemp__init()
        {
            CurrentTemp__field = 20;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void CurrentTemp__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref CurrentTemp__state, "current_temp", CurrentTemp__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int CurrentTemp
        {
            get
            {
                if (!__Swept.Done)
                    CurrentTemp__slow();
                return ref CurrentTemp__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void CurrentTemp__ensure()
        {
            if (!__Swept.Done)
                CurrentTemp__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int CurrentTemp__set(int value)
        {
            if (__Swept.Done)
                CurrentTemp__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref CurrentTemp__state, ref CurrentTemp__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? CurrentTemp__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref CurrentTemp__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int CurrentTemp__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = CurrentTemp__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref CurrentTemp__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R CurrentTemp__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                CurrentTemp__set(spill.Value!);
            return result;
        }

        public static void Main()
        {
            __Load();
#line (5, 5) - (5, 22) 12 "augmented_assignment_0001.spy"
            CurrentTemp__set(CurrentTemp + 5);
#line (6, 5) - (6, 24) 12 "augmented_assignment_0001.spy"
            global::Sharpy.Builtins.Print(global::AugmentedAssignment0001.AugmentedAssignment0001Module.CurrentTemp);
#line (8, 5) - (8, 22) 12 "augmented_assignment_0001.spy"
            CurrentTemp__set(CurrentTemp - 3);
#line (9, 5) - (9, 24) 12 "augmented_assignment_0001.spy"
            global::Sharpy.Builtins.Print(global::AugmentedAssignment0001.AugmentedAssignment0001Module.CurrentTemp);
#line (11, 5) - (11, 22) 12 "augmented_assignment_0001.spy"
            CurrentTemp__set(CurrentTemp * 2);
#line (12, 5) - (12, 24) 12 "augmented_assignment_0001.spy"
            global::Sharpy.Builtins.Print(global::AugmentedAssignment0001.AugmentedAssignment0001Module.CurrentTemp);
#line (14, 5) - (14, 23) 12 "augmented_assignment_0001.spy"
            CurrentTemp__set(global::Sharpy.Builtins.FloorDiv(CurrentTemp, 4));
#line (15, 5) - (15, 24) 12 "augmented_assignment_0001.spy"
            global::Sharpy.Builtins.Print(global::AugmentedAssignment0001.AugmentedAssignment0001Module.CurrentTemp);
#line (17, 5) - (17, 22) 12 "augmented_assignment_0001.spy"
            CurrentTemp__set(global::Sharpy.Builtins.FloorMod(CurrentTemp, 7));
#line (18, 5) - (18, 24) 12 "augmented_assignment_0001.spy"
            global::Sharpy.Builtins.Print(global::AugmentedAssignment0001.AugmentedAssignment0001Module.CurrentTemp);
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref CurrentTemp__state, "current_temp", CurrentTemp__init);
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
}
#line default
