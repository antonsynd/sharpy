extern alias SharpyRT;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// The compiler's handle on the name-form classifier, which lives in Sharpy.Core with the forward
/// name rule it drives (<c>Sharpy.NameFormDetector</c>, #2040). Delegates only; the classification
/// is written once, in Core.
/// </summary>
internal static class NameFormDetector
{
    /// <inheritdoc cref="SharpyRT::Sharpy.NameFormDetector.Detect"/>
    public static SharpyRT::Sharpy.NameForm Detect(string nameBody)
        => SharpyRT::Sharpy.NameFormDetector.Detect(nameBody);

    /// <inheritdoc cref="SharpyRT::Sharpy.NameFormDetector.HasConsecutiveUnderscores"/>
    public static bool HasConsecutiveUnderscores(string nameBody)
        => SharpyRT::Sharpy.NameFormDetector.HasConsecutiveUnderscores(nameBody);

    /// <inheritdoc cref="SharpyRT::Sharpy.NameFormDetector.IsConstantCaseName"/>
    public static bool IsConstantCaseName(string name)
        => SharpyRT::Sharpy.NameFormDetector.IsConstantCaseName(name);
}
