using System.Reflection;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Discovery.Caching;

/// <summary>
/// Builds an OverloadIndex from assembly reflection.
/// </summary>
public class OverloadIndexBuilder
{
    private readonly TypeMapper _typeMapper = new();

    /// <summary>
    /// Build an index from an assembly by discovering all public static methods
    /// in classes named "Exports".
    /// </summary>
    public OverloadIndex BuildFromAssembly(Assembly assembly)
    {
        var identity = AssemblyIdentity.FromAssembly(assembly);
        var index = new OverloadIndex
        {
            Identity = identity,
            CreatedAt = DateTime.UtcNow,
            CacheFormatVersion = 1
        };

        // Find all classes named "Exports"
        var exportTypes = assembly.GetTypes()
            .Where(t => t.Name == "Exports" && t.IsClass && t.IsPublic && t.IsAbstract && t.IsSealed)
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

        return index;
    }

    private string DeriveModuleName(Type exportType)
    {
        // For Sharpy.Core.Exports, use "builtins"
        if (exportType.FullName == "Sharpy.Core.Exports")
            return "builtins";

        // Otherwise derive from namespace
        var ns = exportType.Namespace ?? "unknown";
        return ns.ToLowerInvariant().Replace(".", "_");
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
            .Where(m => !m.IsGenericMethodDefinition)  // Skip generic methods for now
            .Where(m => !IsTypeConstructor(m))  // Skip type constructors like Bool, Int, Str
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
                    Console.Error.WriteLine($"Warning: Skipping {exportType.Name}.{method.Name}: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    // Skip methods that can't be mapped
                    Console.Error.WriteLine($"Warning: Skipping {exportType.Name}.{method.Name}: {ex.Message}");
                }
                catch (NotSupportedException ex)
                {
                    // Skip methods that can't be mapped
                    Console.Error.WriteLine($"Warning: Skipping {exportType.Name}.{method.Name}: {ex.Message}");
                }
            }

            if (signatures.Count > 0)
            {
                moduleOverloads.Functions[functionName] = signatures;
            }
        }

        return moduleOverloads;
    }

    private bool IsTypeConstructor(MethodInfo method)
    {
        // Type constructor functions have the same name as built-in types
        // and their return type matches their name
        var typeConstructors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Bool", "Int", "Long", "Float", "Double", "Str", "List", "Dict", "Set", "Tuple"
        };
        
        return typeConstructors.Contains(method.Name);
    }

    private string GetFunctionName(MethodInfo method)
    {
        // Convert PascalCase to snake_case
        var name = method.Name;
        name = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
        return name;
    }

    private FunctionSignature CreateFunctionSignature(MethodInfo method)
    {
        var signature = new FunctionSignature
        {
            Name = GetFunctionName(method),
            ReturnType = CreateTypeSignature(method.ReturnType),
            MethodToken = CreateMethodToken(method)
        };

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
        var semanticType = _typeMapper.MapClrTypeToSemanticType(clrType);
        
        var signature = new TypeSignature
        {
            Name = semanticType.GetDisplayName(),
            ClrTypeName = clrType.FullName ?? clrType.Name
        };

        if (semanticType is GenericType genericType)
        {
            signature.IsGeneric = true;
            signature.TypeArguments = genericType.TypeArguments
                .Select(t => new TypeSignature
                {
                    Name = t.GetDisplayName(),
                    ClrTypeName = string.Empty
                })
                .ToList();
        }

        return signature;
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
            bool b => b.ToString().ToLowerInvariant(),
            int or long or short or byte or sbyte or uint or ulong or ushort => value.ToString(),
            float f => f.ToString("G9"),
            double d => d.ToString("G17"),
            _ => null
        };
    }
}
