// Snapshot: Enum comparison and variable assignment
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class EnumComparisonAndAssignment0000
{
    public enum OrderStatus
    {
        Pending = 0,
        Processing = 1,
        Shipped = 2,
        Delivered = 3,
        Cancelled = 4
    }

    public static string GetStatusDescription(OrderStatus status)
    {
#line 10 "enum_comparison_and_assignment_0000.spy"
        if (status == OrderStatus.Pending)
        {
#line 11 "enum_comparison_and_assignment_0000.spy"
            return "Order is pending";
        }
        else if (status == OrderStatus.Processing)
        {
#line 13 "enum_comparison_and_assignment_0000.spy"
            return "Order is being processed";
        }
        else if (status == OrderStatus.Shipped)
        {
#line 15 "enum_comparison_and_assignment_0000.spy"
            return "Order has been shipped";
        }
        else if (status == OrderStatus.Delivered)
        {
#line 17 "enum_comparison_and_assignment_0000.spy"
            return "Order delivered successfully";
        }
        else
        {
#line 19 "enum_comparison_and_assignment_0000.spy"
            return "Order was cancelled";
        }
    }

    public static bool CanCancel(OrderStatus status)
    {
#line 22 "enum_comparison_and_assignment_0000.spy"
        if (status == OrderStatus.Pending)
        {
#line 23 "enum_comparison_and_assignment_0000.spy"
            return true;
        }
        else if (status == OrderStatus.Processing)
        {
#line 25 "enum_comparison_and_assignment_0000.spy"
            return true;
        }
        else
        {
#line 27 "enum_comparison_and_assignment_0000.spy"
            return false;
        }
    }

    public static OrderStatus CurrentStatus = OrderStatus.Pending;
    public static void Main()
    {
#line 33 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
#line 34 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(CanCancel(CurrentStatus));
#line 36 "enum_comparison_and_assignment_0000.spy"
        CurrentStatus = OrderStatus.Processing;
#line 37 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
#line 38 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(CanCancel(CurrentStatus));
#line 40 "enum_comparison_and_assignment_0000.spy"
        CurrentStatus = OrderStatus.Shipped;
#line 41 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
#line 42 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(CanCancel(CurrentStatus));
#line 44 "enum_comparison_and_assignment_0000.spy"
        CurrentStatus = OrderStatus.Delivered;
#line 45 "enum_comparison_and_assignment_0000.spy"
        global::Sharpy.Builtins.Print(GetStatusDescription(CurrentStatus));
    }
}
