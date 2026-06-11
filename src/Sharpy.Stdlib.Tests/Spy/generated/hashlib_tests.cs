// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using hashlib = global::Sharpy.HashlibModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Hashlib.HashlibTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Hashlib
    {
        [global::Sharpy.SharpyModule("hashlib.hashlib_tests")]
        public static partial class HashlibTests
        {
        }
    }

    public static partial class Hashlib
    {
        public partial class HashlibTestsTests
        {
            [Xunit.FactAttribute]
            public void TestSha256HelloProducesKnownHash()
            {
#line (7, 5) - (7, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var h = hashlib.Sha256("hello");
#line (8, 5) - (8, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestMd5HelloProducesKnownHash()
            {
#line (14, 5) - (14, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var h = hashlib.Md5("hello");
#line (15, 5) - (15, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("5d41402abc4b2a76b9719d911017c592", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestSha1HelloProducesKnownHash()
            {
#line (21, 5) - (21, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var h = hashlib.Sha1("hello");
#line (22, 5) - (22, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestSha384HelloProducesKnownHash()
            {
#line (28, 5) - (28, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var h = hashlib.Sha384("hello");
#line (29, 5) - (29, 128) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("59e1748777448c69de6b800d7a33bbfb9ff1b463e44354c3553bcdb9c666fa90125a3c79f90397bdf5f6a13de828684f", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestSha512HelloProducesKnownHash()
            {
#line (35, 5) - (35, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var h = hashlib.Sha512("hello");
#line (36, 5) - (36, 160) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("9b71d224bd62f3785d96d46ad3ea3d73319bfbc2890caadae2dff72519673ca72323c3d99ba5c11d7c7acc6e14b8c5da0c4663475c2e5c3adef46f73bcdec043", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestUpdateThenHexdigestMatchesPython()
            {
#line (42, 5) - (42, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var h = hashlib.Sha256();
#line (43, 5) - (43, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                h.Update("hello");
#line (44, 5) - (44, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestUpdateMultipleCallsAccumulate()
            {
#line (48, 5) - (48, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var h1 = hashlib.Sha256("helloworld");
#line (49, 5) - (49, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var h2 = hashlib.Sha256("hello");
#line (50, 5) - (50, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                h2.Update("world");
#line (51, 5) - (51, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal(h1.Hexdigest(), h2.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestDigestSizeReturnsCorrectValues()
            {
#line (57, 5) - (57, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal(16, hashlib.Md5().DigestSize);
#line (58, 5) - (58, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal(20, hashlib.Sha1().DigestSize);
#line (59, 5) - (59, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal(32, hashlib.Sha256().DigestSize);
#line (60, 5) - (60, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal(48, hashlib.Sha384().DigestSize);
#line (61, 5) - (61, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal(64, hashlib.Sha512().DigestSize);
            }

            [Xunit.FactAttribute]
            public void TestNameReturnsAlgorithmName()
            {
#line (67, 5) - (67, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("md5", hashlib.Md5().Name);
#line (68, 5) - (68, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("sha1", hashlib.Sha1().Name);
#line (69, 5) - (69, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("sha256", hashlib.Sha256().Name);
#line (70, 5) - (70, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("sha384", hashlib.Sha384().Name);
#line (71, 5) - (71, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("sha512", hashlib.Sha512().Name);
            }

            [Xunit.FactAttribute]
            public void TestDigestReturnsRawBytes()
            {
#line (77, 5) - (77, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var h = hashlib.Md5("hello");
#line (78, 5) - (78, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var digest = h.Digest();
#line (79, 5) - (79, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal(16, global::Sharpy.Builtins.Len(digest));
#line (81, 5) - (81, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal(93, digest[0]);
            }

            [Xunit.FactAttribute]
            public void TestCopyProducesIndependentClone()
            {
#line (87, 5) - (87, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var h = hashlib.Sha256("hello");
#line (88, 5) - (88, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var copy = h.Copy();
#line (89, 5) - (89, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                copy.Update("world");
#line (91, 5) - (91, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", h.Hexdigest());
#line (93, 5) - (93, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal(hashlib.Sha256("helloworld").Hexdigest(), copy.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestEmptyStringProducesKnownHash()
            {
#line (99, 5) - (99, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                var h = hashlib.Sha256();
#line (100, 5) - (100, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_tests.spy"
                Xunit.Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", h.Hexdigest());
            }
        }
    }
}
