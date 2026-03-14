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
        var collector = new ReferenceCollector(referencedNames);

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
                    collector.Visit(stmt);
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
        }

        foreach (var typeArg in typeAnnotation.TypeArguments)
        {
            CollectReferencesFromTypeAnnotation(typeArg, refs);
        }
    }

    /// <summary>
    /// AstVisitor that collects all referenced identifiers, decorator names, and type annotation
    /// names from the AST. DefaultVisit handles recursive traversal into child nodes automatically.
    /// </summary>
    private sealed class ReferenceCollector(HashSet<string> refs) : AstVisitor
    {
        public override void VisitIdentifier(Identifier node) => refs.Add(node.Name);

        public override void VisitFunctionDef(FunctionDef node)
        {
            foreach (var decorator in node.Decorators)
                refs.Add(decorator.Name);
            foreach (var param in node.Parameters)
            {
                if (param.Type != null)
                    CollectReferencesFromTypeAnnotation(param.Type, refs);
            }
            if (node.ReturnType != null)
                CollectReferencesFromTypeAnnotation(node.ReturnType, refs);
            base.VisitFunctionDef(node);
        }

        public override void VisitClassDef(ClassDef node)
        {
            foreach (var decorator in node.Decorators)
                refs.Add(decorator.Name);
            foreach (var baseType in node.BaseClasses)
                CollectReferencesFromTypeAnnotation(baseType, refs);
            base.VisitClassDef(node);
        }

        public override void VisitStructDef(StructDef node)
        {
            foreach (var decorator in node.Decorators)
                refs.Add(decorator.Name);
            foreach (var baseType in node.BaseClasses)
                CollectReferencesFromTypeAnnotation(baseType, refs);
            base.VisitStructDef(node);
        }

        public override void VisitInterfaceDef(InterfaceDef node)
        {
            foreach (var baseIface in node.BaseInterfaces)
                CollectReferencesFromTypeAnnotation(baseIface, refs);
            base.VisitInterfaceDef(node);
        }

        public override void VisitVariableDeclaration(VariableDeclaration node)
        {
            if (node.Type != null)
                CollectReferencesFromTypeAnnotation(node.Type, refs);
            base.VisitVariableDeclaration(node);
        }

        public override void VisitTypeAlias(TypeAlias node)
        {
            if (node.Type != null)
                CollectReferencesFromTypeAnnotation(node.Type, refs);
            base.VisitTypeAlias(node);
        }

        public override void VisitPropertyDef(PropertyDef node)
        {
            foreach (var decorator in node.Decorators)
                refs.Add(decorator.Name);
            if (node.Type != null)
                CollectReferencesFromTypeAnnotation(node.Type, refs);
            if (node.ReturnType != null)
                CollectReferencesFromTypeAnnotation(node.ReturnType, refs);
            foreach (var param in node.Parameters)
            {
                if (param.Type != null)
                    CollectReferencesFromTypeAnnotation(param.Type, refs);
            }
            base.VisitPropertyDef(node);
        }

        public override void VisitEventDef(EventDef node)
        {
            foreach (var decorator in node.Decorators)
                refs.Add(decorator.Name);
            if (node.Type != null)
                CollectReferencesFromTypeAnnotation(node.Type, refs);
            foreach (var param in node.Parameters)
            {
                if (param.Type != null)
                    CollectReferencesFromTypeAnnotation(param.Type, refs);
            }
            base.VisitEventDef(node);
        }

        public override void VisitTypeCoercion(TypeCoercion node)
        {
            CollectReferencesFromTypeAnnotation(node.TargetType, refs);
            base.VisitTypeCoercion(node);
        }

        public override void VisitTypeCheck(TypeCheck node)
        {
            CollectReferencesFromTypeAnnotation(node.CheckType, refs);
            base.VisitTypeCheck(node);
        }

        public override void VisitTryStatement(TryStatement node)
        {
            foreach (var handler in node.Handlers)
            {
                if (handler.ExceptionType != null)
                    CollectReferencesFromTypeAnnotation(handler.ExceptionType, refs);
            }
            base.VisitTryStatement(node);
        }

        public override void VisitTryExpression(TryExpression node)
        {
            if (node.ExceptionType != null)
                CollectReferencesFromTypeAnnotation(node.ExceptionType, refs);
            base.VisitTryExpression(node);
        }
    }

    private record ImportInfo(string OriginalName, string LocalName, int? Line, int? Column, Text.TextSpan? Span);
}
