using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Tests.Conformance;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// A3 of plan-775c50 (P77, rule N3; D4's first predicate): no user expression is left in a static initializer or a
/// static constructor of the emitted C#, a C# <c>const</c> aside. A Guarded value is initialized by its host's sweep
/// (<c>N__init</c>, run by <c>Sharpy.StaticInit</c>); a value initialized by C#'s type initializer instead passes every
/// cell that does not read it before it is ready (the plan's pre-mortem: "how the fix could be inert").
/// <para>The census compiles every executing fixture and every cell of the stage-0 pool
/// (<see cref="InitializerDependencyDifferentialTests.GeneratedCells"/>) through the production pipeline, captures the
/// emitter's units before anything reparses them, and lists every static field or property with an initializer and
/// every static constructor. What is allowed carries no user expression by construction
/// (<see cref="IsScaffolding"/>): a <c>const</c>; a private parameterless construction the emitter synthesizes (a
/// memoization cache); a Guarded value's storage written as its CLR default (<c>N__field = default!</c>, F15); and the
/// one static constructor of a host's <c>__Swept</c> holder.</para>
/// </summary>
[Trait("Category", "GapDiscovery")]
public class StaticInitializerCensusTests
{
    private readonly ITestOutputHelper _output;

    public StaticInitializerCensusTests(ITestOutputHelper output) => _output = output;

