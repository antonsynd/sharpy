using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// The persistent form of P82 tranche 1's temporary agreement sweep (#2443, b67ca2ded). The sweep
/// compared the lowering pass's derived Optional store wraps against the checker's
/// <c>_optionalStoreWraps</c> table; that table is deleted, so the comparison cannot be rerun. What
/// can be checked forever is the property the derivation relies on.
///
/// <para><b>The property.</b> <c>LoweringPass</c> decides a payload store into a declared Optional
/// slot <c>S</c> by reading the recorded type of the store's TARGET (a plain store and an unpacking
/// element: <c>type(t)</c> is not Optional/Unknown; a walrus: <c>type(walrus)</c> is not
/// Optional/Unknown). The emitted store is right only if that agrees with the recorded type of the
/// stored VALUE, an independent checker fact. Over every front-end-clean fixture, for every store into
/// an identifier whose declared slot is an <c>OptionalType</c>:
/// <list type="bullet">
/// <item>plain <c>t = v</c>, a tuple-literal unpacking element, and a walrus <c>(t := v)</c>: the
/// lowering wraps (<see cref="IrOptionalStoreWrap"/> / <see cref="IrOptionalWrappedElement"/> /
/// <see cref="IrOptionalWalrusStore"/>, <c>WrapAs == S</c>) <b>iff</b> <c>type(v)</c> is neither
/// <c>OptionalType</c> nor <c>None</c> (<c>VoidType</c>). A wrap over an Optional value would
/// double-wrap; a missing wrap over a payload value would leave a bare <c>T</c> in an
/// <c>Optional&lt;T&gt;</c> slot (CS0029 or the #1720 implicit operator).</item>
/// <item>an augmented store <c>t op= v</c> that is not a collection mutation: the lowering wraps
/// unconditionally, which is right only because the checker refuses an un-narrowed Optional target
/// (SPY0222), so <c>type(t)</c> is never <c>OptionalType</c> there.</item>
/// </list>
/// <c>??=</c> (D1 (d)) is not checked: its derivation already reads <c>type(v)</c> directly, so the
/// property would restate the code.</para>
///
/// <para>Categorised <c>GapDiscovery</c>: it compiles every fixture whose source can declare an
/// Optional slot, a standing sweep rather than an edit-loop test (the #1720 executing-corpus arm is
/// the precedent).</para>
/// </summary>
[Trait("Category", "GapDiscovery")]
public class OptionalStoreWrapAgreementTests
{
    private readonly ITestOutputHelper _output;

    public OptionalStoreWrapAgreementTests(ITestOutputHelper output) => _output = output;

    /// <summary>A fixture can declare an Optional slot only by spelling one of these.</summary>
    private static readonly Regex MayDeclareOptional = new(@"\?|None|Optional", RegexOptions.CultureInvariant);

    [Fact]
    public void FixtureCorpus_DerivedWrapsAgreeWithTheStoredValueType()
    {
        var fixtures = FixtureDiscoveryHelper.DiscoverFixturesFrom(FixtureRoots.CompilerTests)
            .Where(f => !f.IsSkipped)
            .OrderBy(f => f.TestName, StringComparer.Ordinal)
            .ToList();
        Assert.True(fixtures.Count > 0, "No fixtures discovered — corpus is empty, guard is vacuous");

        var tally = new Tally();
        var disagreements = new List<string>();
        int compiled = 0, units = 0, emptyIrContexts = 0;

        foreach (var fixture in fixtures)
        {
            if (!SourcesOf(fixture).Any(s => MayDeclareOptional.IsMatch(File.ReadAllText(s))))
                continue;

            List<(CodeGenContext Context, Module Module)> captured;
            try
            {
                var (clean, contexts) = CompileCapturing(fixture);
                if (!clean)
                    continue;
                captured = contexts;
            }
            catch (Exception ex)
            {
                // A fixture whose compile throws is FileBasedIntegrationTests' failure to report.
                _output.WriteLine($"skipped {fixture.TestName}: compile threw {ex.GetType().Name}");
                continue;
            }

            compiled++;
            foreach (var (context, module) in captured)
            {
                units++;
                if (ReferenceEquals(context.Ir, IrCompilation.Empty))
                {
                    emptyIrContexts++;
                    continue;
                }

                CheckModule(fixture.TestName, module, context, tally, disagreements);
            }
        }

        _output.WriteLine($"compiled {compiled} fixture(s), {units} emitter unit(s); {tally}");

        // The emitter's required IR reads (RequiredIrNodeFor) skip only over IrCompilation.Empty; no
        // pipeline context may be one, or that skip path is what ran.
        Assert.Equal(0, emptyIrContexts);

        // Non-vacuity: both sides of the iff were exercised, for every derivation checked.
        Assert.True(tally.PlainWrap > 0 && tally.PlainNoWrap > 0,
            $"plain stores must exercise both a wrap and a non-wrap under an Optional slot: {tally}");
        Assert.True(tally.ElementWrap > 0, $"no unpacking element wrap was checked: {tally}");
        Assert.True(tally.WalrusWrap > 0, $"no walrus wrap was checked: {tally}");
        Assert.True(tally.Augmented > 0, $"no augmented store under an Optional slot was checked: {tally}");

        Assert.True(disagreements.Count == 0,
            $"{disagreements.Count} Optional store(s) where the lowering's wrap decision (read from the "
            + "target's recorded type) disagrees with the stored value's recorded type (#2443):\n"
            + string.Join("\n", disagreements.Take(50)));
    }

