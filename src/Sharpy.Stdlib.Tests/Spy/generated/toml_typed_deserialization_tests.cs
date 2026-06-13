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
using toml = global::Sharpy.Toml;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Toml.TomlTypedDeserializationTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Toml
    {
        [global::Sharpy.SharpyModule("toml.toml_typed_deserialization_tests")]
        public static partial class TomlTypedDeserializationTests
        {
            public class ServerConfig
            {
                public string Host = "";
                public long Port = 0;
                public bool Debug = false;
            }
        }
    }

    public static partial class Toml
    {
        public partial class TomlTypedDeserializationTestsTests
        {
            [Xunit.FactAttribute]
            public void TestLoadsTMalformedTomlReturnsErr()
            {
#line (26, 5) - (26, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var result = toml.Loads<ServerConfig>("invalid = [");
#line (27, 5) - (27, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (28, 5) - (28, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var err = result.UnwrapErr();
#line (29, 5) - (29, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("invalid = [", err.Doc);
            }
        }
    }
}
