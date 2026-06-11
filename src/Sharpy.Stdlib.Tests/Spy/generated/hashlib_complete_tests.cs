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
using static Sharpy.Stdlib.Tests.Spy.Hashlib.HashlibCompleteTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Hashlib
    {
        [global::Sharpy.SharpyModule("hashlib.hashlib_complete_tests")]
        public static partial class HashlibCompleteTests
        {
        }
    }

    public static partial class Hashlib
    {
        public partial class HashlibCompleteTestsTests
        {
            [Xunit.FactAttribute]
            public void TestMd5EmptyStringProducesKnownHash()
            {
#line (7, 5) - (7, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Md5();
#line (8, 5) - (8, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestSha1EmptyStringProducesKnownHash()
            {
#line (12, 5) - (12, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha1();
#line (13, 5) - (13, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal("da39a3ee5e6b4b0d3255bfef95601890afd80709", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestSha384EmptyStringProducesKnownHash()
            {
#line (17, 5) - (17, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha384();
#line (18, 5) - (18, 128) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal("38b060a751ac96384cd9327eb1b1e36a21fdb71114be07434c0cc7bf63f6e1da274edebfe76f65fbd51ad2f14898b95b", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestSha512EmptyStringProducesKnownHash()
            {
#line (22, 5) - (22, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha512();
#line (23, 5) - (23, 160) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal("cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e", h.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestSha1DigestLengthIs20()
            {
#line (29, 5) - (29, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha1("hello");
#line (30, 5) - (30, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(20, global::Sharpy.Builtins.Len(h.Digest()));
            }

            [Xunit.FactAttribute]
            public void TestSha256DigestLengthIs32()
            {
#line (34, 5) - (34, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha256("hello");
#line (35, 5) - (35, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(32, global::Sharpy.Builtins.Len(h.Digest()));
            }

            [Xunit.FactAttribute]
            public void TestSha384DigestLengthIs48()
            {
#line (39, 5) - (39, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha384("hello");
#line (40, 5) - (40, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(48, global::Sharpy.Builtins.Len(h.Digest()));
            }

            [Xunit.FactAttribute]
            public void TestSha512DigestLengthIs64()
            {
#line (44, 5) - (44, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha512("hello");
#line (45, 5) - (45, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(64, global::Sharpy.Builtins.Len(h.Digest()));
            }

            [Xunit.FactAttribute]
            public void TestSha256DigestFirstByteMatchesHexdigest()
            {
#line (51, 5) - (51, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha256("hello");
#line (53, 5) - (53, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var digest = h.Digest();
#line (54, 5) - (54, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(44, digest[0]);
            }

            [Xunit.FactAttribute]
            public void TestSha1DigestFirstByteMatchesHexdigest()
            {
#line (58, 5) - (58, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha1("hello");
#line (60, 5) - (60, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var digest = h.Digest();
#line (61, 5) - (61, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(170, digest[0]);
            }

            [Xunit.FactAttribute]
            public void TestMd5HexdigestLengthIsDigestSizeTimesTwo()
            {
#line (67, 5) - (67, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Md5("hello");
#line (68, 5) - (68, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(h.DigestSize * 2, global::Sharpy.Builtins.Len(h.Hexdigest()));
            }

            [Xunit.FactAttribute]
            public void TestSha256HexdigestLengthIsDigestSizeTimesTwo()
            {
#line (72, 5) - (72, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha256("hello");
#line (73, 5) - (73, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(h.DigestSize * 2, global::Sharpy.Builtins.Len(h.Hexdigest()));
            }

            [Xunit.FactAttribute]
            public void TestSha512HexdigestLengthIsDigestSizeTimesTwo()
            {
#line (77, 5) - (77, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha512("hello");
#line (78, 5) - (78, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(h.DigestSize * 2, global::Sharpy.Builtins.Len(h.Hexdigest()));
            }

            [Xunit.FactAttribute]
            public void TestMd5HexdigestIsLowercase()
            {
#line (84, 5) - (84, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Md5("hello");
#line (85, 5) - (85, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var hexStr = h.Hexdigest();
#line (86, 5) - (86, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(hexStr.Lower(), hexStr);
            }

            [Xunit.FactAttribute]
            public void TestSha256HexdigestIsLowercase()
            {
#line (90, 5) - (90, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha256("hello");
#line (91, 5) - (91, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var hexStr = h.Hexdigest();
#line (92, 5) - (92, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(hexStr.Lower(), hexStr);
            }

            [Xunit.FactAttribute]
            public void TestMd5CopyProducesIndependentClone()
            {
#line (98, 5) - (98, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Md5("hello");
#line (99, 5) - (99, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var copy = h.Copy();
#line (100, 5) - (100, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                copy.Update("world");
#line (102, 5) - (102, 64) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal("5d41402abc4b2a76b9719d911017c592", h.Hexdigest());
#line (104, 5) - (104, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(hashlib.Md5("helloworld").Hexdigest(), copy.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestSha1CopyProducesIndependentClone()
            {
#line (108, 5) - (108, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h = hashlib.Sha1("hello");
#line (109, 5) - (109, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var copy = h.Copy();
#line (110, 5) - (110, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                copy.Update("world");
#line (112, 5) - (112, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal("aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d", h.Hexdigest());
#line (114, 5) - (114, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(hashlib.Sha1("helloworld").Hexdigest(), copy.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestUpdateEmptyStringDoesNotChangeHash()
            {
#line (120, 5) - (120, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h1 = hashlib.Sha256("hello");
#line (121, 5) - (121, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h2 = hashlib.Sha256("hello");
#line (122, 5) - (122, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                h2.Update("");
#line (123, 5) - (123, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(h1.Hexdigest(), h2.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestMd5IncrementalUpdateMatchesSingleUpdate()
            {
#line (129, 5) - (129, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h1 = hashlib.Md5("helloworld");
#line (130, 5) - (130, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h2 = hashlib.Md5("hello");
#line (131, 5) - (131, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                h2.Update("world");
#line (132, 5) - (132, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(h1.Hexdigest(), h2.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestSha512IncrementalUpdateMatchesSingleUpdate()
            {
#line (136, 5) - (136, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h1 = hashlib.Sha512("helloworld");
#line (137, 5) - (137, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                var h2 = hashlib.Sha512("hello");
#line (138, 5) - (138, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                h2.Update("world");
#line (139, 5) - (139, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal(h1.Hexdigest(), h2.Hexdigest());
            }

            [Xunit.FactAttribute]
            public void TestMd5NameIsMd5()
            {
#line (145, 5) - (145, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal("md5", hashlib.Md5().Name);
            }

            [Xunit.FactAttribute]
            public void TestSha256NameIsSha256()
            {
#line (149, 5) - (149, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/hashlib/hashlib_complete_tests.spy"
                Xunit.Assert.Equal("sha256", hashlib.Sha256().Name);
            }
        }
    }
}