    private static void CheckModule(
        string fixture, Module module, CodeGenContext context, Tally tally, List<string> disagreements)
    {
        var info = context.SemanticInfo!;
        var index = context.Ir.Index;

        OptionalType? SlotOf(Symbol? symbol)
        {
            if (symbol is not VariableSymbol variable)
                return null;
            var root = info.GetRootBinding(variable);
            var bound = context.SemanticBinding.GetVariableType(root);
            return (bound != SemanticType.Unknown ? bound : root.Type) as OptionalType;
        }

        static bool IsOptionalLike(SemanticType? type) => type is OptionalType or VoidType;

        IrNode? IrOf(Node ast) => index.TryGetValue(ast, out var ir) ? ir : null;

        void Check(string cell, Node site, OptionalType slot, OptionalType? wrapAs, Expression value)
        {
            var valueType = info.GetExpressionType(value);
            var expectWrap = !IsOptionalLike(valueType);

            if ((wrapAs != null) != expectWrap || (wrapAs != null && wrapAs != slot))
            {
                disagreements.Add(
                    $"{fixture} {cell} @ {site.LineStart}:{site.ColumnStart}: slot={slot.GetDisplayName()}, "
                    + $"value type={valueType?.GetDisplayName()}, lowered wrap={wrapAs?.GetDisplayName() ?? "none"}");
            }

            tally.Count(cell, wrapAs != null);
        }

        foreach (var node in Walk(module))
        {
            switch (node)
            {
                case Assignment { Operator: AssignmentOperator.Assign, Target: Identifier target } plain
                    when SlotOf(info.GetIdentifierSymbol(target)) is { } slot:
                    Check(Tally.Plain, plain, slot, (IrOf(plain) as IrOptionalStoreWrap)?.WrapAs, plain.Value);
                    break;

                case Assignment { Operator: AssignmentOperator.Assign, Target: TupleLiteral targets } unpack:
                    CheckUnpacking(targets.Elements, unpack.Value);
                    break;

                case Assignment { Target: Identifier augTarget } augmented
                    when augmented.Operator is not (AssignmentOperator.Assign or AssignmentOperator.NullCoalesceAssign)
                         && SlotOf(info.GetIdentifierSymbol(augTarget)) is { } augSlot
                         && index.TryGetValue(augmented, out var augIr)
                         && augIr is not IrCollectionMutation:
                    tally.Augmented++;
                    if (info.GetExpressionType(augTarget) is OptionalType || augIr is not IrOptionalStoreWrap { WrapAs: var augWrap } || augWrap != augSlot)
                    {
                        disagreements.Add(
                            $"{fixture} augmented @ {augmented.LineStart}:{augmented.ColumnStart}: slot={augSlot.GetDisplayName()}, "
                            + $"target type={info.GetExpressionType(augTarget)?.GetDisplayName()}, lowered={augIr.GetType().Name}");
                    }
                    break;

                case WalrusExpression walrus when SlotOf(info.GetWalrusSymbol(walrus)) is { } walrusSlot:
                    // An unpacking element's wrap holds the walrus's own node as its single child.
                    var walrusIr = IrOf(walrus) is IrOptionalWrappedElement { Children: [IrNode inner] } ? inner : IrOf(walrus);
                    Check(Tally.Walrus, walrus, walrusSlot, (walrusIr as IrOptionalWalrusStore)?.WrapAs, walrus.Value);
                    break;
            }
        }

        // Mirrors the derivation's pairing (D1 (b)): spread-free tuple literal, equal arity, nested
        // tuple targets recurse.
        void CheckUnpacking(System.Collections.Immutable.ImmutableArray<Expression> targets, Expression value)
        {
            if (AstHelper.UnwrapParenthesized(value) is not TupleLiteral literal
                || literal.Elements.Any(e => e is SpreadElement)
                || targets.Any(t => t is StarExpression)
                || literal.Elements.Length != targets.Length)
            {
                return;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                var element = literal.Elements[i];
                switch (targets[i])
                {
                    case Identifier target when SlotOf(info.GetIdentifierSymbol(target)) is { } slot:
                        Check(Tally.Element, element, slot, (IrOf(element) as IrOptionalWrappedElement)?.WrapAs, element);
                        break;
                    case TupleLiteral nested:
                        CheckUnpacking(nested.Elements, element);
                        break;
                }
            }
        }
    }

