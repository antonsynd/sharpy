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
        {
#line 19 "interface_implementation_0003.spy"
            this.TotalProcessed = this.TotalProcessed + amount;
#line 20 "interface_implementation_0003.spy"
            return true;
        }

        public virtual int GetFee()
        {
#line 23 "interface_implementation_0003.spy"
            return this.Fee;
        }

        public virtual bool ProcessRefund(int amount)
        {
#line 26 "interface_implementation_0003.spy"
            this.TotalProcessed = this.TotalProcessed - amount;
#line 27 "interface_implementation_0003.spy"
            return true;
        }

        public CreditCardProcessor()
        {
#line 15 "interface_implementation_0003.spy"
            this.Fee = 3;
#line 16 "interface_implementation_0003.spy"
            this.TotalProcessed = 0;
        }
    }

    public class CashProcessor : IPaymentProcessor
    {
        public int Fee;
        public virtual bool ProcessPayment(int amount)
        {
#line 36 "interface_implementation_0003.spy"
            return true;
        }

        public virtual int GetFee()
        {
#line 39 "interface_implementation_0003.spy"
            return this.Fee;
        }

        public CashProcessor()
        {
#line 33 "interface_implementation_0003.spy"
            this.Fee = 0;
        }
    }

    public static void Main()
    {
#line 42 "interface_implementation_0003.spy"
        var cc = new CreditCardProcessor();
#line 43 "interface_implementation_0003.spy"
        var cash = new CashProcessor();
#line 45 "interface_implementation_0003.spy"
        cc.ProcessPayment(100);
#line 46 "interface_implementation_0003.spy"
        global::Sharpy.Builtins.Print(cc.TotalProcessed);
#line 47 "interface_implementation_0003.spy"
        global::Sharpy.Builtins.Print(cc.GetFee());
#line 49 "interface_implementation_0003.spy"
        cc.ProcessRefund(25);
#line 50 "interface_implementation_0003.spy"
        global::Sharpy.Builtins.Print(cc.TotalProcessed);
#line 52 "interface_implementation_0003.spy"
        cash.ProcessPayment(50);
#line 53 "interface_implementation_0003.spy"
        global::Sharpy.Builtins.Print(cash.GetFee());
    }
}
