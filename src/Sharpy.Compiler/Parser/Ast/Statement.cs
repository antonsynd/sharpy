using System.Collections.Immutable;
using System.Diagnostics;

namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Base class for all statement nodes
/// </summary>
public abstract record Statement : Node;

#region Simple Statements

/// <summary>
/// Expression statement (expression used as statement)
/// </summary>
public record ExpressionStatement : Statement
{
    public Expression Expression { get; init; } = null!;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Expression != null, "ExpressionStatement.Expression cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Expression;
    }
}

/// <summary>
/// Assignment statement (x = value, x += value, etc.)
/// </summary>
public record Assignment : Statement
{
    public Expression Target { get; init; } = null!;
    public Expression Value { get; init; } = null!;
    public AssignmentOperator Operator { get; init; } = AssignmentOperator.Assign;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Target != null, "Assignment.Target cannot be null");
        Debug.Assert(Value != null, "Assignment.Value cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Target;
        yield return Value;
    }
}

public enum AssignmentOperator
{
    Assign,        // =
    PlusAssign,    // +=
    MinusAssign,   // -=
    StarAssign,    // *=
    SlashAssign,   // /=
    DoubleSlashAssign,  // //=
    PercentAssign, // %=
    PowerAssign,   // **=
    AndAssign,     // &=
    OrAssign,      // |=
    XorAssign,     // ^=
    LeftShiftAssign,  // <<=
    RightShiftAssign,  // >>=
    NullCoalesceAssign // ??=
}

/// <summary>
/// Variable declaration with optional type annotation (x: int = 5 or x = 5 with inference)
/// </summary>
public record VariableDeclaration : Statement
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
    public Expression? InitialValue { get; init; }
    public bool IsConst { get; init; }
    public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "VariableDeclaration.Name cannot be null or empty");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        // Note: Type is a TypeAnnotation which doesn't inherit from Node
        if (InitialValue != null)
            yield return InitialValue;
    }
}

/// <summary>
/// Assert statement (assert condition, message)
/// </summary>
public record AssertStatement : Statement
{
    public Expression Test { get; init; } = null!;
    public Expression? Message { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Test != null, "AssertStatement.Test cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Test;
        if (Message != null)
            yield return Message;
    }
}

/// <summary>
/// Pass statement (no-op placeholder)
/// </summary>
public record PassStatement : Statement;

/// <summary>
/// Break statement
/// </summary>
public record BreakStatement : Statement;

/// <summary>
/// Break statement with flag assignment (internal, generated for loop else support)
/// Sets the flag to false before breaking.
/// </summary>
public record BreakWithFlagStatement : Statement
{
    public string FlagName { get; init; } = "";
}

/// <summary>
/// Continue statement
/// </summary>
public record ContinueStatement : Statement;

/// <summary>
/// Return statement
/// </summary>
public record ReturnStatement : Statement
{
    public Expression? Value { get; init; }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        if (Value != null)
            yield return Value;
    }
}

/// <summary>
/// Yield statement (yield value or yield from iterable)
/// </summary>
public record YieldStatement : Statement
{
    /// <summary>The expression to yield. Required (bare yield not supported).</summary>
    public Expression Value { get; init; } = null!;

    /// <summary>True for "yield from iterable" (delegation).</summary>
    public bool IsFrom { get; init; }

    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Value != null, "YieldStatement.Value cannot be null");
    }

    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Value;
    }
}

/// <summary>
/// Raise statement (raise exception or raise)
/// </summary>
public record RaiseStatement : Statement
{
    public Expression? Exception { get; init; }
    public Expression? Cause { get; init; }  // raise ... from cause

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        if (Exception != null)
            yield return Exception;
        if (Cause != null)
            yield return Cause;
    }
}

#endregion

#region Compound Statements

/// <summary>
/// If statement with optional elif and else branches
/// </summary>
public record IfStatement : Statement
{
    public Expression Test { get; init; } = null!;
    public ImmutableArray<Statement> ThenBody { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<ElifClause> ElifClauses { get; init; } = ImmutableArray<ElifClause>.Empty;
    public ImmutableArray<Statement> ElseBody { get; init; } = ImmutableArray<Statement>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Test != null, "IfStatement.Test cannot be null");
        Debug.Assert(ThenBody != null, "IfStatement.ThenBody cannot be null");
        Debug.Assert(ElifClauses != null, "IfStatement.ElifClauses cannot be null");
        Debug.Assert(ElseBody != null, "IfStatement.ElseBody cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Test;
        foreach (var stmt in ThenBody)
            yield return stmt;
        foreach (var elif in ElifClauses)
        {
            yield return elif.Test;
            foreach (var stmt in elif.Body)
                yield return stmt;
        }
        foreach (var stmt in ElseBody)
            yield return stmt;
    }
}

