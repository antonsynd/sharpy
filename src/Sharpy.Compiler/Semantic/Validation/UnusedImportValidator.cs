using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Warns about imported names that are never referenced in the module.
/// Skips wildcard imports (from X import *) since tracking individual usage is complex.
/// </summary>
internal class UnusedImportValidator : ValidatingAstWalker
{
    public override string Name => "UnusedImportValidator";
    public override int Order => 430;

    private Dictionary<string, ImportInfo> _importedNames = null!;
    private HashSet<string> _referencedNames = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _importedNames = new Dictionary<string, ImportInfo>();
        _referencedNames = new HashSet<string>();

        // First pass: collect imports from module body (imports are always top-level).
        // Unwrap suppress-decorated imports (#1124) so they are collected here and can still
        // warn SPY0452 — suppression is applied downstream by SuppressionCollector, not by making
        // the import invisible. Without this unwrap an @suppress-wrapped import with an unrelated
        // code would silently never warn.
        foreach (var stmt in module.Body)
        {
            switch (stmt.UnwrapDecorated())
            {
                case FromImportStatement fromImport:
                    if (!fromImport.ImportAll)
                    {
                        foreach (var alias in fromImport.Names)
                        {
                            var localName = alias.AsName ?? alias.Name;
                            _importedNames[localName] = new ImportInfo(
                                alias.Name, localName, alias.LineStart, alias.ColumnStart, alias.Span);
                        }
                    }
                    break;

                case ImportStatement import:
                    foreach (var alias in import.Names)
                    {
                        var localName = alias.AsName ?? GetTopLevelName(alias.Name);
                        _importedNames[localName] = new ImportInfo(
                            alias.Name, localName, alias.LineStart, alias.ColumnStart, alias.Span);
                    }
                    break;
            }
        }

        // Second pass: walk all non-import AST nodes to collect references
        base.Validate(module, context);

