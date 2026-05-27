using System.Collections.Immutable;
using System.Diagnostics;

namespace Sharpy.Compiler.Parser.Ast;

// =============================================================================
// FUTURE AND RECENTLY-IMPLEMENTED STATEMENT NODES
// Some types below are fully implemented (MatchStatement, MatchCase);
// others remain placeholders for forward compatibility.
// =============================================================================

#region Pattern Matching

/// <summary>
/// Match statement (match expr: case1: ..., case2: ...).
/// Executes code based on pattern matching (statement form, unlike MatchExpression).
/// </summary>
public record MatchStatement : Statement
{
    /// <summary>
    /// The expression being matched against patterns.
    /// </summary>
    public Expression Scrutinee { get; init; } = null!;

    /// <summary>
    /// The match cases (pattern: body pairs).
    /// </summary>
    public ImmutableArray<MatchCase> Cases { get; init; } = ImmutableArray<MatchCase>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Scrutinee != null, "MatchStatement.Scrutinee cannot be null");
        Debug.Assert(Cases != null, "MatchStatement.Cases cannot be null");
        Debug.Assert(Cases.Length > 0, "MatchStatement.Cases must have at least one case");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Scrutinee;
        foreach (var matchCase in Cases)
        {
            yield return matchCase.Pattern;
            if (matchCase.Guard != null)
                yield return matchCase.Guard;
            foreach (var stmt in matchCase.Body)
                yield return stmt;
        }
    }
}

/// <summary>
/// A single case in a match statement (pattern: body).
/// </summary>
public record MatchCase
{
    /// <summary>
    /// The pattern to match against.
    /// </summary>
    public Pattern Pattern { get; init; } = null!;

    /// <summary>
    /// Optional guard condition (when clause).
    /// </summary>
    public Expression? Guard { get; init; }

    /// <summary>
    /// The body statements to execute if the pattern matches.
    /// </summary>
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}

#endregion

#region Tagged Unions / ADTs (v0.2.x)

/// <summary>
/// Union type definition (tagged union / algebraic data type).
/// </summary>
/// <example>
/// union Result[T, E]:
///     case Ok(value: T)
///     case Err(error: E)
/// </example>
/// <remarks>
/// Fully implemented in Phase 8.6: parser, semantic analysis, and code generation.
/// Lowers to abstract base class + sealed nested case classes in C#.
/// Pattern matching on union cases is implemented in Phase 8.7.
/// </remarks>
public record UnionDef : Statement
{
    /// <summary>
    /// The name of the union type.
    /// </summary>
    public string Name { get; init; } = "";
    public int NameLineStart { get; init; }
    public int NameColumnStart { get; init; }

    public bool IsNameBacktickEscaped { get; init; }

    /// <summary>
    /// Type parameters for generic unions (e.g., T, E in Result[T, E]).
    /// </summary>
    public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;

    /// <summary>
    /// The union cases (variants).
    /// </summary>
    public ImmutableArray<UnionCaseDef> Cases { get; init; } = ImmutableArray<UnionCaseDef>.Empty;

    /// <summary>
    /// Methods and other statements defined in the union body.
    /// </summary>
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;

    /// <summary>
    /// Decorators applied to the union.
    /// </summary>
    public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;

    /// <summary>
    /// Documentation string.
    /// </summary>
    public string? DocString { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "UnionDef.Name cannot be null or empty");
        Debug.Assert(TypeParameters != null, "UnionDef.TypeParameters cannot be null");
        Debug.Assert(Cases != null, "UnionDef.Cases cannot be null");
        Debug.Assert(Body != null, "UnionDef.Body cannot be null");
        Debug.Assert(Decorators != null, "UnionDef.Decorators cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        // TypeParameters, Cases, and Decorators don't contain Node-derived children
        // that we need to traverse (TypeAnnotation doesn't inherit from Node)
        foreach (var stmt in Body)
            yield return stmt;
    }
}

/// <summary>
/// A single case (variant) in a union type definition.
/// </summary>
/// <example>
/// case Ok(value: T)       // Case with named field
/// case None               // Case with no fields
/// case Tuple(int, str)    // Case with positional fields
/// </example>
public record UnionCaseDef
{
    /// <summary>
    /// The name of this case (e.g., Ok, Err, None).
    /// </summary>
    public string Name { get; init; } = "";
    public int NameLineStart { get; init; }
    public int NameColumnStart { get; init; }


    /// <summary>
    /// Fields for this case. Empty for singleton cases (e.g., None).
    /// </summary>
    public ImmutableArray<UnionCaseField> Fields { get; init; } = ImmutableArray<UnionCaseField>.Empty;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}

