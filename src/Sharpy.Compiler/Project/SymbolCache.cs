namespace Sharpy.Compiler.Project;

/// <summary>
/// Serializable representation of a symbol for incremental compilation cache.
/// Uses stable IDs to preserve cross-references after deserialization.
/// </summary>
internal record CachedSymbol
{
    /// <summary>
    /// Stable symbol ID: "{file}:{kind}:{name}"
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Symbol kind: Type, Function, Variable, Module, TypeAlias, TypeParameter
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Symbol name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// File path where the symbol is defined
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Access level: Public, Protected, Private
    /// </summary>
    public string AccessLevel { get; init; } = "Public";

    /// <summary>
    /// Declaration line number (1-based)
    /// </summary>
    public int? DeclarationLine { get; init; }

    /// <summary>
    /// Declaration column number (1-based)
    /// </summary>
    public int? DeclarationColumn { get; init; }

    public int? NameDeclarationLine { get; init; }
    public int? NameDeclarationColumn { get; init; }

    /// <summary>
    /// Declaration span start offset (for LSP go-to-definition)
    /// </summary>
    public int? DeclarationSpanStart { get; init; }

    /// <summary>
    /// Declaration span length (for LSP go-to-definition)
    /// </summary>
    public int? DeclarationSpanLength { get; init; }

    /// <summary>
    /// For VariableSymbol: serialized type ID
    /// </summary>
    public string? TypeId { get; init; }

    /// <summary>
    /// For TypeSymbol: base type ID (reference to another CachedSymbol).
    /// Only set for user-defined bases whose ID resolves within the project.
    /// CLR bases (Exception, etc.) are carried via <see cref="UnresolvedBaseName"/> (#1309).
    /// </summary>
    public string? BaseTypeId { get; init; }

    /// <summary>
    /// For TypeSymbol: serialized base-class type argument annotations (#1287).
    /// Mirrors <see cref="CachedInterfaceEntry.TypeArgs"/>.
    /// </summary>
    public List<string>? BaseTypeArgs { get; init; }

    /// <summary>
    /// For TypeSymbol: unresolved base type name (e.g., "Exception").
    /// Set when the base is a CLR type or cannot be expressed as a registry ID.
    /// The Phase 4b/4c inheritance machinery resolves it on restore (#1309).
    /// </summary>
    public string? UnresolvedBaseName { get; init; }

    /// <summary>
    /// For TypeSymbol: unresolved interface names that cannot be expressed as registry IDs.
    /// </summary>
    public List<string>? UnresolvedInterfaceNames { get; init; }

    /// <summary>
    /// For TypeSymbol: interface entries (symbol ID + type arg annotations)
    /// </summary>
    public List<CachedInterfaceEntry>? InterfaceEntries { get; init; }

    /// <summary>
    /// For TypeSymbol: type kind (Class, Struct, Interface, Enum)
    /// </summary>
    public string? TypeKind { get; init; }

    /// <summary>
    /// For TypeSymbol: whether it's abstract
    /// </summary>
    public bool IsAbstract { get; init; }

    /// <summary>String-backed enum (#1284) — decides emission shape and checker rules.</summary>
    public bool IsStringEnum { get; init; }

    /// <summary>
    /// For TypeSymbol: defining module path
    /// </summary>
    public string? DefiningModule { get; init; }

    /// <summary>
    /// For TypeSymbol: fields (serialized as CachedSymbol with Kind=Variable)
    /// </summary>
    public List<CachedSymbol>? Fields { get; init; }

    /// <summary>
    /// For TypeSymbol: methods (serialized as CachedSymbol with Kind=Function)
    /// </summary>
    public List<CachedSymbol>? Methods { get; init; }

    /// <summary>
    /// For TypeSymbol: constructors (serialized as CachedSymbol with Kind=Function)
    /// </summary>
    public List<CachedSymbol>? Constructors { get; init; }

    /// <summary>
    /// For TypeSymbol: nested types (serialized as CachedSymbol with Kind=Type)
    /// </summary>
    public List<CachedSymbol>? NestedTypes { get; init; }

    /// <summary>
    /// For FunctionSymbol: parameters
    /// </summary>
    public List<CachedParameter>? Parameters { get; init; }

    /// <summary>
    /// For FunctionSymbol: return type ID
    /// </summary>
    public string? ReturnTypeId { get; init; }

    /// <summary>
    /// For FunctionSymbol: whether it's static
    /// </summary>
    public bool IsStatic { get; init; }

