#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Source
{
    public static class Program
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
            if (status == Program.OrderStatus.Pending)
            {
                return "Order is pending";
            }
            else if (status == Program.OrderStatus.Processing)
            {
                return "Order is being processed";
            }
            else if (status == Program.OrderStatus.Shipped)
            {
                return "Order has been shipped";
            }
            else if (status == Program.OrderStatus.Delivered)
            {
                return "Order delivered successfully";
            }
            else
            {
                return "Order was cancelled";
            }
        }

        public static bool CanCancel(OrderStatus status)
        {
            if (status == Program.OrderStatus.Pending)
            {
                return true;
            }
            else if (status == Program.OrderStatus.Processing)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static OrderStatus CurrentStatus = Program.OrderStatus.Pending;
        public static void Main()
        {
            global::Sharpy.Core.Exports.Print(GetStatusDescription(CurrentStatus));
            global::Sharpy.Core.Exports.Print(CanCancel(CurrentStatus));
            CurrentStatus = Program.OrderStatus.Processing;
            global::Sharpy.Core.Exports.Print(GetStatusDescription(CurrentStatus));
            global::Sharpy.Core.Exports.Print(CanCancel(CurrentStatus));
            CurrentStatus = Program.OrderStatus.Shipped;
            global::Sharpy.Core.Exports.Print(GetStatusDescription(CurrentStatus));
            global::Sharpy.Core.Exports.Print(CanCancel(CurrentStatus));
            CurrentStatus = Program.OrderStatus.Delivered;
            global::Sharpy.Core.Exports.Print(GetStatusDescription(CurrentStatus));
        }
    }
}