namespace Sharpy;

public abstract partial class Object
{
    /// <remarks>
    /// Unlike Python where all base objects are implicitly truthy, Sharpy
    /// ones are not, to enforce custom implementations that are suitable
    /// for the given subclass.
    /// </remarks>
    public abstract bool __Bool__();
}
