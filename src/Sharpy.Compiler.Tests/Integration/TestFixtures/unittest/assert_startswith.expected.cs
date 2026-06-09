#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

public static partial class AssertStartswith
{
    public static void Main()
    {
#line (12, 5) - (12, 16) 1 "assert_startswith.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class AssertStartswithTests
{
    [Xunit.FactAttribute]
    public void TestStrStartswith()
    {
#line (3, 5) - (3, 31) 1 "assert_startswith.spy"
        string name = "hello world";
#line (4, 5) - (4, 37) 1 "assert_startswith.spy"
        Xunit.Assert.StartsWith("hello", name);
    }

    [Xunit.FactAttribute]
    public void TestStrEndswith()
    {
#line (8, 5) - (8, 31) 1 "assert_startswith.spy"
        string name = "hello world";
#line (9, 5) - (9, 35) 1 "assert_startswith.spy"
        Xunit.Assert.EndsWith("world", name);
    }
}
