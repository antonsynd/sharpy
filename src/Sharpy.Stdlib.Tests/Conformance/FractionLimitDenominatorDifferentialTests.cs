using SCG = System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sharpy.Stdlib.Tests.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// The class guard for <c>Fraction.limit_denominator</c> (#2606): it returns what CPython's
/// <c>fractions.Fraction.limit_denominator</c> returns over a generated input space, never a hand list. ONE program
/// is compiled and run under Sharpy (the production path) and run under python3, and every printed line must be
/// equal. The program generates its own cells:
/// <list type="bullet">
/// <item>a dense grid: numerator −30 … 30 × denominator 1 … 30 × max_denominator 1 … 12, one line per
/// (numerator, denominator) with the twelve results joined (negatives, the early return when the reduced
/// denominator already fits, and incidental ties);</item>
/// <item>every exact tie: for each max_denominator m in 1 … 12, the midpoint of each pair of consecutive terms of the
/// Farey sequence F_m (walked by the next-term recurrence), shifted by each integer in −3 … 2. Such a midpoint is
/// equidistant from its best lower and upper approximations, so the tie rule alone decides it;</item>
/// <item>large values: <see cref="LargeValues"/> × <see cref="LargeMaxDenominators"/>, crossed here in C#;</item>
/// <item>the refusal: max_denominator 0 and −1 raise <c>ValueError</c> with CPython's message;</item>
/// <item>the early return yields an equal Fraction that is a new object, as CPython's <c>Fraction(self)</c>.</item>
/// </list>
/// A Sharpy run that crashes (an escaped CLR exception) is reported with the lines it did not print.
/// The oracle is CPython 3.15 (the repo's <c>.python-version</c>): an older python skips, except under
/// <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI), which fails.
/// </summary>
public class FractionLimitDenominatorDifferentialTests : StdlibIntegrationTestBase
{
    public FractionLimitDenominatorDifferentialTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    private const int NumeratorMin = -30;
    private const int NumeratorMax = 30;
    private const int DenominatorMax = 30;
    private const int MaxDenominatorMax = 12;
    private const int TieShiftMin = -3;
    private const int TieShiftMax = 2;

    // Values as Fraction(str) literals both languages parse alike: decimals, a long ratio, tiny and huge magnitudes.
    private static readonly string[] LargeValues =
    {
        "3.141592653589793", "-3.141592653589793", "2.718281828459045", "-2.718281828459045",
        "1.4142135623730951", "-0.000001234",
        "123456789012345678901234567890/987654321098765432109876543211",
        "-123456789012345678901234567890/987654321098765432109876543211",
        "-99999999999.5", "1e-30", "-1e-30", "7/18446744073709551617",
    };

    private static readonly string[] LargeMaxDenominators =
    {
        "1", "2", "3", "7", "10", "100", "113", "1000000", "1000000000000",
    };

    private static readonly string[] RefusedMaxDenominators = { "0", "-1" };

    private static string ProgramText()
    {
        var sb = new StringBuilder();
        sb.Append("from fractions import Fraction\n\n");
        sb.Append("def main():\n");
        sb.Append("    for n in range(").Append(Num(NumeratorMin)).Append(", ").Append(Num(NumeratorMax + 1)).Append("):\n");
        sb.Append("        for d in range(1, ").Append(Num(DenominatorMax + 1)).Append("):\n");
        sb.Append("            row: list[str] = []\n");
        sb.Append("            for m in range(1, ").Append(Num(MaxDenominatorMax + 1)).Append("):\n");
        sb.Append("                row.append(str(Fraction(n, d).limit_denominator(m)))\n");
        sb.Append("            print(\"grid\", n, d, \" \".join(row))\n");
        // Farey F_m from 0/1, 1/m: the term after a/b, c/e is (k*c - a)/(k*e - b) with k = (m + b) // e.
        sb.Append("    for m in range(1, ").Append(Num(MaxDenominatorMax + 1)).Append("):\n");
        sb.Append("        a = 0\n        b = 1\n        c = 1\n        e = m\n");
        sb.Append("        while True:\n");
        sb.Append("            for s in range(").Append(Num(TieShiftMin)).Append(", ").Append(Num(TieShiftMax + 1)).Append("):\n");
        sb.Append("                num = a * e + b * c + 2 * b * e * s\n");
        sb.Append("                den = 2 * b * e\n");
        sb.Append("                print(\"tie\", num, den, m, Fraction(num, den).limit_denominator(m))\n");
        sb.Append("            if c == 1 and e == 1:\n                break\n");
        sb.Append("            k = (m + b) // e\n");
        sb.Append("            a, b, c, e = c, e, k * c - a, k * e - b\n");
        foreach (var v in LargeValues)
            foreach (var m in LargeMaxDenominators)
                sb.Append("    print(\"large\", \"").Append(v).Append("\", ").Append(m)
                  .Append(", Fraction(\"").Append(v).Append("\").limit_denominator(").Append(m).Append("))\n");
        foreach (var m in RefusedMaxDenominators)
        {
            sb.Append("    try:\n");
            sb.Append("        print(\"refuse\", ").Append(m).Append(", Fraction(-1, 3).limit_denominator(").Append(m).Append("))\n");
            sb.Append("    except ValueError as ex:\n");
            sb.Append("        print(\"refuse\", ").Append(m).Append(", \"ValueError:\", ex)\n");
        }
        sb.Append("    f = Fraction(3, 7)\n");
        sb.Append("    g = f.limit_denominator(7)\n");
        sb.Append("    print(\"early\", g == f, g is f)\n");
        return sb.ToString();
    }

