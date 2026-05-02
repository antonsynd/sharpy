// Snapshot: Multiple interface implementation
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class InterfaceImplementation0003
{
    public interface IPaymentProcessor
    {
        bool ProcessPayment(int amount);
        int GetFee();
    }

    public interface IRefundable
    {
        bool ProcessRefund(int amount);
    }

    public class CreditCardProcessor : IPaymentProcessor, IRefundable
    {
        public int Fee;
        public int TotalProcessed;
        public virtual bool ProcessPayment(int amount)
#line 18 "interface_implementation_0003.spy"
        {
#line (19, 9) - (19, 39) 1 "interface_implementation_0003.spy"
            this.TotalProcessed = this.TotalProcessed + amount;
#line (20, 9) - (20, 21) 1 "interface_implementation_0003.spy"
            return true;
        }

        public virtual int GetFee()
#line 22 "interface_implementation_0003.spy"
        {
#line (23, 9) - (23, 25) 1 "interface_implementation_0003.spy"
            return this.Fee;
        }

        public virtual bool ProcessRefund(int amount)
#line 25 "interface_implementation_0003.spy"
        {
#line (26, 9) - (26, 39) 1 "interface_implementation_0003.spy"
            this.TotalProcessed = this.TotalProcessed - amount;
#line (27, 9) - (27, 21) 1 "interface_implementation_0003.spy"
            return true;
        }

        public CreditCardProcessor()
#line 14 "interface_implementation_0003.spy"
        {
#line (15, 9) - (15, 21) 1 "interface_implementation_0003.spy"
            this.Fee = 3;
#line (16, 9) - (16, 33) 1 "interface_implementation_0003.spy"
            this.TotalProcessed = 0;
        }
    }

    public class CashProcessor : IPaymentProcessor
    {
        public int Fee;
        public virtual bool ProcessPayment(int amount)
#line 35 "interface_implementation_0003.spy"
        {
#line (36, 9) - (36, 21) 1 "interface_implementation_0003.spy"
            return true;
        }

        public virtual int GetFee()
#line 38 "interface_implementation_0003.spy"
        {
#line (39, 9) - (39, 25) 1 "interface_implementation_0003.spy"
            return this.Fee;
        }

        public CashProcessor()
#line 32 "interface_implementation_0003.spy"
        {
#line (33, 9) - (33, 21) 1 "interface_implementation_0003.spy"
            this.Fee = 0;
        }
    }

    public static void Main()
    {
#line (42, 5) - (42, 31) 1 "interface_implementation_0003.spy"
        var cc = new CreditCardProcessor();
#line (43, 5) - (43, 27) 1 "interface_implementation_0003.spy"
        var cash = new CashProcessor();
#line (45, 5) - (45, 28) 1 "interface_implementation_0003.spy"
        cc.ProcessPayment(100);
#line (46, 5) - (46, 30) 1 "interface_implementation_0003.spy"
        global::Sharpy.Builtins.Print(cc.TotalProcessed);
#line (47, 5) - (47, 24) 1 "interface_implementation_0003.spy"
        global::Sharpy.Builtins.Print(cc.GetFee());
#line (49, 5) - (49, 26) 1 "interface_implementation_0003.spy"
        cc.ProcessRefund(25);
#line (50, 5) - (50, 30) 1 "interface_implementation_0003.spy"
        global::Sharpy.Builtins.Print(cc.TotalProcessed);
#line (52, 5) - (52, 29) 1 "interface_implementation_0003.spy"
        cash.ProcessPayment(50);
#line (53, 5) - (53, 26) 1 "interface_implementation_0003.spy"
        global::Sharpy.Builtins.Print(cash.GetFee());
    }
}
