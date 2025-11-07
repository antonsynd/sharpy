namespace Sharpy;

public static partial class Exports
{
    /// <summary>
    /// Allows for code like <c>var opt = Some(5);</c> instead of
    /// <c>var opt = new Optional&lt;int&gt;(5);</c>.
    /// </summary>
    public static Optional<T> Some<T>(T value)
    {
        return new Optional<T>(value);
    }
}
