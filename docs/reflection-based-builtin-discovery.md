# Reflection-Based Builtin Function Discovery

## Overview

This document outlines the design and implementation approach for automatically discovering builtin function overloads from the `Sharpy.Core` assembly using .NET reflection, eliminating the need for manual registration of each overload.

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

## Proposed Solution

Use .NET reflection to automatically discover and register all public static methods from `Sharpy.Core.Exports` class.

## Implementation Steps

### Step 1: Create CLR Type to SemanticType Mapper

Create a robust type mapping system that converts CLR types to Sharpy's `SemanticType`:

```csharp
private SemanticType MapClrTypeToSemanticType(Type clrType)
{
    // Handle primitive types
    if (clrType == typeof(int)) return SemanticType.Int;
    if (clrType == typeof(long)) return SemanticType.Long;
    if (clrType == typeof(float)) return SemanticType.Float;
    if (clrType == typeof(double)) return SemanticType.Double;
    if (clrType == typeof(bool)) return SemanticType.Bool;
    if (clrType == typeof(string)) return SemanticType.Str;
    if (clrType == typeof(void)) return SemanticType.Void;
    if (clrType == typeof(object)) return SemanticType.Object;
    
    // Handle generic types
    if (clrType.IsGenericType)
    {
        var genericDef = clrType.GetGenericTypeDefinition();
        var typeArgs = clrType.GetGenericArguments();
        
        // List<T>
        if (genericDef == typeof(List<>) || genericDef == typeof(Sharpy.Core.List<>))
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
        
        // Dict<K, V>
        if (genericDef == typeof(Dictionary<,>) || genericDef == typeof(Sharpy.Core.Dict<,>))
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
        
        // Handle other generic types...
    }
    
    // Handle interfaces and abstract types
    if (clrType.IsInterface || clrType.IsAbstract)
    {
        // Map to appropriate semantic type based on interface
        // e.g., IIterable<T> -> iterable type
    }
    
    // Fallback to object for unknown types
    return SemanticType.Object;
}
```

### Step 2: Handle Generic Method Definitions

Generic methods like `Max<T>` need special handling:

```csharp
private void RegisterGenericMethod(MethodInfo method)
{
    // Skip generic methods for now, or handle with special logic
    // Option 1: Skip entirely
    if (method.IsGenericMethodDefinition)
    {
        // Log or document that generic methods aren't auto-registered
        return;
    }
    
    // Option 2: Create concrete instantiations for common types
    if (method.IsGenericMethodDefinition)
    {
        var commonTypes = new[] { typeof(int), typeof(string), typeof(object) };
        foreach (var typeArg in commonTypes)
        {
            try
            {
                var concreteMethod = method.MakeGenericMethod(typeArg);
                RegisterMethodOverload(concreteMethod);
            }
            catch (ArgumentException)
            {
                // Type constraint not satisfied, skip
            }
        }
        return;
    }
}
```

### Step 3: Extract Default Parameter Values

Convert CLR default values to AST expressions:

```csharp
private Expression? ConvertDefaultValueToExpression(ParameterInfo parameter)
{
    if (!parameter.HasDefaultValue)
        return null;
    
    var defaultValue = parameter.DefaultValue;
    
    // Handle null
    if (defaultValue == null)
    {
        return new NoneLiteral(); // or appropriate null representation
    }
    
    // Handle primitive types
    if (defaultValue is int intVal)
    {
        return new IntegerLiteral { Value = intVal.ToString() };
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
        return new FloatLiteral { Value = doubleVal.ToString() };
    }
    
    // Handle complex default values
    // May need to serialize and parse, or create AST nodes programmatically
    
    // Fallback: log warning and treat as no default
    return null;
}
```

### Step 4: Implement Main Discovery Logic

