using Sharpy.Core;
namespace Sharpy.Operator
{
    public static partial class Operator
    {
        public static bool IsNot(object left, object right)
        {
            return !ReferenceEquals(left, right);
        }
    }
}
