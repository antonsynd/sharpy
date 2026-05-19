#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

public static partial class TestClass
{
    public class TestCalc : System.IDisposable
    {
        public TestCalc()
        {
            Setup();
        }

        public int X;
        private void Setup()
#line 6 "test_class.spy"
        {
#line (7, 9) - (7, 20) 1 "test_class.spy"
            this.X = 42;
        }

        private void Teardown()
#line 9 "test_class.spy"
        {
#line (10, 9) - (10, 14) 1 "test_class.spy"
            ;
        }

        [Xunit.FactAttribute]
        public void TestValue()
#line 13 "test_class.spy"
        {
#line (14, 9) - (14, 29) 1 "test_class.spy"
            Xunit.Assert.Equal(42, this.X);
        }

        public void Dispose()
        {
            Teardown();
        }
    }

    public static void Main()
    {
#line (17, 5) - (17, 16) 1 "test_class.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}
