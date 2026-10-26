using System;

namespace Sharpy.Compiler.Tests.Fuzz;

/// <summary>
/// The fuzz suite's scale and seed knobs, read from the environment so the nightly soak
/// (<c>.github/workflows/nightly.yml</c>) can explore far past what a PR run affords.
/// <list type="bullet">
/// <item><see cref="ScaleVariable"/>: a positive integer that multiplies every seeded loop's
/// iteration count. Unset means 1.</item>
/// <item><see cref="SaltVariable"/>: a non-zero integer mixed into every seed, so each night
/// draws inputs no PR run draws. Unset or 0 means the seed is used as written.</item>
/// </list>
/// With both unset, a PR run generates exactly the inputs it generated before the knobs existed.
/// A malformed value throws instead of falling back, so a typo in the workflow fails the job rather
/// than silently soaking at the PR budget.
/// </summary>
internal static class FuzzBudget
{
    public const string ScaleVariable = "SHARPY_FUZZ_SCALE";
    public const string SaltVariable = "SHARPY_FUZZ_SALT";

    /// <summary>The iteration multiplier from <see cref="ScaleVariable"/>.</summary>
    public static int Scale => ParseScale(Environment.GetEnvironmentVariable(ScaleVariable));

    /// <summary>The seed salt from <see cref="SaltVariable"/>.</summary>
    public static int Salt => ParseSalt(Environment.GetEnvironmentVariable(SaltVariable));

    /// <summary>The iteration count for a loop written with <paramref name="baseCount"/> iterations.</summary>
    public static int Iterations(int baseCount) => checked(baseCount * Scale);

    /// <summary>The seed a fuzzer is built from: <paramref name="seed"/> mixed with <see cref="Salt"/>.</summary>
    public static int Seed(int seed) => Mix(seed, Salt);

    /// <summary>How a failure message names its seed, including how to reproduce it.</summary>
    public static string Label(int seed)
    {
        var salt = Salt;
        return salt == 0 ? $"Seed {seed}" : $"Seed {seed} ({SaltVariable}={salt}, effective {Mix(seed, salt)})";
    }

    internal static int ParseScale(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return 1;
        if (int.TryParse(raw, out var value) && value > 0)
            return value;
        throw new InvalidOperationException($"{ScaleVariable} must be a positive integer, got '{raw}'.");
    }

    internal static int ParseSalt(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return 0;
        if (int.TryParse(raw, out var value))
            return value;
        throw new InvalidOperationException($"{SaltVariable} must be an integer, got '{raw}'.");
    }

    /// <summary>Salt 0 is the identity, so PR runs keep their seeds. Otherwise the golden-ratio multiply
    /// spreads consecutive salts (the nightly passes the run number) across the seed space.</summary>
    internal static int Mix(int seed, int salt) => salt == 0 ? seed : unchecked(seed ^ (salt * -1640531527));
}
