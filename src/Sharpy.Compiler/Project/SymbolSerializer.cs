using System.Collections.Immutable;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Utilities;
using TypeAnnotation = Sharpy.Compiler.Parser.Ast.TypeAnnotation;
using TypeParameterVariance = Sharpy.Compiler.Parser.Ast.TypeParameterVariance;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Serializes and deserializes symbols for incremental compilation cache.
/// Uses stable IDs to preserve cross-references after deserialization.
/// </summary>
internal static class SymbolSerializer
{
    /// <summary>
    /// Computes a stable ID for a symbol based on its file, kind, and name.
    /// </summary>
    public static string ComputeSymbolId(Symbol symbol, string filePath)
    {
        var normalizedPath = PathNormalizer.Normalize(filePath);
        return $"{normalizedPath}:{symbol.Kind}:{symbol.Name}";
    }

    /// <summary>
    /// Serializes a Symbol to a CachedSymbol for storage.
    /// </summary>
    public static CachedSymbol Serialize(Symbol symbol, string filePath)
    {
        var id = ComputeSymbolId(symbol, filePath);

        return symbol switch
        {
            TypeSymbol ts => SerializeTypeSymbol(ts, id, filePath),
            FunctionSymbol fs => SerializeFunctionSymbol(fs, id, filePath),
            VariableSymbol vs => SerializeVariableSymbol(vs, id, filePath),
            ModuleSymbol ms => SerializeModuleSymbol(ms, id, filePath),
            TypeAliasSymbol tas => SerializeTypeAliasSymbol(tas, id, filePath),
            TypeParameterSymbol tps => SerializeTypeParameterSymbol(tps, id, filePath),
            _ => throw new NotSupportedException($"Cannot serialize symbol of type {symbol.GetType().Name}")
        };
    }

    /// <summary>
    /// Deserializes a CachedSymbol back to a Symbol.
    /// </summary>
    /// <param name="cached">The cached symbol to deserialize.</param>
    /// <param name="symbolRegistry">Registry of already-deserialized symbols for resolving cross-references.</param>
    /// <param name="typeResolver">Optional function to resolve type IDs to SemanticTypes.</param>
    public static Symbol Deserialize(
        CachedSymbol cached,
        Dictionary<string, Symbol> symbolRegistry,
        Func<string, SemanticType>? typeResolver = null)
    {
        typeResolver ??= ResolveTypeFromId;

        return cached.Kind switch
        {
            "Type" => DeserializeTypeSymbol(cached, symbolRegistry, typeResolver),
            "Function" => DeserializeFunctionSymbol(cached, typeResolver),
            "Variable" => DeserializeVariableSymbol(cached, typeResolver),
            "Module" => DeserializeModuleSymbol(cached, symbolRegistry),
            "TypeAlias" => DeserializeTypeAliasSymbol(cached),
            "TypeParameter" => DeserializeTypeParameterSymbol(cached),
            _ => throw new NotSupportedException($"Cannot deserialize symbol of kind {cached.Kind}")
        };
    }

    #region Serialization Methods

    private static CachedSymbol SerializeTypeSymbol(TypeSymbol ts, string id, string filePath)
    {
        // Serialize fields as nested CachedSymbols
        List<CachedSymbol>? fields = null;
        if (ts.Fields.Count > 0)
        {
            fields = ts.Fields.Select(f =>
                SerializeVariableSymbol(f, ComputeSymbolId(f, filePath), filePath)).ToList();
        }

        // Serialize methods as nested CachedSymbols
        List<CachedSymbol>? methods = null;
        if (ts.Methods.Count > 0)
        {
            methods = ts.Methods.Select(m =>
                SerializeFunctionSymbol(m, ComputeSymbolId(m, filePath), filePath)).ToList();
        }

        // Serialize constructors as nested CachedSymbols
        List<CachedSymbol>? constructors = null;
        if (ts.Constructors.Count > 0)
        {
            constructors = ts.Constructors.Select(c =>
                SerializeFunctionSymbol(c, ComputeSymbolId(c, filePath), filePath)).ToList();
        }

        return new CachedSymbol
        {
            Id = id,
            Kind = "Type",
            Name = ts.Name,
            FilePath = PathNormalizer.Normalize(filePath),
            AccessLevel = ts.AccessLevel.ToString(),
            DeclarationLine = ts.DeclarationLine,
            DeclarationColumn = ts.DeclarationColumn,
            DeclarationSpanStart = ts.DeclarationSpan?.Start,
            DeclarationSpanLength = ts.DeclarationSpan?.Length,
            TypeKind = ts.TypeKind.ToString(),
            IsAbstract = ts.IsAbstract,
            DefiningModule = ts.DefiningModule,
            BaseTypeId = ts.BaseType != null ? ComputeSymbolId(ts.BaseType, ts.BaseType.DefiningFilePath ?? filePath) : null,
            InterfaceEntries = ts.Interfaces.Count > 0
                ? ts.Interfaces.Select(i => new CachedInterfaceEntry
                {
                    SymbolId = ComputeSymbolId(i.Definition, i.Definition.DefiningFilePath ?? filePath),
                    TypeArgs = i.TypeArgAnnotations.IsDefaultOrEmpty
                          ? null
                          : i.TypeArgAnnotations.Select(SerializeTypeAnnotation).ToList()
                }).ToList()
                : null,
            Fields = fields,
            Methods = methods,
            Constructors = constructors,
            IsReExport = ts.IsReExport,
            OriginalModule = ts.OriginalModule,
            CodeGenInfo = SerializeCodeGenInfo(ts.CodeGenInfo)
        };
    }

