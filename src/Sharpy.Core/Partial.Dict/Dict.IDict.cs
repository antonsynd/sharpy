using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    using static Builtins;

    public sealed partial class Dict<K, V> : IDict
    {
        private bool IsNullIKey(object key) => key is null;

        object? IDict.this[object key]
        {
            get
            {
                if (IsNullIKey(key))
                {
                    if (_hasNullKey) return _nullValue;
                    throw new KeyError(Repr(key));
                }

                if (key is K typedKey)
                {
                    if (_dict.TryGetValue(typedKey, out V? value))
                    {
                        return value;
                    }

                    throw new KeyError(Repr(key));
                }

                throw new KeyError(Repr(key));
            }
            set
            {
                if (IsNullIKey(key))
                {
                    if (!_hasNullKey) _nullOrdinal = _dict.Count;
                    _nullValue = (V)value!;
                    _hasNullKey = true;
                    return;
                }

                _dict[(K)key] = (V)value!;
            }
        }

        IEnumerable<(object?, object?)> IDict.Items()
        {
            int dictIndex = 0;
            foreach (var kvp in _dict)
            {
                if (_hasNullKey && dictIndex == _nullOrdinal)
                {
                    yield return (default(K), (object?)_nullValue);
                }

                yield return (kvp.Key, kvp.Value);
                dictIndex++;
            }

            if (_hasNullKey && _nullOrdinal >= _dict.Count)
            {
                yield return (default(K), (object?)_nullValue);
            }
        }

        IEnumerable<object?> IDict.Keys()
        {
            int dictIndex = 0;
            foreach (var key in _dict.Keys)
            {
                if (_hasNullKey && dictIndex == _nullOrdinal)
                {
                    yield return default(K);
                }

                yield return key;
                dictIndex++;
            }

            if (_hasNullKey && _nullOrdinal >= _dict.Count)
            {
                yield return default(K);
            }
        }

        IEnumerable<object?> IDict.Values()
        {
            int dictIndex = 0;
            foreach (var kvp in _dict)
            {
                if (_hasNullKey && dictIndex == _nullOrdinal)
                {
                    yield return _nullValue;
                }

                yield return kvp.Value;
                dictIndex++;
            }

            if (_hasNullKey && _nullOrdinal >= _dict.Count)
            {
                yield return _nullValue;
            }
        }

        bool IDict.Contains(object key)
        {
            if (IsNullIKey(key)) return _hasNullKey;
            return key is K typedKey && _dict.ContainsKey(typedKey);
        }

        Optional<object?> IDict.Get(object key)
        {
            if (IsNullIKey(key))
            {
                return _hasNullKey ? Optional<object?>.Some((object?)_nullValue) : Optional<object?>.None;
            }

            if (key is K typedKey && _dict.TryGetValue(typedKey, out V? value))
            {
                return Optional<object?>.Some(value);
            }

            return Optional<object?>.None;
        }

        object? IDict.Get(object key, object? defaultValue)
        {
            if (IsNullIKey(key))
            {
                return _hasNullKey ? (object?)_nullValue : defaultValue;
            }

            if (key is K typedKey && _dict.TryGetValue(typedKey, out V? value))
            {
                return value;
            }

            return defaultValue;
        }

        object? IDict.Pop(object key)
        {
            if (IsNullIKey(key))
            {
                if (_hasNullKey)
                {
                    V val = _nullValue;
                    _hasNullKey = false;
                    _nullValue = default!;
                    return val;
                }

                throw new KeyError(Repr(key));
            }

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
            if (IsNullIKey(key))
            {
                if (_hasNullKey)
                {
                    V val = _nullValue;
                    _hasNullKey = false;
                    _nullValue = default!;
                    return val;
                }

                return defaultValue;
            }

            if (key is K typedKey && _dict.TryGetValue(typedKey, out V? value))
            {
                _dict.Remove(typedKey);
                return value;
            }

            return defaultValue;
        }

        (object?, object?) IDict.PopItem(bool last)
        {
            var (k, v) = PopItem(last);
            return (k, v);
        }

        object? IDict.SetDefault(object key, object? defaultValue)
        {
            if (IsNullIKey(key))
            {
                if (_hasNullKey) return _nullValue;

                var typedValue = (V)defaultValue!;
                _nullOrdinal = _dict.Count;
                _nullValue = typedValue;
                _hasNullKey = true;
                return typedValue;
            }

            var typedKey = (K)key;

            if (_dict.TryGetValue(typedKey, out V? value))
            {
                return value;
            }

            var tv = (V)defaultValue!;
            _dict[typedKey] = tv;
            return tv;
        }

        void IDict.Clear()
        {
            Clear();
        }

        IDict IDict.Copy()
        {
            return Copy();
        }

        void IDict.Update(IDict other)
        {
            foreach (var (k, v) in other.Items())
            {
                if (k is null)
                {
                    if (!_hasNullKey) _nullOrdinal = _dict.Count;
                    _nullValue = (V)v!;
                    _hasNullKey = true;
                }
                else
                {
                    _dict[(K)k] = (V)v!;
                }
            }
        }

        void IDict.Update(IEnumerable<(object?, object?)> other)
        {
            foreach (var (k, v) in other)
            {
                if (k is null)
                {
                    if (!_hasNullKey) _nullOrdinal = _dict.Count;
                    _nullValue = (V)v!;
                    _hasNullKey = true;
                }
                else
                {
                    _dict[(K)k] = (V)v!;
                }
            }
        }

        void IDict.Remove(object key)
        {
            if (IsNullIKey(key))
            {
                if (!_hasNullKey)
                {
                    throw new KeyError(Repr(key));
                }

                _hasNullKey = false;
                _nullValue = default!;
                return;
            }

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
