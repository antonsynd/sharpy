using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Warns about identifiers with consecutive underscores (e.g., <c>foo__bar</c>),
/// which may cause name mangling collisions or be passed through as unrecognized forms.
/// Exempts dunder names (<c>__init__</c>) and backtick-escaped literals (<c>`foo__bar`</c>).
/// </summary>
internal sealed class NamingConventionValidator : SemanticValidatorBase
{
    public override string Name => "NamingConvention";
    public override int Order => 55; // After ModuleLevelValidator (50), before DecoratorValidator (60)

    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;

        foreach (var stmt in module.Body)
        {
            ValidateStatement(stmt);
        }
    }

    private void ValidateStatement(Statement stmt)
    {
        switch (stmt)
        {
            case FunctionDef funcDef:
                ValidateFunction(funcDef);
                break;
            case ClassDef classDef:
                ValidateClass(classDef);
                break;
            case StructDef structDef:
                ValidateStruct(structDef);
                break;
            case InterfaceDef interfaceDef:
                ValidateInterface(interfaceDef);
                break;
            case EnumDef enumDef:
                ValidateEnum(enumDef);
                break;
            case UnionDef unionDef:
                ValidateUnion(unionDef);
                break;
            case VariableDeclaration varDecl:
                CheckName(varDecl.Name, varDecl.LineStart, varDecl.ColumnStart, varDecl.Span);
                break;
            case PropertyDef propDef:
                CheckName(propDef.Name, propDef.LineStart, propDef.ColumnStart, propDef.Span);
                break;
        }
    }

    private void ValidateFunction(FunctionDef funcDef)
    {
        CheckName(funcDef.Name, funcDef.LineStart, funcDef.ColumnStart, funcDef.Span);
        ValidateParameters(funcDef.Parameters);
        ValidateBody(funcDef.Body);
    }

    private void ValidateClass(ClassDef classDef)
    {
        CheckName(classDef.Name, classDef.LineStart, classDef.ColumnStart, classDef.Span);

        foreach (var member in classDef.Body)
        {
            switch (member)
            {
                case FunctionDef method:
                    ValidateFunction(method);
                    break;
                case VariableDeclaration field:
                    CheckName(field.Name, field.LineStart, field.ColumnStart, field.Span);
                    break;
            }
        }
    }

    private void ValidateStruct(StructDef structDef)
    {
        CheckName(structDef.Name, structDef.LineStart, structDef.ColumnStart, structDef.Span);

        foreach (var member in structDef.Body)
        {
            switch (member)
            {
                case FunctionDef method:
                    ValidateFunction(method);
                    break;
                case VariableDeclaration field:
                    CheckName(field.Name, field.LineStart, field.ColumnStart, field.Span);
                    break;
            }
        }
    }

    private void ValidateInterface(InterfaceDef interfaceDef)
    {
        CheckName(interfaceDef.Name, interfaceDef.LineStart, interfaceDef.ColumnStart, interfaceDef.Span);

        foreach (var member in interfaceDef.Body)
        {
            if (member is FunctionDef method)
            {
                CheckName(method.Name, method.LineStart, method.ColumnStart, method.Span);
                ValidateParameters(method.Parameters);
            }
        }
    }

    private void ValidateEnum(EnumDef enumDef)
    {
        CheckName(enumDef.Name, enumDef.LineStart, enumDef.ColumnStart, enumDef.Span);

        foreach (var member in enumDef.Members)
        {
            CheckName(member.Name, member.LineStart, member.ColumnStart, member.Span);
        }
    }

    private void ValidateUnion(UnionDef unionDef)
    {
        CheckName(unionDef.Name, unionDef.LineStart, unionDef.ColumnStart, unionDef.Span);

        foreach (var caseDef in unionDef.Cases)
        {
            CheckName(caseDef.Name, caseDef.LineStart, caseDef.ColumnStart, caseDef.Span);

            foreach (var field in caseDef.Fields)
            {
                if (!string.IsNullOrEmpty(field.Name))
                {
                    CheckName(field.Name, field.LineStart, field.ColumnStart, field.Span);
                }
            }
        }
    }

    private void ValidateParameters(System.Collections.Immutable.ImmutableArray<Parameter> parameters)
    {
        foreach (var param in parameters)
        {
            CheckName(param.Name, param.LineStart, param.ColumnStart, param.Span);
        }
    }

    private void ValidateBody(System.Collections.Immutable.ImmutableArray<Statement> body)
    {
        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case VariableDeclaration varDecl:
                    CheckName(varDecl.Name, varDecl.LineStart, varDecl.ColumnStart, varDecl.Span);
                    break;
                case FunctionDef funcDef:
                    ValidateFunction(funcDef);
                    break;
                case ClassDef classDef:
                    ValidateClass(classDef);
                    break;
                case IfStatement ifStmt:
                    ValidateBody(ifStmt.ThenBody);
                    foreach (var elif in ifStmt.ElifClauses)
                        ValidateBody(elif.Body);
                    if (ifStmt.ElseBody.Length > 0)
                        ValidateBody(ifStmt.ElseBody);
                    break;
                case WhileStatement whileStmt:
                    ValidateBody(whileStmt.Body);
                    if (whileStmt.ElseBody.Length > 0)
                        ValidateBody(whileStmt.ElseBody);
                    break;
                case ForStatement forStmt:
                    CheckForTarget(forStmt.Target);
                    ValidateBody(forStmt.Body);
                    if (forStmt.ElseBody.Length > 0)
                        ValidateBody(forStmt.ElseBody);
                    break;
                case TryStatement tryStmt:
                    ValidateBody(tryStmt.Body);
                    foreach (var handler in tryStmt.Handlers)
                    {
                        if (handler.Name != null)
                            CheckName(handler.Name, handler.LineStart, handler.ColumnStart, handler.Span);
                        ValidateBody(handler.Body);
                    }
                    if (tryStmt.ElseBody.Length > 0)
                        ValidateBody(tryStmt.ElseBody);
                    if (tryStmt.FinallyBody.Length > 0)
                        ValidateBody(tryStmt.FinallyBody);
                    break;
                case WithStatement withStmt:
                    foreach (var item in withStmt.Items)
                    {
                        if (item.Name != null)
                            CheckName(item.Name, item.LineStart, item.ColumnStart, item.Span);
                    }
                    ValidateBody(withStmt.Body);
                    break;
            }
        }
    }

    private void CheckForTarget(Expression target)
    {
        switch (target)
        {
            case Identifier id:
                CheckName(id.Name, id.LineStart, id.ColumnStart, id.Span);
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    CheckForTarget(element);
                break;
        }
    }

    /// <summary>
    /// Checks a single name for consecutive underscores and emits SPY0453 if found.
    /// Skips dunder names and backtick-escaped literals.
    /// </summary>
    private void CheckName(string name, int line, int column, TextSpan? span)
    {
        if (string.IsNullOrEmpty(name))
            return;

        // Skip backtick-escaped literals
        if (name.StartsWith("`") && name.EndsWith("`"))
            return;

        // Skip dunder names (__init__, __str__, etc.)
        if (name.StartsWith("__") && name.EndsWith("__") && name.Length > 4)
            return;

        // Strip _ or __ prefix for the body check
        var body = name;
        if (body.StartsWith("__"))
            body = body[2..];
        else if (body.StartsWith("_"))
            body = body[1..];

        // Strip trailing underscores
        body = body.TrimEnd('_');

        if (string.IsNullOrEmpty(body))
            return;

        if (NameFormDetector.HasConsecutiveUnderscores(body))
        {
            AddWarning(_context,
                $"Identifier '{name}' contains consecutive underscores, which may cause name mangling collisions. Use backtick escaping or rename.",
                line, column,
                code: DiagnosticCodes.Validation.NamingConventionWarning,
                span: span);
        }
    }
}
