using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// The declaration-shape breaks (#2324, P63b Phase 2 Task 5) — the axis the site enumerator cannot list:
/// each is a one-off rewrite of one declaration (a handler, a raise, an <c>__exit__</c>, an
/// <c>async with</c> manager, an <c>@override</c>, a local's name, <c>main</c>'s signature, a comparison
/// dunder), over a NORMALIZED module through <see cref="AstRewriter"/>. Null when the program has no such
/// declaration. No diagnostic code here: the lanes carry them.
/// </summary>
internal static class DeclarationShapes
{
    /// <summary>The first typed handler catches <c>int</c>, which is not an exception type.</summary>
    public static Module? HandlerTypeNotException(ProgramFacts facts)
    {
        var handlerType = AstRewriter.Descendants(facts.Module).OfType<TryStatement>()
            .SelectMany(t => t.Handlers).Select(h => h.ExceptionType).FirstOrDefault(t => t is not null);
        return handlerType is null ? null : AstRewriter.Replace(facts.Module, handlerType, new TypeAnnotation { Name = "int" });
    }

    /// <summary>The first <c>raise X(…)</c> raises <c>42</c>.</summary>
    public static Module? RaiseNonException(ProgramFacts facts)
    {
        var raised = AstRewriter.Descendants(facts.Module).OfType<RaiseStatement>().Select(r => r.Exception).FirstOrDefault(e => e is not null);
        return raised is null ? null : AstRewriter.Replace(facts.Module, raised, new IntegerLiteral { Value = "42" });
    }

