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

            public class AppConfig
            {
                public string Title = "";
                public ServerConfig Server = new ServerConfig();
            }
        }
    }

    public static partial class Toml
    {
        public partial class TomlTypedDeserializationTestsTests
        {
            [Xunit.FactAttribute]
            public void TestLoadsTSimpleConfigDeserializes()
            {
#line (27, 5) - (27, 89) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var result = toml.Loads<ServerConfig>("host = \"localhost\"\nport = 8080\ndebug = true");
#line (28, 5) - (28, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (29, 5) - (29, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var config = result.Unwrap();
#line (30, 5) - (30, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("localhost", config.Host);
#line (31, 5) - (31, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(8080, config.Port);
#line (32, 5) - (32, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(config.Debug);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTNestedConfigDeserializes()
            {
#line (36, 5) - (36, 117) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var result = toml.Loads<AppConfig>("title = \"My App\"\n\n[server]\nhost = \"0.0.0.0\"\nport = 3000\ndebug = false");
#line (37, 5) - (37, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (38, 5) - (38, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var config = result.Unwrap();
#line (39, 5) - (39, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("My App", config.Title);
#line (40, 5) - (40, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("0.0.0.0", config.Server.Host);
#line (41, 5) - (41, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(3000, config.Server.Port);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTMalformedTomlReturnsErr()
            {
#line (47, 5) - (47, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var result = toml.Loads<ServerConfig>("invalid = [");
#line (48, 5) - (48, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (49, 5) - (49, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var err = result.UnwrapErr();
#line (50, 5) - (50, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("invalid = [", err.Doc);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTTypeMismatchReturnsErr()
            {
#line (54, 5) - (54, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var result = toml.Loads<ServerConfig>("host = 123\nport = \"not_a_number\"");
#line (55, 5) - (55, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
            }
        }
    }
}
