using System.Collections.Generic;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Maps Python typing module names to Sharpy native syntax redirect messages.
/// </summary>
public static class TypingEquivalences
{
    public const string GenericModuleMessage =
        "The 'typing' module is not needed in Sharpy. Use native type syntax: 'X?' for Optional, 'list[X]' for List, etc.";

    private static readonly Dictionary<string, string> Messages = new()
    {
        ["Optional"] = "Use native 'X?' syntax instead of 'Optional[X]'",
        ["List"] = "Use native 'list[X]' syntax instead of 'List[X]'",
        ["Dict"] = "Use native 'dict[K, V]' syntax instead of 'Dict[K, V]'",
        ["Set"] = "Use native 'set[X]' syntax instead of 'Set[X]'",
        ["Tuple"] = "Use native 'tuple[X, Y]' syntax instead of 'Tuple[X, Y]'",
        ["Union"] = "Sharpy does not support arbitrary union types. Use 'X?' for nullable values, or the 'union' keyword for tagged unions (algebraic data types)",
        ["Callable"] = "Use native '(X) -> Y' function type syntax instead of 'Callable[[X], Y]'",
        ["Any"] = "Sharpy is statically typed and does not support 'Any'. Use a concrete type or generic parameter 'T'",
        ["TypeVar"] = "Use generic type parameters directly: 'class Box[T]' or 'def identity[T](x: T) -> T'",
        ["Protocol"] = "Use 'interface' keyword instead of 'Protocol'",
        ["ClassVar"] = "Use class-level field declarations directly",
        ["Final"] = "Use the '@final' decorator instead of 'Final' type annotation",
        ["Self"] = "Use native 'Self' type annotation directly (no import needed)",
        ["TypeAlias"] = "Use 'type X = Y' syntax instead of 'TypeAlias'",
        ["TypeGuard"] = "Use 'x is T' type narrowing instead of 'TypeGuard'",
        ["NamedTuple"] = "Use 'type Point = tuple[x: float, y: float]' for named tuples",
        ["Literal"] = "Use string literal types directly (no import needed)",
    };

    private const string FallbackMessage =
        "The 'typing' module is not needed in Sharpy. Most typing constructs have native syntax equivalents.";

    public static string GetMessage(string name) =>
        Messages.TryGetValue(name, out var message) ? message : FallbackMessage;
}
