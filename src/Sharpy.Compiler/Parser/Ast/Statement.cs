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
}

/// <summary>
/// Assignment statement (x = value, x += value, etc.)
/// </summary>
public record Assignment : Statement
{
    public Expression Target { get; init; } = null!;
    public Expression Value { get; init; } = null!;
    public AssignmentOperator Operator { get; init; } = AssignmentOperator.Assign;
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
    RightShiftAssign  // >>=
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
}

/// <summary>
/// Assert statement (assert condition, message)
/// </summary>
public record AssertStatement : Statement
{
    public Expression Test { get; init; } = null!;
    public Expression? Message { get; init; }
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
}

/// <summary>
/// Raise statement (raise exception or raise)
/// </summary>
public record RaiseStatement : Statement
{
    public Expression? Exception { get; init; }
    public Expression? Cause { get; init; }  // raise ... from cause
}

#endregion

#region Compound Statements

/// <summary>
/// If statement with optional elif and else branches
/// </summary>
public record IfStatement : Statement
{
    public Expression Test { get; init; } = null!;
    public List<Statement> ThenBody { get; init; } = new();
    public List<ElifClause> ElifClauses { get; init; } = new();
    public List<Statement> ElseBody { get; init; } = new();
}

public record ElifClause
{
    public Expression Test { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}

/// <summary>
/// While loop with optional else clause (runs if loop completes without break)
/// </summary>
public record WhileStatement : Statement
{
    public Expression Test { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
    public List<Statement> ElseBody { get; init; } = new();
}

/// <summary>
/// For loop (for item in iterable:) with optional else clause (runs if loop completes without break)
/// </summary>
public record ForStatement : Statement
{
    public Expression Target { get; init; } = null!;  // Loop variable(s)
    public Expression Iterator { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
    public List<Statement> ElseBody { get; init; } = new();
}

/// <summary>
/// Try-except-else-finally statement
/// </summary>
public record TryStatement : Statement
{
    public List<Statement> Body { get; init; } = new();
    public List<ExceptHandler> Handlers { get; init; } = new();
    public List<Statement> ElseBody { get; init; } = new();
    public List<Statement> FinallyBody { get; init; } = new();
}

public record ExceptHandler
{
    public TypeAnnotation? ExceptionType { get; init; }
    public string? Name { get; init; }  // except Exception as e:
    public List<Statement> Body { get; init; } = new();

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}

#endregion

#region Definitions

/// <summary>
/// Function definition
/// </summary>
public record FunctionDef : Statement
{
    public string Name { get; init; } = "";
    public List<Parameter> Parameters { get; init; } = new();
    public TypeAnnotation? ReturnType { get; init; }
    public List<Statement> Body { get; init; } = new();
    public List<Decorator> Decorators { get; init; } = new();
    public string? DocString { get; init; }
}

/// <summary>
/// Class definition
/// </summary>
public record ClassDef : Statement
{
    public string Name { get; init; } = "";
    public List<string> TypeParameters { get; init; } = new();
    public List<TypeAnnotation> BaseClasses { get; init; } = new();
    public List<Statement> Body { get; init; } = new();
    public List<Decorator> Decorators { get; init; } = new();
    public string? DocString { get; init; }
}

/// <summary>
/// Struct definition (value type)
/// </summary>
public record StructDef : Statement
{
    public string Name { get; init; } = "";
    public List<string> TypeParameters { get; init; } = new();
    public List<TypeAnnotation> BaseClasses { get; init; } = new();  // Interfaces only
    public List<Statement> Body { get; init; } = new();
    public List<Decorator> Decorators { get; init; } = new();
    public string? DocString { get; init; }
}

/// <summary>
/// Interface definition
/// </summary>
public record InterfaceDef : Statement
{
    public string Name { get; init; } = "";
    public List<string> TypeParameters { get; init; } = new();
    public List<TypeAnnotation> BaseInterfaces { get; init; } = new();
    public List<Statement> Body { get; init; } = new();
    public string? DocString { get; init; }
}

/// <summary>
/// Enum definition (simple enums only in v0.5)
/// </summary>
public record EnumDef : Statement
{
    public string Name { get; init; } = "";
    public List<EnumMember> Members { get; init; } = new();
    public string? DocString { get; init; }
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
}

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
}

/// <summary>
/// Function/method parameter
/// </summary>
public record Parameter
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
    public Expression? DefaultValue { get; init; }

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}

#endregion

#region Import Statements

/// <summary>
/// Import statement (import module, import module as alias)
/// </summary>
public record ImportStatement : Statement
{
    public List<ImportAlias> Names { get; init; } = new();
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
}

/// <summary>
/// From-import statement (from module import name1, name2)
/// </summary>
public record FromImportStatement : Statement
{
    public string Module { get; init; } = "";
    public List<ImportAlias> Names { get; init; } = new();
    public bool ImportAll { get; init; }  // from module import *
}

#endregion
