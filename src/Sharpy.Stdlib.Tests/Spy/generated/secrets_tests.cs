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
using secrets = global::Sharpy.SecretsModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Secrets.SecretsTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Secrets
    {
        [global::Sharpy.SharpyModule("secrets.secrets_tests")]
        public static partial class SecretsTests
        {
        }
    }

    public static partial class Secrets
    {
        public partial class SecretsTestsTests
        {
            [Xunit.FactAttribute]
            public void TestTokenBytesReturnsCorrectLength()
            {
#line (7, 5) - (7, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Sharpy.Bytes result = secrets.TokenBytes(16);
#line (8, 5) - (8, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.Equal(16, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestTokenBytesDefaultLength32()
            {
#line (12, 5) - (12, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Sharpy.Bytes result = secrets.TokenBytes();
#line (13, 5) - (13, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.Equal(32, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestTokenBytesZeroReturnsEmpty()
            {
#line (17, 5) - (17, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Sharpy.Bytes result = secrets.TokenBytes(0);
#line (18, 5) - (18, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.Equal(0, global::Sharpy.Builtins.Len(result));
            }

            [Xunit.FactAttribute]
            public void TestTokenBytesNegativeThrows()
            {
#line (22, 5) - (25, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (23, 9) - (23, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    secrets.TokenBytes(-1);
                }));
            }

            [Xunit.FactAttribute]
            public void TestTokenHexReturnsCorrectLength()
            {
#line (27, 5) - (27, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                string result = secrets.TokenHex(16);
#line (28, 5) - (28, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.Equal(32, result.Length);
            }

            [Xunit.FactAttribute]
            public void TestTokenHexContainsOnlyHexChars()
            {
#line (32, 5) - (32, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                string result = secrets.TokenHex(32);
#line (34, 5) - (34, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                bool valid = true;
#line (35, 5) - (38, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                foreach (var __loopVar_0 in global::Sharpy.StringHelpers.Iterate(result))
                {
                    var c = __loopVar_0;
#line (36, 9) - (38, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    if (!"0123456789abcdef".Contains(c))
                    {
#line (37, 13) - (37, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                        valid = false;
                    }
                }

#line (38, 5) - (38, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.True(valid);
            }

            [Xunit.FactAttribute]
            public void TestTokenHexZeroReturnsEmpty()
            {
#line (42, 5) - (42, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                string result = secrets.TokenHex(0);
#line (43, 5) - (43, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.Equal(0, result.Length);
            }

            [Xunit.FactAttribute]
            public void TestTokenUrlsafeReturnsUrlSafeChars()
            {
#line (47, 5) - (47, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                string result = secrets.TokenUrlsafe(32);
#line (49, 5) - (49, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                bool valid = true;
#line (50, 5) - (53, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                foreach (var __loopVar_1 in global::Sharpy.StringHelpers.Iterate(result))
                {
                    var c = __loopVar_1;
#line (51, 9) - (53, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    if (!"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-".Contains(c))
                    {
#line (52, 13) - (52, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                        valid = false;
                    }
                }

#line (53, 5) - (53, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.True(valid);
            }

            [Xunit.FactAttribute]
            public void TestTokenUrlsafeZeroReturnsEmpty()
            {
#line (57, 5) - (57, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                string result = secrets.TokenUrlsafe(0);
#line (58, 5) - (58, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.Equal(0, result.Length);
            }

            [Xunit.FactAttribute]
            public void TestRandbelowReturnsInRange()
            {
#line (62, 5) - (62, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                int i = 0;
#line (63, 5) - (69, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                while (i < 100)
                {
#line (64, 9) - (64, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    int val = secrets.Randbelow(10);
#line (65, 9) - (65, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    Xunit.Assert.True(val >= 0);
#line (66, 9) - (66, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    Xunit.Assert.True(val < 10);
#line (67, 9) - (67, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestRandbelowOneAlwaysReturnsZero()
            {
#line (71, 5) - (71, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                int i = 0;
#line (72, 5) - (76, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                while (i < 10)
                {
#line (73, 9) - (73, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    Xunit.Assert.Equal(0, secrets.Randbelow(1));
#line (74, 9) - (74, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    i = i + 1;
                }
            }

            [Xunit.FactAttribute]
            public void TestRandbelowZeroThrows()
            {
#line (78, 5) - (81, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (79, 9) - (79, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    secrets.Randbelow(0);
                }));
            }

            [Xunit.FactAttribute]
            public void TestRandbelowNegativeThrows()
            {
#line (83, 5) - (86, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (84, 9) - (84, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    secrets.Randbelow(-5);
                }));
            }

            [Xunit.FactAttribute]
            public void TestChoiceReturnsElementFromList()
            {
#line (88, 5) - (88, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                    10,
                    20,
                    30
                };
#line (89, 5) - (89, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                int result = secrets.Choice(items);
#line (90, 5) - (90, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.Contains(result, items);
            }

            [Xunit.FactAttribute]
            public void TestChoiceEmptyListThrows()
            {
#line (94, 5) - (94, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Sharpy.List<int> items = new Sharpy.List<int>()
                {
                };
#line (95, 5) - (98, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (96, 9) - (96, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                    secrets.Choice(items);
                }));
            }

            [Xunit.FactAttribute]
            public void TestCompareDigestMatchingStrings()
            {
#line (100, 5) - (100, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.True(secrets.CompareDigest("hello", "hello"));
            }

            [Xunit.FactAttribute]
            public void TestCompareDigestNonMatchingStrings()
            {
#line (104, 5) - (104, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.False(secrets.CompareDigest("hello", "world"));
            }

            [Xunit.FactAttribute]
            public void TestCompareDigestDifferentLengths()
            {
#line (108, 5) - (108, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.False(secrets.CompareDigest("short", "longer string"));
            }

            [Xunit.FactAttribute]
            public void TestCompareDigestEmptyStrings()
            {
#line (112, 5) - (112, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.True(secrets.CompareDigest("", ""));
            }

            [Xunit.FactAttribute]
            public void TestCompareDigestMatchingBytes()
            {
#line (116, 5) - (116, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Sharpy.Bytes x = new Sharpy.Bytes(new byte[] { 1, 2, 3 });
#line (117, 5) - (117, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Sharpy.Bytes y = new Sharpy.Bytes(new byte[] { 1, 2, 3 });
#line (118, 5) - (118, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.True(secrets.CompareDigest(x, y));
            }

            [Xunit.FactAttribute]
            public void TestCompareDigestNonMatchingBytes()
            {
#line (122, 5) - (122, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Sharpy.Bytes x = new Sharpy.Bytes(new byte[] { 1, 2, 3 });
#line (123, 5) - (123, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Sharpy.Bytes y = new Sharpy.Bytes(new byte[] { 4, 5, 6 });
#line (124, 5) - (124, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/secrets/secrets_tests.spy"
                Xunit.Assert.False(secrets.CompareDigest(x, y));
            }
        }
    }
}
