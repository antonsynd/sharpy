namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public bool __Eq__(Collections.Interfaces.Set<T> other)
        {
            uint numElems = 0;

            foreach (var elem in other)
            {
                if (!_set.Contains(elem))
                {
                    return false;
                }

                ++numElems;
            }

            return numElems == _set.Count;
        }

        public bool __Eq__(Set<T> other)
        {
            if (other is null)
            {
                return false;
            }

            return _set == other._set;
        }

        public override bool __Eq__(Object other)
        {
            if (other is Set<T> set)
            {
                return __Eq__(set);
            }

            return false;
        }
    }
}
