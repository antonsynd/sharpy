using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

internal class ConversionOperatorValidator : ValidatingAstWalker
{
    public override string Name => "ConversionOperatorValidator";
    public override int Order => 152;

    public override void VisitClassDef(ClassDef node)
    {
        ValidateConversionOperators(node.Name, node.Body);
        base.VisitClassDef(node);
    }

    public override void VisitStructDef(StructDef node)
    {
        ValidateConversionOperators(node.Name, node.Body);
        base.VisitStructDef(node);
    }

    private void ValidateConversionOperators(string typeName, IReadOnlyList<Statement> body)
    {
        var conversionPairs = new Dictionary<(string paramType, string returnType), string>();

        foreach (var member in body)
        {
            if (member is not FunctionDef funcDef)
                continue;

            if (!OperatorRegistry.IsConversionOperator(funcDef.Name))
                continue;

            var isStatic = funcDef.Decorators.Any(d =>
                d.Name == DecoratorNames.Static || d.Name == DecoratorNames.StaticMethod);

            if (!isStatic)
            {
                AddError(
                    $"Conversion operator '{funcDef.Name}' on '{typeName}' must be @static",
                    funcDef.LineStart, funcDef.ColumnStart,
                    code: DiagnosticCodes.Validation.ConversionOperatorNotStatic,
                    span: funcDef.Span);
                continue;
            }

            var paramCount = funcDef.Parameters.Length;
            if (paramCount != 1)
            {
                AddError(
                    $"Conversion operator '{funcDef.Name}' on '{typeName}' must have exactly 1 parameter, got {paramCount}",
                    funcDef.LineStart, funcDef.ColumnStart,
                    code: DiagnosticCodes.Validation.ConversionOperatorParamCount,
                    span: funcDef.Span);
                continue;
            }

            var paramTypeName = funcDef.Parameters[0].Type?.Name ?? "?";
            var returnTypeName = funcDef.ReturnType?.Name ?? "?";

            if (paramTypeName != "?" && returnTypeName != "?" &&
                paramTypeName != typeName && returnTypeName != typeName)
            {
                AddError(
                    $"Conversion operator '{funcDef.Name}' on '{typeName}': at least one of the parameter type or return type must be '{typeName}'",
                    funcDef.LineStart, funcDef.ColumnStart,
                    code: DiagnosticCodes.Validation.ConversionOperatorNoEnclosingType,
                    span: funcDef.Span);
            }

            var pairKey = (paramTypeName, returnTypeName);
            if (conversionPairs.TryGetValue(pairKey, out var existingKind))
            {
                if (existingKind != funcDef.Name)
                {
                    AddError(
                        $"Cannot define both '{DunderNames.Implicit}' and '{DunderNames.Explicit}' conversions for the same type pair ({paramTypeName} → {returnTypeName}) on '{typeName}'",
                        funcDef.LineStart, funcDef.ColumnStart,
                        code: DiagnosticCodes.Validation.ConversionOperatorDuplicate,
                        span: funcDef.Span);
                }
            }
            else
            {
                conversionPairs[pairKey] = funcDef.Name;
            }
        }
    }
}
