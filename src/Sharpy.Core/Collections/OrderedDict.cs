using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    /// <summary>
    /// A dictionary that remembers insertion order.
    /// </summary>
    public class OrderedDict<TKey, TValue> where TKey : notnull
    {
        private readonly System.Collections.Generic.Dictionary<TKey, TValue> _dict;
        private readonly System.Collections.Generic.LinkedList<TKey> _order;
        private readonly System.Collections.Generic.Dictionary<TKey, LinkedListNode<TKey>> _nodes;

        public OrderedDict()
        {
            _dict = new System.Collections.Generic.Dictionary<TKey, TValue>();
            _order = new System.Collections.Generic.LinkedList<TKey>();
            _nodes = new System.Collections.Generic.Dictionary<TKey, LinkedListNode<TKey>>();
        }

        public TValue this[TKey key]
        {
            get
            {
                if (!_dict.TryGetValue(key, out TValue? value))
                {
                    throw new KeyError($"'{key}'");
                }

                return value;
            }
            set
            {
                if (!_dict.ContainsKey(key))
                {
                    var node = _order.AddLast(key);
                    _nodes[key] = node;
                }

                _dict[key] = value;
            }
        }

        public bool ContainsKey(TKey key) => _dict.ContainsKey(key);

        public int Count => _dict.Count;

        public IEnumerable<TKey> Keys => _order;

        public IEnumerable<TValue> Values => _order.Select(k => _dict[k]);

        public IEnumerable<(TKey, TValue)> Items => _order.Select(k => (k, _dict[k]));

        public bool Remove(TKey key)
        {
            if (!_dict.Remove(key))
            {
                return false;
            }

            var node = _nodes[key];
            _order.Remove(node);
            _nodes.Remove(key);
            return true;
        }

        /// <summary>
        /// Move an existing key to either end of the ordered dictionary.
        /// </summary>
        public void MoveToEnd(TKey key, bool last = true)
        {
            if (!_nodes.TryGetValue(key, out var node))
            {
                throw new KeyError($"'{key}'");
            }

            _order.Remove(node);
            if (last)
            {
                var newNode = _order.AddLast(key);
                _nodes[key] = newNode;
            }
            else
            {
                var newNode = _order.AddFirst(key);
                _nodes[key] = newNode;
            }
        }

        /// <summary>
        /// Remove and return a (key, value) pair. Pairs are returned in LIFO order if last is true
        /// or FIFO order if false.
        /// </summary>
        public (TKey, TValue) Popitem(bool last = true)
        {
            if (_dict.Count == 0)
            {
                throw new KeyError("dictionary is empty");
            }

            TKey key;
            if (last)
            {
                key = _order.Last!.Value;
                _order.RemoveLast();
            }
            else
            {
                key = _order.First!.Value;
                _order.RemoveFirst();
            }

            var value = _dict[key];
            _dict.Remove(key);
            _nodes.Remove(key);
            return (key, value);
        }

        public TValue Get(TKey key, TValue defaultValue = default!)
        {
            return _dict.TryGetValue(key, out TValue? value) ? value : defaultValue;
        }

        public void Clear()
        {
            _dict.Clear();
            _order.Clear();
            _nodes.Clear();
        }
    }
}
