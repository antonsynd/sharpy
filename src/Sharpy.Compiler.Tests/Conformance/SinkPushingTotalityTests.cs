using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Totality guard for the emitter's sink-pushing sites (#1680, #1739):
/// every AST kind that introduces conditional, repeated, deferred or scoped evaluation
/// is rostered, and the emitter's <c>WithSink(</c> call sites are scanned against the roster.
/// A new such kind or a removed push fails. The definition of <c>WithSink</c> itself and the
/// <c>GenerateExpressionsInOrder</c> helper (which calls <c>WithSink</c> for ordering capture)
/// are excluded from the roster of context-manufacturing sites.
/// </summary>
public class SinkPushingTotalityTests
{
    private readonly ITestOutputHelper _output;

    public SinkPushingTotalityTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Every method in the emitter that calls <c>WithSink(</c> to manufacture a context-specific
    /// hoist sink. Keyed by the method name, with the evaluation context it covers.
    /// </summary>
    private static readonly Dictionary<string, string> SinkPushingMethods = new()
    {
        // Conditional: the rhs is evaluated only when the lhs does not short-circuit
        ["GenerateShortCircuitOp"] = "and/or rhs — conditional evaluation",

        // Conditional: then/else branches evaluated based on condition
        ["GenerateConditionalExpression"] = "ternary then/else — conditional evaluation",

        // Conditional/repeated: elif test evaluated only when prior tests fail
        ["GenerateIf"] = "elif test — conditional evaluation",

        // Repeated: while test re-evaluated each iteration
        ["GenerateWhile"] = "while test — repeated evaluation",

        // Deferred: lambda body evaluated when called, not when defined
        ["GenerateLambdaExpression"] = "lambda body — deferred evaluation",

        // Deferred: typed lambda body evaluated when called, not when defined
        ["GenerateTypedLambdaExpression"] = "typed lambda body — deferred evaluation",

        // Scoped: comprehension body runs in its own scope
        ["CaptureHoisted"] = "comprehension — scoped evaluation",

        // Conditional: assert message evaluated only on failure
        ["GenerateAssert"] = "assert message — conditional evaluation",

        // Context: with-as target store may hoist
        ["GenerateWithTargetStore"] = "with-as target — scoped evaluation",

        // Ordering: captures earlier operands when later ones produce hoists
        ["GenerateExpressionsInOrder"] = "ordering axis — left-to-right capture",
    };

    /// <summary>
    /// Methods that define or delegate <c>WithSink</c> but do not manufacture a context sink.
    /// These are excluded from the roster check.
    /// </summary>
    private static readonly HashSet<string> InfrastructureMethods = new()
    {
        "WithSink",
    };

    [Fact]
    [Trait("Category", "Conformance")]
    public void AllWithSinkCallSites_AreRostered()
    {
        var emitterFiles = FindEmitterSourceFiles();
        var methodsWithSinkCalls = new HashSet<string>();

        foreach (var file in emitterFiles)
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var name = GetInvokedMethodName(invocation);
                if (name != "WithSink")
                    continue;

                var enclosingMethod = invocation
                    .Ancestors()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault();

                if (enclosingMethod == null)
                    continue;

                var methodName = enclosingMethod.Identifier.Text;
                if (!InfrastructureMethods.Contains(methodName))
                    methodsWithSinkCalls.Add(methodName);
            }
        }

        _output.WriteLine($"WithSink call sites found in {methodsWithSinkCalls.Count} methods:");
        foreach (var method in methodsWithSinkCalls.OrderBy(m => m, StringComparer.Ordinal))
        {
            var status = SinkPushingMethods.ContainsKey(method)
                ? $"ROSTERED ({SinkPushingMethods[method]})"
                : "*** UNROSTERED ***";
            _output.WriteLine($"  {method,-40} {status}");
        }

        var unrostered = methodsWithSinkCalls
            .Where(m => !SinkPushingMethods.ContainsKey(m))
            .OrderBy(m => m, StringComparer.Ordinal)
            .ToList();

        var phantom = SinkPushingMethods.Keys
            .Where(m => !methodsWithSinkCalls.Contains(m))
            .OrderBy(m => m, StringComparer.Ordinal)
            .ToList();

        if (unrostered.Count > 0)
            _output.WriteLine($"\nUnrostered (new WithSink sites not in the roster): {string.Join(", ", unrostered)}");
        if (phantom.Count > 0)
            _output.WriteLine($"\nPhantom (rostered but no longer calling WithSink): {string.Join(", ", phantom)}");

        Assert.True(unrostered.Count == 0,
            $"Sink-pushing totality (#1680): {unrostered.Count} method(s) call WithSink but " +
            $"are not rostered — add them to SinkPushingMethods.\n  " +
            string.Join("\n  ", unrostered));

        Assert.True(phantom.Count == 0,
            $"Sink-pushing totality (#1680): {phantom.Count} rostered method(s) no longer call " +
            $"WithSink — remove them from SinkPushingMethods or investigate.\n  " +
            string.Join("\n  ", phantom));
    }

    private static string? GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            _ => null
        };
    }

    private static IReadOnlyList<string> FindEmitterSourceFiles()
    {
        var repoRoot = FindRepoRoot();
        var codegenDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler", "CodeGen");
        if (!Directory.Exists(codegenDir))
            throw new DirectoryNotFoundException($"CodeGen directory not found at '{codegenDir}'.");

        return Directory.GetFiles(codegenDir, "RoslynEmitter*.cs")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git"))
                || File.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException(
            $"Could not find repository root starting from '{AppContext.BaseDirectory}'.");
    }
}
