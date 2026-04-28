using FluentAssertions;
using Newtonsoft.Json.Linq;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class LspConfigurationTests
{
    [Fact]
    public void Defaults_TransitionHintsEnabledIsTrue()
    {
        var config = new LspConfiguration();
        config.TransitionHintsEnabled.Should().BeTrue();
    }

    [Fact]
    public void UpdateFrom_Null_LeavesDefaults()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(null);
        config.TransitionHintsEnabled.Should().BeTrue();
    }

    [Fact]
    public void UpdateFrom_DisablesTransitionHints()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"transitionHints":{"enabled":false}}"""));
        config.TransitionHintsEnabled.Should().BeFalse();
    }

    [Fact]
    public void UpdateFrom_AcceptsSharpyNamespacedSettings()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"sharpy":{"transitionHints":{"enabled":false}}}"""));
        config.TransitionHintsEnabled.Should().BeFalse();
    }

    [Fact]
    public void UpdateFrom_RoundTripEnableDisable()
    {
        var config = new LspConfiguration();

        config.UpdateFrom(JToken.Parse("""{"transitionHints":{"enabled":false}}"""));
        config.TransitionHintsEnabled.Should().BeFalse();

        config.UpdateFrom(JToken.Parse("""{"transitionHints":{"enabled":true}}"""));
        config.TransitionHintsEnabled.Should().BeTrue();
    }

    [Fact]
    public void UpdateFrom_NonBooleanValueIsIgnored()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"transitionHints":{"enabled":"yes"}}"""));
        // Default preserved when the value is not a JSON boolean.
        config.TransitionHintsEnabled.Should().BeTrue();
    }

    [Fact]
    public void UpdateFrom_MissingTransitionHintsKeepsDefault()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"someOther":"setting"}"""));
        config.TransitionHintsEnabled.Should().BeTrue();
    }
}
