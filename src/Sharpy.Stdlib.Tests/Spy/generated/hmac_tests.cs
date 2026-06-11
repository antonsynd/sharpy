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
using hmac = global::Sharpy.HmacModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Hmac.HmacTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Hmac
    {
        [global::Sharpy.SharpyModule("hmac.hmac_tests")]
        public static partial class HmacTests
        {
        }
    }

    public static partial class Hmac
    {
        public partial class HmacTestsTests
        {
            [Xunit.FactAttribute]
            public void TestHmacSha256KnownValue()
            {
#line (8, 5) - (8, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                global::Sharpy.HmacObject h = hmac.New("secret", "message", "sha256");
#line (9, 5) - (9, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Equal("8b5f48702995c1598c573db1e21866a9b825d4a794d169d7060a03605796360b", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestHmacSha256IncrementalUpdate()
            {
#line (13, 5) - (13, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                global::Sharpy.HmacObject h = hmac.New("secret", digestmod: "sha256");
#line (14, 5) - (14, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                h.Update("message");
#line (15, 5) - (15, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Equal("8b5f48702995c1598c573db1e21866a9b825d4a794d169d7060a03605796360b", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestHmacSha256BytesOverload()
            {
#line (19, 5) - (19, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Sharpy.Bytes key = new Sharpy.Bytes(new byte[] { 115, 101, 99, 114, 101, 116 });
#line (20, 5) - (20, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Sharpy.Bytes msg = new Sharpy.Bytes(new byte[] { 109, 101, 115, 115, 97, 103, 101 });
#line (21, 5) - (21, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                global::Sharpy.HmacObject h = hmac.New(key, msg, "sha256");
#line (22, 5) - (22, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Equal("8b5f48702995c1598c573db1e21866a9b825d4a794d169d7060a03605796360b", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestCopyProducesIndependentClone()
            {
#line (26, 5) - (26, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                global::Sharpy.HmacObject h1 = hmac.New("key", "hello", "sha256");
#line (27, 5) - (27, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                global::Sharpy.HmacObject h2 = h1.Copy();
#line (28, 5) - (28, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                h2.Update(" world");
#line (29, 5) - (29, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.NotEqual(h2.Hexdigest(), h1.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestNameProperty()
            {
#line (33, 5) - (33, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                global::Sharpy.HmacObject h = hmac.New("key", digestmod: "sha512");
#line (34, 5) - (34, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Equal("hmac-sha512", h.Name);
            }

            [Xunit.FactAttribute]
            public void TestDigestSizeProperty()
            {
#line (38, 5) - (38, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Equal(32, hmac.New("key", digestmod: "sha256").DigestSize);
#line (39, 5) - (39, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Equal(64, hmac.New("key", digestmod: "sha512").DigestSize);
#line (40, 5) - (40, 63) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Equal(16, hmac.New("key", digestmod: "md5").DigestSize);
            }

            [Xunit.FactAttribute]
            public void TestBlockSizeProperty()
            {
#line (44, 5) - (44, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Equal(64, hmac.New("key", digestmod: "sha256").BlockSize);
#line (45, 5) - (45, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Equal(128, hmac.New("key", digestmod: "sha512").BlockSize);
            }

            [Xunit.FactAttribute]
            public void TestCompareDigestMatchingStrings()
            {
#line (49, 5) - (49, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.True(hmac.CompareDigest("hello", "hello"));
            }

            [Xunit.FactAttribute]
            public void TestCompareDigestNonMatchingStrings()
            {
#line (53, 5) - (53, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.False(hmac.CompareDigest("hello", "world"));
            }

            [Xunit.FactAttribute]
            public void TestCompareDigestBytes()
            {
#line (57, 5) - (57, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Sharpy.Bytes a = global::Sharpy.Bytes.Fromhex("010203");
#line (58, 5) - (58, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Sharpy.Bytes b = global::Sharpy.Bytes.Fromhex("010203");
#line (59, 5) - (59, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.True(hmac.CompareDigest(a, b));
            }

            [Xunit.FactAttribute]
            public void TestDigestOneShotReturnsBytes()
            {
#line (63, 5) - (63, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Sharpy.Bytes result = hmac.Digest("secret", "message", "sha256");
#line (64, 5) - (64, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Equal(32, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestUnsupportedAlgorithmThrowsValueError()
            {
#line (68, 5) - (71, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (69, 9) - (69, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                    hmac.New("key", digestmod: "unsupported");
                }));
            }

            [Xunit.FactAttribute]
            public void TestUpdateWithBytes()
            {
#line (73, 5) - (73, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                global::Sharpy.HmacObject h = hmac.New("secret", digestmod: "sha256");
#line (74, 5) - (74, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                h.Update(new Sharpy.Bytes(new byte[] { 109, 101, 115, 115, 97, 103, 101 }));
#line (75, 5) - (75, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/hmac/hmac_tests.spy"
                Xunit.Assert.Equal("8b5f48702995c1598c573db1e21866a9b825d4a794d169d7060a03605796360b", h.Hexdigest());
            }
        }
    }
}
