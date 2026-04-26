using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;
using Sharpy.Compiler.Utilities;

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
    private bool _resolvingGenericAlias;
    private TypeSymbol? _currentTypeContext;
    private bool _isStaticContext;

    public TypeResolver(SymbolTable symbolTable, SemanticInfo semanticInfo, ICompilerLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        _symbolTable = symbolTable;
        _semanticInfo = semanticInfo;
        _logger = logger ?? NullLogger.Instance;
        _cancellationToken = cancellationToken;
        _logger.LogInfo("Type resolver initialized");
    }

    public DiagnosticBag Diagnostics => _diagnostics;

    /// <summary>
    /// Sets the current class/struct/interface context for resolving Self types.
    /// </summary>
    public void SetCurrentTypeContext(TypeSymbol? type) => _currentTypeContext = type;

    /// <summary>
    /// Sets whether the current function context is static (no self parameter).
    /// When true, Self type usage will emit SPY0385.
    /// </summary>
    public void SetIsStaticContext(bool isStatic) => _isStaticContext = isStatic;

    public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
    {
        if (annotation == null)
            return SemanticType.Unknown;

        // Skip cache when resolving inside a generic type alias body — the same
        // annotation objects (e.g., `T` in `tuple[T, T]`) are shared across all
        // usages and would produce stale TypeParameterType results.
        if (!_resolvingGenericAlias)
        {
            var cached = _semanticInfo.GetTypeAnnotation(annotation);
            if (cached != null)
                return cached;
        }

        _cancellationToken.ThrowIfCancellationRequested();

        SemanticType result;

        // Handle 'auto' keyword for type inference
        if (annotation.Name == "auto")
        {
            result = SemanticType.Unknown;
            _semanticInfo.SetTypeAnnotation(annotation, result);
            return result;
        }

        // Handle 'Self' type — resolves to the enclosing class/struct/interface
        if (annotation.Name == BuiltinNames.Self)
        {
            if (_currentTypeContext == null)
            {
                AddError("'Self' can only be used inside a class, struct, or interface definition",
                    annotation.LineStart, annotation.ColumnStart,
                    code: DiagnosticCodes.Semantic.SelfOutsideClass, span: annotation.Span);
                result = SemanticType.Unknown;
            }
            else if (_isStaticContext)
            {
                AddError("'Self' cannot be used in static methods",
                    annotation.LineStart, annotation.ColumnStart,
                    code: DiagnosticCodes.Semantic.SelfInStaticMethod, span: annotation.Span);
                result = SemanticType.Unknown;
            }
            else
            {
                result = new SelfType { DeclaringType = _currentTypeContext };
            }
            _semanticInfo.SetTypeAnnotation(annotation, result);
            return result;
        }

        // Handle LiteralString compile-time type (PEP 675)
        if (annotation.Name == "LiteralString")
        {
            result = LiteralStringType.Instance;
            _semanticInfo.SetTypeAnnotation(annotation, result);
            return result;
        }

        // Handle Template type annotation (PEP 750)
        if (annotation.Name == BuiltinNames.Template)
        {
            result = TemplateType.Instance;
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
            if (aliasSymbol.TypeParameters.Count > 0)
            {
                // Generic type alias: resolve type arguments and substitute
                if (annotation.TypeArguments.Length == 0)
                {
                    AddError($"Generic type alias '{aliasSymbol.Name}' requires {aliasSymbol.TypeParameters.Count} type argument(s)",
                        annotation.LineStart, annotation.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeAliasArityMismatch, span: annotation.Span);
                    result = SemanticType.Unknown;
                }
                else if (annotation.TypeArguments.Length != aliasSymbol.TypeParameters.Count)
                {
                    AddError($"Type alias '{aliasSymbol.Name}' expects {aliasSymbol.TypeParameters.Count} type argument(s) but got {annotation.TypeArguments.Length}",
                        annotation.LineStart, annotation.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeAliasArityMismatch, span: annotation.Span);
                    result = SemanticType.Unknown;
                }
                else
                {
                    var typeArgs = annotation.TypeArguments.Select(ResolveTypeAnnotation).ToList();
                    result = ExpandGenericTypeAlias(aliasSymbol, typeArgs, annotation.IsOptional);
                }
            }
            else
            {
                result = ExpandTypeAlias(aliasSymbol, annotation.IsOptional);
            }
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
                Constraints = typeParamSymbol.Constraints,
                Variance = typeParamSymbol.Variance
            };
        }
        // Look up user-defined type
        else
        {
            var typeSymbol = _symbolTable.LookupType(annotation.Name)
                ?? LookupNestedType(annotation.Name);

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
                        var typeMessage = $"Type '{annotation.Name}' not found";
                        var typeSuggestion = FindTypeSuggestion(annotation.Name);
                        if (typeSuggestion != null)
                            typeMessage += $". Did you mean '{typeSuggestion}'?";
                        AddError(typeMessage, annotation.LineStart, annotation.ColumnStart,
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

        // Cache the result (skip when resolving inside generic alias body)
        if (!_resolvingGenericAlias)
        {
            _semanticInfo.SetTypeAnnotation(annotation, result);
        }
        return result;
    }

    private TypeSymbol? LookupNestedType(string dottedName)
    {
        if (!dottedName.Contains('.', StringComparison.Ordinal))
            return null;

        var parts = dottedName.Split('.');
        var outerSymbol = _symbolTable.LookupType(parts[0]);
        if (outerSymbol == null)
            return null;

        for (int i = 1; i < parts.Length && outerSymbol != null; i++)
        {
            var nested = outerSymbol.NestedTypes.FirstOrDefault(n => n.Name == parts[i]);
            outerSymbol = nested;
        }

        return outerSymbol;
    }

    private bool TryResolveBuiltinType(string name, out SemanticType type)
    {
        type = name switch
        {
            BuiltinNames.Int => SemanticType.Int,
            BuiltinNames.Long => SemanticType.Long,
            BuiltinNames.Float => SemanticType.Float,       // float -> double (per spec)
            BuiltinNames.Float32 => SemanticType.Float32,   // float32 -> C# float
            "float64" => SemanticType.Double,    // float64 -> double
            BuiltinNames.Double => SemanticType.Double,
            BuiltinNames.Decimal => SemanticType.Decimal,
            BuiltinNames.Bool => SemanticType.Bool,
            BuiltinNames.Str => SemanticType.Str,
            BuiltinNames.None => SemanticType.Void,
            BuiltinNames.Object => SemanticType.Object,
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

        // Special handling for array types - array[T] maps to .NET T[]
        if (annotation.Name == BuiltinNames.Array)
        {
            if (annotation.TypeArguments.Length != 1)
            {
                AddError("Array type 'array' requires exactly 1 type argument",
                    annotation.LineStart, annotation.ColumnStart,
                    code: DiagnosticCodes.Semantic.WrongArgumentCount, span: annotation.Span);
                return SemanticType.Unknown;
            }

            var elementType = ResolveTypeAnnotation(annotation.TypeArguments[0]);
            return new GenericType
            {
                Name = BuiltinNames.Array,
                TypeArguments = new List<SemanticType> { elementType }
            };
        }

        // Special handling for tuple types - they have variable arity (tuple[int], tuple[int, str], etc.)
        if (annotation.Name == BuiltinNames.Tuple)
        {
            var elementTypes = annotation.TypeArguments
                .Select(ResolveTypeAnnotation)
                .ToList();

            var tupleType = new TupleType { ElementTypes = elementTypes };

            // Propagate element names for named tuples
            if (!annotation.TupleElementNames.IsEmpty)
            {
                tupleType = tupleType with { ElementNames = annotation.TupleElementNames };
            }

            return tupleType;
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

        var typeSymbol = _symbolTable.LookupType(annotation.Name)
            ?? LookupNestedType(annotation.Name);
        if (typeSymbol == null)
        {
            var genericMessage = $"Generic type '{annotation.Name}' not found";
            var genericSuggestion = FindTypeSuggestion(annotation.Name);
            if (genericSuggestion != null)
                genericMessage += $". Did you mean '{genericSuggestion}'?";
            AddError(genericMessage, annotation.LineStart, annotation.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedType, span: annotation.Span);
            return SemanticType.Unknown;
        }

        // Resolve type arguments
        var typeArgs = annotation.TypeArguments
            .Select(ResolveTypeAnnotation)
            .ToList();

        // Validate type argument count (PEP 696: allow fewer if remaining have defaults)
        if (typeSymbol.IsGeneric && typeArgs.Count != typeSymbol.TypeParameters.Count)
        {
            if (typeArgs.Count < typeSymbol.TypeParameters.Count)
            {
                // Fill in defaults for missing type arguments
                bool allDefaulted = true;
                for (int i = typeArgs.Count; i < typeSymbol.TypeParameters.Count; i++)
                {
                    var tp = typeSymbol.TypeParameters[i];
                    if (tp.DefaultType != null)
                    {
                        typeArgs.Add(ResolveTypeAnnotation(tp.DefaultType));
                    }
                    else
                    {
                        allDefaulted = false;
                        break;
                    }
                }

                if (!allDefaulted)
                {
                    AddError($"Type '{annotation.Name}' expects {typeSymbol.TypeParameters.Count} type arguments but got {typeArgs.Count}",
                        annotation.LineStart, annotation.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: annotation.Span);
                    return SemanticType.Unknown;
                }
            }
            else
            {
                AddError($"Type '{annotation.Name}' expects {typeSymbol.TypeParameters.Count} type arguments but got {typeArgs.Count}",
                    annotation.LineStart, annotation.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: annotation.Span);
                return SemanticType.Unknown;
            }
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

    /// <summary>
    /// Expands a generic type alias with concrete type arguments.
    /// For `type Cb[T] = (T) -> None` used as `Cb[int]`, registers T as a TypeParameterSymbol
    /// in a temporary scope, resolves the alias body (producing TypeParameterType references),
    /// then substitutes those with the concrete type arguments.
    /// </summary>
    private SemanticType ExpandGenericTypeAlias(TypeAliasSymbol aliasSymbol, IReadOnlyList<SemanticType> typeArgs, bool isOptional)
    {
        // Enter temporary scope for type parameters
        _symbolTable.EnterScope("type-alias-params");
        try
        {
            // Register type parameter symbols so ResolveTypeAnnotation finds them
            for (int i = 0; i < aliasSymbol.TypeParameters.Count; i++)
            {
                var paramDef = aliasSymbol.TypeParameters[i];
                var typeParamSymbol = new TypeParameterSymbol
                {
                    Name = paramDef.Name,
                    Kind = SymbolKind.TypeParameter,
                    AccessLevel = AccessLevel.Public,
                    DeclarationLine = paramDef.LineStart,
                    DeclarationColumn = paramDef.ColumnStart,
                    NameDeclarationLine = paramDef.LineStart,
                    NameDeclarationColumn = paramDef.ColumnStart
                };
                _symbolTable.Define(typeParamSymbol);
            }

            // Resolve the alias body — type parameters resolve to TypeParameterType.
            // Disable annotation caching because the alias body's annotation objects are
            // shared across all usages and would produce stale TypeParameterType results.
            _resolvingGenericAlias = true;
            SemanticType expanded;
            try
            {
                if (aliasSymbol.TypeAnnotation != null)
                {
                    expanded = ResolveTypeAnnotation(aliasSymbol.TypeAnnotation);
                }
                else if (aliasSymbol.FunctionType != null)
                {
                    expanded = ResolveFunctionType(aliasSymbol.FunctionType);
                }
                else
                {
                    return SemanticType.Unknown;
                }
            }
            finally
            {
                _resolvingGenericAlias = false;
            }

            // Build substitution map: TypeParameter name → concrete type
            var substitution = new Dictionary<string, SemanticType>();
            for (int i = 0; i < aliasSymbol.TypeParameters.Count; i++)
            {
                substitution[aliasSymbol.TypeParameters[i].Name] = typeArgs[i];
            }

            // Substitute TypeParameterType references with concrete types
            var result = SubstituteTypeParameters(expanded, substitution);

            if (isOptional && result != SemanticType.Unknown)
            {
                result = new OptionalType { UnderlyingType = result };
            }

            return result;
        }
        finally
        {
            _symbolTable.ExitScope();
        }
    }

    /// <summary>
    /// Recursively substitutes TypeParameterType references in a SemanticType with concrete types.
    /// </summary>
    private static SemanticType SubstituteTypeParameters(SemanticType type, Dictionary<string, SemanticType> substitution)
    {
        return TypeSubstitution.Apply(type, substitution);
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

    private string? FindTypeSuggestion(string name)
    {
        var typeNames = _symbolTable.GetVisibleSymbolNames()
            .Where(n => _symbolTable.LookupType(n) != null);
        return EditDistance.FindClosestMatch(name, typeNames);
    }

    private void AddError(string message, int? line = null, int? column = null, string? code = null,
        TextSpan? span = null)
    {
        _diagnostics.AddPhaseError(message, CompilerPhase.TypeChecking,
            span, line, column, code: code, logger: _logger);
    }
}
