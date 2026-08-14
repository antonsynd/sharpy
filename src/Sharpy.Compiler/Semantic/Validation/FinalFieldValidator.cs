using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates @final field assignment restrictions.
///
/// Rules:
/// - A field declared with @final can only be assigned inside the declaring type's __init__.
///   Assignments in other methods, properties, or nested functions are forbidden (SPY0440).
/// - A derived class may not assign a @final field declared on a base class, even inside
///   its own __init__ (SPY0440). The base class is the only place that may initialize it.
/// - For structs, assignment in any __init__ overload of the declaring type is allowed.
/// - The @final decorator is not valid on local variables (SPY0441). Use a plain const
///   instead. Module-level @final variables are already rejected by DecoratorValidator.
/// </summary>
internal class FinalFieldValidator : SemanticValidatorBase
{
    public override string Name => "FinalFieldValidator";
    public override int Order => 411; // After PropertyValidator (410), before EventValidator (412)

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        _logger.LogDebug("Starting @final field validation");

        // Validate module-level statements (and walk nested function bodies for @final on locals)
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
                ValidateTypeBody(classDef, classDef.Name, classDef.Body, isStruct: false, context);
                break;
            case StructDef structDef:
                ValidateTypeBody(structDef, structDef.Name, structDef.Body, isStruct: true, context);
                break;
            case FunctionDef funcDef:
                // Module-level function — scan body for @final on locals.
                WalkForFinalLocalDeclarations(funcDef.Body, context);
                break;
        }
    }

    /// <summary>
    /// Validates one type's @final field rules, and recurses into the types nested inside it.
    ///
    /// <para>The recursion was already here and already reached nested declarations — what it could
    /// not do was RESOLVE one. The bare-name symbol lookup returned null for every nested type, and
    /// a null symbol here means an empty @final field set, so the recursion ran the rules against
    /// nothing (#1461). This is the failure shape the issue names: a walk that arrives and a lookup
    /// that cannot answer look exactly like a passing check.</para>
    /// </summary>
    private void ValidateTypeBody(Statement declaration, string typeName, IReadOnlyList<Statement> body, bool isStruct, SemanticContext context)
    {
        // Look up the declaring type symbol. It carries the field IsFinal flags set during name
        // resolution. Through NestedTypes, not by bare name — the bare lookup cannot see a nested
        // type, and a null symbol here means an empty field set, i.e. no rules (#1461).
        var typeSymbol = context.LookupDeclaredType(declaration, typeName);

        // Collect @final field names declared *directly* on this type.
        var ownFinalFields = new HashSet<string>();
        if (typeSymbol != null)
        {
            foreach (var field in typeSymbol.Fields)
            {
                if (field.IsFinal)
                    ownFinalFields.Add(field.Name);
            }
        }

        // Collect @final field names inherited from base classes (still off-limits in derived __init__).
        var inheritedFinalFields = new HashSet<string>();
        if (typeSymbol != null && !isStruct)
        {
            foreach (var baseType in TypeHierarchyService.GetAllBaseTypes(typeSymbol, context.SemanticBinding))
            {
                foreach (var field in baseType.Fields)
                {
                    if (field.IsFinal && !ownFinalFields.Contains(field.Name))
                        inheritedFinalFields.Add(field.Name);
                }
            }
        }

        foreach (var member in body)
        {
            switch (member)
            {
                case FunctionDef method:
                    {
                        bool isConstructor = method.Name == DunderNames.Init;
                        var forbidden = isConstructor
                            ? inheritedFinalFields                 // in __init__: only inherited finals are off-limits
                            : Union(ownFinalFields, inheritedFinalFields); // elsewhere: all finals are off-limits

                        if (forbidden.Count > 0)
                        {
                            WalkForFinalAssignments(method.Body, forbidden, typeName, context);
                        }

                        // Also scan method bodies for @final on local variable declarations.
                        WalkForFinalLocalDeclarations(method.Body, context);
                        break;
                    }
                case PropertyDef propDef:
                    {
                        var forbidden = Union(ownFinalFields, inheritedFinalFields);
                        if (forbidden.Count > 0)
                        {
                            WalkForFinalAssignments(propDef.Body, forbidden, typeName, context);
                        }
                        WalkForFinalLocalDeclarations(propDef.Body, context);
                        break;
                    }
                case EventDef eventDef:
                    {
                        var forbidden = Union(ownFinalFields, inheritedFinalFields);
                        if (forbidden.Count > 0)
                        {
                            WalkForFinalAssignments(eventDef.Body, forbidden, typeName, context);
                        }
                        WalkForFinalLocalDeclarations(eventDef.Body, context);
                        break;
                    }
                case ClassDef nestedClass:
                    ValidateTypeBody(nestedClass, nestedClass.Name, nestedClass.Body, isStruct: false, context);
                    break;
                case StructDef nestedStruct:
                    ValidateTypeBody(nestedStruct, nestedStruct.Name, nestedStruct.Body, isStruct: true, context);
                    break;
            }
        }
    }

    private void WalkForFinalAssignments(
        IReadOnlyList<Statement> statements,
        HashSet<string> forbiddenFields,
        string typeName,
        SemanticContext context)
    {
        foreach (var stmt in statements)
        {
            if (stmt is Assignment assign
                && assign.Target is MemberAccess { Object: Identifier { Name: PythonNames.Self }, Member: var member }
                && forbiddenFields.Contains(member))
            {
                AddError(context,
                    $"Cannot assign to @final field '{member}' outside of '{typeName}' constructor",
                    assign.LineStart, assign.ColumnStart,
                    code: DiagnosticCodes.Validation.FinalFieldAssignmentOutsideConstructor,
                    span: assign.Span);
            }

            foreach (var child in GetChildStatements(stmt))
            {
                WalkForFinalAssignments(child, forbiddenFields, typeName, context);
            }
        }
    }

    /// <summary>
    /// Reports SPY0441 when @final appears on a VariableDeclaration nested inside a function body
    /// (i.e., a local variable, not a class/struct field).
    /// </summary>
    private void WalkForFinalLocalDeclarations(IReadOnlyList<Statement> statements, SemanticContext context)
    {
        foreach (var stmt in statements)
        {
            if (stmt is VariableDeclaration varDecl)
            {
                var finalDecorator = varDecl.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Final);
                if (finalDecorator != null)
                {
                    AddError(context,
                        $"Decorator '@final' is not valid on local variable '{varDecl.Name}'. " +
                        "The @final decorator may only be applied to class or struct fields.",
                        finalDecorator.LineStart, finalDecorator.ColumnStart,
                        code: DiagnosticCodes.Validation.FinalOnLocalVariable,
                        span: finalDecorator.Span);
                }
            }

            foreach (var child in GetChildStatements(stmt))
            {
                WalkForFinalLocalDeclarations(child, context);
            }
        }
    }

    private static HashSet<string> Union(HashSet<string> a, HashSet<string> b)
    {
        if (b.Count == 0)
            return a;
        if (a.Count == 0)
            return b;
        var combined = new HashSet<string>(a);
        combined.UnionWith(b);
        return combined;
    }

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
                // @suppress wrapper (#1024): suppression is warning-only metadata; the final-field
                // assignment *error* inside must still be found (errors are never suppressible).
                yield return new[] { decorated.Statement };
                break;
        }
    }
}
