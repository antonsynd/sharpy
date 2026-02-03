#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassInstanceMethods0000
{
    public static class Program
    {
        public static void Main()
        {
#line 20 "class_instance_methods_0000.spy"
            var calc = new Calculator(20);
#line 21 "class_instance_methods_0000.spy"
            global::Sharpy.Core.Exports.Print(calc.GetValue());
#line 22 "class_instance_methods_0000.spy"
            global::Sharpy.Core.Exports.Print(calc.Add(15));
#line 23 "class_instance_methods_0000.spy"
            global::Sharpy.Core.Exports.Print(calc.Subtract(10));
#line 24 "class_instance_methods_0000.spy"
            global::Sharpy.Core.Exports.Print(calc.GetValue());
        }
    }

    public class Calculator
    {
        public int CurrentValue;
        public int Add(int n)
        {
#line 9 "class_instance_methods_0000.spy"
            this.CurrentValue = this.CurrentValue + n;
#line 10 "class_instance_methods_0000.spy"
            return this.CurrentValue;
        }

        public int Subtract(int n)
        {
#line 13 "class_instance_methods_0000.spy"
            this.CurrentValue = this.CurrentValue - n;
#line 14 "class_instance_methods_0000.spy"
            return this.CurrentValue;
        }

        public int GetValue()
        {
#line 17 "class_instance_methods_0000.spy"
            return this.CurrentValue;
        }

        public Calculator(int start)
        {
#line 6 "class_instance_methods_0000.spy"
            this.CurrentValue = start;
        }
    }
}