    private static CachedSymbol SerializeFunctionSymbol(FunctionSymbol fs, string id, string filePath)
    {
        return new CachedSymbol
        {
            Id = id,
            Kind = "Function",
            Name = fs.Name,
            FilePath = PathNormalizer.Normalize(filePath),
            AccessLevel = fs.AccessLevel.ToString(),
            DeclarationLine = fs.DeclarationLine,
            DeclarationColumn = fs.DeclarationColumn,
            DeclarationSpanStart = fs.DeclarationSpan?.Start,
            DeclarationSpanLength = fs.DeclarationSpan?.Length,
            Parameters = fs.Parameters.Select(SerializeParameter).ToList(),
            ReturnTypeId = SerializeType(fs.ReturnType),
            IsStatic = fs.IsStatic,
            IsAbstract = fs.IsAbstract,
            IsVirtual = fs.IsVirtual,
            IsOverride = fs.IsOverride,
            IsGenerator = fs.IsGenerator,
            IsReExport = fs.IsReExport,
            OriginalModule = fs.OriginalModule,
            CodeGenInfo = SerializeCodeGenInfo(fs.CodeGenInfo)
        };
    }

    private static CachedSymbol SerializeVariableSymbol(VariableSymbol vs, string id, string filePath)
    {
        return new CachedSymbol
        {
            Id = id,
            Kind = "Variable",
            Name = vs.Name,
            FilePath = PathNormalizer.Normalize(filePath),
            AccessLevel = vs.AccessLevel.ToString(),
            DeclarationLine = vs.DeclarationLine,
            DeclarationColumn = vs.DeclarationColumn,
            DeclarationSpanStart = vs.DeclarationSpan?.Start,
            DeclarationSpanLength = vs.DeclarationSpan?.Length,
            TypeId = SerializeType(vs.Type),
            IsReExport = vs.IsReExport,
            OriginalModule = vs.OriginalModule,
            CodeGenInfo = SerializeCodeGenInfo(vs.CodeGenInfo),
            Properties = new Dictionary<string, object>
            {
                ["IsParameter"] = vs.IsParameter,
                ["IsConstant"] = vs.IsConstant,
                ["HasDefaultValue"] = vs.HasDefaultValue
            }
        };
    }

    private static CachedSymbol SerializeModuleSymbol(ModuleSymbol ms, string id, string filePath)
    {
        return new CachedSymbol
        {
            Id = id,
            Kind = "Module",
            Name = ms.Name,
            FilePath = PathNormalizer.Normalize(ms.FilePath),
            AccessLevel = ms.AccessLevel.ToString(),
            DeclarationLine = ms.DeclarationLine,
            DeclarationColumn = ms.DeclarationColumn,
            DeclarationSpanStart = ms.DeclarationSpan?.Start,
            DeclarationSpanLength = ms.DeclarationSpan?.Length,
            ExportIds = ms.Exports.ToDictionary(
                kvp => kvp.Key,
                kvp => ComputeSymbolId(kvp.Value, ms.FilePath)),
            IsReExport = ms.IsReExport,
            OriginalModule = ms.OriginalModule,
            CodeGenInfo = SerializeCodeGenInfo(ms.CodeGenInfo)
        };
    }

