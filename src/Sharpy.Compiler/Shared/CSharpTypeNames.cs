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
    internal const string SharpyDefaultDict = "Sharpy.DefaultDict";
    internal const string SharpyFrozenDict = "Sharpy.FrozenDict";
    internal const string SharpyBytes = "Sharpy.Bytes";
    internal const string SharpyTemplate = "Sharpy.Template";
    internal const string SharpyInterpolation = "Sharpy.Interpolation";
    internal const string SharpyOptional = "Sharpy.Optional";
    internal const string SharpyResult = "Sharpy.Result";
    internal const string SharpyNdArray = "Sharpy.NdArray";
    internal const string IEnumerable = "IEnumerable";
    internal const string IAsyncEnumerable = "IAsyncEnumerable";
    internal const string XunitTraitCategory = "Category";

    // Sharpy-name -> C# type-name resolution lives on ClrTypeBridge (the single owner of the
    // CLR<->Sharpy<->C# name mapping). The former FromSharpyName collection map was collapsed into
    // ClrTypeBridge.SpecialCases.TryGetWrapperCollectionName.
}
