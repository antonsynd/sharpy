namespace Sharpy
{
    /// <summary>
    /// Interface for types that support shallow copying without reflection.
    /// Used by <see cref="CopyModule.Copy(object)"/> for type-safe dispatch.
    /// </summary>
    public interface IShallowCopyable
    {
        /// <summary>
        /// Return a shallow copy of this object.
        /// </summary>
        object ShallowCopy();
    }
}
