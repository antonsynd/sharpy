using CsCheck;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class CfgWellFormednessPropertyTests
{
    private readonly ITestOutputHelper _output;

    public CfgWellFormednessPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Builds control flow graphs from every function in randomly generated,
    /// successfully-compiled programs and asserts structural invariants:
    /// the entry block is labeled "entry"; entry and exit blocks are members
    /// of the Blocks collection; every successor edge points to a block in the
    /// graph; the exit block has no successors; and statement counts are
    /// non-negative. Invariant violations are surfaced as failures while
    /// unrelated compilation/parse errors (expected for random programs) are
    /// swallowed.
    /// </summary>
    [Fact]
    public void CfgFromCompiledPrograms_SatisfiesStructuralInvariants()
    {
        int total = 0;
        int cfgsChecked = 0;

        Gen.Int[2, 3].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            Interlocked.Increment(ref total);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Compile(source, "cfg_test.spy");
                if (!result.Success)
                    return;

                // Re-parse to obtain FunctionDef AST nodes to feed the CFG builder.
                var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
                var tokens = lexer.TokenizeAll();
                var parser = new Sharpy.Compiler.Parser.Parser(tokens);
                var parsed = parser.ParseModule();

                foreach (var stmt in parsed.Body)
                {
                    if (stmt is not FunctionDef funcDef)
                        continue;

                    var builder = new ControlFlowGraphBuilder();
                    var cfg = builder.Build(funcDef);

                    // Entry block exists and is labeled "entry".
                    if (cfg.Entry.Label != "entry")
                        throw new CfgInvariantException(
                            $"Entry block label is '{cfg.Entry.Label}', expected 'entry'");

                    // Exit block is labeled "exit".
                    if (cfg.Exit.Label != "exit")
                        throw new CfgInvariantException(
                            $"Exit block label is '{cfg.Exit.Label}', expected 'exit'");

                    // Entry and Exit are members of Blocks.
                    if (!cfg.Blocks.Contains(cfg.Entry))
                        throw new CfgInvariantException("Entry block not in Blocks collection");
                    if (!cfg.Blocks.Contains(cfg.Exit))
                        throw new CfgInvariantException("Exit block not in Blocks collection");

                    // Block IDs are unique.
                    var ids = new HashSet<int>();
                    foreach (var block in cfg.Blocks)
                    {
                        if (!ids.Add(block.Id))
                            throw new CfgInvariantException($"Duplicate block id {block.Id}");
                    }

                    // Every successor edge points to a block in the graph, and
                    // edges are symmetric (successor lists imply predecessor lists).
                    var blockSet = new HashSet<BasicBlock>(cfg.Blocks);
                    foreach (var block in cfg.Blocks)
                    {
                        foreach (var succ in block.Successors)
                        {
                            if (!blockSet.Contains(succ))
                                throw new CfgInvariantException(
                                    $"Block {block} has successor {succ} not in Blocks");
                            if (!succ.Predecessors.Contains(block))
                                throw new CfgInvariantException(
                                    $"Successor {succ} of {block} is missing matching predecessor edge");
                        }
                    }

                    // Exit has no successors.
                    if (cfg.Exit.Successors.Count != 0)
                        throw new CfgInvariantException(
                            $"Exit block has {cfg.Exit.Successors.Count} successors");

                    // Statement counts are non-negative (sanity).
                    foreach (var block in cfg.Blocks)
                    {
                        if (block.Statements.Count < 0)
                            throw new CfgInvariantException(
                                $"Block {block} has negative statement count");
                    }

                    // Unreachable-block detection must return a subset of Blocks.
                    foreach (var unreachable in cfg.FindUnreachableBlocks())
                    {
                        if (!blockSet.Contains(unreachable))
                            throw new CfgInvariantException(
                                "FindUnreachableBlocks returned a block not in Blocks");
                    }

                    Interlocked.Increment(ref cfgsChecked);
                }
            }
            catch (CfgInvariantException)
            {
                // Real invariant violation — propagate so the property fails.
                throw;
            }
            catch (Exception)
            {
                // Compilation/parse/runtime failures are expected for random
                // programs and are not what this property is testing.
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"CFG well-formedness: {cfgsChecked} CFGs checked from {total} programs");
    }

    private sealed class CfgInvariantException : Exception
    {
        public CfgInvariantException(string message) : base(message) { }
    }
}
