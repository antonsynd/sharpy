using System.Globalization;
using System.Text;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>
/// The class guard for <c>round()</c> (#2460): <c>round(x)</c> and <c>round(x, n)</c> behave as CPython's
/// <c>float.__round__</c> / <c>int.__round__</c>. Two axes are crossed in code, never written out cell by cell: a
/// float value axis (NaN, ±inf, the ±.5 ties, values whose decimal text is not their binary value, magnitudes past
/// <c>int</c>) × digits {absent, −3 … 20}, and an int value axis × digits {absent, −3 … 2}. Every cell becomes one
/// <c>try: print(id, round(v, d)) except ValueError / OverflowError</c> statement of ONE program, compiled and run
/// once under Sharpy (the production path) and once under python3, and each line is compared: the printed result,
/// or the exception type by name.
/// </summary>
/// <remarks>
/// <para>One documented deviation is generated, not listed: <c>int</c> is <c>System.Int32</c> (Axiom 1), so a
/// one-argument <c>round(float)</c> whose exact python result lies outside the 32-bit range is expected to raise
/// <c>OverflowError</c> (the refusal <c>int(float)</c> makes, R-IS), never a wrapped value.</para>
/// <para>Skips when no python3 ≥ 3.12 answers, except under <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI), which fails.</para>
/// </remarks>
public class RoundDifferentialTests : IntegrationTestBase
{
    public RoundDifferentialTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    private const string OverflowErrorName = "OverflowError";
    private const string ValueErrorName = "ValueError";

    // The float value axis, as source text both languages read the same way.
    private static readonly string[] FloatValues =
    {
        "float(\"nan\")", "float(\"inf\")", "-float(\"inf\")",
        "0.5", "1.5", "2.5", "-0.5", "-1.5",
        "1e10", "2.675", "0.125", "0.375", "1e300", "2.0 ** 62", "123.456",
    };

    // The int value axis; 2 ** 40 is an int64 in Sharpy.
    private static readonly string[] IntValues = { "5", "15", "25", "-25", "2 ** 40" };

    private const int FloatDigitsMin = -3;
    private const int FloatDigitsMax = 20;
    private const int IntDigitsMin = -3;
    private const int IntDigitsMax = 2;

    private sealed record Cell(int Id, string Value, int? Digits, bool IntAxis)
    {
        public string Call => Digits is int d
            ? "round(" + Value + ", " + d.ToString(CultureInfo.InvariantCulture) + ")"
            : "round(" + Value + ")";
    }

    private static List<Cell> GenerateCells()
    {
        var cells = new List<Cell>();
        void Cross(string[] values, int min, int max, bool intAxis)
        {
            var digits = new int?[] { null }.Concat(Enumerable.Range(min, max - min + 1).Select(d => (int?)d)).ToList();
            foreach (var v in values)
                foreach (var d in digits)
                    cells.Add(new Cell(cells.Count, v, d, intAxis));
        }
        Cross(FloatValues, FloatDigitsMin, FloatDigitsMax, intAxis: false);
        Cross(IntValues, IntDigitsMin, IntDigitsMax, intAxis: true);
        return cells;
    }

    private static string ProgramFor(IEnumerable<Cell> cells)
    {
        var sb = new StringBuilder("def main():\n");
        foreach (var c in cells)
        {
            var id = c.Id.ToString(CultureInfo.InvariantCulture);
            sb.Append("    try:\n");
            sb.Append("        print(").Append(id).Append(", ").Append(c.Call).Append(")\n");
            sb.Append("    except ValueError:\n");
            sb.Append("        print(").Append(id).Append(", \"").Append(ValueErrorName).Append("\")\n");
            sb.Append("    except OverflowError:\n");
            sb.Append("        print(").Append(id).Append(", \"").Append(OverflowErrorName).Append("\")\n");
        }
        return sb.ToString();
    }

    private static Dictionary<int, string> ParseLines(string stdout)
    {
        var byId = new Dictionary<int, string>();
        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
                continue;
            var space = line.IndexOf(' ');
            if (space > 0 && int.TryParse(line[..space], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                byId[id] = line[(space + 1)..];
        }
        return byId;
    }

    /// <summary>
    /// What Sharpy must print for a cell, from python's line: python's own text, except the one generated deviation
    /// (a one-argument float round whose integer result does not fit <c>System.Int32</c> raises <c>OverflowError</c>).
    /// </summary>
    private static string ExpectedSharpy(Cell cell, string python)
    {
        if (!cell.IntAxis && cell.Digits is null
            && System.Numerics.BigInteger.TryParse(python, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var exact)
            && (exact < int.MinValue || exact > int.MaxValue))
        {
            return OverflowErrorName;
        }
        return python;
    }

    [SkippableFact]
    public void Round_DigitsByValueMatrix_MatchesCPython()
    {
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 12));

        var cells = GenerateCells();
        int expectedCount = FloatValues.Length * (FloatDigitsMax - FloatDigitsMin + 2)
            + IntValues.Length * (IntDigitsMax - IntDigitsMin + 2);
        Assert.Equal(expectedCount, cells.Count);

        var source = ProgramFor(cells);

        var py = oracle.RunBatch(new[] { new PyRequest(0, source + "\nmain()\n") });
        Assert.True(py.TryGetValue(0, out var pyResult) && pyResult.Ok,
            "python3 did not run the round matrix: " + (pyResult?.Stderr ?? "no verdict"));
        var pythonById = ParseLines(pyResult!.Stdout);
        Assert.Equal(cells.Count, pythonById.Count);

        var run = CompileAndExecute(source, "round_matrix.spy", executionTimeoutMs: 60_000);
        Assert.True(RanToCompletion(run), JudgeRun(run, source, "Sharpy round matrix") ?? "timed out");
        var sharpyById = ParseLines(run.StandardOutput);

        var divergences = new List<string>();
        int deviationCells = 0;
        foreach (var cell in cells)
        {
            var python = pythonById[cell.Id];
            var expected = ExpectedSharpy(cell, python);
            if (!ReferenceEquals(expected, python))
                deviationCells++;
            var actual = sharpyById.TryGetValue(cell.Id, out var s) ? s : "<no line>";
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                divergences.Add($"  {cell.Call}: python {python}, expected {expected}, sharpy {actual}");
        }

        // Positive controls: the matrix really reaches the exception arms and the int32 deviation.
        Assert.Contains(cells, c => pythonById[c.Id] == ValueErrorName);
        Assert.Contains(cells, c => pythonById[c.Id] == OverflowErrorName);
        Assert.True(deviationCells >= 2, $"only {deviationCells} cells exercised the int32 OverflowError deviation");

        Assert.True(divergences.Count == 0,
            $"{divergences.Count} of {cells.Count} round() cells diverge from CPython:\n" + string.Join("\n", divergences));
    }
}
