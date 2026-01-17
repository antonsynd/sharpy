#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Source
{
    public static class Program
    {
        public class BankAccount
        {
            public int Balance;
            public string AccountName;
            public void Deposit(int amount)
            {
                this.Balance = this.Balance + amount;
            }

            public bool Withdraw(int amount)
            {
                if (amount <= this.Balance)
                {
                    this.Balance = this.Balance - amount;
                    return true;
                }

                return false;
            }

            public int GetBalance()
            {
                return this.Balance;
            }

            public bool TransferTo(BankAccount other, int amount)
            {
                if (this.Withdraw(amount))
                {
                    other.Deposit(amount);
                    return true;
                }

                return false;
            }

            public BankAccount(string name, int initial)
            {
                this.AccountName = name;
                this.Balance = initial;
            }
        }

        public static void Main()
        {
            var account1 = new BankAccount("Alice", 100);
            var account2 = new BankAccount("Bob", 50);
            global::Sharpy.Core.Exports.Print(account1.GetBalance());
            global::Sharpy.Core.Exports.Print(account2.GetBalance());
            account1.Deposit(25);
            global::Sharpy.Core.Exports.Print(account1.GetBalance());
            bool success = account1.TransferTo(account2, 75);
            global::Sharpy.Core.Exports.Print(success);
            global::Sharpy.Core.Exports.Print(account1.GetBalance());
            global::Sharpy.Core.Exports.Print(account2.GetBalance());
        }
    }
}