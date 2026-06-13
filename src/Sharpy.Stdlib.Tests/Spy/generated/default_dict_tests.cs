// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using collections = global::Sharpy.Collections;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Collections.DefaultDictTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Collections
    {
        [global::Sharpy.SharpyModule("collections.default_dict_tests")]
        public static partial class DefaultDictTests
        {
        }
    }

    public static partial class Collections
    {
        public partial class DefaultDictTestsTests
        {
            [Xunit.FactAttribute]
            public void TestDefaultDictGetNoDefaultReturnsDefaultT()
            {
#line (7, 5) - (7, 91) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 42);
#line (9, 5) - (9, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Equal(0, dd.Get("missing"));
#line (10, 5) - (10, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.False(dd.ContainsKey("missing"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictGetExistingKeyNoDefaultReturnsValue()
            {
#line (14, 5) - (14, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (15, 5) - (15, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd["x"] = 99;
#line (16, 5) - (16, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Equal(99, dd.Get("x"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictContainsExistingKeyReturnsTrue()
            {
#line (22, 5) - (22, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (23, 5) - (23, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd["a"] = 1;
#line (24, 5) - (24, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.True(dd.Contains("a"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictContainsMissingKeyReturnsFalse()
            {
#line (28, 5) - (28, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (29, 5) - (29, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.False(dd.Contains("missing"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictContainsAfterAutoCreateReturnsTrue()
            {
#line (33, 5) - (33, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (34, 5) - (34, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                int _val = dd["key"];
#line (35, 5) - (35, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.True(dd.Contains("key"));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictFactoryNotCalledForExistingKey()
            {
#line (41, 5) - (41, 92) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 999);
#line (42, 5) - (42, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd["a"] = 42;
#line (44, 5) - (44, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Equal(42, dd["a"]);
#line (45, 5) - (45, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Equal(42, dd["a"]);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictKeysEnumerationConsistentWithInsertion()
            {
#line (51, 5) - (51, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (52, 5) - (52, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd["z"] = 1;
#line (53, 5) - (53, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd["a"] = 2;
#line (54, 5) - (54, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd["m"] = 3;
#line (55, 5) - (55, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Sharpy.List<string> keys = new global::Sharpy.List<string>(dd.Keys);
#line (56, 5) - (56, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Contains("z", keys);
#line (57, 5) - (57, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Contains("a", keys);
#line (58, 5) - (58, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Contains("m", keys);
#line (59, 5) - (59, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(keys));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictValuesEnumerationIncludesAllValues()
            {
#line (63, 5) - (63, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (64, 5) - (64, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd["a"] = 10;
#line (65, 5) - (65, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd["b"] = 20;
#line (66, 5) - (66, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd["c"] = 30;
#line (67, 5) - (67, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Sharpy.List<int> vals = new global::Sharpy.List<int>(dd.Values);
#line (68, 5) - (68, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Contains(10, vals);
#line (69, 5) - (69, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Contains(20, vals);
#line (70, 5) - (70, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Contains(30, vals);
#line (71, 5) - (71, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(vals));
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictPopItemDefaultIsFirstNotLast()
            {
#line (77, 5) - (77, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (78, 5) - (78, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd["a"] = 1;
#line (79, 5) - (79, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd["b"] = 2;
#line (81, 5) - (81, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::System.ValueTuple<string, int> pair = dd.PopItem();
#line (82, 5) - (82, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Equal(("a", 1), pair);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictUpdateFromDefaultDictUsesDictionary()
            {
#line (88, 5) - (88, 91) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd1 = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (89, 5) - (89, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd1["a"] = 1;
#line (90, 5) - (90, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd1["b"] = 2;
#line (91, 5) - (91, 91) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd2 = new global::Sharpy.DefaultDict<string, int>(() => 0);
#line (92, 5) - (92, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd2["b"] = 99;
#line (93, 5) - (93, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd2["c"] = 3;
#line (95, 5) - (95, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd1.Update(dd2.ToDictionary());
#line (96, 5) - (96, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Equal(1, dd1["a"]);
#line (97, 5) - (97, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Equal(99, dd1["b"]);
#line (98, 5) - (98, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Equal(3, dd1["c"]);
            }

            [Xunit.FactAttribute]
            public void TestDefaultDictSetDefaultMissingKeyUsesProvidedValue()
            {
#line (104, 5) - (104, 91) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                global::Sharpy.DefaultDict<string, int> dd = new global::Sharpy.DefaultDict<string, int>(() => 99);
#line (106, 5) - (106, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                dd.SetDefault("key", 5);
#line (107, 5) - (107, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/collections/default_dict_tests.spy"
                Xunit.Assert.Equal(5, dd["key"]);
            }
        }
    }
}
