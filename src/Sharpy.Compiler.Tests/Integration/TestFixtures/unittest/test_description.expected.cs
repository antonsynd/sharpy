#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

namespace TestDescription
{
    public static partial class TestDescriptionModule
    {
        public static void Main()
        {
#line (6, 5) - (6, 16) 12 "test_description.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }

    public partial class TestDescriptionModuleTests
    {
        [Xunit.FactAttribute(DisplayName = "my test description")]
        public void TestWithDesc()
        {
#line (3, 5) - (3, 19) 12 "test_description.spy"
            Xunit.Assert.Equal(1, 1);
#line hidden
        }
    }
}
#line default
