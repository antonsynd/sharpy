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
using yaml = global::Sharpy.Yaml;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class Yaml
    {
        [global::Sharpy.SharpyModule("yaml.yaml_typed_deserialization_tests")]
        public static partial class YamlTypedDeserializationTests
        {
            public class ServerConfig
            {
                public string Host = "";
                public int Port = 0;
                public bool Enabled = false;
            }

            public class SnakeCaseConfig
            {
                public string ServiceName = "";
                public int MaxConnections = 0;
            }

            public class NestedConfig
            {
                public string Label = "";
                public ServerConfig Server = new ServerConfig();
            }
        }
    }

    public static partial class Yaml
    {
        public partial class YamlTypedDeserializationTestsTests
        {
            [Xunit.FactAttribute]
            public void TestSafeLoadTypedSimpleClassDeserializes()
            {
#line (34, 5) - (34, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<ServerConfig>("host: localhost\nport: 8080\nenabled: true\n");
#line (35, 5) - (35, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (36, 5) - (36, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var config = result.Unwrap();
#line (37, 5) - (37, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("localhost", config.Host);
#line (38, 5) - (38, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(8080, config.Port);
#line (39, 5) - (39, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(config.Enabled);
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTypedSnakeCaseKeysMapToPascalCase()
            {
#line (43, 5) - (43, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<SnakeCaseConfig>("service_name: api\nmax_connections: 100\n");
#line (44, 5) - (44, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (45, 5) - (45, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var config = result.Unwrap();
#line (46, 5) - (46, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("api", config.ServiceName);
#line (47, 5) - (47, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(100, config.MaxConnections);
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTypedNestedClassDeserializes()
            {
#line (51, 5) - (51, 119) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<NestedConfig>("label: outer\nserver:\n  host: db\n  port: 5432\n  enabled: false\n");
#line (52, 5) - (52, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (53, 5) - (53, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var config = result.Unwrap();
#line (54, 5) - (54, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("outer", config.Label);
#line (55, 5) - (55, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("db", config.Server.Host);
#line (56, 5) - (56, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(5432, config.Server.Port);
#line (57, 5) - (57, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.False(config.Server.Enabled);
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTypedTypeMismatchReturnsErr()
            {
#line (63, 5) - (63, 104) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<ServerConfig>("host: localhost\nport: not_a_number\nenabled: true\n");
#line (64, 5) - (64, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (65, 5) - (65, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var error = result.UnwrapErr();
#line (66, 5) - (66, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(error is global::Sharpy.YAMLError);
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTypedMalformedYamlReturnsErr()
            {
#line (70, 5) - (70, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<ServerConfig>("host: [unbalanced\n");
#line (71, 5) - (71, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
            }
        }
    }
}
