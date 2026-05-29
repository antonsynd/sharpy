#if NET10_0_OR_GREATER
using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Tests
{
    public class TomlTypedDeserializationTests
    {
        private class ServerConfig
        {
            public string Host { get; set; } = "";
            public long Port { get; set; }
            public bool Debug { get; set; }
        }

        private class AppConfig
        {
            public string Title { get; set; } = "";
            public ServerConfig Server { get; set; } = new ServerConfig();
        }

        #region Loads<T> — success cases

        [Fact]
        public void LoadsT_SimpleConfig_Deserializes()
        {
            var toml = "host = \"localhost\"\nport = 8080\ndebug = true";
            var result = Toml.Loads<ServerConfig>(toml);
            result.IsOk.Should().BeTrue();
            var config = result.Unwrap();
            config.Host.Should().Be("localhost");
            config.Port.Should().Be(8080);
            config.Debug.Should().BeTrue();
        }

        [Fact]
        public void LoadsT_NestedConfig_Deserializes()
        {
            var toml = "title = \"My App\"\n\n[server]\nhost = \"0.0.0.0\"\nport = 3000\ndebug = false";
            var result = Toml.Loads<AppConfig>(toml);
            result.IsOk.Should().BeTrue();
            var config = result.Unwrap();
            config.Title.Should().Be("My App");
            config.Server.Host.Should().Be("0.0.0.0");
            config.Server.Port.Should().Be(3000);
        }

        #endregion

        #region Loads<T> — error cases

        [Fact]
        public void LoadsT_MalformedToml_ReturnsErr()
        {
            var result = Toml.Loads<ServerConfig>("invalid = [");
            result.IsErr.Should().BeTrue();
            var err = result.UnwrapErr();
            err.Should().BeOfType<TOMLDecodeError>();
        }

        [Fact]
        public void LoadsT_NullInput_ThrowsTypeError()
        {
            var act = () => Toml.Loads<ServerConfig>(null!);
            act.Should().Throw<TypeError>();
        }

        #endregion

        #region Load<T>

        [Fact]
        public void LoadT_NullFp_ThrowsTypeError()
        {
            var act = () => Toml.Load<ServerConfig>(null!);
            act.Should().Throw<TypeError>();
        }

        #endregion
    }
}
#endif
