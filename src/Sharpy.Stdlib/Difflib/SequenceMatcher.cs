using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    [SharpyModuleType("difflib", "SequenceMatcher")]
    public sealed class SequenceMatcher<T> where T : IEquatable<T>
    {
        private Func<T, bool>? _isJunk;
        private readonly bool _autoJunk;
        private IList<T> _a;
        private IList<T> _b;
        private Dictionary<T, System.Collections.Generic.List<int>>? _b2j;
        private HashSet<T>? _bJunk;
        private HashSet<T>? _bPopular;
        private System.Collections.Generic.List<(int a, int b, int size)>? _matchingBlocks;
        private System.Collections.Generic.List<(string tag, int i1, int i2, int j1, int j2)>? _opcodes;

        public SequenceMatcher(Func<T, bool>? isJunk, IList<T> a, IList<T> b, bool autoJunk = true)
        {
            _isJunk = isJunk;
            _autoJunk = autoJunk;
            _a = a ?? throw new ArgumentNullException(nameof(a));
            _b = b ?? throw new ArgumentNullException(nameof(b));
        }

        public void SetSeqs(IList<T> a, IList<T> b)
        {
            SetSeq1(a);
            SetSeq2(b);
        }

        public void SetSeq1(IList<T> a)
        {
            _a = a ?? throw new ArgumentNullException(nameof(a));
            _matchingBlocks = null;
            _opcodes = null;
        }

        public void SetSeq2(IList<T> b)
        {
            _b = b ?? throw new ArgumentNullException(nameof(b));
            _matchingBlocks = null;
            _opcodes = null;
            _b2j = null;
            _bJunk = null;
            _bPopular = null;
        }

        private void ChainB()
        {
            if (_b2j != null) return;

            int bCount = _b.Count;
            var b2j = new Dictionary<T, System.Collections.Generic.List<int>>();
            for (int i = 0; i < bCount; i++)
            {
                T elt = _b[i];
                if (!b2j.TryGetValue(elt, out var indices))
                {
                    indices = new System.Collections.Generic.List<int>();
                    b2j[elt] = indices;
                }
                indices.Add(i);
            }

            _bJunk = new HashSet<T>();
            if (_isJunk != null)
            {
                var keysToRemove = new System.Collections.Generic.List<T>();
                foreach (var elt in b2j.Keys)
                {
                    if (_isJunk(elt))
                    {
                        _bJunk.Add(elt);
                        keysToRemove.Add(elt);
                    }
                }
                foreach (var key in keysToRemove)
                    b2j.Remove(key);
            }

            _bPopular = new HashSet<T>();
            if (_autoJunk && bCount >= 200)
            {
                int ntest = bCount / 100 + 1;
                var keysToRemove = new System.Collections.Generic.List<T>();
                foreach (var kvp in b2j)
                {
                    if (kvp.Value.Count > ntest)
                    {
                        _bPopular.Add(kvp.Key);
                        keysToRemove.Add(kvp.Key);
                    }
                }
                foreach (var key in keysToRemove)
                    b2j.Remove(key);
            }

            _b2j = b2j;
        }

        public (int a, int b, int size) FindLongestMatch(int aLo, int aHi, int bLo, int bHi)
        {
            ChainB();
            int bestI = aLo, bestJ = bLo, bestSize = 0;

            var j2len = new Dictionary<int, int>();

            for (int i = aLo; i < aHi; i++)
            {
                var newJ2len = new Dictionary<int, int>();
                if (_b2j!.TryGetValue(_a[i], out var indices))
                {
                    foreach (int j in indices)
                    {
                        if (j < bLo) continue;
                        if (j >= bHi) break;
                        int k = (j2len.TryGetValue(j - 1, out int prev) ? prev : 0) + 1;
                        newJ2len[j] = k;
                        if (k > bestSize)
                        {
                            bestI = i - k + 1;
                            bestJ = j - k + 1;
                            bestSize = k;
                        }
                    }
                }
                j2len = newJ2len;
            }

            while (bestI > aLo && bestJ > bLo &&
                   !IsBJunk(_a[bestI - 1]) &&
                   _a[bestI - 1].Equals(_b[bestJ - 1]))
            {
                bestI--;
                bestJ--;
                bestSize++;
            }

            while (bestI + bestSize < aHi && bestJ + bestSize < bHi &&
                   !IsBJunk(_a[bestI + bestSize]) &&
                   _a[bestI + bestSize].Equals(_b[bestJ + bestSize]))
            {
                bestSize++;
            }

            while (bestI > aLo && bestJ > bLo &&
                   IsBJunk(_a[bestI - 1]) &&
                   _a[bestI - 1].Equals(_b[bestJ - 1]))
            {
                bestI--;
                bestJ--;
                bestSize++;
            }

            while (bestI + bestSize < aHi && bestJ + bestSize < bHi &&
                   IsBJunk(_a[bestI + bestSize]) &&
                   _a[bestI + bestSize].Equals(_b[bestJ + bestSize]))
            {
                bestSize++;
            }

            return (bestI, bestJ, bestSize);
        }

        private bool IsBJunk(T item) => _bJunk != null && _bJunk.Contains(item);

        public System.Collections.Generic.List<(int a, int b, int size)> GetMatchingBlocks()
        {
            if (_matchingBlocks != null) return _matchingBlocks;

            int aCount = _a.Count;
            int bCount = _b.Count;
            var matching = new System.Collections.Generic.List<(int a, int b, int size)>();
            var queue = new System.Collections.Generic.List<(int aLo, int aHi, int bLo, int bHi)>();
            queue.Add((0, aCount, 0, bCount));

            while (queue.Count > 0)
            {
                int last = queue.Count - 1;
                var item = queue[last];
                queue.RemoveAt(last);
                int aLo = item.aLo, aHi = item.aHi, bLo = item.bLo, bHi = item.bHi;
                var match = FindLongestMatch(aLo, aHi, bLo, bHi);
                if (match.size > 0)
                {
                    matching.Add(match);
                    if (aLo < match.a && bLo < match.b)
                        queue.Add((aLo, match.a, bLo, match.b));
                    if (match.a + match.size < aHi && match.b + match.size < bHi)
                        queue.Add((match.a + match.size, aHi, match.b + match.size, bHi));
                }
            }

            matching.Sort((x, y) =>
            {
                int cmp = x.a.CompareTo(y.a);
                if (cmp != 0) return cmp;
                cmp = x.b.CompareTo(y.b);
                if (cmp != 0) return cmp;
                return x.size.CompareTo(y.size);
            });

            var collapsed = new System.Collections.Generic.List<(int a, int b, int size)>();
            int ci = 0, cj = 0, ck = 0;
            foreach (var block in matching)
            {
                if (ci + ck == block.a && cj + ck == block.b)
                {
                    ck += block.size;
                }
                else
                {
                    if (ck > 0)
                        collapsed.Add((ci, cj, ck));
                    ci = block.a;
                    cj = block.b;
                    ck = block.size;
                }
            }
            if (ck > 0)
                collapsed.Add((ci, cj, ck));

            collapsed.Add((aCount, bCount, 0));
            _matchingBlocks = collapsed;
            return _matchingBlocks;
        }

        public System.Collections.Generic.List<(string tag, int i1, int i2, int j1, int j2)> GetOpcodes()
        {
            if (_opcodes != null) return _opcodes;

            int i = 0, j = 0;
            var opcodes = new System.Collections.Generic.List<(string tag, int i1, int i2, int j1, int j2)>();
            foreach (var block in GetMatchingBlocks())
            {
                string tag = "";
                if (i < block.a && j < block.b) tag = "replace";
                else if (i < block.a) tag = "delete";
                else if (j < block.b) tag = "insert";

                if (tag.Length > 0)
                    opcodes.Add((tag, i, block.a, j, block.b));

                i = block.a + block.size;
                j = block.b + block.size;
                if (block.size > 0)
                    opcodes.Add(("equal", block.a, i, block.b, j));
            }

            _opcodes = opcodes;
            return _opcodes;
        }

        public System.Collections.Generic.List<System.Collections.Generic.List<(string tag, int i1, int i2, int j1, int j2)>> GetGroupedOpcodes(int n = 3)
        {
            var codes = new System.Collections.Generic.List<(string tag, int i1, int i2, int j1, int j2)>(GetOpcodes());
            if (codes.Count == 0)
            {
                codes.Add(("equal", 0, 1, 0, 1));
            }

            if (codes[0].tag == "equal")
            {
                var c = codes[0];
                codes[0] = (c.tag, Math.Max(c.i1, c.i2 - n), c.i2, Math.Max(c.j1, c.j2 - n), c.j2);
            }

            int lastIdx = codes.Count - 1;
            if (codes[lastIdx].tag == "equal")
            {
                var c = codes[lastIdx];
                codes[lastIdx] = (c.tag, c.i1, Math.Min(c.i2, c.i1 + n), c.j1, Math.Min(c.j2, c.j1 + n));
            }

            int nn = n + n;
            var groups = new System.Collections.Generic.List<System.Collections.Generic.List<(string tag, int i1, int i2, int j1, int j2)>>();
            var group = new System.Collections.Generic.List<(string tag, int i1, int i2, int j1, int j2)>();

            foreach (var code in codes)
            {
                string tag = code.tag;
                int i1 = code.i1, i2 = code.i2, j1 = code.j1, j2 = code.j2;
                if (tag == "equal" && i2 - i1 > nn)
                {
                    group.Add((tag, i1, Math.Min(i2, i1 + n), j1, Math.Min(j2, j1 + n)));
                    groups.Add(group);
                    group = new System.Collections.Generic.List<(string tag, int i1, int i2, int j1, int j2)>();
                    i1 = Math.Max(i1, i2 - n);
                    j1 = Math.Max(j1, j2 - n);
                    group.Add((tag, i1, i2, j1, j2));
                }
                else
                {
                    group.Add(code);
                }
            }

            if (group.Count > 0 && !(group.Count == 1 && group[0].tag == "equal"))
                groups.Add(group);

            return groups;
        }

        public double Ratio()
        {
            int matches = 0;
            foreach (var block in GetMatchingBlocks())
                matches += block.size;
            int length = _a.Count + _b.Count;
            return length > 0 ? 2.0 * matches / length : 1.0;
        }

        public double QuickRatio()
        {
            var fullBCount = new Dictionary<T, int>();
            foreach (var elt in _b)
            {
                fullBCount.TryGetValue(elt, out int count);
                fullBCount[elt] = count + 1;
            }

            var avail = new Dictionary<T, int>(fullBCount);
            int matches = 0;
            foreach (var elt in _a)
            {
                if (avail.TryGetValue(elt, out int numb))
                {
                    if (numb > 0)
                    {
                        matches++;
                        avail[elt] = numb - 1;
                    }
                }
            }

            int length = _a.Count + _b.Count;
            return length > 0 ? 2.0 * matches / length : 1.0;
        }

        public double RealQuickRatio()
        {
            int la = _a.Count, lb = _b.Count;
            int length = la + lb;
            return length > 0 ? 2.0 * Math.Min(la, lb) / length : 1.0;
        }
    }
}
