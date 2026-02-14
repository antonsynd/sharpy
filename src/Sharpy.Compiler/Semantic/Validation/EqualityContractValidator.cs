using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

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
        FunctionDef? hashMethod = null;

        foreach (var member in body)
        {
            if (member is FunctionDef func)
            {
                if (func.Name == DunderNames.Eq)
                    eqMethods.Add(func);
                else if (func.Name == DunderNames.Hash)
                    hashMethod = func;
            }
        }

        var hasObjectOverload = eqMethods.Any(IsObjectOverload);

        // SPY0454: __eq__ exists but no object overload
        if (eqMethods.Count > 0 && !hasObjectOverload)
        {
            var firstEq = eqMethods[0];
            AddWarning(context,
                $"Class '{typeName}' defines '__eq__' but not '__eq__(self, other: object)'. " +
                "Collections (set, dict) will use reference equality. " +
                "Define '__eq__(self, other: object)' for value-based collection behavior.",
                firstEq.LineStart, firstEq.ColumnStart,
                code: DiagnosticCodes.Validation.EqWithoutObjectOverload,
                span: firstEq.Span);
        }

        // SPY0455: __eq__(object) without __hash__
        // Inherited __hash__ from a base class satisfies the contract (per .NET semantics).
        if (hasObjectOverload && hashMethod == null && !HasInheritedDunder(typeName, DunderNames.Hash, context))
        {
            var eqObj = eqMethods.First(IsObjectOverload);
            AddError(context,
                $"Class '{typeName}' defines '__eq__(self, other: object)' but not '__hash__'. " +
                "The .NET equality contract requires both. Define '__hash__(self) -> int'.",
                eqObj.LineStart, eqObj.ColumnStart,
                code: DiagnosticCodes.Validation.EqObjectWithoutHash,
                span: eqObj.Span);
        }

        // SPY0456: __hash__ without __eq__(object)
        // Inherited __eq__(object) from a base class satisfies the contract.
        if (hashMethod != null && !hasObjectOverload && !HasInheritedEqObject(typeName, context))
        {
            AddError(context,
                $"Class '{typeName}' defines '__hash__' but not '__eq__(self, other: object)'. " +
                "The .NET equality contract requires both. Define '__eq__(self, other: object) -> bool'.",
                hashMethod.LineStart, hashMethod.ColumnStart,
                code: DiagnosticCodes.Validation.HashWithoutEqObject,
                span: hashMethod.Span);
        }
    }

    private static bool IsObjectOverload(FunctionDef func)
    {
        var otherParam = func.Parameters
            .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
        return otherParam?.Type is TypeAnnotation { Name: "object" };
    }

    /// <summary>
    /// Check if a dunder method (e.g., __hash__) is defined in any ancestor class.
    /// </summary>
    private static bool HasInheritedDunder(string typeName, string dunderName, SemanticContext context)
    {
        var typeSymbol = context.SymbolTable.Lookup(typeName) as TypeSymbol;
        var baseType = typeSymbol?.BaseType;
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
        while (baseType != null && visited.Add(baseType))
        {
            if (baseType.ProtocolMethods.ContainsKey(dunderName)
                || baseType.OperatorMethods.ContainsKey(dunderName)
                || baseType.Methods.Any(m => m.Name == dunderName))
            {
                return true;
            }
            baseType = baseType.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Check if __eq__(self, other: object) is defined in any ancestor class.
    /// </summary>
    private static bool HasInheritedEqObject(string typeName, SemanticContext context)
    {
        var typeSymbol = context.SymbolTable.Lookup(typeName) as TypeSymbol;
        var baseType = typeSymbol?.BaseType;
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
        while (baseType != null && visited.Add(baseType))
        {
            if (baseType.OperatorMethods.TryGetValue(DunderNames.Eq, out var overloads))
            {
                if (overloads.Any(HasObjectParameter))
                    return true;
            }
            baseType = baseType.BaseType;
        }
        return false;
    }

    private static bool HasObjectParameter(FunctionSymbol func)
    {
        return func.Parameters.Any(p =>
            p.Name != PythonNames.Self && p.Type is UserDefinedType { Name: "object" });
    }
}
