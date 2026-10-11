using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class ErrorInjector
{
    /// <summary>
    /// An injected program and the ONE error code it must draw (#2288). The code is exact, never a
    /// prefix: a refusal for an unrelated reason is not a catch, and a mutation that also breaks
    /// something else draws a second code — a bad injection, not a pass. <see cref="KnownRed"/> pins
    /// an injection the compiler does not refuse today; <see cref="Judge"/> then requires the program
    /// to still analyse clean, so the first refusal fails the row and the pin drains with its issue.
    /// <see cref="ExpectedDiagnosticCode"/> is null only on a pin whose refusal has no code yet.
    /// </summary>
    public record InjectionResult(Module Mutated, string? ExpectedDiagnosticCode, KnownRedRefusal? KnownRed = null);

    /// <summary>A refusal the compiler does not make today: <paramref name="Issue"/> tracks it, and
    /// <paramref name="Observed"/> says what happens instead.</summary>
    public sealed record KnownRedRefusal(string Issue, string Observed);

    /// <summary>
    /// Analyses the injected program: null when it drew exactly the expected error code (a pinned
    /// injection: when it analysed clean), otherwise why not. A throw is an internal error, never a
    /// refusal.
    /// </summary>
    public static string? Judge(InjectionResult injection, string label)
    {
        var source = Sharpy.Compiler.Pretty.Unparser.Unparse(injection.Mutated);
        List<string> codes;
        try
        {
            var analysis = new Sharpy.Compiler.Compiler().Analyze(source, "injection.spy");
            codes = analysis.Diagnostics.GetAll()
                .Where(d => d.IsError)
                .Select(d => d.Code ?? "(no code)")
                .Distinct()
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();
            if (!analysis.Success && codes.Count == 0)
                return $"Injected {label} failed analysis without an error code:\n{source}";
        }
        catch (Exception e)
        {
            return $"Injected {label} threw {e.GetType().Name} — an internal error is not a refusal:\n{source}\n{e}";
        }

        var drew = codes.Count == 0 ? "nothing" : string.Join(", ", codes);
        if (injection.KnownRed is { } pin)
        {
            var next = injection.ExpectedDiagnosticCode is { } code
                ? $"expect {code}"
                : "give the injection the code the fix allocated";
            return codes.Count == 0
                ? null
                : $"Injected {label} is now refused [{drew}] — if that is {pin.Issue}'s fix, drop the KnownRed pin "
                  + $"and {next}; otherwise the injection breaks something else:\n{source}";
        }

        var expected = injection.ExpectedDiagnosticCode
            ?? throw new InvalidOperationException($"Injection {label} has neither an expected code nor a KnownRed pin");
        return codes is [var only] && only == expected
            ? null
            : $"Injected {label} must draw exactly {expected}, drew [{drew}]:\n{source}";
    }

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
