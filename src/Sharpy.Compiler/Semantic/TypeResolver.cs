using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Resolves type annotations to semantic types
/// </summary>
public class TypeResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    public TypeResolver(SymbolTable symbolTable, SemanticInfo semanticInfo, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _semanticInfo = semanticInfo;
        _logger = logger ?? NullLogger.Instance;
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

    public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
    {
        if (annotation == null)
            return SemanticType.Unknown;

        // Check cache
        var cached = _semanticInfo.GetTypeAnnotation(annotation);
        if (cached != null)
            return cached;

        SemanticType result;

        // Handle 'auto' keyword for type inference
        if (annotation.Name == "auto")
        {
            result = SemanticType.Unknown;
            _semanticInfo.SetTypeAnnotation(annotation, result);
            return result;
        }

        // Try builtin types first
        if (TryResolveBuiltinType(annotation.Name, out var builtinType))
        {
            result = builtinType;
        }
        // Check for type alias and expand it
        else if (_symbolTable.LookupTypeAlias(annotation.Name) is TypeAliasSymbol aliasSymbol)
        {
            result = ExpandTypeAlias(aliasSymbol, annotation.IsOptional);
        }
        // Check for generic type
        else if (annotation.TypeArguments.Length > 0)
        {
            result = ResolveGenericType(annotation);
        }
        // Check for type parameter (e.g., T in class Box[T])
        else if (_symbolTable.Lookup(annotation.Name) is TypeParameterSymbol typeParamSymbol)
        {
            result = new TypeParameterType
            {
                Name = annotation.Name,
                DeclaringType = typeParamSymbol.DeclaringType
            };
        }
        // Look up user-defined type
        else
        {
            var typeSymbol = _symbolTable.LookupType(annotation.Name);
            if (typeSymbol != null)
            {
                result = new UserDefinedType
                {
                    Name = annotation.Name,
                    Symbol = typeSymbol
                };
            }
            else
            {
                AddError($"Type '{annotation.Name}' not found", null, null);
                result = SemanticType.Unknown;
            }
        }

        // Handle nullable types (already handled for type aliases in ExpandTypeAlias)
        // For non-alias types, apply nullable modifier here
        if (annotation.IsOptional && result != SemanticType.Unknown
            && _symbolTable.LookupTypeAlias(annotation.Name) == null)
        {
            result = new NullableType { UnderlyingType = result };
        }

        // Cache the result
        _semanticInfo.SetTypeAnnotation(annotation, result);
        return result;
    }

    private bool TryResolveBuiltinType(string name, out SemanticType type)
    {
        type = name switch
        {
            "int" => SemanticType.Int,
            "long" => SemanticType.Long,
            "float" => SemanticType.Float,       // float -> double (per spec)
            "float32" => SemanticType.Float32,   // float32 -> C# float
            "float64" => SemanticType.Double,    // float64 -> double
            "double" => SemanticType.Double,
            "bool" => SemanticType.Bool,
            "str" => SemanticType.Str,
            "None" => SemanticType.Void,
            _ => null!
        };

        return type != null;
    }

    private SemanticType ResolveGenericType(TypeAnnotation annotation)
    {
        // Special handling for tuple types - they have variable arity (tuple[int], tuple[int, str], etc.)
        if (annotation.Name == "tuple")
        {
            var elementTypes = annotation.TypeArguments
                .Select(ResolveTypeAnnotation)
                .ToList();

            return new TupleType { ElementTypes = elementTypes };
        }

        // Special handling for function types - (T, U) -> V parsed as "function" with type args
        // TypeArguments contain [param types..., return type] where return type is the last element
        if (annotation.Name == "function")
        {
            if (annotation.TypeArguments.Length == 0)
            {
                AddError("Function type requires at least a return type", null, null);
                return SemanticType.Unknown;
            }

            // Last type argument is the return type, rest are parameter types
            var allTypes = annotation.TypeArguments.Select(ResolveTypeAnnotation).ToList();
            var returnType = allTypes[^1];
            var paramTypes = allTypes.Take(allTypes.Count - 1).ToList();

            return new FunctionType
            {
                ParameterTypes = paramTypes,
                ReturnType = returnType
            };
        }

        var typeSymbol = _symbolTable.LookupType(annotation.Name);
        if (typeSymbol == null)
        {
            AddError($"Generic type '{annotation.Name}' not found", null, null);
            return SemanticType.Unknown;
        }

        // Resolve type arguments
        var typeArgs = annotation.TypeArguments
            .Select(ResolveTypeAnnotation)
            .ToList();

        // Validate type argument count
        if (typeSymbol.IsGeneric && typeArgs.Count != typeSymbol.TypeParameters.Count)
        {
            AddError($"Type '{annotation.Name}' expects {typeSymbol.TypeParameters.Count} type arguments but got {typeArgs.Count}",
                null, null);
            return SemanticType.Unknown;
        }

        return new GenericType
        {
            Name = annotation.Name,
            TypeArguments = typeArgs,
            GenericDefinition = typeSymbol
        };
    }

    private SemanticType ExpandTypeAlias(TypeAliasSymbol aliasSymbol, bool isNullable)
    {
        SemanticType result;

        // Expand type annotation
        if (aliasSymbol.TypeAnnotation != null)
        {
            result = ResolveTypeAnnotation(aliasSymbol.TypeAnnotation);
        }
        // Expand function type
        else if (aliasSymbol.FunctionType != null)
        {
            result = ResolveFunctionType(aliasSymbol.FunctionType);
        }
        else
        {
            AddError($"Type alias '{aliasSymbol.Name}' has no type definition",
                aliasSymbol.DeclarationLine, aliasSymbol.DeclarationColumn);
            return SemanticType.Unknown;
        }

        // Apply nullable modifier if present at usage site
        if (isNullable && result != SemanticType.Unknown)
        {
            result = new NullableType { UnderlyingType = result };
        }

        return result;
    }

    private Semantic.FunctionType ResolveFunctionType(Parser.Ast.FunctionType functionType)
    {
        var paramTypes = functionType.ParameterTypes
            .Select(ResolveTypeAnnotation)
            .ToList();

        var returnType = ResolveTypeAnnotation(functionType.ReturnType);

        return new Semantic.FunctionType
        {
            ParameterTypes = paramTypes,
            ReturnType = returnType
        };
    }

    private void AddError(string message, int? line = null, int? column = null)
    {
        var error = new SemanticError(message, line, column);
        _errors.Add(error);
        _logger.LogError(error.Message, line ?? 0, column ?? 0);
    }
}
