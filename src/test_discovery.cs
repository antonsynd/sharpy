using System;
using System.Reflection;
using Sharpy.Compiler.Discovery.Caching;

// Simple test to verify isinstance is being discovered
var assembly = Assembly.Load("Sharpy.Core");
var builder = new OverloadIndexBuilder();
var index = builder.BuildFromAssembly(assembly);

if (index.Modules.TryGetValue("builtins", out var builtins))
{
    Console.WriteLine($"Found {builtins.Functions.Count} functions in builtins module:");

    foreach (var kvp in builtins.Functions.OrderBy(f => f.Key))
    {
        Console.WriteLine($"  {kvp.Key} ({kvp.Value.Count} overloads)");
    }

    if (builtins.Functions.TryGetValue("isinstance", out var isinstanceOverloads))
    {
        Console.WriteLine($"\nisinstance found with {isinstanceOverloads.Count} overloads:");
        foreach (var sig in isinstanceOverloads)
        {
            Console.WriteLine($"  {sig.Name}({string.Join(", ", sig.Parameters.Select(p => $"{p.Name}: {p.Type.Name}"))}) -> {sig.ReturnType.Name}");
        }
    }
    else
    {
        Console.WriteLine("\n❌ isinstance NOT found in builtins!");
    }
}
else
{
    Console.WriteLine("❌ builtins module not found!");
}
