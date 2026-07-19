#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

public static partial class TestCollectionBasic
{
    [Xunit.CollectionAttribute("database")]
    public class TestDatabaseOps
    {
        public TestDatabaseOps()
        {
            Setup();
        }

        public int Value;
        private void Setup()
#line 7 "test_collection_basic.spy"
        {
#line (8, 9) - (8, 25) 12 "test_collection_basic.spy"
            this.Value = 100;
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestValue()
#line 11 "test_collection_basic.spy"
        {
#line (12, 9) - (12, 34) 12 "test_collection_basic.spy"
            Xunit.Assert.Equal(100, this.Value);
#line hidden
        }
    }

    [Xunit.CollectionAttribute("database")]
    public class TestMoreDatabaseOps
    {
        [Xunit.FactAttribute]
        public void TestSimple()
#line 17 "test_collection_basic.spy"
        {
#line (18, 9) - (18, 20) 12 "test_collection_basic.spy"
            int x = 7;
#line (19, 9) - (19, 23) 12 "test_collection_basic.spy"
            Xunit.Assert.Equal(7, x);
#line hidden
        }
    }

    public static void Main()
    {
#line (22, 5) - (22, 16) 8 "test_collection_basic.spy"
        global::Sharpy.Builtins.Print("ok");
#line hidden
    }
}
#line default