    /// <summary>A static member the census flags: its owner and declaration, as written.</summary>
    internal static IEnumerable<string> Violations(CompilationUnitSyntax unit)
    {
        foreach (var field in unit.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            var isStatic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
                || field.Parent is InterfaceDeclarationSyntax && field.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
            if (field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)) || !isStatic)
                continue;
            foreach (var variable in field.Declaration.Variables.Where(v => v.Initializer != null))
            {
                if (!IsScaffolding(field, variable.Initializer!.Value))
                    yield return $"{Owner(field)}: static field {variable.Identifier.ValueText} = {Flat(variable.Initializer.Value)}";
            }
        }
        foreach (var property in unit.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (property.Initializer != null && property.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
                yield return $"{Owner(property)}: static property {property.Identifier.ValueText} = {Flat(property.Initializer.Value)}";
        }
        foreach (var ctor in unit.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            if (ctor.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)) && !IsSweptHolder(ctor))
                yield return $"{Owner(ctor)}: static constructor";
        }
    }

    /// <summary>
    /// A static initializer with no user expression in it. The emitter's own private parameterless construction (a
    /// memoization cache); and a string enum's members, outside rule N3 (the lead's ruling on T2: an enum member carries
    /// no storage fact), in the two shapes the enum arm writes: a <c>static readonly</c> member constructed from
    /// literals as its own type (<c>new Tone("A", "a")</c>), and its <c>Values</c> list of those members.
    /// </summary>
    internal static bool IsScaffolding(FieldDeclarationSyntax field, ExpressionSyntax initializer)
    {
        var isPrivate = field.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword));
        var owner = (field.Parent as TypeDeclarationSyntax)?.Identifier.ValueText;
        var isReadOnly = field.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));
        return initializer switch
        {
            // A Guarded value's storage holds its CLR default, written out for a nullable-enabled
            // consumer (F15): `default!` is no user expression.
            PostfixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax defaultLiteral } suppressed
                when suppressed.IsKind(SyntaxKind.SuppressNullableWarningExpression)
                && defaultLiteral.IsKind(SyntaxKind.DefaultLiteralExpression)
                => field.Declaration.Variables.Single().Identifier.ValueText.EndsWith(RoslynEmitter.GuardedFieldSuffix, StringComparison.Ordinal),
            ObjectCreationExpressionSyntax { Initializer: null, ArgumentList: null or { Arguments.Count: 0 } } => isPrivate,
            ImplicitObjectCreationExpressionSyntax { Initializer: null, ArgumentList.Arguments.Count: 0 } => isPrivate,
            ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax type, Initializer: null } member
                when isReadOnly && type.Identifier.ValueText == owner
                => member.ArgumentList!.Arguments.All(a => a.Expression is LiteralExpressionSyntax),
            ObjectCreationExpressionSyntax { ArgumentList.Arguments: [{ Expression: ArrayCreationExpressionSyntax { Initializer: { } members } }] } values
                when isReadOnly && field.Declaration.Variables.Single().Identifier.ValueText == "Values"
                => values.Type.ToString() == $"global::Sharpy.List<{owner}>"
                   && members.Expressions.All(e => e is IdentifierNameSyntax),
            _ => false,
        };
    }

    /// <summary>The static constructor of a host's <c>__Swept</c> holder, which calls <c>StaticInit.TryLoad</c> only.</summary>
    private static bool IsSweptHolder(ConstructorDeclarationSyntax ctor) =>
        ctor.Parent is ClassDeclarationSyntax { Identifier.ValueText: RoslynEmitter.SweptClassName }
        && ctor.Body is { Statements.Count: 1 } body
        && body.Statements[0].ToString().Contains("global::Sharpy.StaticInit.TryLoad(ref __host, __Sweep)", StringComparison.Ordinal);

    private static string Owner(SyntaxNode node) =>
        node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "?";

    private static string Flat(SyntaxNode node)
    {
        var text = node.ToString().Replace("\n", " ", StringComparison.Ordinal);
        return text.Length > 80 ? text[..80] + "..." : text;
    }

    [Fact]
    public void PositiveControl_AnInitializedStaticAndAStaticConstructor_AreFlagged()
    {
        var unit = SyntaxFactory.ParseCompilationUnit(
            "class A { public static int T = 7; private static readonly object O = new object(); public const int K = 3; static A() { } }"
            + " class E { public static readonly E X = new E(\"X\", \"x\"); public static readonly E Y = new E(Make(), \"y\"); public static readonly global::Sharpy.List<E> Values = new global::Sharpy.List<E>(new E[] { X }); }"
            + " class B { private static class __Swept { internal static readonly bool Done; static __Swept() { Done = global::Sharpy.StaticInit.TryLoad(ref __host, __Sweep); } } }");
        var found = Violations(unit).ToList();
        Assert.Equal(new[] { "A: static field T = 7", "E: static field Y = new E(Make(), \"y\")", "A: static constructor" }, found);
    }

    [Fact]
    public void FixtureCorpusAndPool_LeaveNoUserExpressionInAStaticInitializer()
    {
        var fixtures = FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.CompilerTests)
            .OrderBy(f => f.TestName, StringComparer.Ordinal)
            .ToList();
        Assert.True(fixtures.Count > 1000, $"only {fixtures.Count} fixtures discovered: the census reads nothing");

        var violations = new List<string>();
        int scannedUnits = 0, scannedPrograms = 0;
        foreach (var fixture in fixtures)
        {
            IReadOnlyList<CompilationUnitSyntax> units;
            try
            {
                units = CompileFixtureCapturing(fixture);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"skipped {fixture.TestName}: compile threw {ex.GetType().Name}");
                continue;
            }
            if (units.Count == 0)
                continue;
            scannedPrograms++;
            scannedUnits += units.Count;
            violations.AddRange(units.SelectMany(Violations).Select(v => $"{fixture.TestName}: {v}"));
        }

        var pool = InitializerDependencyDifferentialTests.GeneratedCells();
        Assert.True(pool.Count > 1000, $"the stage-0 pool holds {pool.Count} cells: the census reads nothing");
        foreach (var cell in pool)
        {
            var units = CompileSource(cell.Source, $"/tmp/census/{Sanitize(cell.Id)}.spy", FeatureFlags.None);
            if (units.Count == 0)
                continue;
            scannedPrograms++;
            scannedUnits += units.Count;
            violations.AddRange(units.SelectMany(Violations).Select(v => $"{cell.Id}: {v}"));
        }

        _output.WriteLine($"Census: {scannedUnits} emitter unit(s) across {scannedPrograms} program(s) ({fixtures.Count} fixtures, {pool.Count} pool cells).");
        Assert.True(scannedPrograms > 2000, $"only {scannedPrograms} programs reached code generation: the census reads nothing");
        Assert.True(violations.Count == 0,
            $"{violations.Count} static initializer(s) or constructor(s) left in emitted C# (plan-775c50 A3):\n"
            + string.Join("\n", violations.Distinct().Take(60)));
    }

    private static string Sanitize(string id) => new(id.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    private static IReadOnlyList<CompilationUnitSyntax> CompileSource(string source, string path, FeatureFlags features)
    {
        var factory = new CapturingEmitterFactory();
        var options = new CompilerOptions
        {
            References = new[] { SharpyCoreReference.Location },
            OutputType = "exe",
            WarningsAsErrors = false,
            Features = features,
        };
        new Compiler(options, NullLogger.Instance, factory).Compile(source, path);
        return factory.Captured;
    }

    private static IReadOnlyList<CompilationUnitSyntax> CompileFixtureCapturing(TestFixtureInfo fixture)
    {
        var features = fixture.Features.Count == 0 ? FeatureFlags.None : FeatureFlags.None.Enable(fixture.Features);
        if (!fixture.IsMultiFile)
            return CompileSource(File.ReadAllText(fixture.SpyFilePath), fixture.SpyFilePath, features);

        var factory = new CapturingEmitterFactory();
        var projectDir = fixture.SpyFilePath;
        var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories)
            .Where(f => !CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(projectDir, f)))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        var entry = File.Exists(Path.Combine(projectDir, "main.spy"))
            ? "main.spy"
            : File.Exists(Path.Combine(projectDir, Path.GetFileName(projectDir) + ".spy"))
                ? Path.GetFileName(projectDir) + ".spy"
                : Path.GetFileName(sourceFiles[0]);
        var config = new ProjectConfig
        {
            ProjectDirectory = projectDir,
            ProjectFilePath = Path.Combine(projectDir, "test.spyproj"),
            RootNamespace = "Sharpy.Test",
            OutputType = "exe",
            EntryPoint = entry,
            SourceFiles = sourceFiles,
            Configuration = "Debug",
            TargetFramework = "net10.0",
        };
        var moduleRegistry = new ModuleRegistry(NullLogger.Instance);
        moduleRegistry.LoadReference(SharpyCoreReference.Location);
        new ProjectCompiler(NullLogger.Instance, moduleRegistry, ProjectCompilerOptions.Default with { Features = features }, factory)
            .Compile(config, CancellationToken.None, emitAssembly: false);
        return factory.Captured;
    }

    /// <summary>Records each emitter-built unit before the compiler reparses it.</summary>
    private sealed class CapturingEmitterFactory : ICodeEmitterFactory
    {
        private readonly RoslynEmitterFactory _inner = new();

        public List<CompilationUnitSyntax> Captured { get; } = new();

        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default) =>
            new CapturingEmitter(_inner.Create(context, cancellationToken), Captured);

        private sealed class CapturingEmitter(ICodeEmitter inner, List<CompilationUnitSyntax> sink) : ICodeEmitter
        {
            public CompilationUnitSyntax GenerateCompilationUnit(Module module)
            {
                var unit = inner.GenerateCompilationUnit(module);
                sink.Add(unit);
                return unit;
            }
        }
    }
}
