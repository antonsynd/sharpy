using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates <c>@dataclass(frozen=True)</c> field assignment restrictions (#1902, R-AV).
///
/// <para>A frozen dataclass field is emitted as a C# init-only property (<c>{ get; init; }</c>,
/// <c>RoslynEmitter.ClassMembers.Dataclass.cs::GenerateDataclassProperty</c>) — legal to set only in
/// an object initializer, or directly inside an instance constructor's OWN body. A
/// <c>__post_init__</c> assignment reaches Roslyn as a call from a separate method the synthesized
/// constructor invokes, not as a statement inside the constructor's own body, so it ICEd CS8852
/// behind SPY0908 (the shape the ordinary <c>@final</c>-field case never hits, because
/// <see cref="FinalFieldValidator"/> already refuses that one by name). This validator refuses the
/// assignment by name before it ever reaches code generation — Python's analog is
/// <c>FrozenInstanceError</c>.</para>
///
/// <para><b>Rule:</b> for <c>@dataclass(frozen=True) class C</c>, every instance field of <c>C</c>
/// (<see cref="MemberClassification.IsInstanceField"/> — excludes <c>const</c>/<c>@static</c>, which
/// are not per-instance state and never become init-only) is off-limits to
/// <c>self.&lt;field&gt; = ...</c> everywhere except <c>C</c>'s own <c>__init__</c>. The ordinary case
/// has no user-written <c>__init__</c> at all (the constructor is synthesized), so
/// <c>__post_init__</c> and every other method are equally off-limits; the exemption exists only for
/// the edge case of an explicit <c>__init__</c>, which suppresses dataclass synthesis and becomes the
/// sole legitimate initializer — the same shape <see cref="FinalFieldValidator"/> grants its own
/// class's constructor.</para>
/// </summary>
internal class FrozenDataclassValidator : SemanticValidatorBase
{
    public override string Name => "FrozenDataclassValidator";
    public override int Order => 414; // After EventValidator (412), before VarianceValidator (415)

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        _logger.LogDebug("Starting frozen dataclass field assignment validation");

        foreach (var stmt in module.Body)
        {
            ValidateModuleStatement(stmt, context);
        }
    }

    private void ValidateModuleStatement(Statement stmt, SemanticContext context)
    {
        switch (stmt)
        {
            case ClassDef classDef:
                ValidateClassBody(classDef, context);
                break;
            default:
                // walker-default-contract: only a class can carry @dataclass(frozen=True); every
                // other top-level kind is deliberately ignored (rostered in DispatchSiteInventoryTests).
                break;
        }
    }

    /// <summary>
    /// Validates one class's frozen-field rule and recurses into the classes nested inside it — a
    /// nested class may be its own frozen dataclass independent of its enclosing type's status.
    /// </summary>
    private void ValidateClassBody(ClassDef classDef, SemanticContext context)
    {
        var options = DataclassSynthesis.ReadOptions(classDef);
        if (options is { Frozen: true })
        {
            var frozenFields = classDef.Body.OfType<VariableDeclaration>()
                .Where(MemberClassification.IsInstanceField)
                .Select(f => f.Name)
                .ToHashSet(StringComparer.Ordinal);

            if (frozenFields.Count > 0)
            {
                foreach (var member in classDef.Body)
                {
                    switch (member)
                    {
                        case FunctionDef method when method.Name != DunderNames.Init:
                            WalkForFrozenFieldAssignments(method.Body, frozenFields, classDef.Name, context);
                            break;
                        case PropertyDef propDef:
                            WalkForFrozenFieldAssignments(propDef.Body, frozenFields, classDef.Name, context);
                            break;
                        case EventDef eventDef:
                            WalkForFrozenFieldAssignments(eventDef.Body, frozenFields, classDef.Name, context);
                            break;
                        default:
                            // walker-default-contract: the class's own __init__ is the sole
                            // legitimate initializer (excluded above by the FunctionDef guard); a
                            // field declaration, nested type, or any other member kind carries no
                            // assignment of its own to walk. Nested classes are handled by the
                            // caller's separate recursion, not this switch.
                            break;
                    }
                }
            }
        }

        foreach (var nestedClass in classDef.Body.OfType<ClassDef>())
        {
            ValidateClassBody(nestedClass, context);
        }
    }

    private void WalkForFrozenFieldAssignments(
        IReadOnlyList<Statement> statements,
        HashSet<string> frozenFields,
        string typeName,
        SemanticContext context)
    {
        foreach (var stmt in statements)
        {
            if (stmt is Assignment assign
                && assign.Target is MemberAccess { Object: Identifier { Name: PythonNames.Self }, Member: var member }
                && frozenFields.Contains(member))
            {
                AddError(context,
                    $"Cannot assign to field '{member}' of frozen dataclass '{typeName}'; " +
                    "remove frozen=True or compute the value before construction",
                    assign.LineStart, assign.ColumnStart,
                    code: DiagnosticCodes.ValidationOverflow.FrozenFieldReassignment,
                    span: assign.Span);
            }

            foreach (var child in GetChildStatements(stmt))
            {
                WalkForFrozenFieldAssignments(child, frozenFields, typeName, context);
            }
        }
    }

    /// <summary>
    /// Same shape as <see cref="FinalFieldValidator"/>'s own statement-body walk — duplicated
    /// deliberately rather than shared: the two validators watch different immutability sources
    /// (a per-field <c>@final</c> decorator vs. a class-level <c>frozen=True</c> option) and must
    /// stay independently correct.
    /// </summary>
    private static IEnumerable<IReadOnlyList<Statement>> GetChildStatements(Statement stmt)
    {
        switch (stmt)
        {
            case IfStatement ifStmt:
                yield return ifStmt.ThenBody;
                foreach (var elif in ifStmt.ElifClauses)
                    yield return elif.Body;
                if (ifStmt.ElseBody.Length > 0)
                    yield return ifStmt.ElseBody;
                break;
            case ForStatement forStmt:
                yield return forStmt.Body;
                break;
            case WhileStatement whileStmt:
                yield return whileStmt.Body;
                break;
            case TryStatement tryStmt:
                yield return tryStmt.Body;
                foreach (var handler in tryStmt.Handlers)
                    yield return handler.Body;
                if (tryStmt.FinallyBody.Length > 0)
                    yield return tryStmt.FinallyBody;
                if (tryStmt.ElseBody.Length > 0)
                    yield return tryStmt.ElseBody;
                break;
            case WithStatement withStmt:
                yield return withStmt.Body;
                break;
            case FunctionDef nestedFunc:
                yield return nestedFunc.Body;
                break;
            case DecoratedStatement decorated:
                // @suppress wrapper (#1024): suppression is warning-only metadata; the frozen-field
                // assignment *error* inside must still be found (errors are never suppressible).
                yield return new[] { decorated.Statement };
                break;
            default:
                // walker-default-contract: any kind not listed above is deliberately ignored by
                // this walker (rostered in DispatchSiteInventoryTests).
                break;
        }
    }
}
