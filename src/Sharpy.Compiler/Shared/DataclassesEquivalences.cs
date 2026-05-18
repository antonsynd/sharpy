using System.Collections.Generic;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Maps Python dataclasses module names to Sharpy native syntax redirect messages.
/// </summary>
public static class DataclassesEquivalences
{
    public const string GenericModuleMessage =
        "The 'dataclasses' module is not needed in Sharpy. Use '@dataclass' directly (no import needed).";

    private static readonly Dictionary<string, string> Messages = new()
    {
        ["dataclass"] = "Use native '@dataclass' decorator directly (no import needed)",
        ["field"] = "Use default values in class body instead of 'field()': 'x: int = 0'",
        ["asdict"] = "dataclasses.asdict() is not yet supported. Use manual dictionary construction.",
        ["astuple"] = "dataclasses.astuple() is not yet supported. Use manual tuple construction.",
        ["replace"] = "dataclasses.replace() is not yet supported.",
        ["FrozenInstanceError"] = "Sharpy frozen dataclasses use '@dataclass(frozen=True)' which maps to init-only properties.",
    };

    private const string FallbackMessage =
        "The 'dataclasses' module is not needed in Sharpy. Use '@dataclass' directly (no import needed).";

    public static string GetMessage(string name) =>
        Messages.TryGetValue(name, out var message) ? message : FallbackMessage;
}
