using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Benchmarks;

/// <summary>
/// Run-time cost of the GENERATED code that reads and stores a module or class static value
/// (plan P77, acceptance A12). P77 emits a Guarded value as a backing field, a state byte and a
/// same-name <c>ref</c> property whose getter takes a fast path once the host's sweep is done; a
/// plain store goes through <c>x__set</c>. This class measures that steady state against the
/// pre-P77 emission (a plain static field), with <c>build_tools/bench_ab.py</c>.
/// </summary>
/// <remarks>
/// <para>
/// One Sharpy program is compiled in-process with the in-tree compiler, its C# compiled in Release
/// and loaded in each benchmark case's setup. Each benchmark calls one generated function that loops
/// <see cref="Operations"/> times over a single operation, so a row's mean is nanoseconds per
/// operation (loop overhead included; <see cref="Local"/> is that overhead with a local).
/// </para>
/// <para>
/// The program must compile at BOTH refs being compared (P77's merge base and its tip), so it uses
/// only surface both accept: no <c>global</c>, no imports. Its <c>main</c> is never run.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class StaticValueBenchmarks
{
    /// <summary>Loop trips per benchmark call; each row reports per-operation time.</summary>
    public const int Operations = 1024;

    private const string Source = @"struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y


class Host[T]:
    @static
    count: int = 1


def seed() -> int:
    return 7


counter: int = seed()
pt: Point = Point(1, 2)
const SCALE: int = seed() * 3


def bench_local(n: int) -> int:
    v: int = seed()
    acc: int = 0
    i: int = 0
    while i < n:
        acc += v
        i += 1
    return acc


def bench_read(n: int) -> int:
    acc: int = 0
    i: int = 0
    while i < n:
        acc += counter
        i += 1
    return acc


def bench_store(n: int) -> int:
    i: int = 0
    while i < n:
        counter = i
        i += 1
    return counter


def bench_augmented(n: int) -> int:
    i: int = 0
    while i < n:
        counter += 1
        i += 1
    return counter


def bench_struct_store(n: int) -> int:
    i: int = 0
    while i < n:
        pt.x = i
        i += 1
    return pt.x


def bench_const_read(n: int) -> int:
    acc: int = 0
    i: int = 0
    while i < n:
        acc += SCALE
        i += 1
    return acc


def bench_generic_read(n: int) -> int:
    acc: int = 0
    i: int = 0
    while i < n:
        acc += Host[int].count
        i += 1
    return acc


def bench_generic_store(n: int) -> int:
    i: int = 0
    while i < n:
        Host[int].count = i
        i += 1
    return Host[int].count


def main() -> None:
    print(bench_local(1))
";

    private Func<int, int> _local = null!;
    private Func<int, int> _read = null!;
    private Func<int, int> _store = null!;
    private Func<int, int> _augmented = null!;
    private Func<int, int> _structStore = null!;
    private Func<int, int> _constRead = null!;
    private Func<int, int> _genericRead = null!;
    private Func<int, int> _genericStore = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A fresh load per benchmark case, so the program's static values start from their
        // initializers for the checks below even when cases share a process (`--inProcess`).
        var program = CompileProgram();
        Func<int, int> Function(string name) => program.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Single(m => m.Name == name)
            .CreateDelegate<Func<int, int>>();

        _local = Function("BenchLocal");
        _read = Function("BenchRead");
        _store = Function("BenchStore");
        _augmented = Function("BenchAugmented");
        _structStore = Function("BenchStructStore");
        _constRead = Function("BenchConstRead");
        _genericRead = Function("BenchGenericRead");
        _genericStore = Function("BenchGenericStore");

        // Each function must compute what the program means, or a row would time a broken loop.
        // The calls also run every host's initialization before measurement starts.
        Expect(_local(10), 70, "bench_local");
        Expect(_read(10), 70, "bench_read");
        Expect(_store(10), 9, "bench_store");
        Expect(_augmented(10), 19, "bench_augmented");
        Expect(_structStore(10), 9, "bench_struct_store");
        Expect(_constRead(10), 210, "bench_const_read");
        Expect(_genericRead(10), 10, "bench_generic_read");
        Expect(_genericStore(10), 9, "bench_generic_store");
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Operations, Description = "local read (baseline)")]
    public int Local() => _local(Operations);

    [Benchmark(OperationsPerInvoke = Operations, Description = "module value read")]
    public int Read() => _read(Operations);

    [Benchmark(OperationsPerInvoke = Operations, Description = "module value store")]
    public int Store() => _store(Operations);

    [Benchmark(OperationsPerInvoke = Operations, Description = "module value +=")]
    public int Augmented() => _augmented(Operations);

    [Benchmark(OperationsPerInvoke = Operations, Description = "module struct member store")]
    public int StructMemberStore() => _structStore(Operations);

    [Benchmark(OperationsPerInvoke = Operations, Description = "computed const read")]
    public int ComputedConstRead() => _constRead(Operations);

    [Benchmark(OperationsPerInvoke = Operations, Description = "generic host @static read")]
    public int GenericHostRead() => _genericRead(Operations);

    [Benchmark(OperationsPerInvoke = Operations, Description = "generic host @static store")]
    public int GenericHostStore() => _genericStore(Operations);

    private static void Expect(int actual, int expected, string function)
    {
        if (actual != expected)
            throw new InvalidOperationException($"{function}(10) returned {actual}, expected {expected}");
    }

    private static Assembly CompileProgram()
    {
        const string fileName = "static_value_bench.spy";
        var result = new Compiler().Compile(Source, fileName);
        if (!result.Success || result.GeneratedCSharpCode is not { } code)
        {
            throw new InvalidOperationException(
                "the benchmark program does not compile: "
                + string.Join(" | ", result.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message}")));
        }

        var compilation = CSharpCompilation.Create(
            "StaticValueBench",
            new[] { CSharpSyntaxTree.ParseText(code, CSharpParseOptions.Default) },
            AssemblyCompiler.GetDefaultReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release));

        using var image = new MemoryStream();
        var emit = compilation.Emit(image);
        if (!emit.Success)
        {
            throw new InvalidOperationException(
                "the benchmark program's C# does not compile: "
                + string.Join(" | ", emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        }

        return Assembly.Load(image.ToArray());
    }
}
