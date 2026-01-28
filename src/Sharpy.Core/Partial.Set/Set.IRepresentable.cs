using System.Text;

namespace Sharpy.Core;

using static Sharpy.Core.Exports;

public sealed partial class Set<T>
{
    /// <summary>
    /// Returns a string representation of this set.
    /// </summary>
    /// <remarks>
    /// While Set inherits from Object, ToString() is sealed and delegates to __Str__(),
    /// which by default calls __Repr__(). Once Set no longer inherits from Object,
    /// this will become:
    /// <code>public override string ToString()</code>
    /// </remarks>
    public override string __Repr__()
    {
        var builder = new StringBuilder();
        builder.Append('{');

        int i = 1;
        var numElems = _set.Count;

        foreach (var item in _set)
        {
            builder.Append(Repr(item));

            if (i < numElems)
            {
                builder.Append(", ");
            }

            ++i;
        }

        builder.Append('}');

        return builder.ToString();
    }
}
