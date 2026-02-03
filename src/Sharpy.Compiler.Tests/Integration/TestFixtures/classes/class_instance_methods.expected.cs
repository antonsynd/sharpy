#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassInstanceMethods
{
    public static class Program
    {
        public static void Main()
        {
#line 16 "class_instance_methods.spy"
            var calc = new Calculator(5);
#line 17 "class_instance_methods.spy"
            global::Sharpy.Core.Exports.Print(calc.GetResult());
#line 18 "class_instance_methods.spy"
            global::Sharpy.Core.Exports.Print(calc.Add(7));
#line 19 "class_instance_methods.spy"
            global::Sharpy.Core.Exports.Print(calc.GetResult());
        }
    }

    public class Calculator
    {
        public int Result;
        public int Add(int x)
        {
#line 9 "class_instance_methods.spy"
            this.Result = this.Result + x;
#line 10 "class_instance_methods.spy"
            return this.Result;
        }

        public int GetResult()
        {
#line 13 "class_instance_methods.spy"
            return this.Result;
        }

        public Calculator(int initial)
        {
#line 6 "class_instance_methods.spy"
            this.Result = initial;
        }
    }
}
