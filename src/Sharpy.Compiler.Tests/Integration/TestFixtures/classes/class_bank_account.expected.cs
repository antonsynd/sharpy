// Snapshot: Class with instance methods and field mutation
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ClassBankAccount
{
    public class BankAccount
    {
        public int Balance;
        public string AccountHolder;
        public bool IsActive;
        public void Deposit(int amount)
#line 13 "class_bank_account.spy"
        {
#line (14, 9) - (17, 1) 12 "class_bank_account.spy"
            if (this.IsActive)
#line hidden
            {
#line (15, 13) - (15, 35) 16 "class_bank_account.spy"
                this.Balance = this.Balance + amount;
#line hidden
            }
        }

        public bool Withdraw(int amount)
#line 17 "class_bank_account.spy"
        {
#line (18, 9) - (21, 1) 12 "class_bank_account.spy"
            if (this.IsActive && this.Balance >= amount)
#line hidden
            {
#line (19, 13) - (19, 35) 16 "class_bank_account.spy"
                this.Balance = this.Balance - amount;
#line (20, 13) - (20, 25) 16 "class_bank_account.spy"
                return true;
#line hidden
            }

#line (21, 9) - (21, 22) 12 "class_bank_account.spy"
            return false;
#line hidden
        }

        public void Deactivate()
#line 23 "class_bank_account.spy"
        {
#line (24, 9) - (24, 31) 12 "class_bank_account.spy"
            this.IsActive = false;
#line hidden
        }

        public int GetBalance()
#line 26 "class_bank_account.spy"
        {
#line (27, 9) - (27, 29) 12 "class_bank_account.spy"
            return this.Balance;
#line hidden
        }

        public BankAccount(string holder, int initialBalance)
#line 8 "class_bank_account.spy"
        {
#line (9, 9) - (9, 37) 12 "class_bank_account.spy"
            this.AccountHolder = holder;
#line (10, 9) - (10, 39) 12 "class_bank_account.spy"
            this.Balance = initialBalance;
#line (11, 9) - (11, 30) 12 "class_bank_account.spy"
            this.IsActive = true;
#line hidden
        }
    }

    public static void Main()
    {
#line (30, 5) - (30, 41) 8 "class_bank_account.spy"
        var account = new BankAccount("Alice", 1000);
#line (31, 5) - (31, 33) 8 "class_bank_account.spy"
        global::Sharpy.Builtins.Print(account.GetBalance());
#line (33, 5) - (33, 25) 8 "class_bank_account.spy"
        account.Deposit(500);
#line (34, 5) - (34, 33) 8 "class_bank_account.spy"
        global::Sharpy.Builtins.Print(account.GetBalance());
#line (36, 5) - (36, 43) 8 "class_bank_account.spy"
        bool success = account.Withdraw(300);
#line (37, 5) - (37, 19) 8 "class_bank_account.spy"
        global::Sharpy.Builtins.Print(success);
#line (38, 5) - (38, 33) 8 "class_bank_account.spy"
        global::Sharpy.Builtins.Print(account.GetBalance());
#line (40, 5) - (40, 25) 8 "class_bank_account.spy"
        account.Deactivate();
#line (41, 5) - (41, 25) 8 "class_bank_account.spy"
        account.Deposit(100);
#line (42, 5) - (42, 33) 8 "class_bank_account.spy"
        global::Sharpy.Builtins.Print(account.GetBalance());
#line hidden
    }
}
#line default
