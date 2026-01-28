namespace Sharpy.Core;

public sealed partial class Set<T>
{
    /// <summary>
    /// Returns a hash code for this set.
    /// </summary>
    /// <remarks>
    /// While Set inherits from Object, GetHashCode() is sealed and delegates to __Hash__().
    /// Once Set no longer inherits from Object, this will become:
    /// <code>public override int GetHashCode()</code>
    /// </remarks>
    public override int __Hash__()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(Set<T>).GetHashCode());
        hashCode.Add(_set.GetHashCode());

        return hashCode.ToHashCode();
    }
}
