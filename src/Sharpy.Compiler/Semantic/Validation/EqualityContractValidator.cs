using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Warns when a class defines __eq__ but no __eq__(self, other: object) overload.
/// Without the object overload, collections (set, dict) will use reference equality.
/// </summary>
internal class EqualityContractValidator : SemanticValidatorBase
{
    public override string Name => "EqualityContractValidator";
    public override int Order => 160; // After SignatureValidator (150)

    public override void Validate(Module module, SemanticContext context)
    {
        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    ValidateClass(classDef, context);
                    break;
                case StructDef structDef:
                    ValidateStruct(structDef, context);
                    break;
            }
        }
    }

    private void ValidateClass(ClassDef classDef, SemanticContext context)
    {
        CheckEqOverloads(classDef.Name, classDef.Body, context);
    }

    private void ValidateStruct(StructDef structDef, SemanticContext context)
    {
        CheckEqOverloads(structDef.Name, structDef.Body, context);
    }

    private void CheckEqOverloads(string typeName, IReadOnlyList<Statement> body, SemanticContext context)
    {
        var eqMethods = new List<FunctionDef>();

        foreach (var member in body)
        {
            if (member is FunctionDef func && func.Name == DunderNames.Eq)
            {
                eqMethods.Add(func);
            }
        }

        if (eqMethods.Count == 0)
            return;

        var hasObjectOverload = eqMethods.Any(IsObjectOverload);

        if (!hasObjectOverload)
        {
            // Use location of the first __eq__ method for the warning
            var firstEq = eqMethods[0];
            AddWarning(context,
                $"Class '{typeName}' defines '__eq__' but not '__eq__(self, other: object)'. " +
                "Collections (set, dict) will use reference equality. " +
                "Define '__eq__(self, other: object)' for value-based collection behavior.",
                firstEq.LineStart, firstEq.ColumnStart,
                code: DiagnosticCodes.Validation.EqWithoutObjectOverload,
                span: firstEq.Span);
        }
    }

    private static bool IsObjectOverload(FunctionDef func)
    {
        var otherParam = func.Parameters
            .FirstOrDefault(p => !string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));
        return otherParam?.Type is TypeAnnotation { Name: "object" };
    }
}
