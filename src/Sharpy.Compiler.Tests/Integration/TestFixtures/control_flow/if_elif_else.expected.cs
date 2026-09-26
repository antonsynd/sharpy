// Snapshot: If/elif/else conditional branching
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace IfElifElse
{
    public static partial class IfElifElseModule
    {
        public static void Categorize(int x)
        {
#line (2, 5) - (7, 22) 12 "if_elif_else.spy"
            if (x > 0)
#line hidden
            {
#line (3, 9) - (3, 26) 16 "if_elif_else.spy"
                global::Sharpy.Builtins.Print("positive");
#line hidden
            }
            else if (x < 0)
            {
#line (5, 9) - (5, 26) 16 "if_elif_else.spy"
                global::Sharpy.Builtins.Print("negative");
#line hidden
            }
            else
            {
#line (7, 9) - (7, 22) 16 "if_elif_else.spy"
                global::Sharpy.Builtins.Print("zero");
#line hidden
            }
        }

        public static void Main()
        {
#line (10, 5) - (10, 18) 12 "if_elif_else.spy"
            global::IfElifElse.IfElifElseModule.Categorize(5);
#line (11, 5) - (11, 19) 12 "if_elif_else.spy"
            global::IfElifElse.IfElifElseModule.Categorize(-3);
#line (12, 5) - (12, 18) 12 "if_elif_else.spy"
            global::IfElifElse.IfElifElseModule.Categorize(0);
#line hidden
        }
    }
}
#line default
