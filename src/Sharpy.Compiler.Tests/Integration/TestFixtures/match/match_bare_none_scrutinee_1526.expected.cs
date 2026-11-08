// Snapshot: `match None` scrutinee lowered to a typed null — `switch ((object?)null)` in both statements and `(object?)null switch` in the expression (#1526; plan-1faae3 Phase 0)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace MatchBareNoneScrutinee1526
{
    public static partial class MatchBareNoneScrutinee1526Module
    {
        public static void Main()
        {
#line (4, 5) - (8, 30) 12 "match_bare_none_scrutinee_1526.spy"
            switch ((object?)null)
#line hidden
            {
                case null:
#line (6, 13) - (6, 34) 20 "match_bare_none_scrutinee_1526.spy"
                    global::Sharpy.Builtins.Print("matched None");
#line hidden
                    break;
                default:
#line (8, 13) - (8, 30) 20 "match_bare_none_scrutinee_1526.spy"
                    global::Sharpy.Builtins.Print("wildcard");
#line hidden
                    break;
            }

#line (10, 5) - (14, 30) 12 "match_bare_none_scrutinee_1526.spy"
            switch ((object?)null)
#line hidden
            {
                case 5:
#line (12, 13) - (12, 26) 20 "match_bare_none_scrutinee_1526.spy"
                    global::Sharpy.Builtins.Print("five");
#line hidden
                    break;
                default:
#line (14, 13) - (14, 30) 20 "match_bare_none_scrutinee_1526.spy"
                    global::Sharpy.Builtins.Print("not five");
#line hidden
                    break;
            }

#line (16, 5) - (19, 1) 12 "match_bare_none_scrutinee_1526.spy"
            int v = (object?)null switch
#line hidden
            {
                null => 1,
                var _ => 2
            };
#line (19, 5) - (19, 13) 12 "match_bare_none_scrutinee_1526.spy"
            global::Sharpy.Builtins.Print(v);
#line hidden
        }
    }
}
#line default
