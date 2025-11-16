# Reflection-Based Function Discovery System

## Overview

This document outlines the design and implementation approach for automatically discovering function overloads from .NET assemblies using reflection. The system is designed to work with:
1. **Sharpy.Core builtins** - Standard library functions shipped with Sharpy
2. **Third-party Sharpy modules** - External packages developed by the community
3. **User assemblies** - Custom .NET libraries the user wants to expose to Sharpy

This eliminates the need for manual registration of each overload and provides a standardized way to expose .NET functionality to Sharpy code.

## Current State

Currently, builtin functions are manually registered in `BuiltinRegistry.LoadBuiltins()`:

```csharp
// Manual registration example
private void RegisterRangeOverloads()
{
    var rangeReturnType = new GenericType { Name = "list", TypeArguments = new() { SemanticType.Int } };
    RegisterFunction("range", rangeReturnType,
        new ParameterSymbol { Name = "stop", Type = SemanticType.Int });
    RegisterFunction("range", rangeReturnType,
        new ParameterSymbol { Name = "start", Type = SemanticType.Int },
        new ParameterSymbol { Name = "stop", Type = SemanticType.Int });
    RegisterFunction("range", rangeReturnType,
        new ParameterSymbol { Name = "start", Type = SemanticType.Int },
        new ParameterSymbol { Name = "stop", Type = SemanticType.Int },
        new ParameterSymbol { Name = "step", Type = SemanticType.Int });
}
```

**Problems with current approach:**
- High maintenance burden - each new overload requires manual code changes
- Error-prone - easy to miss overloads or make mistakes in parameter definitions
- Tight coupling between `Sharpy.Core` and `Sharpy.Compiler`
- No compile-time validation that registered functions match actual implementations
- **No mechanism for third-party modules** - Users can't extend Sharpy with their own packages
- **No standard for library interop** - Each library would need custom integration code

## Proposed Solution

Use .NET reflection to automatically discover and register all eligible public methods from:
1. **Sharpy.Core.Exports** - Core builtin functions
2. **Third-party module exports** - Any class marked with `[SharpyModule]` attribute
3. **User-specified assemblies** - Assemblies provided via compiler flags or config

### Module Discovery Contract

Third-party modules must follow this contract to be discoverable:

```csharp
// Option 1: Attribute-based discovery (Recommended)
namespace MySharpyPackage
{
    [SharpyModule("mypackage")]
    public static class Exports
    {
        [SharpyFunction("greet")]
        public static void Greet(string name)
        {
            Console.WriteLine($"Hello, {name}!");
        }
        
        // Multiple overloads automatically discovered
        [SharpyFunction("add")]
        public static int Add(int a, int b) => a + b;
        
        [SharpyFunction("add")]
        public static double Add(double a, double b) => a + b;
    }
}

// Option 2: Convention-based discovery (fallback)
namespace MySharpyPackage
{
    // Any public static class named "Exports" in the assembly
    public static class Exports
    {
        // All public static methods are exposed
        public static void Greet(string name) { ... }
    }
}
```

### Compiler Integration

```csharp
// Command line usage
sharpyc --reference MySharpyPackage.dll program.spy

// In Sharpy code
import mypackage

mypackage.greet("World")
```

## Module System Architecture

### Assembly Discovery Pipeline

```
┌─────────────────────────────────────────────────────────────┐
│                    Compilation Process                       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. Parse command-line arguments                            │
│     --reference MyPackage.dll                               │
│     --module-path ./modules                                 │
│                                                              │
│  2. Scan for assemblies                                     │
│     ├─ Sharpy.Core.dll (always included)                    │
│     ├─ Explicitly referenced assemblies                     │
│     ├─ Assemblies in module search paths                    │
│     └─ Transitive dependencies (optional)                   │
│                                                              │
│  3. Load and validate assemblies                            │
│     ├─ Check for [SharpyModule] attributes                  │
│     ├─ Verify compatibility version                         │
│     └─ Resolve dependencies                                 │
│                                                              │
│  4. Discover exports from each assembly                     │
│     ├─ Find classes with [SharpyModule]                     │
│     ├─ Fallback to "Exports" classes by convention          │
│     └─ Extract public static methods                        │
│                                                              │
│  5. Build module registry                                   │
│     ├─ Map module names to symbols                          │
│     ├─ Register functions in appropriate namespaces         │
│     └─ Handle name conflicts                                │
│                                                              │
│  6. Make available to semantic analyzer                     │
│     └─ Resolve import statements                            │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### Module Metadata Schema

Each discovered module maintains this metadata:

```csharp
public class ModuleMetadata
{
    // Module identity
    public string Name { get; set; }                    // e.g., "mypackage"
    public Version Version { get; set; }                // Semantic version
    public string AssemblyPath { get; set; }            // Full path to DLL
    
    // Discovery information
    public Type ExportsType { get; set; }               // The class containing exports
    public bool IsBuiltin { get; set; }                 // True for Sharpy.Core
    public DiscoveryMethod Method { get; set; }         // Attribute vs Convention
    
    // Exported symbols
    public List<FunctionSymbol> Functions { get; set; }
    public List<TypeSymbol> Types { get; set; }         // Future: exported classes
    public List<string> Dependencies { get; set; }      // Other required modules
    
    // Metadata for tooling
    public string? Documentation { get; set; }          // XML doc summary
    public Dictionary<string, string> Attributes { get; set; }
}

public enum DiscoveryMethod
{
    Attribute,      // Found via [SharpyModule]
    Convention,     // Found via naming convention
    Manual          // Manually registered
}
```

### Module Registry Architecture

```csharp
public class ModuleRegistry
{
    private readonly Dictionary<string, ModuleMetadata> _modules;
    private readonly List<string> _searchPaths;
    private readonly AssemblyLoader _assemblyLoader;
    private readonly TypeMapper _typeMapper;
    
    // Register a module from an assembly
    public ModuleMetadata RegisterAssembly(string assemblyPath);
    
    // Discover all modules in a directory
    public List<ModuleMetadata> DiscoverModules(string directory);
    
    // Get module by name
    public ModuleMetadata? GetModule(string moduleName);
    
    // Resolve an import statement
    public ModuleMetadata? ResolveImport(string importPath);
    
    // Get all functions from a module
    public List<FunctionSymbol> GetModuleFunctions(string moduleName);
}
```

## Implementation Steps

### Step 0: Define Sharpy Module Attributes

Create attribute classes to mark modules and functions:

```csharp
// In Sharpy.Core or a shared assembly
namespace Sharpy.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SharpyModuleAttribute : Attribute
    {
        public string ModuleName { get; }
        public string? Version { get; set; }
        public string? Description { get; set; }
        
        public SharpyModuleAttribute(string moduleName)
        {
            ModuleName = moduleName;
        }
    }
    
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class SharpyFunctionAttribute : Attribute
    {
        public string? FunctionName { get; set; }
        public int OverloadPriority { get; set; } = 0;
        public bool Hidden { get; set; } = false;
        
        public SharpyFunctionAttribute(string? functionName = null)
        {
            FunctionName = functionName;
        }
    }
    
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class SharpyParameterAttribute : Attribute
    {
        public string? Description { get; set; }
        public bool IsVariadic { get; set; } = false;
    }
}
```

### Step 1: Create Assembly Loader

Build infrastructure to safely load and validate assemblies:

```csharp
public class AssemblyLoader
{
    private readonly List<string> _searchPaths;
    private readonly Dictionary<string, Assembly> _loadedAssemblies;
    
    public AssemblyLoader(IEnumerable<string> searchPaths)
    {
        _searchPaths = searchPaths.ToList();
        _loadedAssemblies = new Dictionary<string, Assembly>();
    }
    
    public Assembly LoadAssembly(string assemblyPath)
    {
        // Normalize path
        var fullPath = Path.GetFullPath(assemblyPath);
        
        // Check cache
        if (_loadedAssemblies.TryGetValue(fullPath, out var cached))
            return cached;
        
        // Load assembly
        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(fullPath);
        }
        catch (Exception ex)
        {
            throw new ModuleLoadException(
                $"Failed to load assembly: {assemblyPath}", ex);
        }
        
        // Validate compatibility
        ValidateAssembly(assembly);
        
        // Cache and return
        _loadedAssemblies[fullPath] = assembly;
        return assembly;
    }
    
    public List<Assembly> DiscoverAssemblies(string directory)
    {
        var assemblies = new List<Assembly>();
        
        if (!Directory.Exists(directory))
            return assemblies;
        
        var dllFiles = Directory.GetFiles(directory, "*.dll", 
            SearchOption.TopDirectoryOnly);
        
        foreach (var dllPath in dllFiles)
        {
            try
            {
                var assembly = LoadAssembly(dllPath);
                assemblies.Add(assembly);
            }
            catch (ModuleLoadException ex)
            {
                // Log warning but continue
                Console.WriteLine($"Warning: {ex.Message}");
            }
        }
        
        return assemblies;
    }
    
    private void ValidateAssembly(Assembly assembly)
    {
        // Check target framework compatibility
        var targetFramework = assembly.GetCustomAttribute<
            System.Runtime.Versioning.TargetFrameworkAttribute>();
        
        if (targetFramework != null)
        {
            // Ensure compatible with current runtime
            // e.g., .NET 9.0 assemblies should work
        }
        
        // Check for Sharpy compatibility version (future)
        var sharpyVersion = assembly.GetCustomAttribute<SharpyVersionAttribute>();
        if (sharpyVersion != null)
        {
            // Validate version compatibility
        }
    }
}

public class ModuleLoadException : Exception
{
    public ModuleLoadException(string message, Exception? inner = null)
        : base(message, inner) { }
}
```

### Step 2: Create CLR Type to SemanticType Mapper

### Step 2: Create CLR Type to SemanticType Mapper

Create a robust type mapping system that converts CLR types to Sharpy's `SemanticType`:

```csharp
public class TypeMapper
{
    private readonly Dictionary<Type, SemanticType> _typeCache;
    private readonly Dictionary<string, SemanticType> _customMappings;
    
    public TypeMapper()
    {
        _typeCache = new Dictionary<Type, SemanticType>();
        _customMappings = new Dictionary<string, SemanticType>();
    }
    
