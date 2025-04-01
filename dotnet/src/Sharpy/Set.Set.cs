namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public bool IsDisjoint(Set<T> other)
        {
            if (other is null)
            {
                // Not actually true here, but whatever
                throw new TypeError("'NoneType' object is not iterable");
            }

            return _set.Overlaps(other._set);
        }

        public Set<T> __And__(Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Set<T> __Or__(Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Set<T> __ROr__(Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Set<T> __RSub__(Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Set<T> __Sub__(Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Set<T> __XOr__(Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Collections.Interfaces.Set<T> __And__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Collections.Interfaces.Set<T> __Or__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Collections.Interfaces.Set<T> __Sub__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Collections.Interfaces.Set<T> __RSub__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Collections.Interfaces.Set<T> __XOr__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public Collections.Interfaces.Set<T> __ROr__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsDisjoint(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }
    }
}
