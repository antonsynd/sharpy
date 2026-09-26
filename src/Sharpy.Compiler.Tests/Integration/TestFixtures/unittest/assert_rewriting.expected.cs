#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace AssertRewriting
{
    public static partial class AssertRewritingModule
    {
        public static void Main()
        {
#line (44, 5) - (44, 16) 12 "assert_rewriting.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Widget")]
    public class Widget
    {
        public string Name;
        public Widget(string name)
#line 32 "assert_rewriting.spy"
        {
#line (33, 9) - (33, 25) 12 "assert_rewriting.spy"
            this.Name = name;
#line hidden
        }
    }

    public partial class AssertRewritingModuleTests
    {
        [Xunit.FactAttribute]
        public void TestEquality()
        {
#line (3, 5) - (3, 16) 12 "assert_rewriting.spy"
            int x = 1;
#line (4, 5) - (4, 16) 12 "assert_rewriting.spy"
            int y = 2;
#line (5, 5) - (5, 19) 12 "assert_rewriting.spy"
            Xunit.Assert.Equal(y, x);
#line (6, 5) - (6, 19) 12 "assert_rewriting.spy"
            Xunit.Assert.NotEqual(y, x);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestComparisons()
        {
#line (10, 5) - (10, 16) 12 "assert_rewriting.spy"
            int x = 1;
#line (11, 5) - (11, 16) 12 "assert_rewriting.spy"
            int y = 2;
#line (12, 5) - (12, 18) 12 "assert_rewriting.spy"
            Xunit.Assert.True(x > y);
#line (13, 5) - (13, 18) 12 "assert_rewriting.spy"
            Xunit.Assert.True(x < y);
#line (14, 5) - (14, 19) 12 "assert_rewriting.spy"
            Xunit.Assert.True(x >= y);
#line (15, 5) - (15, 19) 12 "assert_rewriting.spy"
            Xunit.Assert.True(x <= y);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNullability()
        {
#line (19, 5) - (19, 22) 12 "assert_rewriting.spy"
            Optional<string> s = Optional<string>.None;
#line (20, 5) - (20, 22) 12 "assert_rewriting.spy"
            Xunit.Assert.Null(s);
#line (21, 5) - (21, 26) 12 "assert_rewriting.spy"
            Xunit.Assert.NotNull(s);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestMembership()
        {
#line (25, 5) - (25, 34) 12 "assert_rewriting.spy"
            Sharpy.List<int> items = new Sharpy.List<int>()
#line hidden
            {
                1,
                2,
                3
            };
#line (26, 5) - (26, 23) 12 "assert_rewriting.spy"
            Xunit.Assert.Contains(1, items);
#line (27, 5) - (27, 27) 12 "assert_rewriting.spy"
            Xunit.Assert.DoesNotContain(4, items);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestNoneEquality()
        {
#line (39, 5) - (39, 34) 12 "assert_rewriting.spy"
            global::AssertRewriting.Widget w = new global::AssertRewriting.Widget("gadget");
#line (40, 5) - (40, 22) 12 "assert_rewriting.spy"
            Xunit.Assert.Null(w);
#line (41, 5) - (41, 22) 12 "assert_rewriting.spy"
            Xunit.Assert.NotNull(w);
#line hidden
        }
    }
}
#line default
