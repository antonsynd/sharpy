namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public bool Equals(Collections.Interfaces.Set<T>? other)
        {
            if (other is null)
            {
                return false;
            }

            return __Eq__(other);
        }
    }
}
