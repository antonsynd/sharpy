using System.Text;

namespace Sharpy;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public override string __Repr__()
    {
        var builder = new StringBuilder();
        builder.Append('[');

        uint i = 1;
        var numElems = _list.Count;

        foreach (var item in _list)
        {
            builder.Append(Repr(item));

            if (i < numElems)
            {
                builder.Append(", ");
            }

            ++i;
        }

        builder.Append(']');

        return builder.ToString();
    }
}
