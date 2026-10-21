using CsCheck;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class ValidatorOrderingPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ValidatorOrderingPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// The validation pipeline assigns each <see cref="ISemanticValidator"/> a fixed
    /// <see cref="ISemanticValidator.Order"/> and <see cref="ValidationPipeline.AddValidator"/>
    /// re-sorts by that order on every insertion. This property asserts that the diagnostics
    /// produced are independent of the order in which validators are REGISTERED: no matter how
    /// callers wire the pipeline together, the sort restores the canonical execution sequence
    /// and the resulting diagnostics are identical.
    ///
    /// This is the order-independence guarantee the compiler actually relies on. (Validators
    /// are NOT mutually independent at the execution level — several share mutable
    /// <c>SemanticInfo</c> state and would produce different results if executed out of their
    /// canonical order — so the pipeline deliberately pins execution order via <c>Order</c>.
    /// This test guards the contract that registration order can never perturb that.)
    ///
    /// Methodology: generate well-typed programs (filtered so each analyzes cleanly and thus
    /// genuinely exercises the validators), type-check once via <see cref="Compiler.Analyze"/>
    /// to obtain a faithful semantic state, then run the FULL default validator set through the
    /// real <see cref="ValidationPipeline.Validate"/> twice — once built in canonical
    /// registration order and once built with a randomly shuffled registration order — against
    /// independent, fresh contexts. The shuffled pipeline's execution order must match the
    /// canonical one, and the distinct diagnostic sets must be identical.
    /// </summary>
    [Fact]
    public void ValidationDiagnostics_AreRegistrationOrderIndependent()
    {
        int total = 0;
        int compared = 0;
        // An admitted sample whose re-analysis does not analyse clean is a RECORDED failure, never a
        // skipped comparison (#2288): before, ValidateWith returned null and the caller returned, so a
        // re-analysis that failed on every sample left the fact green with "0/30 programs compared".
        var failures = new System.Collections.Concurrent.ConcurrentQueue<string>();

        // The canonical (Order-sorted) validator NAME sequence, used to assert that
        // AddValidator restores execution order regardless of registration order. Validator
        // INSTANCES must never be reused across compilations (they hold per-compilation
        // state), so we always materialize fresh instances via CreateDefault().
        var canonicalNames = ValidationPipelineFactory.CreateDefault()
            .Validators.Select(v => v.Name).ToList();

        // Generate well-typed programs that analyze cleanly (they include a main()), so every
        // sampled program actually exercises the validators.
        var sink = new RejectionSink("ValidatorOrdering.ValidationDiagnostics_AreRegistrationOrderIndependent");
        var gen = Gen.Select(
            SemanticFilter.WellTypedProgram(
                Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                    GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2, withStatements: true)), sink),
            Gen.Int,
            (module, shuffleSeed) => (module, shuffleSeed));

        gen.Sample(sample =>
        {
            var (module, shuffleSeed) = sample;
            Interlocked.Increment(ref total);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            // Reference: pipeline registered in canonical order, built from FRESH validator
            // instances. Each ValidateWith call re-analyzes from source, so it starts from a
            // fresh, deterministic semantic state (validators mutate SemanticInfo, so the
            // state must not be shared between runs). Returns null if the program does not
            // analyze cleanly.
            var reference = ValidateWith(source, ValidationPipelineFactory.CreateDefault().Validators.ToList(),
                out var referenceFailure);
            if (reference is null)
            {
                failures.Enqueue($"reference run: {referenceFailure}\n{source}");
                return;
            }

            // The shuffle RNG is seeded from CsCheck, so the seed CsCheck prints reproduces the
            // exact registration orders this case tried. It used to be source.GetHashCode(),
            // which .NET randomizes per process: the printed seed picked the same program and
            // then a different shuffle, so a failure did not reproduce (#1439 — same defect as
            // the whitespace helper, three sites away from the one the issue named).
            var rng = new Random(shuffleSeed);

            for (int trial = 0; trial < 3; trial++)
            {
                // Fresh validator instances every trial — never reuse across compilations.
                var shuffled = ValidationPipelineFactory.CreateDefault().Validators.ToList();
                FisherYatesShuffle(shuffled, rng);

                // Build the pipeline by registering in shuffled order; AddValidator must
                // re-sort it back to the canonical execution order.
                var pipeline = ValidationPipelineFactory.CreateMinimal();
                foreach (var v in shuffled)
                    pipeline.AddValidator(v);

                var executionNames = pipeline.Validators.Select(v => v.Name).ToList();
                if (!executionNames.SequenceEqual(canonicalNames))
                {
                    throw new ValidatorOrderException(
                        "AddValidator did not restore canonical execution order." +
                        Environment.NewLine + "Expected: " + string.Join(" -> ", canonicalNames) +
                        Environment.NewLine + "Actual:   " + string.Join(" -> ", executionNames));
                }

                var actual = ValidateWith(source, pipeline, out var actualFailure);
                if (actual is null)
                {
                    failures.Enqueue($"shuffled trial {trial}: {actualFailure}\n{source}");
                    return;
                }

                if (!reference.SequenceEqual(actual))
                {
                    var nl = Environment.NewLine;
                    var indent = nl + "  ";
                    var message =
                        "Validation diagnostics depend on validator registration order." + nl +
                        "Canonical:" + indent + string.Join(indent, reference) + nl +
                        "Shuffled: " + indent + string.Join(indent, actual) + nl +
                        "Registration order: " + string.Join(" -> ", shuffled.Select(v => v.Name)) + nl +
                        "Source:" + nl + source;
                    throw new ValidatorOrderException(message);
                }
            }

            Interlocked.Increment(ref compared);
        }, print: s => $"shuffle seed {s.shuffleSeed}{Environment.NewLine}"
                       + Sharpy.Compiler.Pretty.Unparser.Unparse(s.module), iter: 30);
        _output.WriteLine(sink.Summary);
        _output.WriteLine("Validator registration ordering: " + compared + "/" + total + " programs compared");
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        Assert.True(failures.IsEmpty,
            $"{failures.Count} admitted program(s) did not re-analyse clean, so their validator order was never "
            + $"compared; the first:\n{failures.FirstOrDefault()}");
        Assert.True(total > 0, "No program was admitted — the lane compared nothing");
        Assert.True(compared == total,
            $"{compared}/{total} admitted programs compared — every admitted program must be compared");
    }

    private static void FisherYatesShuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static List<string>? ValidateWith(
        string source, IReadOnlyList<ISemanticValidator> validators, out string? failure)
    {
        var pipeline = ValidationPipelineFactory.CreateMinimal();
        foreach (var v in validators)
            pipeline.AddValidator(v);
        return ValidateWith(source, pipeline, out failure);
    }

    /// <summary>
    /// Re-analyzes <paramref name="source"/> via the real compiler to obtain a fresh,
    /// deterministic semantic state (a fresh state is required because validators mutate
    /// <c>SemanticInfo</c>), then runs the given pipeline against it using a fresh
    /// <see cref="SemanticContext"/> with an unbounded error budget so every validator runs.
    /// Returns the sorted set of distinct resulting diagnostics (keyed by code, position, and
    /// message), or <c>null</c> with <paramref name="failure"/> saying why when the program does not
    /// analyze cleanly — the caller records that as a failure, never a skip (a throw propagates and
    /// fails the sample). The distinct set is
    /// used because validators legitimately deduplicate against already-reported diagnostics,
    /// so the count of identical duplicates is not significant.
    /// </summary>
    private static List<string>? ValidateWith(string source, ValidationPipeline pipeline, out string? failure)
    {
        var logger = NullLogger.Instance;

        var analysis = new Sharpy.Compiler.Compiler().Analyze(source, "validator_order_test.spy");
        var errors = analysis.Diagnostics.GetErrors().Select(d => d.Code ?? "(no code)").Distinct().ToList();
        failure = errors.Count > 0 ? $"re-analysis drew [{string.Join(", ", errors)}]"
            : !analysis.Success ? "re-analysis failed without an error code"
            : analysis.Module is null ? "re-analysis produced no module"
            : analysis.SymbolTable is null ? "re-analysis produced no symbol table"
            : analysis.SemanticInfo is null ? "re-analysis produced no semantic info"
            : null;
        if (failure is not null)
            return null;

        var symbolTable = analysis.SymbolTable!;
        var semanticInfo = analysis.SemanticInfo!;
        var module = analysis.Module!;

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver, logger)
        {
            CurrentFilePath = "validator_order_test.spy",
            SemanticBinding = analysis.SemanticBinding ?? new SemanticBinding(),
            MaxErrors = int.MaxValue,
            ContinueAfterErrors = true,
            IsEntryPoint = true,
        };

        pipeline.Validate(module, context);

        return context.Diagnostics
            .GetAll()
            .Select(d => d.Code + "@" + d.Line + ":" + d.Column + ":" + d.Message)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
    }

    private sealed class ValidatorOrderException : Exception
    {
        public ValidatorOrderException(string message) : base(message) { }
    }
}