    private static CachedSymbol SerializeTypeAliasSymbol(TypeAliasSymbol tas, string id, string filePath)
    {
        return new CachedSymbol
        {
            Id = id,
            Kind = "TypeAlias",
            Name = tas.Name,
            FilePath = PathNormalizer.Normalize(filePath),
            AccessLevel = tas.AccessLevel.ToString(),
            DeclarationLine = tas.DeclarationLine,
            DeclarationColumn = tas.DeclarationColumn,
            DeclarationSpanStart = tas.DeclarationSpan?.Start,
            DeclarationSpanLength = tas.DeclarationSpan?.Length,
            // TypeAnnotation is AST-based, we don't serialize it (reconstructed from source on reparse)
            IsReExport = tas.IsReExport,
            OriginalModule = tas.OriginalModule,
            CodeGenInfo = SerializeCodeGenInfo(tas.CodeGenInfo)
        };
    }

    private static CachedSymbol SerializeTypeParameterSymbol(TypeParameterSymbol tps, string id, string filePath)
    {
        return new CachedSymbol
        {
            Id = id,
            Kind = "TypeParameter",
            Name = tps.Name,
            FilePath = PathNormalizer.Normalize(filePath),
            AccessLevel = tps.AccessLevel.ToString(),
            DeclarationLine = tps.DeclarationLine,
            DeclarationColumn = tps.DeclarationColumn,
            DeclarationSpanStart = tps.DeclarationSpan?.Start,
            DeclarationSpanLength = tps.DeclarationSpan?.Length,
            Variance = tps.Variance != TypeParameterVariance.None ? tps.Variance.ToString() : null,
            IsReExport = tps.IsReExport,
            OriginalModule = tps.OriginalModule,
            CodeGenInfo = SerializeCodeGenInfo(tps.CodeGenInfo)
        };
    }

    private static CachedParameter SerializeParameter(ParameterSymbol ps)
    {
        return new CachedParameter
        {
            Name = ps.Name,
            TypeId = SerializeType(ps.Type),
            HasDefault = ps.HasDefault,
            IsVariadic = ps.IsVariadic,
            IsPositionalOnly = ps.IsPositionalOnly,
            IsKeywordOnly = ps.IsKeywordOnly
        };
    }

    private static CachedCodeGenInfo? SerializeCodeGenInfo(CodeGenInfo? cgi)
    {
        if (cgi == null)
            return null;

        return new CachedCodeGenInfo
        {
            CSharpName = cgi.CSharpName,
            OriginalName = cgi.OriginalName,
            Version = cgi.Version,
            IsModuleLevel = cgi.IsModuleLevel,
            IsConstant = cgi.IsConstant,
            HasExecutionOrderIssues = cgi.HasExecutionOrderIssues,
            IsStringEnum = cgi.IsStringEnum,
            ImportKind = cgi.ImportKind.ToString(),
            OriginalImportName = cgi.OriginalImportName
        };
    }

    /// <summary>
    /// Serializes a SemanticType to a string ID for storage.
    /// </summary>
    private static string SerializeType(SemanticType type)
    {
        return type switch
        {
            BuiltinType bt => $"builtin:{bt.Name}",
            GenericType gt => $"generic:{gt.Name}[{string.Join(",", gt.TypeArguments.Select(SerializeType))}]",
            UserDefinedType udt => $"user:{udt.Name}",
            NullableType nt => $"nullable:{SerializeType(nt.UnderlyingType)}",
            OptionalType ot => $"optional:{SerializeType(ot.UnderlyingType)}",
            FunctionType ft => $"func:({string.Join(",", ft.ParameterTypes.Select(SerializeType))})->{SerializeType(ft.ReturnType)}",
            TupleType tt => $"tuple:[{string.Join(",", tt.ElementTypes.Select(SerializeType))}]",
            VoidType => "void:",
            UnknownType => "unknown:",
            ModuleType mt => $"module:{mt.Symbol.Name}",
            TypeParameterType tpt => $"typeparam:{tpt.Name}",
            ResultType rt => $"result:{SerializeType(rt.OkType)}!{SerializeType(rt.ErrorType)}",

            // GenericFunctionType: serialize as the underlying function with type args marker
            GenericFunctionType gft => $"gfunc:{gft.FunctionSymbol.Name}[{string.Join(",", gft.TypeArguments.Select(SerializeType))}]",

            // UnionType: placeholder for v0.2.x tagged unions
            UnionType ut => $"union:{ut.Name}[{string.Join(",", ut.CaseTypes.Select(SerializeType))}]",

            // TaskType: async task wrapper
            TaskType tt => tt.ResultType == null
                ? "task:"
                : $"task:{SerializeType(tt.ResultType)}",

            // Exhaustive check - if a new SemanticType is added, this will fail at runtime
            _ => throw new NotSupportedException(
                $"Unhandled SemanticType in SerializeType: {type.GetType().Name}")
        };
    }

