#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.InventoryWarehouseManagement
{
    public static class Program
    {
        public static void Main()
        {
#line 61 "inventory_warehouse_management.spy"
            var item1 = new InventoryItem("Widgets", 5, 10, 100);
#line 62 "inventory_warehouse_management.spy"
            var item2 = new InventoryItem("Gadgets", 0, 15, 80);
#line 63 "inventory_warehouse_management.spy"
            var item3 = new InventoryItem("Tools", 100, 20, 100);
#line 65 "inventory_warehouse_management.spy"
            var manager = new WarehouseManager();
#line 67 "inventory_warehouse_management.spy"
            manager.AnalyzeItem(item1);
#line 68 "inventory_warehouse_management.spy"
            manager.AnalyzeItem(item2);
#line 69 "inventory_warehouse_management.spy"
            manager.AnalyzeItem(item3);
#line 71 "inventory_warehouse_management.spy"
            global::Sharpy.Core.Exports.Print(manager.LowStockCount);
#line 72 "inventory_warehouse_management.spy"
            global::Sharpy.Core.Exports.Print(manager.EmptyStockCount);
#line 73 "inventory_warehouse_management.spy"
            global::Sharpy.Core.Exports.Print(manager.OverstockedCount);
#line 75 "inventory_warehouse_management.spy"
            var success = item1.AddStock(50);
#line 76 "inventory_warehouse_management.spy"
            global::Sharpy.Core.Exports.Print(success);
#line 77 "inventory_warehouse_management.spy"
            global::Sharpy.Core.Exports.Print(item1.Quantity);
#line 79 "inventory_warehouse_management.spy"
            success = item3.AddStock(10);
#line 80 "inventory_warehouse_management.spy"
            global::Sharpy.Core.Exports.Print(success);
#line 82 "inventory_warehouse_management.spy"
            success = item2.RemoveStock(5);
#line 83 "inventory_warehouse_management.spy"
            global::Sharpy.Core.Exports.Print(success);
        }
    }

    public class InventoryItem
    {
        public string Name;
        public int Quantity;
        public int ReorderLevel;
        public int MaxCapacity;
        public int CheckStatus()
        {
#line 16 "inventory_warehouse_management.spy"
            if (this.Quantity == 0)
            {
#line 17 "inventory_warehouse_management.spy"
                return 0;
            }
            else if (this.Quantity <= this.ReorderLevel)
            {
#line 19 "inventory_warehouse_management.spy"
                return 1;
            }
            else if (this.Quantity >= this.MaxCapacity)
            {
#line 21 "inventory_warehouse_management.spy"
                return 2;
            }
            else
            {
#line 23 "inventory_warehouse_management.spy"
                return 3;
            }
        }

        public bool AddStock(int amount)
        {
#line 26 "inventory_warehouse_management.spy"
            var newQuantity = this.Quantity + amount;
#line 27 "inventory_warehouse_management.spy"
            if (newQuantity > this.MaxCapacity)
            {
#line 28 "inventory_warehouse_management.spy"
                return false;
            }
            else
            {
#line 30 "inventory_warehouse_management.spy"
                this.Quantity = newQuantity;
#line 31 "inventory_warehouse_management.spy"
                return true;
            }
        }

        public bool RemoveStock(int amount)
        {
#line 34 "inventory_warehouse_management.spy"
            var newQuantity = this.Quantity - amount;
#line 35 "inventory_warehouse_management.spy"
            if (newQuantity < 0)
            {
#line 36 "inventory_warehouse_management.spy"
                return false;
            }
            else
            {
#line 38 "inventory_warehouse_management.spy"
                this.Quantity = newQuantity;
#line 39 "inventory_warehouse_management.spy"
                return true;
            }
        }

        public InventoryItem(string name, int quantity, int reorderLevel, int maxCapacity)
        {
#line 10 "inventory_warehouse_management.spy"
            this.Name = name;
#line 11 "inventory_warehouse_management.spy"
            this.Quantity = quantity;
#line 12 "inventory_warehouse_management.spy"
            this.ReorderLevel = reorderLevel;
#line 13 "inventory_warehouse_management.spy"
            this.MaxCapacity = maxCapacity;
        }
    }

    public class WarehouseManager
    {
        public int LowStockCount;
        public int EmptyStockCount;
        public int OverstockedCount;
        public void AnalyzeItem(InventoryItem item)
        {
#line 52 "inventory_warehouse_management.spy"
            var status = item.CheckStatus();
#line 53 "inventory_warehouse_management.spy"
            if (status == 0)
            {
#line 54 "inventory_warehouse_management.spy"
                this.EmptyStockCount = this.EmptyStockCount + 1;
            }
            else if (status == 1)
            {
#line 56 "inventory_warehouse_management.spy"
                this.LowStockCount = this.LowStockCount + 1;
            }
            else if (status == 2)
            {
#line 58 "inventory_warehouse_management.spy"
                this.OverstockedCount = this.OverstockedCount + 1;
            }
        }

        public WarehouseManager()
        {
#line 47 "inventory_warehouse_management.spy"
            this.LowStockCount = 0;
#line 48 "inventory_warehouse_management.spy"
            this.EmptyStockCount = 0;
#line 49 "inventory_warehouse_management.spy"
            this.OverstockedCount = 0;
        }
    }
}