public record ElifClause
{
    public Expression Test { get; init; } = null!;
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }

    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// </summary>
    public Text.TextSpan? Span { get; init; }
}

/// <summary>
/// While loop with optional else clause (runs if loop completes without break)
/// </summary>
public record WhileStatement : Statement
{
    public Expression Test { get; init; } = null!;
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<Statement> ElseBody { get; init; } = ImmutableArray<Statement>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Test != null, "WhileStatement.Test cannot be null");
        Debug.Assert(Body != null, "WhileStatement.Body cannot be null");
        Debug.Assert(ElseBody != null, "WhileStatement.ElseBody cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Test;
        foreach (var stmt in Body)
            yield return stmt;
        foreach (var stmt in ElseBody)
            yield return stmt;
    }
}

/// <summary>
/// For loop (for item in iterable:) with optional else clause (runs if loop completes without break)
/// </summary>
public record ForStatement : Statement
{
    public Expression Target { get; init; } = null!;  // Loop variable(s)
    public Expression Iterator { get; init; } = null!;
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<Statement> ElseBody { get; init; } = ImmutableArray<Statement>.Empty;
    public bool IsAsync { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Target != null, "ForStatement.Target cannot be null");
        Debug.Assert(Iterator != null, "ForStatement.Iterator cannot be null");
        Debug.Assert(Body != null, "ForStatement.Body cannot be null");
        Debug.Assert(ElseBody != null, "ForStatement.ElseBody cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        yield return Target;
        yield return Iterator;
        foreach (var stmt in Body)
            yield return stmt;
        foreach (var stmt in ElseBody)
            yield return stmt;
    }
}

/// <summary>
/// Try-except-else-finally statement
/// </summary>
public record TryStatement : Statement
{
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<ExceptHandler> Handlers { get; init; } = ImmutableArray<ExceptHandler>.Empty;
    public ImmutableArray<Statement> ElseBody { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<Statement> FinallyBody { get; init; } = ImmutableArray<Statement>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Body != null, "TryStatement.Body cannot be null");
        Debug.Assert(Handlers != null, "TryStatement.Handlers cannot be null");
        Debug.Assert(ElseBody != null, "TryStatement.ElseBody cannot be null");
        Debug.Assert(FinallyBody != null, "TryStatement.FinallyBody cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        foreach (var stmt in Body)
            yield return stmt;
        // Note: handler.ExceptionType is a TypeAnnotation which doesn't inherit from Node
        foreach (var handler in Handlers)
        {
            foreach (var stmt in handler.Body)
                yield return stmt;
        }
        foreach (var stmt in ElseBody)
            yield return stmt;
        foreach (var stmt in FinallyBody)
            yield return stmt;
    }
}

public record ExceptHandler
{
    public TypeAnnotation? ExceptionType { get; init; }
    public string? Name { get; init; }  // except Exception as e:
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }

    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// </summary>
    public Text.TextSpan? Span { get; init; }
}

/// <summary>
/// With statement (with expr as name:) maps to C# using statement
/// </summary>
public record WithStatement : Statement
{
    public ImmutableArray<WithItem> Items { get; init; } = ImmutableArray<WithItem>.Empty;
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public bool IsAsync { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Items != null && Items.Length > 0, "WithStatement.Items must have at least one item");
        Debug.Assert(Body != null, "WithStatement.Body cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        foreach (var item in Items)
            yield return item.ContextExpression;
        foreach (var stmt in Body)
            yield return stmt;
    }
}

public record WithItem
{
    public Expression ContextExpression { get; init; } = null!;
    public string? Name { get; init; }  // The "as name" binding

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }

    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// </summary>
    public Text.TextSpan? Span { get; init; }
}

#endregion

#region Definitions

