using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sharpy
{
    public static partial class DifflibModule
    {
        public static IEnumerable<string> UnifiedDiff(
            IList<string> a,
            IList<string> b,
            string fromFile = "",
            string toFile = "",
            string fromFileDate = "",
            string toFileDate = "",
            int n = 3,
            string lineterm = "\n")
        {
            var sm = new SequenceMatcher<string>(null, a, b);
            var groups = sm.GetGroupedOpcodes(n);
            if (groups.Count == 0)
                yield break;

            bool started = false;
            foreach (var group in groups)
            {
                if (!started)
                {
                    started = true;
                    string fromDate = fromFileDate.Length > 0 ? "\t" + fromFileDate : "";
                    string toDate = toFileDate.Length > 0 ? "\t" + toFileDate : "";
                    yield return "--- " + fromFile + fromDate + lineterm;
                    yield return "+++ " + toFile + toDate + lineterm;
                }

                var first = group[0];
                var last = group[group.Count - 1];
                int i1 = first.i1, i2 = last.i2;
                int j1 = first.j1, j2 = last.j2;

                string fromRange = FormatRangeUnified(i1, i2);
                string toRange = FormatRangeUnified(j1, j2);
                yield return $"@@ -{fromRange} +{toRange} @@{lineterm}";

                foreach (var op in group)
                {
                    if (op.tag == "equal")
                    {
                        for (int i = op.i1; i < op.i2; i++)
                            yield return " " + a[i];
                        continue;
                    }
                    if (op.tag == "replace" || op.tag == "delete")
                    {
                        for (int i = op.i1; i < op.i2; i++)
                            yield return "-" + a[i];
                    }
                    if (op.tag == "replace" || op.tag == "insert")
                    {
                        for (int j = op.j1; j < op.j2; j++)
                            yield return "+" + b[j];
                    }
                }
            }
        }

        public static IEnumerable<string> ContextDiff(
            IList<string> a,
            IList<string> b,
            string fromFile = "",
            string toFile = "",
            string fromFileDate = "",
            string toFileDate = "",
            int n = 3,
            string lineterm = "\n")
        {
            var sm = new SequenceMatcher<string>(null, a, b);
            var groups = sm.GetGroupedOpcodes(n);
            if (groups.Count == 0)
                yield break;

            bool started = false;
            foreach (var group in groups)
            {
                if (!started)
                {
                    started = true;
                    string fromDate = fromFileDate.Length > 0 ? "\t" + fromFileDate : "";
                    string toDate = toFileDate.Length > 0 ? "\t" + toFileDate : "";
                    yield return "*** " + fromFile + fromDate + lineterm;
                    yield return "--- " + toFile + toDate + lineterm;
                }

                var first = group[0];
                var last = group[group.Count - 1];

                yield return "***************" + lineterm;

                string fromRange = FormatRangeContext(first.i1, last.i2);
                yield return $"*** {fromRange} ****{lineterm}";

                bool hasFromContent = false;
                foreach (var op in group)
                {
                    if (op.tag == "replace" || op.tag == "delete")
                    {
                        hasFromContent = true;
                        break;
                    }
                }

                if (hasFromContent)
                {
                    foreach (var op in group)
                    {
                        string? prefix = op.tag switch
                        {
                            "equal" => "  ",
                            "replace" => "! ",
                            "delete" => "- ",
                            _ => null
                        };
                        if (prefix != null)
                        {
                            for (int i = op.i1; i < op.i2; i++)
                                yield return prefix + a[i];
                        }
                    }
                }

                string toRange = FormatRangeContext(first.j1, last.j2);
                yield return $"--- {toRange} ----{lineterm}";

                bool hasToContent = false;
                foreach (var op in group)
                {
                    if (op.tag == "replace" || op.tag == "insert")
                    {
                        hasToContent = true;
                        break;
                    }
                }

                if (hasToContent)
                {
                    foreach (var op in group)
                    {
                        string? prefix = op.tag switch
                        {
                            "equal" => "  ",
                            "replace" => "! ",
                            "insert" => "+ ",
                            _ => null
                        };
                        if (prefix != null)
                        {
                            for (int j = op.j1; j < op.j2; j++)
                                yield return prefix + b[j];
                        }
                    }
                }
            }
        }

        public static IEnumerable<string> Ndiff(
            IList<string> a,
            IList<string> b,
            Func<string, bool>? lineJunk = null,
            Func<string, bool>? charJunk = null)
        {
            return new Differ(lineJunk, charJunk).Compare(a, b);
        }

        public static System.Collections.Generic.List<string> GetCloseMatches(
            string word,
            IList<string> possibilities,
            int n = 3,
            double cutoff = 0.6)
        {
            if (n <= 0)
                throw new ValueError("n must be > 0: " + n);
            if (cutoff < 0.0 || cutoff > 1.0)
                throw new ValueError("cutoff must be in [0.0, 1.0]: " + cutoff);

            var result = new System.Collections.Generic.List<(double score, string word)>();
            var sm = new SequenceMatcher<char>(null, Array.Empty<char>(), word.ToCharArray());

            foreach (var x in possibilities)
            {
                sm.SetSeq1(x.ToCharArray());
                if (sm.RealQuickRatio() >= cutoff &&
                    sm.QuickRatio() >= cutoff &&
                    sm.Ratio() >= cutoff)
                {
                    result.Add((sm.Ratio(), x));
                }
            }

            result.Sort((a2, b2) =>
            {
                int cmp = b2.score.CompareTo(a2.score);
                return cmp != 0 ? cmp : string.Compare(a2.word, b2.word, StringComparison.Ordinal);
            });

            var output = new System.Collections.Generic.List<string>();
            int count = Math.Min(n, result.Count);
            for (int i = 0; i < count; i++)
                output.Add(result[i].word);
            return output;
        }

        private static readonly Regex LineJunkPattern = new Regex(
            @"^\s*(?:#\s*)?$",
            RegexOptions.Compiled);

        public static bool IsLineJunk(string line)
        {
            return LineJunkPattern.IsMatch(line);
        }

        public static bool IsCharacterJunk(string ch)
        {
            return ch == " " || ch == "\t";
        }

        public static IEnumerable<string> Restore(IEnumerable<string> delta, int which)
        {
            if (which != 1 && which != 2)
                throw new ValueError("which must be 1 or 2");

            string tag = which == 1 ? "- " : "+ ";
            foreach (var line in delta)
            {
                if (line.Length >= 2)
                {
                    string prefix = line.Substring(0, 2);
                    if (prefix == tag || prefix == "  ")
                        yield return line.Substring(2);
                }
            }
        }

        private static string FormatRangeUnified(int start, int stop)
        {
            int beginning = start + 1;
            int length = stop - start;
            if (length == 1)
                return beginning.ToString();
            if (length == 0)
                return $"{beginning},0";
            return $"{beginning},{length}";
        }

        private static string FormatRangeContext(int start, int stop)
        {
            int beginning = start + 1;
            int length = stop - start;
            if (length == 0)
                beginning--;
            if (length <= 1)
                return beginning.ToString();
            return $"{beginning},{beginning + length - 1}";
        }
    }
}
