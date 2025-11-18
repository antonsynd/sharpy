using Sharpy.Compiler.Discovery.Caching;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests;

public class DiscoveryDebugTests
{
    private readonly ITestOutputHelper _output;

    public DiscoveryDebugTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DebugIsInstanceDiscovery()
    {
        var assembly = Assembly.Load("Sharpy.Core");
        var builder = new OverloadIndexBuilder();
        var index = builder.BuildFromAssembly(assembly);

        if (index.Modules.TryGetValue("builtins", out var builtins))
        {
            _output.WriteLine($"Found {builtins.Functions.Count} functions in builtins module:");

            foreach (var kvp in builtins.Functions.OrderBy(f => f.Key).Take(20))
            {
                _output.WriteLine($"  {kvp.Key} ({kvp.Value.Count} overloads)");
            }

            if (builtins.Functions.TryGetValue("isinstance", out var isinstanceOverloads))
            {
                _output.WriteLine($"\nisinstance found with {isinstanceOverloads.Count} overloads:");
                foreach (var sig in isinstanceOverloads)
                {
                    _output.WriteLine($"  {sig.Name}({string.Join(", ", sig.Parameters.Select(p => $"{p.Name}: {p.Type.Name}"))}) -> {sig.ReturnType.Name}");
                }
            }
            else
            {
                _output.WriteLine("\n❌ isinstance NOT found in builtins!");

                // Check if is_instance exists instead
                if (builtins.Functions.TryGetValue("is_instance", out var isInstanceOverloads))
                {
                    _output.WriteLine($"\nBUT is_instance WAS found with {isInstanceOverloads.Count} overloads:");
                    foreach (var sig in isInstanceOverloads)
                    {
                        _output.WriteLine($"  {sig.Name}({string.Join(", ", sig.Parameters.Select(p => $"{p.Name}: {p.Type.Name}"))}) -> {sig.ReturnType.Name}");
                    }
                }
            }
        }
        else
        {
            _output.WriteLine("❌ builtins module not found!");
        }
    }
}
