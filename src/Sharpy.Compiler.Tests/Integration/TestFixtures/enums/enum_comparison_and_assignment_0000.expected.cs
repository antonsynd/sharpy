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
#line (10, 5) - (21, 1) 1 "enum_comparison_and_assignment_0000.spy"
        if (status == OrderStatus.PENDING)
        {
#line (11, 9) - (11, 35) 1 "enum_comparison_and_assignment_0000.spy"
            return "Order is pending";
        }
        else if (status == OrderStatus.PROCESSING)
        {
#line (13, 9) - (13, 43) 1 "enum_comparison_and_assignment_0000.spy"
            return "Order is being processed";
        }
        else if (status == OrderStatus.SHIPPED)
        {
#line (15, 9) - (15, 41) 1 "enum_comparison_and_assignment_0000.spy"
            return "Order has been shipped";
        }
        else if (status == OrderStatus.DELIVERED)
        {
#line (17, 9) - (17, 47) 1 "enum_comparison_and_assignment_0000.spy"
            return "Order delivered successfully";
        }
        else
        {
#line (19, 9) - (19, 38) 1 "enum_comparison_and_assignment_0000.spy"
            return "Order was cancelled";
        }
    }

    public static bool CanCancel(OrderStatus status)
    {
#line (22, 5) - (30, 1) 1 "enum_comparison_and_assignment_0000.spy"
        if (status == OrderStatus.PENDING)
        {
#line (23, 9) - (23, 21) 1 "enum_comparison_and_assignment_0000.spy"
            return true;
        }
        else if (status == OrderStatus.PROCESSING)
        {
#line (25, 9) - (25, 21) 1 "enum_comparison_and_assignment_0000.spy"
            return true;
        }
        else
        {
#line (27, 9) - (27, 22) 1 "enum_comparison_and_assignment_0000.spy"
            return false;
        }
    }

    public static OrderStatus CurrentStatus = OrderStatus.PENDING;
    public static void Main()
    {
#line (33, 5) - (33, 50) 1 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
#line (34, 5) - (34, 38) 1 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(CanCancel(CurrentStatus));
#line (36, 5) - (36, 44) 1 "enum_comparison_and_assignment_0000.spy"
        CurrentStatus = OrderStatus.PROCESSING;
#line (37, 5) - (37, 50) 1 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
#line (38, 5) - (38, 38) 1 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(CanCancel(CurrentStatus));
#line (40, 5) - (40, 41) 1 "enum_comparison_and_assignment_0000.spy"
        CurrentStatus = OrderStatus.SHIPPED;
#line (41, 5) - (41, 50) 1 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
#line (42, 5) - (42, 38) 1 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(CanCancel(CurrentStatus));
#line (44, 5) - (44, 43) 1 "enum_comparison_and_assignment_0000.spy"
        CurrentStatus = OrderStatus.DELIVERED;
#line (45, 5) - (45, 50) 1 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
    }
}