/// <summary>
/// Function definition
/// </summary>
public record FunctionDef : Statement
{
    public string Name { get; init; } = "";
    public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
    public ImmutableArray<Parameter> Parameters { get; init; } = ImmutableArray<Parameter>.Empty;
    public TypeAnnotation? ReturnType { get; init; }
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;
    public string? DocString { get; init; }
    public bool IsAsync { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "FunctionDef.Name cannot be null or empty");
        Debug.Assert(TypeParameters != null, "FunctionDef.TypeParameters cannot be null");
        Debug.Assert(Parameters != null, "FunctionDef.Parameters cannot be null");
        Debug.Assert(Body != null, "FunctionDef.Body cannot be null");
        Debug.Assert(Decorators != null, "FunctionDef.Decorators cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        // Note: ReturnType and param.Type are TypeAnnotations which don't inherit from Node
        foreach (var param in Parameters)
        {
            if (param.DefaultValue != null)
                yield return param.DefaultValue;
        }
        foreach (var stmt in Body)
            yield return stmt;
    }
}

/// <summary>
/// Class definition
/// </summary>
public record ClassDef : Statement
{
    public string Name { get; init; } = "";
    public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
    public ImmutableArray<TypeAnnotation> BaseClasses { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;
    public string? DocString { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "ClassDef.Name cannot be null or empty");
        Debug.Assert(TypeParameters != null, "ClassDef.TypeParameters cannot be null");
        Debug.Assert(BaseClasses != null, "ClassDef.BaseClasses cannot be null");
        Debug.Assert(Body != null, "ClassDef.Body cannot be null");
        Debug.Assert(Decorators != null, "ClassDef.Decorators cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        // Note: BaseClasses are TypeAnnotations which don't inherit from Node
        foreach (var stmt in Body)
            yield return stmt;
    }
}

/// <summary>
/// Struct definition (value type)
/// </summary>
public record StructDef : Statement
{
    public string Name { get; init; } = "";
    public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
    public ImmutableArray<TypeAnnotation> BaseClasses { get; init; } = ImmutableArray<TypeAnnotation>.Empty;  // Interfaces only
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;
    public string? DocString { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "StructDef.Name cannot be null or empty");
        Debug.Assert(TypeParameters != null, "StructDef.TypeParameters cannot be null");
        Debug.Assert(BaseClasses != null, "StructDef.BaseClasses cannot be null");
        Debug.Assert(Body != null, "StructDef.Body cannot be null");
        Debug.Assert(Decorators != null, "StructDef.Decorators cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        // Note: BaseClasses are TypeAnnotations which don't inherit from Node
        foreach (var stmt in Body)
            yield return stmt;
    }
}

/// <summary>
/// Interface definition
/// </summary>
public record InterfaceDef : Statement
{
    public string Name { get; init; } = "";
    public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
    public ImmutableArray<TypeAnnotation> BaseInterfaces { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public string? DocString { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "InterfaceDef.Name cannot be null or empty");
        Debug.Assert(TypeParameters != null, "InterfaceDef.TypeParameters cannot be null");
        Debug.Assert(BaseInterfaces != null, "InterfaceDef.BaseInterfaces cannot be null");
        Debug.Assert(Body != null, "InterfaceDef.Body cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        // Note: BaseInterfaces are TypeAnnotations which don't inherit from Node
        foreach (var stmt in Body)
            yield return stmt;
    }
}

/// <summary>
/// Enum definition (simple enums only in v0.5)
/// </summary>
public record EnumDef : Statement
{
    public string Name { get; init; } = "";
    public ImmutableArray<EnumMember> Members { get; init; } = ImmutableArray<EnumMember>.Empty;
    public string? DocString { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "EnumDef.Name cannot be null or empty");
        Debug.Assert(Members != null, "EnumDef.Members cannot be null");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        foreach (var member in Members)
        {
            if (member.Value != null)
                yield return member.Value;
        }
    }
}

public record EnumMember
{
    public string Name { get; init; } = "";
    public Expression? Value { get; init; }  // Optional explicit value

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }

    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// </summary>
    public Text.TextSpan? Span { get; init; }
}

/// <summary>
/// Type alias declaration (type UserId = int, type Callback = (int, str) -> bool)
/// Exactly one of Type or FunctionType must be set.
/// </summary>
public record TypeAlias : Statement
{
    public string Name { get; init; } = "";
    public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
    public TypeAnnotation? Type { get; init; }
    public FunctionType? FunctionType { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "TypeAlias.Name cannot be null or empty");
        // Exactly one of Type or FunctionType should be set
        Debug.Assert(Type != null || FunctionType != null,
            "TypeAlias must have either Type or FunctionType set");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        // Type and FunctionType don't inherit from Node
        yield break;
    }
}

