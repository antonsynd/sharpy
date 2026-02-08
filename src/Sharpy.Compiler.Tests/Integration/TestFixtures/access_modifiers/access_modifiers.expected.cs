// Snapshot: Access modifiers (public, private, protected)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class AccessModifiers
{
    public abstract class Account
    {
        public double Balance;
        public int AccountId;
        private void LogTransaction(double amount)
        {
#line 15 "access_modifiers.spy"
            global::Sharpy.Builtins.Print(1000);
        }

        protected bool ValidateAmount(double amount)
        {
#line 19 "access_modifiers.spy"
            if (amount > 0)
            {
#line 20 "access_modifiers.spy"
                return true;
            }

#line 21 "access_modifiers.spy"
            return false;
        }

        internal int GetInternalId()
        {
#line 25 "access_modifiers.spy"
            return this.AccountId;
        }

        public virtual void Deposit(double amount)
        {
#line 29 "access_modifiers.spy"
            if (this.ValidateAmount(amount))
            {
#line 30 "access_modifiers.spy"
                this.Balance = this.Balance + amount;
#line 31 "access_modifiers.spy"
                this.LogTransaction(amount);
            }
        }

        public abstract bool Withdraw(double amount);
        public Account(int id, double initialBalance)
        {
#line 10 "access_modifiers.spy"
            this.AccountId = id;
#line 11 "access_modifiers.spy"
            this.Balance = initialBalance;
        }
    }

    public class SavingsAccount : Account
    {
        public double WithdrawalLimit;
        public int WithdrawalCount;
        public override bool Withdraw(double amount)
        {
#line 48 "access_modifiers.spy"
            if (this.ValidateAmount(amount))
            {
#line 49 "access_modifiers.spy"
                if (amount <= this.WithdrawalLimit)
                {
#line 50 "access_modifiers.spy"
                    if (this.Balance >= amount)
                    {
#line 51 "access_modifiers.spy"
                        this.Balance = this.Balance - amount;
#line 52 "access_modifiers.spy"
                        this.WithdrawalCount = this.WithdrawalCount + 1;
#line 53 "access_modifiers.spy"
                        return true;
                    }
                }
            }

#line 54 "access_modifiers.spy"
            return false;
        }

        public SavingsAccount(int id, double balance, double limit) : base(id, balance)
        {
#line 43 "access_modifiers.spy"
            this.WithdrawalLimit = limit;
#line 44 "access_modifiers.spy"
            this.WithdrawalCount = 0;
        }
    }

    public class CheckingAccount : Account
    {
        public double OverdraftLimit;
        public override bool Withdraw(double amount)
        {
#line 65 "access_modifiers.spy"
            if (this.ValidateAmount(amount))
            {
#line 66 "access_modifiers.spy"
                double available = this.Balance + this.OverdraftLimit;
#line 67 "access_modifiers.spy"
                if (amount <= available)
                {
#line 68 "access_modifiers.spy"
                    this.Balance = this.Balance - amount;
#line 69 "access_modifiers.spy"
                    return true;
                }
            }

#line 70 "access_modifiers.spy"
            return false;
        }

        public override void Deposit(double amount)
        {
#line 74 "access_modifiers.spy"
            base.Deposit(amount);
#line 75 "access_modifiers.spy"
            global::Sharpy.Builtins.Print(2000);
        }

        public CheckingAccount(int id, double balance, double overdraft) : base(id, balance)
        {
#line 61 "access_modifiers.spy"
            this.OverdraftLimit = overdraft;
        }
    }

    public static void Main()
    {
#line 78 "access_modifiers.spy"
        SavingsAccount savings = new SavingsAccount(101, 5000, 1000);
#line 79 "access_modifiers.spy"
        global::Sharpy.Builtins.Print(savings.AccountId);
#line 80 "access_modifiers.spy"
        global::Sharpy.Builtins.Print(savings.GetInternalId());
#line 82 "access_modifiers.spy"
        savings.Deposit(500);
#line 83 "access_modifiers.spy"
        global::Sharpy.Builtins.Print(savings.Balance);
#line 85 "access_modifiers.spy"
        bool success = savings.Withdraw(800);
#line 86 "access_modifiers.spy"
        if (success)
        {
#line 87 "access_modifiers.spy"
            global::Sharpy.Builtins.Print(4200);
        }

#line 89 "access_modifiers.spy"
        global::Sharpy.Builtins.Print(savings.WithdrawalCount);
#line 91 "access_modifiers.spy"
        CheckingAccount checking = new CheckingAccount(102, 1000, 500);
#line 92 "access_modifiers.spy"
        checking.Deposit(250);
#line 93 "access_modifiers.spy"
        global::Sharpy.Builtins.Print(checking.Balance);
#line 95 "access_modifiers.spy"
        bool withdrawSuccess = checking.Withdraw(1100);
#line 96 "access_modifiers.spy"
        if (withdrawSuccess)
        {
#line 97 "access_modifiers.spy"
            global::Sharpy.Builtins.Print(150);
        }
    }
}
