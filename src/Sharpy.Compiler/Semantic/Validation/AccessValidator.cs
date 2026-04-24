using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates access level rules in Sharpy code:
/// - Private members (__name) only accessible within the same class
/// - Protected members (_name) only accessible within class hierarchy
/// - Public members accessible everywhere
///
/// This is the pipeline-compatible version of AccessValidator.
/// Unlike the legacy version which is called during expression type-checking,
/// this validator performs a post-pass over the AST.
/// </summary>
internal class AccessValidator : ValidatingAstWalker
{
    public override string Name => "AccessValidator";
    public override int Order => 450; // After control flow (400)

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        _logger.LogDebug("Starting access validation");
        base.Validate(module, context);
    }

    public override void VisitClassDef(ClassDef node)
    {
        var classSymbol = Context.SymbolTable.LookupType(node.Name)
            ?? Context.Traversal.CurrentClass?.NestedTypes.FirstOrDefault(n => n.Name == node.Name);
        using (Context.Traversal.EnterClass(classSymbol))
        {
            base.VisitClassDef(node);
        }
    }

    public override void VisitStructDef(StructDef node)
    {
        var structSymbol = Context.SymbolTable.LookupType(node.Name)
            ?? Context.Traversal.CurrentClass?.NestedTypes.FirstOrDefault(n => n.Name == node.Name);
        using (Context.Traversal.EnterClass(structSymbol))
        {
            base.VisitStructDef(node);
        }
    }

    public override void VisitMemberAccess(MemberAccess node)
    {
        ValidateMemberAccess(node);
        base.VisitMemberAccess(node);
    }

    private void ValidateMemberAccess(MemberAccess memberAccess)
    {
        // Get the type of the object being accessed
        var objectType = Context.SemanticInfo.GetExpressionType(memberAccess.Object);
        if (objectType == null)
            return;

        // Get the owning type symbol
        TypeSymbol? owningType = null;
        if (objectType is UserDefinedType udt)
        {
            owningType = udt.Symbol ?? Context.SymbolTable.LookupType(udt.Name);
        }

        if (owningType == null)
            return;

        ValidateMemberAccess(memberAccess.Member, owningType,
            memberAccess.LineStart, memberAccess.ColumnStart, memberAccess.Span);
    }

    private void ValidateMemberAccess(string memberName, TypeSymbol owningType, int? lineStart, int? columnStart,
        TextSpan? span = null)
    {
        // Try to find the member symbol for explicit access level override
        var memberSymbol = FindMemberSymbol(memberName, owningType);
        var accessLevel = DetermineAccessLevel(memberName, memberSymbol);

        switch (accessLevel)
        {
            case AccessLevel.Private:
                if (Context.Traversal.CurrentClass != owningType &&
                    !IsNestedWithin(Context.Traversal.CurrentClass, owningType))
                {
                    AddError(
                        $"Cannot access private member '{memberName}' of '{owningType.Name}' from outside the class",
                        lineStart, columnStart, code: DiagnosticCodes.Semantic.AccessViolation,
                        span: span);
                }
                break;

            case AccessLevel.Protected:
                if (Context.Traversal.CurrentClass == null ||
                    (!IsInHierarchy(Context.Traversal.CurrentClass, owningType) &&
                     !IsNestedWithin(Context.Traversal.CurrentClass, owningType)))
                {
                    AddError(
                        $"Cannot access protected member '{memberName}' of '{owningType.Name}' from outside the class hierarchy",
                        lineStart, columnStart, code: DiagnosticCodes.Semantic.AccessViolation,
                        span: span);
                }
                break;

            case AccessLevel.Public:
                // Public members accessible everywhere
                break;
        }
    }

    private AccessLevel DetermineAccessLevel(string name, Symbol? symbol = null)
    {
        // If the symbol has an explicit access level from a decorator, use it
        if (symbol?.ExplicitAccessLevel != null)
            return symbol.ExplicitAccessLevel.Value;

        // Fall back to name-based convention
        return AccessLevelConventions.FromName(name);
    }

    /// <summary>
    /// Finds the symbol for a member in the owning type (method, field, property, or event).
    /// </summary>
    private static Symbol? FindMemberSymbol(string memberName, TypeSymbol owningType)
    {
        // Check methods
        var method = owningType.Methods.FirstOrDefault(m => m.Name == memberName);
        if (method != null)
            return method;

        // Check fields
        var field = owningType.Fields.FirstOrDefault(f => f.Name == memberName);
        if (field != null)
            return field;

        // Properties and events don't inherit from Symbol, so they can't have ExplicitAccessLevel.
        // They already handle access level overrides in NameResolver.Members.cs directly.

        return null;
    }

    /// <summary>
    /// Check whether two types are in the same class hierarchy (one inherits from the other).
    /// Only walks the base class chain — interface relationships do not grant protected access.
    /// Does not use <see cref="TypeHierarchyService.InheritsFrom"/> because that also checks
    /// interfaces, which would incorrectly grant protected access through interface relationships.
    /// </summary>
    /// <remarks>
    /// Name equality fallback (ancestor.Name == target.Name) is a cross-module identity
    /// approximation tracked by TODO(#361).
    /// </remarks>
    private bool IsInHierarchy(TypeSymbol currentClass, TypeSymbol targetClass)
    {
        if (currentClass == targetClass)
            return true;

        // Check if currentClass is a subclass of targetClass
        foreach (var ancestor in TypeHierarchyService.GetAllBaseTypes(currentClass, Context.SemanticBinding))
        {
            if (ReferenceEquals(ancestor, targetClass) || ancestor.Name == targetClass.Name)
                return true;
        }

        // Check if targetClass is a subclass of currentClass
        foreach (var ancestor in TypeHierarchyService.GetAllBaseTypes(targetClass, Context.SemanticBinding))
        {
            if (ReferenceEquals(ancestor, currentClass) || ancestor.Name == currentClass.Name)
                return true;
        }

        return false;
    }

    private static bool IsNestedWithin(TypeSymbol? currentClass, TypeSymbol owningType)
    {
        var declaring = currentClass?.DeclaringType;
        while (declaring != null)
        {
            if (ReferenceEquals(declaring, owningType) || declaring.Name == owningType.Name)
                return true;
            declaring = declaring.DeclaringType;
        }
        return false;
    }
}