    private static IEnumerable<Node> Walk(Node root)
    {
        foreach (var child in root.GetChildNodes())
        {
            yield return child;
            foreach (var descendant in Walk(child))
                yield return descendant;
        }
    }

    private sealed class Tally
    {
        public const string Plain = "plain";
        public const string Element = "element";
        public const string Walrus = "walrus";

        public int PlainWrap, PlainNoWrap, ElementWrap, ElementNoWrap, WalrusWrap, WalrusNoWrap, Augmented;

        public void Count(string cell, bool wrapped)
        {
            switch (cell)
            {
                case Plain when wrapped:
                    PlainWrap++;
                    break;
                case Plain:
                    PlainNoWrap++;
                    break;
                case Element when wrapped:
                    ElementWrap++;
                    break;
                case Element:
                    ElementNoWrap++;
                    break;
                case Walrus when wrapped:
                    WalrusWrap++;
                    break;
                case Walrus:
                    WalrusNoWrap++;
                    break;
            }
        }

        public override string ToString() =>
            $"plain wrap/no-wrap {PlainWrap}/{PlainNoWrap}, element {ElementWrap}/{ElementNoWrap}, "
            + $"walrus {WalrusWrap}/{WalrusNoWrap}, augmented {Augmented}";
    }

    private static IEnumerable<string> SourcesOf(TestFixtureInfo fixture) => fixture.IsMultiFile
        ? Directory.GetFiles(fixture.SpyFilePath, "*.spy", SearchOption.AllDirectories)
            .Where(f => !CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(fixture.SpyFilePath, f)))
        : new[] { fixture.SpyFilePath };

    /// <summary>
    /// Compiles one fixture through the production pipeline (the shape of
    /// <c>EmittedOptionalConstructionConformanceTests.CompileFixtureCapturing</c>), capturing each
    /// emitter's <see cref="CodeGenContext"/> and module. Clean = no Sharpy front-end error.
    /// </summary>
    private static (bool Clean, List<(CodeGenContext, Module)> Captured) CompileCapturing(TestFixtureInfo fixture)
    {
        var factory = new CapturingEmitterFactory();
        var features = fixture.Features.Count == 0
            ? FeatureFlags.None
            : FeatureFlags.None.Enable(fixture.Features);

        DiagnosticBag diagnostics;
        if (fixture.IsMultiFile)
        {
            var projectDir = fixture.SpyFilePath;
            var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories)
                .Where(f => !CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(projectDir, f)))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            var entry = File.Exists(Path.Combine(projectDir, "main.spy")) ? "main.spy"
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
                TargetFramework = "net10.0"
            };

            var moduleRegistry = new ModuleRegistry(NullLogger.Instance);
            moduleRegistry.LoadReference(SharpyCoreReference.Location);
            diagnostics = new ProjectCompiler(
                    NullLogger.Instance, moduleRegistry,
                    ProjectCompilerOptions.Default with { Features = features }, factory)
                .Compile(config, CancellationToken.None, emitAssembly: false).Diagnostics;
        }
        else
        {
            var options = new CompilerOptions
            {
                References = new[] { SharpyCoreReference.Location },
                OutputType = "exe",
                WarningsAsErrors = false,
                Features = features
            };
            diagnostics = new Compiler(options, NullLogger.Instance, factory)
                .Compile(File.ReadAllText(fixture.SpyFilePath), fixture.SpyFilePath).Diagnostics;
        }

        var clean = !diagnostics.GetErrors().Any(d =>
            d.Code is not (DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                or DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError
                or DiagnosticCodes.CodeGen.EmittedTreePrecedenceInversion));
        return (clean, factory.Captured);
    }

    private sealed class CapturingEmitterFactory : ICodeEmitterFactory
    {
        private readonly RoslynEmitterFactory _inner = new();

        public List<(CodeGenContext, Module)> Captured { get; } = new();

        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default) =>
            new CapturingEmitter(_inner.Create(context, cancellationToken), context, Captured);

        private sealed class CapturingEmitter : ICodeEmitter
        {
            private readonly ICodeEmitter _inner;
            private readonly CodeGenContext _context;
            private readonly List<(CodeGenContext, Module)> _sink;

            public CapturingEmitter(ICodeEmitter inner, CodeGenContext context, List<(CodeGenContext, Module)> sink)
            {
                _inner = inner;
                _context = context;
                _sink = sink;
            }

            public CompilationUnitSyntax GenerateCompilationUnit(Module module)
            {
                _sink.Add((_context, module));
                return _inner.GenerateCompilationUnit(module);
            }
        }
    }
}
