using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Helpers;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class ErrorInjector
{
    /// <summary>
    /// An injected program and the ONE code it must draw (#2288). The code is exact, never a
    /// prefix: a refusal for an unrelated reason is not a catch, and a mutation that also breaks
    /// something else draws a second code — a bad injection, not a pass. <see cref="KnownRed"/> pins
    /// an injection the compiler does not refuse today; <see cref="Judge"/> then requires the program
    /// to still analyse clean, so the first refusal fails the row and the pin drains with its issue.
    /// <see cref="ExpectedDiagnosticCode"/> is null only on a pin whose refusal has no code yet.
    /// <see cref="Severity"/> is the severity the code is drawn at — <c>Error</c> unless the one
    /// broken fact is a warning (SPY0463); see <see cref="JudgeSource"/>.
    /// </summary>
    public record InjectionResult(
        Module Mutated,
        string? ExpectedDiagnosticCode,
        KnownRedRefusal? KnownRed = null,
        CompilerDiagnosticSeverity Severity = CompilerDiagnosticSeverity.Error);

    /// <summary>A refusal the compiler does not make today: <paramref name="Issue"/> tracks it, and
    /// <paramref name="Observed"/> says what happens instead.</summary>
    public sealed record KnownRedRefusal(string Issue, string Observed);

    /// <summary>
    /// Unparses the injected program and judges it through <see cref="JudgeSource"/> at the
    /// injection's <see cref="InjectionResult.Severity"/> — this overload adds nothing but the
    /// unparse, so the AST and source-text lanes share one verdict.
    /// </summary>
    public static string? Judge(InjectionResult injection, string label)
    {
        var source = Sharpy.Compiler.Pretty.Unparser.Unparse(injection.Mutated);
        return JudgeSource(source, injection.ExpectedDiagnosticCode, injection.Severity, injection.KnownRed, label);
    }

    /// <summary>
    /// The ONE refusal verdict for every injected or source-text generator lane (#2288). Analyses
    /// <paramref name="source"/>: null when it drew exactly what <paramref name="severity"/> asks,
    /// otherwise why not. A throw is an internal error, never a refusal; a failed analysis with no
    /// error code is a failure.
    /// <list type="bullet">
    /// <item><c>Error</c>: the distinct error codes are exactly [<paramref name="expectedCode"/>]
    /// (warnings are not read). Pinned: no error at all.</item>
    /// <item><c>Warning</c>: exactly ONE warning DIAGNOSTIC, carrying <paramref name="expectedCode"/>, AND no
    /// error — a warning lane whose program is also refused proves nothing about the warning, and two
    /// warnings of the code are two broken facts, not the one the injection made (P63 Design Decision
    /// 6: "exactly one diagnostic of the expected severity"). Pinned: no error, and no warning of the
    /// code (no warning at all when the code is null).</item>
    /// </list>
    /// Expected codes are literal strings at the call site, never <c>DiagnosticCodes</c> constants —
    /// a renumbering mutation must stay live.
    /// </summary>
    public static string? JudgeSource(
        string source,
        string? expectedCode,
        CompilerDiagnosticSeverity severity,
        KnownRedRefusal? pin,
        string label) =>
        JudgeDiagnostics(() => AnalyseSingleFile(source), source, expectedCode, severity, pin, label);

    /// <summary>
    /// The multi-file front-end of the same verdict: compiles <paramref name="files"/> (relative
    /// path, content) as one project through <see cref="ProjectCompilationHelper"/> — the route the
    /// import lanes have always used — and judges its diagnostics exactly as
    /// <see cref="JudgeSource"/> judges one file's. Only the analysis differs; the verdict is
    /// <see cref="JudgeDiagnostics"/> for both.
    /// </summary>
    public static string? JudgeProject(
        IReadOnlyList<(string Path, string Content)> files,
        string? expectedCode,
        CompilerDiagnosticSeverity severity,
        KnownRedRefusal? pin,
        string label) =>
        JudgeDiagnostics(() => AnalyseProject(files), DescribeProject(files), expectedCode, severity, pin, label);

    /// <summary>
    /// The ONE verdict core behind <see cref="JudgeSource"/> and <see cref="JudgeProject"/>:
    /// runs <paramref name="analyse"/> (a throw is an internal error, never a refusal; a failed
    /// analysis with no error code is a failure) and judges the diagnostics it returns by the rules
    /// documented on <see cref="JudgeSource"/>. <paramref name="sourceForMessage"/> is printed with
    /// every failure.
    /// </summary>
    private static string? JudgeDiagnostics(
        Func<(bool Success, List<CompilerDiagnostic> All)> analyse,
        string sourceForMessage,
        string? expectedCode,
        CompilerDiagnosticSeverity severity,
        KnownRedRefusal? pin,
        string label)
    {
        if (severity is not (CompilerDiagnosticSeverity.Error or CompilerDiagnosticSeverity.Warning))
            throw new ArgumentOutOfRangeException(nameof(severity), severity,
                "A refusal lane is judged at Error or Warning severity");

        var source = sourceForMessage;
        List<string> errors;
        List<string> warnings;
        int warningDiagnostics;
        try
        {
            var (success, all) = analyse();
            errors = DistinctCodes(all.Where(d => d.IsError));
            warnings = DistinctCodes(all.Where(d => d.IsWarning));
            warningDiagnostics = all.Count(d => d.IsWarning);
            if (!success && errors.Count == 0)
                return $"Injected {label} failed analysis without an error code:\n{source}";
        }
        catch (Exception e)
        {
            return $"Injected {label} threw {e.GetType().Name} — an internal error is not a refusal:\n{source}\n{e}";
        }

        var isWarning = severity == CompilerDiagnosticSeverity.Warning;
        // The arm's own codes: errors on the error arm, warnings on the warning arm.
        var drew = Describe(isWarning ? warnings : errors);
        var drewAll = isWarning
            ? $"errors [{Describe(errors)}], warnings [{drew}] ({warningDiagnostics} warning diagnostic(s))"
            : $"[{drew}]";

        if (pin is not null)
        {
            var next = expectedCode is { } code
                ? $"expect {code}"
                : "give the injection the code the fix allocated";
            var stillUnrefused = errors.Count == 0
                && (!isWarning || (expectedCode is null ? warnings.Count == 0 : !warnings.Contains(expectedCode)));
            return stillUnrefused
                ? null
                : $"Injected {label} is now refused {drewAll} — if that is {pin.Issue}'s fix, drop the KnownRed pin "
                  + $"and {next}; otherwise the injection breaks something else:\n{source}";
        }

        var expected = expectedCode
            ?? throw new InvalidOperationException($"Injection {label} has neither an expected code nor a KnownRed pin");
        if (isWarning)
        {
            return warningDiagnostics == 1 && warnings is [var onlyWarning] && onlyWarning == expected && errors.Count == 0
                ? null
                : $"Injected {label} must draw exactly one warning, {expected}, and no error, drew {drewAll}:\n{source}";
        }

        return errors is [var only] && only == expected
            ? null
            : $"Injected {label} must draw exactly {expected}, drew {drewAll}:\n{source}";
    }

    /// <summary>
    /// The accepted-program verdict beside <see cref="JudgeSource"/>: null when
    /// <paramref name="source"/> analyses successfully with no error (warnings are not read),
    /// otherwise why not — a throw is an internal error, never an acceptance. The VALID half of a
    /// refusal lane judges every sample with this, so "the complete program is accepted" is measured
    /// per sample, never summed with the refusal half (#2288).
    /// </summary>
    public static string? JudgeClean(string source, string label)
    {
        try
        {
            var (success, all) = AnalyseSingleFile(source);
            var errors = DistinctCodes(all.Where(d => d.IsError));
            if (errors.Count > 0)
                return $"Valid {label} must analyse clean, drew [{Describe(errors)}]:\n{source}";
            return success
                ? null
                : $"Valid {label} failed analysis without an error code:\n{source}";
        }
        catch (Exception e)
        {
            return $"Valid {label} threw {e.GetType().Name} — an internal error is not an acceptance:\n{source}\n{e}";
        }
    }

    /// <summary>
    /// The no-crash verdict for a RAW sample — the one a <c>*NeverThrows*</c> lane reads (#2288):
    /// null when analysing <paramref name="source"/> neither throws nor records an internal compiler
    /// error (SPY0909, the code the compiler's last-chance handlers turn an escaped exception into),
    /// otherwise why not. A refusal is NOT a failure here — the sample is unfiltered — but no throw is
    /// ever acceptable: before, the lanes caught only <c>InternalCompilerErrorException</c> (which
    /// <c>Analyze</c> never lets escape: it records SPY0909) and swallowed every other exception, so
    /// they could not fail.
    /// </summary>
    public static string? JudgeNoInternalError(string source, string label)
    {
        try
        {
            var (_, all) = AnalyseSingleFile(source);
            var internalErrors = all.Where(d => d.Code == InternalCompilerErrorCode).ToList();
            return internalErrors.Count == 0
                ? null
                : $"Raw {label} drew an internal compiler error ({InternalCompilerErrorCode}): "
                  + $"{internalErrors[0].Message}\n{source}";
        }
        catch (Exception e)
        {
            return $"Raw {label} threw {e.GetType().Name} — the compiler must never throw:\n{source}\n{e}";
        }
    }

    /// <summary><c>DiagnosticCodes.Infrastructure.InternalCompilerError</c>, spelled literally (a
    /// renumbering must stay a live mutation).</summary>
    private const string InternalCompilerErrorCode = "SPY0909";

    /// <summary>What <see cref="JudgeSamples"/> saw: <see cref="Tested"/> samples judged,
    /// <see cref="Failures"/> non-null verdicts, <see cref="First"/> the first of them.</summary>
    public sealed record SampleVerdicts(int Tested, int Failures, string? First);

    /// <summary>
    /// Samples <paramref name="gen"/> <paramref name="iter"/> times, unparses each module and asks
    /// <paramref name="judge"/> (<see cref="JudgeSource"/> or <see cref="JudgeClean"/>) for its verdict.
    /// It counts, it never decides: the caller asserts <c>Tested &gt; 0</c> and <c>First is null</c>.
    /// </summary>
    public static SampleVerdicts JudgeSamples(Gen<Module> gen, Func<string, string?> judge, int iter) =>
        JudgeSamples(gen, module => judge(Sharpy.Compiler.Pretty.Unparser.Unparse(module)), iter,
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m));

    /// <summary>The same count over any sample type — source tuples of a multi-file lane judged by
    /// <see cref="JudgeProject"/>.</summary>
    public static SampleVerdicts JudgeSamples<T>(
        Gen<T> gen, Func<T, string?> judge, int iter, Func<T, string>? print = null)
    {
        int tested = 0;
        int failures = 0;
        string? first = null;
        gen.Sample(sample =>
        {
            Interlocked.Increment(ref tested);
            if (judge(sample) is { } failure)
            {
                Interlocked.Increment(ref failures);
                Interlocked.CompareExchange(ref first, failure, null);
            }
        }, print: print, iter: iter);
        return new SampleVerdicts(tested, failures, first);
    }

    /// <summary>The one in-process analysis both verdicts read: the property host's
    /// <c>Compiler().Analyze</c> (no stdlib references — a lane's expected code is measured here).</summary>
    private static (bool Success, List<CompilerDiagnostic> All) AnalyseSingleFile(string source)
    {
        var analysis = new Sharpy.Compiler.Compiler().Analyze(source, "injection.spy");
        return (analysis.Success, analysis.Diagnostics.GetAll().ToList());
    }

    /// <summary>The multi-file analysis: one project, root namespace <c>Test</c>, compiled by
    /// <see cref="ProjectCompilationHelper.Compile()"/> (the import lanes' route).</summary>
    private static (bool Success, List<CompilerDiagnostic> All) AnalyseProject(
        IReadOnlyList<(string Path, string Content)> files)
    {
        using var helper = new ProjectCompilationHelper();
        helper.WithRootNamespace("Test");
        foreach (var (path, content) in files)
            helper.AddSourceFile(path, content);
        helper.CreateProjectFile();
        var result = helper.Compile();
        return (result.Success, result.Diagnostics.GetAll().ToList());
    }

    private static string DescribeProject(IReadOnlyList<(string Path, string Content)> files) =>
        string.Join("\n", files.Select(f => $"# --- {f.Path}\n{f.Content}"));

    private static List<string> DistinctCodes(IEnumerable<CompilerDiagnostic> diagnostics) =>
        diagnostics
            .Select(d => d.Code ?? "(no code)")
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    private static string Describe(List<string> codes) =>
        codes.Count == 0 ? "nothing" : string.Join(", ", codes);

    public static InjectionResult? InjectTypeMismatchAssignment(Module module)
    {
        var body = GetMainBody(module);
        if (body == null)
            return null;

        for (int i = 0; i < body.Value.Length; i++)
        {
            if (body.Value[i] is VariableDeclaration { Type: { } typeAnn } decl
                && typeAnn.Name == "int")
            {
                var mutated = ReplaceStatement(module, body.Value, i,
                    decl with { InitialValue = new StringLiteral { Value = "wrong" } });
                return new InjectionResult(mutated, "SPY0220");
            }

            if (body.Value[i] is VariableDeclaration { Type: { } typeAnn2 } decl2
                && typeAnn2.Name == "str")
            {
                var mutated = ReplaceStatement(module, body.Value, i,
                    decl2 with { InitialValue = new IntegerLiteral { Value = "42" } });
                return new InjectionResult(mutated, "SPY0220");
            }
        }

        return null;
    }

    public static InjectionResult? InjectUndefinedVariable(Module module)
    {
        var body = GetMainBody(module);
        if (body == null)
            return null;

        for (int i = body.Value.Length - 1; i >= 0; i--)
        {
            if (body.Value[i] is ExpressionStatement { Expression: FunctionCall call }
                && FindIdentifier(call) is { } ident)
            {
                var replaced = ReplaceIdentifier(call, ident.Name, "undefined_xyz_var");
                var mutated = ReplaceStatement(module, body.Value, i,
                    new ExpressionStatement { Expression = (Expression)replaced });
                return new InjectionResult(mutated, "SPY0200");
            }
        }

        return null;
    }

    public static InjectionResult? InjectWrongArgumentType(Module module)
    {
        var body = GetMainBody(module);
        if (body == null)
            return null;

        for (int i = body.Value.Length - 1; i >= 0; i--)
        {
            if (body.Value[i] is ExpressionStatement { Expression: { } expr })
            {
                var mutated = ReplaceLenArg(expr, new IntegerLiteral { Value = "42" });
                if (mutated != null)
                {
                    var result = ReplaceStatement(module, body.Value, i,
                        new ExpressionStatement { Expression = mutated });
                    return new InjectionResult(result, "SPY0320");
                }
            }
        }

        return null;
    }

    public static InjectionResult? InjectMissingArgument(Module module)
    {
        var body = GetMainBody(module);
        if (body == null)
            return null;

        for (int i = body.Value.Length - 1; i >= 0; i--)
        {
            if (body.Value[i] is ExpressionStatement { Expression: { } expr })
            {
                var mutated = StripLenArgs(expr);
                if (mutated != null)
                {
                    var result = ReplaceStatement(module, body.Value, i,
                        new ExpressionStatement { Expression = mutated });
                    return new InjectionResult(result, "SPY0224");
                }
            }
        }

        return null;
    }

    private static Expression? ReplaceLenArg(Expression expr, Expression replacement) => expr switch
    {
        FunctionCall { Function: Identifier { Name: "len" }, Arguments.Length: > 0 } call =>
            call with { Arguments = ImmutableArray.Create(replacement) },
        FunctionCall call => call.Arguments.Length > 0
            ? ReplaceLenArg(call.Arguments[0], replacement) is { } a
                ? call with { Arguments = call.Arguments.SetItem(0, a) }
                : null
            : null,
        BinaryOp bin => ReplaceLenArg(bin.Left, replacement) is { } l
            ? bin with { Left = l }
            : ReplaceLenArg(bin.Right, replacement) is { } r
                ? bin with { Right = r }
                : null,
        _ => null
    };

    private static Expression? StripLenArgs(Expression expr) => expr switch
    {
        FunctionCall { Function: Identifier { Name: "len" }, Arguments.Length: > 0 } call =>
            call with { Arguments = ImmutableArray<Expression>.Empty },
        FunctionCall call => call.Arguments.Length > 0
            ? StripLenArgs(call.Arguments[0]) is { } a
                ? call with { Arguments = call.Arguments.SetItem(0, a) }
                : null
            : null,
        BinaryOp bin => StripLenArgs(bin.Left) is { } l
            ? bin with { Left = l }
            : StripLenArgs(bin.Right) is { } r
                ? bin with { Right = r }
                : null,
        _ => null
    };

    public static InjectionResult? InjectWrongFunctionArgCount(Module module)
    {
        var body = GetMainBody(module);
        if (body == null)
            return null;

        for (int i = body.Value.Length - 1; i >= 0; i--)
        {
            if (body.Value[i] is ExpressionStatement { Expression: { } expr }
                && FindUserFunctionCall(expr) is { } target
                && target.Arguments.Length > 0)
            {
                // The LAST argument, so the others keep their parameters: dropping the first shifted
                // a `bool` into an `int` slot and drew SPY0220 beside SPY0224 (#2288).
                var broken = target with { Arguments = target.Arguments.RemoveAt(target.Arguments.Length - 1) };
                var newExpr = ReplaceNestedCall(expr, target, broken);
                var mutated = ReplaceStatement(module, body.Value, i,
                    new ExpressionStatement { Expression = newExpr });
                return new InjectionResult(mutated, "SPY0224");
            }
        }

        return null;
    }

    public static InjectionResult? InjectWrongFunctionArgType(Module module)
    {
        var body = GetMainBody(module);
        if (body == null)
            return null;

        for (int i = body.Value.Length - 1; i >= 0; i--)
        {
            if (body.Value[i] is ExpressionStatement { Expression: { } expr }
                && FindUserFunctionCall(expr) is { } target
                && target.Arguments.Length > 0)
            {
                // Replace with a list literal — incompatible with int, str, and bool params
                Expression replacement = new ListLiteral
                {
                    Elements = ImmutableArray.Create<Expression>(new IntegerLiteral { Value = "1" })
                };

                var broken = target with { Arguments = target.Arguments.SetItem(0, replacement) };
                var newExpr = ReplaceNestedCall(expr, target, broken);
                var mutated = ReplaceStatement(module, body.Value, i,
                    new ExpressionStatement { Expression = newExpr });
                return new InjectionResult(mutated, "SPY0220");
            }
        }

        return null;
    }

    public static InjectionResult? InjectUndefinedFunctionCall(Module module)
    {
        var body = GetMainBody(module);
        if (body == null)
            return null;

        for (int i = body.Value.Length - 1; i >= 0; i--)
        {
            if (body.Value[i] is ExpressionStatement { Expression: { } expr }
                && FindUserFunctionCall(expr) is { } target)
            {
                var broken = target with { Function = new Identifier { Name = "nonexistent_func_xyz" } };
                var newExpr = ReplaceNestedCall(expr, target, broken);
                var mutated = ReplaceStatement(module, body.Value, i,
                    new ExpressionStatement { Expression = newExpr });
                return new InjectionResult(mutated, "SPY0200");
            }
        }

        return null;
    }

    private static readonly HashSet<string> BuiltinFuncNames = new()
    {
        "print", "len", "range", "abs", "int", "str", "bool", "float", "list", "set", "dict", "type"
    };

    private static FunctionCall? FindUserFunctionCall(Expression expr) => expr switch
    {
        FunctionCall { Function: Identifier { Name: var name } } call
            when !BuiltinFuncNames.Contains(name) && call.Arguments.Length > 0 => call,
        FunctionCall call => call.Arguments
            .Select(FindUserFunctionCall)
            .FirstOrDefault(r => r != null),
        BinaryOp bin => FindUserFunctionCall(bin.Left) ?? FindUserFunctionCall(bin.Right),
        _ => null
    };

    private static Expression ReplaceNestedCall(Expression expr, FunctionCall target, FunctionCall replacement)
    {
        if (expr is FunctionCall fc && ReferenceEquals(fc, target))
            return replacement;

        return expr switch
        {
            FunctionCall call => call with
            {
                Arguments = call.Arguments.Select(a => ReplaceNestedCall(a, target, replacement)).ToImmutableArray()
            },
            BinaryOp bin => bin with
            {
                Left = ReplaceNestedCall(bin.Left, target, replacement),
                Right = ReplaceNestedCall(bin.Right, target, replacement)
            },
            _ => expr
        };
    }

    public static InjectionResult? InjectOverrideSignatureMismatch(Module module)
    {
        for (int i = 0; i < module.Body.Length; i++)
        {
            if (module.Body[i] is ClassDef classDef && classDef.BaseClasses.Length > 0)
            {
                for (int j = 0; j < classDef.Body.Length; j++)
                {
                    if (classDef.Body[j] is FunctionDef method
                        && method.Name != "__init__"
                        && method.Decorators.Any(d => d.Name == "override")
                        && method.ReturnType != null)
                    {
                        var wrongReturnType = method.ReturnType.Name == "int"
                            ? new TypeAnnotation { Name = "str" }
                            : new TypeAnnotation { Name = "int" };

                        // The body returns the new type, so the override's signature is the one
                        // broken fact: keeping `return "result"` under `-> int` drew SPY0260 from the
                        // body and never reached an override check (#2288).
                        var brokenMethod = method with
                        {
                            ReturnType = wrongReturnType,
                            Body = ImmutableArray.Create<Statement>(
                                new ReturnStatement { Value = GenTyped.DefaultValue(wrongReturnType.Name) })
                        };
                        var newClassBody = classDef.Body.SetItem(j, brokenMethod);
                        var newClass = classDef with { Body = newClassBody };
                        var newModuleBody = module.Body.SetItem(i, newClass);
                        return new InjectionResult(
                            module with { Body = newModuleBody }, "SPY0282",
                            KnownRed: new KnownRedRefusal("#2143",
                                "analyses clean; `run` is SPY0908 CS0508 — no override signature check for a Sharpy base"));
                    }
                }
            }
        }

        return null;
    }

    public static InjectionResult? InjectMissingAbstractImplementation(Module module)
    {
        for (int i = 0; i < module.Body.Length; i++)
        {
            if (module.Body[i] is ClassDef baseDef && baseDef.BaseClasses.Length == 0)
            {
                for (int j = 0; j < baseDef.Body.Length; j++)
                {
                    if (baseDef.Body[j] is FunctionDef method
                        && method.Name != "__init__"
                        && method.Decorators.Any(d => d.Name == "virtual"))
                    {
                        var abstractDecorators = method.Decorators
                            .Select(d => d.Name == "virtual"
                                ? new Decorator { QualifiedParts = ImmutableArray.Create("abstract") }
                                : d)
                            .ToImmutableArray();
                        var abstractMethod = method with
                        {
                            Decorators = abstractDecorators,
                            Body = ImmutableArray.Create<Statement>(
                                new ExpressionStatement
                                {
                                    Expression = new EllipsisLiteral()
                                })
                        };
                        // The class is @abstract too, so the derived class's missing member is the
                        // one broken fact: an @abstract method in a plain class drew SPY0493 (#1307)
                        // for the BASE, and no sample ever asked about the derived class (#2288).
                        var newBaseBody = baseDef.Body.SetItem(j, abstractMethod);
                        var newBase = baseDef with
                        {
                            Body = newBaseBody,
                            Decorators = baseDef.Decorators.Add(
                                new Decorator { QualifiedParts = ImmutableArray.Create("abstract") })
                        };
                        var newModuleBody = module.Body.SetItem(i, newBase);

                        var derivedIdx = -1;
                        for (int k = 0; k < newModuleBody.Length; k++)
                        {
                            if (newModuleBody[k] is ClassDef c
                                && c.BaseClasses.Length > 0
                                && c.BaseClasses[0].Name == baseDef.Name)
                            {
                                derivedIdx = k;
                                break;
                            }
                        }
                        if (derivedIdx < 0 || newModuleBody[derivedIdx] is not ClassDef derived)
                            return null; // no concrete subclass, so no member can be missing

                        var withoutOverride = derived.Body
                            .Where(s => s is not FunctionDef fd || fd.Name != method.Name)
                            .ToImmutableArray();
                        if (withoutOverride.Length < derived.Body.Length)
                        {
                            newModuleBody = newModuleBody.SetItem(derivedIdx,
                                derived with { Body = withoutOverride });
                        }

                        return new InjectionResult(
                            module with { Body = newModuleBody }, ExpectedDiagnosticCode: null,
                            KnownRed: new KnownRedRefusal("#2143",
                                "analyses clean; `run` is SPY0908 CS0534 — no missing-abstract-member check for a Sharpy base"));
                    }
                }
            }
        }

        return null;
    }

    public static InjectionResult? InjectWrongConstructorArgs(Module module)
    {
        var body = GetMainBody(module);
        if (body == null)
            return null;

        for (int i = body.Value.Length - 1; i >= 0; i--)
        {
            if (body.Value[i] is VariableDeclaration { InitialValue: FunctionCall ctorCall } decl
                && ctorCall.Function is Identifier { Name: var name }
                && char.IsUpper(name[0])
                && ctorCall.Arguments.Length > 0)
            {
                // The last argument, for the reason InjectWrongFunctionArgCount gives.
                var broken = ctorCall with { Arguments = ctorCall.Arguments.RemoveAt(ctorCall.Arguments.Length - 1) };
                var mutated = ReplaceStatement(module, body.Value, i,
                    decl with { InitialValue = broken });
                return new InjectionResult(mutated, "SPY0224");
            }
        }

        return null;
    }

    /// <summary>
    /// The constraint violated where the type argument is INFERRED: the first <c>print(…)</c> in
    /// <c>main</c> that calls a module-level function with a constrained type parameter becomes
    /// <c>print(f(Plain(), …))</c>, with <c>class Plain: pass</c> (no interface) added — SPY0237
    /// "Inferred type 'Plain' does not satisfy constraint" (#2288 Phase 4). Over
    /// <c>GenGenerics.GenericConstraintProgram</c>.
    /// </summary>
    public static InjectionResult? InjectConstraintViolationAtInferredCall(Module module)
    {
        var fn = module.Body.OfType<FunctionDef>()
            .FirstOrDefault(f => f.Name != "main" && HasTypeConstraint(f.TypeParameters));
        var body = GetMainBody(module);
        if (fn is null || body is null)
            return null;

        for (int i = 0; i < body.Value.Length; i++)
        {
            if (PrintArgument(body.Value[i]) is { } arg
                && ContainsCall(arg, callee => callee is Identifier { Name: var n } && n == fn.Name))
            {
                var violating = new FunctionCall
                {
                    Function = new Identifier { Name = fn.Name },
                    Arguments = fn.Parameters.Select(_ => PlainInstance()).ToImmutableArray()
                };
                return new InjectionResult(
                    WithPlainClass(ReplaceStatement(module, body.Value, i, PrintStatement(violating))),
                    "SPY0237");
            }
        }

        return null;
    }

    /// <summary>
    /// The constraint violated where the type argument is EXPLICIT: the first <c>print(…)</c> in
    /// <c>main</c> that instantiates a class with a constrained type parameter becomes
    /// <c>print(C[Plain](Plain(), …))</c>. Sharpy does not refuse it today — the program analyses
    /// clean and Roslyn refuses the emitted C# (SPY0908 CS0311) — so it is pinned to #2212.
    /// </summary>
    public static InjectionResult? InjectConstraintViolationAtExplicitInstantiation(Module module)
    {
        var cls = module.Body.OfType<ClassDef>().FirstOrDefault(c => HasTypeConstraint(c.TypeParameters));
        var body = GetMainBody(module);
        if (cls is null || body is null)
            return null;

        var ctorArity = cls.Body.OfType<FunctionDef>()
            .FirstOrDefault(f => f.Name == "__init__")?.Parameters.Count(p => p.Name != "self") ?? 0;
        for (int i = 0; i < body.Value.Length; i++)
        {
            if (PrintArgument(body.Value[i]) is { } arg
                && ContainsCall(arg, callee => callee is IndexAccess { Object: Identifier { Name: var n } } && n == cls.Name))
            {
                var violating = new FunctionCall
                {
                    Function = new IndexAccess
                    {
                        Object = new Identifier { Name = cls.Name },
                        Index = new Identifier { Name = PlainClassName }
                    },
                    Arguments = Enumerable.Range(0, ctorArity).Select(_ => PlainInstance()).ToImmutableArray()
                };
                return new InjectionResult(
                    WithPlainClass(ReplaceStatement(module, body.Value, i, PrintStatement(violating))),
                    null, ExplicitInstantiationConstraintPin);
            }
        }

        return null;
    }

    private static readonly KnownRedRefusal ExplicitInstantiationConstraintPin = new KnownRedRefusal("#2212",
        "analyses clean; run is SPY0908 CS0311 — a violated constraint at an explicit instantiation is refused by Roslyn, not Sharpy");

    private const string PlainClassName = "Plain";

    private static bool HasTypeConstraint(ImmutableArray<TypeParameterDef> typeParameters) =>
        typeParameters.Any(tp => tp.Constraints.OfType<TypeConstraint>().Any());

    private static Expression? PrintArgument(Statement statement) => statement is ExpressionStatement
    {
        Expression: FunctionCall { Function: Identifier { Name: "print" }, Arguments.Length: 1 } call
    }
        ? call.Arguments[0]
        : null;

    private static bool ContainsCall(Expression expr, Func<Expression, bool> isCallee) => expr switch
    {
        FunctionCall call => isCallee(call.Function) || call.Arguments.Any(a => ContainsCall(a, isCallee)),
        MemberAccess access => ContainsCall(access.Object, isCallee),
        _ => false
    };

    private static ExpressionStatement PrintStatement(Expression arg) => new()
    {
        Expression = new FunctionCall
        {
            Function = new Identifier { Name = "print" },
            Arguments = ImmutableArray.Create(arg)
        }
    };

    private static Expression PlainInstance() => new FunctionCall
    {
        Function = new Identifier { Name = PlainClassName },
        Arguments = ImmutableArray<Expression>.Empty
    };

    /// <summary>Adds <c>class Plain: pass</c> — a class implementing nothing — before <c>main</c>.</summary>
    private static Module WithPlainClass(Module module)
    {
        var plain = new ClassDef
        {
            Name = PlainClassName,
            Body = ImmutableArray.Create<Statement>(new PassStatement())
        };
        var mainIndex = module.Body.IndexOf(module.Body.First(s => s is FunctionDef { Name: "main" }));
        return module with { Body = module.Body.Insert(mainIndex, plain) };
    }

    /// <summary>
    /// An Optional stored to its payload type with no narrowing (#2288 Phase 4): appends
    /// <c>unwrapped: int = maybe_n</c> (the payload of the chosen <c>T?</c> declaration) at the end of
    /// <c>main</c> — SPY0220 "the value is Optional[…]; narrow it". Over
    /// <c>GenTyped.TypedProgram(TypeEnv.WithOptionals, …)</c>; the Optional chosen is one the program's
    /// last statement reads, else the first declared.
    /// </summary>
    public static InjectionResult? InjectOptionalStoreWithoutNarrowing(Module module)
    {
        var body = GetMainBody(module);
        if (body is null || ChooseOptional(body.Value) is not { } optional)
            return null;

        var store = new VariableDeclaration
        {
            Name = UnwrappedName,
            Type = new TypeAnnotation { Name = optional.Payload },
            InitialValue = new Identifier { Name = optional.Name }
        };
        return new InjectionResult(AppendToMain(module, body.Value, store), "SPY0220");
    }

    /// <summary>
    /// An Optional used as an arithmetic operand with no narrowing (#2288 Phase 4): appends
    /// <c>print(maybe_n + 1)</c> (<c>+ "x"</c> for a <c>str?</c>) at the end of <c>main</c> — SPY0222.
    /// The other operand has the payload type, so the Optional-ness is the one broken fact.
    /// </summary>
    public static InjectionResult? InjectOptionalOperandWithoutNarrowing(Module module)
    {
        var body = GetMainBody(module);
        if (body is null || ChooseOptional(body.Value) is not { } optional)
            return null;

        var operand = new BinaryOp
        {
            Left = new Identifier { Name = optional.Name },
            Operator = BinaryOperator.Add,
            Right = optional.Payload == "str"
                ? new StringLiteral { Value = "x" }
                : new IntegerLiteral { Value = "1" }
        };
        return new InjectionResult(AppendToMain(module, body.Value, PrintStatement(operand)), "SPY0222");
    }

    /// <summary>
    /// A protocol dunder removed while its builtin is still called (#2288 Phase 4): the class's
    /// <c>__len__</c> is renamed <c>length</c> (same signature and body — the method stays, only the
    /// protocol goes), so <c>len(c)</c> in <c>main</c> draws SPY0320. Over
    /// <c>GenInterfaces.ModuleWithProtocolDunder("__len__")</c>; inapplicable unless <c>main</c> calls
    /// <c>len</c>.
    /// </summary>
    public static InjectionResult? InjectProtocolDunderRemoved(Module module)
    {
        var body = GetMainBody(module);
        if (body is null || !body.Value.Any(s => Sharpy.Compiler.Tests.Infrastructure.AstDescendants
                .SelfAndDescendants(s).Any(n => n is FunctionCall { Function: Identifier { Name: "len" } })))
            return null;

        for (int i = 0; i < module.Body.Length; i++)
        {
            if (module.Body[i] is not ClassDef cls)
                continue;
            var j = cls.Body.IndexOf(cls.Body.FirstOrDefault(s => s is FunctionDef { Name: "__len__" })!);
            if (j < 0)
                continue;
            var renamed = ((FunctionDef)cls.Body[j]) with { Name = "length" };
            var mutated = module with { Body = module.Body.SetItem(i, cls with { Body = cls.Body.SetItem(j, renamed) }) };
            return new InjectionResult(mutated, "SPY0320");
        }

        return null;
    }

    /// <summary>
    /// An interface member a class no longer implements (#2288 Phase 4): the implementation of the
    /// interface's LAST method is dropped from the implementing class — SPY0325. Over
    /// <c>GenInterfaces.ModuleWithInterface(_, completeImpl: true)</c>; the generator's own
    /// <c>completeImpl: false</c> drops the FIRST, so the two lanes are different cells.
    /// </summary>
    public static InjectionResult? InjectInterfaceMemberRemoved(Module module)
    {
        var interfaces = module.Body.OfType<InterfaceDef>().ToDictionary(d => d.Name, StringComparer.Ordinal);
        for (int i = 0; i < module.Body.Length; i++)
        {
            if (module.Body[i] is not ClassDef cls)
                continue;
            var iface = cls.BaseClasses
                .Select(b => interfaces.GetValueOrDefault(b.Name))
                .FirstOrDefault(d => d is not null && d.Body.OfType<FunctionDef>().Any());
            if (iface is null)
                continue;
            var last = iface.Body.OfType<FunctionDef>().Last().Name;
            var without = cls.Body.Where(s => s is not FunctionDef { Name: var n } || n != last).ToImmutableArray();
            if (without.Length == cls.Body.Length)
                continue; // the class never implemented it, so nothing can be removed
            return new InjectionResult(
                module with { Body = module.Body.SetItem(i, cls with { Body = without }) }, "SPY0325");
        }

        return null;
    }

    /// <summary>
    /// The Optional used as its payload in the FAILED branch of a narrowing test (#2288 Phase 4): the
    /// first <c>if v is not None: print(v)</c> block of <c>main</c> (<c>GenTyped.NarrowedBlock</c>)
    /// gains <c>else: unwrapped: T = v</c> — in the <c>is None</c> branch <c>v</c> is still
    /// <c>T?</c>, so SPY0220. The then-branch is untouched: the narrowing it proves stays accepted.
    /// </summary>
    public static InjectionResult? InjectUseInFailedNarrowingBranch(Module module)
    {
        var body = GetMainBody(module);
        if (body is null)
            return null;
        var payloads = body.Value.OfType<VariableDeclaration>()
            .Where(d => d.Type is { IsOptional: true, TypeArguments.Length: 0 })
            .ToDictionary(d => d.Name, d => d.Type!.Name, StringComparer.Ordinal);

        for (int i = 0; i < body.Value.Length; i++)
        {
            if (body.Value[i] is IfStatement
                {
                    Test: ComparisonChain
                    {
                        Operators: [ComparisonOperator.IsNot],
                        Operands: [Identifier { Name: var name }, NoneLiteral]
                    },
                    ElseBody.Length: 0
                } narrowing
                && payloads.TryGetValue(name, out var payload))
            {
                var failedBranchUse = new VariableDeclaration
                {
                    Name = UnwrappedName,
                    Type = new TypeAnnotation { Name = payload },
                    InitialValue = new Identifier { Name = name }
                };
                return new InjectionResult(
                    ReplaceStatement(module, body.Value, i,
                        narrowing with { ElseBody = ImmutableArray.Create<Statement>(failedBranchUse) }),
                    "SPY0220");
            }
        }

        return null;
    }

    /// <summary>
    /// A derived-only member read in the ELSE branch of an <c>isinstance</c> test (#2288 Phase 4):
    /// appends to <c>main</c>
    /// <code>
    /// narrowed: Base = obj
    /// if isinstance(narrowed, Derived):
    ///     print(narrowed.extra)
    /// else:
    ///     print(narrowed.extra)
    /// </code>
    /// where <c>extra</c> is a field <c>Derived</c> declares and <c>Base</c> lacks. The then-branch
    /// read is narrowed and accepted; the else-branch read is on <c>Base</c> — SPY0203. Over
    /// <c>GenClasses.ModuleWithClasses</c> (<c>GenClasses.ClassHierarchy</c>'s module).
    /// <see cref="IsinstanceNarrowingValid"/> is its positive control.
    /// </summary>
    public static InjectionResult? InjectIsinstanceElseBranchMember(Module module) =>
        IsinstanceNarrowing(module, breakElse: true) is { } mutated
            ? new InjectionResult(mutated, "SPY0203")
            : null;

    /// <summary>The valid twin of <see cref="InjectIsinstanceElseBranchMember"/>: the else branch reads a
    /// member the base declares, so the program analyses clean.</summary>
    public static Module? IsinstanceNarrowingValid(Module module) => IsinstanceNarrowing(module, breakElse: false);

    private static Module? IsinstanceNarrowing(Module module, bool breakElse)
    {
        var body = GetMainBody(module);
        if (body is null)
            return null;
        var classes = module.Body.OfType<ClassDef>().ToDictionary(c => c.Name, StringComparer.Ordinal);
        foreach (var derived in classes.Values)
        {
            if (derived.BaseClasses is not [{ Name: var baseName }] || !classes.TryGetValue(baseName, out var baseDef))
                continue;
            var baseFields = baseDef.Body.OfType<VariableDeclaration>().Select(v => v.Name).ToHashSet(StringComparer.Ordinal);
            var extra = derived.Body.OfType<VariableDeclaration>().FirstOrDefault(v => !baseFields.Contains(v.Name));
            var shared = baseDef.Body.OfType<VariableDeclaration>().FirstOrDefault();
            var instance = body.Value.OfType<VariableDeclaration>()
                .FirstOrDefault(v => v.Type?.Name == derived.Name);
            if (extra is null || shared is null || instance is null)
                continue;

            Statement Read(string member) => PrintStatement(new MemberAccess
            {
                Object = new Identifier { Name = NarrowedName },
                Member = member
            });

            var statements = ImmutableArray.Create<Statement>(
                new VariableDeclaration
                {
                    Name = NarrowedName,
                    Type = new TypeAnnotation { Name = baseName },
                    InitialValue = new Identifier { Name = instance.Name }
                },
                new IfStatement
                {
                    Test = new FunctionCall
                    {
                        Function = new Identifier { Name = "isinstance" },
                        Arguments = ImmutableArray.Create<Expression>(
                            new Identifier { Name = NarrowedName },
                            new Identifier { Name = derived.Name })
                    },
                    ThenBody = ImmutableArray.Create(Read(extra.Name)),
                    ElseBody = ImmutableArray.Create(Read(breakElse ? extra.Name : shared.Name))
                });
            var newModuleBody = module.Body
                .Select(s => s is FunctionDef { Name: "main" } main ? main with { Body = body.Value.AddRange(statements) } : s)
                .ToImmutableArray();
            return module with { Body = newModuleBody };
        }

        return null;
    }

    private const string NarrowedName = "narrowed";

    /// <summary>
    /// A <c>const</c> reassigned (#2288 Phase 4): <c>main</c>'s <c>const LIMIT: T = …</c>
    /// (<c>GenTyped.TypedProgram</c> with statements) gets <c>LIMIT = &lt;a T value&gt;</c> appended —
    /// the value has the const's type, so the reassignment is the one broken fact: SPY0225.
    /// </summary>
    public static InjectionResult? InjectConstReassignment(Module module)
    {
        var body = GetMainBody(module);
        if (body?.OfType<VariableDeclaration>().FirstOrDefault(d => d.IsConst && d.Type is not null) is not { } constDecl)
            return null;

        var reassignment = new VariableDeclaration
        {
            Name = constDecl.Name,
            InitialValue = GenTyped.DefaultValue(constDecl.Type!.Name)
        };
        return new InjectionResult(AppendToMain(module, body.Value, reassignment), "SPY0225");
    }

    private const string UnwrappedName = "unwrapped";

    /// <summary>The <c>T?</c> declarations of <c>main</c> (name, payload), preferring one the last
    /// statement reads.</summary>
    private static (string Name, string Payload)? ChooseOptional(ImmutableArray<Statement> body)
    {
        var optionals = body
            .OfType<VariableDeclaration>()
            .Where(d => d.Type is { IsOptional: true, TypeArguments.Length: 0 } && d.Type.Name is "int" or "str")
            .Select(d => (d.Name, Payload: d.Type!.Name))
            .ToList();
        if (optionals.Count == 0)
            return null;
        var read = body.Length > 0 && body[^1] is ExpressionStatement { Expression: { } last }
            ? optionals.FirstOrDefault(o => MentionsName(last, o.Name))
            : default;
        return read.Name is null ? optionals[0] : read;
    }

    private static bool MentionsName(Node node, string name) =>
        Sharpy.Compiler.Tests.Infrastructure.AstDescendants.SelfAndDescendants(node)
            .Any(n => n is Identifier id && id.Name == name);

    private static Module AppendToMain(Module module, ImmutableArray<Statement> body, Statement statement)
    {
        var newModuleBody = module.Body
            .Select(s => s is FunctionDef { Name: "main" } main ? main with { Body = body.Add(statement) } : s)
            .ToImmutableArray();
        return module with { Body = newModuleBody };
    }

    private static ImmutableArray<Statement>? GetMainBody(Module module)
    {
        foreach (var stmt in module.Body)
        {
            if (stmt is FunctionDef { Name: "main" } main && main.Body.Length > 0)
                return main.Body;
        }
        return null;
    }

    private static Module ReplaceStatement(Module module, ImmutableArray<Statement> body, int index, Statement replacement)
    {
        var newBody = body.SetItem(index, replacement);
        var newModuleBody = ImmutableArray.CreateBuilder<Statement>();
        foreach (var stmt in module.Body)
        {
            if (stmt is FunctionDef { Name: "main" } main)
                newModuleBody.Add(main with { Body = newBody });
            else
                newModuleBody.Add(stmt);
        }
        return module with { Body = newModuleBody.ToImmutable() };
    }

    private static Identifier? FindIdentifier(Expression expr) => expr switch
    {
        Identifier id when id.Name != "print" && id.Name != "len" && id.Name != "range" => id,
        FunctionCall { Arguments.Length: > 0 } call => FindIdentifier(call.Arguments[0]),
        BinaryOp bin => FindIdentifier(bin.Left) ?? FindIdentifier(bin.Right),
        _ => null
    };

    private static Expression ReplaceIdentifier(Expression expr, string oldName, string newName) => expr switch
    {
        Identifier id when id.Name == oldName => id with { Name = newName },
        FunctionCall call => call with
        {
            Function = ReplaceIdentifier(call.Function, oldName, newName),
            Arguments = call.Arguments.Select(a => ReplaceIdentifier(a, oldName, newName)).ToImmutableArray()
        },
        BinaryOp bin => bin with
        {
            Left = ReplaceIdentifier(bin.Left, oldName, newName),
            Right = ReplaceIdentifier(bin.Right, oldName, newName)
        },
        ConditionalExpression cond => cond with
        {
            Test = ReplaceIdentifier(cond.Test, oldName, newName),
            ThenValue = ReplaceIdentifier(cond.ThenValue, oldName, newName),
            ElseValue = ReplaceIdentifier(cond.ElseValue, oldName, newName)
        },
        _ => expr
    };
}
