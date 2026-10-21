using CsCheck;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class CfgWellFormednessPropertyTests
{
    private readonly ITestOutputHelper _output;

    public CfgWellFormednessPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Builds control flow graphs from every function in typed-generator programs that compile
    /// clean and asserts structural invariants: the entry block is labeled "entry"; entry and exit
    /// blocks are members of the Blocks collection; every successor edge points to a block in the
    /// graph; the exit block has no successors; and statement counts are non-negative. The lane reads
    /// <see cref="GenFunctions.ModuleWithFunctions"/> through <see cref="SemanticFilter"/> with a
    /// <see cref="RejectionSink"/>: every raw sample compiles (or is a pinned known red), and a THROW
    /// anywhere — compile, re-parse or CFG build — fails the sample. It used to sample the untyped
    /// generator and skip what the compiler refused, which was every program with a function: the
    /// lane checked 0 CFGs from 50 programs and passed (#2288). At least one CFG must be checked.
    /// </summary>
    [Fact]
    public void CfgFromCompiledPrograms_SatisfiesStructuralInvariants()
    {
        int total = 0;
        int cfgsTested = 0;
        var sink = new RejectionSink("CfgWellFormedness.CfgFromCompiledPrograms_SatisfiesStructuralInvariants");

        SemanticFilter.CompilableProgram(GenFunctions.ModuleWithFunctions(TypeEnv.Default, "int", fuel: 2), sink)
            .Sample(module =>
        {
            Interlocked.Increment(ref total);
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            // Re-parse to obtain FunctionDef AST nodes to feed the CFG builder.
            var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            var parsed = parser.ParseModule();

            int functions = 0;
            foreach (var stmt in parsed.Body)
            {
                if (stmt is not FunctionDef funcDef)
                    continue;
                functions++;

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

                Interlocked.Increment(ref cfgsTested);
            }
            if (functions == 0)
                throw new CfgInvariantException($"ModuleWithFunctions drew a module without a function:\n{source}");
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine(sink.Summary);
        _output.WriteLine($"CFG well-formedness: {cfgsTested} CFGs checked from {total} programs");
        Assert.True(sink.Unexplained.Count == 0, sink.Report());
        // Applicability floor: a run that checks no CFG proves nothing (#2288).
        Assert.True(cfgsTested > 0, $"no CFG was checked from {total} programs");
    }

    private sealed class CfgInvariantException : Exception
    {
        public CfgInvariantException(string message) : base(message) { }
    }
}
