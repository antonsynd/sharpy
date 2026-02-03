#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassInstanceMethods0007
{
    public static class Program
    {
        public static void Main()
        {
#line 30 "class_instance_methods_0007.spy"
            var account1 = new BankAccount(101, 500);
#line 31 "class_instance_methods_0007.spy"
            var account2 = new BankAccount(102, 200);
#line 33 "class_instance_methods_0007.spy"
            global::Sharpy.Core.Exports.Print(account1.GetBalance());
#line 34 "class_instance_methods_0007.spy"
            global::Sharpy.Core.Exports.Print(account2.GetBalance());
#line 36 "class_instance_methods_0007.spy"
            account1.Deposit(100);
#line 37 "class_instance_methods_0007.spy"
            global::Sharpy.Core.Exports.Print(account1.GetBalance());
#line 39 "class_instance_methods_0007.spy"
            var success = account1.Withdraw(150);
#line 40 "class_instance_methods_0007.spy"
            global::Sharpy.Core.Exports.Print(success);
#line 41 "class_instance_methods_0007.spy"
            global::Sharpy.Core.Exports.Print(account1.GetBalance());
#line 43 "class_instance_methods_0007.spy"
            var transferred = account1.TransferTo(account2, 200);
#line 44 "class_instance_methods_0007.spy"
            global::Sharpy.Core.Exports.Print(transferred);
#line 45 "class_instance_methods_0007.spy"
            global::Sharpy.Core.Exports.Print(account1.GetBalance());
#line 46 "class_instance_methods_0007.spy"
            global::Sharpy.Core.Exports.Print(account2.GetBalance());
        }
    }

    public class BankAccount
    {
        public int Balance;
        public int AccountId;
        public void Deposit(int amount)
        {
#line 12 "class_instance_methods_0007.spy"
            this.Balance = this.Balance + amount;
        }

        public bool Withdraw(int amount)
        {
#line 15 "class_instance_methods_0007.spy"
            if (amount <= this.Balance)
            {
#line 16 "class_instance_methods_0007.spy"
                this.Balance = this.Balance - amount;
#line 17 "class_instance_methods_0007.spy"
                return true;
            }

#line 18 "class_instance_methods_0007.spy"
            return false;
        }

        public int GetBalance()
        {
#line 21 "class_instance_methods_0007.spy"
            return this.Balance;
        }

        public bool TransferTo(BankAccount other, int amount)
        {
#line 24 "class_instance_methods_0007.spy"
            if (this.Withdraw(amount))
            {
#line 25 "class_instance_methods_0007.spy"
                other.Deposit(amount);
#line 26 "class_instance_methods_0007.spy"
                return true;
            }

#line 27 "class_instance_methods_0007.spy"
            return false;
        }

        public BankAccount(int accountId, int initialBalance)
        {
#line 8 "class_instance_methods_0007.spy"
            this.AccountId = accountId;
#line 9 "class_instance_methods_0007.spy"
            this.Balance = initialBalance;
        }
    }
}
