#if NET10_0_OR_GREATER
using System;
using Xunit;

namespace Sharpy.Tests
{
    public class YamlTypedDeserializationTests
    {
        private class ServerConfig
        {
            public string Host { get; set; } = "";
            public int Port { get; set; }
            public bool Enabled { get; set; }
        }

        private class SnakeCaseConfig
        {
            // YAML uses snake_case keys; UnderscoredNamingConvention maps them to PascalCase.
            public string ServiceName { get; set; } = "";
            public int MaxConnections { get; set; }
        }

        private class NestedConfig
        {
            public string Label { get; set; } = "";
            public ServerConfig Server { get; set; } = new ServerConfig();
        }

        #region SafeLoadTyped - success

        [Fact]
        public void SafeLoadTyped_SimpleClass_Deserializes()
        {
            string yaml = "host: localhost\nport: 8080\nenabled: true\n";
            var result = Yaml.SafeLoadTyped<ServerConfig>(yaml);
            Assert.True(result.IsOk);
            var config = result.Unwrap();
            Assert.Equal("localhost", config.Host);
            Assert.Equal(8080, config.Port);
            Assert.True(config.Enabled);
        }

        [Fact]
        public void SafeLoadTyped_SnakeCaseKeys_MapToPascalCase()
        {
            string yaml = "service_name: api\nmax_connections: 100\n";
            var result = Yaml.SafeLoadTyped<SnakeCaseConfig>(yaml);
            Assert.True(result.IsOk);
            var config = result.Unwrap();
            Assert.Equal("api", config.ServiceName);
            Assert.Equal(100, config.MaxConnections);
        }

        [Fact]
        public void SafeLoadTyped_NestedClass_Deserializes()
        {
            string yaml =
                "label: outer\n" +
                "server:\n" +
                "  host: db\n" +
                "  port: 5432\n" +
                "  enabled: false\n";
            var result = Yaml.SafeLoadTyped<NestedConfig>(yaml);
            Assert.True(result.IsOk);
            var config = result.Unwrap();
            Assert.Equal("outer", config.Label);
            Assert.Equal("db", config.Server.Host);
            Assert.Equal(5432, config.Server.Port);
            Assert.False(config.Server.Enabled);
        }

        #endregion

        #region SafeLoadTyped - errors

        [Fact]
        public void SafeLoadTyped_TypeMismatch_ReturnsErr()
        {
            string yaml = "host: localhost\nport: not_a_number\nenabled: true\n";
            var result = Yaml.SafeLoadTyped<ServerConfig>(yaml);
            Assert.True(result.IsErr);
            var error = result.UnwrapErr();
            Assert.IsAssignableFrom<YAMLError>(error);
        }

        [Fact]
        public void SafeLoadTyped_MalformedYaml_ReturnsErr()
        {
            var result = Yaml.SafeLoadTyped<ServerConfig>("host: [unbalanced\n");
            Assert.True(result.IsErr);
        }

        [Fact]
        public void SafeLoadTyped_Null_ThrowsTypeError()
        {
            Assert.Throws<TypeError>(() => Yaml.SafeLoadTyped<ServerConfig>(null!));
        }

        #endregion
    }
}
#endif
