using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

internal static class ErrorInjector
{
    public record InjectionResult(Module Mutated, string ExpectedDiagnosticCode);

    public static InjectionResult? InjectTypeMismatchAssignment(Module module)
    {
        var body = GetMainBody(module);
        if (body == null) return null;

        for (int i = 0; i < body.Value.Length; i++)
        {
            if (body.Value[i] is VariableDeclaration { Type: { } typeAnn } decl
                && typeAnn.Name == "int")
            {
                var mutated = ReplaceStatement(module, body.Value, i,
                    decl with { InitialValue = new StringLiteral { Value = "wrong" } });
                return new InjectionResult(mutated, "SPY02");
            }

            if (body.Value[i] is VariableDeclaration { Type: { } typeAnn2 } decl2
                && typeAnn2.Name == "str")
            {
                var mutated = ReplaceStatement(module, body.Value, i,
                    decl2 with { InitialValue = new IntegerLiteral { Value = "42" } });
                return new InjectionResult(mutated, "SPY02");
            }
        }

        return null;
    }

    public static InjectionResult? InjectUndefinedVariable(Module module)
    {
        var body = GetMainBody(module);
        if (body == null) return null;

        for (int i = body.Value.Length - 1; i >= 0; i--)
        {
            if (body.Value[i] is ExpressionStatement { Expression: FunctionCall call }
                && FindIdentifier(call) is { } ident)
            {
                var replaced = ReplaceIdentifier(call, ident.Name, "undefined_xyz_var");
                var mutated = ReplaceStatement(module, body.Value, i,
                    new ExpressionStatement { Expression = (Expression)replaced });
                return new InjectionResult(mutated, "SPY02");
            }
        }

        return null;
    }

    public static InjectionResult? InjectWrongArgumentType(Module module)
    {
        var body = GetMainBody(module);
        if (body == null) return null;

        for (int i = body.Value.Length - 1; i >= 0; i--)
        {
            if (body.Value[i] is ExpressionStatement { Expression: { } expr })
            {
                var mutated = ReplaceLenArg(expr, new IntegerLiteral { Value = "42" });
                if (mutated != null)
                {
                    var result = ReplaceStatement(module, body.Value, i,
                        new ExpressionStatement { Expression = mutated });
                    return new InjectionResult(result, "SPY02");
                }
            }
        }

        return null;
    }

    public static InjectionResult? InjectMissingArgument(Module module)
    {
        var body = GetMainBody(module);
        if (body == null) return null;

        for (int i = body.Value.Length - 1; i >= 0; i--)
        {
            if (body.Value[i] is ExpressionStatement { Expression: { } expr })
            {
                var mutated = StripLenArgs(expr);
                if (mutated != null)
                {
                    var result = ReplaceStatement(module, body.Value, i,
                        new ExpressionStatement { Expression = mutated });
                    return new InjectionResult(result, "SPY02");
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