    /// <summary>
    /// For FunctionSymbol: whether it's virtual
    /// </summary>
    public bool IsVirtual { get; init; }

    /// <summary>
    /// For FunctionSymbol: whether it's override
    /// </summary>
    public bool IsOverride { get; init; }

    /// <summary>
    /// For FunctionSymbol: whether it's a generator (contains yield)
    /// </summary>
    public bool IsGenerator { get; init; }

    /// <summary>
    /// For ModuleSymbol: canonical (fully-qualified) module name
    /// </summary>
    public string? CanonicalModuleName { get; init; }

    /// <summary>
    /// For ModuleSymbol: the value-position view of <see cref="Semantic.ModuleExports"/> (symbol IDs).
    /// Persisted together with <see cref="ExportedTypeIds"/> and restored as one unit.
    /// </summary>
    public Dictionary<string, string>? ExportIds { get; init; }

    /// <summary>
    /// For ModuleSymbol: the types-only view of <see cref="Semantic.ModuleExports"/> (symbol IDs).
    /// It cannot be re-derived from <see cref="ExportIds"/> alone — a type shadowed there by a
    /// same-named value survives only here — so both views are written and restored together, or a
    /// cache round-trip loses annotation-position resolution (#1105/#1092/#1145).
    /// </summary>
    public Dictionary<string, string>? ExportedTypeIds { get; init; }

    /// <summary>
    /// For ModuleSymbol: whether this is a .NET (CLR) module. Gates the PascalCase fallback in
    /// <c>TryGetExportedType</c>; must round-trip so the fallback survives deserialization (#1105).
    /// </summary>
    public bool IsNetModule { get; init; }

    /// <summary>
    /// For ModuleSymbol: the .NET namespace name (e.g. "System"), or null for non-CLR modules (#1105).
    /// </summary>
    public string? NetNamespaceName { get; init; }

    /// <summary>
    /// Whether this symbol is a re-export from another module
    /// </summary>
    public bool IsReExport { get; init; }

    /// <summary>
    /// For re-exports: original module name
    /// </summary>
    public string? OriginalModule { get; init; }

    /// <summary>
    /// Serialized CodeGenInfo if available
    /// </summary>
    public CachedCodeGenInfo? CodeGenInfo { get; init; }

    /// <summary>
    /// For TypeParameterSymbol: variance annotation (None, Covariant, Contravariant).
    /// Omitted (null) when None.
    /// </summary>
    public string? Variance { get; init; }

    /// <summary>
    /// For FunctionSymbol and TypeSymbol: generic type parameters (e.g. T in def identity[T]).
    /// Null when the symbol is non-generic. Round-tripping these preserves
    /// <see cref="Semantic.Symbol.IsGeneric"/> across an incremental cache reload so a
    /// cross-module generic export stays generic on reuse (#1142).
    /// </summary>
    public List<CachedTypeParameter>? TypeParameters { get; init; }

    /// <summary>
    /// Documentation string (from source docstrings or XML docs)
    /// </summary>
    public string? Documentation { get; init; }

    /// <summary>
    /// Additional properties for extensibility
    /// </summary>
    public Dictionary<string, object>? Properties { get; init; }
}

/// <summary>
/// Cached representation of an InterfaceReference (symbol ID + type arg annotations).
/// </summary>
internal record CachedInterfaceEntry
{
    /// <summary>
    /// Stable symbol ID of the interface TypeSymbol.
    /// </summary>
    public required string SymbolId { get; init; }

    /// <summary>
    /// Serialized TypeAnnotation strings for type arguments (e.g., ["str"] for IEquatable[str]).
    /// Null or empty if the interface has no type arguments.
    /// </summary>
    public List<string>? TypeArgs { get; init; }
}

/// <summary>
/// Serializable representation of a generic type parameter (e.g. T in def identity[T]).
/// Captures the fields the resolver and generic-inference paths consult after a cache reload:
/// the name (used for substitution), variance, default type, and constraints (#1142).
/// </summary>
internal record CachedTypeParameter
{
    /// <summary>
    /// Type parameter name (e.g. "T").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Variance annotation (None, Covariant, Contravariant). Omitted (null) when None.
    /// </summary>
    public string? Variance { get; init; }

    /// <summary>
    /// Serialized default-type TypeAnnotation (e.g. "int" for T = int). Null when no default.
    /// </summary>
    public string? DefaultType { get; init; }

    /// <summary>
    /// Constraint clauses. Each is a discriminated string: "type:&lt;annotation&gt;" for an
    /// interface/type constraint, or a bare marker ("class", "struct", "new", "notnull").
    /// Null or empty when the parameter is unconstrained.
    /// </summary>
    public List<string>? Constraints { get; init; }
}

