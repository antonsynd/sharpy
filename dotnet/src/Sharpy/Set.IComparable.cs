namespace Sharpy
{
    public sealed partial class Set<T>
    {
        /// <inheritdoc/>
        public int CompareTo(Collections.Interfaces.Set<T>? other)
        {
            if (other is null)
            {
                return 1;
            }

            if (__Eq__(other))
            {
                return 0;
            }

            if (__Lt__(other))
            {
                return -1;
            }

            if (__Gt__(other))
            {
                return 1;
            }

            return 0;
        }


        public int CompareTo(Set<T>? other)
        {
            if (other is null)
            {
                return 1;
            }

            if (__Eq__(other))
            {
                return 0;
            }

            if (__Lt__(other))
            {
                return -1;
            }

            if (__Gt__(other))
            {
                return 1;
            }

            return 0;
        }
    }
}
