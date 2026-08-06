using System.Collections.Frozen;
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

    /// <summary>
    /// Suppresses the <see cref="SemanticInfo"/> annotation cache. Set while resolving a body whose
    /// annotation objects are SHARED across every use site and would therefore cache a result
    /// belonging to one instantiation — a generic type alias body (<c>T</c> in <c>tuple[T, T]</c>)
    /// and, since #1245, a type parameter's default (<c>K</c> in <c>class Dup[K, V = K]</c>).
    /// </summary>
    private bool _suppressAnnotationCache;

    /// <summary>
    /// Silences this resolver while a type-parameter default is resolved at a USE site. Whether a
    /// default is well-formed is a property of the declaration, and
    /// <c>TypeChecker.ValidateTypeParameterDefaultOrdering</c> resolves every default there and
    /// reports once (#1245). Without this the use site re-reported the same defect at every
    /// reference — and, for a default naming an enclosing declaration's parameter, stacked a
    /// misleading "Type 'T' not found" underneath the SPY0347 that names the actual rule.
    /// </summary>
    private bool _suppressDiagnostics;
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

        // Skip the cache while resolving a shared annotation body — see _suppressAnnotationCache.
        if (!_suppressAnnotationCache)
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
                ?? LookupNestedType(annotation.Name)
                ?? LookupModuleQualifiedType(annotation.Name);

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
        if (!_suppressAnnotationCache)
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

    /// <summary>
    /// Resolves a dotted name like "argparse.Namespace" or "email.message.Message" where the
    /// leading part(s) are imported modules and the final part is an exported type. Walks nested
    /// <see cref="ModuleSymbol.Exports"/> dictionaries, applying a PascalCase fallback for .NET
    /// modules (mirrors the module branch in TypeChecker.Expressions.Access.cs).
    /// </summary>
    private TypeSymbol? LookupModuleQualifiedType(string dottedName)
    {
        if (!dottedName.Contains('.', StringComparison.Ordinal))
            return null;

        var parts = dottedName.Split('.');

        // The leading part must resolve to an imported module.
        if (_symbolTable.Lookup(parts[0]) is not ModuleSymbol moduleSymbol)
            return null;

        return moduleSymbol.ResolveQualifiedType(parts, startIndex: 1);
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

        if (type != null)
            return true;

        // Fall back to PrimitiveCatalog for CLR interop types (short, byte, uint, etc.)
        var primitiveInfo = PrimitiveCatalog.GetByName(name);
        if (primitiveInfo != null)
        {
            var resolved = ClrTypeToSemanticType(primitiveInfo.ClrType);
            if (resolved != null)
            {
                type = resolved;
                return true;
            }
        }

        return false;
    }

    private static SemanticType? ClrTypeToSemanticType(Type clrType)
    {
        if (clrType == typeof(sbyte))
            return SemanticType.SByte;
        if (clrType == typeof(byte))
            return SemanticType.Byte;
        if (clrType == typeof(short))
            return SemanticType.Short;
        if (clrType == typeof(ushort))
            return SemanticType.UShort;
        if (clrType == typeof(int))
            return SemanticType.Int;
        if (clrType == typeof(long))
            return SemanticType.Long;
        if (clrType == typeof(uint))
            return SemanticType.UInt;
        if (clrType == typeof(ulong))
            return SemanticType.ULong;
        if (clrType == typeof(float))
            return SemanticType.Float32;
        if (clrType == typeof(double))
            return SemanticType.Double;
        if (clrType == typeof(decimal))
            return SemanticType.Decimal;
        if (clrType == typeof(bool))
            return SemanticType.Bool;
        if (clrType == typeof(string))
            return SemanticType.Str;
        if (clrType == typeof(void))
            return SemanticType.Void;
        return null;
    }

    /// <summary>
    /// CamelCase interop spellings of the builtin collections mapped to their lowercase builtin
    /// name. Reverse of the <c>SharpyToClrNameMap</c> in <see cref="Discovery.CachedModuleDiscovery"/>.
    /// Consulted only when the CamelCase name would otherwise resolve to a non-generic static
    /// factory companion (currently only <c>Dict</c>, whose <c>Sharpy.Dict</c> holds
    /// <c>dict.fromkeys</c>); see the redirect in <see cref="ResolveGenericType"/> (#1134).
    /// </summary>
    private static readonly FrozenDictionary<string, string> BuiltinCamelCaseAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Dict"] = BuiltinNames.Dict,
            ["List"] = BuiltinNames.List,
            ["Set"] = BuiltinNames.Set,
            ["Bytes"] = BuiltinNames.Bytes,
            ["Str"] = BuiltinNames.Str,
        }.ToFrozenDictionary(StringComparer.Ordinal);

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

        // Module-qualified generic type (e.g. difflib.SequenceMatcher[str], geometry.Box[int]).
        // Track this so the GenericType name can be normalized to the bare type name below —
        // the dotted annotation name would otherwise mismatch the bare name produced by
        // generic instantiation (Box[int]) and emit a false assignment error.
        var isModuleQualified = false;
        if (typeSymbol == null)
        {
            typeSymbol = LookupModuleQualifiedType(annotation.Name);
            if (typeSymbol != null)
                isModuleQualified = true;
        }

        typeSymbol ??= _symbolTable.BuiltinRegistry.TryResolveClrType(annotation.Name);

        // #1134: A builtin collection that also ships a non-generic static factory companion
        // shadows its real generic type in the CLR fallback above. `Sharpy.Dict` (the
        // `dict.fromkeys` helper) is such a static class, so the CamelCase interop spelling
        // `Dict[K, V]` resolves to it — annotatable, but exposing none of the collection
        // protocols, so `d[k]` reports SPY0320 while native `dict[K, V]` indexes. When the only
        // CLR match is such a static shadow, redirect to the builtin so the CamelCase surface is
        // a true alias of the lowercase spelling for type-checking, protocols, and codegen alike.
        // The static-shadow gate keeps `List`/`Set` (no companion) resolving as before.
        var normalizedToBuiltin = false;
        if (typeSymbol is { ClrType: { IsAbstract: true, IsSealed: true, IsInterface: false } }
            && BuiltinCamelCaseAliases.TryGetValue(annotation.Name, out var builtinAlias)
            && _symbolTable.BuiltinRegistry.GetType(builtinAlias) is { } aliasedBuiltin)
        {
            typeSymbol = aliasedBuiltin;
            normalizedToBuiltin = true;
        }

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
                        typeArgs.Add(ResolveTypeParameterDefault(typeSymbol.TypeParameters, i, typeArgs));
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
            // For module-qualified generics, use the bare type name so identity matches the
            // GenericType produced by `module.Type[T](...)` instantiation. Emission relies on
            // GenericDefinition (which carries DefiningModule) to fully-qualify the name.
            // A CamelCase builtin alias normalized to its lowercase builtin (#1134) likewise
            // takes the builtin's name so its representation is identical to the native spelling.
            Name = isModuleQualified || normalizedToBuiltin ? typeSymbol.Name : annotation.Name,
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
    /// The one answer to "what does a PEP-696 type-parameter default MEAN" (#1245). Resolves the
    /// default of <paramref name="typeParameters"/>[<paramref name="index"/>] with the parameters
    /// declared BEFORE it in scope, then substitutes each of those for whatever this reference
    /// supplied, so <c>class Dup[K, V = K]</c> referenced as <c>Dup[str]</c> is <c>Dup[str, str]</c>.
    ///
    /// <para>Only preceding parameters are registered, which is the PEP's left-to-right rule
    /// expressed as scope rather than as a separate check: a default naming a LATER parameter or one
    /// from an enclosing scope cannot resolve here, and is refused at the declaration by
    /// <c>TypeChecker.ValidateTypeParameterDefaultOrdering</c> (SPY0347) so the refusal fires whether
    /// or not the declaration is ever used.</para>
    ///
    /// <para><paramref name="boundArguments"/> is the vector resolved so far; entries beyond it stay
    /// as <see cref="TypeParameterType"/>, which is exactly what the declaration-time callers want
    /// (they check the default's shape, not a particular instantiation).</para>
    ///
    /// <para><paramref name="reportDiagnostics"/> defaults to <c>false</c> because most callers are
    /// use sites, and a defect in a default belongs to the declaration that wrote it, not to every
    /// reference that omits the type argument. Only the declaration-site validator passes true.</para>
    /// </summary>
    public SemanticType ResolveTypeParameterDefault(
        IReadOnlyList<TypeParameterDef> typeParameters,
        int index,
        IReadOnlyList<SemanticType> boundArguments,
        bool reportDiagnostics = false)
    {
        var defaultAnnotation = typeParameters[index].DefaultType;
        if (defaultAnnotation == null)
            return SemanticType.Unknown;

        var wasSuppressed = _suppressDiagnostics;
        _suppressDiagnostics = !reportDiagnostics;
        try
        {
            return ResolveInTypeParameterScope(
                typeParameters,
                count: index,
                resolve: () => ResolveTypeAnnotation(defaultAnnotation),
                boundArguments);
        }
        finally
        {
            _suppressDiagnostics = wasSuppressed;
        }
    }

    /// <summary>
    /// Registers the first <paramref name="count"/> of <paramref name="typeParameters"/> in a
    /// temporary scope, runs <paramref name="resolve"/> there with the annotation cache suppressed
    /// (the annotation objects are shared across every use site), then substitutes each registered
    /// parameter for its entry in <paramref name="boundArguments"/>. Follows the mechanism
    /// <see cref="ExpandGenericTypeAlias"/> established for the same problem — resolve a body
    /// written in terms of type parameters, then close it over this reference's arguments.
    /// </summary>
    private SemanticType ResolveInTypeParameterScope(
        IReadOnlyList<TypeParameterDef> typeParameters,
        int count,
        Func<SemanticType> resolve,
        IReadOnlyList<SemanticType> boundArguments)
    {
        _symbolTable.EnterScope("type-parameter-scope");
        var wasSuppressed = _suppressAnnotationCache;
        try
        {
            for (int i = 0; i < count; i++)
            {
                var paramDef = typeParameters[i];
                _symbolTable.Define(new TypeParameterSymbol
                {
                    Name = paramDef.Name,
                    Kind = SymbolKind.TypeParameter,
                    AccessLevel = AccessLevel.Public,
                    Constraints = paramDef.Constraints,
                    DeclarationLine = paramDef.LineStart,
                    DeclarationColumn = paramDef.ColumnStart,
                    NameDeclarationLine = paramDef.LineStart,
                    NameDeclarationColumn = paramDef.ColumnStart
                });
            }

            _suppressAnnotationCache = true;
            var resolved = resolve();
            if (resolved is UnknownType)
                return resolved;

            var substitution = new Dictionary<string, SemanticType>(StringComparer.Ordinal);
            for (int i = 0; i < count && i < boundArguments.Count; i++)
                substitution[typeParameters[i].Name] = boundArguments[i];

            return substitution.Count == 0 ? resolved : TypeSubstitution.Apply(resolved, substitution);
        }
        finally
        {
            _suppressAnnotationCache = wasSuppressed;
            _symbolTable.ExitScope();
        }
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
            // The previous value is restored rather than cleared, so an expansion nested inside
            // another suppressed resolution does not re-enable the cache for its remainder.
            var wasSuppressed = _suppressAnnotationCache;
            _suppressAnnotationCache = true;
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
                _suppressAnnotationCache = wasSuppressed;
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
        if (_suppressDiagnostics)
            return;

        _diagnostics.AddPhaseError(message, CompilerPhase.TypeChecking,
            span, line, column, code: code, logger: _logger);
    }
}
