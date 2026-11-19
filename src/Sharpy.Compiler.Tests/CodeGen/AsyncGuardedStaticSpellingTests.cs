using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// plan-775c50 D4/D6 crossed with every spelling of a <c>@static</c> value through a type-denoting
/// receiver, run: in an <c>async def</c> body each D6 use of the value across an <c>await</c> (an
/// <c>out</c> operand, an augmented assignment, a mutating call on it, a store) binds the value through
/// the one resolver, so it is emitted through the value's helpers, never as a by-reference C# access
/// across the <c>await</c> (CS8178 behind SPY0908) nor as a spelling the C# cannot name.
///
/// <para><b>Generator.</b> <see cref="Spelling"/> (the receiver: its own class, a subclass, a type
/// alias, a nested type and a nested subclass, a subclass of a generic base, a constructed generic and
/// its alias, a type nested under a constructed generic, a subclass imported by name, a class imported
/// under an alias and a local subclass of that alias, a local subclass of another module's class,
/// another module's class and subclass qualified, and a struct) × <see cref="Use"/>, each pair a cell
/// unless <see cref="Excluded"/> says why not. Each cell is an <c>async def</c> holding the use with an
/// <c>await</c> operand, and its plain <c>def</c> twin, the control, on values of their own; both must
/// print the model. One project.</para>
/// <para><b>Python.</b> No twin: Python has no <c>@static</c> and no <c>out</c>.</para>
/// </summary>
public class AsyncGuardedStaticSpellingTests
{
    private readonly ITestOutputHelper _output;

    public AsyncGuardedStaticSpellingTests(ITestOutputHelper output) => _output = output;

    public enum Spelling
    {
        OwnClass,
        Subclass,
        Alias,
        NestedOwn,
        NestedSubclass,
        GenericBaseSubclass,
        ClosedGeneric,
        ClosedGenericAlias,
        GenericNested,
        FromImportSubclass,
        FromImportAlias,
        FromImportAliasSubclass,
        LocalSubclassOfLib,
        ModuleQualifiedType,
        ModuleQualifiedSubclass,
        Struct,
    }

    public enum Use
    {
        OutOperand,
        Augmented,
        MutatingCall,
        Store,
    }

    /// <summary>Why a pair has no cell, or null.</summary>
    internal static string? Excluded(Spelling spelling, Use use)
    {
        if (use == Use.Augmented && spelling is Spelling.ClosedGeneric or Spelling.ModuleQualifiedType or Spelling.ModuleQualifiedSubclass)
            return "#2435: an augmented assignment through a constructed generic or a module-qualified type is a C# compound on a type name (CS0119), in a plain def too";
        return null;
    }

    internal sealed record Cell(Spelling Spelling, Use Use)
    {
        public string Id => $"{Spelling}/{Use}";
    }

    internal static IReadOnlyList<Cell> Cells() =>
        (from spelling in Enum.GetValues<Spelling>()
         from use in Enum.GetValues<Use>()
         where Excluded(spelling, use) == null
         select new Cell(spelling, use)).ToList();

    /// <summary>One function of a cell (its async use's or its plain twin's) and the value it uses.</summary>
    private sealed record Value(int Key, Cell Cell, bool IsAsync)
    {
        private string Operand => IsAsync ? "await one()" : "one_s()";

        /// <summary>The receiver, as the cell spells it.</summary>
        private string Receiver => Cell.Spelling switch
        {
            Spelling.OwnClass => $"H{Key}",
            Spelling.Subclass => $"D{Key}",
            Spelling.Alias => $"E{Key}",
            Spelling.NestedOwn => $"O{Key}.C",
            Spelling.NestedSubclass => $"O{Key}.D",
            Spelling.GenericBaseSubclass => $"GD{Key}",
            Spelling.ClosedGeneric => $"G{Key}[int]",
            Spelling.ClosedGenericAlias => $"GA{Key}",
            Spelling.GenericNested => $"G{Key}[int].N",
            Spelling.FromImportSubclass => $"LD{Key}",
            Spelling.FromImportAlias => $"CC{Key}",
            Spelling.FromImportAliasSubclass => $"D2{Key}",
            Spelling.LocalSubclassOfLib => $"SL{Key}",
            Spelling.ModuleQualifiedType => $"lib.LC{Key}",
            Spelling.ModuleQualifiedSubclass => $"lib.LD{Key}",
            Spelling.Struct => $"St{Key}",
            _ => throw new ArgumentOutOfRangeException(),
        };

        private static readonly string[] Members = { "    @static", "    s: int = 1", "    @static", "    q: list[int] = [1]" };

        private static IEnumerable<string> Indented(string indent) => Members.Select(m => indent + m);

        private bool InLib => Cell.Spelling is Spelling.FromImportSubclass or Spelling.FromImportAlias
            or Spelling.FromImportAliasSubclass or Spelling.LocalSubclassOfLib
            or Spelling.ModuleQualifiedType or Spelling.ModuleQualifiedSubclass;

        public IEnumerable<string> Imports()
        {
            if (Cell.Spelling == Spelling.FromImportSubclass)
                yield return $"from lib import LD{Key}";
            if (Cell.Spelling is Spelling.FromImportAlias or Spelling.FromImportAliasSubclass)
                yield return $"from lib import LC{Key} as CC{Key}";
        }

        public IEnumerable<string> LibDeclarations()
        {
            if (!InLib)
                yield break;
            yield return $"class LC{Key}:";
            foreach (var line in Indented(""))
                yield return line;
            yield return $"class LD{Key}(LC{Key}):";
            yield return "    pass";
        }

