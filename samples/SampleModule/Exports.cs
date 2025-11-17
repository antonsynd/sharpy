using System;

namespace SampleModule;

/// <summary>
/// Sample third-party module for Sharpy.
/// All public static methods in this class will be automatically discovered.
/// </summary>
public static class Exports
{
    /// <summary>
    /// Square a number
    /// </summary>
    public static int Square(int x) => x * x;

    /// <summary>
    /// Cube a number
    /// </summary>
    public static int Cube(int x) => x * x * x;

    /// <summary>
    /// Calculate the average of numbers
    /// </summary>
    public static double Average(params double[] numbers)
    {
        if (numbers.Length == 0)
            return 0;
        
        double sum = 0;
        foreach (var num in numbers)
            sum += num;
        
        return sum / numbers.Length;
    }

    /// <summary>
    /// Check if a number is prime
    /// </summary>
    public static bool IsPrime(int n)
    {
        if (n < 2) return false;
        for (int i = 2; i <= Math.Sqrt(n); i++)
        {
            if (n % i == 0) return false;
        }
        return true;
    }

    /// <summary>
    /// Get factorial of a number
    /// </summary>
    public static long Factorial(int n)
    {
        if (n < 0)
            throw new ArgumentException("Factorial is not defined for negative numbers");
        if (n == 0 || n == 1)
            return 1;
        
        long result = 1;
        for (int i = 2; i <= n; i++)
        {
            result *= i;
        }
        return result;
    }
}