        // Emit warnings for unused imports
        foreach (var (localName, info) in _importedNames)
        {
            if (!_referencedNames.Contains(localName))
            {
                AddWarning(
                    $"Imported name '{info.OriginalName}' is never used",
                    info.Line, info.Column,
                    code: DiagnosticCodes.Validation.UnusedImport,
                    span: info.Span);
            }
        }
    }

    // Skip traversal into import statements — we already collected them above
    public override void VisitFromImportStatement(FromImportStatement node) { }
    public override void VisitImportStatement(ImportStatement node) { }

    // Collect identifier references
    public override void VisitIdentifier(Identifier node)
    {
        _referencedNames.Add(node.Name);
    }

    // Collect decorator names and type annotation references
    public override void VisitFunctionDef(FunctionDef node)
    {
        foreach (var decorator in node.Decorators)
            _referencedNames.Add(decorator.Name);
        foreach (var param in node.Parameters)
        {
            if (param.Type != null)
                CollectReferencesFromTypeAnnotation(param.Type, _referencedNames);
        }
        if (node.ReturnType != null)
            CollectReferencesFromTypeAnnotation(node.ReturnType, _referencedNames);
        CollectReferencesFromTypeParameters(node.TypeParameters);
        base.VisitFunctionDef(node);
    }

    public override void VisitClassDef(ClassDef node)
    {
        foreach (var decorator in node.Decorators)
            _referencedNames.Add(decorator.Name);
        foreach (var baseType in node.BaseClasses)
            CollectReferencesFromTypeAnnotation(baseType, _referencedNames);
        CollectReferencesFromTypeParameters(node.TypeParameters);
        base.VisitClassDef(node);
    }

    public override void VisitStructDef(StructDef node)
    {
        foreach (var decorator in node.Decorators)
            _referencedNames.Add(decorator.Name);
        foreach (var baseType in node.BaseClasses)
            CollectReferencesFromTypeAnnotation(baseType, _referencedNames);
        CollectReferencesFromTypeParameters(node.TypeParameters);
        base.VisitStructDef(node);
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        foreach (var baseIface in node.BaseInterfaces)
            CollectReferencesFromTypeAnnotation(baseIface, _referencedNames);
        CollectReferencesFromTypeParameters(node.TypeParameters);
        base.VisitInterfaceDef(node);
    }

    /// <summary>
    /// A type parameter's constraints and its PEP-696 default are type annotations like any other,
    /// but nothing walked them, so an import used ONLY in one of those positions was reported unused
    /// — <c>def widest[T: Shape](a: T)</c> and <c>class Box[T = Shape]</c> both warned. Unlike the
    /// dotted-name reduction in <see cref="CollectReferencesFromTypeAnnotation"/> this is a missing
    /// traversal, not a missing reduction: the BARE spelling was affected too, which is what a
    /// neutral control showed and what makes it a separate defect rather than the same one.
    /// </summary>
    private void CollectReferencesFromTypeParameters(
        System.Collections.Immutable.ImmutableArray<TypeParameterDef> typeParameters)
    {
        foreach (var typeParam in typeParameters)
        {
            foreach (var constraint in typeParam.Constraints)
            {
                if (constraint is TypeConstraint typeConstraint)
                    CollectReferencesFromTypeAnnotation(typeConstraint.Type, _referencedNames);
            }

            if (typeParam.DefaultType != null)
                CollectReferencesFromTypeAnnotation(typeParam.DefaultType, _referencedNames);
        }
    }

    public override void VisitVariableDeclaration(VariableDeclaration node)
    {
        if (node.Type != null)
            CollectReferencesFromTypeAnnotation(node.Type, _referencedNames);
        base.VisitVariableDeclaration(node);
    }

    public override void VisitTypeAlias(TypeAlias node)
    {
        if (node.Type != null)
            CollectReferencesFromTypeAnnotation(node.Type, _referencedNames);
        base.VisitTypeAlias(node);
    }

    public override void VisitPropertyDef(PropertyDef node)
    {
        foreach (var decorator in node.Decorators)
            _referencedNames.Add(decorator.Name);
        if (node.Type != null)
            CollectReferencesFromTypeAnnotation(node.Type, _referencedNames);
        if (node.ReturnType != null)
            CollectReferencesFromTypeAnnotation(node.ReturnType, _referencedNames);
        foreach (var param in node.Parameters)
        {
            if (param.Type != null)
                CollectReferencesFromTypeAnnotation(param.Type, _referencedNames);
        }
        base.VisitPropertyDef(node);
    }

    public override void VisitEventDef(EventDef node)
    {
        foreach (var decorator in node.Decorators)
            _referencedNames.Add(decorator.Name);
        if (node.Type != null)
            CollectReferencesFromTypeAnnotation(node.Type, _referencedNames);
        foreach (var param in node.Parameters)
        {
            if (param.Type != null)
                CollectReferencesFromTypeAnnotation(param.Type, _referencedNames);
        }
        base.VisitEventDef(node);
    }

    public override void VisitTypeCoercion(TypeCoercion node)
    {
        CollectReferencesFromTypeAnnotation(node.TargetType, _referencedNames);
        base.VisitTypeCoercion(node);
    }

    public override void VisitTypeCheck(TypeCheck node)
    {
        CollectReferencesFromTypeAnnotation(node.CheckType, _referencedNames);
        base.VisitTypeCheck(node);
    }

    public override void VisitTryStatement(TryStatement node)
    {
        foreach (var handler in node.Handlers)
        {
            if (handler.ExceptionType != null)
                CollectReferencesFromTypeAnnotation(handler.ExceptionType, _referencedNames);
        }
        base.VisitTryStatement(node);
    }

    public override void VisitTryExpression(TryExpression node)
    {
        foreach (var exceptionType in node.ExceptionTypes)
        {
            CollectReferencesFromTypeAnnotation(exceptionType, _referencedNames);
        }
        base.VisitTryExpression(node);
    }

    /// <summary>
    /// For dotted import names like "geometry.shapes", get the top-level part ("geometry").
    /// </summary>
    private static string GetTopLevelName(string dottedName)
    {
        var dotIndex = dottedName.IndexOf('.', StringComparison.Ordinal);
        return dotIndex >= 0 ? dottedName[..dotIndex] : dottedName;
    }

    /// <summary>
    /// Collect references from type annotations (e.g., parameter types, return types, variable types).
    /// Imported types used in annotations should count as used.
    /// </summary>
    private static void CollectReferencesFromTypeAnnotation(TypeAnnotation typeAnnotation, HashSet<string> refs)
    {
        var typeName = typeAnnotation.Name;
        if (!string.IsNullOrEmpty(typeName))
        {
            refs.Add(typeName);

            // A module-qualified annotation (`genlib.Base`, `genlib.Holder[str]`) is recorded under
            // its written dotted name, but an `import genlib` is registered under the bare module
            // name — so every annotation-only use of an imported module was reported unused
            // (SPY0452). The value positions never had the bug: `genlib.Base()` walks a MemberAccess
            // whose object is the bare Identifier. Record the top-level segment too, the same
            // reduction GetTopLevelName applies on the import side.
            var topLevel = GetTopLevelName(typeName);
            if (topLevel.Length != typeName.Length)
                refs.Add(topLevel);
        }

        foreach (var typeArg in typeAnnotation.TypeArguments)
        {
            CollectReferencesFromTypeAnnotation(typeArg, refs);
        }
    }

    private record ImportInfo(string OriginalName, string LocalName, int? Line, int? Column, Text.TextSpan? Span);
}
