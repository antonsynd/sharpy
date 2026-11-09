// W1.2 (#2460) round() kernel: Sharpy.Builtins.Round, which follows CPython's float.__round__ (correctly
// rounded through decimal/BigInteger arithmetic), against the System.Math.Round route it replaced, over
// the same 4096 finite doubles. Each variant is one call per element; the program prints the median
// nanoseconds per call. See README.md.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class Program
{
    private const int N = 4096;
    private const int WarmUps = 5;

    private static int Main(string[] args)
    {
        var variant = args.Length > 0 ? args[0] : "builtins-int";
        var runs = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 30;

        var core = typeof(Sharpy.Builtins).Assembly;
        var debuggable = core.GetCustomAttribute<DebuggableAttribute>();
        var optimizerDisabled = debuggable?.IsJITOptimizerDisabled ?? false;
        Console.WriteLine($"Sharpy.Core {core.Location}");
        Console.WriteLine($"IsJITOptimizerDisabled={optimizerDisabled}");
        if (optimizerDisabled)
            Console.WriteLine("WARNING: Debug Sharpy.Core — this reading is not evidence (run with -c Release).");

        var xs = Inputs();
        var ints = IntInputs();
        Func<double> kernel = variant switch
        {
            "builtins-int" => () => BuiltinsInt(xs),
            "math-int" => () => MathInt(xs),
            "builtins-2" => () => BuiltinsDigits(xs, 2),
            "math-2" => () => MathDigits(xs, 2),
            "builtins-neg2" => () => BuiltinsDigits(xs, -2),
            "builtins-int-neg1" => () => BuiltinsIntDigits(ints, -1),
            _ => throw new ArgumentException(
                $"unknown variant '{variant}' (builtins-int|math-int|builtins-2|math-2|builtins-neg2|builtins-int-neg1)"),
        };

        double checksum = 0;
        for (int w = 0; w < WarmUps; w++)
            checksum += kernel();

        var samples = new double[runs];
        for (int r = 0; r < runs; r++)
        {
            var sw = Stopwatch.StartNew();
            checksum += kernel();
            sw.Stop();
            samples[r] = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / N;
        }

        Array.Sort(samples);
        var median = runs % 2 == 1 ? samples[runs / 2] : (samples[runs / 2 - 1] + samples[runs / 2]) / 2;
        Console.WriteLine(
            $"variant={variant} n={N} runs={runs} median_ns_per_call={median:F1} min_ns={samples[0]:F1} max_ns={samples[runs - 1]:F1} checksum={checksum:R}");
        return 0;
    }

    // 4096 finite doubles: magnitudes 1e-3 .. 1e6 (uniform in log10), signs alternating, fractional parts
    // from an LCG so ties and near-ties occur but never dominate. Deterministic: every run sees the same inputs.
    private static double[] Inputs()
    {
        var xs = new double[N];
        uint state = 0x9E3779B9u;
        for (int i = 0; i < N; i++)
        {
            state = state * 1664525u + 1013904223u;
            var mantissa = state / (double)uint.MaxValue;            // [0, 1)
            var exponent = -3.0 + 9.0 * ((i * 7919) % N) / N;         // -3 .. 6
            var sign = (i & 1) == 0 ? 1.0 : -1.0;
            xs[i] = sign * (mantissa + 0.5) * Math.Pow(10.0, exponent);
        }
        return xs;
    }

    private static int[] IntInputs()
    {
        var xs = new int[N];
        for (int i = 0; i < N; i++)
            xs[i] = (i * 37) % 2001 - 1000;
        return xs;
    }

    // ---- round(x): Sharpy.Builtins.Round(double) -> int (CPython contract: half to even, exact) ----
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double BuiltinsInt(double[] xs)
    {
        long acc = 0;
        for (int i = 0; i < xs.Length; i++)
            acc += Sharpy.Builtins.Round(xs[i]);
        return acc;
    }

    // ---- the route W1.2 replaced: (int)Math.Round(x, MidpointRounding.ToEven) ----
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double MathInt(double[] xs)
    {
        long acc = 0;
        for (int i = 0; i < xs.Length; i++)
            acc += (int)Math.Round(xs[i], MidpointRounding.ToEven);
        return acc;
    }

    // ---- round(x, n): Sharpy.Builtins.Round(double, int) -> double (correctly rounded) ----
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double BuiltinsDigits(double[] xs, int n)
    {
        double acc = 0;
        for (int i = 0; i < xs.Length; i++)
            acc += Sharpy.Builtins.Round(xs[i], n);
        return acc;
    }

    // ---- the route W1.2 replaced: Math.Round(x, n, MidpointRounding.ToEven), n in 0..15 only ----
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double MathDigits(double[] xs, int n)
    {
        double acc = 0;
        for (int i = 0; i < xs.Length; i++)
            acc += Math.Round(xs[i], n, MidpointRounding.ToEven);
        return acc;
    }

    // ---- round(int, n): the overload W1.2 added (no Math.Round counterpart) ----
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double BuiltinsIntDigits(int[] xs, int n)
    {
        long acc = 0;
        for (int i = 0; i < xs.Length; i++)
            acc += Sharpy.Builtins.Round(xs[i], n);
        return acc;
    }
}
