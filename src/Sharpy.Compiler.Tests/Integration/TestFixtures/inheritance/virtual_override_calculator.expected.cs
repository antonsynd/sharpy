#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.VirtualOverrideCalculator
{
    public static class Program
    {
        public static Calculator Calc1 = new Calculator(10);
        public static Calculator Calc2 = new Multiplier(3, 2);
        public static Calculator Calc3 = new Subtractor(20);
        public static void Main()
        {
#line 45 "virtual_override_calculator.spy"
            global::Sharpy.Core.Exports.Print(Calc1.Calculate(5));
#line 46 "virtual_override_calculator.spy"
            global::Sharpy.Core.Exports.Print(Calc1.GetOperationName());
#line 48 "virtual_override_calculator.spy"
            global::Sharpy.Core.Exports.Print(Calc2.Calculate(4));
#line 49 "virtual_override_calculator.spy"
            global::Sharpy.Core.Exports.Print(Calc2.GetOperationName());
#line 51 "virtual_override_calculator.spy"
            global::Sharpy.Core.Exports.Print(Calc3.Calculate(7));
        }
    }

    public class Calculator
    {
        public int BaseValue;
        public virtual int Calculate(int operand)
        {
#line 11 "virtual_override_calculator.spy"
            return this.BaseValue + operand;
        }

        public virtual string GetOperationName()
        {
#line 15 "virtual_override_calculator.spy"
            return "addition";
        }

        public Calculator(int value)
        {
#line 7 "virtual_override_calculator.spy"
            this.BaseValue = value;
        }
    }

    public class Multiplier : Calculator
    {
        public int Factor;
        public override int Calculate(int operand)
        {
#line 26 "virtual_override_calculator.spy"
            return this.BaseValue * operand * this.Factor;
        }

        public override string GetOperationName()
        {
#line 30 "virtual_override_calculator.spy"
            return "multiplication";
        }

        public Multiplier(int value, int multFactor) : base(value)
        {
#line 22 "virtual_override_calculator.spy"
            this.Factor = multFactor;
        }
    }

    public class Subtractor : Calculator
    {
        public override int Calculate(int operand)
        {
#line 38 "virtual_override_calculator.spy"
            return this.BaseValue - operand;
        }

        public Subtractor(int value) : base(value)
        {
        }
    }
}
