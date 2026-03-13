namespace Sharpy.Compiler.Text;

/// <summary>
/// Wraps a file path and defers reading the file until the source text is first accessed.
/// Useful for project files not open in the editor — avoids eager I/O during project initialization.
/// </summary>
public sealed class LazySourceText
{
    private readonly Lazy<SourceText> _inner;

    /// <summary>
    /// The file path of the source file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Whether the file has been read into memory.
    /// </summary>
    public bool IsLoaded => _inner.IsValueCreated;

    public LazySourceText(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _inner = new Lazy<SourceText>(() => SourceText.FromFile(filePath));
    }

    /// <summary>
    /// Forces loading the file and returns the concrete <see cref="SourceText"/>.
    /// Throws <see cref="FileNotFoundException"/> if the file does not exist.
    /// </summary>
    public SourceText Materialize() => _inner.Value;

    /// <summary>
    /// The total length of the source text in characters. Forces loading.
    /// </summary>
    public int Length => _inner.Value.Length;

    /// <summary>
    /// Returns a debug representation without forcing file I/O.
    /// Use <see cref="Materialize()"/> to get the actual source text content.
    /// </summary>
    public override string ToString() =>
        IsLoaded ? _inner.Value.ToString() : $"LazySourceText({FilePath}, not loaded)";
}
