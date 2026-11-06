namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Thrown by <c>SemanticInfo.MergeFrom</c> when a side-table key is present in both operands with
/// different values (#2443).
/// </summary>
/// <remarks>
/// The merge used to keep the first value and drop the second silently, so two analyses that
/// disagreed about one node produced whichever answer was merged first. A conflict is a compiler
/// bug, so this is always thrown, like <see cref="PhaseViolationException"/>.
/// </remarks>
public sealed class SemanticInfoMergeConflictException : InvalidOperationException
{
    /// <summary>The side-table field whose entries disagree (e.g. <c>_expressionTypes</c>).</summary>
    public string TableName { get; }

    /// <summary>The conflicting key: node kind and source position, or symbol kind and name.</summary>
    public string Key { get; }

    public SemanticInfoMergeConflictException(string tableName, string key, object? existing, object? incoming)
        : base($"SemanticInfo merge conflict in {tableName} at {key}: the target holds '{existing}' " +
               $"but the merged instance holds '{incoming}'. This is a compiler bug - two analyses " +
               "recorded different facts for one node (#2443). Please report this issue.")
    {
        TableName = tableName;
        Key = key;
    }
}
