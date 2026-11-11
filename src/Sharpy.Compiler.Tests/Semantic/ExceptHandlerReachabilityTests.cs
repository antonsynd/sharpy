using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The handler-list shapes of #2363 that <c>ExceptHandlerOrderDifferentialTests</c>' (base, derived)
/// generator does not reach: a <c>when</c> filter on either side, a bare <c>except:</c> in each
/// position, a tuple covered by an earlier element of itself, a user-declared hierarchy, a handler
/// covered from two clauses back, the same type twice, nested <c>try</c>, and <c>else</c>/<c>finally</c>.
/// Every python-spellable cell's expected stdout was run with python3.15.2 (a <c>when</c> cell has no
/// python spelling; its expectation is C#'s filter semantics, which the spec states). Each cell also
/// pins how many SPY0488 warnings it draws, and that none of them is SPY0908.
/// </summary>
public class ExceptHandlerReachabilityTests : IntegrationTestBase
{
    public ExceptHandlerReachabilityTests(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> Cells() => new[]
    {
        new object[] { "user_hierarchy_base_first",
            "class AppError(Exception):\n    pass\n\nclass DbError(AppError):\n    pass\n\n"
            + "def main() -> None:\n    try:\n        raise DbError(\"d\")\n    except AppError:\n        print(\"app\")\n"
            + "    except DbError:\n        print(\"db\")\n",
            "app", 1 },
        new object[] { "covered_two_clauses_back",
            "def main() -> None:\n    try:\n        raise KeyError(\"k\")\n    except LookupError:\n        print(\"lookup\")\n"
            + "    except ValueError:\n        print(\"value\")\n    except KeyError:\n        print(\"key\")\n",
            "lookup", 1 },
        new object[] { "same_type_twice_bound",
            "def main() -> None:\n    try:\n        raise ValueError(\"v\")\n    except ValueError as e:\n        print(\"first\", e)\n"
            + "    except ValueError as e:\n        print(\"second\", e)\n",
            "first v", 1 },
        new object[] { "bare_after_exception",
            "def main() -> None:\n    try:\n        raise KeyError(\"k\")\n    except Exception:\n        print(\"exc\")\n"
            + "    except:\n        print(\"bare\")\n",
            "exc", 1 },
        new object[] { "bare_last_reachable",
            "def main() -> None:\n    try:\n        raise KeyError(\"k\")\n    except ValueError:\n        print(\"value\")\n"
            + "    except:\n        print(\"bare\")\n",
            "bare", 0 },
        new object[] { "tuple_partly_covered_unbound",
            "def main() -> None:\n    for i in range(2):\n        try:\n            if i == 0:\n                raise KeyError(\"k\")\n"
            + "            raise ValueError(\"v\")\n        except LookupError:\n            print(\"lookup\")\n"
            + "        except (KeyError, ValueError):\n            print(\"tuple\")\n",
            "lookup\ntuple", 0 },
        new object[] { "tuple_partly_covered_bound",
            "def main() -> None:\n    for i in range(2):\n        try:\n            if i == 0:\n                raise KeyError(\"k\")\n"
            + "            raise ValueError(\"v\")\n        except LookupError:\n            print(\"lookup\")\n"
            + "        except (KeyError, ValueError) as e:\n            print(\"tuple\", e)\n",
            "lookup\ntuple v", 0 },
        new object[] { "tuple_wholly_covered",
            "def main() -> None:\n    try:\n        raise IndexError(\"i\")\n    except LookupError:\n        print(\"lookup\")\n"
            + "    except (KeyError, IndexError):\n        print(\"tuple\")\n",
            "lookup", 1 },
        new object[] { "tuple_covered_by_its_own_earlier_element",
            "def main() -> None:\n    try:\n        raise KeyError(\"k\")\n    except (LookupError, KeyError):\n        print(\"tuple\")\n",
            "tuple", 0 },
        new object[] { "else_and_finally",
            "def main() -> None:\n    for i in range(2):\n        try:\n            if i == 0:\n                raise ValueError(\"v\")\n"
            + "        except Exception:\n            print(\"exc\")\n        except ValueError:\n            print(\"val\")\n"
            + "        else:\n            print(\"else\")\n        finally:\n            print(\"finally\")\n",
            "exc\nfinally\nelse\nfinally", 1 },
        new object[] { "nested_try",
            "def main() -> None:\n    try:\n        try:\n            raise KeyError(\"k\")\n        except LookupError:\n"
            + "            print(\"inner lookup\")\n            raise\n        except KeyError:\n            print(\"inner key\")\n"
            + "    except Exception:\n        print(\"outer exc\")\n    except KeyError:\n        print(\"outer key\")\n",
            "inner lookup\nouter exc", 2 },
        new object[] { "earlier_filter_covers_nothing",
            "def main() -> None:\n    try:\n        raise ValueError(\"v\")\n    except ValueError when False:\n        print(\"filtered\")\n"
            + "    except ValueError:\n        print(\"plain\")\n",
            "plain", 0 },
        new object[] { "later_filtered_handler_is_covered",
            "def main() -> None:\n    try:\n        raise ValueError(\"v\")\n    except Exception:\n        print(\"exc\")\n"
            + "    except ValueError when True:\n        print(\"filtered\")\n",
            "exc", 1 },
        new object[] { "derived_first_is_untouched",
            "def main() -> None:\n    try:\n        raise LookupError(\"l\")\n    except KeyError:\n        print(\"key\")\n"
            + "    except LookupError:\n        print(\"lookup\")\n",
            "lookup", 0 },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    public void HandlerList_RunsFirstMatchWins_AndWarnsEachDeadHandler(string name, string source, string expected, int warnings)
    {
        var run = CompileAndExecute(source, name + ".spy");

        Assert.DoesNotContain(run.RawDiagnostics, d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
        Assert.True(RanToCompletion(run), JudgeRun(run, source, name) ?? "timed out");
        Assert.Equal(expected, run.StandardOutput.Trim().Replace("\r\n", "\n"));
        Assert.Equal(warnings, run.RawDiagnostics.Count(d => d.Code == DiagnosticCodes.Validation.UnreachableArm));
    }

    /// <summary>
    /// A bare <c>except:</c> that is not last is refused at the handler (SPY0151), as python refuses it,
    /// never handed to Roslyn (CS1017 behind SPY0908).
    /// </summary>
    [Fact]
    public void BareExceptNotLast_IsRefusedAtTheHandler()
    {
        const string source = "def main() -> None:\n    try:\n        raise KeyError(\"k\")\n    except:\n        print(\"bare\")\n"
            + "    except KeyError:\n        print(\"key\")\n";
        var run = CompileAndExecute(source, "bare_not_last.spy");

        Assert.False(run.Success);
        var refusal = Assert.Single(run.RawDiagnostics, d => d.Code == DiagnosticCodes.Parser.BareExceptMustBeLast);
        Assert.Equal(4, refusal.Line);
        Assert.Equal(5, refusal.Column);
        Assert.DoesNotContain(run.RawDiagnostics, d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }
}
