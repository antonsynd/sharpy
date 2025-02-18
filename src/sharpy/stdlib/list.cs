using System.Collections.Generic;
using System.Linq.Enumerable;

namespace Sharpy.Stdlib
{
    public sealed class List<T>
    {
        public List()
        {
        }

        public List(IEnumerable<T> iterable)
        {
            _list.AddRange(iterable)
        }

        public void Append(T x)
        {
            _list.Add(x);
        }

        public void Extend(IEnumerable<T> iterable)
        {
            _list.AddRange(iterable);
        }

        public void Insert(int i, T x)
        {
            _list.Insert(i, x);
        }

        public void Remove(T x)
        {
            _list.Remove(x);
        }

        public T Pop(int i)
        {
            return null;
        }

        public void Clear()
        {
            _list.Clear();
        }

        public bool Contains(T x)
        {
            return _list.Contains(x);
        }

        public int Index(T x, int start = 0, int end = -1)
        {
            start = _NormalizeIndex(start);
            readonly int count = _NormalizeIndex(end) - start;

            return _list.IndexOf(x, start, count);
        }

        public int Count(T x)
        {
            return _list.Count(y => x == y);
        }

        public int Len()
        {
            return _list.Count;
        }

        public void Sort(bool reverse = false, Func<T, bool> key = null)
        {
            if (key == null)
            {
                // not stable, use https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.orderby?view=net-9.0&redirectedfrom=MSDN#System_Linq_Enumerable_OrderBy__2_System_Collections_Generic_IEnumerable___0__System_Func___0___1__
                _list.Sort();
                _list.Reverse();
            }

            _list.Sort();
        }

        public void Reverse()
        {
            _list.Reverse();
        }

        public List<T> Copy()
        {
            var newList = new List<T>();
            newList._list.AddRange(_list);

            return newList;
        }

        public T this[int index]
        {
            index = _NormalizeIndex(index);
            return _list[index];
        }

        public List<T> this[int start, int end, int step = 1]()
        {
            var newList = new List<T>();
            start = _NormalizeIndex(start);
            end = _NormalizeIndex(end);
        }

public System.Collections.Generic.List<T> ToList()
        {
            return _list.Copy();
        }

        public static bool operator ==(List<T> other)
        {
            return _list == other._list;
        }

        public static bool operator !=(List<T> other)
        {
            return !(this == other);
        }

        public override bool Equals(object obj)
        {
            if (obj is List<T> other)
            {
                return this == other;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return _list.GetHashCode();
        }

        public override string ToString()
        {
            return _list.ToString();
        }

        private int _NormalizeIndex(int i)
        {
            if (i < 0)
            {
                return Len() + i;
            }

            return i;
        }

        private System.Collections.Generic.List<T> _list;
    }
}
