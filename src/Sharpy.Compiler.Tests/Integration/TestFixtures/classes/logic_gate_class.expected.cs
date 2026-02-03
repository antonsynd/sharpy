#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.LogicGateClass
{
    public static class Program
    {
        public static void Main()
        {
#line 43 "logic_gate_class.spy"
            LogicGate gate = new LogicGate(true, false, true);
#line 44 "logic_gate_class.spy"
            gate.TestAndOperations();
#line 45 "logic_gate_class.spy"
            gate.TestOrOperations();
#line 46 "logic_gate_class.spy"
            gate.TestNotOperations();
#line 47 "logic_gate_class.spy"
            gate.TestComplexLogic();
        }
    }

    public class LogicGate
    {
        public bool A;
        public bool B;
        public bool C;
        public void TestAndOperations()
        {
#line 15 "logic_gate_class.spy"
            bool result1 = this.A && this.B;
#line 16 "logic_gate_class.spy"
            bool result2 = this.A && this.B && this.C;
#line 17 "logic_gate_class.spy"
            bool result3 = false && this.A;
#line 18 "logic_gate_class.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 19 "logic_gate_class.spy"
            global::Sharpy.Core.Exports.Print(result2);
#line 20 "logic_gate_class.spy"
            global::Sharpy.Core.Exports.Print(result3);
        }

        public void TestOrOperations()
        {
#line 23 "logic_gate_class.spy"
            bool result1 = this.A || this.B;
#line 24 "logic_gate_class.spy"
            bool result2 = false || false || this.C;
#line 25 "logic_gate_class.spy"
            bool result3 = true || this.A;
#line 26 "logic_gate_class.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 27 "logic_gate_class.spy"
            global::Sharpy.Core.Exports.Print(result2);
#line 28 "logic_gate_class.spy"
            global::Sharpy.Core.Exports.Print(result3);
        }

        public void TestNotOperations()
        {
#line 31 "logic_gate_class.spy"
            bool result1 = !this.A;
#line 32 "logic_gate_class.spy"
            bool result2 = !(this.A && this.B);
#line 33 "logic_gate_class.spy"
            bool result3 = !this.A || this.B;
#line 34 "logic_gate_class.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 35 "logic_gate_class.spy"
            global::Sharpy.Core.Exports.Print(result2);
#line 36 "logic_gate_class.spy"
            global::Sharpy.Core.Exports.Print(result3);
        }

        public void TestComplexLogic()
        {
#line 39 "logic_gate_class.spy"
            bool result = (this.A || this.B) && (!this.C || this.A);
#line 40 "logic_gate_class.spy"
            global::Sharpy.Core.Exports.Print(result);
        }

        public LogicGate(bool first, bool second, bool third)
        {
#line 10 "logic_gate_class.spy"
            this.A = first;
#line 11 "logic_gate_class.spy"
            this.B = second;
#line 12 "logic_gate_class.spy"
            this.C = third;
        }
    }
}
