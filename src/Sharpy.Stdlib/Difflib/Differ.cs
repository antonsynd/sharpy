using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sharpy
{
    [SharpyModuleType("difflib", "Differ")]
    public sealed class Differ
    {
        private readonly Func<string, bool>? _lineJunk;
        private readonly Func<string, bool>? _charJunk;

        public Differ(Func<string, bool>? lineJunk = null, Func<string, bool>? charJunk = null)
        {
            _lineJunk = lineJunk;
            _charJunk = charJunk;
        }

        public IEnumerable<string> Compare(IList<string> a, IList<string> b)
        {
            var sm = new SequenceMatcher<string>(_lineJunk, a, b);
            foreach (var op in sm.GetOpcodes())
            {
                switch (op.tag)
                {
                    case "replace":
                        foreach (var line in FancyReplace(a, op.i1, op.i2, b, op.j1, op.j2))
                            yield return line;
                        break;
                    case "delete":
                        foreach (var line in Dump("-", a, op.i1, op.i2))
                            yield return line;
                        break;
                    case "insert":
                        foreach (var line in Dump("+", b, op.j1, op.j2))
                            yield return line;
                        break;
                    case "equal":
                        foreach (var line in Dump(" ", a, op.i1, op.i2))
                            yield return line;
                        break;
                }
            }
        }

        private static IEnumerable<string> Dump(string tag, IList<string> x, int lo, int hi)
        {
            for (int i = lo; i < hi; i++)
                yield return tag + " " + x[i];
        }

        private IEnumerable<string> FancyReplace(IList<string> a, int aLo, int aHi, IList<string> b, int bLo, int bHi)
        {
            double bestRatio = 0.74;
            int bestI = -1, bestJ = -1;
            double cutoff = 0.75;

            Func<char, bool>? charJunkChar = _charJunk != null
                ? (Func<char, bool>)(c => _charJunk(c.ToString()))
                : null;

            var sm = new SequenceMatcher<char>(charJunkChar, Array.Empty<char>(), Array.Empty<char>());

            int eqi = -1, eqj = -1;

            for (int j = bLo; j < bHi; j++)
            {
                char[] bLine = b[j].ToCharArray();
                sm.SetSeq2(bLine);
                for (int i = aLo; i < aHi; i++)
                {
                    if (a[i] == b[j])
                    {
                        if (eqi == -1)
                        {
                            eqi = i;
                            eqj = j;
                        }
                        continue;
                    }

                    sm.SetSeq1(a[i].ToCharArray());
                    if (sm.RealQuickRatio() > bestRatio &&
                        sm.QuickRatio() > bestRatio &&
                        sm.Ratio() > bestRatio)
                    {
                        bestRatio = sm.Ratio();
                        bestI = i;
                        bestJ = j;
                    }
                }
            }

            if (bestRatio < cutoff)
            {
                if (eqi == -1)
                {
                    foreach (var line in Dump("-", a, aLo, aHi))
                        yield return line;
                    foreach (var line in Dump("+", b, bLo, bHi))
                        yield return line;
                    yield break;
                }
                bestI = eqi;
                bestJ = eqj;
                bestRatio = 1.0;
            }

            foreach (var line in FancyHelper(a, aLo, bestI, b, bLo, bestJ))
                yield return line;

            string aElt = a[bestI];
            string bElt = b[bestJ];
            if (bestRatio < 1.0)
            {
                string atags;
                string btags;
                (atags, btags) = GetIntraLineChanges(aElt, bElt);
                yield return "- " + aElt;
                if (atags.Length > 0)
                    yield return "? " + atags + "\n";
                yield return "+ " + bElt;
                if (btags.Length > 0)
                    yield return "? " + btags + "\n";
            }
            else
            {
                yield return "  " + aElt;
            }

            foreach (var line in FancyHelper(a, bestI + 1, aHi, b, bestJ + 1, bHi))
                yield return line;
        }

        private IEnumerable<string> FancyHelper(IList<string> a, int aLo, int aHi, IList<string> b, int bLo, int bHi)
        {
            if (aLo < aHi)
            {
                if (bLo < bHi)
                    return FancyReplace(a, aLo, aHi, b, bLo, bHi);
                return Dump("-", a, aLo, aHi);
            }
            if (bLo < bHi)
                return Dump("+", b, bLo, bHi);
            return Enumerable.Empty<string>();
        }

        private (string atags, string btags) GetIntraLineChanges(string aLine, string bLine)
        {
            Func<char, bool>? charJunkChar = _charJunk != null
                ? (Func<char, bool>)(c => _charJunk(c.ToString()))
                : null;

            var sm = new SequenceMatcher<char>(charJunkChar, aLine.ToCharArray(), bLine.ToCharArray());
            var aBuilder = new StringBuilder();
            var bBuilder = new StringBuilder();

            foreach (var op in sm.GetOpcodes())
            {
                int la = op.i2 - op.i1;
                int lb = op.j2 - op.j1;
                switch (op.tag)
                {
                    case "replace":
                        aBuilder.Append('^', la);
                        bBuilder.Append('^', lb);
                        break;
                    case "delete":
                        aBuilder.Append('-', la);
                        break;
                    case "insert":
                        bBuilder.Append('+', lb);
                        break;
                    case "equal":
                        aBuilder.Append(' ', la);
                        bBuilder.Append(' ', lb);
                        break;
                }
            }

            return (aBuilder.ToString().TrimEnd(), bBuilder.ToString().TrimEnd());
        }
    }
}
