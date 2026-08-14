using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: @dataclass decorator processing and method synthesis
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// Processes @dataclass decorator on a class: extracts options, collects fields,
    /// validates field ordering, and sets IsDataclass/DataclassInfo/DataclassFields on the symbol.
    /// </summary>
    private void ProcessDataclassDecorator(TypeSymbol classSymbol, ClassDef classDef)
    {
        // What @dataclass MEANS lives in DataclassSynthesis, so an IMPORTED dataclass gets the same
        // answer from ModuleLoader's extraction rather than arriving as an ordinary class (#1442).
        // What stays here is what only a compilation that BUILDS this file can say: the diagnostics.
        var options = DataclassSynthesis.ReadOptions(classDef);
        if (options == null)
            return;

        classSymbol.IsDataclass = true;
        classSymbol.DataclassInfo = options;

        // Check for Assignment nodes in class body — these are untyped field declarations
        // that need type annotations in a @dataclass context
        foreach (var assignment in classDef.Body.OfType<Assignment>())
        {
            if (assignment.Target is Identifier ident && assignment.Operator == AssignmentOperator.Assign)
            {
                AddError(
                    $"Dataclass field '{ident.Name}' in '{classDef.Name}' must have a type annotation " +
                    $"(use '{ident.Name}: type = ...' instead of '{ident.Name} = ...').",
                    assignment.LineStart,
                    assignment.ColumnStart,
                    code: DiagnosticCodes.Semantic.DataclassFieldNoType,
                    span: assignment.Span);
            }
        }

        var dataclassFields = DataclassSynthesis.CollectFields(
            classSymbol,
            classDef,
            onUntypedField: fieldDecl => AddError(
                $"Dataclass field '{fieldDecl.Name}' in '{classDef.Name}' must have a type annotation.",
                fieldDecl.LineStart,
                fieldDecl.ColumnStart,
                code: DiagnosticCodes.Semantic.DataclassFieldNoType,
                span: fieldDecl.Span),
            onOrderingViolation: fieldDecl => AddError(
                $"Non-default field '{fieldDecl.Name}' in dataclass '{classDef.Name}' " +
                "cannot follow a field with a default value.",
                fieldDecl.LineStart,
                fieldDecl.ColumnStart,
                code: DiagnosticCodes.Semantic.DataclassFieldOrdering,
                span: fieldDecl.Span));

        classSymbol.DataclassFields = dataclassFields;

        // Synthesize methods that don't have explicit definitions. The field's contributed type is
        // this pass's own resolved binding; the extractor passes the annotation it converted.
        DataclassSynthesis.SynthesizeMembers(
            classSymbol, classDef, dataclassFields, options, GetVariableType);
    }

}
