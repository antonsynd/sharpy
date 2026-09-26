// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using toml = global::Sharpy.Toml;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Toml.TomlTypedDeserializationTests
{
    [global::Sharpy.SharpyModule("toml.toml_typed_deserialization_tests")]
    public static partial class TomlTypedDeserializationTestsModule
    {
    }

    [global::Sharpy.SharpyModuleType("toml.toml_typed_deserialization_tests", "ServerConfig")]
    public class ServerConfig
    {
        public string Host = "";
        public long Port = 0;
        public bool Debug = false;
    }

    [global::Sharpy.SharpyModuleType("toml.toml_typed_deserialization_tests", "AppConfig")]
    public class AppConfig
    {
        public string Title = "";
        public global::Sharpy.Stdlib.Tests.Spy.Toml.TomlTypedDeserializationTests.ServerConfig Server = new global::Sharpy.Stdlib.Tests.Spy.Toml.TomlTypedDeserializationTests.ServerConfig();
    }

    [global::Sharpy.SharpyModuleType("toml.toml_typed_deserialization_tests", "DeployConfig")]
    public class DeployConfig
    {
        public string Name = "";
        public Sharpy.List<global::Sharpy.Stdlib.Tests.Spy.Toml.TomlTypedDeserializationTests.ServerConfig> Servers = new Sharpy.List<global::Sharpy.Stdlib.Tests.Spy.Toml.TomlTypedDeserializationTests.ServerConfig>()
        {
        };
    }

    public partial class TomlTypedDeserializationTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestLoadsTSimpleConfigDeserializes()
        {
#line (32, 5) - (32, 89) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            var result = toml.Loads<global::Sharpy.Stdlib.Tests.Spy.Toml.TomlTypedDeserializationTests.ServerConfig>("host = \"localhost\"\nport = 8080\ndebug = true");
#line (33, 5) - (33, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsOk);
#line (34, 5) - (34, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            var config = result.Unwrap();
#line (35, 5) - (35, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("localhost", config.Host);
#line (36, 5) - (36, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal(8080, config.Port);
#line (37, 5) - (37, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.True(config.Debug);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLoadsTNestedConfigDeserializes()
        {
#line (41, 5) - (41, 117) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            var result = toml.Loads<global::Sharpy.Stdlib.Tests.Spy.Toml.TomlTypedDeserializationTests.AppConfig>("title = \"My App\"\n\n[server]\nhost = \"0.0.0.0\"\nport = 3000\ndebug = false");
#line (42, 5) - (42, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsOk);
#line (43, 5) - (43, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            var config = result.Unwrap();
#line (44, 5) - (44, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("My App", config.Title);
#line (45, 5) - (45, 44) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("0.0.0.0", config.Server.Host);
#line (46, 5) - (46, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal(3000, config.Server.Port);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLoadsTMalformedTomlReturnsErr()
        {
#line (52, 5) - (52, 53) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            var result = toml.Loads<global::Sharpy.Stdlib.Tests.Spy.Toml.TomlTypedDeserializationTests.ServerConfig>("invalid = [");
#line (53, 5) - (53, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsErr);
#line (54, 5) - (54, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            var err = result.UnwrapErr();
#line (55, 5) - (55, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("invalid = [", err.Doc);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLoadsTTableArrayDeserializesToList()
        {
#line (59, 5) - (59, 128) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            string tomlStr = "name = \"prod\"\n\n[[servers]]\nhost = \"a.com\"\nport = 80\n\n[[servers]]\nhost = \"b.com\"\nport = 443";
#line (60, 5) - (60, 48) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            var result = toml.Loads<global::Sharpy.Stdlib.Tests.Spy.Toml.TomlTypedDeserializationTests.DeployConfig>(tomlStr);
#line (61, 5) - (61, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsOk);
#line (62, 5) - (62, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            var config = result.Unwrap();
#line (63, 5) - (63, 34) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("prod", config.Name);
#line (64, 5) - (64, 37) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(config.Servers));
#line (65, 5) - (65, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("a.com", config.Servers[0].Host);
#line (66, 5) - (66, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("b.com", config.Servers[1].Host);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestLoadsTTypeMismatchReturnsErr()
        {
#line (70, 5) - (70, 77) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            var result = toml.Loads<global::Sharpy.Stdlib.Tests.Spy.Toml.TomlTypedDeserializationTests.ServerConfig>("host = 123\nport = \"not_a_number\"");
#line (71, 5) - (71, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/toml/toml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsErr);
#line hidden
        }
    }
}
#line default
