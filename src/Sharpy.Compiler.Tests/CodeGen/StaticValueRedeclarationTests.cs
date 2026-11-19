using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// plan-775c50: which value a member access through a subclass binds when the subclass, or a base
/// between it and the <c>@static</c> field's class, redeclares the name. The rule is Python's
/// attribute lookup, MRO by name: the first class on the chain that declares the name decides, and the
/// access binds the base's field only when that first declaration IS the field. A redeclaration of
/// another kind (a property, a method, a nested type) or a field of its own hides the base's field; the
/// base's own spelling keeps its value whatever the subclass does through its own.
///
/// <para><b>Generator.</b> <see cref="Redeclaration"/> (what the redeclaring class declares under the
/// name: nothing, a field, a property, a method or a nested type) × <see cref="Level"/> (the subclass
/// itself, or an intermediate base the leaf inherits it from, plain or generic and closed by the leaf's
/// base list) × <see cref="Use"/> (a read, a store,
/// an augmented assignment and an <c>out</c> operand in an <c>async def</c> across an <c>await</c>,
/// and a read through an instance), each tuple a cell unless <see cref="Excluded"/> says why not.
/// Each cell prints the base's value and the leaf's after the use; the model is the rule's.</para>
/// <para><b>Python.</b> A <c>@static</c> field and a static property have no twin; the method and
/// nested-type reads are CPython's (<c>class C: s = 1</c>, <c>class D(C): @staticmethod def s()</c>
/// reads <c>1 5</c>).</para>
/// </summary>
public class StaticValueRedeclarationTests
{
    private readonly ITestOutputHelper _output;

    public StaticValueRedeclarationTests(ITestOutputHelper output) => _output = output;

    public enum Redeclaration
    {
        None,
        Field,
        Property,
        Method,
        NestedType,
    }

    public enum Level
    {
        Subclass,
        IntermediateBase,
        GenericIntermediateBase,
    }

    public enum Use
    {
        Read,
        Store,
        AsyncAugmented,
        AsyncOut,
        InstanceRead,
    }

    /// <summary>Why a tuple has no cell, or null.</summary>
    internal static string? Excluded(Redeclaration redeclaration, Level level, Use use)
    {
        if (redeclaration == Redeclaration.Method && use is Use.Store or Use.AsyncAugmented or Use.AsyncOut)
            return "a method is no store target";
        if (redeclaration == Redeclaration.Property && use == Use.AsyncOut)
            return "a property is no variable an `out` operand can name";
        if (redeclaration == Redeclaration.Property && use == Use.InstanceRead)
            return "a static property read through an instance is emitted on the instance (CS0176), with no subclass at all (findings ledger)";
        if (redeclaration == Redeclaration.Method && use == Use.InstanceRead)
            return "a static method called through an instance is emitted on the instance (CS0176), with no subclass at all (findings ledger)";
        if (redeclaration == Redeclaration.NestedType && use == Use.InstanceRead)
            return "a nested type is named through its declaring type, not an instance";
        if (redeclaration == Redeclaration.Field && level == Level.GenericIntermediateBase && use == Use.InstanceRead)
            return "a generic class's static read through an instance spells the open generic (CS0305), `G[int]().s` with no subclass at all (#2435, findings ledger)";
        return null;
    }

    internal sealed record Cell(Redeclaration Redeclaration, Level Level, Use Use)
    {
        public string Id => $"{Redeclaration}/{Level}/{Use}";
    }

    internal static IReadOnlyList<Cell> Cells() =>
        (from redeclaration in Enum.GetValues<Redeclaration>()
         from level in Enum.GetValues<Level>()
         from use in Enum.GetValues<Use>()
         where Excluded(redeclaration, level, use) == null
         select new Cell(redeclaration, level, use)).ToList();

    private sealed record Program(int Key, Cell Cell)
    {
        private string Base => $"C{Key}";
        private string Leaf => $"L{Key}";

        private bool IsAsync => Cell.Use is Use.AsyncAugmented or Use.AsyncOut;

        /// <summary>The leaf's value as the cell spells it.</summary>
        private string Value
        {
            get
            {
                var receiver = Cell.Use == Use.InstanceRead ? $"{Leaf}()" : Leaf;
                return Cell.Redeclaration switch
                {
                    Redeclaration.Method => $"{receiver}.s()",
                    Redeclaration.NestedType => $"{receiver}.s.z",
                    _ => $"{receiver}.s",
                };
            }
        }

        private IEnumerable<string> RedeclaringBody() => Cell.Redeclaration switch
        {
            Redeclaration.None => new[] { "    pass" },
            Redeclaration.Field => new[] { "    @static", "    s: int = 5" },
            Redeclaration.Property => new[] { "    @static", "    property s: int = 5" },
            Redeclaration.Method => new[] { "    @static", "    def s() -> int:", "        return 5" },
            Redeclaration.NestedType => new[] { "    class s:", "        @static", "        z: int = 5" },
            _ => throw new ArgumentOutOfRangeException(),
        };

