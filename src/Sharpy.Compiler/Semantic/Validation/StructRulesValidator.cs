using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates struct-specific rules:
/// - All struct constructors must initialize all fields
/// </summary>
internal class StructRulesValidator : ValidatingAstWalker
{
    public override string Name => "StructRulesValidator";
    public override int Order => 145;

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        base.Validate(module, context);
    }

    public override void VisitStructDef(StructDef node)
    {
        var structSymbol = Context.SymbolTable.LookupType(node.Name);
        if (structSymbol != null)
        {
            ValidateStructRules(structSymbol, node);
        }
        base.VisitStructDef(node);
    }

    private void ValidateStructRules(TypeSymbol structSymbol, StructDef structDef)
    {
        _logger.LogDebug($"Validating struct-specific rules for '{structSymbol.Name}'");

        if (structSymbol.Constructors.Count > 0)
        {
            foreach (var constructor in structSymbol.Constructors)
            {
                ValidateStructConstructorInitializesAllFields(structSymbol, constructor, structDef);
            }
        }
    }

    private void ValidateStructConstructorInitializesAllFields(
        TypeSymbol structSymbol,
        FunctionSymbol constructor,
        StructDef structDef)
    {
        var constructorDef = structDef.Body
            .OfType<FunctionDef>()
            .FirstOrDefault(f => f.Name == DunderNames.Init && f.LineStart == constructor.DeclarationLine);

        if (constructorDef == null)
            return;

        var cfgBuilder = new ControlFlowGraphBuilder();
        var cfg = cfgBuilder.Build(constructorDef);
        var definitelyAssigned = DefiniteFieldAssignmentAnalysis.FindDefinitelyAssignedFields(cfg);

        var uninitializedFields = structSymbol.Fields
            .Where(f => !definitelyAssigned.Contains(f.Name))
            .ToList();

        if (uninitializedFields.Count > 0)
        {
            var fieldNames = string.Join(", ", uninitializedFields.Select(f => $"'{f.Name}'"));
            AddError(
                $"Struct '{structSymbol.Name}' constructor must initialize all fields. " +
                $"Missing initialization for: {fieldNames}",
                constructorDef.LineStart,
                constructorDef.ColumnStart,
                code: DiagnosticCodes.Semantic.UninitializedStructField,
                span: constructorDef.Span);
        }
    }
}
