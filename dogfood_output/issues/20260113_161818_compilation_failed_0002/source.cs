using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Source
{
    public static class Exports
    {
        /// <summary>
        /// Check if a number is prime using trial division
        /// </summary>
        public static bool IsPrime(int n)
        {
            if (n < 2)
            {
                return false;
            }

            if (n == 2)
            {
                return true;
            }

            if (n % 2 == 0)
            {
                return false;
            }

            int i = 3;
            while (i * i <= n)
            {
                if (n % i == 0)
                {
                    return false;
                }

                i = i + 2;
            }

            return true;
        }

        /// <summary>
        /// Classify a number based on multiple criteria
        /// </summary>
        public static string ClassifyNumber(int n)
        {
            string result = "";
            if (n < 0)
            {
                result = "negative";
                if (n % 2 == 0)
                {
                    result = result + "_even";
                }
                else
                {
                    result = result + "_odd";
                }
            }
            else if (n == 0)
            {
                result = "zero";
            }
            else
            {
                if (IsPrime(n))
                {
                    result = "prime";
                }
                else
                {
                    result = "composite";
                }

                if (n % 3 == 0)
                {
                    if (n % 5 == 0)
                    {
                        result = result + "_fizzbuzz";
                    }
                    else
                    {
                        result = result + "_fizz";
                    }
                }
                else if (n % 5 == 0)
                {
                    result = result + "_buzz";
                }
            }

            return result;
        }

        /// <summary>
        /// Sum numbers matching specific divisibility patterns
        /// </summary>
        public static int FindPatternSum(int limit, int divisor1, int divisor2)
        {
            int total = 0;
            foreach (var i in global::Sharpy.Core.Exports.Range(1, limit))
            {
                if (i % divisor1 == 0)
                {
                    if (i % divisor2 == 0)
                    {
                        total = total + i * 2;
                        global::Sharpy.Core.Exports.Print($"  Both: {i} contributes {i * 2}");
                    }
                    else
                    {
                        total = total + i;
                        global::Sharpy.Core.Exports.Print($"  First only: {i} contributes {i}");
                    }
                }
                else if (i % divisor2 == 0)
                {
                    total = total + i;
                    global::Sharpy.Core.Exports.Print($"  Second only: {i} contributes {i}");
                }
            }

            return total;
        }

        /// <summary>
        /// Generate a pattern matrix and compute diagonal sum
        /// </summary>
        public static int NestedLoopMatrix(int size)
        {
            int diagonalSum = 0;
            global::Sharpy.Core.Exports.Print($"Matrix {size}x{size}:");
            foreach (var row in global::Sharpy.Core.Exports.Range(size))
            {
                string line = "";
                foreach (var col in global::Sharpy.Core.Exports.Range(size))
                {
                    int value = 0;
                    if (row == col)
                    {
                        value = row + col + 1;
                        diagonalSum = diagonalSum + value;
                    }
                    else if (row < col)
                    {
                        if ((row + col) % 2 == 0)
                        {
                            value = 1;
                        }
                        else
                        {
                            value = 2;
                        }
                    }
                    else
                    {
                        if (row % 2 == 0)
                        {
                            if (col % 2 == 0)
                            {
                                value = 3;
                            }
                            else
                            {
                                value = 4;
                            }
                        }
                        else
                        {
                            value = 5;
                        }
                    }

                    if (col > 0)
                    {
                        line = line + " ";
                    }

                    line = line + $"{value}";
                }

                global::Sharpy.Core.Exports.Print($"  {line}");
            }

            return diagonalSum;
        }

        /// <summary>
        /// Count Collatz sequence steps with early termination
        /// </summary>
        public static int CollatzSteps(int n, int maxSteps)
        {
            int steps = 0;
            int current = n;
            global::Sharpy.Core.Exports.Print($"Collatz({n}):");
            while (current != 1)
            {
                if (steps >= maxSteps)
                {
                    global::Sharpy.Core.Exports.Print($"  Exceeded {maxSteps} steps, stopping");
                    break;
                }

                global::Sharpy.Core.Exports.Print($"  Step {steps}: {current}");
                if (current % 2 == 0)
                {
                    current = (int)Math.Floor((double)(current) / 2);
                }
                else
                {
                    current = current * 3 + 1;
                }

                steps = steps + 1;
            }

            if (current == 1)
            {
                global::Sharpy.Core.Exports.Print($"  Step {steps}: {current} (done)");
            }

            return steps;
        }

        /// <summary>
        /// Process a range with conditional breaks and continues
        /// </summary>
        public static int ProcessRangeWithBreaks(int start, int end)
        {
            int processed = 0;
            int skipped = 0;
            foreach (var i in global::Sharpy.Core.Exports.Range(start, end))
            {
                if (i % 7 == 0)
                {
                    global::Sharpy.Core.Exports.Print($"  Breaking at {i} (divisible by 7)");
                    break;
                }

                if (i % 3 == 0)
                {
                    if (i % 2 == 0)
                    {
                        skipped = skipped + 1;
                        global::Sharpy.Core.Exports.Print($"  Skipping {i} (divisible by 6)");
                        continue;
                    }
                }

                processed = processed + i;
                global::Sharpy.Core.Exports.Print($"  Processed {i}, running total: {processed}");
            }

            global::Sharpy.Core.Exports.Print($"  Skipped count: {skipped}");
            return processed;
        }

        public static void Main()
        {
            global::Sharpy.Core.Exports.Print("=== Sharpy Nested Control Flow Test ===");
            global::Sharpy.Core.Exports.Print("");
            global::Sharpy.Core.Exports.Print("--- Test 1: Number Classification ---");
            global::Sharpy.Core.List<int> testNumbers = new global::Sharpy.Core.List<int>()
            {
                -4,
                -3,
                0,
                1,
                2,
                7,
                15,
                30
            };
            foreach (var num in testNumbers)
            {
                string classification = ClassifyNumber(num);
                global::Sharpy.Core.Exports.Print($"classify({num}) = {classification}");
            }

            global::Sharpy.Core.Exports.Print("");
            global::Sharpy.Core.Exports.Print("--- Test 2: Pattern Sum (limit=16, div1=3, div2=4) ---");
            int patternResult = FindPatternSum(16, 3, 4);
            global::Sharpy.Core.Exports.Print($"Pattern sum result: {patternResult}");
            global::Sharpy.Core.Exports.Print("");
            global::Sharpy.Core.Exports.Print("--- Test 3: Nested Loop Matrix ---");
            int diagSum = NestedLoopMatrix(4);
            global::Sharpy.Core.Exports.Print($"Diagonal sum: {diagSum}");
            global::Sharpy.Core.Exports.Print("");
            global::Sharpy.Core.Exports.Print("--- Test 4: Collatz Sequences ---");
            int c1 = CollatzSteps(6, 20);
            global::Sharpy.Core.Exports.Print($"Steps for 6: {c1}");
            int c2 = CollatzSteps(27, 10);
            global::Sharpy.Core.Exports.Print($"Steps for 27 (max 10): {c2}");
            global::Sharpy.Core.Exports.Print("");
            global::Sharpy.Core.Exports.Print("--- Test 5: Range Processing with Breaks ---");
            int procResult = ProcessRangeWithBreaks(1, 20);
            global::Sharpy.Core.Exports.Print($"Final processed sum: {procResult}");
            global::Sharpy.Core.Exports.Print("");
            global::Sharpy.Core.Exports.Print("--- Test 6: Prime Pairs in Range ---");
            int pairCount = 0;
            foreach (var i in global::Sharpy.Core.Exports.Range(2, 20))
            {
                if (IsPrime(i))
                {
                    foreach (var j in global::Sharpy.Core.Exports.Range(i + 1, 20))
                    {
                        if (IsPrime(j))
                        {
                            if (j - i == 2)
                            {
                                global::Sharpy.Core.Exports.Print($"  Twin primes: ({i}, {j})");
                                pairCount = pairCount + 1;
                            }
                        }
                    }
                }
            }

            global::Sharpy.Core.Exports.Print($"Twin prime pairs found: {pairCount}");
            global::Sharpy.Core.Exports.Print("");
            global::Sharpy.Core.Exports.Print("=== All Tests Complete ===");
        }
    }
}