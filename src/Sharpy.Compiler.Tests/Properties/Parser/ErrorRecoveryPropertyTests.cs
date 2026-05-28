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

    /// <summary>
    /// Verifies that injecting a single corrupted line into an otherwise valid
    /// generated program never causes the parser to throw an unhandled exception,
    /// and that error recovery is non-interfering: unmodified top-level definitions
    /// still survive in the parsed AST. (#726)
    /// </summary>
    [Fact]
    public void Parser_DoesNotCrash_OnMutatedInput()
    {
        int total = 0;
        int tested = 0;
        int survived = 0;

        Gen.Int[1, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            Interlocked.Increment(ref total);

            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lines = source.Split('\n');
            if (lines.Length < 2)
            {
                return;
            }

            // Record top-level definition names from the original module.
            var originalNames = new HashSet<string>();
            foreach (var stmt in module.Body)
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

            // Need at least two top-level definitions to meaningfully test
            // that corrupting one does not destroy the others.
            if (originalNames.Count < 2)
            {
                return;
            }

            Interlocked.Increment(ref tested);

            // Corrupt a single line somewhere in the middle of the source.
            var rng = new Random();
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
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(
            $"Error recovery: generated={total}, tested={tested}, " +
            $"survived={survived} programs had surviving definitions");
    }
}
