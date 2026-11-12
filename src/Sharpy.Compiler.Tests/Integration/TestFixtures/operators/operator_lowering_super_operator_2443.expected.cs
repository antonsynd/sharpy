// Snapshot: a super operator call, lowered from a FunctionCall key (plan-92249e T1, #2443)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace OperatorLoweringSuperOperator2443
{
    public static partial class OperatorLoweringSuperOperator2443Module
    {
        public static void Main()
        {
#line (24, 5) - (24, 27) 12 "operator_lowering_super_operator_2443.spy"
            global::Sharpy.Builtins.Print((new global::OperatorLoweringSuperOperator2443.W(1) + new global::OperatorLoweringSuperOperator2443.V(2)).X);
#line (25, 5) - (25, 27) 12 "operator_lowering_super_operator_2443.spy"
            global::Sharpy.Builtins.Print((new global::OperatorLoweringSuperOperator2443.V(1) + new global::OperatorLoweringSuperOperator2443.V(2)).X);
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "V")]
    public class V
    {
        public int X;
        public static global::OperatorLoweringSuperOperator2443.V operator +(V left, global::OperatorLoweringSuperOperator2443.V right)
        {
#line (11, 9) - (11, 36) 12 "operator_lowering_super_operator_2443.spy"
            return new global::OperatorLoweringSuperOperator2443.V(left.X + right.X);
#line hidden
        }

        public V(int x)
#line 7 "operator_lowering_super_operator_2443.spy"
        {
#line (8, 9) - (8, 19) 12 "operator_lowering_super_operator_2443.spy"
            this.X = x;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "W")]
    public class W : global::OperatorLoweringSuperOperator2443.V
    {
        public static global::OperatorLoweringSuperOperator2443.V operator +(W left, global::OperatorLoweringSuperOperator2443.V right)
        {
#line (19, 9) - (19, 38) 12 "operator_lowering_super_operator_2443.spy"
            var @base = (global::OperatorLoweringSuperOperator2443.V)left + right;
#line (20, 9) - (20, 32) 12 "operator_lowering_super_operator_2443.spy"
            return new global::OperatorLoweringSuperOperator2443.V(@base.X + 100);
#line hidden
        }

        public W(int x) : base(x)
#line 15 "operator_lowering_super_operator_2443.spy"
        {
        }
    }
}
