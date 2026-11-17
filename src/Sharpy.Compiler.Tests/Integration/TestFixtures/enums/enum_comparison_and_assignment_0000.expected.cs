// Snapshot: Enum comparison and variable assignment
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace EnumComparisonAndAssignment0000
{
    public static partial class EnumComparisonAndAssignment0000Module
    {
        public static string GetStatusDescription(global::EnumComparisonAndAssignment0000.OrderStatus status)
        {
#line (10, 5) - (19, 38) 12 "enum_comparison_and_assignment_0000.spy"
            if (status == global::EnumComparisonAndAssignment0000.OrderStatus.PENDING)
#line hidden
            {
#line (11, 9) - (11, 35) 16 "enum_comparison_and_assignment_0000.spy"
                return "Order is pending";
#line hidden
            }
            else if (status == global::EnumComparisonAndAssignment0000.OrderStatus.PROCESSING)
            {
#line (13, 9) - (13, 43) 16 "enum_comparison_and_assignment_0000.spy"
                return "Order is being processed";
#line hidden
            }
            else if (status == global::EnumComparisonAndAssignment0000.OrderStatus.SHIPPED)
            {
#line (15, 9) - (15, 41) 16 "enum_comparison_and_assignment_0000.spy"
                return "Order has been shipped";
#line hidden
            }
            else if (status == global::EnumComparisonAndAssignment0000.OrderStatus.DELIVERED)
            {
#line (17, 9) - (17, 47) 16 "enum_comparison_and_assignment_0000.spy"
                return "Order delivered successfully";
#line hidden
            }
            else
            {
#line (19, 9) - (19, 38) 16 "enum_comparison_and_assignment_0000.spy"
                return "Order was cancelled";
#line hidden
            }
        }

        public static bool CanCancel(global::EnumComparisonAndAssignment0000.OrderStatus status)
        {
#line (22, 5) - (27, 22) 12 "enum_comparison_and_assignment_0000.spy"
            if (status == global::EnumComparisonAndAssignment0000.OrderStatus.PENDING)
#line hidden
            {
#line (23, 9) - (23, 21) 16 "enum_comparison_and_assignment_0000.spy"
                return true;
#line hidden
            }
            else if (status == global::EnumComparisonAndAssignment0000.OrderStatus.PROCESSING)
            {
#line (25, 9) - (25, 21) 16 "enum_comparison_and_assignment_0000.spy"
                return true;
#line hidden
            }
            else
            {
#line (27, 9) - (27, 22) 16 "enum_comparison_and_assignment_0000.spy"
                return false;
#line hidden
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::EnumComparisonAndAssignment0000.OrderStatus CurrentStatus__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte CurrentStatus__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void CurrentStatus__init()
        {
            CurrentStatus__field = global::EnumComparisonAndAssignment0000.OrderStatus.PENDING;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void CurrentStatus__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref CurrentStatus__state, "current_status", CurrentStatus__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref global::EnumComparisonAndAssignment0000.OrderStatus CurrentStatus
        {
            get
            {
                if (!__Swept.Done)
                    CurrentStatus__slow();
                return ref CurrentStatus__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void CurrentStatus__ensure()
        {
            if (!__Swept.Done)
                CurrentStatus__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::EnumComparisonAndAssignment0000.OrderStatus CurrentStatus__set(global::EnumComparisonAndAssignment0000.OrderStatus value)
        {
            if (__Swept.Done)
                CurrentStatus__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref CurrentStatus__state, ref CurrentStatus__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<global::EnumComparisonAndAssignment0000.OrderStatus>? CurrentStatus__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref CurrentStatus__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<global::EnumComparisonAndAssignment0000.OrderStatus>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref global::EnumComparisonAndAssignment0000.OrderStatus CurrentStatus__out(out global::System.Runtime.CompilerServices.StrongBox<global::EnumComparisonAndAssignment0000.OrderStatus>? spill)
        {
            spill = CurrentStatus__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref CurrentStatus__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R CurrentStatus__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<global::EnumComparisonAndAssignment0000.OrderStatus>? spill)
        {
            if (spill != null)
                CurrentStatus__set(spill.Value!);
            return result;
        }

        public static void Main()
        {
            __Load();
#line (33, 5) - (33, 50) 12 "enum_comparison_and_assignment_0000.spy"
            global::Sharpy.Builtins.Print(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.GetStatusDescription(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.CurrentStatus));
#line (34, 5) - (34, 38) 12 "enum_comparison_and_assignment_0000.spy"
            global::Sharpy.Builtins.Print(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.CanCancel(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.CurrentStatus));
#line (36, 5) - (36, 44) 12 "enum_comparison_and_assignment_0000.spy"
            CurrentStatus__set(global::EnumComparisonAndAssignment0000.OrderStatus.PROCESSING);
#line (37, 5) - (37, 50) 12 "enum_comparison_and_assignment_0000.spy"
            global::Sharpy.Builtins.Print(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.GetStatusDescription(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.CurrentStatus));
#line (38, 5) - (38, 38) 12 "enum_comparison_and_assignment_0000.spy"
            global::Sharpy.Builtins.Print(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.CanCancel(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.CurrentStatus));
#line (40, 5) - (40, 41) 12 "enum_comparison_and_assignment_0000.spy"
            CurrentStatus__set(global::EnumComparisonAndAssignment0000.OrderStatus.SHIPPED);
#line (41, 5) - (41, 50) 12 "enum_comparison_and_assignment_0000.spy"
            global::Sharpy.Builtins.Print(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.GetStatusDescription(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.CurrentStatus));
#line (42, 5) - (42, 38) 12 "enum_comparison_and_assignment_0000.spy"
            global::Sharpy.Builtins.Print(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.CanCancel(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.CurrentStatus));
#line (44, 5) - (44, 43) 12 "enum_comparison_and_assignment_0000.spy"
            CurrentStatus__set(global::EnumComparisonAndAssignment0000.OrderStatus.DELIVERED);
#line (45, 5) - (45, 50) 12 "enum_comparison_and_assignment_0000.spy"
            global::Sharpy.Builtins.Print(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.GetStatusDescription(global::EnumComparisonAndAssignment0000.EnumComparisonAndAssignment0000Module.CurrentStatus));
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref CurrentStatus__state, "current_status", CurrentStatus__init);
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

    [global::Sharpy.SharpyModuleType("__main__", "OrderStatus")]
    public enum OrderStatus
    {
        PENDING = 0,
        PROCESSING = 1,
        SHIPPED = 2,
        DELIVERED = 3,
        CANCELLED = 4
    }
}
#line default
