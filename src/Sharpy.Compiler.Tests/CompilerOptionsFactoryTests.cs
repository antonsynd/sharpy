using System.Collections;
using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests;

/// <summary>
/// Behavior-preservation tests for <see cref="CompilerOptionsFactory"/> (#1144): one representative
/// per entry surface, asserting the factory produces options value-identical to the hand-assembled
/// initializer that surface used before the migration. Each expected literal below is the verbatim
/// initializer from the migrated call site, so a future change to a per-surface method that silently
/// alters what an entry point compiles with fails here rather than in a user report.
/// </summary>
public class CompilerOptionsFactoryTests
{
    [Fact]
    public void Default_MatchesBareCompilerOptions()
    {
        // Compiler ctor, CompilerApi.Compile and CompilerApi.AnalyzeProject defaults.
        AssertSameOptions(new CompilerOptions(), CompilerOptionsFactory.Default());
    }

    [Fact]
    public void ForCli_MatchesBuildCommandInitializer()
    {
        var references = new[] { "Sharpy.Core.dll" };
        var modulePaths = new[] { "/modules" };
        var nowarn = new HashSet<string>(new[] { "SPY0451" }, StringComparer.OrdinalIgnoreCase);
        var features = FeatureFlags.None.Enable("matmul");

        var expected = new CompilerOptions
        {
            OutputType = "exe",
            References = references,
            ModulePaths = modulePaths,
            WarningsAsErrors = true,
            SuppressedWarnings = nowarn,
            MaxErrors = 7,
            Features = features,
            Configuration = "Release",
            AssemblyName = "app",
            OutputAssemblyPath = "/out/app.exe"
        };

        var actual = CompilerOptionsFactory.ForCli(
            outputType: "exe",
            references: references,
            modulePaths: modulePaths,
            warningsAsErrors: true,
            suppressedWarnings: nowarn,
            maxErrors: 7,
            features: features,
            configuration: "Release",
            assemblyName: "app",
            outputAssemblyPath: "/out/app.exe");

        AssertSameOptions(expected, actual);
    }

    [Fact]
    public void ForCli_MatchesEmitCSharpInitializer()
    {
        var expected = new CompilerOptions
        {
            OutputType = "library",
            References = Array.Empty<string>(),
            ModulePaths = Array.Empty<string>(),
            WarningsAsErrors = false,
            SuppressedWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            MaxErrors = 0,
            Namespace = "Game.Scripts",
            Features = FeatureFlags.None
        };

        var actual = CompilerOptionsFactory.ForCli(
            outputType: "library",
            references: Array.Empty<string>(),
            modulePaths: Array.Empty<string>(),
            @namespace: "Game.Scripts");

        AssertSameOptions(expected, actual);
    }

    [Fact]
    public void ForProject_MatchesProjectCommandInitializer()
    {
        var config = new ProjectConfig
        {
            References = { "Project.dll" },
            ModulePaths = { "/project/modules" },
            WarningsAsErrors = true,
            SuppressedWarnings = { "SPY0452" }
        };
        var defaultReferences = new[] { "Sharpy.Core.dll", "Sharpy.Stdlib.dll" };
        var nowarn = new HashSet<string>(new[] { "SPY0451" }, StringComparer.OrdinalIgnoreCase);

        var mergedSuppressed = new HashSet<string>(config.SuppressedWarnings);
        mergedSuppressed.UnionWith(nowarn);
        var expected = new CompilerOptions
        {
            References = defaultReferences.Concat(config.References).Distinct().ToArray(),
            ModulePaths = config.ModulePaths.ToArray(),
            // warnAsError (false here) OR-ed with the project's own setting.
            WarningsAsErrors = false || config.WarningsAsErrors,
            SuppressedWarnings = mergedSuppressed,
            MaxErrors = 3,
            Incremental = true,
            Features = FeatureFlags.None.Enable("defer")
        };

        var actual = CompilerOptionsFactory.ForProject(
            config,
            defaultReferences: defaultReferences,
            warningsAsErrors: false,
            additionalSuppressedWarnings: nowarn,
            maxErrors: 3,
            incremental: true,
            features: FeatureFlags.None.Enable("defer"));

        AssertSameOptions(expected, actual);
    }

