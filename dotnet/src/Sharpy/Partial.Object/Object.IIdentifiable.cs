namespace Sharpy;

public abstract partial class Object
{
    /// <remarks>
    /// In Sharpy's reference implementation in C#, this returns the
    /// hashcode of the object by calling <see cref="object.GetHashCode()"/>
    /// by default (not by <see cref="__Hash__()"/>). This is because
    /// objects in C# are not guaranteed to have pinned memory addresses.
    /// </remarks>
    public virtual int __Id__()
    {
        return base.GetHashCode();
    }
}
