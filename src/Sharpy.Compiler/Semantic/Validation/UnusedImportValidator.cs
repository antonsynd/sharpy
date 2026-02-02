using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Warns about imported names that are never referenced in the module.
/// Skips wildcard imports (from X import *) since tracking individual usage is complex.
/// </summary>
internal class UnusedImportValidator : SemanticValidatorBase
{
    public override string Name => "UnusedImportValidator";
    public override int Order => 430;

    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;

        // Collect all imported names
        var importedNames = new Dictionary<string, ImportInfo>();

        // Collect all referenced identifiers from non-import code
        var referencedNames = new HashSet<string>();

        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case FromImportStatement fromImport:
                    if (!fromImport.ImportAll)
                    {
                        foreach (var alias in fromImport.Names)
                        {
                            var localName = alias.AsName ?? alias.Name;
                            importedNames[localName] = new ImportInfo(
                                alias.Name, localName, alias.LineStart, alias.ColumnStart, alias.Span);
                        }
                    }
                    break;

                case ImportStatement import:
                    foreach (var alias in import.Names)
                    {
                        var localName = alias.AsName ?? GetTopLevelName(alias.Name);
                        importedNames[localName] = new ImportInfo(
                            alias.Name, localName, alias.LineStart, alias.ColumnStart, alias.Span);
                    }
                    break;

