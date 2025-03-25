namespace Sharpy
{
    public sealed partial class List<T>
    {
        public List<T> __Add__(List<T> other)
        {
            if (other is null)
            {
                throw new TypeError($"can only concatenate List<${typeof(T).Name}> (not \"NoneType\") to List<${typeof(T).Name}");
            }

            var res = Copy();
            res.Extend(other);

            return res;
        }

        public List<T> __RAdd__(List<T> other)
        {
            if (other is null)
            {
                throw new TypeError($"can only concatenate List<${typeof(T).Name}> (not \"NoneType\") to List<${typeof(T).Name}");
            }

            var res = other.Copy();
            res.Extend(this);

            return res;
        }

        public List<T> __IAdd__(List<T> other)
        {
            if (other is null)
            {
                throw new TypeError($"can only concatenate List<${typeof(T).Name}> (not \"NoneType\") to List<${typeof(T).Name}");
            }

            Extend(other);

            return this;
        }

        public List<T> __Mul__(int i)
        {
            var res = new List<T>();

            if (i <= 0)
            {
                return res;
            }

            res._list.EnsureCapacity(_list.Count * i);

            for (; i > 0; --i)
            {
                res.Extend(this);
            }

            return res;
        }

        public List<T> __IMul__(int i)
        {
            if (i <= 0)
            {
                Clear();

                return this;
            }

            var originalLength = _list.Count;
            _list.EnsureCapacity(originalLength * i);

            for (; i > 0; --i)
            {
                for (uint j = 0; j < originalLength; ++j)
                {
                    _list.Add(_list[(int)j]);
                }
            }

            return this;
        }

        public List<T> __RMul__(int i)
        {
            return __Mul__(i);
        }
    }
}
