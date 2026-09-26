// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using yaml = global::Sharpy.Yaml;
using Xunit;

namespace Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests
{
    [global::Sharpy.SharpyModule("yaml.yaml_typed_deserialization_tests")]
    public static partial class YamlTypedDeserializationTestsModule
    {
    }

    [global::Sharpy.SharpyModuleType("yaml.yaml_typed_deserialization_tests", "ServerConfig")]
    public class ServerConfig
    {
        public string Host = "";
        public int Port = 0;
        public bool Enabled = false;
    }

    [global::Sharpy.SharpyModuleType("yaml.yaml_typed_deserialization_tests", "SnakeCaseConfig")]
    public class SnakeCaseConfig
    {
        public string ServiceName = "";
        public int MaxConnections = 0;
    }

    [global::Sharpy.SharpyModuleType("yaml.yaml_typed_deserialization_tests", "NestedConfig")]
    public class NestedConfig
    {
        public string Label = "";
        public global::Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests.ServerConfig Server = new global::Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests.ServerConfig();
    }

    [global::Sharpy.SharpyModuleType("yaml.yaml_typed_deserialization_tests", "RatioConfig")]
    public class RatioConfig
    {
        public double Ratio = 0.0d;
    }

    [global::Sharpy.SharpyModuleType("yaml.yaml_typed_deserialization_tests", "DataclassConfig")]
    public class DataclassConfig
    {
        public double Ratio { get; set; }

