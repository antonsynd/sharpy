namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for mapping views over keys.
/// </summary>
public interface IKeysView<T> : IMappingView<T> where T : notnull
{
}
