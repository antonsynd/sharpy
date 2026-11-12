extern alias SharpyRT;

using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Integration;
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
/// pins how many SPY0488 warnings it draws, and that none of them is SPY0908; the SystemExit cells pin
/// the one deviation (no BaseException) and its refusal, SPY0709.
/// </summary>
public class ExceptHandlerReachabilityTests : StdlibAwareIntegrationTestBase
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
        // python3.15.2 prints "first" for both: the earlier SystemExit handler, not `except Exception:`,
        // is what covers the third handler, so it is a plain dead handler and the message names it.
        new object[] { "systemexit_exception_systemexit",
            "def main() -> None:\n    try:\n        raise SystemExit(2)\n    except SystemExit:\n        print(\"first\")\n"
            + "    except Exception:\n        print(\"exc\")\n    except SystemExit:\n        print(\"third\")\n",
            "first", 1 },
        new object[] { "systemexit_exception_bare",
            "def main() -> None:\n    try:\n        raise SystemExit(2)\n    except SystemExit:\n        print(\"first\")\n"
            + "    except Exception:\n        print(\"exc\")\n    except:\n        print(\"bare\")\n",
            "first", 1 },
        new object[] { "one_tuple_covered",
            "def main() -> None:\n    try:\n        raise KeyError(\"k\")\n    except LookupError:\n        print(\"lookup\")\n"
            + "    except (KeyError,):\n        print(\"one\")\n",
            "lookup", 1 },
        // socket.timeout / socket.error are OSError-family aliases (#2579): a tuple element covered by an
        // earlier element of the same tuple, and a later handler covered by `except OSError:`, both
        // order-sensitive in C# (CS0160). python3.15.2 prints the same seven lines.
        new object[] { "socket_aliases_under_oserror",
            "import socket\n\n\ndef main() -> None:\n    try:\n        raise socket.timeout(\"t\")\n    except (OSError, socket.timeout):\n        print(\"1 tuple os-first\")\n    try:\n        raise socket.timeout(\"t\")\n    except (socket.timeout, OSError):\n        print(\"2 tuple timeout-first\")\n    try:\n        raise socket.timeout(\"t\")\n    except OSError:\n        print(\"3 os\")\n    except socket.timeout:\n        print(\"3 timeout\")\n    try:\n        raise socket.error(\"e\")\n    except (OSError, socket.error):\n        print(\"4 tuple os-first\")\n    try:\n        raise socket.error(\"e\")\n    except (socket.error, OSError):\n        print(\"5 tuple error-first\")\n    try:\n        raise socket.error(\"e\")\n    except OSError:\n        print(\"6 os\")\n    except socket.error:\n        print(\"6 error\")\n    try:\n        raise OSError(\"o\")\n    except socket.timeout:\n        print(\"10 timeout\")\n    except OSError:\n        print(\"10 os\")\n\n",
            "1 tuple os-first\n2 tuple timeout-first\n3 os\n4 tuple os-first\n5 tuple error-first\n6 os\n10 os", 2 },
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

        // The one cell CPython would run differently (no BaseException in Sharpy) says so; no other does.
        Assert.Equal(name == "bare_after_exception" ? warnings : 0, run.RawDiagnostics.Count(d =>
            d.Code == DiagnosticCodes.Validation.UnreachableArm && d.Message.Contains("never runs in Sharpy", StringComparison.Ordinal)));

        // Each way the warning names the covered handler (DescribeHandler's arms).
        if (MessageFragments.TryGetValue(name, out var fragment))
        {
            Assert.Contains(run.RawDiagnostics, d =>
                d.Code == DiagnosticCodes.Validation.UnreachableArm && d.Message.Contains(fragment, StringComparison.Ordinal));
        }
    }

    private static readonly Dictionary<string, string> MessageFragments = new(StringComparer.Ordinal)
    {
        ["user_hierarchy_base_first"] = "catches 'AppError', which covers 'DbError'",
        ["tuple_wholly_covered"] = "covers every type of this handler",
        ["one_tuple_covered"] = "catches 'LookupError', which covers 'KeyError'",
        ["systemexit_exception_systemexit"] = "catches 'SystemExit', which covers 'SystemExit'",
        ["systemexit_exception_bare"] = "covers this bare 'except:'",
        ["bare_after_exception"] = "This bare 'except:' never runs in Sharpy",
    };

    /// <summary>
    /// A handler that NAMES a type CPython roots at BaseException, after <c>except Exception:</c>, is
    /// refused (SPY0709) on that element: CPython runs it, Sharpy cannot. The message names only the
    /// BaseException-rooted types and the handler that covers them. A handler naming only such types is
    /// told to move, and moved first it runs as CPython does; a tuple with other types is told to handle
    /// SystemExit on its own (see <see cref="MixedTuple_AdvisedSplit_RunsAsCPythonRunsTheOriginal"/>).
    /// </summary>
    [Theory]
    [InlineData("except SystemExit:", "Move this handler before the 'Exception' handler", 12)]
    [InlineData("except (SystemExit,):", "Move this handler before the 'Exception' handler", 13)]
    [InlineData("except (ValueError, SystemExit):", "Handle 'SystemExit' in a handler of its own before the 'Exception' handler", 25)]
    public void NamedBaseExceptionHandlerAfterException_IsRefused(string header, string steer, int column)
    {
        var refused = "def main() -> None:\n    try:\n        raise SystemExit(2)\n    except Exception:\n        print(\"exc\")\n"
            + "    " + header + "\n        print(\"exit\")\n";
        var run = CompileAndExecute(refused, "systemexit_after_exception.spy");
        Assert.False(run.Success);
        var error = Assert.Single(run.RawDiagnostics, d => d.Code == DiagnosticCodes.ValidationOverflow.BaseExceptionHandlerAfterException);
        Assert.Contains(steer, error.Message, StringComparison.Ordinal);
        Assert.Contains("CPython would run this handler for 'SystemExit'", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'ValueError'", error.Message, StringComparison.Ordinal);
        Assert.Equal(6, error.Line);
        Assert.Equal(column, error.Column);
        Assert.DoesNotContain(run.RawDiagnostics, d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);

        if (!steer.StartsWith("Move", StringComparison.Ordinal))
            return;
        var reordered = "def main() -> None:\n    try:\n        raise SystemExit(2)\n    " + header + "\n        print(\"exit\")\n"
            + "    except Exception:\n        print(\"exc\")\n";
        var movedRun = CompileAndExecute(reordered, "systemexit_moved_first.spy");
        Assert.True(RanToCompletion(movedRun), JudgeRun(movedRun, reordered, "SystemExit handler moved first") ?? "timed out");
        Assert.Equal("exit", movedRun.StandardOutput.Trim());
    }

    /// <summary>
    /// The mixed-tuple advice, followed: python3.15.2 runs the ORIGINAL <c>except Exception:</c> /
    /// <c>except (ValueError, SystemExit):</c> as "exc" for ValueError and "tuple" for SystemExit, and the
    /// advised split (SystemExit first, the tuple's ValueError left after Exception) prints the same in
    /// Sharpy. Moving the whole tuple first would print "tuple" twice.
    /// </summary>
    [Fact]
    public void MixedTuple_AdvisedSplit_RunsAsCPythonRunsTheOriginal()
    {
        const string source = "def main() -> None:\n    for i in range(2):\n        try:\n            if i == 0:\n"
            + "                raise ValueError(\"v\")\n            raise SystemExit(2)\n        except SystemExit:\n"
            + "            print(\"tuple\")\n        except Exception:\n            print(\"exc\")\n"
            + "        except ValueError:\n            print(\"tuple\")\n";
        var run = CompileAndExecute(source, "mixed_tuple_split.spy");
        Assert.True(RanToCompletion(run), JudgeRun(run, source, "advised split") ?? "timed out");
        Assert.Equal("exc\ntuple", run.StandardOutput.Trim().Replace("\r\n", "\n"));
        Assert.Single(run.RawDiagnostics, d => d.Code == DiagnosticCodes.Validation.UnreachableArm);
    }

    /// <summary>
    /// SPY0709 names the handler that covers the SystemExit element (<c>Exception</c>), not the cover of
    /// the tuple's first element: <c>except ValueError:</c> / <c>except Exception:</c> /
    /// <c>except (ValueError, SystemExit):</c>.
    /// </summary>
    [Fact]
    public void BaseExceptionRefusal_NamesTheSystemExitElementsCover()
    {
        const string source = "def main() -> None:\n    try:\n        raise SystemExit(2)\n    except ValueError:\n        print(\"v\")\n"
            + "    except Exception:\n        print(\"exc\")\n    except (ValueError, SystemExit):\n        print(\"tuple\")\n";
        var run = CompileAndExecute(source, "systemexit_cover_named.spy");
        var error = Assert.Single(run.RawDiagnostics, d => d.Code == DiagnosticCodes.ValidationOverflow.BaseExceptionHandlerAfterException);
        Assert.Contains("the earlier handler for 'Exception' catches 'SystemExit'", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("handler for 'ValueError'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The verdict marks a SystemExit element that only <c>except Exception:</c> covers even when the
    /// handler as a whole stays reachable (another element has no recorded type), so the validator, which
    /// tests <c>PythonLiveTypes</c> before its reachability skip, refuses it instead of the lowering
    /// silently dropping it. No source spelling leaves an element unrecorded, so the facts are built here.
    /// </summary>
    [Fact]
    public void Verdict_MarksAPythonLiveElement_OfAReachableHandler()
    {
        var exceptionSymbol = new TypeSymbol { Name = "Exception", Kind = SymbolKind.Type, TypeKind = TypeKind.Class, ClrType = typeof(System.Exception) };
        var exitSymbol = new TypeSymbol { Name = "SystemExit", Kind = SymbolKind.Type, TypeKind = TypeKind.Class, ClrType = typeof(SharpyRT::Sharpy.SystemExit) };
        var exceptionAnnotation = new TypeAnnotation { Name = "Exception" };
        var exitElement = new TypeAnnotation { Name = "SystemExit" };
        var unrecorded = new TypeAnnotation { Name = "Unrecorded" };
        var tuple = new TypeAnnotation { Name = "tuple", TypeArguments = ImmutableArray.Create(exitElement, unrecorded) };
        var tryStmt = new TryStatement
        {
            Handlers = ImmutableArray.Create(
                new ExceptHandler { ExceptionType = exceptionAnnotation },
                new ExceptHandler { ExceptionType = tuple }),
        };
        var info = new SemanticInfo();
        info.SetTypeTestType(exceptionAnnotation,
            new UserDefinedType { Name = "Exception", Symbol = exceptionSymbol });
        info.SetTypeTestType(exitElement,
            new UserDefinedType { Name = "SystemExit", Symbol = exitSymbol });

        var verdict = ExceptHandlerReachability.Classify(tryStmt, info)[1];

        Assert.False(verdict.IsUnreachable);
        Assert.Equal(new[] { false, true }, verdict.TypeReachable);
        Assert.Equal("SystemExit", Assert.Single(verdict.PythonLiveTypes).GetDisplayName());
        Assert.Equal(0, verdict.PythonLiveElement);
        Assert.False(verdict.NamesOnlyPythonLiveTypes);
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
