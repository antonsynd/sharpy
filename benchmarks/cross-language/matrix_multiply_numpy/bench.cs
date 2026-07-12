using System;
using MathNet.Numerics.LinearAlgebra;

class Program
{
    // Hand-written C# baseline for the numeric path: MathNet.Numerics dense
    // matrix multiply — the same backend Sharpy's numpy delegates to, so a
    // Spy/C# ratio near 1.0 means the numpy path adds negligible overhead.
    static void Main()
    {
        int size = 256;
        var a = Matrix<double>.Build.Dense(size, size, (i, j) => (i + j) % 7);
        var b = Matrix<double>.Build.Dense(size, size, (i, j) => (i * j + 1) % 11);

        Matrix<double> result = a * b;
        for (int n = 1; n < 200; n++)
        {
            result = a * b;
        }
        Console.WriteLine($"done: {(long)result[0, 0]}");
    }
}