    // Allow modules to register custom type mappings
    public void RegisterTypeMapping(Type clrType, SemanticType sharpyType)
    {
        _customMappings[clrType.FullName!] = sharpyType;
    }
    
    public SemanticType MapClrTypeToSemanticType(Type clrType)
    {
        // Check cache first
        if (_typeCache.TryGetValue(clrType, out var cached))
            return cached;
        
        var result = MapTypeInternal(clrType);
        _typeCache[clrType] = result;
        return result;
    }
    
    private SemanticType MapTypeInternal(Type clrType)
    {
        // Check custom mappings first
        if (_customMappings.TryGetValue(clrType.FullName!, out var custom))
            return custom;
        
        // Handle primitive types
        if (clrType == typeof(int)) return SemanticType.Int;
        if (clrType == typeof(long)) return SemanticType.Long;
        if (clrType == typeof(float)) return SemanticType.Float;
        if (clrType == typeof(double)) return SemanticType.Double;
        if (clrType == typeof(bool)) return SemanticType.Bool;
        if (clrType == typeof(string)) return SemanticType.Str;
        if (clrType == typeof(void)) return SemanticType.Void;
        if (clrType == typeof(object)) return SemanticType.Object;
        
        // Handle arrays
        if (clrType.IsArray)
        {
            var elementType = clrType.GetElementType()!;
            return new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(elementType)
                }
            };
        }
        
        // Handle generic types
        if (clrType.IsGenericType)
        {
            return MapGenericType(clrType);
        }
        
        // Handle interfaces and abstract types
        if (clrType.IsInterface)
        {
            return MapInterfaceType(clrType);
        }
        
        // Handle enums
        if (clrType.IsEnum)
        {
            return SemanticType.Int; // Or create enum type
        }
        
        // Check for Sharpy-specific types
        if (clrType.Namespace?.StartsWith("Sharpy.Core") == true)
        {
            return MapSharpyCoreType(clrType);
        }
        
        // Fallback to object for unknown types
        return SemanticType.Object;
    }
    
    private SemanticType MapGenericType(Type clrType)
    {
        var genericDef = clrType.GetGenericTypeDefinition();
        var typeArgs = clrType.GetGenericArguments();
        
        // List<T> or Sharpy.Core.List<T>
        if (IsGenericTypeOf(genericDef, typeof(List<>)) || 
            IsGenericTypeOf(genericDef, "Sharpy.Core.List`1"))
        {
            return new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> 
                { 
                    MapClrTypeToSemanticType(typeArgs[0]) 
                }
            };
        }
        
        // Dict<K, V> or Dictionary<K, V>
        if (IsGenericTypeOf(genericDef, typeof(Dictionary<,>)) || 
            IsGenericTypeOf(genericDef, "Sharpy.Core.Dict`2"))
        {
            return new GenericType
            {
                Name = "dict",
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }
        
        // Set<T> or HashSet<T>
        if (IsGenericTypeOf(genericDef, typeof(HashSet<>)) ||
            IsGenericTypeOf(genericDef, "Sharpy.Core.Set`1"))
        {
            return new GenericType
            {
                Name = "set",
                TypeArguments = new List<SemanicType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }
        
        // Tuple<...>
        if (genericDef.FullName?.StartsWith("System.Tuple") == true)
        {
            return new GenericType
            {
                Name = "tuple",
                TypeArguments = typeArgs
                    .Select(MapClrTypeToSemanticType)
                    .ToList()
            };
        }
        
        // IEnumerable<T>, IList<T>, etc.
        if (IsGenericTypeOf(genericDef, typeof(IEnumerable<>)))
        {
            return new GenericType
            {
                Name = "iterable",
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }
        
        // Nullable<T>
        if (Nullable.GetUnderlyingType(clrType) != null)
        {
            var underlyingType = Nullable.GetUnderlyingType(clrType)!;
            // In Sharpy, all types can be None, so just map the underlying type
            return MapClrTypeToSemanticType(underlyingType);
        }
        
        // Unknown generic type - fallback to object
        return SemanticType.Object;
    }
    
    private SemanticType MapInterfaceType(Type interfaceType)
    {
        // IEnumerable -> iterable
        if (interfaceType == typeof(IEnumerable))
        {
            return new GenericType
            {
                Name = "iterable",
                TypeArguments = new List<SemanticType> { SemanticType.Object }
            };
        }
        
        // IComparable -> comparable
        if (interfaceType == typeof(IComparable))
        {
            return SemanticType.Object; // Or create a protocol type
        }
        
        // Custom Sharpy interfaces
        if (interfaceType.Namespace?.StartsWith("Sharpy.Core") == true)
        {
            return MapSharpyCoreType(interfaceType);
        }
        
        return SemanticType.Object;
    }
    
    private SemanticType MapSharpyCoreType(Type sharpyType)
    {
        var typeName = sharpyType.Name;
        
        // Remove generic tick marks
        var backtickIndex = typeName.IndexOf('`');
        if (backtickIndex > 0)
            typeName = typeName.Substring(0, backtickIndex);
        
        // Convert to lowercase for Sharpy convention
        var sharpyName = typeName.ToLowerInvariant();
        
        // Handle known Sharpy types
        return sharpyName switch
        {
            "list" => SemanticType.List,
            "dict" => SemanticType.Dict,
            "set" => SemanticType.Set,
            "tuple" => SemanticType.Tuple,
            "range" => SemanticType.Range,
            _ => SemanticType.Object
        };
    }
    
    private bool IsGenericTypeOf(Type type, Type genericTypeDef)
    {
        return type == genericTypeDef || type.FullName == genericTypeDef.FullName;
    }
    
    private bool IsGenericTypeOf(Type type, string fullName)
    {
        return type.FullName == fullName;
    }
}
```

### Step 3: Handle Generic Method Definitions

Generic methods like `Max<T>` need special handling:

```csharp
public class GenericMethodHandler
{
    private readonly TypeMapper _typeMapper;
    
    public GenericMethodHandler(TypeMapper typeMapper)
    {
        _typeMapper = typeMapper;
    }
    
    public List<FunctionSymbol> ProcessGenericMethod(
        string functionName, 
        MethodInfo method,
        ModuleMetadata module)
    {
        var symbols = new List<FunctionSymbol>();
        
        if (!method.IsGenericMethodDefinition)
        {
            // Not generic, process normally
            symbols.Add(CreateFunctionSymbol(functionName, method, module));
            return symbols;
        }
        
        // Get generic type parameters and their constraints
        var typeParams = method.GetGenericArguments();
        var constraints = typeParams
            .Select(tp => new TypeParameterConstraints(tp))
            .ToList();
        
        // Strategy 1: Create concrete instantiations for common types
        var concreteSymbols = CreateConcreteInstantiations(
            functionName, method, typeParams, module);
        symbols.AddRange(concreteSymbols);
        
        // Strategy 2: Create generic function symbol (future enhancement)
        // symbols.Add(CreateGenericFunctionSymbol(functionName, method, 
        //     typeParams, constraints, module));
        
        return symbols;
    }
    
    private List<FunctionSymbol> CreateConcreteInstantiations(
        string functionName,
        MethodInfo method,
        Type[] typeParams,
        ModuleMetadata module)
    {
        var symbols = new List<FunctionSymbol>();
        
        // Define common type instantiations based on constraints
        var instantiationSets = GenerateInstantiationSets(typeParams);
        
        foreach (var typeArgs in instantiationSets)
        {
            try
            {
                var concreteMethod = method.MakeGenericMethod(typeArgs);
                var symbol = CreateFunctionSymbol(
                    functionName, concreteMethod, module);
                symbols.Add(symbol);
            }
            catch (ArgumentException)
            {
                // Type constraint not satisfied, skip this instantiation
                continue;
            }
        }
        
        return symbols;
    }
    
    private IEnumerable<Type[]> GenerateInstantiationSets(Type[] typeParams)
    {
        // For single type parameter, generate common types
        if (typeParams.Length == 1)
        {
            var typeParam = typeParams[0];
            var constraints = typeParam.GetGenericParameterConstraints();
            
            var candidates = new List<Type> 
            { 
                typeof(int), 
                typeof(string), 
                typeof(double),
                typeof(bool),
                typeof(object) 
            };
            
            // Filter by constraints
            foreach (var candidate in candidates)
            {
                if (SatisfiesConstraints(candidate, constraints))
                {
                    yield return new[] { candidate };
                }
            }
        }
        // For multiple type parameters, generate combinations
        else
        {
            // Generate common combinations
            // This can get complex, so limit to practical cases
            var commonCombinations = new[]
            {
                new[] { typeof(int), typeof(int) },
                new[] { typeof(string), typeof(string) },
                new[] { typeof(object), typeof(object) },
                // Add more as needed
            };
            
            foreach (var combo in commonCombinations)
            {
                if (combo.Length == typeParams.Length)
                {
                    bool allSatisfy = true;
                    for (int i = 0; i < typeParams.Length; i++)
                    {
                        var constraints = typeParams[i]
                            .GetGenericParameterConstraints();
                        if (!SatisfiesConstraints(combo[i], constraints))
                        {
                            allSatisfy = false;
                            break;
                        }
                    }
                    
                    if (allSatisfy)
                        yield return combo;
                }
            }
        }
    }
    
    private bool SatisfiesConstraints(Type type, Type[] constraints)
    {
        foreach (var constraint in constraints)
        {
            if (constraint.IsInterface)
            {
                if (!constraint.IsAssignableFrom(type))
                    return false;
            }
            else if (constraint.IsClass)
            {
                if (!type.IsSubclassOf(constraint) && type != constraint)
                    return false;
            }
        }
        
        return true;
    }
    
    private FunctionSymbol CreateFunctionSymbol(
        string functionName,
        MethodInfo method,
        ModuleMetadata module)
    {
        // Implementation in Step 5
        throw new NotImplementedException();
    }
}

public class TypeParameterConstraints
{
    public string Name { get; set; }
    public bool HasReferenceTypeConstraint { get; set; }
    public bool HasValueTypeConstraint { get; set; }
    public bool HasDefaultConstructorConstraint { get; set; }
    public List<Type> TypeConstraints { get; set; }
    
    public TypeParameterConstraints(Type typeParam)
    {
        Name = typeParam.Name;
        var constraints = typeParam.GenericParameterAttributes;
        
        HasReferenceTypeConstraint = 
            (constraints & GenericParameterAttributes.ReferenceTypeConstraint) != 0;
        HasValueTypeConstraint = 
            (constraints & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0;
        HasDefaultConstructorConstraint = 
            (constraints & GenericParameterAttributes.DefaultConstructorConstraint) != 0;
        
        TypeConstraints = typeParam.GetGenericParameterConstraints().ToList();
    }
}
```

### Step 4: Extract Default Parameter Values

Convert CLR default values to AST expressions:

```csharp
public class DefaultValueConverter
{
    public Expression? ConvertDefaultValueToExpression(ParameterInfo parameter)
    {
        if (!parameter.HasDefaultValue)
            return null;
        
        var defaultValue = parameter.DefaultValue;
        
        // Handle DBNull (represents missing optional parameter)
        if (defaultValue == DBNull.Value)
            return null;
        
        // Handle null
        if (defaultValue == null)
        {
            return new NoneLiteral();
        }
        
        // Handle primitive types
        if (defaultValue is int intVal)
        {
            return new IntegerLiteral { Value = intVal.ToString() };
        }
        
        if (defaultValue is long longVal)
        {
            return new IntegerLiteral { Value = longVal.ToString() };
        }
        
        if (defaultValue is string strVal)
        {
            return new StringLiteral { Value = strVal };
        }
        
        if (defaultValue is bool boolVal)
        {
            return new BooleanLiteral { Value = boolVal };
        }
        
        if (defaultValue is double doubleVal)
        {
            return new FloatLiteral { Value = doubleVal.ToString("G17") };
        }
        
        if (defaultValue is float floatVal)
        {
            return new FloatLiteral { Value = floatVal.ToString("G9") };
        }
        
        // Handle enums
        if (defaultValue.GetType().IsEnum)
        {
            var enumValue = Convert.ToInt32(defaultValue);
            return new IntegerLiteral { Value = enumValue.ToString() };
        }
        
        // Handle static field references (e.g., Console.Out)
        if (TryResolveStaticFieldReference(parameter, out var fieldRef))
        {
            return fieldRef;
        }
        
        // Handle empty collections
        if (defaultValue is Array arr && arr.Length == 0)
        {
            return new ListLiteral { Elements = new List<Expression>() };
        }
        
        // Fallback: log warning and treat as no default
        Console.WriteLine(
            $"Warning: Cannot convert default value of type {defaultValue.GetType()} " +
            $"for parameter {parameter.Name}");
        return null;
    }
    
    private bool TryResolveStaticFieldReference(
        ParameterInfo parameter, 
        out Expression? expression)
    {
        expression = null;
        
        // Check if there's a [DefaultValue] attribute with field reference
        var defaultAttr = parameter.GetCustomAttribute<DefaultValueAttribute>();
        if (defaultAttr?.Value is string fieldPath)
        {
            // Parse field path like "Console.Out" or "MyClass.DefaultValue"
            var parts = fieldPath.Split('.');
            if (parts.Length >= 2)
            {
                // Create a member access expression
                Expression current = new Identifier { Name = parts[0] };
                for (int i = 1; i < parts.Length; i++)
                {
                    current = new MemberAccess
                    {
                        Object = current,
                        MemberName = parts[i]
                    };
                }
                expression = current;
                return true;
            }
        }
        
        return false;
    }
}
```

### Step 5: Implement Module Discovery and Registration

The main discovery logic that ties everything together:

```csharp
public class ModuleDiscoveryEngine
{
    private readonly AssemblyLoader _assemblyLoader;
    private readonly TypeMapper _typeMapper;
    private readonly GenericMethodHandler _genericHandler;
    private readonly DefaultValueConverter _defaultConverter;
    private readonly Dictionary<string, ModuleMetadata> _modules;
    
    public ModuleDiscoveryEngine(IEnumerable<string> searchPaths)
    {
        _assemblyLoader = new AssemblyLoader(searchPaths);
        _typeMapper = new TypeMapper();
        _genericHandler = new GenericMethodHandler(_typeMapper);
        _defaultConverter = new DefaultValueConverter();
        _modules = new Dictionary<string, ModuleMetadata>();
    }
    
    /// <summary>
    /// Discover and register all modules from the given assemblies
    /// </summary>
    public void DiscoverModules(IEnumerable<string> assemblyPaths)
    {
        foreach (var path in assemblyPaths)
        {
            try
            {
                var assembly = _assemblyLoader.LoadAssembly(path);
                var modules = DiscoverModulesFromAssembly(assembly, path);
                
                foreach (var module in modules)
                {
                    RegisterModule(module);
                }
            }
            catch (ModuleLoadException ex)
            {
                Console.WriteLine($"Error loading assembly {path}: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Discover modules from a single assembly
    /// </summary>
    private List<ModuleMetadata> DiscoverModulesFromAssembly(
        Assembly assembly, 
        string assemblyPath)
    {
        var modules = new List<ModuleMetadata>();
        
        // Strategy 1: Look for [SharpyModule] attributes
        var attributedTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<SharpyModuleAttribute>() != null)
            .ToList();
        
        foreach (var type in attributedTypes)
        {
            var module = CreateModuleFromAttributedType(type, assemblyPath);
            if (module != null)
                modules.Add(module);
        }
        
        // Strategy 2: Convention-based discovery (if no attributed types found)
        if (modules.Count == 0)
        {
            var exportTypes = assembly.GetTypes()
                .Where(t => t.Name == "Exports" && t.IsClass && t.IsPublic)
                .ToList();
            
            foreach (var type in exportTypes)
            {
                var module = CreateModuleFromConvention(type, assemblyPath);
                if (module != null)
                    modules.Add(module);
            }
        }
        
        return modules;
    }
    
    private ModuleMetadata? CreateModuleFromAttributedType(
        Type type, 
        string assemblyPath)
    {
        var attr = type.GetCustomAttribute<SharpyModuleAttribute>();
        if (attr == null)
            return null;
        
        var module = new ModuleMetadata
        {
            Name = attr.ModuleName,
            Version = ParseVersion(attr.Version),
            AssemblyPath = assemblyPath,
            ExportsType = type,
            IsBuiltin = IsBuiltinAssembly(assemblyPath),
            Method = DiscoveryMethod.Attribute,
            Documentation = ExtractDocumentation(type),
            Functions = new List<FunctionSymbol>(),
            Types = new List<TypeSymbol>(),
            Dependencies = new List<string>(),
            Attributes = new Dictionary<string, string>()
        };
        
        // Discover functions from the type
        DiscoverFunctionsFromType(type, module);
        
        return module;
    }
    
    private ModuleMetadata? CreateModuleFromConvention(
        Type type, 
        string assemblyPath)
    {
        // Derive module name from assembly name
        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        var moduleName = assemblyName.ToLowerInvariant()
            .Replace(".", "_")
            .Replace("sharpy_", "");
        
        var module = new ModuleMetadata
        {
            Name = moduleName,
            Version = new Version(1, 0, 0),
            AssemblyPath = assemblyPath,
            ExportsType = type,
            IsBuiltin = IsBuiltinAssembly(assemblyPath),
            Method = DiscoveryMethod.Convention,
            Documentation = ExtractDocumentation(type),
            Functions = new List<FunctionSymbol>(),
            Types = new List<TypeSymbol>(),
            Dependencies = new List<string>(),
            Attributes = new Dictionary<string, string>()
        };
        
        DiscoverFunctionsFromType(type, module);
        
        return module;
    }
    
    private void DiscoverFunctionsFromType(Type type, ModuleMetadata module)
    {
        // Get all public static methods
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
        
        // Filter out methods we don't want to register
        var eligibleMethods = methods
            .Where(m => !m.Name.StartsWith("_"))         // Skip private helpers
            .Where(m => !m.Name.StartsWith("get_"))      // Skip property getters
            .Where(m => !m.Name.StartsWith("set_"))      // Skip property setters
            .Where(m => !m.IsSpecialName)                // Skip operators, etc.
            .Where(m => !IsHidden(m))                    // Skip [SharpyFunction(Hidden=true)]
            .ToList();
        
        // Group by function name (considering [SharpyFunction] attribute)
        var methodGroups = eligibleMethods
            .GroupBy(m => GetFunctionName(m), StringComparer.OrdinalIgnoreCase);
        
        foreach (var group in methodGroups)
        {
            var functionName = group.Key;
            
            foreach (var method in group)
            {
                try
                {
                    var symbols = _genericHandler.ProcessGenericMethod(
                        functionName, method, module);
                    module.Functions.AddRange(symbols);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Failed to register {module.Name}.{method.Name}: {ex.Message}");
                }
            }
        }
    }
    
    private FunctionSymbol CreateFunctionSymbol(
        string functionName,
        MethodInfo method,
        ModuleMetadata module)
    {
        // Map return type
        var returnType = _typeMapper.MapClrTypeToSemanticType(method.ReturnType);
        
        // Map parameters
        var parameters = method.GetParameters()
            .Select(p => new ParameterSymbol
            {
                Name = p.Name ?? "arg",
                Type = _typeMapper.MapClrTypeToSemanticType(p.ParameterType),
                HasDefault = p.HasDefaultValue,
                DefaultValue = _defaultConverter.ConvertDefaultValueToExpression(p),
                IsVariadic = IsVariadicParameter(p)
            })
            .ToList();
        
        // Extract documentation
        var documentation = ExtractMethodDocumentation(method);
        
        // Create function symbol
        var symbol = new FunctionSymbol
        {
            Name = functionName,
            ReturnType = returnType,
            Parameters = parameters,
            IsBuiltin = module.IsBuiltin,
            ModuleName = module.Name,
            Documentation = documentation,
            // Store reference to CLR method for code generation
            Metadata = new Dictionary<string, object>
            {
                ["ClrMethod"] = method,
                ["ClrType"] = method.DeclaringType!
            }
        };
        
        return symbol;
    }
    
    private void RegisterModule(ModuleMetadata module)
    {
        if (_modules.ContainsKey(module.Name))
        {
            throw new ModuleRegistrationException(
                $"Module '{module.Name}' is already registered");
        }
        
        _modules[module.Name] = module;
        
        Console.WriteLine(
            $"Registered module '{module.Name}' with {module.Functions.Count} functions");
    }
    
    // Helper methods
    
    private bool IsBuiltinAssembly(string assemblyPath)
    {
        return assemblyPath.Contains("Sharpy.Core");
    }
    
    private string GetFunctionName(MethodInfo method)
    {
        // Check for [SharpyFunction] attribute
        var attr = method.GetCustomAttribute<SharpyFunctionAttribute>();
        if (attr?.FunctionName != null)
            return attr.FunctionName;
        
        // Convert method name to Python convention
        // e.g., "PrintLine" -> "print_line"
        var name = method.Name;
        name = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
        
        return name;
    }
    
    private bool IsHidden(MethodInfo method)
    {
        var attr = method.GetCustomAttribute<SharpyFunctionAttribute>();
        return attr?.Hidden == true;
    }
    
    private bool IsVariadicParameter(ParameterInfo parameter)
    {
        var attr = parameter.GetCustomAttribute<SharpyParameterAttribute>();
        if (attr?.IsVariadic == true)
            return true;
        
        // Also check for params keyword
        return parameter.GetCustomAttribute<ParamArrayAttribute>() != null;
    }
    
    private Version ParseVersion(string? versionString)
    {
        if (string.IsNullOrEmpty(versionString))
            return new Version(1, 0, 0);
        
        return Version.TryParse(versionString, out var version) 
            ? version 
            : new Version(1, 0, 0);
    }
    
    private string? ExtractDocumentation(Type type)
    {
        // TODO: Parse XML documentation if available
        return null;
    }
    
    private string? ExtractMethodDocumentation(MethodInfo method)
    {
        // TODO: Parse XML documentation if available
        return null;
    }
    
    // Public API for accessing registered modules
    
    public ModuleMetadata? GetModule(string moduleName)
    {
        return _modules.TryGetValue(moduleName, out var module) ? module : null;
    }
    
    public IEnumerable<ModuleMetadata> GetAllModules()
    {
        return _modules.Values;
    }
    
    public IEnumerable<FunctionSymbol> GetModuleFunctions(string moduleName)
    {
        return GetModule(moduleName)?.Functions ?? Enumerable.Empty<FunctionSymbol>();
    }
}

public class ModuleRegistrationException : Exception
{
    public ModuleRegistrationException(string message) : base(message) { }
}
```

### Step 6: Integrate with Compiler Pipeline

Connect the module discovery system to the Sharpy compiler:

```csharp
public class CompilerOptions
{
    public List<string> ReferencedAssemblies { get; set; } = new();
    public List<string> ModuleSearchPaths { get; set; } = new();
    public bool AutoDiscoverModules { get; set; } = true;
    public bool IncludeBuiltins { get; set; } = true;
}

public class Compiler
{
    private ModuleDiscoveryEngine _moduleDiscovery;
    private ModuleRegistry _moduleRegistry;
    
    public void Initialize(CompilerOptions options)
    {
        // Setup module search paths
        var searchPaths = new List<string>(options.ModuleSearchPaths);
        
        // Add default paths
        searchPaths.Add(GetSharpyCoreDirectory());
        searchPaths.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sharpy", "modules"));
        
        // Initialize discovery engine
        _moduleDiscovery = new ModuleDiscoveryEngine(searchPaths);
        _moduleRegistry = new ModuleRegistry(_moduleDiscovery);
        
        // Discover builtins
        if (options.IncludeBuiltins)
        {
            var coreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
            _moduleDiscovery.DiscoverModules(new[] { coreAssembly });
        }
        
        // Discover explicitly referenced assemblies
        _moduleDiscovery.DiscoverModules(options.ReferencedAssemblies);
        
        // Auto-discover modules in search paths
        if (options.AutoDiscoverModules)
        {
            foreach (var searchPath in searchPaths)
            {
                if (Directory.Exists(searchPath))
                {
                    var assemblies = Directory.GetFiles(searchPath, "*.dll");
                    _moduleDiscovery.DiscoverModules(assemblies);
                }
            }
        }
    }
    
    private string GetSharpyCoreDirectory()
    {
        var coreAssembly = typeof(Sharpy.Core.Exports).Assembly;
        return Path.GetDirectoryName(coreAssembly.Location)!;
    }
}

public class ModuleRegistry
{
    private readonly ModuleDiscoveryEngine _discovery;
    private readonly Dictionary<string, NamespaceSymbol> _namespaces;
    
    public ModuleRegistry(ModuleDiscoveryEngine discovery)
    {
        _discovery = discovery;
        _namespaces = new Dictionary<string, NamespaceSymbol>();
        BuildNamespaceTree();
    }
    
    private void BuildNamespaceTree()
    {
        foreach (var module in _discovery.GetAllModules())
        {
            var ns = GetOrCreateNamespace(module.Name);
            
            // Add all functions to namespace
            foreach (var function in module.Functions)
            {
                ns.AddSymbol(function);
            }
            
            // Add all types to namespace
            foreach (var type in module.Types)
            {
                ns.AddSymbol(type);
            }
        }
    }
    
    private NamespaceSymbol GetOrCreateNamespace(string name)
    {
        if (!_namespaces.TryGetValue(name, out var ns))
        {
            ns = new NamespaceSymbol { Name = name };
            _namespaces[name] = ns;
        }
        return ns;
    }
    
    public NamespaceSymbol? ResolveImport(string moduleName)
    {
        return _namespaces.TryGetValue(moduleName, out var ns) ? ns : null;
    }
    
    public FunctionSymbol? ResolveFunction(string moduleName, string functionName)
    {
        var ns = ResolveImport(moduleName);
        return ns?.GetSymbol<FunctionSymbol>(functionName);
    }
}
```

### Step 7: Handle Edge Cases

### Step 7: Handle Edge Cases

**Special method name transformations:**
```csharp
private string GetSharpyFunctionName(MethodInfo method)
{
    // Check for explicit attribute first
    var attr = method.GetCustomAttribute<SharpyFunctionAttribute>();
    if (attr?.FunctionName != null)
        return attr.FunctionName;
    
    var name = method.Name;
    
    // Handle Python naming conventions
    // e.g., "PrintLine" -> "print_line"
    name = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
    
    // Handle special cases
    var specialCases = new Dictionary<string, string>
    {
        ["print_line"] = "print",
        ["to_string"] = "str",
        ["get_length"] = "len",
        ["get_type"] = "type"
    };
    
    return specialCases.TryGetValue(name, out var mapped) ? mapped : name;
}
```

**Type constraint validation:**
```csharp
private bool ValidateTypeConstraints(MethodInfo method)
{
    if (!method.IsGenericMethodDefinition)
        return true;
    
    var typeParams = method.GetGenericArguments();
    foreach (var typeParam in typeParams)
    {
        var constraints = typeParam.GetGenericParameterConstraints();
        
        // Check if constraints can be mapped to Sharpy's type system
        foreach (var constraint in constraints)
        {
            if (!CanMapConstraintType(constraint))
            {
                Console.WriteLine(
                    $"Warning: Cannot map constraint {constraint} for {method.Name}");
                return false;
            }
        }
        
        // Check special constraints
        var attrs = typeParam.GenericParameterAttributes;
        if ((attrs & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
        {
            // struct constraint - may not be expressible in Sharpy
        }
    }
    
    return true;
}

private bool CanMapConstraintType(Type constraintType)
{
    // Can we map this type to a Sharpy semantic type?
    try
    {
        var mapped = _typeMapper.MapClrTypeToSemanticType(constraintType);
        return mapped != SemanticType.Object || constraintType == typeof(object);
    }
    catch
    {
        return false;
    }
}
```

**Handling method overload ambiguity:**
```csharp
private void RegisterFunctionWithAmbiguityResolution(
    string functionName,
    FunctionSymbol symbol,
    ModuleMetadata module)
{
    // Check for existing overloads
    var existing = module.Functions
        .Where(f => f.Name == functionName)
        .ToList();
    
    foreach (var other in existing)
    {
        if (AreSignaturesEquivalent(symbol, other))
        {
            // Ambiguous overload detected
            Console.WriteLine(
                $"Warning: Ambiguous overload for {functionName} in {module.Name}");
            Console.WriteLine($"  Existing: {FormatSignature(other)}");
            Console.WriteLine($"  New:      {FormatSignature(symbol)}");
            
            // Strategy: Keep the one with more specific types
            if (IsMoreSpecific(symbol, other))
            {
                module.Functions.Remove(other);
                module.Functions.Add(symbol);
            }
            // Otherwise skip the new one
            return;
        }
    }
    
    // No conflict, add the symbol
    module.Functions.Add(symbol);
}

private bool AreSignaturesEquivalent(FunctionSymbol a, FunctionSymbol b)
{
    if (a.Parameters.Count != b.Parameters.Count)
        return false;
    
    for (int i = 0; i < a.Parameters.Count; i++)
    {
        if (!AreTypesEquivalent(a.Parameters[i].Type, b.Parameters[i].Type))
            return false;
    }
    
    return true;
}

private bool AreTypesEquivalent(SemanticType a, SemanticType b)
{
    // Compare semantic types for equivalence
    // Handle generic types, etc.
    return a.Equals(b);
}

private bool IsMoreSpecific(FunctionSymbol a, FunctionSymbol b)
{
    // Determine which overload is more specific
    // Prefer: non-generic over generic, specific types over object, etc.
    
    for (int i = 0; i < a.Parameters.Count; i++)
    {
        var typeA = a.Parameters[i].Type;
        var typeB = b.Parameters[i].Type;
        
        if (typeA == SemanticType.Object && typeB != SemanticType.Object)
            return false;
        if (typeA != SemanticType.Object && typeB == SemanticType.Object)
            return true;
    }
    
    return false;
}

private string FormatSignature(FunctionSymbol symbol)
{
    var paramStr = string.Join(", ", symbol.Parameters.Select(p => 
        $"{p.Name}: {p.Type}"));
    return $"{symbol.Name}({paramStr}) -> {symbol.ReturnType}";
}
```

**Versioning and compatibility:**
```csharp
public class ModuleCompatibilityChecker
{
    private readonly Version _compilerVersion;
    
    public ModuleCompatibilityChecker(Version compilerVersion)
    {
        _compilerVersion = compilerVersion;
    }
    
    public bool IsCompatible(ModuleMetadata module)
    {
        // Check if module version is compatible with compiler
        if (module.Version > _compilerVersion)
        {
            Console.WriteLine(
                $"Warning: Module {module.Name} v{module.Version} " +
                $"is newer than compiler v{_compilerVersion}");
            return false;
        }
        
        // Check major version compatibility
        if (module.Version.Major != _compilerVersion.Major)
        {
            Console.WriteLine(
                $"Error: Module {module.Name} major version {module.Version.Major} " +
                $"incompatible with compiler v{_compilerVersion}");
            return false;
        }
        
        return true;
    }
    
    public void CheckDependencies(ModuleMetadata module, 
        ModuleDiscoveryEngine discovery)
    {
        foreach (var depName in module.Dependencies)
        {
            var dep = discovery.GetModule(depName);
            if (dep == null)
            {
                throw new ModuleLoadException(
                    $"Module {module.Name} requires missing dependency: {depName}");
            }
            
            if (!IsCompatible(dep))
            {
                throw new ModuleLoadException(
                    $"Module {module.Name} requires incompatible version of {depName}");
            }
        }
    }
}
```

## Implementation Challenges and Solutions

### Challenge 1: Generic Type Parameters

**Problem:** Methods like `Max<T>(IIterable<T> iterable)` have unbounded type parameters.

**Solutions:**
1. **Skip generic methods** - Simplest approach, manually register these methods
2. **Create concrete instantiations** - Generate versions for common types (int, string, object)
3. **Implement generic function symbols** - Extend `FunctionSymbol` to support generic parameters (complex)

**Recommended:** Start with option 1, migrate to option 2 as needed.

### Challenge 2: Complex Default Values

**Problem:** Default values like `file = Stdout` reference constants that need to be resolved.

**Solutions:**
1. **Evaluate constants** - Use reflection to get constant values
2. **Create identifier expressions** - Generate AST nodes that reference the constant
3. **Skip parameters with complex defaults** - Treat as required parameters

**Recommended:** Implement option 1 for primitive types, option 3 for complex types initially.

### Challenge 3: Interface and Abstract Types

**Problem:** Parameters like `IIterable<T>` don't have direct semantic type mappings.

**Solutions:**
1. **Create interface type symbols** - Extend type system to support interfaces
2. **Map to concrete types** - Map `IIterable<T>` to `list<T>` or similar
3. **Use object type** - Fallback to `SemanticType.Object`

**Recommended:** Option 2 with fallback to option 3.

### Challenge 4: Method Overload Ambiguity

**Problem:** Multiple overloads might map to same signature in Sharpy's type system.

**Solutions:**
1. **Skip ambiguous overloads** - Log warning and use first match
2. **Enhance type system** - Add more specific type information
3. **Use CLR method info** - Store reference to actual CLR method for disambiguation

**Recommended:** Option 1 with detailed logging.

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public void ReflectionDiscovery_FindsRangeOverloads()
{
    var registry = new BuiltinRegistry();
    var overloads = registry.GetFunctionOverloads("range");
    
    Assert.NotNull(overloads);
    Assert.Equal(3, overloads.Count);
    Assert.Contains(overloads, o => o.Parameters.Count == 1);
    Assert.Contains(overloads, o => o.Parameters.Count == 2);
    Assert.Contains(overloads, o => o.Parameters.Count == 3);
}

[Fact]
public void ReflectionDiscovery_MapsTypesCorrectly()
{
    var registry = new BuiltinRegistry();
    var func = registry.GetFunction("range");
    
    Assert.NotNull(func);
    Assert.Equal(SemanticType.Int, func.Parameters[0].Type);
}

[Fact]
public void ReflectionDiscovery_HandlesDefaultParameters()
{
    var registry = new BuiltinRegistry();
    var func = registry.GetFunction("print");
    
    Assert.NotNull(func);
    var sepParam = func.Parameters.FirstOrDefault(p => p.Name == "sep");
    Assert.NotNull(sepParam);
    Assert.True(sepParam.HasDefault);
}
```

### Integration Tests

Ensure all existing tests still pass after switching to reflection-based discovery.

## Detailed Implementation Plan

This section provides a comprehensive, step-by-step guide to implementing the reflection-based discovery system.

### Phase 0: Preparation and Foundation (Week 1)

**Goal:** Set up the foundation and required infrastructure.

#### Step 0.1: Create Sharpy.Attributes Assembly

1. Create a new project `Sharpy.Attributes.csproj`
2. Define all attribute classes (`SharpyModuleAttribute`, `SharpyFunctionAttribute`, etc.)
3. Build and publish the attributes assembly
4. Add reference to `Sharpy.Core` project

**Deliverable:** `Sharpy.Attributes.dll` that can be referenced by both `Sharpy.Core` and third-party modules

**Testing:**
- Verify attributes can be applied to test classes
- Verify attributes are accessible via reflection

```bash
# Commands
cd src
dotnet new classlib -n Sharpy.Attributes
cd Sharpy.Attributes
# Add attribute code
dotnet build
cd ../Sharpy.Core
dotnet add reference ../Sharpy.Attributes/Sharpy.Attributes.csproj
dotnet build
```

#### Step 0.2: Update Sharpy.Core with Attributes

1. Add `[SharpyModule("builtins")]` to `Exports` class
2. Add `[SharpyFunction]` attributes to key functions
3. Document which functions are attributed

**Testing:**
- Build `Sharpy.Core` successfully
- Verify attributes are present via reflection in a test program

```csharp
// Verification test
var exportsType = typeof(Sharpy.Core.Exports);
var attr = exportsType.GetCustomAttribute<SharpyModuleAttribute>();
Assert.NotNull(attr);
Assert.Equal("builtins", attr.ModuleName);
```

#### Step 0.3: Design Module Metadata Schema

1. Define all classes: `ModuleMetadata`, `FunctionSymbol`, etc.
2. Create unit tests for the data structures
3. Document the schema

**Deliverable:** Well-defined data structures with XML documentation

---

### Phase 1: Core Discovery Engine (Weeks 2-3)

**Goal:** Implement the core reflection-based discovery mechanism for simple cases.

#### Step 1.1: Implement AssemblyLoader

**Tasks:**
1. Create `AssemblyLoader` class
2. Implement safe assembly loading with error handling
3. Add assembly validation logic
4. Create unit tests

**Implementation order:**
```
1. Basic LoadAssembly(string path)
2. Error handling and validation
3. Caching mechanism
4. DiscoverAssemblies(string directory)
5. Search path resolution
```

**Testing:**
- Load `Sharpy.Core.dll` successfully
- Handle invalid assembly paths gracefully
- Verify caching works correctly
- Load multiple assemblies from a directory

**Success criteria:**
- All unit tests pass
- Can load Sharpy.Core without errors
- Proper error messages for invalid assemblies

#### Step 1.2: Implement TypeMapper (Part 1: Primitives)

**Tasks:**
1. Create `TypeMapper` class
2. Implement primitive type mapping (int, string, bool, etc.)
3. Add unit tests for each primitive type

**Implementation order:**
```
1. MapTypeInternal() for primitives
2. Caching mechanism
3. Unit tests
```

**Testing:**
```csharp
[Fact]
public void TypeMapper_MapsPrimitives()
{
    var mapper = new TypeMapper();
    Assert.Equal(SemanticType.Int, mapper.MapClrTypeToSemanticType(typeof(int)));
    Assert.Equal(SemanticType.Str, mapper.MapClrTypeToSemanticType(typeof(string)));
    Assert.Equal(SemanticType.Bool, mapper.MapClrTypeToSemanticType(typeof(bool)));
    // etc.
}
```

**Success criteria:**
- All primitive types map correctly
- Caching improves performance on repeated calls

#### Step 1.3: Implement TypeMapper (Part 2: Collections)

**Tasks:**
1. Add generic type mapping for `List<T>`, `Dictionary<K,V>`, etc.
2. Handle arrays
3. Add comprehensive tests

**Testing:**
```csharp
[Fact]
public void TypeMapper_MapsCollections()
{
    var mapper = new TypeMapper();
    
    var listInt = mapper.MapClrTypeToSemanticType(typeof(List<int>));
    Assert.IsType<GenericType>(listInt);
    Assert.Equal("list", ((GenericType)listInt).Name);
    
    var dictType = mapper.MapClrTypeToSemanticType(typeof(Dictionary<string, int>));
    Assert.IsType<GenericType>(dictType);
    Assert.Equal("dict", ((GenericType)dictType).Name);
}
```

**Success criteria:**
- Generic collections map correctly
- Arrays are mapped to lists
- Nested generics work (e.g., `List<List<int>>`)

#### Step 1.4: Implement DefaultValueConverter

**Tasks:**
1. Create `DefaultValueConverter` class
2. Handle primitive default values
3. Handle null values
4. Add tests

**Testing:**
- Convert various default values correctly
- Handle `DBNull.Value` appropriately
- Null maps to `NoneLiteral`

**Success criteria:**
- All primitive default values convert correctly
- Proper AST nodes created for each type

#### Step 1.5: Implement Basic ModuleDiscoveryEngine

**Tasks:**
1. Create `ModuleDiscoveryEngine` class structure
2. Implement attribute-based discovery only
3. Discover methods from a type
4. Handle simple (non-generic) methods only
5. Add integration tests

**Implementation order:**
```
1. Class structure and dependencies
2. DiscoverModulesFromAssembly() - attribute path only
3. CreateModuleFromAttributedType()
4. DiscoverFunctionsFromType() - simple methods only
5. CreateFunctionSymbol() - simple cases
6. Integration test with a mock module
```

**Testing:**
```csharp
[Fact]
public void DiscoveryEngine_DiscoversAttributedModule()
{
    // Create test assembly with [SharpyModule]
    var engine = new ModuleDiscoveryEngine(new[] { "./test" });
    engine.DiscoverModules(new[] { "TestModule.dll" });
    
    var module = engine.GetModule("testmodule");
    Assert.NotNull(module);
    Assert.True(module.Functions.Count > 0);
}
```

**Success criteria:**
- Can discover a simple test module with [SharpyModule]
- Functions with primitive types are registered correctly
- Overloads are grouped correctly

---

### Phase 2: Extended Discovery Features (Weeks 4-5)

**Goal:** Add support for generic methods, complex types, and convention-based discovery.

#### Step 2.1: Implement GenericMethodHandler

**Tasks:**
1. Create `GenericMethodHandler` class
2. Implement concrete instantiation strategy
3. Add constraint checking
4. Test with generic methods

**Testing:**
```csharp
[Fact]
public void GenericHandler_CreatesConcreteInstantiations()
{
    var handler = new GenericMethodHandler(typeMapper);
    var method = typeof(TestClass).GetMethod("Max");
    
    var symbols = handler.ProcessGenericMethod("max", method, module);
    
    // Should create Max<int>, Max<string>, etc.
    Assert.True(symbols.Count >= 2);
}
```

**Success criteria:**
- Generic methods create multiple concrete overloads
- Type constraints are respected
- Common type combinations are generated

#### Step 2.2: Add Convention-Based Discovery

**Tasks:**
1. Implement `CreateModuleFromConvention()`
2. Handle "Exports" class naming convention
3. Module name derivation from assembly name
4. Add tests

**Testing:**
- Discover module without [SharpyModule] attribute
- Correct module name derived from assembly

**Success criteria:**
- Modules without attributes are discovered
- Fallback naming works correctly

#### Step 2.3: Handle Complex Default Values

**Tasks:**
1. Extend `DefaultValueConverter` for complex cases
2. Handle static field references
3. Handle empty collections
4. Add comprehensive tests

**Success criteria:**
- Complex default values handled or gracefully skipped
- No crashes on unsupported defaults

#### Step 2.4: Implement Module Registry

**Tasks:**
1. Create `ModuleRegistry` class
2. Build namespace tree
3. Implement import resolution
4. Add tests

**Testing:**
```csharp
[Fact]
public void ModuleRegistry_ResolvesImports()
{
    var registry = new ModuleRegistry(discovery);
    var ns = registry.ResolveImport("mymodule");
    
    Assert.NotNull(ns);
    var func = ns.GetSymbol<FunctionSymbol>("myfunction");
    Assert.NotNull(func);
}
```

**Success criteria:**
- Modules organized into namespaces
- Import resolution works
- Function lookup by module and name works

---

### Phase 3: Compiler Integration (Week 6)

**Goal:** Integrate discovery system with the Sharpy compiler.

#### Step 3.1: Update Compiler Class

**Tasks:**
1. Add module discovery to compiler initialization
2. Add command-line options (`--reference`, `--module-path`)
3. Parse and process module options
4. Initialize `ModuleDiscoveryEngine` during compilation

**Changes needed:**
```csharp
// In Compiler.cs
public class Compiler
{
    private ModuleDiscoveryEngine _moduleDiscovery;
    private ModuleRegistry _moduleRegistry;
    
    public void Initialize(CompilerOptions options)
    {
        // Discovery initialization code
    }
}
```

**Testing:**
- Compile with `--reference MyModule.dll`
- Verify module functions are available
- Error handling for missing modules

#### Step 3.2: Update Semantic Analyzer

**Tasks:**
1. Modify semantic analyzer to use `ModuleRegistry`
2. Handle import statements
3. Resolve function calls to modules
4. Update name resolution

**Changes needed:**
- Import statement resolution
- Qualified name resolution (e.g., `mymodule.function()`)
- Function call type checking with module functions

**Testing:**
```sharpy
# Test file
import mymodule

mymodule.greet("World")  # Should resolve and type-check
```

**Success criteria:**
- Import statements work
- Module functions are resolved
- Type checking works with module functions

#### Step 3.3: Update Code Generator

**Tasks:**
1. Generate C# code that references the CLR methods
2. Emit proper using statements for module assemblies
3. Handle fully qualified names

**Changes needed:**
```csharp
// In CodeGenerator.cs
public override string VisitFunctionCall(FunctionCall node)
{
    if (node.Symbol.IsBuiltin || node.Symbol.ModuleName != null)
    {
        // Get CLR method from metadata
        var clrMethod = (MethodInfo)node.Symbol.Metadata["ClrMethod"];
        var clrType = (Type)node.Symbol.Metadata["ClrType"];
        
        // Generate: TypeName.MethodName(args)
        return $"{clrType.FullName}.{clrMethod.Name}({...})";
    }
    // ... existing code
}
```

**Testing:**
- Generated C# code compiles
- Function calls execute correctly
- Assembly references are included

---

### Phase 4: Testing and Validation (Week 7)

**Goal:** Comprehensive testing of the complete system.

#### Step 4.1: Create Test Modules

**Tasks:**
1. Create 3-5 sample third-party modules
2. Cover various scenarios:
   - Simple functions with primitives
   - Generic functions
   - Functions with default parameters
   - Functions with complex types
3. Attribute-based and convention-based modules

**Example test module:**
```csharp
namespace TestModules
{
    [SharpyModule("mathutils")]
    public static class MathUtils
    {
        [SharpyFunction("square")]
        public static int Square(int x) => x * x;
        
        [SharpyFunction("max")]
        public static T Max<T>(T a, T b) where T : IComparable<T>
        {
            return a.CompareTo(b) > 0 ? a : b;
        }
        
        [SharpyFunction("sum")]
        public static double Sum(params double[] numbers)
        {
            return numbers.Sum();
        }
    }
}
```

#### Step 4.2: End-to-End Integration Tests

**Tasks:**
1. Write Sharpy programs that use test modules
2. Compile and run them
3. Verify outputs

**Test cases:**
```sharpy
# test1.spy - Basic module usage
import mathutils

print(mathutils.square(5))  # Should print 25
print(mathutils.max(10, 20))  # Should print 20

# test2.spy - Generic function usage
import mathutils

print(mathutils.max("apple", "banana"))  # Should work with strings

# test3.spy - Variadic function
import mathutils

print(mathutils.sum(1.0, 2.0, 3.0, 4.0))  # Should print 10.0
```

**Success criteria:**
- All test programs compile
- All test programs run correctly
- Output matches expected

#### Step 4.3: Regression Testing

**Tasks:**
1. Ensure all existing Sharpy tests still pass
2. Verify Sharpy.Core builtins work as before
3. Check for performance regressions

**Success criteria:**
- No existing tests broken
- Compilation time acceptable
- Runtime performance unchanged

#### Step 4.4: Error Handling Tests

**Tasks:**
1. Test missing module errors
2. Test invalid assembly errors
3. Test version incompatibility
4. Test name conflicts

**Test cases:**
```bash
# Should fail gracefully
sharpyc --reference NonExistent.dll test.spy
sharpyc --reference Invalid.txt test.spy

# Should detect conflicts
# (two modules with same name)
```

**Success criteria:**
- Clear error messages
- No crashes
- Proper exit codes

---

### Phase 5: Documentation and Polish (Week 8)

**Goal:** Document the system and prepare for release.

#### Step 5.1: Write Developer Documentation

**Tasks:**
1. Document how to create a Sharpy module
2. Provide examples and templates
3. Document attributes and conventions
4. Create a module authoring guide

**Deliverables:**
- `docs/module-development-guide.md`
- `docs/module-api-reference.md`
- Sample module template project

#### Step 5.2: Write User Documentation

**Tasks:**
1. Document compiler options
2. Explain import system
3. Provide usage examples
4. Update language reference

**Deliverables:**
- Update `docs/language_reference.md` with import syntax
- Add module examples to `docs/manual/`

#### Step 5.3: Performance Optimization

**Tasks:**
1. Profile the discovery process
2. Optimize hot paths
3. Add parallel assembly loading if beneficial
4. Cache results appropriately

**Metrics to track:**
- Discovery time for Sharpy.Core
- Discovery time for 10 modules
- Memory usage
- Compiler startup time

**Success criteria:**
- Discovery adds < 100ms to compilation time
- Memory usage reasonable (< 50MB for typical projects)

#### Step 5.4: Create Migration Guide

**Tasks:**
1. Document migration path from manual registration
2. Provide before/after examples
3. List breaking changes (if any)
4. Create migration script/tool if needed

---

### Phase 6: Advanced Features (Future)

These features can be implemented after the core system is stable.

#### Step 6.1: Generic Function Symbols

Instead of concrete instantiations, support true generic functions in Sharpy:

```sharpy
# Future syntax
def max<T>(a: T, b: T) -> T:
    # Generic function
    pass
```

**Requirements:**
- Extend `FunctionSymbol` to support type parameters
- Update semantic analyzer for generic type inference
- Update code generator for generic method calls

#### Step 6.2: XML Documentation Parsing

**Tasks:**
1. Parse XML documentation from assemblies
2. Extract function descriptions
3. Extract parameter descriptions
4. Make available to tooling (LSP, etc.)

#### Step 6.3: Transitive Dependency Resolution

**Tasks:**
1. Automatically discover dependencies of modules
2. Load transitive dependencies
3. Handle version conflicts
4. Create a package.config system

#### Step 6.4: Module Package Manager

**Tasks:**
1. Create a module registry (like npm, PyPI)
2. Implement `sharpy install <module>` command
3. Version resolution and dependency management
4. Lock file support

---

### Phase 7: Cleanup and Release (Week 9)

#### Step 7.1: Code Review and Cleanup

**Tasks:**
1. Code review all implementations
2. Remove debug code
3. Add XML documentation to all public APIs
4. Ensure consistent coding style

#### Step 7.2: Remove Manual Registration

**Tasks:**
1. Delete old manual registration code from `BuiltinRegistry`
2. Verify all builtins are discovered automatically
3. Update tests that depended on manual registration

**Verification:**
```csharp
[Fact]
public void AllBuiltinFunctionsDiscovered()
{
    var discovery = new ModuleDiscoveryEngine(...);
    var builtins = discovery.GetModule("builtins");
    
    // Verify all expected builtins are present
    Assert.Contains(builtins.Functions, f => f.Name == "print");
    Assert.Contains(builtins.Functions, f => f.Name == "range");
    Assert.Contains(builtins.Functions, f => f.Name == "len");
    // etc.
}
```

#### Step 7.3: Final Testing

**Tasks:**
1. Run full test suite
2. Test on different platforms (Windows, macOS, Linux)
3. Performance benchmarking
4. Stress testing with many modules

#### Step 7.4: Release

**Tasks:**
1. Update version numbers
2. Create release notes
3. Tag release in git
4. Build and publish packages

---

## Implementation Checklist

### Phase 0: Preparation ✓
- [ ] Create `Sharpy.Attributes` project
- [ ] Define all attribute classes
- [ ] Update `Sharpy.Core` with attributes
- [ ] Verify attributes via reflection
- [ ] Design and document metadata schema

### Phase 1: Core Discovery ✓
- [ ] Implement `AssemblyLoader`
- [ ] Implement `TypeMapper` for primitives
- [ ] Implement `TypeMapper` for collections
- [ ] Implement `DefaultValueConverter`
- [ ] Implement basic `ModuleDiscoveryEngine`
- [ ] Write unit tests for each component
- [ ] Integration test with simple module

### Phase 2: Extended Features ✓
- [ ] Implement `GenericMethodHandler`
- [ ] Add convention-based discovery
- [ ] Handle complex default values
- [ ] Implement `ModuleRegistry`
- [ ] Write comprehensive tests

### Phase 3: Compiler Integration ✓
- [ ] Update `Compiler` class
- [ ] Add command-line options
- [ ] Update semantic analyzer
- [ ] Update code generator
- [ ] Integration tests

### Phase 4: Testing ✓
- [ ] Create test modules
- [ ] End-to-end tests
- [ ] Regression tests
- [ ] Error handling tests

### Phase 5: Documentation ✓
- [ ] Developer documentation
- [ ] User documentation
- [ ] Performance optimization
- [ ] Migration guide

### Phase 6: Advanced Features (Optional)
- [ ] Generic function symbols
- [ ] XML documentation parsing
- [ ] Transitive dependencies
- [ ] Package manager

### Phase 7: Release ✓
- [ ] Code review and cleanup
- [ ] Remove manual registration
- [ ] Final testing
- [ ] Release preparation

---

## Success Metrics

### Functional Requirements
- ✅ Sharpy.Core builtins discovered automatically
- ✅ Third-party modules can be loaded
- ✅ Function overloads handled correctly
- ✅ Type mapping works for common types
- ✅ Import statements resolve correctly
- ✅ Generated code compiles and runs

### Quality Requirements
- Test coverage > 80%
- All existing tests pass
- No performance regression
- Clear error messages
- Comprehensive documentation

### User Experience
- Easy to create new modules
- Clear compiler errors
- Good IDE support (future)
- Discoverable features

## Third-Party Module Examples

### Example 1: Simple Utility Module

```csharp
// File: MySharpyUtils/StringUtils.cs
using Sharpy.Attributes;

namespace MySharpyUtils
{
    [SharpyModule("strutils", Version = "1.0.0", 
        Description = "String utility functions")]
    public static class StringUtils
    {
        [SharpyFunction("reverse")]
        public static string Reverse(string text)
        {
            var chars = text.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }
        
        [SharpyFunction("repeat")]
        public static string Repeat(string text, int count)
        {
            return string.Concat(Enumerable.Repeat(text, count));
        }
        
        [SharpyFunction("truncate")]
        public static string Truncate(
            string text, 
            int maxLength, 
            [SharpyParameter(Description = "Suffix to add when truncated")]
            string suffix = "...")
        {
            if (text.Length <= maxLength)
                return text;
            
            return text.Substring(0, maxLength - suffix.Length) + suffix;
        }
    }
}
```

**Usage in Sharpy:**
```sharpy
import strutils

text = "Hello, World!"
print(strutils.reverse(text))  # !dlroW ,olleH
print(strutils.repeat("Ha", 3))  # HaHaHa
print(strutils.truncate("This is a long string", 10))  # This is...
```

### Example 2: Math Module with Generics

```csharp
// File: MathExtensions/Statistics.cs
using Sharpy.Attributes;

namespace MathExtensions
{
    [SharpyModule("stats")]
    public static class Statistics
    {
        [SharpyFunction("mean")]
        public static double Mean(params double[] numbers)
        {
            if (numbers.Length == 0)
                throw new ArgumentException("Cannot compute mean of empty array");
            return numbers.Average();
        }
        
        [SharpyFunction("median")]
        public static double Median(params double[] numbers)
        {
            if (numbers.Length == 0)
                throw new ArgumentException("Cannot compute median of empty array");
            
            var sorted = numbers.OrderBy(x => x).ToArray();
            int mid = sorted.Length / 2;
            
            if (sorted.Length % 2 == 0)
                return (sorted[mid - 1] + sorted[mid]) / 2.0;
            else
                return sorted[mid];
        }
        
        [SharpyFunction("min")]
        public static T Min<T>(params T[] values) where T : IComparable<T>
        {
            if (values.Length == 0)
                throw new ArgumentException("Cannot find min of empty array");
            
            T min = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i].CompareTo(min) < 0)
                    min = values[i];
            }
            return min;
        }
        
        [SharpyFunction("max")]
        public static T Max<T>(params T[] values) where T : IComparable<T>
        {
            if (values.Length == 0)
                throw new ArgumentException("Cannot find max of empty array");
            
            T max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i].CompareTo(max) > 0)
                    max = values[i];
            }
            return max;
        }
    }
}
```

**Usage in Sharpy:**
```sharpy
import stats

numbers = [1.5, 2.7, 3.2, 4.1, 5.9]
print(stats.mean(numbers))    # 3.48
print(stats.median(numbers))  # 3.2
print(stats.min(numbers))     # 1.5
print(stats.max(numbers))     # 5.9

# Generic functions work with different types
scores = [85, 92, 78, 95, 88]
print(stats.max(scores))      # 95
```

### Example 3: Module with Complex Types

```csharp
// File: DataStructures/Collections.cs
using Sharpy.Attributes;
using Sharpy.Core;

namespace DataStructures
{
    [SharpyModule("collections")]
    public static class Collections
    {
        [SharpyFunction("counter")]
        public static Dict<T, int> Counter<T>(List<T> items) where T : notnull
        {
            var counts = new Dict<T, int>();
            foreach (var item in items)
            {
                if (counts.ContainsKey(item))
                    counts[item]++;
                else
                    counts[item] = 1;
            }
            return counts;
        }
        
        [SharpyFunction("unique")]
        public static List<T> Unique<T>(List<T> items)
        {
            return new List<T>(items.Distinct());
        }
        
        [SharpyFunction("flatten")]
        public static List<T> Flatten<T>(List<List<T>> nested)
        {
            var result = new List<T>();
            foreach (var list in nested)
            {
                result.AddRange(list);
            }
            return result;
        }
        
        [SharpyFunction("chunk")]
        public static List<List<T>> Chunk<T>(List<T> items, int size)
        {
            var chunks = new List<List<T>>();
            for (int i = 0; i < items.Count; i += size)
            {
                var chunk = new List<T>();
                for (int j = i; j < Math.Min(i + size, items.Count); j++)
                {
                    chunk.Add(items[j]);
                }
                chunks.Add(chunk);
            }
            return chunks;
        }
    }
}
```

**Usage in Sharpy:**
```sharpy
import collections

# Counter
words = ["apple", "banana", "apple", "cherry", "banana", "apple"]
counts = collections.counter(words)
print(counts)  # {"apple": 3, "banana": 2, "cherry": 1}

# Unique
numbers = [1, 2, 2, 3, 3, 3, 4, 4, 4, 4]
print(collections.unique(numbers))  # [1, 2, 3, 4]

# Flatten
nested = [[1, 2], [3, 4], [5, 6]]
print(collections.flatten(nested))  # [1, 2, 3, 4, 5, 6]

# Chunk
items = [1, 2, 3, 4, 5, 6, 7, 8, 9]
print(collections.chunk(items, 3))  # [[1, 2, 3], [4, 5, 6], [7, 8, 9]]
```

### Example 4: Convention-Based Module (No Attributes)

```csharp
// File: SimpleModule/Exports.cs
// No attributes needed - discovered by convention

namespace SimpleModule
{
    public static class Exports
    {
        // All public static methods are automatically exposed
        
        public static string Greet(string name)
        {
            return $"Hello, {name}!";
        }
        
        public static int Add(int a, int b)
        {
            return a + b;
        }
        
        public static double Add(double a, double b)
        {
            return a + b;
        }
        
        // Methods starting with _ are ignored
        private static void _InternalHelper()
        {
            // Not exposed
        }
    }
}
```

**Usage in Sharpy:**
```sharpy
import simplemodule

print(simplemodule.greet("Alice"))  # Hello, Alice!
print(simplemodule.add(2, 3))       # 5
print(simplemodule.add(2.5, 3.7))   # 6.2
```

---

## Best Practices for Module Authors

### 1. Module Organization

**DO:**
- One module per assembly
- Clear, descriptive module names
- Group related functionality
- Use semantic versioning

**DON'T:**
- Mix multiple unrelated modules in one assembly
- Use overly generic names (e.g., "utils", "helpers")
- Break semantic versioning conventions

### 2. Function Naming

**DO:**
- Use snake_case for function names
- Use descriptive names
- Use `[SharpyFunction]` to specify exact names
- Follow Python naming conventions

**DON'T:**
- Use PascalCase (it will be converted)
- Use abbreviations unless widely understood
- Use names that conflict with builtins

**Examples:**
```csharp
// Good
[SharpyFunction("calculate_distance")]
public static double CalculateDistance(double x1, double y1, double x2, double y2)

// Also good (auto-converted)
public static double CalculateDistance(...)  // becomes "calculate_distance"

// Bad
public static double CalcDist(...)  // Unclear abbreviation
public static double Print(...)     // Conflicts with builtin
```

### 3. Type Usage

**DO:**
- Use Sharpy.Core types when possible (`List<T>`, `Dict<K,V>`)
- Use primitive types (int, string, double, bool)
- Document type constraints clearly

**DON'T:**
- Use exotic .NET types
- Rely on complex inheritance hierarchies
- Use types that can't be mapped to Sharpy

**Examples:**
```csharp
// Good
public static List<int> GetNumbers() { ... }
public static Dict<string, object> GetData() { ... }

// Bad
public static SortedDictionary<int, MyCustomClass> GetComplexData() { ... }
```

### 4. Default Parameters

**DO:**
- Use simple default values (primitives, null, empty strings)
- Document defaults in XML comments
- Use reasonable defaults

**DON'T:**
- Use complex objects as defaults
- Use defaults that require expensive computation

**Examples:**
```csharp
// Good
public static void Log(string message, string level = "info") { ... }
public static List<T> Filter<T>(List<T> items, int maxCount = 100) { ... }

// Problematic
public static void Process(Data data, Configuration config = new Configuration())
// Complex default - may not convert properly
```

### 5. Generic Methods

**DO:**
- Use simple type constraints
- Test with multiple type instantiations
- Document which types are supported

**DON'T:**
- Use multiple type parameters unless necessary
- Use overly restrictive constraints
- Rely on type inference (not yet supported)

**Examples:**
```csharp
// Good
public static T Max<T>(T a, T b) where T : IComparable<T> { ... }

// Complex - may not auto-discover well
public static TResult Transform<TInput, TOutput, TResult>(...)
```

### 6. Documentation

**DO:**
- Use XML documentation comments
- Document all parameters
- Include usage examples
- Specify exceptions thrown

**Example:**
```csharp
/// <summary>
/// Calculates the distance between two points in 2D space.
/// </summary>
/// <param name="x1">X coordinate of first point</param>
/// <param name="y1">Y coordinate of first point</param>
/// <param name="x2">X coordinate of second point</param>
/// <param name="y2">Y coordinate of second point</param>
/// <returns>The Euclidean distance between the points</returns>
/// <exception cref="ArgumentException">
/// Thrown when any coordinate is NaN or Infinity
/// </exception>
[SharpyFunction("distance")]
public static double CalculateDistance(double x1, double y1, double x2, double y2)
{
    // Implementation
}
```

### 7. Error Handling

**DO:**
- Throw standard .NET exceptions
- Use descriptive error messages
- Validate inputs

**DON'T:**
- Swallow exceptions
- Use custom exception types (they won't map well)
- Return error codes instead of throwing

**Examples:**
```csharp
// Good
public static double Divide(double a, double b)
{
    if (b == 0)
        throw new ArgumentException("Cannot divide by zero", nameof(b));
    return a / b;
}

// Bad
public static double Divide(double a, double b)
{
    return b == 0 ? double.NaN : a / b;  // Unclear error handling
}
```

### 8. Testing

**DO:**
- Write unit tests for all functions
- Test with Sharpy compiler
- Test all overloads
- Test edge cases

**Example test:**
```csharp
[Fact]
public void DiscoveryEngine_FindsAllOverloads()
{
    var engine = new ModuleDiscoveryEngine(...);
    engine.DiscoverModules(new[] { "MyModule.dll" });
    
    var module = engine.GetModule("mymodule");
    var addFunctions = module.Functions.Where(f => f.Name == "add").ToList();
    
    Assert.Equal(2, addFunctions.Count);  // int and double versions
}
```

### 9. Versioning

**DO:**
- Follow semantic versioning (MAJOR.MINOR.PATCH)
- Increment MAJOR for breaking changes
- Increment MINOR for new features
- Increment PATCH for bug fixes
- Document changes in changelog

**Example:**
```csharp
[SharpyModule("mymodule", Version = "2.1.0")]
public static class Exports
{
    // Version 2.1.0 adds new features, maintains compatibility
}
```

### 10. Distribution

**DO:**
- Publish as NuGet packages
- Include README with usage examples
- Specify dependencies clearly
- Include license information

**Example package structure:**
```
MySharpyModule/
├── MySharpyModule.dll
├── README.md
├── LICENSE.txt
├── CHANGELOG.md
└── examples/
    ├── basic_usage.spy
    └── advanced_usage.spy
```

---

## Compiler Usage Guide

### Command-Line Options

```bash
# Reference a single module
sharpyc --reference MyModule.dll program.spy

# Reference multiple modules
sharpyc --reference Module1.dll --reference Module2.dll program.spy

# Specify module search paths
sharpyc --module-path ./modules --module-path ~/.sharpy/modules program.spy

# Combine options
sharpyc --reference Special.dll --module-path ./modules program.spy

# Disable auto-discovery
sharpyc --no-auto-discover program.spy
```

### Configuration File

Create a `sharpy.config.json` in your project directory:

```json
{
  "references": [
    "./libs/MyModule.dll",
    "./libs/AnotherModule.dll"
  ],
  "modulePaths": [
    "./modules",
    "~/.sharpy/modules"
  ],
  "autoDiscover": true,
  "includeBuiltins": true
}
```

### Project Structure

**Recommended structure:**
```
my-sharpy-project/
├── sharpy.config.json
├── src/
│   ├── main.spy
│   ├── utils.spy
│   └── models.spy
├── modules/
│   ├── custom-module.dll
│   └── another-module.dll
└── libs/
    └── third-party.dll
```

### Import Resolution

Sharpy resolves imports in this order:

1. **Builtins** - Standard library (always available)
2. **Explicitly referenced assemblies** - via `--reference`
3. **Module search paths** - via `--module-path`
4. **Current directory** - `./modules` by default
5. **User modules** - `~/.sharpy/modules`

**Example:**
```sharpy
# Imports are resolved automatically
import builtins    # From Sharpy.Core
import mymodule    # From referenced assembly or search paths
import custom      # From local modules/ directory
```

---

## Troubleshooting

### Issue: Module Not Found

**Symptoms:**
```
Error: Cannot resolve import 'mymodule'
```

**Solutions:**
1. Verify the assembly is referenced: `sharpyc --reference MyModule.dll program.spy`
2. Check module search paths: `sharpyc --module-path ./libs program.spy`
3. Verify the module name matches: Check `[SharpyModule("name")]` attribute
4. Use verbose mode: `sharpyc --verbose program.spy`

### Issue: Function Not Found

**Symptoms:**
```
Error: Module 'mymodule' has no function 'myfunc'
```

**Solutions:**
1. Verify function is public and static
2. Check function name (case-insensitive, snake_case)
3. Ensure function isn't hidden: `[SharpyFunction(Hidden = false)]`
4. Check if method was filtered out (starts with `_`, `get_`, `set_`)

### Issue: Type Mismatch

**Symptoms:**
```
Error: Cannot convert type 'CustomType' to Sharpy type
```

**Solutions:**
1. Use Sharpy.Core types instead of custom types
2. Use primitive types (int, string, double, bool)
3. Check type mapper supports your types
4. Use `object` as fallback type

### Issue: Assembly Load Failure

**Symptoms:**
```
Error: Failed to load assembly: MyModule.dll
```

**Solutions:**
1. Verify file exists and path is correct
2. Check assembly targets compatible .NET version (net9.0)
3. Verify all dependencies are available
4. Use absolute paths if relative paths don't work

### Issue: Version Conflict

**Symptoms:**
```
Warning: Module version 2.0.0 incompatible with compiler 1.5.0
```

**Solutions:**
1. Update Sharpy compiler to latest version
2. Use older version of the module
3. Check module compatibility documentation

---

## Performance Considerations

### Discovery Performance

- **Assembly loading**: ~10-50ms per assembly
- **Type discovery**: ~1-5ms per module
- **Function registration**: ~0.1ms per function
- **Total overhead**: ~100ms for typical projects

### Optimization Tips

1. **Use caching**: Discovery results are cached per compilation
2. **Minimize modules**: Only reference what you need
3. **Disable auto-discovery**: Use `--no-auto-discover` if not needed
4. **Pre-build module index**: Generate metadata at build time (future)

### Memory Usage

- **Per assembly**: ~1-5 MB
- **Per module**: ~100 KB
- **Per function**: ~1 KB
- **Total**: ~50 MB for large projects

---

## Future Enhancements

1. **Build-time code generation**: Generate registration code during build instead of runtime reflection
2. **Module package manager**: `sharpy install <package>` command
3. **XML documentation integration**: Show docs in IDE
4. **Generic type inference**: Full support for generic methods without explicit instantiation
5. **Cross-platform module discovery**: Better support for platform-specific modules
6. **Module signing and verification**: Security for third-party modules
7. **Hot reload**: Reload modules without recompilation
8. **Module composition**: Combine multiple modules into one namespace

---

## Conclusion

The reflection-based discovery system provides a robust, extensible foundation for Sharpy's module ecosystem. By supporting both Sharpy.Core builtins and third-party modules through a unified mechanism, it enables:

- **Reduced maintenance burden**: No manual registration needed
- **Extensibility**: Easy creation of third-party modules
- **Type safety**: Automatic type mapping and validation
- **Developer experience**: Simple, attribute-based API
- **Future-proof**: Designed for growth and evolution

### Key Achievements

✅ **Automatic discovery** - Functions discovered via reflection  
✅ **Third-party support** - Standard interface for external modules  
✅ **Type safety** - Robust CLR to Sharpy type mapping  
✅ **Overload handling** - Multiple signatures supported  
✅ **Generic methods** - Common instantiations generated  
✅ **Default parameters** - Preserved from CLR  
✅ **Error handling** - Clear, actionable error messages  
✅ **Documentation ready** - Comprehensive guides and examples  

### Next Steps

1. **Implement Phase 0**: Create attributes and update Sharpy.Core
2. **Implement Phase 1**: Build core discovery engine
3. **Test thoroughly**: Ensure robustness and correctness
4. **Document extensively**: Enable third-party developers
5. **Iterate and improve**: Based on real-world usage

This system transforms Sharpy from a standalone language into an extensible platform, enabling a rich ecosystem of libraries and tools built by the community.
