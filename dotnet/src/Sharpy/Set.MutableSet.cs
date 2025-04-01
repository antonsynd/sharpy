namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public void Add(T x)
        {
            _set.Add(x);
        }

        public void Discard(T x)
        {
            _set.Remove(x);
        }

        public void Clear()
        {
            _set.Clear();
        }

        public T Pop()
        {
            throw new NotImplementedException();
        }

        public void Remove(T x)
        {
            if (!_set.Remove(x))
            {
                throw new KeyError($"{x}");
            }
        }

        public void __IOr__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public void __IAnd__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public void __IXOr__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        public void __ISub__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }
    }
}
