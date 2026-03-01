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
    /// For TypeSymbol: base type ID (reference to another CachedSymbol)
    /// </summary>
    public string? BaseTypeId { get; init; }

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
    /// For ModuleSymbol: exports (symbol IDs)
    /// </summary>
    public Dictionary<string, string>? ExportIds { get; init; }

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
}
