using System.Globalization;
using System.Reflection;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery.Caching;

/// <summary>
/// Builds an OverloadIndex from assembly reflection.
/// </summary>
internal class OverloadIndexBuilder
{
    private readonly ClrTypeMapper _typeMapper = new();
    private readonly ICompilerLogger _logger;
    private XmlDocReader? _xmlDocReader;

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
        // Try to load XML documentation file alongside the assembly
        var assemblyLocation = assembly.Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            var xmlPath = Path.ChangeExtension(assemblyLocation, ".xml");
            _xmlDocReader = XmlDocReader.TryCreate(xmlPath);
        }

        var identity = AssemblyIdentity.FromAssembly(assembly);
        var index = new OverloadIndex
        {
            Identity = identity,
            CreatedAt = DateTime.UtcNow,
            CacheFormatVersion = OverloadIndexCache.CurrentCacheFormatVersion
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
            moduleOverloads.CSharpNamespace = exportType.Namespace;
            moduleOverloads.CSharpClassName = exportType.Name;

            // Look up XML documentation for this module class
            if (_xmlDocReader != null)
            {
                var fullName = (exportType.FullName ?? exportType.Name).Replace('+', '.');
                var doc = _xmlDocReader.GetMemberDoc("T:" + fullName);
                moduleOverloads.Documentation = doc?.Summary;
            }

            // Register the module if it has functions, static fields, or static properties
            // (e.g., string module has only const fields).
            if (moduleOverloads.Functions.Count > 0 || moduleOverloads.Fields.Count > 0)
            {
                index.Modules[moduleName] = moduleOverloads;
            }
        }

        // Discover public types (classes, structs, enums) from the assembly
        DiscoverPublicTypes(assembly, index);

        // Discover nested types inside module classes (spy-sourced modules define
        // types as nested classes inside the static module class)
        DiscoverNestedModuleTypes(assembly, index);

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
            var moduleName = DeriveModuleNameForType(type);

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
            var moduleTypeAttr = type.CustomAttributes.FirstOrDefault(
                a => a.AttributeType.FullName == "Sharpy.SharpyModuleTypeAttribute");
            var isModuleType = moduleTypeAttr != null;
            var pythonName = GetPythonNameFromModuleTypeAttr(moduleTypeAttr);

            // Look up XML documentation for this type
            string? typeDoc = null;
            if (_xmlDocReader != null)
            {
                var typeMemberId = "T:" + (type.FullName ?? type.Name).Replace('+', '.');
                var doc = _xmlDocReader.GetMemberDoc(typeMemberId);
                typeDoc = doc?.Summary;
            }

            var typeInfo = new DiscoveredTypeInfo
            {
                Name = pythonName ?? type.Name,
                Namespace = type.Namespace ?? string.Empty,
                ClrTypeName = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
                IsException = isException,
                IsModuleType = isModuleType,
                BaseTypeName = type.BaseType?.Name,
                TypeKind = typeKind,
                Documentation = typeDoc
            };

            DiscoverTypeMethods(type, typeInfo);
            DiscoverTypeOperators(type, typeInfo);
            DiscoverTypeProtocols(type, typeInfo);
            DiscoverTypeProperties(type, typeInfo);

            moduleOverloads.Types.Add(typeInfo);
        }
    }

    private void DiscoverNestedModuleTypes(Assembly assembly, OverloadIndex index)
    {
        var moduleClasses = assembly.GetTypes()
            .Where(t => t.IsClass && t.CustomAttributes.Any(
                a => a.AttributeType.FullName == "Sharpy.SharpyModuleAttribute"))
            .ToList();

        foreach (var moduleClass in moduleClasses)
        {
            var moduleName = DeriveModuleName(moduleClass);
            var nestedTypes = moduleClass.GetNestedTypes(BindingFlags.Public);

            if (nestedTypes.Length == 0)
                continue;

            if (!index.Modules.TryGetValue(moduleName, out var moduleOverloads))
            {
                moduleOverloads = new ModuleOverloads { ModuleName = moduleName };
                index.Modules[moduleName] = moduleOverloads;
            }

            foreach (var nestedType in nestedTypes)
            {
                if (nestedType.Name.StartsWith("<"))
                    continue;

                var typeKind = "Class";
                if (nestedType.IsEnum)
                    typeKind = "Enum";
                else if (nestedType.IsValueType)
                    typeKind = "Struct";
                else if (nestedType.IsInterface)
                    typeKind = "Interface";

                if (nestedType.IsAbstract && nestedType.IsSealed && !nestedType.IsInterface)
                    continue;

                var isException = typeof(Exception).IsAssignableFrom(nestedType);

                string? typeDoc = null;
                if (_xmlDocReader != null)
                {
                    var typeMemberId = "T:" + (nestedType.FullName ?? nestedType.Name).Replace('+', '.');
                    var doc = _xmlDocReader.GetMemberDoc(typeMemberId);
                    typeDoc = doc?.Summary;
                }

                var typeInfo = new DiscoveredTypeInfo
                {
                    Name = nestedType.Name,
                    Namespace = moduleClass.Namespace ?? string.Empty,
                    ClrTypeName = nestedType.AssemblyQualifiedName ?? nestedType.FullName ?? nestedType.Name,
                    IsException = isException,
                    IsModuleType = true,
                    BaseTypeName = nestedType.BaseType?.Name,
                    TypeKind = typeKind,
                    Documentation = typeDoc
                };

                DiscoverTypeMethods(nestedType, typeInfo);
                DiscoverTypeOperators(nestedType, typeInfo);
                DiscoverTypeProtocols(nestedType, typeInfo);
                DiscoverTypeProperties(nestedType, typeInfo);

                moduleOverloads.Types.Add(typeInfo);
            }
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
        ["op_Division"] = "__div__",
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

        // Collect names of methods declared directly on this type (including overrides and
        // 'new' methods). Inherited methods with the same mangled name are excluded so that
        // e.g. TextWriter.Write(char) doesn't shadow StringIO.Write(string) after mangling.
        var declaredNames = new HashSet<string>(
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Select(m => GetFunctionName(m)));

        var methodGroups = methods.GroupBy(m => GetFunctionName(m));

        foreach (var group in methodGroups)
        {
            // When the type declares its own methods with this name, skip inherited ones
            var relevantMethods = declaredNames.Contains(group.Key)
                ? group.Where(m => m.DeclaringType == type)
                : group;

            foreach (var method in relevantMethods)
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
        var containsMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Any(m => m.Name == "Contains");
        if (containsMethods)
            AddProtocolStub(typeInfo, "__contains__");
    }

    /// <summary>
    /// Discovers public instance properties on a type, excluding indexers.
    /// </summary>
    private void DiscoverTypeProperties(Type type, DiscoveredTypeInfo typeInfo)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .ToList();

        foreach (var property in properties)
        {
            try
            {
                // Look up XML documentation for this property
                string? propDoc = null;
                if (_xmlDocReader != null && property.DeclaringType != null)
                {
                    var propMemberId = "P:" + (property.DeclaringType.FullName ?? property.DeclaringType.Name).Replace('+', '.') + "." + property.Name;
                    var doc = _xmlDocReader.GetMemberDoc(propMemberId);
                    propDoc = doc?.Summary;
                }

                var propertyInfo = new DiscoveredPropertyInfo
                {
                    Name = property.Name,
                    PropertyType = CreateTypeSignature(property.PropertyType),
                    HasGetter = property.GetGetMethod() != null,
                    HasSetter = property.GetSetMethod() != null,
                    Documentation = propDoc
                };

                typeInfo.Properties.Add(propertyInfo);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                _logger.LogDebug($"Skipping property {type.Name}.{property.Name}: {ex.Message}");
            }
        }
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

    private string DeriveModuleNameForType(Type type)
    {
        // Check for [SharpyModuleType("name")] attribute on the type itself
        var attr = type.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "Sharpy.SharpyModuleTypeAttribute");
        if (attr != null && attr.ConstructorArguments.Count > 0)
            return (string)attr.ConstructorArguments[0].Value!;

        // Fallback to namespace-based derivation
        return DeriveModuleNameFromNamespace(type.Namespace);
    }

    /// <summary>
    /// Reads the optional <c>pythonName</c> argument from a SharpyModuleType attribute.
    /// Returns <c>null</c> if the attribute is absent or used with the single-argument constructor.
    /// </summary>
    private static string? GetPythonNameFromModuleTypeAttr(CustomAttributeData? attr)
    {
        if (attr == null || attr.ConstructorArguments.Count < 2)
            return null;
        return attr.ConstructorArguments[1].Value as string;
    }

    private string DeriveModuleNameFromNamespace(string? ns)
    {
        if (ns == null)
            return "builtins";
        if (ns == "Sharpy" || ns.StartsWith("Sharpy."))
            return "builtins";
        return ns.ToLowerInvariant().Replace(".", "_", StringComparison.Ordinal);
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

        // Discover public static fields (e.g., string module constants)
        var fields = exportType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => !f.Name.StartsWith("<", StringComparison.Ordinal))
            .ToList();

        foreach (var field in fields)
        {
            try
            {
                var fieldSignature = new FieldSignature
                {
                    Name = field.Name,
                    FieldType = CreateTypeSignature(field.FieldType),
                    IsConst = field.IsLiteral
                };

                moduleOverloads.Fields[field.Name] = fieldSignature;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                _logger.LogDebug($"Skipping field {exportType.Name}.{field.Name}: {ex.Message}");
            }
        }

        // Discover public static properties with a getter (e.g., sys.Stdout => Console.Out).
        // Treated the same as static fields for consumers: they are accessed with
        // identical C# syntax, so they feed into moduleOverloads.Fields.
        var staticProperties = exportType.GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => p.GetGetMethod() != null)
            .ToList();

        foreach (var property in staticProperties)
        {
            if (moduleOverloads.Fields.ContainsKey(property.Name))
                continue;

            try
            {
                var propertyFieldSignature = new FieldSignature
                {
                    Name = property.Name,
                    FieldType = CreateTypeSignature(property.PropertyType),
                    IsConst = false
                };

                moduleOverloads.Fields[property.Name] = propertyFieldSignature;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                _logger.LogDebug($"Skipping static property {exportType.Name}.{property.Name}: {ex.Message}");
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
            ClrName = method.Name,
            ReturnType = CreateTypeSignature(method.ReturnType),
            MethodToken = CreateMethodToken(method),
            IsVirtual = method.IsVirtual && !method.IsFinal,
            IsAbstract = method.IsAbstract,
        };

        // Extract generic type parameters (e.g., T from Min<T>)
        if (method.IsGenericMethodDefinition)
        {
            signature.TypeParameters = method.GetGenericArguments()
                .Select(t => t.Name)
                .ToList();
        }

        // Look up XML documentation for this method
        XmlMemberDoc? methodDoc = null;
        if (_xmlDocReader != null)
        {
            var memberId = BuildMethodMemberId(method);
            methodDoc = _xmlDocReader.GetMemberDoc(memberId);
            signature.Documentation = methodDoc?.Summary;
        }

        foreach (var param in method.GetParameters())
        {
            signature.Parameters.Add(CreateParameterSignature(param, methodDoc));
        }

        return signature;
    }

    private ParameterSignature CreateParameterSignature(ParameterInfo param, XmlMemberDoc? methodDoc = null)
    {
        string? paramDoc = null;
        if (methodDoc != null && param.Name != null)
        {
            methodDoc.Parameters.TryGetValue(param.Name, out paramDoc);
        }

        return new ParameterSignature
        {
            Name = param.Name ?? "arg",
            Type = CreateTypeSignature(param.ParameterType),
            HasDefault = param.HasDefaultValue,
            DefaultValue = param.HasDefaultValue ? ConvertDefaultValue(param.DefaultValue) : null,
            IsVariadic = param.GetCustomAttribute<ParamArrayAttribute>() != null,
            Documentation = paramDoc
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
                IsMethodLevelTypeParam = clrType.DeclaringMethod != null,
                ClrTypeName = string.Empty
            };
        }

        var semanticType = _typeMapper.MapClrTypeToSemanticType(clrType);

        var signature = new TypeSignature
        {
            Name = semanticType.GetDisplayName(),
            ClrTypeName = clrType.IsGenericType
                ? (clrType.GetGenericTypeDefinition().AssemblyQualifiedName
                    ?? clrType.GetGenericTypeDefinition().FullName ?? clrType.Name)
                : (clrType.AssemblyQualifiedName ?? clrType.FullName ?? clrType.Name)
        };

        // For generic CLR types, recurse via CreateTypeSignature using CLR generic arguments
        // to preserve GenericParameterPosition on nested type parameters.
        if (clrType.IsGenericType)
        {
            var clrTypeArgs = clrType.GetGenericArguments();
            var genericDefName = clrType.GetGenericTypeDefinition().FullName;

            // Func<T1,...,TResult> / Action<T1,...> -> encode as __func__/__action__
            // so ConvertTypeSignature reconstructs as FunctionType. This enables
            // generic inference for all callback parameters (e.g., filter<T>, sorted key=,
            // and Result<T,E>.Map<U>).
            if (semanticType is FunctionType)
            {
                signature.Name = genericDefName?.StartsWith("System.Action`") == true
                    ? TypeSignature.ActionSentinel : TypeSignature.FuncSentinel;
                signature.IsGeneric = true;
                signature.TypeArguments = clrTypeArgs
                    .Select(CreateTypeSignature)
                    .ToList();
            }
            else if (semanticType is OptionalType)
            {
                // Emit Optional as a generic TypeSignature so ConvertTypeSignature
                // can reconstruct it as OptionalType via its "Optional" handling.
                signature.Name = "Optional";
                signature.IsGeneric = true;
                signature.TypeArguments = clrTypeArgs
                    .Select(CreateTypeSignature)
                    .ToList();
            }
            else if (semanticType is NullableType)
            {
                // Emit value-type Nullable<T> (C# T?) under a sentinel so ConvertTypeSignature
                // can reconstruct it as NullableType. Without this, Nullable<Bytes> fell through
                // to the non-generic ClrTypeName branch and resolved to the OPEN Nullable<>
                // type, breaking overload matching for e.g. hmac.new(bytes, bytes?, str) (#890).
                signature.Name = TypeSignature.NullableSentinel;
                signature.IsGeneric = true;
                signature.TypeArguments = clrTypeArgs
                    .Select(CreateTypeSignature)
                    .ToList();
            }
            else if (semanticType is ResultType)
            {
                // Emit Result as a generic TypeSignature so ConvertTypeSignature
                // can reconstruct it as ResultType via its "Result" handling.
                signature.Name = "Result";
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
            else if (semanticType is BuiltinType && ClrTypeHelper.GetIteratorElementType(clrType) != null)
            {
                // Generic Iterator<T> subtypes (e.g., CombinationsIterator<T> : Iterator<T[]>)
                // Emit as Iterator[elementType] so the round-trip preserves iterability.
                var elementClrType = ClrTypeHelper.GetIteratorElementType(clrType)!;
                signature.Name = BuiltinNames.Iterator;
                signature.IsGeneric = true;
                signature.TypeArguments = new List<TypeSignature> { CreateTypeSignature(elementClrType) };
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

    /// <summary>
    /// Builds an XML documentation member ID for a method.
    /// Format: M:Namespace.Type.Method(ParamType1,ParamType2)
    /// </summary>
    private static string BuildMethodMemberId(MethodInfo method)
    {
        var declaringType = method.DeclaringType;
        var typeName = (declaringType?.FullName ?? declaringType?.Name ?? "Unknown").Replace('+', '.');

        var methodName = method.Name;

        var parameters = method.GetParameters();
        if (parameters.Length == 0)
            return $"M:{typeName}.{methodName}";

        var paramTypes = string.Join(",", parameters.Select(p => GetXmlDocTypeName(p.ParameterType)));
        return $"M:{typeName}.{methodName}({paramTypes})";
    }

    /// <summary>
    /// Converts a CLR type to its XML documentation ID format.
    /// Handles arrays, generics, by-ref types, and nested types.
    /// </summary>
    private static string GetXmlDocTypeName(Type type)
    {
        if (type.IsByRef)
            return GetXmlDocTypeName(type.GetElementType()!) + "@";

        if (type.IsArray)
        {
            var rank = type.GetArrayRank();
            var elementName = GetXmlDocTypeName(type.GetElementType()!);
            return rank == 1
                ? elementName + "[]"
                : elementName + "[" + new string(',', rank - 1) + "]";
        }

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var baseName = (genericDef.FullName ?? genericDef.Name).Replace('+', '.');
            baseName = ClrNameHelper.StripArity(baseName);

            var typeArgs = string.Join(",", type.GetGenericArguments().Select(GetXmlDocTypeName));
            return $"{baseName}{{{typeArgs}}}";
        }

        if (type.IsGenericParameter)
        {
            // Method-level type parameters use ``N, type-level use `N
            return type.DeclaringMethod != null
                ? $"``{type.GenericParameterPosition}"
                : $"`{type.GenericParameterPosition}";
        }

        return (type.FullName ?? type.Name).Replace('+', '.');
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
            float f => f.ToString("G9", CultureInfo.InvariantCulture),
            double d => d.ToString("G17", CultureInfo.InvariantCulture),
            _ => null
        };
    }
}
