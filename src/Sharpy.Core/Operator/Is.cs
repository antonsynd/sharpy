using Sharpy.Core;
namespace Sharpy.Operator
{
    public static partial class Operator
    {
        public static bool Is(object left, object right)
        {
            return ReferenceEquals(left, right);
        }
    }
}