    private static string Num(int i) => i.ToString(CultureInfo.InvariantCulture);

    private static SCG.List<string> Lines(string stdout) =>
        stdout.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();

    [SkippableFact]
    public void LimitDenominator_GeneratedGrid_MatchesCPython()
    {
        var interpreter = PythonInterpreter.RequireOrSkip(15);
        var source = ProgramText();

        var script = SysPath.Combine(SysPath.GetTempPath(), $"sharpy-limit-denominator-{Guid.NewGuid():N}.py");
        File.WriteAllText(script, source + "\nmain()\n");
        SCG.List<string> python;
        try
        {
            python = interpreter.RunBatch(script, Array.Empty<string>());
        }
        finally
        {
            File.Delete(script);
        }

        int tiePairs = 0;
        for (int m = 1; m <= MaxDenominatorMax; m++)
            tiePairs += FareyLength(m) - 1;
        int gridCells = (NumeratorMax - NumeratorMin + 1) * DenominatorMax * MaxDenominatorMax;
        int tieCells = tiePairs * (TieShiftMax - TieShiftMin + 1);
        int largeCells = LargeValues.Length * LargeMaxDenominators.Length;
        int expectedLines = gridCells / MaxDenominatorMax + tieCells + largeCells + RefusedMaxDenominators.Length + 1;
        Assert.Equal(expectedLines, python.Count);
        Output.WriteLine($"{gridCells} grid + {tieCells} tie + {largeCells} large + {RefusedMaxDenominators.Length} refusal"
            + " + 1 early-return cells");

        // Positive controls: the space reaches the refusal, a positive and a negative tie, and the early-return copy.
        Assert.Contains("refuse 0 ValueError: max_denominator should be at least 1", python);
        Assert.Contains("tie 1 2 1 0", python);
        Assert.Contains(python, l => l.StartsWith("grid -1 2 -1 ", StringComparison.Ordinal));
        Assert.Contains("early True False", python);

        var run = CompileAndExecute(source, "fraction_limit_denominator.spy", executionTimeoutMs: 120_000);
        var sharpy = Lines(run.StandardOutput);

        var divergences = new SCG.List<string>();
        for (int i = 0; i < python.Count; i++)
        {
            var actual = i < sharpy.Count ? sharpy[i] : "<no line>";
            if (!string.Equals(python[i], actual, StringComparison.Ordinal))
                divergences.Add($"  line {i}: python `{python[i]}`, sharpy `{actual}`");
        }
        if (sharpy.Count > python.Count)
            divergences.Add($"  sharpy printed {sharpy.Count - python.Count} extra line(s)");

        bool completed = RanToCompletion(run);
        Assert.True(divergences.Count == 0 && completed,
            $"{divergences.Count} of {python.Count} limit_denominator lines diverge from CPython "
            + $"(sharpy printed {sharpy.Count}):\n" + string.Join("\n", divergences.Take(40))
            + (completed ? "" : "\n" + (JudgeRun(run, source, "Sharpy limit_denominator program") ?? "timed out")));
    }

    /// <summary>|F_m| = 1 + Σ_{k=1..m} φ(k): the number of terms of the Farey sequence of order m in [0, 1].</summary>
    private static int FareyLength(int m)
    {
        int length = 1;
        for (int k = 1; k <= m; k++)
        {
            int phi = 0;
            for (int j = 1; j <= k; j++)
            {
                if (Gcd(j, k) == 1)
                    phi++;
            }
            length += phi;
        }
        return length;
    }

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
}
