extern alias SharpyRT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance guard: every public instance member on System.Exception that is not
/// redeclared by a Sharpy exception class must be refused by BuiltinExceptionSurface.
/// Enumerated by reflection so it cannot go stale (#1515).
///
/// <para>Two layers, both required: the predicate tests pin the authority's answers over the
/// reflection-enumerated surface, and the compile-based route matrix pins that every
/// resolution route (reads, writes, calls; snake_case and verbatim; concrete,
/// <c>Exception</c>-typed, and undeclared-subclass receivers) actually consults the
/// authority — a route that stops consulting it turns a matrix cell green-to-red even
/// while the predicate tests stay green.</para>
/// </summary>
public class BuiltinExceptionSurfaceConformanceTests
{
    private static string[] GetSystemExceptionPropertyNames()
    {
        return typeof(Exception)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => p.Name)
            .Distinct()
            .ToArray();
    }

    private static string[] GetSystemExceptionMethodNames()
    {
        // The complete public instance surface — including the object-declared methods
        // (Equals, GetHashCode, GetType, ToString): Python spells none of them this way,
        // so they are refused like every other CLR-only member on a builtin exception.
        return typeof(Exception)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .Distinct()
            .ToArray();
    }

    [Fact]
    public void AllSystemExceptionProperties_RefusedOnValueError_SnakeCase()
    {
        var type = typeof(SharpyRT::Sharpy.ValueError);
        foreach (var propName in GetSystemExceptionPropertyNames())
        {
            var sharpyName = NameMangler.ToSharpyName(propName, ReverseNameContext.Property);
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(type, sharpyName),
                $"Expected '{sharpyName}' (from '{propName}') to be refused on ValueError");
        }
    }

    [Fact]
    public void AllSystemExceptionProperties_RefusedOnValueError_Verbatim()
    {
        var type = typeof(SharpyRT::Sharpy.ValueError);
        foreach (var propName in GetSystemExceptionPropertyNames())
        {
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(type, propName),
                $"Expected verbatim '{propName}' to be refused on ValueError");
        }
    }

    [Fact]
    public void AllSystemExceptionMethods_RefusedOnValueError_SnakeCase()
    {
        var type = typeof(SharpyRT::Sharpy.ValueError);
        foreach (var methodName in GetSystemExceptionMethodNames())
        {
            var sharpyName = NameMangler.ToSharpyName(methodName, ReverseNameContext.Method);
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(type, sharpyName),
                $"Expected '{sharpyName}' (from '{methodName}') to be refused on ValueError");
        }
    }

    [Fact]
    public void AllSystemExceptionMethods_RefusedOnValueError_Verbatim()
    {
        var type = typeof(SharpyRT::Sharpy.ValueError);
        foreach (var methodName in GetSystemExceptionMethodNames())
        {
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(type, methodName),
                $"Expected verbatim '{methodName}' to be refused on ValueError");
        }
    }

    [Fact]
    public void AllSystemExceptionProperties_RefusedOnSystemException_SnakeCase()
    {
        foreach (var propName in GetSystemExceptionPropertyNames())
        {
            var sharpyName = NameMangler.ToSharpyName(propName, ReverseNameContext.Property);
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(typeof(Exception), sharpyName),
                $"Expected '{sharpyName}' to be refused on System.Exception");
        }
    }

    [Fact]
    public void AllSystemExceptionProperties_RefusedOnSystemException_Verbatim()
    {
        foreach (var propName in GetSystemExceptionPropertyNames())
        {
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(typeof(Exception), propName),
                $"Expected verbatim '{propName}' to be refused on System.Exception");
        }
    }

    [Fact]
    public void AllSystemExceptionMethods_RefusedOnSystemException_BothSpellings()
    {
        foreach (var methodName in GetSystemExceptionMethodNames())
        {
            var sharpyName = NameMangler.ToSharpyName(methodName, ReverseNameContext.Method);
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(typeof(Exception), sharpyName),
                $"Expected '{sharpyName}' to be refused on System.Exception");
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(typeof(Exception), methodName),
                $"Expected verbatim '{methodName}' to be refused on System.Exception");
        }
    }

    // --- Positive controls: ExceptionGroup's Python surface resolves ---

    [Theory]
    [InlineData("message")]
    [InlineData("Message")]
    [InlineData("exceptions")]
    [InlineData("Exceptions")]
    [InlineData("subgroup")]
    [InlineData("Subgroup")]
    [InlineData("split")]
    [InlineData("Split")]
    [InlineData("derive")]
    [InlineData("Derive")]
    public void ExceptionGroup_PythonSurface_IsAllowed(string memberName)
    {
        Assert.False(
            BuiltinExceptionSurface.IsRefusedMember(typeof(SharpyRT::Sharpy.ExceptionGroup), memberName),
            $"ExceptionGroup's Python member '{memberName}' must NOT be refused");
    }

    // --- Negative control: non-builtin CLR exception is never filtered ---

    [Theory]
    [InlineData("message")]
    [InlineData("Message")]
    [InlineData("stack_trace")]
    [InlineData("StackTrace")]
    public void IOException_NeverRefused(string memberName)
    {
        Assert.False(
            BuiltinExceptionSurface.IsRefusedMember(typeof(System.IO.IOException), memberName),
            $"IOException is not a builtin exception — '{memberName}' must not be refused");
    }

    // --- SystemExit's declared Code property resolves ---

    [Fact]
    public void SystemExit_DeclaredCode_IsAllowed()
    {
        Assert.False(BuiltinExceptionSurface.IsRefusedMember(
            typeof(SharpyRT::Sharpy.SystemExit), "code"));
        Assert.False(BuiltinExceptionSurface.IsRefusedMember(
            typeof(SharpyRT::Sharpy.SystemExit), "Code"));
    }

    // ================================================================================
    // Discovery seam: the TypeSymbol surface — what LSP completion/hover reads — is
    // filtered by the same authority (CachedModuleDiscovery.PopulateTypeSymbolMembers).
    // ================================================================================

    [Fact]
    public void DiscoveredValueErrorTypeSymbol_CarriesNoClrOnlyMembers()
    {
        var symbol = Assert.IsType<Sharpy.Compiler.Semantic.TypeSymbol>(
            new Sharpy.Compiler.Semantic.Registry.BuiltinRegistry().GetType("ValueError"));

        var propertyNames = symbol.Properties.Select(p => p.Name).ToList();
        var methodNames = symbol.Methods.Select(m => m.Name).ToList();

        Assert.DoesNotContain("message", propertyNames);
        Assert.DoesNotContain("stack_trace", propertyNames);
        Assert.DoesNotContain("inner_exception", propertyNames);
        Assert.DoesNotContain("get_base_exception", methodNames);
    }

    [Fact]
    public void DiscoveredExceptionGroupTypeSymbol_KeepsItsDeclaredPythonSurface()
    {
        // The positive control that keeps the seam test above non-vacuous: an EMPTY surface
        // would also satisfy DoesNotContain.
        var symbol = Assert.IsType<Sharpy.Compiler.Semantic.TypeSymbol>(
            new Sharpy.Compiler.Semantic.Registry.BuiltinRegistry().GetType("ExceptionGroup"));

        var propertyNames = symbol.Properties.Select(p => p.Name).ToList();
        var methodNames = symbol.Methods.Select(m => m.Name).ToList();

        Assert.Contains("message", propertyNames);
        Assert.Contains("exceptions", propertyNames);
        Assert.Contains("subgroup", methodNames);
        Assert.Contains("split", methodNames);
        Assert.Contains("derive", methodNames);
        Assert.DoesNotContain("inner_exceptions", propertyNames);
        Assert.DoesNotContain("flatten", methodNames);
    }

    // ================================================================================
    // Route matrix: every resolution route consults the authority (compile-based).
    // ================================================================================

    private static CompilerApi CompilerApiForRoutes()
    {
        var binDir = Path.GetDirectoryName(typeof(BuiltinExceptionSurfaceConformanceTests).Assembly.Location)!;
        return new CompilerApi(NullLogger.Instance, new[]
        {
            Path.Combine(binDir, "Sharpy.Core.dll"),
            Path.Combine(binDir, "Sharpy.Stdlib.dll"),
        });
    }

    private static List<CompilerDiagnostic> CompileErrors(string source)
    {
        var result = CompilerApiForRoutes().Compile(source, new CompilerOptions { OutputType = "library" });
        return result.Diagnostics
            .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
            .ToList();
    }

    public static IEnumerable<object[]> RefusalRouteCells()
    {
        // (cellKey, source, refusedMemberSpelling)
        yield return new object[]
        {
            "concrete_snake_property_read",
            "def probe() -> None:\n    try:\n        raise ValueError(\"boom\")\n    except ValueError as e:\n        print(e.message)\n",
            "message",
        };
        yield return new object[]
        {
            "concrete_verbatim_property_read",
            "def probe() -> None:\n    try:\n        raise ValueError(\"boom\")\n    except ValueError as e:\n        print(e.StackTrace)\n",
            "StackTrace",
        };
        yield return new object[]
        {
            "concrete_snake_method_call",
            "def probe() -> None:\n    try:\n        raise ValueError(\"boom\")\n    except ValueError as e:\n        e.get_base_exception()\n",
            "get_base_exception",
        };
        yield return new object[]
        {
            "concrete_verbatim_method_call",
            "def probe() -> None:\n    try:\n        raise ValueError(\"boom\")\n    except ValueError as e:\n        e.GetBaseException()\n",
            "GetBaseException",
        };
        yield return new object[]
        {
            "exception_typed_receiver_read",
            "def probe() -> None:\n    try:\n        raise ValueError(\"boom\")\n    except Exception as ex:\n        print(ex.message)\n",
            "message",
        };
        yield return new object[]
        {
            "exception_typed_verbatim_read",
            "def probe() -> None:\n    try:\n        raise ValueError(\"boom\")\n    except Exception as ex:\n        print(ex.StackTrace)\n",
            "StackTrace",
        };
        yield return new object[]
        {
            "exception_typed_method_call",
            "def probe() -> None:\n    try:\n        raise ValueError(\"boom\")\n    except Exception as ex:\n        ex.get_base_exception()\n",
            "get_base_exception",
        };
        // Raise-free shape: raising a user subclass of a BUILTIN exception is itself broken
        // (#1596, pre-existing — the derivation walk dead-ends at CLR-discovered symbols);
        // a plain typed local still exercises the undeclared-subclass receiver route.
        yield return new object[]
        {
            "subclass_without_declaration_read",
            "class MyError(TypeError):\n    def __init__(self, msg: str):\n        super().__init__(msg)\n\ndef probe() -> None:\n    e: MyError = MyError(\"boom\")\n    print(e.stack_trace)\n",
            "stack_trace",
        };
        yield return new object[]
        {
            "member_write",
            "def probe() -> None:\n    try:\n        raise ValueError(\"boom\")\n    except ValueError as e:\n        e.source = \"x\"\n",
            "source",
        };
    }

    [Theory]
    [MemberData(nameof(RefusalRouteCells))]
    public void RefusalRoute_EmitsSpy0215_AndNothingElse(string cellKey, string source, string member)
    {
        var errors = CompileErrors(source);

        var refusals = errors
            .Where(d => d.Code == DiagnosticCodes.Semantic.BuiltinExceptionClrMemberRefused)
            .ToList();
        Assert.True(refusals.Count > 0,
            $"{cellKey}: expected SPY0215 for '{member}', got: " +
            string.Join(" ;; ", errors.Select(d => $"{d.Code}: {d.Message}")));
        Assert.Contains(refusals, d => d.Message.Contains($"'{member}'", StringComparison.Ordinal));

        // The refusal displaces the generic member-resolution failure — a cell that ALSO
        // emits SPY0203 (or anything else) is double-reporting the same access.
        var others = errors
            .Where(d => d.Code != DiagnosticCodes.Semantic.BuiltinExceptionClrMemberRefused)
            .ToList();
        Assert.True(others.Count == 0,
            $"{cellKey}: unexpected extra diagnostics alongside the refusal: " +
            string.Join(" ;; ", others.Select(d => $"{d.Code}: {d.Message}")));
    }

    /// <summary>
    /// Refused CLR spellings must leave the did-you-mean pool: a typo'd member on a builtin
    /// exception may fail however it fails, but the suggestion must not steer the user INTO
    /// the refused surface ('HResult'/'h_result').
    /// </summary>
    [Fact]
    public void SuggestionPool_OmitsRefusedClrSpellings()
    {
        var errors = CompileErrors(
            "def probe() -> None:\n    try:\n        raise ValueError(\"boom\")\n    except ValueError as e:\n        print(e.hresult)\n");

        // Positive arm first — the cell is vacuous unless the access actually fails.
        Assert.True(errors.Count > 0, "expected 'e.hresult' to fail on ValueError");
        Assert.Contains(errors, d => d.Message.Contains("hresult", StringComparison.Ordinal));

        Assert.DoesNotContain(errors, d => d.Message.Contains("HResult", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, d => d.Message.Contains("h_result", StringComparison.Ordinal));
    }

    [Fact]
    public void PositiveRoute_ExceptionGroupSurface_CompilesClean()
    {
        var errors = CompileErrors(
            "def probe() -> None:\n    errors: list[Exception] = [ValueError(\"v1\")]\n    try:\n        raise ExceptionGroup(\"group\", errors)\n    except ExceptionGroup as eg:\n        print(eg.message)\n        _x = eg.exceptions\n");
        Assert.True(errors.Count == 0,
            "ExceptionGroup's declared Python surface must resolve: " +
            string.Join(" ;; ", errors.Select(d => $"{d.Code}: {d.Message}")));
    }

    [Fact]
    public void PositiveRoute_UserSubclassDeclaringMessage_CompilesClean()
    {
        var errors = CompileErrors(
            "class Alert(Exception):\n    message: str\n    def __init__(self, message: str):\n        super().__init__(message)\n        self.message = message\n\ndef probe() -> None:\n    try:\n        raise Alert(\"fire\")\n    except Alert as e:\n        print(e.message)\n");
        Assert.True(errors.Count == 0,
            "a user subclass declaring its own 'message' keeps it: " +
            string.Join(" ;; ", errors.Select(d => $"{d.Code}: {d.Message}")));
    }
}