        public IEnumerable<string> Declarations()
        {
            switch (Cell.Spelling)
            {
                case Spelling.OwnClass or Spelling.Subclass or Spelling.Alias:
                    yield return $"class H{Key}:";
                    foreach (var line in Indented(""))
                        yield return line;
                    if (Cell.Spelling == Spelling.Subclass)
                    {
                        yield return $"class D{Key}(H{Key}):";
                        yield return "    pass";
                    }
                    if (Cell.Spelling == Spelling.Alias)
                        yield return $"type E{Key} = H{Key}";
                    break;
                case Spelling.NestedOwn or Spelling.NestedSubclass:
                    yield return $"class O{Key}:";
                    yield return "    class C:";
                    foreach (var line in Indented("    "))
                        yield return line;
                    yield return "    class D(C):";
                    yield return "        pass";
                    break;
                case Spelling.GenericBaseSubclass or Spelling.ClosedGeneric or Spelling.ClosedGenericAlias or Spelling.GenericNested:
                    yield return $"class G{Key}[T]:";
                    if (Cell.Spelling == Spelling.GenericNested)
                    {
                        yield return "    class N:";
                        foreach (var line in Indented("    "))
                            yield return line;
                    }
                    else
                    {
                        foreach (var line in Indented(""))
                            yield return line;
                    }
                    if (Cell.Spelling == Spelling.GenericBaseSubclass)
                    {
                        yield return $"class GD{Key}(G{Key}[int]):";
                        yield return "    pass";
                    }
                    if (Cell.Spelling == Spelling.ClosedGenericAlias)
                        yield return $"type GA{Key} = G{Key}[int]";
                    break;
                case Spelling.FromImportAliasSubclass:
                    yield return $"class D2{Key}(CC{Key}):";
                    yield return "    pass";
                    break;
                case Spelling.LocalSubclassOfLib:
                    yield return $"class SL{Key}(lib.LC{Key}):";
                    yield return "    pass";
                    break;
                case Spelling.Struct:
                    yield return $"struct St{Key}:";
                    foreach (var line in Indented(""))
                        yield return line;
                    break;
            }
        }

        private string Statement => Cell.Use switch
        {
            Use.OutOperand => $"take(out {Receiver}.s, {Operand})",
            Use.Augmented => $"{Receiver}.s += {Operand}",
            Use.MutatingCall => $"{Receiver}.q.append({Operand})",
            Use.Store => $"{Receiver}.s = 5 + {Operand}",
            _ => throw new ArgumentOutOfRangeException(),
        };

        public string Name => $"c{Key}";

        public IEnumerable<string> Function() => new[]
        {
            $"{(IsAsync ? "async def" : "def")} {Name}() -> str:",
            $"    {Statement}",
            $"    return f\"{{{Receiver}.s}} {{{Receiver}.q}}\"",
        };

        public string Model => Cell.Use switch
        {
            Use.OutOperand => "7 [1]",
            Use.Augmented => "2 [1]",
            Use.MutatingCall => "1 [1, 1]",
            Use.Store => "6 [1]",
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    [Fact]
    public void GuardedUses_OnEveryTypeDenotingSpelling_RunAsTheirPlainTwin()
    {
        var key = 0;
        var values = Cells().SelectMany(c => new[] { new Value(key++, c, true), new Value(key++, c, false) }).ToList();

        var main = values.SelectMany(v => v.Imports()).ToList();
        main.Add("import lib");
        main.Add("def take(a: out int, n: int) -> None:\n    a = 7");
        main.Add("async def one() -> int:\n    return 1");
        main.Add("def one_s() -> int:\n    return 1");
        foreach (var value in values)
        {
            main.AddRange(value.Declarations());
            main.AddRange(value.Function());
        }
        main.Add("async def main() -> None:");
        foreach (var value in values)
            main.Add($"    print(\"{value.Cell.Id}{(value.IsAsync ? "" : "/sync")}\", {(value.IsAsync ? "await " : "")}{value.Name}())");

        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("AsyncGuardedStaticSpelling")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .AddSourceFile("lib.spy", string.Join("\n", values.SelectMany(v => v.LibDeclarations())) + "\n")
            .AddSourceFile("main.spy", string.Join("\n", main) + "\n");
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();
        exec.CompilationErrors.Should().BeEmpty("every cell compiles");
        exec.Success.Should().BeTrue(exec.StandardError);

        var expected = values.Select(v => $"{v.Cell.Id}{(v.IsAsync ? "" : "/sync")} {v.Model}").ToList();
        var actual = exec.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();
        var failures = expected.Zip(actual, (e, a) => (e, a)).Where(p => p.e != p.a).Select(p => $"expected '{p.e}', got '{p.a}'").ToList();
        if (expected.Count != actual.Count)
            failures.Add($"{expected.Count} lines expected, {actual.Count} printed");
        _output.WriteLine($"{values.Count / 2} cells, {values.Count} functions");
        failures.Should().BeEmpty("each cell prints its model, async and plain");
    }

    /// <summary>The roster: every spelling and use has a cell, and the cells and the named exclusions
    /// partition the product.</summary>
    [Fact]
    public void Generator_IsTotal_OverSpellingsAndUses()
    {
        var cells = Cells();
        Enum.GetValues<Spelling>().Should().OnlyContain(s => cells.Any(c => c.Spelling == s));
        Enum.GetValues<Use>().Should().OnlyContain(u => cells.Any(c => c.Use == u));
        var excluded = (from s in Enum.GetValues<Spelling>()
                        from u in Enum.GetValues<Use>()
                        where Excluded(s, u) != null
                        select 1).Count();
        (Enum.GetValues<Spelling>().Length * Enum.GetValues<Use>().Length).Should().Be(cells.Count + excluded);
        _output.WriteLine($"{cells.Count} cells, {excluded} excluded");
    }
}
