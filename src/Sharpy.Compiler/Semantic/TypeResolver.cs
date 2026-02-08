using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Resolves type annotations to semantic types
/// </summary>
internal class TypeResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly ICompilerLogger _logger;
    private readonly DiagnosticBag _diagnostics = new();
    private readonly CancellationToken _cancellationToken;

    public TypeResolver(SymbolTable symbolTable, SemanticInfo semanticInfo, ICompilerLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        _symbolTable = symbolTable;
        _semanticInfo = semanticInfo;
        _logger = logger ?? NullLogger.Instance;
        _cancellationToken = cancellationToken;
    }

    public DiagnosticBag Diagnostics => _diagnostics;

    public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
    {
        if (annotation == null)
            return SemanticType.Unknown;

        // Check cache
        var cached = _semanticInfo.GetTypeAnnotation(annotation);
        if (cached != null)
            return cached;

        _cancellationToken.ThrowIfCancellationRequested();

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
                DeclaringType = typeParamSymbol.DeclaringType,
                Constraints = typeParamSymbol.Constraints
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
                // Try CLR type fallback for .NET interop types (Exception, etc.)
                var clrTypeSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType(annotation.Name);
                if (clrTypeSymbol != null)
                {
                    result = new UserDefinedType
                    {
                        Name = annotation.Name,
                        Symbol = clrTypeSymbol
                    };
                }
                else
                {
                    // Check if this is an error recovery symbol (from a failed import).
                    // If so, suppress the "type not found" error - the import error was already reported.
                    var symbol = _symbolTable.Lookup(annotation.Name);
                    if (symbol?.IsErrorRecovery == true)
                    {
                        result = SemanticType.Unknown;
                    }
                    else
                    {
                        AddError($"Type '{annotation.Name}' not found", annotation.LineStart, annotation.ColumnStart,
                            code: DiagnosticCodes.Semantic.UndefinedType, span: annotation.Span);
                        result = SemanticType.Unknown;
                    }
                }
            }
        }

        // Handle T !E (Result type) — must come before T? and | None
        if (annotation.ErrorType != null && result != SemanticType.Unknown)
        {
            var errorType = ResolveTypeAnnotation(annotation.ErrorType);
            result = new ResultType
            {
                OkType = result,
                ErrorType = errorType
            };
        }

        // Handle T? (Optional type) — Sharpy native optional
        // Already handled for type aliases in ExpandTypeAlias
        if (annotation.IsOptional && result != SemanticType.Unknown
            && _symbolTable.LookupTypeAlias(annotation.Name) == null)
        {
            result = new OptionalType { UnderlyingType = result };
        }

        // Handle T | None (C# nullable) — .NET interop
        if (annotation.IsCSharpNullable && result != SemanticType.Unknown)
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
        // Handle explicit Optional[T] syntax
        if (annotation.Name == "Optional" && annotation.TypeArguments.Length == 1)
        {
            var underlyingType = ResolveTypeAnnotation(annotation.TypeArguments[0]);
            return new OptionalType { UnderlyingType = underlyingType };
        }

        // Handle explicit Result[T, E] syntax
        if (annotation.Name == "Result" && annotation.TypeArguments.Length == 2)
        {
            var okType = ResolveTypeAnnotation(annotation.TypeArguments[0]);
            var errorType = ResolveTypeAnnotation(annotation.TypeArguments[1]);
            return new ResultType { OkType = okType, ErrorType = errorType };
        }

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
                AddError("Function type requires at least a return type", annotation.LineStart, annotation.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidFunctionType, span: annotation.Span);
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
            AddError($"Generic type '{annotation.Name}' not found", annotation.LineStart, annotation.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedType, span: annotation.Span);
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
                annotation.LineStart, annotation.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                span: annotation.Span);
            return SemanticType.Unknown;
        }

        return new GenericType
        {
            Name = annotation.Name,
            TypeArguments = typeArgs,
            GenericDefinition = typeSymbol
        };
    }

    private SemanticType ExpandTypeAlias(TypeAliasSymbol aliasSymbol, bool isOptional)
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
                aliasSymbol.DeclarationLine, aliasSymbol.DeclarationColumn, code: DiagnosticCodes.Semantic.InvalidTypeAlias);
            return SemanticType.Unknown;
        }

        // Apply optional modifier if present at usage site (T? → OptionalType)
        if (isOptional && result != SemanticType.Unknown)
        {
            result = new OptionalType { UnderlyingType = result };
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

    private void AddError(string message, int? line = null, int? column = null, string? code = null,
        TextSpan? span = null)
    {
        _diagnostics.AddError(message, span, line, column, code: code, phase: CompilerPhase.TypeChecking);
        _logger.LogError(message, line ?? 0, column ?? 0);
    }
}
