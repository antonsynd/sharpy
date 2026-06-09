using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    using static Builtins;

    public sealed partial class Dict<K, V> : IDict
    {
        object? IDict.this[object key]
        {
            get
            {
                if (key is K typedKey)
                {
                    if (_dict.TryGetValue(typedKey, out V? value))
                    {
                        return value;
                    }

                    throw new KeyError(Repr(key));
                }

                // Wrong-typed key cannot be present — raise KeyError (matches Python)
                throw new KeyError(Repr(key));
            }
            set
            {
                _dict[(K)key] = (V)value!;
            }
        }

        IEnumerable<(object?, object?)> IDict.Items()
        {
            foreach (var kvp in _dict)
            {
                yield return (kvp.Key, kvp.Value);
            }
        }

        IEnumerable<object?> IDict.Keys()
        {
            foreach (var key in _dict.Keys)
            {
                yield return key;
            }
        }

        IEnumerable<object?> IDict.Values()
        {
            foreach (var value in _dict.Values)
            {
                yield return value;
            }
        }

        bool IDict.Contains(object key)
        {
            return key is K typedKey && _dict.ContainsKey(typedKey);
        }

        Optional<object?> IDict.Get(object key)
        {
            if (key is K typedKey && _dict.TryGetValue(typedKey, out V? value))
            {
                return Optional<object?>.Some(value);
            }

            return Optional<object?>.None;
        }

        object? IDict.Get(object key, object? defaultValue)
        {
            if (key is K typedKey && _dict.TryGetValue(typedKey, out V? value))
            {
                return value;
            }

            return defaultValue;
        }

        object? IDict.Pop(object key)
        {
            if (key is K typedKey)
            {
                if (_dict.TryGetValue(typedKey, out V? value))
                {
                    _dict.Remove(typedKey);
                    return value;
                }

                throw new KeyError(Repr(key));
            }

            throw new KeyError(Repr(key));
        }

        object? IDict.Pop(object key, object? defaultValue)
        {
            if (key is K typedKey && _dict.TryGetValue(typedKey, out V? value))
            {
                _dict.Remove(typedKey);
                return value;
            }

            return defaultValue;
        }

        (object?, object?) IDict.PopItem(bool last)
        {
            var pair = last ? _dict.Last() : _dict.First();
            _dict.Remove(pair.Key);
            return (pair.Key, pair.Value);
        }

        object? IDict.SetDefault(object key, object? defaultValue)
        {
            var typedKey = (K)key;

            if (_dict.TryGetValue(typedKey, out V? value))
            {
                return value;
            }

            var typedValue = (V)defaultValue!;
            _dict[typedKey] = typedValue;
            return typedValue;
        }

        void IDict.Clear()
        {
            _dict.Clear();
        }

        IDict IDict.Copy()
        {
            return Copy();
        }

        void IDict.Update(IDict other)
        {
            foreach (var (k, v) in other.Items())
            {
                _dict[(K)k!] = (V)v!;
            }
        }

        void IDict.Update(IEnumerable<(object?, object?)> other)
        {
            foreach (var (k, v) in other)
            {
                _dict[(K)k!] = (V)v!;
            }
        }

        void IDict.Remove(object key)
        {
            if (key is K typedKey)
            {
                if (!_dict.Remove(typedKey))
                {
                    throw new KeyError(Repr(key));
                }

                return;
            }

            throw new KeyError(Repr(key));
        }
    }
}
