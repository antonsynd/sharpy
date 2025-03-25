namespace Sharpy
{
    public sealed partial class List<T>
    {
        /// <summary>
        /// Returns whether the item is in the list.
        /// </summary>
        public bool __Contains__(T x)
        {
            return _list.Contains(x);
        }

        /// <summary>
        /// Returns whether the item is in the list.
        /// </summary>
        public bool Contains(T x)
        {
            return __Contains__(x);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (ReferenceEquals(obj, null))
            {
                return false;
            }

            throw new NotImplementedException();
        }
    }
}
