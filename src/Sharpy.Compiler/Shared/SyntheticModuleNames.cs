namespace Sharpy.Compiler.Shared;

/// <summary>
/// Names of synthetic (compiler-provided) modules.
/// Synthetic modules map to special codegen patterns rather than .spy files or .NET assemblies.
/// </summary>
internal static class SyntheticModuleNames
{
    internal const string Asyncio = "asyncio";
}
