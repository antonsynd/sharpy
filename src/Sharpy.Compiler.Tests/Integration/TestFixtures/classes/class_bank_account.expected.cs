// Snapshot: Class with instance methods and field mutation
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class ClassBankAccount
{
    public class BankAccount
    {
        public int Balance;
        public string AccountHolder;
        public bool IsActive;
        public void Deposit(int amount)
        {
#line 14 "class_bank_account.spy"
            if (this.IsActive)
            {
#line 15 "class_bank_account.spy"
                this.Balance = this.Balance + amount;
            }
        }

        public bool Withdraw(int amount)
        {
#line 18 "class_bank_account.spy"
            if (this.IsActive && this.Balance >= amount)
            {
#line 19 "class_bank_account.spy"
                this.Balance = this.Balance - amount;
#line 20 "class_bank_account.spy"
                return true;
            }

#line 21 "class_bank_account.spy"
            return false;
        }

        public void Deactivate()
        {
#line 24 "class_bank_account.spy"
            this.IsActive = false;
        }

        public int GetBalance()
        {
#line 27 "class_bank_account.spy"
            return this.Balance;
        }

        public BankAccount(string holder, int initialBalance)
        {
#line 9 "class_bank_account.spy"
            this.AccountHolder = holder;
#line 10 "class_bank_account.spy"
            this.Balance = initialBalance;
#line 11 "class_bank_account.spy"
            this.IsActive = true;
        }
    }

    public static void Main()
    {
#line 30 "class_bank_account.spy"
        var account = new BankAccount("Alice", 1000);
#line 31 "class_bank_account.spy"
        global::Sharpy.Builtins.Print(account.GetBalance());
#line 33 "class_bank_account.spy"
        account.Deposit(500);
#line 34 "class_bank_account.spy"
        global::Sharpy.Builtins.Print(account.GetBalance());
#line 36 "class_bank_account.spy"
        bool success = account.Withdraw(300);
#line 37 "class_bank_account.spy"
        global::Sharpy.Builtins.Print(success);
#line 38 "class_bank_account.spy"
        global::Sharpy.Builtins.Print(account.GetBalance());
#line 40 "class_bank_account.spy"
        account.Deactivate();
#line 41 "class_bank_account.spy"
        account.Deposit(100);
#line 42 "class_bank_account.spy"
        global::Sharpy.Builtins.Print(account.GetBalance());
    }
}
