using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates that no two __init__ methods in a class have the same parameter signature.
/// Unlike Python (which only allows one __init__), Sharpy supports constructor overloading
/// by mapping multiple __init__ methods to C# constructor overloads.
/// </summary>
internal class ConstructorOverloadValidator : ValidatingAstWalker
{
    public override string Name => "ConstructorOverloadValidator";
    public override int Order => 140;

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        base.Validate(module, context);
    }

    public override void VisitClassDef(ClassDef node)
    {
        var classSymbol = Context.SymbolTable.LookupType(node.Name);
        if (classSymbol != null)
        {
            ValidateConstructorOverloads(classSymbol, node.Body);
        }
        base.VisitClassDef(node);
    }

    private void ValidateConstructorOverloads(TypeSymbol type, IReadOnlyList<Statement> classBody)
    {
        var constructors = type.Constructors;
        if (constructors.Count <= 1)
            return;

        _logger.LogDebug($"Validating {constructors.Count} constructor overloads for '{type.Name}'");

        var signatures = new HashSet<string>();
        foreach (var ctor in constructors)
        {
            var paramTypes = ctor.Parameters
                .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Type.GetDisplayName())
                .ToList();
            var signature = string.Join(",", paramTypes);

            if (!signatures.Add(signature))
            {
                var ctorDef = classBody.OfType<FunctionDef>()
                    .FirstOrDefault(f => f.Name == DunderNames.Init && f.LineStart == ctor.DeclarationLine);

                AddError(
                    $"Duplicate constructor signature in '{type.Name}': __init__({signature})",
                    ctor.DeclarationLine,
                    ctor.DeclarationColumn,
                    code: DiagnosticCodes.Semantic.DuplicateDefinition,
                    span: ctorDef?.Span);
            }
        }
    }
}
