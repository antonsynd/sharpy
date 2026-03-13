namespace Sharpy
{
    public static partial class Operator
    {
        /// <summary>Return true if left and right are the same object (identity check).</summary>
        public static bool Is(object left, object right)
        {
            return ReferenceEquals(left, right);
        }
    }
}