    [Fact]
    public void ForProject_WithNoCliFlags_TakesEverythingFromTheProject()
    {
        // ProjectCommand.CompileProjectForServer: no --warn-as-error / --nowarn / --max-errors
        // / --enable-feature to layer, so the project's own settings are the whole story.
        var config = new ProjectConfig
        {
            References = { "Project.dll" },
            ModulePaths = { "/project/modules" },
            WarningsAsErrors = true,
            SuppressedWarnings = { "SPY0452" },
            Features = { "matmul" }
        };

        var expected = new CompilerOptions
        {
            References = config.References.ToArray(),
            ModulePaths = config.ModulePaths.ToArray(),
            WarningsAsErrors = true,
            SuppressedWarnings = new HashSet<string>(config.SuppressedWarnings),
            Incremental = true,
            Features = FeatureFlags.None.Enable("matmul")
        };

        AssertSameOptions(expected, CompilerOptionsFactory.ForProject(config, incremental: true));
    }

    [Fact]
    public void ForProject_LayersCliFeaturesOverProjectFeatures()
    {
        var config = new ProjectConfig
        {
            References = { "Project.dll" },
            ModulePaths = { "/project/modules" },
            Features = { "matmul" }
        };

        var options = CompilerOptionsFactory.ForProject(
            config,
            features: FeatureFlags.None.Enable("defer"));

        options.Features.EnabledFeatures.Should().BeEquivalentTo(new[] { "defer", "matmul" });
    }

    [Fact]
    public void ForProject_WithNoProjectFeatures_TakesCliOnly()
    {
        var config = new ProjectConfig
        {
            References = { "Project.dll" },
            ModulePaths = { "/project/modules" }
        };

        var options = CompilerOptionsFactory.ForProject(
            config,
            features: FeatureFlags.None.Enable("defer"));

        options.Features.EnabledFeatures.Should().BeEquivalentTo(new[] { "defer" });
    }

    [Fact]
    public void ForLsp_WithNoSources_MatchesTheWorkspaceHardcode()
    {
        // SharpyWorkspace's former `new() { OutputType = "library" }` field initializer.
        AssertSameOptions(
            new CompilerOptions { OutputType = "library" },
            CompilerOptionsFactory.ForLsp());
    }

    [Fact]
    public void ForLsp_LayersWorkspaceConfigurationOverTheProject()
    {
        var config = new ProjectConfig
        {
            References = { "Project.dll" },
            ModulePaths = { "/project/modules" },
            WarningsAsErrors = true,
            SuppressedWarnings = { "SPY0452" },
            Features = { "matmul" }
        };

        var options = CompilerOptionsFactory.ForLsp(
            config,
            features: FeatureFlags.None.Enable("defer"),
            suppressedWarnings: new[] { "SPY0451" },
            maxErrors: 5,
            references: new[] { "Extra.dll" });

        options.OutputType.Should().Be("library");
        options.Features.EnabledFeatures.Should().BeEquivalentTo(new[] { "defer", "matmul" });
        options.WarningsAsErrors.Should().BeTrue("the project's setting is OR-ed in, not overwritten");
        options.SuppressedWarnings.Should().BeEquivalentTo(new[] { "SPY0451", "SPY0452" });
        options.MaxErrors.Should().Be(5);
        options.References.Should().Equal("Project.dll", "Extra.dll");
        options.ModulePaths.Should().Equal("/project/modules");
    }

    [Fact]
    public void ForRepl_MatchesTheReplSessionDefaults()
    {
        var features = FeatureFlags.None.Enable("__test_feature");

        var expected = new CompilerOptions
        {
            OutputType = "exe",
            WarningsAsErrors = false,
            SuppressedWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            MaxErrors = 0,
            Incremental = false,
            Features = features
        };

        AssertSameOptions(expected, CompilerOptionsFactory.ForRepl(features));
    }

    [Fact]
    public void ForLibraryAnalysis_MatchesTheAnalyzeAndPlaygroundInitializer()
    {
        AssertSameOptions(
            new CompilerOptions { OutputType = "library" },
            CompilerOptionsFactory.ForLibraryAnalysis());
    }

