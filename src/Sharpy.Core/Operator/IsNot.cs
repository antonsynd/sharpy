namespace Sharpy
{
    public static partial class Operator
    {
        /// <summary>Return true if left and right are not the same object (identity check).</summary>
        public static bool IsNot(object left, object right)
        {
            return !ReferenceEquals(left, right);
        }
    }
}