    private static readonly IReadOnlyDictionary<string, string> ExceptionBases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ValueError"] = "Exception",
        ["TypeError"] = "Exception",
        ["KeyError"] = "Exception",
        ["IndexError"] = "Exception",
        ["RuntimeError"] = "Exception",
    };

    /// <summary>A <c>try</c> whose handlers go subclass → base has them reordered base → subclass: the
    /// subclass handler is unreachable.</summary>
    public static Module? HandlerOrderBaseFirst(ProgramFacts facts)
    {
        foreach (var t in AstRewriter.Descendants(facts.Module).OfType<TryStatement>())
        {
            for (int i = 0; i < t.Handlers.Length; i++)
            {
                for (int j = i + 1; j < t.Handlers.Length; j++)
                {
                    if (t.Handlers[i].ExceptionType is { Name: var sub } && t.Handlers[j].ExceptionType is { Name: var @base }
                        && ExceptionBases.GetValueOrDefault(sub) == @base)
                    {
                        var reordered = t.Handlers.SetItem(i, t.Handlers[j]).SetItem(j, t.Handlers[i]);
                        return AstRewriter.Replace(facts.Module, t, t with { Handlers = reordered });
                    }
                }
            }
        }
        return null;
    }

    /// <summary>A zero-argument <c>__exit__(self)</c> takes two more parameters.</summary>
    public static Module? ExitArity(ProgramFacts facts)
    {
        var exit = AstRewriter.Descendants(facts.Module).OfType<FunctionDef>().FirstOrDefault(f => f.Name == "__exit__" && f.Parameters.Length == 1);
        return exit is null
            ? null
            : AstRewriter.Replace(facts.Module, exit, exit with
            {
                Parameters = exit.Parameters.AddRange(new[]
                {
                    new Parameter { Name = "a", Type = new TypeAnnotation { Name = "int" } },
                    new Parameter { Name = "b", Type = new TypeAnnotation { Name = "int" } },
                })
            });
    }

    /// <summary>An <c>async with</c> manager's <c>async __aenter__/__aexit__</c> become sync
    /// <c>__enter__/__exit__</c>; the <c>async with</c> stays.</summary>
    public static Module? AsyncWithOverSyncManager(ProgramFacts facts)
    {
        var manager = facts.Module.Body.OfType<ClassDef>().FirstOrDefault(c =>
            c.Body.OfType<FunctionDef>().Any(f => f.Name == "__aenter__") && c.Body.OfType<FunctionDef>().Any(f => f.Name == "__aexit__"));
        if (manager is null || !AstRewriter.Descendants(facts.Module).OfType<WithStatement>().Any(w => w.IsAsync))
            return null;
        var body = manager.Body.Select(s => s switch
        {
            FunctionDef { Name: "__aenter__" } f => (Statement)(f with { Name = "__enter__", IsAsync = false }),
            FunctionDef { Name: "__aexit__" } f => f with { Name = "__exit__", IsAsync = false },
            _ => s,
        }).ToImmutableArray();
        return AstRewriter.Replace(facts.Module, manager, manager with { Body = body });
    }

    /// <summary>An <c>@override</c> method renamed so no base member has its name.</summary>
    public static Module? OverrideWithoutBaseMember(ProgramFacts facts)
    {
        var method = AstRewriter.Descendants(facts.Module).OfType<FunctionDef>()
            .FirstOrDefault(f => f.Decorators.Any(d => d.Name == "override"));
        return method is null ? null : AstRewriter.Replace(facts.Module, method, method with { Name = method.Name + PositionMutations.UndefinedSuffix });
    }

    /// <summary>
    /// The first annotated snake_case local of <c>main</c> renamed to camelCase — its declaration and every
    /// read — so the name is the one broken fact and the program stays otherwise the same (an appended
    /// unused camelCase local also drew SPY0451, and a program with an unused binding already carries
    /// SPY0451 warnings — measured @ 98180fcd9 — so the warning lane needs a warning-free base).
    /// </summary>
    public static Module? CamelCaseLocal(ProgramFacts facts)
    {
        if (MainOf(facts) is not { } main)
            return null;
        var declaration = main.Body.OfType<VariableDeclaration>().FirstOrDefault(v =>
            v.Type is not null && v.Name.Length > 0 && v.Name.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_'));
        if (declaration is null)
            return null;
        var renamed = declaration.Name.TrimEnd('_') + "Value";
        var edits = new List<(Node Target, Node Replacement)> { (declaration, declaration with { Name = renamed }) };
        edits.AddRange(AstRewriter.Descendants(main).OfType<Identifier>()
            .Where(i => i.Name == declaration.Name)
            .Select(i => ((Node)i, (Node)new Identifier { Name = renamed })));
        return AstRewriter.ReplaceAll(facts.Module, edits);
    }

    /// <summary><c>def main()</c> takes a parameter.</summary>
    public static Module? MainParameter(ProgramFacts facts) =>
        MainOf(facts) is { Parameters.Length: 0 } main
            ? AstRewriter.Replace(facts.Module, main, main with
            {
                Parameters = ImmutableArray.Create(new Parameter { Name = "arg", Type = new TypeAnnotation { Name = "int" } })
            })
            : null;

    /// <summary>A comparison dunder declared <c>-> int</c> (returning an int): the operator's signature is
    /// the one broken fact.</summary>
    public static Module? DunderWrongReturnType(ProgramFacts facts)
    {
        var dunder = AstRewriter.Descendants(facts.Module).OfType<FunctionDef>().FirstOrDefault(f =>
            f.Name is "__lt__" or "__le__" or "__gt__" or "__ge__" or "__eq__" or "__ne__" && f.ReturnType is { Name: "bool" });
        return dunder is null
            ? null
            : AstRewriter.Replace(facts.Module, dunder, dunder with
            {
                ReturnType = new TypeAnnotation { Name = "int" },
                Body = ImmutableArray.Create<Statement>(new ReturnStatement { Value = new IntegerLiteral { Value = "0" } })
            });
    }

    /// <summary>
    /// The first call statement of <c>main</c> becomes a conditional expression statement over two copies of
    /// the call (<c>print(x) if True else print(x)</c>): a value-producing expression used as a statement —
    /// SPY0603 <c>ExpressionStatementNotDiscardable</c> (#1622), measured by the P63b verifier.
    /// </summary>
    public static Module? ConditionalExpressionStatement(ProgramFacts facts)
    {
        var statement = MainOf(facts)?.Body.OfType<ExpressionStatement>().FirstOrDefault(s => s.Expression is FunctionCall);
        return statement is null
            ? null
            : AstRewriter.Replace(facts.Module, statement, new ExpressionStatement
            {
                Expression = new ConditionalExpression
                {
                    Test = new BooleanLiteral { Value = true },
                    ThenValue = statement.Expression,
                    ElseValue = statement.Expression with { },
                }
            });
    }

    /// <summary>The by-design control for an expression statement: <c>main</c> gains <c>42</c> as a statement.</summary>
    public static Module? ExpressionStatementOfAnyValue(ProgramFacts facts) =>
        MainOf(facts) is { } main
            ? AstRewriter.Replace(facts.Module, main, main with
            {
                Body = main.Body.Insert(0, new ExpressionStatement { Expression = new IntegerLiteral { Value = "42" } })
            })
            : null;

    private static FunctionDef? MainOf(ProgramFacts facts) => facts.Module.Body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == "main");
}
