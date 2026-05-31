using System;

class Program
{
    static long FibRecursive(long n)
    {
        if (n <= 1) return n;
        return FibRecursive(n - 1) + FibRecursive(n - 2);
    }

    static long FibIterative(long n)
    {
        if (n <= 1) return n;
        long a = 0, b = 1;
        for (long i = 2; i <= n; i++)
        {
            long temp = a + b;
            a = b;
            b = temp;
        }
        return b;
    }

    static void Main()
    {
        // Recursive (expensive)
        long result = FibRecursive(30);
        Console.WriteLine(result);
        // Iterative (fast)
        for (int i = 0; i < 100_000; i++)
        {
            FibIterative(30);
        }
        Console.WriteLine("done");
    }
}
