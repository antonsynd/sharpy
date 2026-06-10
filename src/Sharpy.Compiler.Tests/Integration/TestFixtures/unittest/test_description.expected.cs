#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static TestDescription;

public static partial class TestDescription
{
    public static void Main()
    {
#line (6, 5) - (6, 16) 1 "test_description.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class TestDescriptionTests
{
    [Xunit.FactAttribute(DisplayName = "my test description")]
    public void TestWithDesc()
    {
#line (3, 5) - (3, 19) 1 "test_description.spy"
        Xunit.Assert.Equal(1, 1);
    }
}
