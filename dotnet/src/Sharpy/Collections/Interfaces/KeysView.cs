namespace Sharpy.Collections.Interfaces
{
    /// <summary>
    /// Interface for mapping views over keys.
    /// </summary>
    public interface KeysView<T> : MappingView<T> where T : notnull
    {
    }
}