/// <summary>
/// Represents a single type parameter with its constraints (e.g., "T: IComparable")
/// </summary>
public record TypeParameterDef
{
    public string Name { get; init; } = "";
    public ImmutableArray<ConstraintClause> Constraints { get; init; } = ImmutableArray<ConstraintClause>.Empty;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }

    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// </summary>
    public Text.TextSpan? Span { get; init; }
}

/// <summary>
/// Base type for constraint clauses
/// </summary>
public abstract record ConstraintClause;

/// <summary>
/// Interface/type constraint: T: IComparable
/// </summary>
public record TypeConstraint : ConstraintClause
{
    public TypeAnnotation Type { get; init; } = null!;
}

/// <summary>
/// Reference type constraint: T: class
/// </summary>
public record ClassConstraint : ConstraintClause;

/// <summary>
/// Value type constraint: T: struct
/// </summary>
public record StructConstraint : ConstraintClause;

/// <summary>
/// Constructor constraint: T: new()
/// </summary>
public record NewConstraint : ConstraintClause;

/// <summary>
/// Decorator applied to function/class/struct
/// </summary>
public record Decorator
{
    public string Name { get; init; } = "";
    // Note: v0.3 only supports simple identifier decorators
    // No arguments or dotted names in v0.3

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }

    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// </summary>
    public Text.TextSpan? Span { get; init; }
}

/// <summary>
/// Function/method parameter
/// </summary>
public record Parameter
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
    public Expression? DefaultValue { get; init; }
    /// <summary>
    /// True if this parameter is variadic (*args). Maps to C# params T[].
    /// </summary>
    public bool IsVariadic { get; init; }

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }

    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// </summary>
    public Text.TextSpan? Span { get; init; }
}

#endregion

#region Property Definitions

/// <summary>
/// Property accessor type
/// </summary>
public enum PropertyAccessor { None, Get, Set, Init }

/// <summary>
/// Property definition (property name: type = default or property get name(self) -> type: body)
/// </summary>
public record PropertyDef : Statement
{
    public string Name { get; init; } = "";
    public PropertyAccessor Accessor { get; init; } = PropertyAccessor.None;
    public TypeAnnotation? Type { get; init; }
    public Expression? DefaultValue { get; init; }
    public bool IsFunctionStyle { get; init; }
    public ImmutableArray<Parameter> Parameters { get; init; } = ImmutableArray<Parameter>.Empty;
    public TypeAnnotation? ReturnType { get; init; }
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;
    public string? ExplicitInterface { get; init; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Name), "PropertyDef.Name cannot be null or empty");
    }

    /// <inheritdoc/>
    public override IEnumerable<Node> GetChildNodes()
    {
        if (DefaultValue != null)
            yield return DefaultValue;
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

#region Import Statements

/// <summary>
/// Import statement (import module, import module as alias)
/// </summary>
public record ImportStatement : Statement
{
    public ImmutableArray<ImportAlias> Names { get; init; } = ImmutableArray<ImportAlias>.Empty;

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(Names != null, "ImportStatement.Names cannot be null");
    }
}

public record ImportAlias
{
    public string Name { get; init; } = "";  // module.submodule
    public string? AsName { get; init; }  // Optional alias

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }

    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// </summary>
    public Text.TextSpan? Span { get; init; }
}

/// <summary>
/// From-import statement (from module import name1, name2)
/// </summary>
public record FromImportStatement : Statement
{
    public string Module { get; init; } = "";
    public ImmutableArray<ImportAlias> Names { get; init; } = ImmutableArray<ImportAlias>.Empty;
    public bool ImportAll { get; init; }  // from module import *

    /// <summary>
    /// The resolved module path relative to the project root, set during semantic analysis.
    /// For example, ".helpers" in package "mypackage" resolves to "mypackage.helpers".
    /// This is used during code generation to generate correct namespace references.
    /// </summary>
    public string? ResolvedModulePath { get; set; }

    /// <summary>
    /// Symbols that are re-exported from this from-import statement, set during semantic analysis.
    /// Maps the local name (possibly aliased) to the symbol information.
    /// This is used during code generation to generate delegating members in the module class.
    /// </summary>
    public Dictionary<string, Semantic.Symbol>? ReExportedSymbols { get; set; }

    /// <inheritdoc/>
    public override void ValidateInvariants()
    {
        base.ValidateInvariants();
        Debug.Assert(!string.IsNullOrEmpty(Module), "FromImportStatement.Module cannot be null or empty");
        Debug.Assert(Names != null, "FromImportStatement.Names cannot be null");
    }
}

#endregion
