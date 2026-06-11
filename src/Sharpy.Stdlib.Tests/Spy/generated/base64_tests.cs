// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using base64 = global::Sharpy.Base64Module;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Base64.Base64Tests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Base64
    {
        [global::Sharpy.SharpyModule("base64.base64_tests")]
        public static partial class Base64Tests
        {
        }
    }

    public static partial class Base64
    {
        public partial class Base64TestsTests
        {
            [Xunit.FactAttribute]
            public void TestB64encodeHelloWorld()
            {
#line (7, 5) - (7, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes result = base64.B64encode(new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111, 32, 119, 111, 114, 108, 100 }));
#line (8, 5) - (8, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Equal("aGVsbG8gd29ybGQ=", result.Decode("ascii"));
            }

            [Xunit.FactAttribute]
            public void TestB64decodeRoundtrip()
            {
#line (12, 5) - (12, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes original = new Sharpy.Bytes(new byte[] { 116, 101, 115, 116, 32, 100, 97, 116, 97 });
#line (13, 5) - (13, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes encoded = base64.B64encode(original);
#line (14, 5) - (14, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes decoded = base64.B64decode(encoded);
#line (15, 5) - (15, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Equal(original, decoded);
            }

            [Xunit.FactAttribute]
            public void TestUrlsafeB64encodeReplacesChars()
            {
#line (19, 5) - (19, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes data = global::Sharpy.Bytes.Fromhex("fffefd");
#line (20, 5) - (20, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes result = base64.UrlsafeB64encode(data);
#line (21, 5) - (21, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                string resultStr = result.Decode("ascii");
#line (22, 5) - (22, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.DoesNotContain("+", resultStr);
#line (23, 5) - (23, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.DoesNotContain("/", resultStr);
            }

            [Xunit.FactAttribute]
            public void TestB32encodeHello()
            {
#line (27, 5) - (27, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes result = base64.B32encode(new Sharpy.Bytes(new byte[] { 104, 101, 108, 108, 111 }));
#line (28, 5) - (28, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Equal("NBSWY3DP", result.Decode("ascii"));
            }

            [Xunit.FactAttribute]
            public void TestB32decodeRejectsLowercaseByDefault()
            {
#line (32, 5) - (32, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes inputData = new Sharpy.Bytes(new byte[] { 110, 98, 115, 119, 121, 51, 100, 112 });
#line (33, 5) - (36, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (34, 9) - (34, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                    base64.B32decode(inputData);
                }));
            }

            [Xunit.FactAttribute]
            public void TestB32decodeAcceptsLowercaseWithCasefold()
            {
#line (38, 5) - (38, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes inputData = new Sharpy.Bytes(new byte[] { 110, 98, 115, 119, 121, 51, 100, 112 });
#line (39, 5) - (39, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes result = base64.B32decode(inputData, casefold: true);
#line (40, 5) - (40, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Equal("hello", result.Decode("ascii"));
            }

            [Xunit.FactAttribute]
            public void TestB16encodeProducesUppercase()
            {
#line (44, 5) - (44, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes result = base64.B16encode(global::Sharpy.Bytes.Fromhex("dead"));
#line (45, 5) - (45, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Equal("DEAD", result.Decode("ascii"));
            }

            [Xunit.FactAttribute]
            public void TestB16decodeRejectsLowercaseByDefault()
            {
#line (49, 5) - (49, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes inputData = new Sharpy.Bytes(new byte[] { 100, 101, 97, 100 });
#line (50, 5) - (53, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (51, 9) - (51, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                    base64.B16decode(inputData);
                }));
            }

            [Xunit.FactAttribute]
            public void TestB16decodeAcceptsLowercaseWithCasefold()
            {
#line (55, 5) - (55, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes inputData = new Sharpy.Bytes(new byte[] { 100, 101, 97, 100 });
#line (56, 5) - (56, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes result = base64.B16decode(inputData, casefold: true);
#line (57, 5) - (57, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Equal(global::Sharpy.Bytes.Fromhex("dead"), result);
            }

            [Xunit.FactAttribute]
            public void TestB85encodeRoundtrip()
            {
#line (61, 5) - (61, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes original = new Sharpy.Bytes(new byte[] { 72, 101, 108, 108, 111, 32, 87, 111, 114, 108, 100, 33 });
#line (62, 5) - (62, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes encoded = base64.B85encode(original);
#line (63, 5) - (63, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes decoded = base64.B85decode(encoded);
#line (64, 5) - (64, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Equal(original, decoded);
            }

            [Xunit.FactAttribute]
            public void TestA85encodeRoundtrip()
            {
#line (68, 5) - (68, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes original = new Sharpy.Bytes(new byte[] { 72, 101, 108, 108, 111, 32, 87, 111, 114, 108, 100, 33 });
#line (69, 5) - (69, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes encoded = base64.A85encode(original);
#line (70, 5) - (70, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes decoded = base64.A85decode(encoded);
#line (71, 5) - (71, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Equal(original, decoded);
            }

            [Xunit.FactAttribute]
            public void TestB64decodeStringOverload()
            {
#line (75, 5) - (75, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes result = base64.B64decode("aGVsbG8=");
#line (76, 5) - (76, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Equal("hello", result.Decode("ascii"));
            }

            [Xunit.FactAttribute]
            public void TestB64encodeEmpty()
            {
#line (80, 5) - (80, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Sharpy.Bytes result = base64.B64encode(new Sharpy.Bytes(new byte[] { }));
#line (81, 5) - (81, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/base64/base64_tests.spy"
                Xunit.Assert.Equal("", result.Decode("ascii"));
            }
        }
    }
}