    #endregion

    #region Deserialization Methods

    private static Text.TextSpan? DeserializeDeclarationSpan(CachedSymbol cached)
    {
        if (cached.DeclarationSpanStart != null && cached.DeclarationSpanLength != null)
            return new Text.TextSpan(cached.DeclarationSpanStart.Value, cached.DeclarationSpanLength.Value);
        return null;
    }

    private static TypeSymbol DeserializeTypeSymbol(
        CachedSymbol cached,
        Dictionary<string, Symbol> symbolRegistry,
        Func<string, SemanticType> typeResolver)
    {
        var typeKind = Enum.Parse<TypeKind>(cached.TypeKind ?? "Class");
        var accessLevel = Enum.Parse<AccessLevel>(cached.AccessLevel);

        // Deserialize fields
        var fields = cached.Fields?.Select(f => DeserializeVariableSymbol(f, typeResolver)).ToList()
            ?? new List<VariableSymbol>();

        // Deserialize methods
        var methods = cached.Methods?.Select(m => DeserializeFunctionSymbol(m, typeResolver)).ToList()
            ?? new List<FunctionSymbol>();

        // Deserialize constructors
        var constructors = cached.Constructors?.Select(c => DeserializeFunctionSymbol(c, typeResolver)).ToList()
            ?? new List<FunctionSymbol>();

        var symbol = new TypeSymbol
        {
            Name = cached.Name,
            Kind = SymbolKind.Type,
            AccessLevel = accessLevel,
            DeclarationLine = cached.DeclarationLine,
            DeclarationColumn = cached.DeclarationColumn,
            DeclarationSpan = DeserializeDeclarationSpan(cached),
            DeclaringFilePath = cached.FilePath,
            TypeKind = typeKind,
            IsAbstract = cached.IsAbstract,
            DefiningModule = cached.DefiningModule,
            DefiningFilePath = cached.FilePath,
            Fields = fields,
            Methods = methods,
            Constructors = constructors,
            IsReExport = cached.IsReExport,
            OriginalModule = cached.OriginalModule,
            CodeGenInfo = DeserializeCodeGenInfo(cached.CodeGenInfo)
        };

        // BaseType and Interfaces resolved in a second pass via symbolRegistry
        return symbol;
    }

    private static FunctionSymbol DeserializeFunctionSymbol(
        CachedSymbol cached,
        Func<string, SemanticType> typeResolver)
    {
        var accessLevel = Enum.Parse<AccessLevel>(cached.AccessLevel);
        var parameters = cached.Parameters?.Select(p => DeserializeParameter(p, typeResolver)).ToList()
            ?? new List<ParameterSymbol>();

        return new FunctionSymbol
        {
            Name = cached.Name,
            Kind = SymbolKind.Function,
            AccessLevel = accessLevel,
            DeclarationLine = cached.DeclarationLine,
            DeclarationColumn = cached.DeclarationColumn,
            DeclarationSpan = DeserializeDeclarationSpan(cached),
            DeclaringFilePath = cached.FilePath,
            Parameters = parameters,
            ReturnType = typeResolver(cached.ReturnTypeId ?? "void:"),
            IsStatic = cached.IsStatic,
            IsAbstract = cached.IsAbstract,
            IsVirtual = cached.IsVirtual,
            IsOverride = cached.IsOverride,
            IsGenerator = cached.IsGenerator,
            IsReExport = cached.IsReExport,
            OriginalModule = cached.OriginalModule,
            CodeGenInfo = DeserializeCodeGenInfo(cached.CodeGenInfo)
        };
    }

