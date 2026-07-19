// Snapshot: Access modifiers (public, private, protected)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AccessModifiers
{
    public abstract class Account
    {
        public double Balance;
        public int AccountId;
        private void LogTransaction(double amount)
#line 14 "access_modifiers.spy"
        {
#line (15, 9) - (15, 20) 12 "access_modifiers.spy"
            global::Sharpy.Builtins.Print(1000);
#line hidden
        }

        protected bool ValidateAmount(double amount)
#line 18 "access_modifiers.spy"
        {
#line (19, 9) - (21, 1) 12 "access_modifiers.spy"
            if (amount > 0.0d)
#line hidden
            {
#line (20, 13) - (20, 25) 16 "access_modifiers.spy"
                return true;
#line hidden
            }

#line (21, 9) - (21, 22) 12 "access_modifiers.spy"
            return false;
#line hidden
        }

        internal int GetInternalId()
#line 24 "access_modifiers.spy"
        {
#line (25, 9) - (25, 32) 12 "access_modifiers.spy"
            return this.AccountId;
#line hidden
        }

        public virtual void Deposit(double amount)
#line 28 "access_modifiers.spy"
        {
#line (29, 9) - (33, 1) 12 "access_modifiers.spy"
            if (this.ValidateAmount(amount))
#line hidden
            {
#line (30, 13) - (30, 35) 16 "access_modifiers.spy"
                this.Balance = this.Balance + amount;
#line (31, 13) - (31, 41) 16 "access_modifiers.spy"
                this.LogTransaction(amount);
#line hidden
            }
        }

        public abstract bool Withdraw(double amount);
        public Account(int id, double initialBalance)
#line 9 "access_modifiers.spy"
        {
#line (10, 9) - (10, 29) 12 "access_modifiers.spy"
            this.AccountId = id;
#line (11, 9) - (11, 39) 12 "access_modifiers.spy"
            this.Balance = initialBalance;
#line hidden
        }
    }

    public class SavingsAccount : Account
    {
        public double WithdrawalLimit;
        public int WithdrawalCount;
        public override bool Withdraw(double amount)
#line 47 "access_modifiers.spy"
        {
#line (48, 9) - (54, 1) 12 "access_modifiers.spy"
            if (this.ValidateAmount(amount))
#line hidden
            {
#line (49, 13) - (54, 1) 16 "access_modifiers.spy"
                if (amount <= this.WithdrawalLimit)
#line hidden
                {
#line (50, 17) - (54, 1) 20 "access_modifiers.spy"
                    if (this.Balance >= amount)
#line hidden
                    {
#line (51, 21) - (51, 43) 24 "access_modifiers.spy"
                        this.Balance = this.Balance - amount;
#line (52, 21) - (52, 47) 24 "access_modifiers.spy"
                        this.WithdrawalCount = this.WithdrawalCount + 1;
#line (53, 21) - (53, 33) 24 "access_modifiers.spy"
                        return true;
#line hidden
                    }
                }
            }

#line (54, 9) - (54, 22) 12 "access_modifiers.spy"
            return false;
#line hidden
        }

        public SavingsAccount(int id, double balance, double limit) : base(id, balance)
#line 41 "access_modifiers.spy"
        {
#line (43, 9) - (43, 38) 12 "access_modifiers.spy"
            this.WithdrawalLimit = limit;
#line (44, 9) - (44, 34) 12 "access_modifiers.spy"
            this.WithdrawalCount = 0;
#line hidden
        }
    }

    public class CheckingAccount : Account
    {
        public double OverdraftLimit;
        public override bool Withdraw(double amount)
#line 64 "access_modifiers.spy"
        {
#line (65, 9) - (70, 1) 12 "access_modifiers.spy"
            if (this.ValidateAmount(amount))
#line hidden
            {
#line (66, 13) - (66, 68) 16 "access_modifiers.spy"
                double available = this.Balance + this.OverdraftLimit;
#line (67, 13) - (70, 1) 16 "access_modifiers.spy"
                if (amount <= available)
#line hidden
                {
#line (68, 17) - (68, 39) 20 "access_modifiers.spy"
                    this.Balance = this.Balance - amount;
#line (69, 17) - (69, 29) 20 "access_modifiers.spy"
                    return true;
#line hidden
                }
            }

#line (70, 9) - (70, 22) 12 "access_modifiers.spy"
            return false;
#line hidden
        }

        public override void Deposit(double amount)
#line 73 "access_modifiers.spy"
        {
#line (74, 9) - (74, 32) 12 "access_modifiers.spy"
            base.Deposit(amount);
#line (75, 9) - (75, 20) 12 "access_modifiers.spy"
            global::Sharpy.Builtins.Print(2000);
#line hidden
        }

        public CheckingAccount(int id, double balance, double overdraft) : base(id, balance)
#line 59 "access_modifiers.spy"
        {
#line (61, 9) - (61, 41) 12 "access_modifiers.spy"
            this.OverdraftLimit = overdraft;
#line hidden
        }
    }

    public static void Main()
    {
#line (78, 5) - (78, 67) 8 "access_modifiers.spy"
        SavingsAccount savings = new SavingsAccount(101, 5000.0d, 1000.0d);
#line (79, 5) - (79, 30) 8 "access_modifiers.spy"
        global::Sharpy.Builtins.Print(savings.AccountId);
#line (80, 5) - (80, 37) 8 "access_modifiers.spy"
        global::Sharpy.Builtins.Print(savings.GetInternalId());
#line (82, 5) - (82, 27) 8 "access_modifiers.spy"
        savings.Deposit(500.0d);
#line (83, 5) - (83, 27) 8 "access_modifiers.spy"
        global::Sharpy.Builtins.Print(savings.Balance);
#line (85, 5) - (85, 45) 8 "access_modifiers.spy"
        bool success = savings.Withdraw(800.0d);
#line (86, 5) - (89, 1) 8 "access_modifiers.spy"
        if (success)
#line hidden
        {
#line (87, 9) - (87, 20) 12 "access_modifiers.spy"
            global::Sharpy.Builtins.Print(4200);
#line hidden
        }

#line (89, 5) - (89, 36) 8 "access_modifiers.spy"
        global::Sharpy.Builtins.Print(savings.WithdrawalCount);
#line (91, 5) - (91, 69) 8 "access_modifiers.spy"
        CheckingAccount checking = new CheckingAccount(102, 1000.0d, 500.0d);
#line (92, 5) - (92, 28) 8 "access_modifiers.spy"
        checking.Deposit(250.0d);
#line (93, 5) - (93, 28) 8 "access_modifiers.spy"
        global::Sharpy.Builtins.Print(checking.Balance);
#line (95, 5) - (95, 56) 8 "access_modifiers.spy"
        bool withdrawSuccess = checking.Withdraw(1100.0d);
#line (96, 5) - (98, 1) 8 "access_modifiers.spy"
        if (withdrawSuccess)
#line hidden
        {
#line (97, 9) - (97, 19) 12 "access_modifiers.spy"
            global::Sharpy.Builtins.Print(150);
#line hidden
        }
    }
}
#line default
