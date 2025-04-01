namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public bool __Eq__(Set<T> other)
        {
            if (other is null)
            {
                return false;
            }

            return _set.SetEquals(other._set);
        }

        public bool __Eq__(Collections.Interfaces.Set<T> other)
        {
            uint numElems = 0;

            foreach (var x in other)
            {
                if (_set.Contains(x))
                {
                    numElems++;
                }
            }

            return numElems == _set.Count;
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
