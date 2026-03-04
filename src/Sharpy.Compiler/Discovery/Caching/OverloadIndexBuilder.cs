using System.Reflection;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Discovery.Caching;

/// <summary>
/// Builds an OverloadIndex from assembly reflection.
/// </summary>
internal class OverloadIndexBuilder
{
    private readonly ClrTypeMapper _typeMapper = new();
    private readonly ICompilerLogger _logger;

    public OverloadIndexBuilder(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Build an index from an assembly by discovering all public static methods
    /// in [SharpyModule]-decorated classes.
    /// </summary>
    public OverloadIndex BuildFromAssembly(Assembly assembly)
    {
        var identity = AssemblyIdentity.FromAssembly(assembly);
        var index = new OverloadIndex
        {
            Identity = identity,
            CreatedAt = DateTime.UtcNow,
            CacheFormatVersion = 6
        };

        // Find all module classes decorated with [SharpyModule]
        var exportTypes = assembly.GetTypes()
            .Where(t => t.IsClass && t.CustomAttributes.Any(
                a => a.AttributeType.FullName == "Sharpy.SharpyModuleAttribute"))
            .ToList();

        foreach (var exportType in exportTypes)
        {
            var moduleName = DeriveModuleName(exportType);
            var moduleOverloads = DiscoverModuleFunctions(exportType);

            if (moduleOverloads.Functions.Count > 0)
            {
                index.Modules[moduleName] = moduleOverloads;
            }
        }

        // Discover public types (classes, structs, enums) from the assembly
        DiscoverPublicTypes(assembly, index);

        return index;
    }

    private void DiscoverPublicTypes(Assembly assembly, OverloadIndex index)
    {
        var allTypes = assembly.GetTypes()
            .Where(t => t.IsPublic)
            .Where(t => !t.Name.StartsWith("<"))  // Exclude compiler-generated types
            .Where(t => !t.CustomAttributes.Any(
                a => a.AttributeType.FullName == "Sharpy.SharpyModuleAttribute"))  // Exclude module classes
            .ToList();

        foreach (var type in allTypes)
        {
            var moduleName = DeriveModuleNameFromNamespace(type.Namespace);

            if (!index.Modules.TryGetValue(moduleName, out var moduleOverloads))
            {
                moduleOverloads = new ModuleOverloads { ModuleName = moduleName };
                index.Modules[moduleName] = moduleOverloads;
            }

            var typeKind = "Class";
            if (type.IsEnum)
                typeKind = "Enum";
            else if (type.IsValueType)
                typeKind = "Struct";
            else if (type.IsInterface)
                typeKind = "Interface";

            // Skip static classes and [SharpyModule]-decorated classes (they are module containers, not constructible types)
            if ((type.IsAbstract && type.IsSealed && !type.IsInterface) ||
                type.CustomAttributes.Any(a => a.AttributeType.FullName == "Sharpy.SharpyModuleAttribute"))
                continue;

            var isException = typeof(Exception).IsAssignableFrom(type);

            var typeInfo = new DiscoveredTypeInfo
            {
                Name = type.Name,
                Namespace = type.Namespace ?? string.Empty,
                ClrTypeName = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
                IsException = isException,
                BaseTypeName = type.BaseType?.Name,
                TypeKind = typeKind
            };

            DiscoverTypeMethods(type, typeInfo);
            DiscoverTypeOperators(type, typeInfo);
            DiscoverTypeProtocols(type, typeInfo);

            moduleOverloads.Types.Add(typeInfo);
        }
    }

    /// <summary>
    /// Inherited Object methods to exclude from instance method discovery.
    /// </summary>
    private static readonly HashSet<string> ExcludedObjectMethods = new()
    {
        "GetHashCode", "Equals", "ToString", "GetType"
    };

    /// <summary>
    /// Maps CLR operator method names to Python dunder names.
    /// </summary>
    private static readonly Dictionary<string, string> ClrOperatorToDunder = new()
    {
        ["op_Addition"] = "__add__",
        ["op_Subtraction"] = "__sub__",
        ["op_Multiply"] = "__mul__",
        ["op_Division"] = "__truediv__",
        ["op_Modulus"] = "__mod__",
        ["op_Equality"] = "__eq__",
        ["op_Inequality"] = "__ne__",
        ["op_LessThan"] = "__lt__",
        ["op_GreaterThan"] = "__gt__",
        ["op_LessThanOrEqual"] = "__le__",
        ["op_GreaterThanOrEqual"] = "__ge__",
        ["op_BitwiseAnd"] = "__and__",
        ["op_BitwiseOr"] = "__or__",
        ["op_ExclusiveOr"] = "__xor__",
    };

    /// <summary>
    /// Discovers public instance methods on a type and stores them as FunctionSignatures.
    /// Filters out property accessors, operator methods, and inherited Object methods.
    /// </summary>
    private void DiscoverTypeMethods(Type type, DiscoveredTypeInfo typeInfo)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
            .Where(m => !ExcludedObjectMethods.Contains(m.Name))
            .ToList();

        var methodGroups = methods.GroupBy(m => GetFunctionName(m));

        foreach (var group in methodGroups)
        {
            foreach (var method in group)
            {
                try
                {
                    var signature = CreateFunctionSignature(method);
                    typeInfo.Methods.Add(signature);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
                {
                    _logger.LogDebug($"Skipping {type.Name}.{method.Name}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Discovers CLR operators on a type and maps them to dunder names.
    /// </summary>
    private void DiscoverTypeOperators(Type type, DiscoveredTypeInfo typeInfo)
    {
        var operators = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.IsSpecialName && m.Name.StartsWith("op_"))
            .ToList();

        foreach (var op in operators)
        {
            if (!ClrOperatorToDunder.TryGetValue(op.Name, out var dunderName))
                continue;

            try
            {
                var signature = CreateFunctionSignature(op);
                signature.Name = dunderName;

                if (!typeInfo.OperatorMethods.TryGetValue(dunderName, out var signatures))
                {
                    signatures = new List<FunctionSignature>();
                    typeInfo.OperatorMethods[dunderName] = signatures;
                }
                signatures.Add(signature);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                _logger.LogDebug($"Skipping operator {type.Name}.{op.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Discovers protocol implementations on a type by checking interface implementations
    /// and specific patterns (indexers, Contains method, etc.).
    /// </summary>
    private void DiscoverTypeProtocols(Type type, DiscoveredTypeInfo typeInfo)
    {
        var interfaces = type.GetInterfaces();

        // ISized -> __len__
        if (interfaces.Any(i => i.FullName == "Sharpy.ISized"))
            AddProtocolStub(typeInfo, "__len__");

        // IBoolConvertible -> __bool__
        if (interfaces.Any(i => i.FullName == "Sharpy.IBoolConvertible"))
            AddProtocolStub(typeInfo, "__bool__");

        // IReverseEnumerable<T> -> __reversed__
        if (interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == "Sharpy.IReverseEnumerable`1"))
            AddProtocolStub(typeInfo, "__reversed__");

        // IEnumerable<T> -> __iter__
        if (interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            AddProtocolStub(typeInfo, "__iter__");

        // Indexer property (parameterized this[]) -> __getitem__ / __setitem__
        var indexers = type.GetProperties()
            .Where(p => p.GetIndexParameters().Length > 0)
            .ToList();

        if (indexers.Any(p => p.GetGetMethod() != null))
            AddProtocolStub(typeInfo, "__getitem__");

        if (indexers.Any(p => p.GetSetMethod() != null))
            AddProtocolStub(typeInfo, "__setitem__");

        // Contains method -> __contains__
        var containsMethod = type.GetMethod("Contains", BindingFlags.Public | BindingFlags.Instance);
        if (containsMethod != null)
            AddProtocolStub(typeInfo, "__contains__");
    }

    private static void AddProtocolStub(DiscoveredTypeInfo typeInfo, string dunderName)
    {
        if (!typeInfo.ProtocolMethods.ContainsKey(dunderName))
        {
            typeInfo.ProtocolMethods[dunderName] = new List<FunctionSignature>
            {
                new FunctionSignature { Name = dunderName }
            };
        }
    }

    private string DeriveModuleNameFromNamespace(string? ns)
    {
        if (ns == null)
            return "builtins";
        if (ns == "Sharpy" || ns.StartsWith("Sharpy."))
            return "builtins";
        return ns.ToLowerInvariant().Replace(".", "_");
    }

    private string DeriveModuleName(Type exportType)
    {
        // Read module name from [SharpyModule("name")] attribute
        var attr = exportType.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "Sharpy.SharpyModuleAttribute");
        if (attr != null && attr.ConstructorArguments.Count > 0)
            return (string)attr.ConstructorArguments[0].Value!;

        // Fallback to namespace-based derivation
        return DeriveModuleNameFromNamespace(exportType.Namespace);
    }

    private ModuleOverloads DiscoverModuleFunctions(Type exportType)
    {
        var moduleOverloads = new ModuleOverloads
        {
            ModuleName = DeriveModuleName(exportType)
        };

        // Get all public static methods
        var methods = exportType.GetMethods(BindingFlags.Public | BindingFlags.Static);

        // Filter out methods we don't want to expose
        var eligibleMethods = methods
            .Where(m => !m.Name.StartsWith("_"))
            .Where(m => !m.Name.StartsWith("get_"))
            .Where(m => !m.Name.StartsWith("set_"))
            .Where(m => !m.IsSpecialName)
            // Note: Type constructors (Int, Bool, etc.) are included as they are
            // valid builtin functions that can be called for type conversion.
            .ToList();

        // Group by function name
        var methodGroups = eligibleMethods.GroupBy(m => GetFunctionName(m));

        foreach (var group in methodGroups)
        {
            var functionName = group.Key;
            var signatures = new List<FunctionSignature>();

            foreach (var method in group)
            {
                try
                {
                    var signature = CreateFunctionSignature(method);
                    signatures.Add(signature);
                }
                catch (ArgumentException ex)
                {
                    // Skip methods that can't be mapped
                    _logger.LogDebug($"Skipping {exportType.Name}.{method.Name}: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    // Skip methods that can't be mapped
                    _logger.LogDebug($"Skipping {exportType.Name}.{method.Name}: {ex.Message}");
                }
                catch (NotSupportedException ex)
                {
                    // Skip methods that can't be mapped
                    _logger.LogDebug($"Skipping {exportType.Name}.{method.Name}: {ex.Message}");
                }
            }

            if (signatures.Count > 0)
            {
                moduleOverloads.Functions[functionName] = signatures;
            }
        }

        return moduleOverloads;
    }

    private string GetFunctionName(MethodInfo method)
    {
        return ReverseNameMangler.ToSharpyName(method.Name, ReverseNameContext.Method);
    }

    private FunctionSignature CreateFunctionSignature(MethodInfo method)
    {
        var signature = new FunctionSignature
        {
            Name = GetFunctionName(method),
            ReturnType = CreateTypeSignature(method.ReturnType),
            MethodToken = CreateMethodToken(method)
        };

        // Extract generic type parameters (e.g., T from Min<T>)
        if (method.IsGenericMethodDefinition)
        {
            signature.TypeParameters = method.GetGenericArguments()
                .Select(t => t.Name)
                .ToList();
        }

        foreach (var param in method.GetParameters())
        {
            signature.Parameters.Add(CreateParameterSignature(param));
        }

        return signature;
    }

    private ParameterSignature CreateParameterSignature(ParameterInfo param)
    {
        return new ParameterSignature
        {
            Name = param.Name ?? "arg",
            Type = CreateTypeSignature(param.ParameterType),
            HasDefault = param.HasDefaultValue,
            DefaultValue = param.HasDefaultValue ? ConvertDefaultValue(param.DefaultValue) : null,
            IsVariadic = param.GetCustomAttribute<ParamArrayAttribute>() != null
        };
    }

    private TypeSignature CreateTypeSignature(Type clrType)
    {
        // Handle generic type parameters (e.g., T in Min<T>(T[] items))
        if (clrType.IsGenericParameter)
        {
            return new TypeSignature
            {
                Name = clrType.Name,
                IsGenericParameter = true,
                GenericParameterPosition = clrType.GenericParameterPosition,
                ClrTypeName = string.Empty
            };
        }

        var semanticType = _typeMapper.MapClrTypeToSemanticType(clrType);

        var signature = new TypeSignature
        {
            Name = semanticType.GetDisplayName(),
            ClrTypeName = clrType.AssemblyQualifiedName ?? clrType.FullName ?? clrType.Name
        };

        // For generic CLR types, recurse via CreateTypeSignature using CLR generic arguments
        // to preserve GenericParameterPosition on nested type parameters.
        if (clrType.IsGenericType)
        {
            var clrTypeArgs = clrType.GetGenericArguments();

            if (semanticType is OptionalType)
            {
                // Emit Optional as a generic TypeSignature so ConvertTypeSignature
                // can reconstruct it as OptionalType via its "Optional" handling.
                signature.Name = "Optional";
                signature.IsGeneric = true;
                signature.TypeArguments = clrTypeArgs
                    .Select(CreateTypeSignature)
                    .ToList();
            }
            else if (semanticType is TupleType)
            {
                signature.Name = "tuple";
                signature.IsGeneric = true;
                signature.TypeArguments = clrTypeArgs
                    .Select(CreateTypeSignature)
                    .ToList();
            }
            else if (semanticType is GenericType)
            {
                signature.IsGeneric = true;
                signature.TypeArguments = clrTypeArgs
                    .Select(CreateTypeSignature)
                    .ToList();
            }
        }
        else if (semanticType is GenericType genericType)
        {
            // Non-generic CLR types mapped to GenericType (e.g., arrays mapped to list[T]).
            // Use the semantic type arguments since CLR generic args aren't available.
            signature.IsGeneric = true;
            signature.TypeArguments = genericType.TypeArguments
                .Select(CreateTypeSignatureFromSemantic)
                .ToList();
        }

        return signature;
    }

    /// <summary>
    /// Creates a TypeSignature from a SemanticType. Used for type arguments of generic,
    /// optional, and tuple types where we already have a mapped SemanticType.
    /// </summary>
    private TypeSignature CreateTypeSignatureFromSemantic(SemanticType semanticType)
    {
        if (semanticType is TypeParameterType typeParam)
        {
            return new TypeSignature
            {
                Name = typeParam.Name,
                IsGenericParameter = true,
                ClrTypeName = string.Empty
            };
        }

        return new TypeSignature
        {
            Name = semanticType.GetDisplayName(),
            ClrTypeName = string.Empty
        };
    }

    private string CreateMethodToken(MethodInfo method)
    {
        var assemblyName = method.DeclaringType?.Assembly.GetName().Name ?? "Unknown";
        var typeName = method.DeclaringType?.FullName ?? "Unknown";
        var methodName = method.Name;
        var paramCount = method.GetParameters().Length;

        return $"{assemblyName}|{typeName}|{methodName}|{paramCount}";
    }

    private string? ConvertDefaultValue(object? value)
    {
        if (value == null || value == DBNull.Value)
            return null;

        // Convert to string representation
        return value switch
        {
            string s => $"\"{s}\"",
            char c => $"'{c}'",
            bool b => b.ToString().ToLowerInvariant(),
            int or long or short or byte or sbyte or uint or ulong or ushort => value.ToString(),
            float f => f.ToString("G9"),
            double d => d.ToString("G17"),
            _ => null
        };
    }
}
