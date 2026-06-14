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

            public class DeployConfig
            {
                public string Name = "";
                public Sharpy.List<ServerConfig> Servers = new Sharpy.List<ServerConfig>()
                {
                };
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
#line (32, 5) - (32, 89) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var result = toml.Loads<ServerConfig>("host = \"localhost\"\nport = 8080\ndebug = true");
#line (33, 5) - (33, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (34, 5) - (34, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var config = result.Unwrap();
#line (35, 5) - (35, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("localhost", config.Host);
#line (36, 5) - (36, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(8080, config.Port);
#line (37, 5) - (37, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(config.Debug);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTNestedConfigDeserializes()
            {
#line (41, 5) - (41, 117) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var result = toml.Loads<AppConfig>("title = \"My App\"\n\n[server]\nhost = \"0.0.0.0\"\nport = 3000\ndebug = false");
#line (42, 5) - (42, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (43, 5) - (43, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var config = result.Unwrap();
#line (44, 5) - (44, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("My App", config.Title);
#line (45, 5) - (45, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("0.0.0.0", config.Server.Host);
#line (46, 5) - (46, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(3000, config.Server.Port);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTMalformedTomlReturnsErr()
            {
#line (52, 5) - (52, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var result = toml.Loads<ServerConfig>("invalid = [");
#line (53, 5) - (53, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (54, 5) - (54, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var err = result.UnwrapErr();
#line (55, 5) - (55, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("invalid = [", err.Doc);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTTableArrayDeserializesToList()
            {
#line (59, 5) - (59, 128) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                string tomlStr = "name = \"prod\"\n\n[[servers]]\nhost = \"a.com\"\nport = 80\n\n[[servers]]\nhost = \"b.com\"\nport = 443";
#line (60, 5) - (60, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var result = toml.Loads<DeployConfig>(tomlStr);
#line (61, 5) - (61, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (62, 5) - (62, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var config = result.Unwrap();
#line (63, 5) - (63, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("prod", config.Name);
#line (64, 5) - (64, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(config.Servers));
#line (65, 5) - (65, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("a.com", config.Servers[0].Host);
#line (66, 5) - (66, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("b.com", config.Servers[1].Host);
            }

            [Xunit.FactAttribute]
            public void TestLoadsTTypeMismatchReturnsErr()
            {
#line (70, 5) - (70, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                var result = toml.Loads<ServerConfig>("host = 123\nport = \"not_a_number\"");
#line (71, 5) - (71, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
            }
        }
    }
}
