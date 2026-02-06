using System.Collections.Generic;
namespace Sharpy
{
    /// <summary>
    /// LINQ-style extension methods for Sharpy collections to enable natural C# integration.
    /// </summary>
    /// <remarks>
    /// These extension methods allow Sharpy collections to work seamlessly with C# LINQ
    /// query syntax and standard LINQ operators.
    ///
    /// Note: The original IIterable interface has been removed. Sharpy collections now
    /// implement standard .NET interfaces (IEnumerable, IReadOnlyCollection, etc.) directly,
    /// so standard LINQ methods work without the need for these extensions.
    /// </remarks>
    public static class IterableLinqExtensions
    {
        // This class is now empty as Sharpy collections implement standard .NET interfaces.
        // Standard LINQ methods from System.Linq work directly with our collections.
    }
}
