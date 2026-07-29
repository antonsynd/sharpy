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

    // sharpy.features — the editor's counterpart of --enable-feature (#1149). Names are validated
    // where they become compiler options (SharpyWorkspace.SetConfiguredFeatures), so parsing only
    // has to extract them faithfully.

    [Fact]
    public void Defaults_FeaturesIsEmpty()
    {
        new LspConfiguration().Features.Should().BeEmpty();
    }

    [Fact]
    public void UpdateFrom_ReadsFeatureArray()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"features":["defer","matmul"]}"""));
        config.Features.Should().Equal("defer", "matmul");
    }

    [Fact]
    public void UpdateFrom_ReadsSharpyNamespacedFeatures()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"sharpy":{"features":["defer"]}}"""));
        config.Features.Should().Equal("defer");
    }

    [Fact]
    public void UpdateFrom_AcceptsSeparatedString()
    {
        // A user who writes the setting the way <Features> is written in a .spyproj is understood
        // rather than silently ignored.
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"features":"defer; matmul"}"""));
        config.Features.Should().Equal("defer", "matmul");
    }

    [Fact]
    public void UpdateFrom_IgnoresNonStringEntries()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"features":["defer",7,null]}"""));
        config.Features.Should().Equal("defer");
    }

    [Fact]
    public void UpdateFrom_EmptyFeatureArrayClearsFeatures()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"features":["defer"]}"""));

        config.UpdateFrom(JToken.Parse("""{"features":[]}"""));

        config.Features.Should().BeEmpty("removing the setting must disable the features it enabled");
    }

    // sharpy.lsp.maxNumberOfProblems and sharpy.inlayHints.typeAnnotations (#1165).

    [Fact]
    public void Defaults_NoProblemCapAndTypeAnnotationsOn()
    {
        var config = new LspConfiguration();
        config.MaxNumberOfProblems.Should().BeNull();
        config.InlayHintTypeAnnotations.Should().BeTrue();
    }

    [Fact]
    public void UpdateFrom_ReadsMaxNumberOfProblems()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"lsp":{"maxNumberOfProblems":100}}"""));
        config.MaxNumberOfProblems.Should().Be(100);
    }

    [Fact]
    public void UpdateFrom_ZeroMaxNumberOfProblemsIsARealCap()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"lsp":{"maxNumberOfProblems":0}}"""));
        config.MaxNumberOfProblems.Should().Be(0);
    }

    [Fact]
    public void UpdateFrom_NegativeMaxNumberOfProblemsMeansUncapped()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"lsp":{"maxNumberOfProblems":-5}}"""));
        config.MaxNumberOfProblems.Should().BeNull();
    }

    [Fact]
    public void UpdateFrom_NullMaxNumberOfProblemsClearsTheCap()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"lsp":{"maxNumberOfProblems":10}}"""));

        config.UpdateFrom(JToken.Parse("""{"lsp":{"maxNumberOfProblems":null}}"""));

        config.MaxNumberOfProblems.Should().BeNull();
    }

    [Fact]
    public void UpdateFrom_NonNumericMaxNumberOfProblemsKeepsPrevious()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"lsp":{"maxNumberOfProblems":10}}"""));

        config.UpdateFrom(JToken.Parse("""{"lsp":{"maxNumberOfProblems":"lots"}}"""));

        config.MaxNumberOfProblems.Should().Be(10,
            "a malformed value must not silently start hiding diagnostics");
    }

    [Fact]
    public void UpdateFrom_ReadsInlayHintTypeAnnotations()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"inlayHints":{"typeAnnotations":false}}"""));
        config.InlayHintTypeAnnotations.Should().BeFalse();
    }

    [Fact]
    public void UpdateFrom_InlayHintTypeAnnotationsRoundTrips()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"sharpy":{"inlayHints":{"typeAnnotations":false}}}"""));
        config.InlayHintTypeAnnotations.Should().BeFalse();

        config.UpdateFrom(JToken.Parse("""{"sharpy":{"inlayHints":{"typeAnnotations":true}}}"""));
        config.InlayHintTypeAnnotations.Should().BeTrue();
    }

    [Fact]
    public void UpdateFrom_UnrelatedInlayHintKeysAreIgnored()
    {
        // sharpy.inlayHints.parameterNames was dropped from the contribution (#1165); an old
        // client still sending it must not disturb the setting next to it.
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"inlayHints":{"parameterNames":false}}"""));
        config.InlayHintTypeAnnotations.Should().BeTrue();
    }

    [Fact]
    public void UpdateFrom_MissingFeaturesKeepsPrevious()
    {
        var config = new LspConfiguration();
        config.UpdateFrom(JToken.Parse("""{"features":["defer"]}"""));

        config.UpdateFrom(JToken.Parse("""{"transitionHints":{"enabled":false}}"""));

        config.Features.Should().Equal(new[] { "defer" },
            "a settings payload that omits the section says nothing about it");
    }
}