                default:
                    // Non-import statement - collect referenced identifiers
                    CollectReferencesFromStatement(stmt, referencedNames);
                    break;
            }
        }

        // Emit warnings for unused imports
        foreach (var (localName, info) in importedNames)
        {
            if (!referencedNames.Contains(localName))
            {
                AddWarning(_context,
                    $"Imported name '{info.OriginalName}' is never used",
                    info.Line, info.Column,
                    code: DiagnosticCodes.Validation.UnusedImport,
                    span: info.Span);
            }
        }
    }

    /// <summary>
    /// For dotted import names like "geometry.shapes", get the top-level part ("geometry").
    /// </summary>
    private static string GetTopLevelName(string dottedName)
    {
        var dotIndex = dottedName.IndexOf('.');
        return dotIndex >= 0 ? dottedName[..dotIndex] : dottedName;
    }

    private void CollectReferencesFromStatement(Statement stmt, HashSet<string> refs)
    {
        switch (stmt)
        {
            case VariableDeclaration varDecl:
                if (varDecl.Type != null)
                    CollectReferencesFromTypeAnnotation(varDecl.Type, refs);
                if (varDecl.InitialValue != null)
                    CollectReferencesFromExpression(varDecl.InitialValue, refs);
                break;

            case Assignment assign:
                CollectReferencesFromExpression(assign.Target, refs);
                CollectReferencesFromExpression(assign.Value, refs);
                break;

            case ReturnStatement ret:
                if (ret.Value != null)
                    CollectReferencesFromExpression(ret.Value, refs);
                break;

            case ExpressionStatement exprStmt:
                CollectReferencesFromExpression(exprStmt.Expression, refs);
                break;

            case IfStatement ifStmt:
                CollectReferencesFromExpression(ifStmt.Test, refs);
                foreach (var s in ifStmt.ThenBody)
                    CollectReferencesFromStatement(s, refs);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    CollectReferencesFromExpression(elif.Test, refs);
                    foreach (var s in elif.Body)
                        CollectReferencesFromStatement(s, refs);
                }
                foreach (var s in ifStmt.ElseBody)
                    CollectReferencesFromStatement(s, refs);
                break;

            case WhileStatement whileStmt:
                CollectReferencesFromExpression(whileStmt.Test, refs);
                foreach (var s in whileStmt.Body)
                    CollectReferencesFromStatement(s, refs);
                foreach (var s in whileStmt.ElseBody)
                    CollectReferencesFromStatement(s, refs);
                break;

            case ForStatement forStmt:
                CollectReferencesFromExpression(forStmt.Target, refs);
                CollectReferencesFromExpression(forStmt.Iterator, refs);
                foreach (var s in forStmt.Body)
                    CollectReferencesFromStatement(s, refs);
                foreach (var s in forStmt.ElseBody)
                    CollectReferencesFromStatement(s, refs);
                break;

            case TryStatement tryStmt:
                foreach (var s in tryStmt.Body)
                    CollectReferencesFromStatement(s, refs);
                foreach (var handler in tryStmt.Handlers)
                {
                    if (handler.ExceptionType != null)
                        CollectReferencesFromTypeAnnotation(handler.ExceptionType, refs);
                    foreach (var s in handler.Body)
                        CollectReferencesFromStatement(s, refs);
                }
                foreach (var s in tryStmt.ElseBody)
                    CollectReferencesFromStatement(s, refs);
                foreach (var s in tryStmt.FinallyBody)
                    CollectReferencesFromStatement(s, refs);
                break;

            case RaiseStatement raiseStmt:
                if (raiseStmt.Exception != null)
                    CollectReferencesFromExpression(raiseStmt.Exception, refs);
                break;

            case AssertStatement assertStmt:
                CollectReferencesFromExpression(assertStmt.Test, refs);
                if (assertStmt.Message != null)
                    CollectReferencesFromExpression(assertStmt.Message, refs);
                break;

            case FunctionDef func:
                // Check decorators (imported names used as decorators)
                foreach (var decorator in func.Decorators)
                    refs.Add(decorator.Name);
                // Check parameter type annotations and return type
                foreach (var param in func.Parameters)
                {
                    if (param.Type != null)
                        CollectReferencesFromTypeAnnotation(param.Type, refs);
                    if (param.DefaultValue != null)
                        CollectReferencesFromExpression(param.DefaultValue, refs);
                }
                if (func.ReturnType != null)
                    CollectReferencesFromTypeAnnotation(func.ReturnType, refs);
                foreach (var s in func.Body)
                    CollectReferencesFromStatement(s, refs);
                break;

            case ClassDef cls:
                // Check decorators
                foreach (var decorator in cls.Decorators)
                    refs.Add(decorator.Name);
                // Check base classes and interfaces
                foreach (var baseType in cls.BaseClasses)
                    CollectReferencesFromTypeAnnotation(baseType, refs);
                foreach (var s in cls.Body)
                    CollectReferencesFromStatement(s, refs);
                break;

            case StructDef str:
                // Check decorators
                foreach (var decorator in str.Decorators)
                    refs.Add(decorator.Name);
                foreach (var baseType in str.BaseClasses)
                    CollectReferencesFromTypeAnnotation(baseType, refs);
                foreach (var s in str.Body)
                    CollectReferencesFromStatement(s, refs);
                break;

            case InterfaceDef iface:
                foreach (var baseIface in iface.BaseInterfaces)
                    CollectReferencesFromTypeAnnotation(baseIface, refs);
                foreach (var s in iface.Body)
                    CollectReferencesFromStatement(s, refs);
                break;

            case TypeAlias typeAlias:
                if (typeAlias.Type != null)
                    CollectReferencesFromTypeAnnotation(typeAlias.Type, refs);
                break;

            case EnumDef enumDef:
                foreach (var member in enumDef.Members)
                {
                    if (member.Value != null)
                        CollectReferencesFromExpression(member.Value, refs);
                }
                break;
        }
    }

    private void CollectReferencesFromExpression(Expression expr, HashSet<string> refs)
    {
        switch (expr)
        {
            case Identifier id:
                refs.Add(id.Name);
                break;

            case BinaryOp binOp:
                CollectReferencesFromExpression(binOp.Left, refs);
                CollectReferencesFromExpression(binOp.Right, refs);
                break;

            case UnaryOp unaryOp:
                CollectReferencesFromExpression(unaryOp.Operand, refs);
                break;

            case FunctionCall call:
                CollectReferencesFromExpression(call.Function, refs);
                foreach (var arg in call.Arguments)
                    CollectReferencesFromExpression(arg, refs);
                foreach (var kwarg in call.KeywordArguments)
                    CollectReferencesFromExpression(kwarg.Value, refs);
                break;

            case MemberAccess memberAccess:
                CollectReferencesFromExpression(memberAccess.Object, refs);
                break;

            case IndexAccess indexAccess:
                CollectReferencesFromExpression(indexAccess.Object, refs);
                CollectReferencesFromExpression(indexAccess.Index, refs);
                break;

            case SliceAccess sliceAccess:
                CollectReferencesFromExpression(sliceAccess.Object, refs);
                if (sliceAccess.Start != null)
                    CollectReferencesFromExpression(sliceAccess.Start, refs);
                if (sliceAccess.Stop != null)
                    CollectReferencesFromExpression(sliceAccess.Stop, refs);
                if (sliceAccess.Step != null)
                    CollectReferencesFromExpression(sliceAccess.Step, refs);
                break;

            case ListLiteral listLit:
                foreach (var elem in listLit.Elements)
                    CollectReferencesFromExpression(elem, refs);
                break;

            case DictLiteral dictLit:
                foreach (var entry in dictLit.Entries)
                {
                    CollectReferencesFromExpression(entry.Key, refs);
                    CollectReferencesFromExpression(entry.Value, refs);
                }
                break;

            case SetLiteral setLit:
                foreach (var elem in setLit.Elements)
                    CollectReferencesFromExpression(elem, refs);
                break;

            case TupleLiteral tupleLit:
                foreach (var elem in tupleLit.Elements)
                    CollectReferencesFromExpression(elem, refs);
                break;

            case ConditionalExpression condExpr:
                CollectReferencesFromExpression(condExpr.Test, refs);
                CollectReferencesFromExpression(condExpr.ThenValue, refs);
                CollectReferencesFromExpression(condExpr.ElseValue, refs);
                break;

            case FStringLiteral fStr:
                foreach (var part in fStr.Parts)
                {
                    if (part.Expression != null)
                        CollectReferencesFromExpression(part.Expression, refs);
                }
                break;

            case ListComprehension listComp:
                CollectReferencesFromExpression(listComp.Element, refs);
                foreach (var clause in listComp.Clauses)
                    CollectReferencesFromClause(clause, refs);
                break;

            case SetComprehension setComp:
                CollectReferencesFromExpression(setComp.Element, refs);
                foreach (var clause in setComp.Clauses)
                    CollectReferencesFromClause(clause, refs);
                break;

            case DictComprehension dictComp:
                CollectReferencesFromExpression(dictComp.Key, refs);
                CollectReferencesFromExpression(dictComp.Value, refs);
                foreach (var clause in dictComp.Clauses)
                    CollectReferencesFromClause(clause, refs);
                break;

            case LambdaExpression lambda:
                CollectReferencesFromExpression(lambda.Body, refs);
                break;

            case TypeCast castExpr:
                CollectReferencesFromExpression(castExpr.Value, refs);
                CollectReferencesFromTypeAnnotation(castExpr.TargetType, refs);
                break;

            case TypeCoercion coercion:
                CollectReferencesFromExpression(coercion.Value, refs);
                CollectReferencesFromTypeAnnotation(coercion.TargetType, refs);
                break;

            case TypeCheck typeCheck:
                CollectReferencesFromExpression(typeCheck.Value, refs);
                CollectReferencesFromTypeAnnotation(typeCheck.CheckType, refs);
                break;

            case ComparisonChain chain:
                foreach (var operand in chain.Operands)
                    CollectReferencesFromExpression(operand, refs);
                break;

            case Parenthesized paren:
                CollectReferencesFromExpression(paren.Expression, refs);
                break;

            case TryExpression tryExpr:
                CollectReferencesFromExpression(tryExpr.Operand, refs);
                if (tryExpr.ExceptionType != null)
                    CollectReferencesFromTypeAnnotation(tryExpr.ExceptionType, refs);
                break;

            case MaybeExpression maybeExpr:
                CollectReferencesFromExpression(maybeExpr.Operand, refs);
                break;

            case WalrusExpression walrus:
                CollectReferencesFromExpression(walrus.Value, refs);
                break;

            // Literals - no references
            case IntegerLiteral:
            case FloatLiteral:
            case StringLiteral:
            case BooleanLiteral:
            case NoneLiteral:
            case EllipsisLiteral:
            case SuperExpression:
                break;
        }
    }

    private void CollectReferencesFromClause(ComprehensionClause clause, HashSet<string> refs)
    {
        if (clause is ForClause forClause)
        {
            CollectReferencesFromExpression(forClause.Iterator, refs);
        }
        else if (clause is IfClause ifClause)
        {
            CollectReferencesFromExpression(ifClause.Condition, refs);
        }
    }

    /// <summary>
    /// Collect references from type annotations (e.g., parameter types, return types, variable types).
    /// Imported types used in annotations should count as used.
    /// </summary>
    private void CollectReferencesFromTypeAnnotation(TypeAnnotation typeAnnotation, HashSet<string> refs)
    {
        // Type annotations reference type names which may be imported
        // The type name is the first identifier in the annotation
        var typeName = typeAnnotation.Name;
        if (!string.IsNullOrEmpty(typeName))
        {
            // For generic types like list[int], the Name is "list"
            // For imported types like "Rectangle", the Name is "Rectangle"
            refs.Add(typeName);
        }

        // Recurse into generic type arguments
        foreach (var typeArg in typeAnnotation.TypeArguments)
        {
            CollectReferencesFromTypeAnnotation(typeArg, refs);
        }
    }

    private record ImportInfo(string OriginalName, string LocalName, int? Line, int? Column, Text.TextSpan? Span);
}
