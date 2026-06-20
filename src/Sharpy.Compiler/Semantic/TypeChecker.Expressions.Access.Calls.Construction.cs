using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Constructor call validation and generic type argument inference
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// Handles constructor calls: validates arguments against __init__ parameters,
    /// checks for abstract instantiation, and infers generic type arguments.
    /// </summary>
    private SemanticType CheckConstructorCall(
        FunctionCall call, TypeSymbol typeSymbol, List<SemanticType> argTypes,
        Dictionary<string, SemanticType> kwargTypes, int totalArgCount)
    {
        CheckDeprecatedUsage(typeSymbol, call);

        // Validate constructor arguments against __init__ parameters (skip 'self').
        // Only validate when there's a single __init__ (no overloads) — overloaded
        // constructors have complex resolution that the C# compiler handles.
        var initMethods = typeSymbol.Methods.Where(m => m.Name == DunderNames.Init).ToList();
        if (initMethods.Count == 1)
        {
            var initParams = initMethods[0].Parameters.Skip(1).ToList(); // skip 'self'

            // SPY0357: Check for iterable spread into non-variadic constructor
            if (CheckSpreadIntoNonVariadic(call, typeSymbol.Name, initParams))
                return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };

            // Validate argument count and positional-only/keyword-only constraints.
            // Skip type checking — the C# compiler handles type validation, and there
            // are edge cases (None to nullable, enum conversions) it handles correctly.
            ValidateCallArgumentsCountAndKinds(call, initParams, argTypes, kwargTypes, totalArgCount);
        }
        else if (initMethods.Count > 1)
        {
            // Multiple __init__ overloads — only check spread into non-variadic
            var firstInit = initMethods[0];
            var initParams = firstInit.Parameters.Skip(1).ToList();
            if (CheckSpreadIntoNonVariadic(call, typeSymbol.Name, initParams))
                return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
        }

        // Cannot instantiate abstract classes
        if (typeSymbol.IsAbstract)
        {
            AddError($"Cannot instantiate abstract class '{typeSymbol.Name}'",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AbstractInstantiation,
                span: call.Span);
            return SemanticType.Unknown;
        }

        // For generic types called without type arguments (e.g., set()),
        // infer type arguments from the expected type annotation if available,
        // otherwise emit a diagnostic for empty constructors or fall back to
        // UnknownType args for wildcard matching.
        if (typeSymbol.IsGeneric)
        {
            List<SemanticType>? typeArgs = null;
            if (_expectedType is GenericType expectedGeneric
                && expectedGeneric.Name == typeSymbol.Name
                && expectedGeneric.TypeArguments.Count == typeSymbol.TypeParameters.Count
                && !expectedGeneric.TypeArguments.Any(ContainsTypeParameter))
            {
                typeArgs = expectedGeneric.TypeArguments;
            }
            else if (call.Arguments.Length == 0 && call.KeywordArguments.Length == 0)
            {
                // Empty generic constructor with no type annotation — cannot infer type args
                AddError($"Cannot infer type of empty {typeSymbol.Name} constructor; add a type annotation (e.g., x: {typeSymbol.Name}[...] = {typeSymbol.Name}())",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                    span: call.Span);
                return SemanticType.Unknown;
            }
            else if (call.Arguments.Length == 1 && call.KeywordArguments.Length == 0)
            {
                // Single-argument constructor: try to infer type args from iterable argument type
                var argType = argTypes.Count > 0 ? argTypes[0] : null;
                if (argType != null && argType != SemanticType.Unknown)
                {
                    var elementType = _typeInference.InferIterableElementType(argType);
                    if (elementType != null && elementType != SemanticType.Unknown)
                    {
                        if (typeSymbol.Name is BuiltinNames.List or BuiltinNames.Set
                            && typeSymbol.TypeParameters.Count == 1)
                        {
                            typeArgs = new List<SemanticType> { elementType };
                        }
                        else if (typeSymbol.Name == BuiltinNames.Dict
                                 && typeSymbol.TypeParameters.Count == 2
                                 && elementType is TupleType tt && tt.ElementTypes.Count == 2)
                        {
                            typeArgs = new List<SemanticType> { tt.ElementTypes[0], tt.ElementTypes[1] };
                        }
                    }
                }

                // Fallback: try __init__-based inference for user-defined generic constructors
                if (typeArgs == null)
                {
                    typeArgs = TryInferConstructorTypeArgs(typeSymbol, call, argTypes);
                }
            }
            else
            {
                // Multiple arguments or keyword arguments: infer type args from __init__ parameters
                typeArgs = TryInferConstructorTypeArgs(typeSymbol, call, argTypes);
            }

            // If inference failed, fall back to UnknownType args for builtin
            // collections (lets C# compiler report the real error) or emit
            // a diagnostic for user-defined generic types.
            if (typeArgs == null)
            {
                if (typeSymbol.Name is BuiltinNames.List or BuiltinNames.Set or BuiltinNames.Dict)
                {
                    typeArgs = Enumerable.Range(0, typeSymbol.TypeParameters.Count)
                        .Select(_ => (SemanticType)SemanticType.Unknown)
                        .ToList();
                }
                else
                {
                    AddError(
                        $"Cannot infer type arguments for '{typeSymbol.Name}'; " +
                        $"use explicit syntax: {typeSymbol.Name}[{string.Join(", ", typeSymbol.TypeParameters.Select(tp => tp.Name))}](...)",
                        call.LineStart, call.ColumnStart,
                        code: DiagnosticCodes.Semantic.CannotInferGenericType,
                        span: call.Span);
                    return SemanticType.Unknown;
                }
            }

            return new GenericType
            {
                Name = typeSymbol.Name,
                TypeArguments = typeArgs,
                GenericDefinition = typeSymbol
            };
        }

        // Constructor call returns an instance of the type
        return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
    }
}
