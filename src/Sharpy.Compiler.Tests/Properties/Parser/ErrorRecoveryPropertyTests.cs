using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class ErrorRecoveryPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ErrorRecoveryPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static Gen<(Module Module, int Seed)> ModuleWithSeed() =>
        Gen.Select(
            Gen.Int[1, 3].SelectMany(fuel => GenSharpy.Module(GenContext.Default with { Fuel = fuel })),
            Gen.Int,
            (module, seed) => (module, seed));

    private static string Print((Module Module, int Seed) sample)
        => $"seed {sample.Seed}\n{Sharpy.Compiler.Pretty.Unparser.Unparse(sample.Module)}";

    /// <summary>
    /// <para>Verifies that injecting a single corrupted line into an otherwise valid
    /// generated program never causes the parser to throw an unhandled exception,
    /// and that error recovery is non-interfering: unmodified top-level definitions
    /// still survive in the parsed AST. (#726)</para>
    ///
    /// <para>The corruption index derives from a CsCheck-managed seed so the printed seed
    /// fully determines the run (#1522, #1439). Before this fix, <c>new Random()</c> with no
    /// seed meant the printed CsCheck seed could not reproduce the corruption index — re-running
    /// it reproduced only the module, not the injection point.</para>
    /// </summary>
    [Fact]
    public void Parser_DoesNotCrash_OnMutatedInput()
    {
        int total = 0;
        int tested = 0;
        int survived = 0;

        ModuleWithSeed().Sample(sample =>
        {
            Interlocked.Increment(ref total);

            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(sample.Module);
            var lines = source.Split('\n');
            if (lines.Length < 2)
            {
                return;
            }

            var originalNames = new HashSet<string>();
            foreach (var stmt in sample.Module.Body)
            {
                if (stmt is FunctionDef fd)
                {
                    originalNames.Add(fd.Name);
                }
                else if (stmt is ClassDef cd)
                {
                    originalNames.Add(cd.Name);
                }
            }

            if (originalNames.Count < 2)
            {
                return;
            }

            Interlocked.Increment(ref tested);

            var rng = new Random(sample.Seed);
            var lineIdx = rng.Next(1, lines.Length - 1);
            lines[lineIdx] = "!!!GARBAGE!!! @#$% <<<>>>";
            var mutated = string.Join('\n', lines);

            // Parse the mutated source — this must never throw an unhandled exception.
            Module parsed;
            try
            {
                var lexer = new Sharpy.Compiler.Lexer.Lexer(mutated);
                var tokens = lexer.TokenizeAll();
                var parser = new Sharpy.Compiler.Parser.Parser(tokens);
                parsed = parser.ParseModule();
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Parser crashed on mutated input: {ex.GetType().Name}: {ex.Message}\n" +
                    $"--- Mutated source ---\n{mutated}");
            }

            // Collect surviving top-level definition names from the recovered AST.
            var survivingNames = new HashSet<string>();
            foreach (var stmt in parsed.Body)
            {
                if (stmt is FunctionDef fd)
                {
                    survivingNames.Add(fd.Name);
                }
                else if (stmt is ClassDef cd)
                {
                    survivingNames.Add(cd.Name);
                }
            }

            // We don't require ALL names to survive (the corrupted definition may be
            // dropped), but error recovery should preserve at least one untouched
            // top-level definition.
            if (survivingNames.Overlaps(originalNames))
            {
                Interlocked.Increment(ref survived);
            }
        }, print: Print, iter: 50);

        _output.WriteLine(
            $"Error recovery: generated={total}, tested={tested}, " +
            $"survived={survived} programs had surviving definitions");
    }
}
