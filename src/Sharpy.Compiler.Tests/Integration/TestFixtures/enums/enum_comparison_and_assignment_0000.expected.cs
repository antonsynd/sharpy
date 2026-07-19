// Snapshot: Enum comparison and variable assignment
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class EnumComparisonAndAssignment0000
{
    public enum OrderStatus
    {
        PENDING = 0,
        PROCESSING = 1,
        SHIPPED = 2,
        DELIVERED = 3,
        CANCELLED = 4
    }

    public static string GetStatusDescription(OrderStatus status)
    {
#line (10, 5) - (21, 1) 8 "enum_comparison_and_assignment_0000.spy"
        if (status == OrderStatus.PENDING)
#line hidden
        {
#line (11, 9) - (11, 35) 12 "enum_comparison_and_assignment_0000.spy"
            return "Order is pending";
#line hidden
        }
        else if (status == OrderStatus.PROCESSING)
        {
#line (13, 9) - (13, 43) 12 "enum_comparison_and_assignment_0000.spy"
            return "Order is being processed";
#line hidden
        }
        else if (status == OrderStatus.SHIPPED)
        {
#line (15, 9) - (15, 41) 12 "enum_comparison_and_assignment_0000.spy"
            return "Order has been shipped";
#line hidden
        }
        else if (status == OrderStatus.DELIVERED)
        {
#line (17, 9) - (17, 47) 12 "enum_comparison_and_assignment_0000.spy"
            return "Order delivered successfully";
#line hidden
        }
        else
        {
#line (19, 9) - (19, 38) 12 "enum_comparison_and_assignment_0000.spy"
            return "Order was cancelled";
#line hidden
        }
    }

    public static bool CanCancel(OrderStatus status)
    {
#line (22, 5) - (30, 1) 8 "enum_comparison_and_assignment_0000.spy"
        if (status == OrderStatus.PENDING)
#line hidden
        {
#line (23, 9) - (23, 21) 12 "enum_comparison_and_assignment_0000.spy"
            return true;
#line hidden
        }
        else if (status == OrderStatus.PROCESSING)
        {
#line (25, 9) - (25, 21) 12 "enum_comparison_and_assignment_0000.spy"
            return true;
#line hidden
        }
        else
        {
#line (27, 9) - (27, 22) 12 "enum_comparison_and_assignment_0000.spy"
            return false;
#line hidden
        }
    }

    public static OrderStatus CurrentStatus = OrderStatus.PENDING;
    public static void Main()
    {
#line (33, 5) - (33, 50) 8 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
#line (34, 5) - (34, 38) 8 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(CanCancel(CurrentStatus));
#line (36, 5) - (36, 44) 8 "enum_comparison_and_assignment_0000.spy"
        CurrentStatus = OrderStatus.PROCESSING;
#line (37, 5) - (37, 50) 8 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
#line (38, 5) - (38, 38) 8 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(CanCancel(CurrentStatus));
#line (40, 5) - (40, 41) 8 "enum_comparison_and_assignment_0000.spy"
        CurrentStatus = OrderStatus.SHIPPED;
#line (41, 5) - (41, 50) 8 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
#line (42, 5) - (42, 38) 8 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(CanCancel(CurrentStatus));
#line (44, 5) - (44, 43) 8 "enum_comparison_and_assignment_0000.spy"
        CurrentStatus = OrderStatus.DELIVERED;
#line (45, 5) - (45, 50) 8 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
#line hidden
    }
}
#line default
