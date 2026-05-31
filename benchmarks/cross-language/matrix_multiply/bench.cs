using System;

class Program
{
    static long[][] MatrixMultiply(long[][] a, long[][] b)
    {
        int rowsA = a.Length;
        int colsA = a[0].Length;
        int colsB = b[0].Length;
        var result = new long[rowsA][];
        for (int i = 0; i < rowsA; i++)
        {
            result[i] = new long[colsB];
            for (int j = 0; j < colsB; j++)
            {
                long sum = 0;
                for (int k = 0; k < colsA; k++)
                    sum += a[i][k] * b[k][j];
                result[i][j] = sum;
            }
        }
        return result;
    }

    static void Main()
    {
        int size = 100;
        var a = new long[size][];
        var b = new long[size][];
        for (int i = 0; i < size; i++)
        {
            a[i] = new long[size];
            b[i] = new long[size];
            for (int j = 0; j < size; j++)
            {
                a[i][j] = (i + j) % 7;
                b[i][j] = (i * j + 1) % 11;
            }
        }

        long[][] result = null;
        for (int n = 0; n < 10; n++)
        {
            result = MatrixMultiply(a, b);
        }
        Console.WriteLine($"done: {result[0][0]}");
    }
}
