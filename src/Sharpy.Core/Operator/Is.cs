using Sharpy.Core;
namespace Sharpy.Operator
{
    public static partial class Exports
    {
        public static bool Is(object left, object right)
        {
            return ReferenceEquals(left, right);
        }
    }
}
