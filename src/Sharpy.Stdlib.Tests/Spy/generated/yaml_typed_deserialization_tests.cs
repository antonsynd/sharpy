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

            public class RatioConfig
            {
                public double Ratio = 0.0d;
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
#line (38, 5) - (38, 96) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<ServerConfig>("host: localhost\nport: 8080\nenabled: true\n");
#line (39, 5) - (39, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (40, 5) - (40, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var config = result.Unwrap();
#line (41, 5) - (41, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("localhost", config.Host);
#line (42, 5) - (42, 32) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(8080, config.Port);
#line (43, 5) - (43, 27) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(config.Enabled);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTypedSnakeCaseKeysMapToPascalCase()
            {
#line (47, 5) - (47, 96) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<SnakeCaseConfig>("service_name: api\nmax_connections: 100\n");
#line (48, 5) - (48, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (49, 5) - (49, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var config = result.Unwrap();
#line (50, 5) - (50, 41) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("api", config.ServiceName);
#line (51, 5) - (51, 42) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(100, config.MaxConnections);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTypedNestedClassDeserializes()
            {
#line (55, 5) - (55, 119) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<NestedConfig>("label: outer\nserver:\n  host: db\n  port: 5432\n  enabled: false\n");
#line (56, 5) - (56, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (57, 5) - (57, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var config = result.Unwrap();
#line (58, 5) - (58, 36) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("outer", config.Label);
#line (59, 5) - (59, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal("db", config.Server.Host);
#line (60, 5) - (60, 39) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(5432, config.Server.Port);
#line (61, 5) - (61, 38) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.False(config.Server.Enabled);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTypedTypeMismatchReturnsErr()
            {
#line (67, 5) - (67, 104) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<ServerConfig>("host: localhost\nport: not_a_number\nenabled: true\n");
#line (68, 5) - (68, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line (69, 5) - (69, 31) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var error = result.UnwrapErr();
#line (70, 5) - (70, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.IsAssignableFrom<global::Sharpy.YAMLError>(error);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTypedMalformedYamlReturnsErr()
            {
#line (74, 5) - (74, 71) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<ServerConfig>("host: [unbalanced\n");
#line (75, 5) - (75, 25) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsErr);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTypedFloatFieldKeepsDoublePrecision()
            {
#line (90, 5) - (90, 61) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<RatioConfig>("ratio: 0.1");
#line (91, 5) - (91, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (92, 5) - (92, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var cfg = result.Unwrap();
#line (93, 5) - (93, 29) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(0.1d, cfg.Ratio);
#line hidden
            }

            [Xunit.FactAttribute]
            public void TestSafeLoadTypedFloatFieldSurvivesNonFloat32ExactValues()
            {
#line (98, 5) - (98, 40) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var doc = "ratio: -0.30000000000000004";
#line (99, 5) - (99, 52) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var result = yaml.SafeLoadTyped<RatioConfig>(doc);
#line (100, 5) - (100, 24) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.True(result.IsOk);
#line (101, 5) - (101, 26) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                var cfg = result.Unwrap();
#line (102, 5) - (102, 46) 16 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
                Xunit.Assert.Equal(-0.30000000000000004d, cfg.Ratio);
#line hidden
            }
        }
    }
}
#line default
