// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

#pragma warning disable xUnit2009, xUnit2017

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using collections = global::Sharpy.Collections;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Collections.DefaultDictTests
{
    [global::Sharpy.SharpyModule("collections.default_dict_tests")]
    public static partial class DefaultDictTestsModule
    {
    }

    public partial class DefaultDictTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestDefaultDictGetNoDefaultReturnsNone()
        {
#line (7, 5) - (7, 91) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 42);
#line (10, 5) - (10, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.True(dd.Get("missing").IsNone);
#line (11, 5) - (11, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.False(dd.ContainsKey("missing"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDefaultDictGetExistingKeyNoDefaultReturnsValue()
        {
#line (15, 5) - (15, 90) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (16, 5) - (16, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd["x"] = 99;
#line (17, 5) - (17, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.Equal(99, dd.Get("x").Unwrap());
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDefaultDictContainsExistingKeyReturnsTrue()
        {
#line (23, 5) - (23, 90) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (24, 5) - (24, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd["a"] = 1;
#line (25, 5) - (25, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.True(dd.Contains("a"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDefaultDictContainsMissingKeyReturnsFalse()
        {
#line (29, 5) - (29, 90) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (30, 5) - (30, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.False(dd.Contains("missing"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDefaultDictContainsAfterAutoCreateReturnsTrue()
        {
#line (34, 5) - (34, 90) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (35, 5) - (35, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            int _val = dd["key"];
#line (36, 5) - (36, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.True(dd.Contains("key"));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDefaultDictFactoryNotCalledForExistingKey()
        {
#line (42, 5) - (42, 92) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 999);
#line (43, 5) - (43, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd["a"] = 42;
#line (45, 5) - (45, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.Equal(42, dd["a"]);
#line (46, 5) - (46, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.Equal(42, dd["a"]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDefaultDictKeysEnumerationConsistentWithInsertion()
        {
#line (52, 5) - (52, 90) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (53, 5) - (53, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd["z"] = 1;
#line (54, 5) - (54, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd["a"] = 2;
#line (55, 5) - (55, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd["m"] = 3;
#line (56, 5) - (56, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Sharpy.List<string> keys = new global::Sharpy.List<string>(dd.Keys());
#line (57, 5) - (57, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.True(keys.Contains("z"));
#line (58, 5) - (58, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.True(keys.Contains("a"));
#line (59, 5) - (59, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.True(keys.Contains("m"));
#line (60, 5) - (60, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(keys));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDefaultDictValuesEnumerationIncludesAllValues()
        {
#line (64, 5) - (64, 90) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (65, 5) - (65, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd["a"] = 10;
#line (66, 5) - (66, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd["b"] = 20;
#line (67, 5) - (67, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd["c"] = 30;
#line (68, 5) - (68, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Sharpy.List<int> vals = new global::Sharpy.List<int>(dd.Values());
#line (69, 5) - (69, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.True(vals.Contains(10));
#line (70, 5) - (70, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.True(vals.Contains(20));
#line (71, 5) - (71, 23) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.True(vals.Contains(30));
#line (72, 5) - (72, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(vals));
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDefaultDictPopItemDefaultIsLastNotFirst()
        {
#line (78, 5) - (78, 90) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (79, 5) - (79, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd["a"] = 1;
#line (80, 5) - (80, 16) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd["b"] = 2;
#line (82, 5) - (82, 43) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::System.ValueTuple<string, int> pair = dd.PopItem();
#line (83, 5) - (83, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.Equal(("b", 2), pair);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDefaultDictUpdateFromDefaultDictUsesDictionary()
        {
#line (89, 5) - (89, 91) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd1 = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (90, 5) - (90, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd1["a"] = 1;
#line (91, 5) - (91, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd1["b"] = 2;
#line (92, 5) - (92, 91) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd2 = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (93, 5) - (93, 18) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd2["b"] = 99;
#line (94, 5) - (94, 17) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd2["c"] = 3;
#line (96, 5) - (96, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd1.Update(dd2.ToDictionary());
#line (97, 5) - (97, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.Equal(1, dd1["a"]);
#line (98, 5) - (98, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.Equal(99, dd1["b"]);
#line (99, 5) - (99, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.Equal(3, dd1["c"]);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestDefaultDictSetDefaultMissingKeyUsesProvidedValue()
        {
#line (105, 5) - (105, 91) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 99);
#line (107, 5) - (107, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            dd.SetDefault("key", 5);
#line (108, 5) - (108, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
            Xunit.Assert.Equal(5, dd["key"]);
#line hidden
        }
    }
}
#line default
