// P76 (#2404) element-access kernel: one N x N integer matrix multiply, written the way the
// Sharpy compiler emits `c[i][j] += a[i][k] * b[k][j]` (8 element accesses per inner
// iteration: c[i] get, [j] get, c[i] get, [j] set, a[i], [k], b[k], [j]). See README.md.
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class Program
{
    private const int N = 100;
    private const int WarmUps = 5;

    private static int Main(string[] args)
    {
        var variant = args.Length > 0 ? args[0] : "indexer";
        var runs = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 30;

        var core = typeof(Sharpy.List<int>).Assembly;
        var debuggable = core.GetCustomAttribute<DebuggableAttribute>();
        var optimizerDisabled = debuggable?.IsJITOptimizerDisabled ?? false;
        Console.WriteLine($"Sharpy.Core {core.Location}");
        Console.WriteLine($"IsJITOptimizerDisabled={optimizerDisabled}");
        if (optimizerDisabled)
            Console.WriteLine("WARNING: Debug Sharpy.Core — this reading is not evidence (run with -c Release).");

        Func<long> kernel = variant switch
        {
            "indexer" => MakeIndexer(),
            "unchecked" => MakeUnchecked(),
            "bcl" => MakeBcl(),
            "arrayhelpers" => MakeArrayHelpers(),
            "raw" => MakeRaw(),
            _ => throw new ArgumentException($"unknown variant '{variant}' (indexer|unchecked|bcl|arrayhelpers|raw)"),
        };

        long checksum = 0;
        for (int w = 0; w < WarmUps; w++)
            checksum += kernel();

        var samples = new double[runs];
        for (int r = 0; r < runs; r++)
        {
            var sw = Stopwatch.StartNew();
            checksum += kernel();
            sw.Stop();
            samples[r] = sw.Elapsed.TotalMilliseconds;
        }

        Array.Sort(samples);
        var median = runs % 2 == 1 ? samples[runs / 2] : (samples[runs / 2 - 1] + samples[runs / 2]) / 2;
        Console.WriteLine($"variant={variant} n={N} runs={runs} median_ms={median:F3} min_ms={samples[0]:F3} max_ms={samples[runs - 1]:F3} checksum={checksum}");
        return 0;
    }

    private static int Value(int i, int j) => (i * 7 + j * 3) % 11;

    // ---- A: Sharpy.List<T> indexer (rows 1/2) ----
    private static Func<long> MakeIndexer()
    {
        var a = SharpyMatrix(); var b = SharpyMatrix();
        return () => MatMulIndexer(a, b);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MatMulIndexer(Sharpy.List<Sharpy.List<int>> a, Sharpy.List<Sharpy.List<int>> b)
    {
        var c = SharpyZero();
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
                for (int k = 0; k < N; k++)
                    c[i][j] = c[i][j] + a[i][k] * b[k][j];
        return c[N - 1][N - 1];
    }

    // ---- B: GetItemUnchecked reads (row 3), indexer write (the range-loop route) ----
    private static Func<long> MakeUnchecked()
    {
        var a = SharpyMatrix(); var b = SharpyMatrix();
        return () => MatMulUnchecked(a, b);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MatMulUnchecked(Sharpy.List<Sharpy.List<int>> a, Sharpy.List<Sharpy.List<int>> b)
    {
        var c = SharpyZero();
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
                for (int k = 0; k < N; k++)
                    c.GetItemUnchecked(i)[j] = c.GetItemUnchecked(i).GetItemUnchecked(j)
                        + a.GetItemUnchecked(i).GetItemUnchecked(k) * b.GetItemUnchecked(k).GetItemUnchecked(j);
        return c[N - 1][N - 1];
    }

    // ---- C: BCL List<T> (the floor R-KA's 1.3x trigger is measured against) ----
    private static Func<long> MakeBcl()
    {
        var a = BclMatrix(); var b = BclMatrix();
        return () => MatMulBcl(a, b);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MatMulBcl(System.Collections.Generic.List<System.Collections.Generic.List<int>> a,
        System.Collections.Generic.List<System.Collections.Generic.List<int>> b)
    {
        var c = Enumerable.Range(0, N).Select(_ => Enumerable.Repeat(0, N).ToList()).ToList();
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
                for (int k = 0; k < N; k++)
                    c[i][j] = c[i][j] + a[i][k] * b[k][j];
        return c[N - 1][N - 1];
    }

    // ---- D: int[][] through ArrayHelpers (row 11, the array[T] route) ----
    private static Func<long> MakeArrayHelpers()
    {
        var a = ArrayMatrix(); var b = ArrayMatrix();
        return () => MatMulArrayHelpers(a, b);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MatMulArrayHelpers(int[][] a, int[][] b)
    {
        var c = Enumerable.Range(0, N).Select(_ => new int[N]).ToArray();
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
                for (int k = 0; k < N; k++)
                    Sharpy.ArrayHelpers.SetItem(Sharpy.ArrayHelpers.GetItem(c, i), j,
                        Sharpy.ArrayHelpers.GetItem(Sharpy.ArrayHelpers.GetItem(c, i), j)
                        + Sharpy.ArrayHelpers.GetItem(Sharpy.ArrayHelpers.GetItem(a, i), k)
                        * Sharpy.ArrayHelpers.GetItem(Sharpy.ArrayHelpers.GetItem(b, k), j));
        return c[N - 1][N - 1];
    }

    // ---- E: raw int[][] (the lower bound) ----
    private static Func<long> MakeRaw()
    {
        var a = ArrayMatrix(); var b = ArrayMatrix();
        return () => MatMulRaw(a, b);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MatMulRaw(int[][] a, int[][] b)
    {
        var c = Enumerable.Range(0, N).Select(_ => new int[N]).ToArray();
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
                for (int k = 0; k < N; k++)
                    c[i][j] = c[i][j] + a[i][k] * b[k][j];
        return c[N - 1][N - 1];
    }

    private static Sharpy.List<Sharpy.List<int>> SharpyMatrix()
    {
        var m = new Sharpy.List<Sharpy.List<int>>();
        for (int i = 0; i < N; i++)
        {
            var row = new Sharpy.List<int>();
            for (int j = 0; j < N; j++)
                row.Append(Value(i, j));
            m.Append(row);
        }
        return m;
    }

    private static Sharpy.List<Sharpy.List<int>> SharpyZero()
    {
        var m = new Sharpy.List<Sharpy.List<int>>();
        for (int i = 0; i < N; i++)
        {
            var row = new Sharpy.List<int>();
            for (int j = 0; j < N; j++)
                row.Append(0);
            m.Append(row);
        }
        return m;
    }

    private static System.Collections.Generic.List<System.Collections.Generic.List<int>> BclMatrix() =>
        Enumerable.Range(0, N).Select(i => Enumerable.Range(0, N).Select(j => Value(i, j)).ToList()).ToList();

    private static int[][] ArrayMatrix() =>
        Enumerable.Range(0, N).Select(i => Enumerable.Range(0, N).Select(j => Value(i, j)).ToArray()).ToArray();
}