/// <summary>
/// Serializable representation of a function parameter
/// </summary>
internal record CachedParameter
{
    /// <summary>
    /// Parameter name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Serialized type ID
    /// </summary>
    public required string TypeId { get; init; }

    /// <summary>
    /// Whether the parameter has a default value
    /// </summary>
    public bool HasDefault { get; init; }

    /// <summary>
    /// Whether the parameter is variadic (params)
    /// </summary>
    public bool IsVariadic { get; init; }

    /// <summary>
    /// Whether the parameter is positional-only (before /)
    /// </summary>
    public bool IsPositionalOnly { get; init; }

    /// <summary>
    /// Whether the parameter is keyword-only (after * or *args)
    /// </summary>
    public bool IsKeywordOnly { get; init; }

    public bool IsLateBound { get; init; }

    /// <summary>
    /// Documentation string (from XML doc param tags or source)
    /// </summary>
    public string? Documentation { get; init; }
}

/// <summary>
/// Serializable representation of CodeGenInfo
/// </summary>
internal record CachedCodeGenInfo
{
    /// <summary>
    /// The C# name for this symbol
    /// </summary>
    public required string CSharpName { get; init; }

    /// <summary>
    /// The original Sharpy name
    /// </summary>
    public required string OriginalName { get; init; }

    /// <summary>
    /// Version number for redeclared variables
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Whether this is a module-level symbol
    /// </summary>
    public bool IsModuleLevel { get; init; }

    /// <summary>
    /// Whether this is a constant
    /// </summary>
    public bool IsConstant { get; init; }

    /// <summary>
    /// Whether there are execution order issues
    /// </summary>
    public bool HasExecutionOrderIssues { get; init; }

    /// <summary>
    /// For enum types, whether it's a string enum
    /// </summary>
    public bool IsStringEnum { get; init; }

    /// <summary>
    /// How the symbol was imported
    /// </summary>
    public string ImportKind { get; init; } = "None";

    /// <summary>
    /// For aliased imports, the original name
    /// </summary>
    public string? OriginalImportName { get; init; }

    /// <summary>
    /// For discovery-loaded methods, the original CLR method name.
    /// </summary>
    public string? ClrMethodName { get; init; }
}

/// <summary>
/// Cache entry for a single file's symbols and generated code.
/// </summary>
internal record FileCacheEntry
{
    /// <summary>
    /// SHA-256 hash of the file content when cached
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// All symbols declared in this file
    /// </summary>
    public required List<CachedSymbol> Symbols { get; init; }

    /// <summary>
    /// Generated C# code for this file
    /// </summary>
    public required string GeneratedCSharp { get; init; }

    /// <summary>
    /// Paths of files this file imports (direct dependencies)
    /// </summary>
    public required List<string> Dependencies { get; init; }

    /// <summary>
    /// Module path for this file (e.g., "mypackage.helpers")
    /// </summary>
    public string? ModulePath { get; init; }

    /// <summary>
    /// Source generator outputs that target a declaration in this file.
    /// Key: generator identity (e.g., "GenerateEquals@MyClass"). Nullable
    /// for backwards compat — pre-v13 cache files won't have this.
    /// </summary>
    public Dictionary<string, GeneratedCacheEntry>? GeneratorOutputs { get; init; }
}

/// <summary>
/// Cache entry for a single source generator invocation against a target declaration.
/// Captures everything needed to detect staleness without re-running the generator.
/// </summary>
internal record GeneratedCacheEntry
{
    /// <summary>
    /// SHA-256 hash of the generator source file content. If the generator
    /// source changes (e.g., the @[GenerateEquals] class is edited), the
    /// cached output must be re-generated.
    /// </summary>
    public required string GeneratorHash { get; init; }

    /// <summary>
    /// SHA-256 hash of the decorated declaration's source (typically the
    /// containing class/function body). Distinct from the file-level
    /// ContentHash so a generator output only invalidates when its own
    /// target changes, not when unrelated code in the same file does.
    /// </summary>
    public required string TargetHash { get; init; }

    /// <summary>
    /// SHA-256 hash of the decorator arguments. Null if the decorator
    /// takes no arguments.
    /// </summary>
    public string? ArgumentsHash { get; init; }

    /// <summary>
    /// The Sharpy source string the generator produced. Re-fed into the
    /// pipeline when the cache is valid, skipping generator execution.
    /// </summary>
    public required string GeneratedSource { get; init; }
}