        public DataclassConfig(double ratio)
        {
            this.Ratio = ratio;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not DataclassConfig other)
                return false;
            return Equals(Ratio, other.Ratio);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Ratio);
        }

        public static bool operator ==(DataclassConfig? left, DataclassConfig? right) => Equals(left, right);
        public static bool operator !=(DataclassConfig? left, DataclassConfig? right) => !Equals(left, right);
        public override string ToString()
        {
            return $"DataclassConfig(ratio={Ratio})";
        }
    }

    [global::Sharpy.SharpyModuleType("yaml.yaml_typed_deserialization_tests", "DataclassMultiField")]
    public class DataclassMultiField
    {
        public string ServiceName { get; set; }
        public int MaxConnections { get; set; }
        public bool Enabled { get; set; }

        public DataclassMultiField(string serviceName, int maxConnections, bool enabled)
        {
            this.ServiceName = serviceName;
            this.MaxConnections = maxConnections;
            this.Enabled = enabled;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not DataclassMultiField other)
                return false;
            return Equals(ServiceName, other.ServiceName) && Equals(MaxConnections, other.MaxConnections) && Equals(Enabled, other.Enabled);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ServiceName, MaxConnections, Enabled);
        }

        public static bool operator ==(DataclassMultiField? left, DataclassMultiField? right) => Equals(left, right);
        public static bool operator !=(DataclassMultiField? left, DataclassMultiField? right) => !Equals(left, right);
        public override string ToString()
        {
            return $"DataclassMultiField(service_name={ServiceName}, max_connections={MaxConnections}, enabled={Enabled})";
        }
    }

    public partial class YamlTypedDeserializationTestsModuleTests
    {
        [Xunit.FactAttribute]
        public void TestSafeLoadTypedSimpleClassDeserializes()
        {
#line (38, 5) - (38, 96) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var result = yaml.SafeLoadTyped<global::Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests.ServerConfig>("host: localhost\nport: 8080\nenabled: true\n");
#line (39, 5) - (39, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsOk);
#line (40, 5) - (40, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var config = result.Unwrap();
#line (41, 5) - (41, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("localhost", config.Host);
#line (42, 5) - (42, 32) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal(8080, config.Port);
#line (43, 5) - (43, 27) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.True(config.Enabled);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSafeLoadTypedSnakeCaseKeysMapToPascalCase()
        {
#line (47, 5) - (47, 96) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var result = yaml.SafeLoadTyped<global::Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests.SnakeCaseConfig>("service_name: api\nmax_connections: 100\n");
#line (48, 5) - (48, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsOk);
#line (49, 5) - (49, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var config = result.Unwrap();
#line (50, 5) - (50, 41) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("api", config.ServiceName);
#line (51, 5) - (51, 42) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal(100, config.MaxConnections);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSafeLoadTypedNestedClassDeserializes()
        {
#line (55, 5) - (55, 119) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var result = yaml.SafeLoadTyped<global::Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests.NestedConfig>("label: outer\nserver:\n  host: db\n  port: 5432\n  enabled: false\n");
#line (56, 5) - (56, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsOk);
#line (57, 5) - (57, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var config = result.Unwrap();
#line (58, 5) - (58, 36) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("outer", config.Label);
#line (59, 5) - (59, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("db", config.Server.Host);
#line (60, 5) - (60, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal(5432, config.Server.Port);
#line (61, 5) - (61, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.False(config.Server.Enabled);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSafeLoadTypedTypeMismatchReturnsErr()
        {
#line (67, 5) - (67, 104) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var result = yaml.SafeLoadTyped<global::Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests.ServerConfig>("host: localhost\nport: not_a_number\nenabled: true\n");
#line (68, 5) - (68, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsErr);
#line (69, 5) - (69, 31) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var error = result.UnwrapErr();
#line (70, 5) - (70, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.IsAssignableFrom<global::Sharpy.YAMLError>(error);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSafeLoadTypedMalformedYamlReturnsErr()
        {
#line (74, 5) - (74, 71) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var result = yaml.SafeLoadTyped<global::Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests.ServerConfig>("host: [unbalanced\n");
#line (75, 5) - (75, 25) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsErr);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSafeLoadTypedFloatFieldKeepsDoublePrecision()
        {
#line (90, 5) - (90, 61) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var result = yaml.SafeLoadTyped<global::Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests.RatioConfig>("ratio: 0.1");
#line (91, 5) - (91, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsOk);
#line (92, 5) - (92, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var cfg = result.Unwrap();
#line (93, 5) - (93, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal(0.1d, cfg.Ratio);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSafeLoadTypedFloatFieldSurvivesNonFloat32ExactValues()
        {
#line (98, 5) - (98, 40) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var doc = "ratio: -0.30000000000000004";
#line (99, 5) - (99, 52) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var result = yaml.SafeLoadTyped<global::Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests.RatioConfig>(doc);
#line (100, 5) - (100, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsOk);
#line (101, 5) - (101, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var cfg = result.Unwrap();
#line (102, 5) - (102, 46) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal(-0.30000000000000004d, cfg.Ratio);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSafeLoadTypedDataclassTargetDeserializes()
        {
#line (131, 5) - (131, 65) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var result = yaml.SafeLoadTyped<global::Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests.DataclassConfig>("ratio: 0.1");
#line (132, 5) - (132, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsOk);
#line (133, 5) - (133, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var cfg = result.Unwrap();
#line (134, 5) - (134, 29) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal(0.1d, cfg.Ratio);
#line hidden
        }

        [Xunit.FactAttribute]
        public void TestSafeLoadTypedDataclassBindsEveryFieldByName()
        {
#line (141, 5) - (141, 69) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var doc = "service_name: api\nmax_connections: 100\nenabled: true\n";
#line (142, 5) - (142, 60) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var result = yaml.SafeLoadTyped<global::Sharpy.Stdlib.Tests.Spy.Yaml.YamlTypedDeserializationTests.DataclassMultiField>(doc);
#line (143, 5) - (143, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.True(result.IsOk);
#line (144, 5) - (144, 26) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            var cfg = result.Unwrap();
#line (145, 5) - (145, 38) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal("api", cfg.ServiceName);
#line (146, 5) - (146, 39) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.Equal(100, cfg.MaxConnections);
#line (147, 5) - (147, 24) 12 "src/Sharpy.Stdlib.Tests/Spy/yaml/yaml_typed_deserialization_tests.spy"
            Xunit.Assert.True(cfg.Enabled);
#line hidden
        }
    }
}
#line default
