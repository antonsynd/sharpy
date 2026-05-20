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
#line (8, 9) - (8, 25) 1 "test_collection_basic.spy"
            this.Value = 100;
        }

        [Xunit.FactAttribute]
        public void TestValue()
#line 11 "test_collection_basic.spy"
        {
#line (12, 9) - (12, 34) 1 "test_collection_basic.spy"
            Xunit.Assert.Equal(100, this.Value);
        }
    }

    [Xunit.CollectionAttribute("database")]
    public class TestMoreDatabaseOps
    {
        [Xunit.FactAttribute]
        public void TestSimple()
#line 17 "test_collection_basic.spy"
        {
#line (18, 9) - (18, 20) 1 "test_collection_basic.spy"
            int x = 7;
#line (19, 9) - (19, 23) 1 "test_collection_basic.spy"
            Xunit.Assert.Equal(7, x);
        }
    }

    public static void Main()
    {
#line (22, 5) - (22, 16) 1 "test_collection_basic.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}
