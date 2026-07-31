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

        // tuple(iterable) is deliberately not modeled; say so instead of letting the generic
        // "cannot infer type arguments" fall out of inference below (#1159).
        if (typeSymbol.Name == BuiltinNames.Tuple
            && ReportUnsupportedTupleFromIterable(call, argTypes) is { } tupleRejection)
        {
            return tupleRejection;
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
                                 && typeSymbol.TypeParameters.Count == 2)
                        {
                            // dict(d) COPIES a dict, so K and V come from the argument itself — its
                            // element type is only the key (Python iterates keys), which is why the
                            // pairs arm below cannot answer for it. Without this the expression
                            // position had no annotation to fall back on and emitted a bare
                            // `Sharpy.Dict` (CS0305, #1201); the annotated form worked only because
                            // _expectedType supplied the arguments.
                            if (argType is GenericType { Name: BuiltinNames.Dict } sourceDict
                                && sourceDict.TypeArguments.Count == 2)
                            {
                                typeArgs = new List<SemanticType>(sourceDict.TypeArguments);
                            }
                            else if (elementType is TupleType tt && tt.ElementTypes.Count == 2)
                            {
                                // dict(pairs): an iterable of 2-tuples, K/V from the tuple.
                                typeArgs = new List<SemanticType> { tt.ElementTypes[0], tt.ElementTypes[1] };
                            }
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

    /// <summary>
    /// Reports SPY0338 for <c>tuple(iterable)</c> — the single-iterable-argument form Python uses to
    /// build a tuple as long as the iterable (#1159).
    ///
    /// <para>Not modeled by design: a Sharpy tuple's arity is part of its type (<c>tuple[int, str]</c>
    /// lowers to a <c>ValueTuple</c>), while an iterable's length is only known at runtime, so the
    /// result has no type to give it. Sharpy's answer is <c>list(...)</c> for a runtime-length sequence
    /// and a tuple literal when the arity is known. Naming the limitation beats the
    /// "cannot infer type arguments for 'tuple'" that inference produced, which read as a missing
    /// annotation the user could supply.</para>
    ///
    /// <para>Scope: exactly this form. A tuple literal, an explicitly parameterized
    /// <c>tuple[int, str](...)</c>, a multi-argument call, and the empty <c>tuple()</c> (SPY0227) all
    /// take other paths. A single argument that is not iterable also keeps its existing diagnostic —
    /// its problem is the argument, not the arity.</para>
    /// </summary>
    /// <returns>Unknown after reporting, or null when the call is not the iterable form.</returns>
    private SemanticType? ReportUnsupportedTupleFromIterable(FunctionCall call, List<SemanticType> argTypes)
    {
        if (call.Arguments.Length != 1 || call.KeywordArguments.Length != 0 || argTypes.Count != 1)
            return null;

        var argType = argTypes[0];
        if (argType is UnknownType || _typeInference.InferIterableElementType(argType) == null)
            return null;

        // An argument that is ALREADY a tuple has a statically known arity, so the arity is not what
        // is missing and the advice is to drop the conversion rather than reach for a list.
        var message = argType is TupleType
            ? $"'tuple(...)' cannot convert an iterable to a tuple: a tuple's arity is part of its type. "
                + $"The argument is already a '{argType.GetDisplayName()}' — drop the conversion."
            : $"variable-length 'tuple(iterable)' is not supported: a tuple's arity is part of its type, "
                + $"but the length of a '{argType.GetDisplayName()}' is not known until runtime. Use "
                + $"'list(...)' for a runtime-length sequence, or a tuple literal when the arity is "
                + $"known (e.g. '(a, b)').";

        AddError(message, call.LineStart, call.ColumnStart,
            code: DiagnosticCodes.Semantic.UnsupportedVariableArityTuple,
            span: call.Span);
        return SemanticType.Unknown;
    }
}
