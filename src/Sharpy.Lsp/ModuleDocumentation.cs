namespace Sharpy.Lsp;

/// <summary>
/// Provides one-line summary descriptions for stdlib modules.
/// Used to augment hover tooltips when ModuleSymbol.Documentation is not set.
/// </summary>
internal static class ModuleDocumentation
{
    private static readonly Dictionary<string, string> Summaries = new()
    {
        ["argparse"] = "Command-line argument parsing.",
        ["asyncio"] = "Async I/O primitives (sleep, gather, tasks).",
        ["builtins"] = "Built-in functions and types (print, len, range, etc.).",
        ["bisect"] = "Array bisection algorithm for sorted sequences.",
        ["collections"] = "Specialized container datatypes (defaultdict, Counter, deque).",
        ["copy"] = "Shallow and deep copy operations.",
        ["csv"] = "CSV file reading and writing.",
        ["datetime"] = "Date and time types and operations.",
        ["fnmatch"] = "Unix filename pattern matching.",
        ["functools"] = "Higher-order functions and operations on callable objects.",
        ["glob"] = "Unix-style pathname pattern expansion.",
        ["hashlib"] = "Secure hash and message digest algorithms.",
        ["heapq"] = "Heap queue (priority queue) algorithm.",
        ["io"] = "Core tools for working with streams.",
        ["itertools"] = "Iterator building blocks for efficient looping.",
        ["json"] = "JSON encoder and decoder.",
        ["logging"] = "Logging facility for Sharpy programs.",
        ["math"] = "Mathematical functions (sin, cos, sqrt, floor, ceil, etc.).",
        ["operator"] = "Standard operators as functions.",
        ["os"] = "Operating system interfaces.",
        ["pathlib"] = "Object-oriented filesystem paths.",
        ["random"] = "Generate pseudo-random numbers and choices.",
        ["re"] = "Regular expression matching operations.",
        ["shutil"] = "High-level file operations (copy, move, remove trees).",
        ["statistics"] = "Mathematical statistics functions.",
        ["string"] = "String constants and template operations.",
        ["sys"] = "System-specific parameters and functions.",
        ["tempfile"] = "Generate temporary files and directories.",
        ["textwrap"] = "Text wrapping and filling.",
        ["time"] = "Time access and conversions.",
    };

    /// <summary>
    /// Gets a one-line summary for a module by name, or null if not found.
    /// </summary>
    public static string? GetSummary(string moduleName) =>
        Summaries.TryGetValue(moduleName, out var summary) ? summary : null;
}
