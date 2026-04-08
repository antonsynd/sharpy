using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Search and manipulation methods for Bytes.
    /// </summary>
    public readonly partial struct Bytes
    {
        /// <summary>
        /// Return the lowest index where subsequence sub is found.
        /// Returns -1 if sub is not found.
        /// </summary>
        public int Find(Bytes sub)
        {
            return FindSubsequence(_data, sub._data, 0, _data.Length);
        }

        /// <summary>
        /// Return the highest index where subsequence sub is found.
        /// Returns -1 if sub is not found.
        /// </summary>
        public int Rfind(Bytes sub)
        {
            return RfindSubsequence(_data, sub._data, 0, _data.Length);
        }

        /// <summary>
        /// Return bytes with all occurrences of old replaced by new.
        /// </summary>
        public Bytes Replace(Bytes old_, Bytes new_, int count = -1)
        {
            if (old_._data.Length == 0)
            {
                return ReplaceEmpty(new_, count);
            }

            var result = new System.Collections.Generic.List<byte>();
            int i = 0;
            int replacements = 0;

            while (i < _data.Length)
            {
                if (count >= 0 && replacements >= count)
                {
                    result.Add(_data[i]);
                    i++;
                    continue;
                }

                int matchPos = FindSubsequence(_data, old_._data, i, _data.Length);
                if (matchPos < 0 || matchPos \!= i)
                {
                    result.Add(_data[i]);
                    i++;
                }
                else
                {
                    for (int j = 0; j < new_._data.Length; j++)
                    {
                        result.Add(new_._data[j]);
                    }
                    i += old_._data.Length;
                    replacements++;
                }
            }

            return new Bytes(result.ToArray(), true);
        }

        private Bytes ReplaceEmpty(Bytes new_, int count)
        {
            var result = new System.Collections.Generic.List<byte>();
            int replacements = 0;

            for (int i = 0; i <= _data.Length; i++)
            {
                if (count < 0 || replacements < count)
                {
                    for (int j = 0; j < new_._data.Length; j++)
                    {
                        result.Add(new_._data[j]);
                    }
                    replacements++;
                }

                if (i < _data.Length)
                {
                    result.Add(_data[i]);
                }
            }

            return new Bytes(result.ToArray(), true);
        }

        /// <summary>Return True if the bytes starts with the specified prefix.</summary>
        public bool Startswith(Bytes prefix)
        {
            if (prefix._data.Length > _data.Length) return false;
            for (int i = 0; i < prefix._data.Length; i++)
            {
                if (_data[i] \!= prefix._data[i]) return false;
            }
            return true;
        }

        /// <summary>Return True if the bytes ends with the specified suffix.</summary>
        public bool Endswith(Bytes suffix)
        {
            if (suffix._data.Length > _data.Length) return false;
            int offset = _data.Length - suffix._data.Length;
            for (int i = 0; i < suffix._data.Length; i++)
            {
                if (_data[offset + i] \!= suffix._data[i]) return false;
            }
            return true;
        }

        /// <summary>Return the number of non-overlapping occurrences of sub.</summary>
        public int Count(Bytes sub)
        {
            if (sub._data.Length == 0) return _data.Length + 1;
            int count = 0;
            int pos = 0;
            while (pos <= _data.Length - sub._data.Length)
            {
                int found = FindSubsequence(_data, sub._data, pos, _data.Length);
                if (found < 0) break;
                count++;
                pos = found + sub._data.Length;
            }
            return count;
        }

        /// <summary>Split the bytes at the given separator.</summary>
        public List<Bytes> Split(Bytes sep = default)
        {
            var result = new List<Bytes>();
            if (sep._data == null || sep._data.Length == 0)
            {
                SplitWhitespace(result);
                return result;
            }
            int start = 0;
            while (start <= _data.Length)
            {
                int found = FindSubsequence(_data, sep._data, start, _data.Length);
                if (found < 0)
                {
                    var remaining = new byte[_data.Length - start];
                    Array.Copy(_data, start, remaining, 0, remaining.Length);
                    result.Add(new Bytes(remaining, true));
                    break;
                }
                var segment = new byte[found - start];
                Array.Copy(_data, start, segment, 0, segment.Length);
                result.Add(new Bytes(segment, true));
                start = found + sep._data.Length;
            }
            return result;
        }

        private void SplitWhitespace(List<Bytes> result)
        {
            int i = 0;
            while (i < _data.Length)
            {
                while (i < _data.Length && IsWhitespace(_data[i])) i++;
                if (i >= _data.Length) break;
                int start = i;
                while (i < _data.Length && \!IsWhitespace(_data[i])) i++;
                var segment = new byte[i - start];
                Array.Copy(_data, start, segment, 0, segment.Length);
                result.Add(new Bytes(segment, true));
            }
        }

        /// <summary>Concatenate bytes sequences from an iterable, separated by this Bytes.</summary>
        public Bytes Join(IEnumerable<Bytes> iterable)
        {
            if (iterable == null) throw new TypeError("can only join an iterable");
            var result = new System.Collections.Generic.List<byte>();
            bool first = true;
            foreach (var item in iterable)
            {
                if (\!first)
                {
                    for (int i = 0; i < _data.Length; i++) result.Add(_data[i]);
                }
                first = false;
                for (int i = 0; i < item._data.Length; i++) result.Add(item._data[i]);
            }
            return new Bytes(result.ToArray(), true);
        }

        /// <summary>Return bytes with leading and trailing whitespace removed.</summary>
        public Bytes Strip()
        {
            int start = 0;
            while (start < _data.Length && IsWhitespace(_data[start])) start++;
            int end = _data.Length;
            while (end > start && IsWhitespace(_data[end - 1])) end--;
            if (start == 0 && end == _data.Length) return this;
            var result = new byte[end - start];
            Array.Copy(_data, start, result, 0, result.Length);
            return new Bytes(result, true);
        }

        /// <summary>Return bytes with leading whitespace removed.</summary>
        public Bytes Lstrip()
        {
            int start = 0;
            while (start < _data.Length && IsWhitespace(_data[start])) start++;
            if (start == 0) return this;
            var result = new byte[_data.Length - start];
            Array.Copy(_data, start, result, 0, result.Length);
            return new Bytes(result, true);
        }

        /// <summary>Return bytes with trailing whitespace removed.</summary>
        public Bytes Rstrip()
        {
            int end = _data.Length;
            while (end > 0 && IsWhitespace(_data[end - 1])) end--;
            if (end == _data.Length) return this;
            var result = new byte[end];
            Array.Copy(_data, 0, result, 0, end);
            return new Bytes(result, true);
        }

        /// <summary>Return a copy with ASCII lowercase converted to uppercase.</summary>
        public Bytes Upper()
        {
            var result = new byte[_data.Length];
            for (int i = 0; i < _data.Length; i++)
            {
                byte b = _data[i];
                result[i] = (b >= (byte)'a' && b <= (byte)'z') ? (byte)(b - 32) : b;
            }
            return new Bytes(result, true);
        }

        /// <summary>Return a copy with ASCII uppercase converted to lowercase.</summary>
        public Bytes Lower()
        {
            var result = new byte[_data.Length];
            for (int i = 0; i < _data.Length; i++)
            {
                byte b = _data[i];
                result[i] = (b >= (byte)'A' && b <= (byte)'Z') ? (byte)(b + 32) : b;
            }
            return new Bytes(result, true);
        }

        #region Private Helpers

        private static bool IsWhitespace(byte b)
        {
            return b == 0x20 || b == 0x09 || b == 0x0A || b == 0x0B || b == 0x0C || b == 0x0D;
        }

        private static int FindSubsequence(byte[] haystack, byte[] needle, int start, int end)
        {
            if (needle.Length == 0) return start;
            int limit = end - needle.Length;
            for (int i = start; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] \!= needle[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        private static int RfindSubsequence(byte[] haystack, byte[] needle, int start, int end)
        {
            if (needle.Length == 0) return end;
            int limit = end - needle.Length;
            for (int i = limit; i >= start; i--)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] \!= needle[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        #endregion
    }
}