```csharp
private void LoadBuiltinFunctionsFromAssembly()
{
    // Get the Sharpy.Core.Exports type
    var exportsType = typeof(Sharpy.Core.Exports);
    
    // Get all public static methods
    var methods = exportsType.GetMethods(BindingFlags.Public | BindingFlags.Static);
    
    // Filter out methods we don't want to register
    var eligibleMethods = methods
        .Where(m => !m.Name.StartsWith("_"))  // Skip private helpers
        .Where(m => !m.Name.StartsWith("get_")) // Skip property getters
        .Where(m => !m.Name.StartsWith("set_")) // Skip property setters
        .Where(m => !m.IsSpecialName); // Skip operators, constructors, etc.
    
    // Group by name (case-insensitive) to handle overloads
    var methodGroups = eligibleMethods
        .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
    
    foreach (var group in methodGroups)
    {
        var sharpyFunctionName = group.Key.ToLowerInvariant();
        
        foreach (var method in group)
        {
            try
            {
                RegisterMethodOverload(sharpyFunctionName, method);
            }
            catch (Exception ex)
            {
                // Log error but continue processing other methods
                Console.WriteLine($"Failed to register {method.Name}: {ex.Message}");
            }
        }
    }
}

private void RegisterMethodOverload(string functionName, MethodInfo method)
{
    // Handle generic methods
    if (method.IsGenericMethodDefinition)
    {
        RegisterGenericMethod(method);
        return;
    }
    
    // Map return type
    var returnType = MapClrTypeToSemanticType(method.ReturnType);
    
    // Map parameters
    var parameters = method.GetParameters()
        .Select(p => new ParameterSymbol
        {
            Name = p.Name ?? "arg",
            Type = MapClrTypeToSemanticType(p.ParameterType),
            HasDefault = p.HasDefaultValue,
            DefaultValue = ConvertDefaultValueToExpression(p)
        })
        .ToArray();
    
    // Register the function overload
    RegisterFunction(functionName, returnType, parameters);
}
```

### Step 5: Handle Edge Cases

**Special method name transformations:**
```csharp
private string GetSharpyFunctionName(MethodInfo method)
{
    var name = method.Name;
    
    // Handle Python naming conventions
    // e.g., "PrintLine" -> "print_line"
    name = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
    
    // Handle special cases
    if (name == "print_line") return "print";
    
    return name;
}
```

**Type constraint validation:**
```csharp
private bool ValidateTypeConstraints(MethodInfo method)
{
    if (!method.IsGenericMethodDefinition)
        return true;
    
    // Check if type constraints can be satisfied
    var typeParams = method.GetGenericArguments();
    foreach (var typeParam in typeParams)
    {
        var constraints = typeParam.GetGenericParameterConstraints();
        // Validate constraints are compatible with Sharpy's type system
    }
    
    return true;
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

## Migration Plan

### Phase 1: Parallel Implementation
1. Keep existing manual registration
2. Implement reflection-based discovery alongside
3. Compare results and validate equivalence
4. Add tests for reflection system

### Phase 2: Gradual Migration
1. Start with simple functions (no generics, no complex defaults)
2. Move to functions with simple defaults
3. Handle remaining cases or keep manual registration

### Phase 3: Cleanup
1. Remove manual registration code for migrated functions
2. Document which functions still need manual registration and why
3. Add validation to ensure manual and automatic registrations don't conflict

## Performance Considerations

- **Caching:** Reflection is expensive - cache results after first discovery
- **Lazy loading:** Consider lazy initialization if startup time is a concern
- **Build-time generation:** Alternative approach - generate registration code at build time

## Future Enhancements

1. **Attribute-based metadata:** Add attributes to Sharpy.Core methods to provide hints
   ```csharp
   [SharpyFunction("range", OverloadPriority = 1)]
   public static RangeIterator Range(int stop) { ... }
   ```

2. **XML documentation parsing:** Extract parameter descriptions from XML docs

3. **Build-time code generation:** Generate registration code during build instead of runtime reflection

4. **Cross-assembly discovery:** Extend to discover builtins from multiple assemblies

## Conclusion

Reflection-based builtin discovery will significantly reduce maintenance burden and improve reliability. Start with simple cases, validate thoroughly, and gradually expand coverage.

**Recommended first implementation:** Focus on non-generic methods with primitive types and simple default values. This covers the majority of builtin functions including `range()`, `len()`, `print()`, etc.
