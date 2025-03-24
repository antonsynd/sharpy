namespace Sharpy
{
    public sealed partial class List<T>
    {
        public override bool __Eq__(Object obj)
        {
            if (obj is List<T> other)
            {
                return __Eq__(other);
            }

            return false;
        }

        public bool __Eq__(List<T> other) {
            if (other is null) {
                return false;
            }

            if (_list.Count == other._list.Count)
            {
                for (uint i = 0; i < _list.Count; ++i)
                {
                    var leftElem = _list[(int)i];
                    var rightElem = other._list[(int)i];

                    if (!EqualityAdapterFactory<T>.AreEqual(leftElem, rightElem))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