    [Fact]
    public void SuppressedWarnings_AreAlwaysCaseInsensitiveAndOwnedByTheOptions()
    {
        var caller = new HashSet<string>(new[] { "spy0451" }); // ordinal comparer, lower case

        var options = CompilerOptionsFactory.ForCli(suppressedWarnings: caller);

        options.SuppressedWarnings.Should().Contain("SPY0451",
            "the seam normalizes to OrdinalIgnoreCase so a lower-case --nowarn/<NoWarn> still suppresses");
        options.SuppressedWarnings.Should().NotBeSameAs(caller,
            "the options own their set; mutating the caller's must not reach a built option");
    }

    /// <summary>
    /// Asserts two <see cref="CompilerOptions"/> carry the same value for every public property —
    /// reflection-driven, so a member added to <see cref="CompilerOptions"/> is compared here
    /// automatically instead of being silently skipped by a hand-written property list.
    /// </summary>
    private static void AssertSameOptions(CompilerOptions expected, CompilerOptions actual)
    {
        foreach (var property in typeof(CompilerOptions).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var expectedValue = property.GetValue(expected);
            var actualValue = property.GetValue(actual);

            if (expectedValue is FeatureFlags expectedFlags)
            {
                actualValue.Should().BeOfType<FeatureFlags>();
                ((FeatureFlags)actualValue!).EnabledFeatures.Should().Equal(expectedFlags.EnabledFeatures,
                    $"CompilerOptions.{property.Name} must match");
                continue;
            }

            if (expectedValue is IEnumerable and not string)
            {
                var expectedItems = ((IEnumerable)expectedValue).Cast<object>().ToList();
                var actualItems = (actualValue as IEnumerable)?.Cast<object>().ToList();
                actualItems.Should().NotBeNull($"CompilerOptions.{property.Name} must not be null");
                actualItems.Should().BeEquivalentTo(expectedItems, $"CompilerOptions.{property.Name} must match");
                continue;
            }

            actualValue.Should().Be(expectedValue, $"CompilerOptions.{property.Name} must match");
        }
    }

    /// <summary>
    /// The end-to-end #1554 acceptance: a PARSER-SCOPED feature declared only in the .spyproj —
    /// no CLI flag — must reach compilation through the ForProject path. Measured latent at HEAD
    /// (every current consumer re-merges config.Features downstream), so this pins the contract
    /// against a future direct reader of the factory's Features. The ungated arm is the
    /// non-vacuity control: the same source without the project declaration must draw SPY0331.
    /// </summary>
    [Fact]
    public void ForProject_SpyprojDeclaredParserScopedFeature_CompilesGatedCodeEndToEnd()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_forproject_feature_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var mainPath = Path.Combine(tempDir, "main.spy");
            File.WriteAllText(mainPath, "def main() -> None:\n    defer print(\"deferred\")\n    print(\"body\")\n");

            ProjectConfig Config(params string[] features) => new()
            {
                ProjectFilePath = Path.Combine(tempDir, "test.spyproj"),
                ProjectDirectory = tempDir,
                RootNamespace = "FeatureE2E",
                SourceFiles = new List<string> { mainPath },
                Configuration = "Debug",
                Features = features.ToList(),
            };

            // Ungated control: no project declaration, no CLI flag → the gated syntax must refuse.
            var ungated = Config();
            var ungatedResult = new Compiler(
                    CompilerOptionsFactory.ForProject(ungated), Sharpy.Compiler.Logging.NullLogger.Instance)
                .CompileProject(ungated);
            ungatedResult.Success.Should().BeFalse(
                "without the feature anywhere, `defer` must draw SPY0331 — otherwise the gated "
                + "arm below proves nothing");
            ungatedResult.Diagnostics.GetAll().Should().Contain(d => d.Code == "SPY0331");

            // The #1554 cell: the feature declared ONLY in the project config, no CLI features arg.
            var gated = Config("defer");
            var gatedResult = new Compiler(
                    CompilerOptionsFactory.ForProject(gated), Sharpy.Compiler.Logging.NullLogger.Instance)
                .CompileProject(gated);
            gatedResult.Success.Should().BeTrue(
                ".spyproj <Features>defer</Features> must reach compilation through ForProject "
                + "with no CLI flag (#1554). Diagnostics:\n"
                + string.Join("\n", gatedResult.Diagnostics.GetAll()));
        }
        finally
        {
            try
            { Directory.Delete(tempDir, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