        public IEnumerable<string> Lines()
        {
            yield return $"class {Base}:";
            yield return "    @static";
            yield return "    s: int = 1";
            var redeclaring = Cell.Level switch
            {
                Level.Subclass => Leaf,
                Level.IntermediateBase => $"M{Key}",
                _ => $"M{Key}[T]",
            };
            yield return $"class {redeclaring}({Base}):";
            foreach (var line in RedeclaringBody())
                yield return line;
            if (Cell.Level != Level.Subclass)
            {
                yield return $"class {Leaf}(M{Key}{(Cell.Level == Level.GenericIntermediateBase ? "[int]" : "")}):";
                yield return "    pass";
            }
            yield return $"{(IsAsync ? "async def" : "def")} c{Key}() -> str:";
            switch (Cell.Use)
            {
                case Use.Store:
                    yield return $"    {Value} = 6";
                    break;
                case Use.AsyncAugmented:
                    yield return $"    {Value} += await one()";
                    break;
                case Use.AsyncOut:
                    yield return $"    take(out {Value}, await one())";
                    break;
            }
            yield return $"    return f\"{{{Base}.s}} {{{Value}}}\"";
        }

        public string Invocation => $"{(IsAsync ? "await " : "")}c{Key}()";

        /// <summary>The base's value and the leaf's: one storage while no class redeclares the name.</summary>
        public string Model
        {
            get
            {
                var hidden = Cell.Redeclaration != Redeclaration.None;
                var leaf = Cell.Use switch
                {
                    Use.Read or Use.InstanceRead => hidden ? 5 : 1,
                    Use.Store => 6,
                    Use.AsyncAugmented => hidden ? 6 : 2,
                    Use.AsyncOut => 7,
                    _ => throw new ArgumentOutOfRangeException(),
                };
                return $"{(hidden ? 1 : leaf)} {leaf}";
            }
        }
    }

    [Fact]
    public void RedeclaredNames_BindTheFirstDeclarationOnTheChain()
    {
        var programs = Cells().Select((c, i) => new Program(i, c)).ToList();
        var main = new List<string>
        {
            "def take(a: out int, n: int) -> None:",
            "    a = 7",
            "async def one() -> int:",
            "    return 1",
        };
        foreach (var program in programs)
            main.AddRange(program.Lines());
        main.Add("async def main() -> None:");
        foreach (var program in programs)
            main.Add($"    print(\"{program.Cell.Id}\", {program.Invocation})");

        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("StaticValueRedeclaration")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .AddSourceFile("main.spy", string.Join("\n", main) + "\n");
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();
        exec.CompilationErrors.Should().BeEmpty("every cell compiles");
        exec.Success.Should().BeTrue(exec.StandardError);

        var expected = programs.Select(p => $"{p.Cell.Id} {p.Model}").ToList();
        var actual = exec.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();
        var failures = expected.Zip(actual, (e, a) => (e, a)).Where(p => p.e != p.a)
            .Select(p => $"expected '{p.e}', got '{p.a}'").ToList();
        if (expected.Count != actual.Count)
            failures.Add($"{expected.Count} lines expected, {actual.Count} printed");
        _output.WriteLine($"{programs.Count} cells");
        failures.Should().BeEmpty("each cell prints the value its first declaration binds");
    }

    /// <summary>
    /// A nested class's base named by a sibling (<c>class Outer: class C; class D(C)</c>) is the
    /// sibling, not a module class of the same name: the emitted C# base clause binds the sibling, so
    /// the checker must too, or <c>Outer.D.s</c> binds the module class's field (CPython reads
    /// <c>2 1</c> then <c>6 1</c>: the class body's own namespace is searched before the module's).
    /// </summary>
    [Fact]
    public void NestedSubclass_OfASibling_BindsTheSiblingsField()
    {
        var source = string.Join("\n",
            "class C:",
            "    @static",
            "    s: int = 1",
            "class Outer:",
            "    class C:",
            "        @static",
            "        s: int = 2",
            "    class D(C):",
            "        pass",
            "async def one() -> int:",
            "    return 1",
            "async def main() -> None:",
            "    print(Outer.D.s, C.s)",
            "    Outer.D.s = 6",
            "    print(Outer.D.s, C.s)",
            "    Outer.D.s += await one()",
            "    print(Outer.D.s, C.s)",
            "") + "\n";

        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("NestedSiblingBase")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .AddSourceFile("main.spy", source);
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();
        exec.CompilationErrors.Should().BeEmpty();
        exec.StandardOutput.Replace("\r\n", "\n").Should().Be("2 1\n6 1\n7 1\n");
    }

    /// <summary>The roster: every redeclaration, level and use has a cell or a named exclusion.</summary>
    [Fact]
    public void Generator_IsTotal_OverRedeclarationsLevelsAndUses()
    {
        var cells = Cells();
        Enum.GetValues<Redeclaration>().Should().OnlyContain(r => cells.Any(c => c.Redeclaration == r));
        Enum.GetValues<Level>().Should().OnlyContain(l => cells.Any(c => c.Level == l));
        Enum.GetValues<Use>().Should().OnlyContain(u => cells.Any(c => c.Use == u));
        var excluded = (from r in Enum.GetValues<Redeclaration>()
                        from l in Enum.GetValues<Level>()
                        from u in Enum.GetValues<Use>()
                        where Excluded(r, l, u) != null
                        select 1).Count();
        (Enum.GetValues<Redeclaration>().Length * Enum.GetValues<Level>().Length * Enum.GetValues<Use>().Length)
            .Should().Be(cells.Count + excluded);
    }
}
