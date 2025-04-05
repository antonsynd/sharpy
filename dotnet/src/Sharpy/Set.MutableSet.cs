namespace Sharpy
{
    public sealed partial class Set<T>
    {
        /// <inheritdoc/>
        public void Add(T x)
        {
            _set.Add(x);
        }

        /// <inheritdoc/>
        public void Discard(T x)
        {
            _set.Remove(x);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _set.Clear();
        }

        /// <inheritdoc/>
        public T Pop()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Remove(T x)
        {
            if (!_set.Remove(x))
            {
                throw new KeyError($"{x}");
            }
        }

        /// <inheritdoc/>
        public void __IOr__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void __IAnd__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void __IXOr__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void __ISub__(Collections.Interfaces.Set<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void __IAnd__(Set<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void __IOr__(Set<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void __ISub__(Set<T> other)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void __IXOr__(Set<T> other)
        {
            throw new NotImplementedException();
        }
    }
}