    private static VariableSymbol DeserializeVariableSymbol(
        CachedSymbol cached,
        Func<string, SemanticType> typeResolver)
    {
        var accessLevel = Enum.Parse<AccessLevel>(cached.AccessLevel);
        var props = cached.Properties ?? new Dictionary<string, object>();

        return new VariableSymbol
        {
            Name = cached.Name,
            Kind = SymbolKind.Variable,
            AccessLevel = accessLevel,
            DeclarationLine = cached.DeclarationLine,
            DeclarationColumn = cached.DeclarationColumn,
            DeclarationSpan = DeserializeDeclarationSpan(cached),
            DeclaringFilePath = cached.FilePath,
            Type = typeResolver(cached.TypeId ?? "unknown:"),
            IsParameter = GetBoolProperty(props, "IsParameter"),
            IsConstant = GetBoolProperty(props, "IsConstant"),
            HasDefaultValue = GetBoolProperty(props, "HasDefaultValue"),
            IsReExport = cached.IsReExport,
            OriginalModule = cached.OriginalModule,
            CodeGenInfo = DeserializeCodeGenInfo(cached.CodeGenInfo)
        };
    }

    /// <summary>
    /// Safely extracts a boolean property from the dictionary, handling JsonElement.
    /// </summary>
    private static bool GetBoolProperty(Dictionary<string, object> props, string key)
    {
        if (!props.TryGetValue(key, out var value))
            return false;

        // Handle JsonElement (from JSON deserialization)
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind == System.Text.Json.JsonValueKind.True;
        }