/// <summary>
/// A field in a union case.
/// </summary>
public record UnionCaseField
{
    /// <summary>
    /// The field name. Positional fields are not yet supported.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// The field type.
    /// </summary>
    public TypeAnnotation Type { get; init; } = null!;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}

#endregion

#region Delegate Types

/// <summary>
/// Delegate type declaration (named function signature type).
/// </summary>
/// <example>
/// delegate Handler(event: Event) -> bool
/// delegate Predicate[T](item: T) -> bool
/// </example>
public record DelegateDef : Statement
{
    /// <summary>
    /// The name of the delegate type.
    /// </summary>
    public string Name { get; init; } = "";
    public int NameLineStart { get; init; }
    public int NameColumnStart { get; init; }

    public bool IsNameBacktickEscaped { get; init; }

    /// <summary>
    /// Type parameters for generic delegates (e.g., T in Predicate[T]).
    /// </summary>
    public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;

    /// <summary>
    /// The delegate's parameter list.
    /// </summary>
    public ImmutableArray<Parameter> Parameters { get; init; } = ImmutableArray<Parameter>.Empty;

    /// <summary>
    /// The return type annotation. Null means void/None return.
    /// </summary>
    public TypeAnnotation? ReturnType { get; init; }

    /// <summary>
    /// Documentation string.
    /// </summary>
    public string? DocString { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "DelegateDef.Name cannot be null or empty");
        Debug.Assert(TypeParameters != null, "DelegateDef.TypeParameters cannot be null");
        Debug.Assert(Parameters != null, "DelegateDef.Parameters cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        foreach (var param in Parameters)
        {
            if (param.DefaultValue != null)
                yield return param.DefaultValue;
        }
    }
}

#endregion

#region Events

/// <summary>
/// Event accessor type.
/// </summary>
public enum EventAccessor { None, Add, Remove }

/// <summary>
/// Event definition. Auto-events use <c>event name: DelegateType</c> syntax,
/// function-style events use <c>event add name(self, handler: T):</c> / <c>event remove name(self, handler: T):</c>.
/// </summary>
/// <example>
/// # Auto-event (compiler-generated backing delegate)
/// event on_click: EventHandler
///
/// # Function-style event (custom add/remove logic)
/// event add on_click(self, handler: EventHandler):
///     self._handlers.append(handler)
///
/// event remove on_click(self, handler: EventHandler):
///     self._handlers.remove(handler)
/// </example>
public record EventDef : Statement
{
    /// <summary>
    /// The event name.
    /// </summary>
    public string Name { get; init; } = "";
    public int NameLineStart { get; init; }
    public int NameColumnStart { get; init; }

    public bool IsNameBacktickEscaped { get; init; }

    /// <summary>
    /// The accessor kind: None for auto-events, Add or Remove for function-style events.
    /// </summary>
    public EventAccessor Accessor { get; init; } = EventAccessor.None;

    /// <summary>
    /// The delegate type annotation (for auto-events).
    /// </summary>
    public TypeAnnotation? Type { get; init; }

    /// <summary>
    /// Whether this is a function-style event (with parameters and body).
    /// </summary>
    public bool IsFunctionStyle { get; init; }

    /// <summary>
    /// Parameters for function-style events (e.g., self, handler: EventHandler).
    /// </summary>
    public ImmutableArray<Parameter> Parameters { get; init; } = ImmutableArray<Parameter>.Empty;

    /// <summary>
    /// Body statements for function-style events.
    /// </summary>
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;

    /// <summary>
    /// Decorators applied to the event.
    /// </summary>
    public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "EventDef.Name cannot be null or empty");
        Debug.Assert(Parameters != null, "EventDef.Parameters cannot be null");
        Debug.Assert(Body != null, "EventDef.Body cannot be null");
        Debug.Assert(Decorators != null, "EventDef.Decorators cannot be null");

        if (IsFunctionStyle)
        {
            Debug.Assert(Accessor != EventAccessor.None,
                "Function-style EventDef must have Add or Remove accessor");
        }
        else
        {
            Debug.Assert(Accessor == EventAccessor.None,
                "Auto-event EventDef must have None accessor");
            Debug.Assert(Type != null,
                "Auto-event EventDef must have a type annotation");
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        foreach (var param in Parameters)
        {
            if (param.DefaultValue != null)
                yield return param.DefaultValue;
        }
        foreach (var stmt in Body)
            yield return stmt;
    }
}

#endregion
