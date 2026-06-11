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
using uuid = global::Sharpy.UuidModule;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Uuid.UuidModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Uuid
    {
        [global::Sharpy.SharpyModule("uuid.uuid_module_tests")]
        public static partial class UuidModuleTests
        {
        }
    }

    public static partial class Uuid
    {
        public partial class UuidModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestUuid4GeneratesValidVersion4()
            {
#line (7, 5) - (7, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id = uuid.Uuid4();
#line (8, 5) - (8, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal(4, id.Version);
#line (9, 5) - (9, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal("specified in RFC 4122", id.Variant);
            }

            [Xunit.FactAttribute]
            public void TestUuid4GeneratesUniqueValues()
            {
#line (13, 5) - (13, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id1 = uuid.Uuid4();
#line (14, 5) - (14, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id2 = uuid.Uuid4();
#line (15, 5) - (15, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.NotEqual(id2, id1);
            }

            [Xunit.FactAttribute]
            public void TestUuidParseFromStringStandardFormat()
            {
#line (19, 5) - (19, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id = new global::Sharpy.UUID("12345678-1234-5678-1234-567812345678");
#line (20, 5) - (20, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal("12345678-1234-5678-1234-567812345678", global::Sharpy.Builtins.Str(id));
            }

            [Xunit.FactAttribute]
            public void TestUuidParseFromStringNoBraces()
            {
#line (24, 5) - (24, 67) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id = new global::Sharpy.UUID("12345678123456781234567812345678");
#line (25, 5) - (25, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal("12345678123456781234567812345678", id.Hex);
            }

            [Xunit.FactAttribute]
            public void TestUuidParseFromStringInvalidThrowsValueError()
            {
#line (29, 5) - (32, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (30, 9) - (30, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                    new global::Sharpy.UUID("not-a-uuid");
                }));
            }

            [Xunit.FactAttribute]
            public void TestUuidHexReturnsNoDashes()
            {
#line (34, 5) - (34, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id = new global::Sharpy.UUID("12345678-1234-5678-1234-567812345678");
#line (35, 5) - (35, 57) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal("12345678123456781234567812345678", id.Hex);
#line (36, 5) - (36, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal(32, global::Sharpy.Builtins.Len(id.Hex));
            }

            [Xunit.FactAttribute]
            public void TestUuidUuidBytesReturns16Bytes()
            {
#line (40, 5) - (40, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id = new global::Sharpy.UUID("12345678-1234-5678-1234-567812345678");
#line (41, 5) - (41, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal(16, global::Sharpy.Builtins.Len(id.UuidBytes));
            }

            [Xunit.FactAttribute]
            public void TestUuidEquality()
            {
#line (45, 5) - (45, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id1 = new global::Sharpy.UUID("12345678-1234-5678-1234-567812345678");
#line (46, 5) - (46, 72) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id2 = new global::Sharpy.UUID("12345678-1234-5678-1234-567812345678");
#line (47, 5) - (47, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal(id2, id1);
            }

            [Xunit.FactAttribute]
            public void TestUuidUrn()
            {
#line (51, 5) - (51, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id = new global::Sharpy.UUID("12345678-1234-5678-1234-567812345678");
#line (52, 5) - (52, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal("urn:uuid:12345678-1234-5678-1234-567812345678", id.Urn);
            }

            [Xunit.FactAttribute]
            public void TestUuid3KnownValue()
            {
#line (56, 5) - (56, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID result = uuid.Uuid3(uuid.NAMESPACE_DNS, "example.com");
#line (57, 5) - (57, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal("9073926b-929f-31c2-abc9-fad77ae3e8eb", global::Sharpy.Builtins.Str(result));
#line (58, 5) - (58, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal(3, result.Version);
            }

            [Xunit.FactAttribute]
            public void TestUuid5KnownValue()
            {
#line (62, 5) - (62, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID result = uuid.Uuid5(uuid.NAMESPACE_DNS, "example.com");
#line (63, 5) - (63, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal("cfbff0d1-9375-5685-968c-48ce8b15ae17", global::Sharpy.Builtins.Str(result));
#line (64, 5) - (64, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal(5, result.Version);
            }

            [Xunit.FactAttribute]
            public void TestUuid1GeneratesVersion1()
            {
#line (68, 5) - (68, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id = uuid.Uuid1();
#line (69, 5) - (69, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal(1, id.Version);
#line (70, 5) - (70, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal("specified in RFC 4122", id.Variant);
            }

            [Xunit.FactAttribute]
            public void TestUuidRfcFields()
            {
#line (74, 5) - (74, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                global::Sharpy.UUID id = new global::Sharpy.UUID("12345678-1234-5678-1234-567812345678");
#line (75, 5) - (75, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal(305419896, id.TimeLow);
#line (76, 5) - (76, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal(4660, id.TimeMid);
#line (77, 5) - (77, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/uuid/uuid_module_tests.spy"
                Xunit.Assert.Equal(22136, id.TimeHiVersion);
            }
        }
    }
}
