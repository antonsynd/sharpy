namespace Sharpy.Compiler.Shared;

/// <summary>
/// Single source of truth for C# type names used in code generation.
/// These constants match the Sharpy.Core runtime wrapper types.
/// </summary>
internal static class CSharpTypeNames
{
    internal const string SharpyList = "Sharpy.List";
    internal const string SharpyDict = "Sharpy.Dict";
    internal const string SharpySet = "Sharpy.Set";
    internal const string SharpyOptional = "Sharpy.Optional";
    internal const string SharpyResult = "Sharpy.Result";
}
