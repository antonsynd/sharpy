using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #2057, R-IF: a generator object prints python's <c>&lt;generator object qualname&gt;</c> (the
/// <c>at 0x…</c> address omitted, R-CI) and formatting it never runs the body. Lowering records
/// each generator function's <c>__qualname__</c>; code generation stamps it on the iterator method
/// (<c>[SharpyGenerator]</c>); the runtime reaches the stamp from the state machine. Before the cure
/// the state machine was a plain sequence, so <c>print(g)</c> ran the body and printed <c>[1, 2]</c>.
/// python3 3.12 runs the same program and prints the same lines with addresses, body unrun.
///
/// <para>Producer {module function, method, nested-class method, local function, generic function}
/// × site {print, repr, f-string hole, list element}; the body's own <c>print</c> is the witness
/// that it did not run, and a loop afterwards still counts the items.</para>
/// </summary>
public class GeneratorObjectFormattingTests : IntegrationTestBase
{
    public GeneratorObjectFormattingTests(ITestOutputHelper output) : base(output) { }

    private const string Program = """
        def gen() -> int:
            print("body ran")
            yield 1
            yield 2

        def repeat[T](v: T) -> T:
            print("generic body ran")
            yield v

        class K:
            def items(self) -> int:
                print("method body ran")
                yield 3

            class Inner:
                def walk(self) -> int:
                    print("nested body ran")
                    yield 4

        def main() -> None:
            g = gen()
            print(g)
            print(repr(g))
            print(f"{g}")
            print([g])
            print(K().items())
            print(K.Inner().walk())
            print(repeat("x"))
            def inner() -> int:
                print("local body ran")
                yield 5
            print(inner())
            n = 0
            for x in g:
                n += 1
            print(n)
        """;

    [Fact]
    public void GeneratorObjects_PrintTheirQualifiedName_AndNoBodyRunsUntilIterated()
    {
        var result = CompileAndExecute(Program);

        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Replace("\r\n", "\n").Should().Be(
            "<generator object gen>\n"
            + "<generator object gen>\n"
            + "<generator object gen>\n"
            + "[<generator object gen>]\n"
            + "<generator object K.items>\n"
            + "<generator object K.Inner.walk>\n"
            + "<generator object repeat>\n"
            + "<generator object main.<locals>.inner>\n"
            + "body ran\n"
            + "2\n");
    }
}