        // Handle direct boolean or other IConvertible
        return Convert.ToBoolean(value);
    }

    private static ModuleSymbol DeserializeModuleSymbol(
        CachedSymbol cached,
        Dictionary<string, Symbol> symbolRegistry)
    {
        var accessLevel = Enum.Parse<AccessLevel>(cached.AccessLevel);

        // Exports are resolved in a second pass
        return new ModuleSymbol
        {
            Name = cached.Name,
            Kind = SymbolKind.Module,
            AccessLevel = accessLevel,
            DeclarationLine = cached.DeclarationLine,
            DeclarationColumn = cached.DeclarationColumn,
            DeclarationSpan = DeserializeDeclarationSpan(cached),
            DeclaringFilePath = cached.FilePath,
            FilePath = cached.FilePath,
            IsReExport = cached.IsReExport,
            OriginalModule = cached.OriginalModule,
            CodeGenInfo = DeserializeCodeGenInfo(cached.CodeGenInfo)
        };
    }

    private static TypeAliasSymbol DeserializeTypeAliasSymbol(CachedSymbol cached)
    {
        var accessLevel = Enum.Parse<AccessLevel>(cached.AccessLevel);

        return new TypeAliasSymbol
        {
            Name = cached.Name,
            Kind = SymbolKind.TypeAlias,
            AccessLevel = accessLevel,
            DeclarationLine = cached.DeclarationLine,
            DeclarationColumn = cached.DeclarationColumn,
            DeclarationSpan = DeserializeDeclarationSpan(cached),
            DeclaringFilePath = cached.FilePath,
            IsReExport = cached.IsReExport,
            OriginalModule = cached.OriginalModule,
            CodeGenInfo = DeserializeCodeGenInfo(cached.CodeGenInfo)
        };
    }

    private static TypeParameterSymbol DeserializeTypeParameterSymbol(CachedSymbol cached)
    {
        var accessLevel = Enum.Parse<AccessLevel>(cached.AccessLevel);
        var variance = cached.Variance != null
            ? Enum.Parse<TypeParameterVariance>(cached.Variance)
            : TypeParameterVariance.None;

        return new TypeParameterSymbol
        {
            Name = cached.Name,
            Kind = SymbolKind.TypeParameter,
            AccessLevel = accessLevel,
            DeclarationLine = cached.DeclarationLine,
            DeclarationColumn = cached.DeclarationColumn,
            DeclarationSpan = DeserializeDeclarationSpan(cached),
            DeclaringFilePath = cached.FilePath,
            Variance = variance,
            IsReExport = cached.IsReExport,
            OriginalModule = cached.OriginalModule,
            CodeGenInfo = DeserializeCodeGenInfo(cached.CodeGenInfo)
        };
    }

    private static ParameterSymbol DeserializeParameter(
        CachedParameter cached,
        Func<string, SemanticType> typeResolver)
    {
        return new ParameterSymbol
        {
            Name = cached.Name,
            Type = typeResolver(cached.TypeId),
            HasDefault = cached.HasDefault,
            IsVariadic = cached.IsVariadic,
            IsPositionalOnly = cached.IsPositionalOnly,
            IsKeywordOnly = cached.IsKeywordOnly
        };
    }

    private static CodeGenInfo? DeserializeCodeGenInfo(CachedCodeGenInfo? cached)
    {
        if (cached == null)
            return null;

        return new CodeGenInfo
        {
            CSharpName = cached.CSharpName,
            OriginalName = cached.OriginalName,
            Version = cached.Version,
            IsModuleLevel = cached.IsModuleLevel,
            IsConstant = cached.IsConstant,
            HasExecutionOrderIssues = cached.HasExecutionOrderIssues,
            IsStringEnum = cached.IsStringEnum,
            ImportKind = Enum.Parse<ImportKind>(cached.ImportKind),
            OriginalImportName = cached.OriginalImportName
        };
    }

    /// <summary>
    /// Resolves a type ID back to a SemanticType.
    /// </summary>
    private static SemanticType ResolveTypeFromId(string typeId)
    {
        if (string.IsNullOrEmpty(typeId))
            return SemanticType.Unknown;

        var colonIndex = typeId.IndexOf(':');
        if (colonIndex < 0)
            return SemanticType.Unknown;

        var prefix = typeId[..colonIndex];
        var value = typeId[(colonIndex + 1)..];

        return prefix switch
        {
            "builtin" => ResolveBuiltinType(value),
            "void" => SemanticType.Void,
            "unknown" => SemanticType.Unknown,
            "user" => new UserDefinedType { Name = value },
            "nullable" => new NullableType { UnderlyingType = ResolveTypeFromId(value) },
            "optional" => new OptionalType { UnderlyingType = ResolveTypeFromId(value) },
            "module" => CreateModuleType(value),
            "typeparam" => new TypeParameterType { Name = value },
            "generic" => ResolveGenericType(value),
            "tuple" => ResolveTupleType(value),
            "func" => ResolveFunctionType(value),
            "result" => ResolveResultType(value),
            "gfunc" => ResolveGenericFunctionType(value),
            "union" => ResolveUnionType(value),
            "task" => ResolveTaskType(value),
            // Unknown prefix - return Unknown for graceful degradation with older cache formats
            _ => SemanticType.Unknown
        };
    }

    private static SemanticType ResolveBuiltinType(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "int" => BuiltinType.Int,
            "long" => BuiltinType.Long,
            "float" => BuiltinType.Float,
            "double" => BuiltinType.Double,
            "float32" => BuiltinType.Float32,
            "bool" => BuiltinType.Bool,
            "str" => BuiltinType.Str,
            _ => SemanticType.Unknown
        };
    }

    private static SemanticType CreateModuleType(string name)
    {
        // Create a placeholder ModuleSymbol for the ModuleType
        // The actual symbol will be resolved later from the symbol table
        var placeholderSymbol = new ModuleSymbol { Name = name, Kind = SymbolKind.Module };
        return new ModuleType { Symbol = placeholderSymbol };
    }

    private static SemanticType ResolveGenericType(string value)
    {
        // Format: Name[Type1,Type2,...]
        var bracketIndex = value.IndexOf('[');
        if (bracketIndex < 0)
            return new GenericType { Name = value, TypeArguments = new List<SemanticType>() };

        var name = value[..bracketIndex];
        var argsStr = value[(bracketIndex + 1)..^1]; // Remove [ and ]

        var typeArgs = ParseTypeArguments(argsStr);
        return new GenericType { Name = name, TypeArguments = typeArgs };
    }

    private static SemanticType ResolveTupleType(string value)
    {
        // Format: [Type1,Type2,...]
        var inner = value.TrimStart('[').TrimEnd(']');
        var elementTypes = ParseTypeArguments(inner);
        return new TupleType { ElementTypes = elementTypes };
    }

    private static SemanticType ResolveFunctionType(string value)
    {
        // Format: (ParamType1,ParamType2,...)->ReturnType
        var arrowIndex = value.IndexOf("->");
        if (arrowIndex < 0)
            return SemanticType.Unknown;

        var paramsStr = value[1..(arrowIndex - 1)]; // Remove ( and )
        var returnStr = value[(arrowIndex + 2)..];

        var paramTypes = string.IsNullOrEmpty(paramsStr)
            ? new List<SemanticType>()
            : ParseTypeArguments(paramsStr);

        var returnType = ResolveTypeFromId(returnStr);
        return new FunctionType { ParameterTypes = paramTypes, ReturnType = returnType };
    }

    private static SemanticType ResolveResultType(string value)
    {
        // Format: OkType!ErrorType
        var bangIndex = value.IndexOf('!');
        if (bangIndex < 0)
            return SemanticType.Unknown;

        var okType = ResolveTypeFromId(value[..bangIndex]);
        var errorType = ResolveTypeFromId(value[(bangIndex + 1)..]);
        return new ResultType { OkType = okType, ErrorType = errorType };
    }

    private static SemanticType ResolveGenericFunctionType(string value)
    {
        // Format: FunctionName[Type1,Type2,...]
        // Note: We can't fully restore GenericFunctionType without the FunctionSymbol,
        // which requires the full symbol table. Return a placeholder that stores the name.
        var bracketIndex = value.IndexOf('[');
        if (bracketIndex < 0)
        {
            // No type arguments, just the function name - return Unknown
            return SemanticType.Unknown;
        }

        var argsStr = value[(bracketIndex + 1)..^1]; // Remove [ and ]
        var typeArgs = ParseTypeArguments(argsStr);

        // Return as a generic type for now - full GenericFunctionType restoration
        // requires the FunctionSymbol which isn't available during deserialization
        return new GenericType { Name = value[..bracketIndex], TypeArguments = typeArgs };
    }

    private static SemanticType ResolveUnionType(string value)
    {
        // Format: UnionName[CaseType1,CaseType2,...]
        var bracketIndex = value.IndexOf('[');
        if (bracketIndex < 0)
            return new UnionType { Name = value, CaseTypes = new List<SemanticType>() };

        var name = value[..bracketIndex];
        var argsStr = value[(bracketIndex + 1)..^1]; // Remove [ and ]
        var caseTypes = ParseTypeArguments(argsStr);

        return new UnionType { Name = name, CaseTypes = caseTypes };
    }

    private static SemanticType ResolveTaskType(string value)
    {
        // Format: (empty for Task, or ResultType for Task<T>)
        if (string.IsNullOrEmpty(value))
            return new TaskType { ResultType = null };

        var resultType = ResolveTypeFromId(value);
        return new TaskType { ResultType = resultType };
    }

    /// <summary>
    /// Parses a comma-separated list of type IDs, handling nested brackets.
    /// </summary>
    private static List<SemanticType> ParseTypeArguments(string argsStr)
    {
        var types = new List<SemanticType>();
        if (string.IsNullOrEmpty(argsStr))
            return types;

        var depth = 0;
        var start = 0;

        for (var i = 0; i < argsStr.Length; i++)
        {
            var c = argsStr[i];
            if (c == '[' || c == '(')
                depth++;
            else if (c == ']' || c == ')')
                depth--;
            else if (c == ',' && depth == 0)
            {
                var typeId = argsStr[start..i].Trim();
                types.Add(ResolveTypeFromId(typeId));
                start = i + 1;
            }
        }

        // Add the last type
        if (start < argsStr.Length)
        {
            var typeId = argsStr[start..].Trim();
            types.Add(ResolveTypeFromId(typeId));
        }

        return types;
    }

    #endregion

    #region TypeAnnotation Serialization

    /// <summary>
    /// Serializes a TypeAnnotation AST node to a string representation.
    /// Format mirrors SerializeType: name, name[arg1,arg2], optional:name, nullable:name, name!error.
    /// Note: TupleElementNames is not round-tripped (extremely unlikely in interface type arguments).
    /// </summary>
    internal static string SerializeTypeAnnotation(TypeAnnotation ann)
    {
        if (ann.IsOptional)
            return "optional:" + SerializeTypeAnnotation(ann with { IsOptional = false });

        if (ann.IsCSharpNullable)
            return "nullable:" + SerializeTypeAnnotation(ann with { IsCSharpNullable = false });

        if (ann.ErrorType != null)
            return SerializeTypeAnnotation(ann with { ErrorType = null }) + "!" + SerializeTypeAnnotation(ann.ErrorType);

        if (!ann.TypeArguments.IsDefaultOrEmpty)
            return ann.Name + "[" + string.Join(",", ann.TypeArguments.Select(SerializeTypeAnnotation)) + "]";

        return ann.Name;
    }

    /// <summary>
    /// Deserializes a string back to a TypeAnnotation AST node.
    /// Source locations are set to 0 since cached symbols don't need location data.
    /// Note: TupleElementNames is not restored (see SerializeTypeAnnotation).
    /// </summary>
    internal static TypeAnnotation DeserializeTypeAnnotation(string s)
    {
        if (string.IsNullOrEmpty(s))
            return new TypeAnnotation { Name = "" };

        // Check for optional: prefix
        if (s.StartsWith("optional:"))
        {
            var inner = DeserializeTypeAnnotation(s["optional:".Length..]);
            return inner with { IsOptional = true };
        }

        // Check for nullable: prefix
        if (s.StartsWith("nullable:"))
        {
            var inner = DeserializeTypeAnnotation(s["nullable:".Length..]);
            return inner with { IsCSharpNullable = true };
        }

        // Check for result type (name!error) — find '!' at depth 0
        var bangIndex = FindCharAtDepthZero(s, '!');
        if (bangIndex > 0)
        {
            var okPart = DeserializeTypeAnnotation(s[..bangIndex]);
            var errorPart = DeserializeTypeAnnotation(s[(bangIndex + 1)..]);
            return okPart with { ErrorType = errorPart };
        }

        // Check for type arguments (name[arg1,arg2])
        var bracketIndex = s.IndexOf('[');
        if (bracketIndex > 0 && s[^1] == ']')
        {
            var name = s[..bracketIndex];
            var argsStr = s[(bracketIndex + 1)..^1];
            var typeArgs = ParseAnnotationArguments(argsStr);
            return new TypeAnnotation
            {
                Name = name,
                TypeArguments = typeArgs.ToImmutableArray()
            };
        }

        // Simple name
        return new TypeAnnotation { Name = s };
    }

    /// <summary>
    /// Finds the index of a character at bracket depth 0, or -1 if not found.
    /// </summary>
    private static int FindCharAtDepthZero(string s, char target)
    {
        var depth = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '[')
                depth++;
            else if (c == ']')
                depth--;
            else if (c == target && depth == 0)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Parses a comma-separated list of serialized TypeAnnotation strings, handling nested brackets.
    /// </summary>
    private static List<TypeAnnotation> ParseAnnotationArguments(string argsStr)
    {
        var annotations = new List<TypeAnnotation>();
        if (string.IsNullOrEmpty(argsStr))
            return annotations;

        var depth = 0;
        var start = 0;

        for (var i = 0; i < argsStr.Length; i++)
        {
            var c = argsStr[i];
            if (c == '[')
                depth++;
            else if (c == ']')
                depth--;
            else if (c == ',' && depth == 0)
            {
                var part = argsStr[start..i].Trim();
                annotations.Add(DeserializeTypeAnnotation(part));
                start = i + 1;
            }
        }

        // Add the last annotation
        if (start < argsStr.Length)
        {
            var part = argsStr[start..].Trim();
            annotations.Add(DeserializeTypeAnnotation(part));
        }

        return annotations;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Resolves cross-references for symbols that were deserialized.
    /// Call this after all symbols have been deserialized into the registry.
    /// </summary>
    public static void ResolveReferences(
        IEnumerable<CachedSymbol> cachedSymbols,
        Dictionary<string, Symbol> symbolRegistry)
    {
        foreach (var cached in cachedSymbols)
        {
            if (!symbolRegistry.TryGetValue(cached.Id, out var symbol))
                continue;

            // Resolve TypeSymbol references
            if (symbol is TypeSymbol ts)
            {
                // Resolve BaseType
                if (cached.BaseTypeId != null && symbolRegistry.TryGetValue(cached.BaseTypeId, out var baseSymbol))
                {
                    if (baseSymbol is TypeSymbol baseType)
                    {
                        ts.BaseType = baseType;
                    }
                }

                // Resolve Interfaces
                if (cached.InterfaceEntries != null)
                {
                    foreach (var entry in cached.InterfaceEntries)
                    {
                        if (symbolRegistry.TryGetValue(entry.SymbolId, out var ifaceSymbol) && ifaceSymbol is TypeSymbol ifaceType)
                        {
                            var typeArgs = entry.TypeArgs != null && entry.TypeArgs.Count > 0
                                ? entry.TypeArgs.Select(DeserializeTypeAnnotation).ToImmutableArray()
                                : ImmutableArray<TypeAnnotation>.Empty;
                            ts.Interfaces.Add(new InterfaceReference
                            {
                                Definition = ifaceType,
                                TypeArgAnnotations = typeArgs
                            });
                        }
                    }
                }
            }

            // Resolve ModuleSymbol exports
            if (symbol is ModuleSymbol ms && cached.ExportIds != null)
            {
                foreach (var (name, exportId) in cached.ExportIds)
                {
                    if (symbolRegistry.TryGetValue(exportId, out var exportSymbol))
                    {
                        ms.Exports[name] = exportSymbol;
                    }
                }
            }
        }
    }

    #endregion
}
