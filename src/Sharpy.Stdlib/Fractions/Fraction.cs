using System;
using System.Globalization;
using System.Numerics;

namespace Sharpy
{
    [SharpyModuleType("fractions", "Fraction")]
    public sealed class Fraction : IEquatable<Fraction>, IComparable<Fraction>, IComparable
    {
        public BigInteger Numerator { get; }
        public BigInteger Denominator { get; }

        public Fraction(BigInteger numerator, BigInteger denominator)
        {
            if (denominator == BigInteger.Zero)
            {
                throw new ZeroDivisionError("Fraction(_, 0)");
            }

            if (denominator < BigInteger.Zero)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            Numerator = numerator / gcd;
            Denominator = denominator / gcd;
        }

        public Fraction(BigInteger value) : this(value, BigInteger.One) { }

        public Fraction(int value) : this(new BigInteger(value), BigInteger.One) { }

        public Fraction(long value) : this(new BigInteger(value), BigInteger.One) { }

        public Fraction(Fraction other) : this(other.Numerator, other.Denominator) { }

        public Fraction(double value)
        {
            if (double.IsNaN(value))
                throw new ValueError("cannot convert NaN to Fraction");
            if (double.IsInfinity(value))
                throw new ValueError("cannot convert Infinity to Fraction");

            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int exponent = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0x000FFFFFFFFFFFFF;

            if (exponent == 0)
            {
                exponent = 1;
            }
            else
            {
                mantissa |= 0x0010000000000000;
            }

            exponent -= 1075; // 1023 (bias) + 52 (mantissa bits)

            BigInteger num = new BigInteger(mantissa);
            if (negative)
                num = -num;

            BigInteger den;
            if (exponent >= 0)
            {
                num <<= exponent;
                den = BigInteger.One;
            }
            else
            {
                den = BigInteger.One << (-exponent);
            }

            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(num), den);
            Numerator = num / gcd;
            Denominator = den / gcd;
        }

        public Fraction(string value)
        {
            if (value == null)
                throw new ValueError("cannot convert null to Fraction");

            value = value.Trim();
            if (value.Length == 0)
                throw new ValueError("cannot convert empty string to Fraction");

            BigInteger numerator;
            BigInteger denominator;

            int slashIndex = value.IndexOf('/');
            if (slashIndex >= 0)
            {
                string numStr = value.Substring(0, slashIndex).Trim();
                string denStr = value.Substring(slashIndex + 1).Trim();

                if (!BigInteger.TryParse(numStr, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out numerator))
                    throw new ValueError($"Invalid literal for Fraction: '{value}'");
                if (!BigInteger.TryParse(denStr, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out denominator))
                    throw new ValueError($"Invalid literal for Fraction: '{value}'");
            }
            else
            {
                int eIndex = value.IndexOfAny(new[] { 'e', 'E' });
                int dotIndex = value.IndexOf('.');

                if (dotIndex >= 0 || eIndex >= 0)
                {
                    string mainPart = eIndex >= 0 ? value.Substring(0, eIndex) : value;
                    int exp = 0;
                    if (eIndex >= 0)
                    {
                        if (!int.TryParse(value.Substring(eIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exp))
                            throw new ValueError($"Invalid literal for Fraction: '{value}'");
                    }

                    dotIndex = mainPart.IndexOf('.');
                    string intPart;
                    string fracPart;
                    if (dotIndex >= 0)
                    {
                        intPart = mainPart.Substring(0, dotIndex);
                        fracPart = mainPart.Substring(dotIndex + 1);
                    }
                    else
                    {
                        intPart = mainPart;
                        fracPart = "";
                    }

                    bool isNegative = false;
                    if (intPart.StartsWith("-"))
                    {
                        isNegative = true;
                        intPart = intPart.Substring(1);
                    }
                    else if (intPart.StartsWith("+"))
                    {
                        intPart = intPart.Substring(1);
                    }

                    if (intPart.Length == 0)
                        intPart = "0";

                    string digits = intPart + fracPart;
                    if (!BigInteger.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out numerator))
                        throw new ValueError($"Invalid literal for Fraction: '{value}'");

                    int scale = fracPart.Length - exp;

                    if (scale > 0)
                    {
                        denominator = BigInteger.Pow(10, scale);
                    }
                    else
                    {
                        numerator *= BigInteger.Pow(10, -scale);
                        denominator = BigInteger.One;
                    }

                    if (isNegative)
                        numerator = -numerator;
                }
                else
                {
                    if (!BigInteger.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out numerator))
                        throw new ValueError($"Invalid literal for Fraction: '{value}'");
                    denominator = BigInteger.One;
                }
            }

            if (denominator == BigInteger.Zero)
                throw new ZeroDivisionError("Fraction(_, 0)");

            if (denominator < BigInteger.Zero)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            Numerator = numerator / gcd;
            Denominator = denominator / gcd;
        }

        public Fraction LimitDenominator(long maxDenominator = 1000000)
        {
            return LimitDenominator(new BigInteger(maxDenominator));
        }

        public Fraction LimitDenominator(BigInteger maxDenominator)
        {
            if (maxDenominator < BigInteger.One)
                throw new ValueError("max_denominator should be at least 1");

            if (Denominator <= maxDenominator)
                return this;

            BigInteger p0 = BigInteger.Zero, q0 = BigInteger.One;
            BigInteger p1 = BigInteger.One, q1 = BigInteger.Zero;
            BigInteger n = Numerator, d = Denominator;

            while (true)
            {
                BigInteger a = BigInteger.DivRem(n, d, out BigInteger rem);
                BigInteger q2 = q0 + a * q1;

                if (q2 > maxDenominator)
                    break;

                BigInteger tempP = p0;
                p0 = p1;
                p1 = tempP + a * p1;

                BigInteger tempQ = q0;
                q0 = q1;
                q1 = q2;

                n = d;
                d = rem;
            }

            BigInteger k = (maxDenominator - q0) / q1;
            var bound1 = new Fraction(p0 + k * p1, q0 + k * q1);
            var bound2 = new Fraction(p1, q1);

            var diff1 = BigInteger.Abs(Numerator * bound1.Denominator - bound1.Numerator * Denominator) * bound2.Denominator;
            var diff2 = BigInteger.Abs(Numerator * bound2.Denominator - bound2.Numerator * Denominator) * bound1.Denominator;

            if (diff1 <= diff2)
                return bound1;
            return bound2;
        }

        // Arithmetic operators

        public static Fraction operator +(Fraction a, Fraction b)
        {
            return new Fraction(
                a.Numerator * b.Denominator + b.Numerator * a.Denominator,
                a.Denominator * b.Denominator);
        }

        public static Fraction operator -(Fraction a, Fraction b)
        {
            return new Fraction(
                a.Numerator * b.Denominator - b.Numerator * a.Denominator,
                a.Denominator * b.Denominator);
        }

        public static Fraction operator -(Fraction a)
        {
            return new Fraction(-a.Numerator, a.Denominator);
        }

        public static Fraction operator +(Fraction a)
        {
            return a;
        }

        public static Fraction operator *(Fraction a, Fraction b)
        {
            return new Fraction(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
        }

        public static Fraction operator /(Fraction a, Fraction b)
        {
            if (b.Numerator == BigInteger.Zero)
                throw new ZeroDivisionError("Fraction division by zero");
            return new Fraction(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
        }

        public static Fraction operator %(Fraction a, Fraction b) => Mod(a, b);

        // Mixed arithmetic with long

        public static Fraction operator +(Fraction a, long b) => a + new Fraction(b);
        public static Fraction operator +(long a, Fraction b) => new Fraction(a) + b;
        public static Fraction operator -(Fraction a, long b) => a - new Fraction(b);
        public static Fraction operator -(long a, Fraction b) => new Fraction(a) - b;
        public static Fraction operator *(Fraction a, long b) => a * new Fraction(b);
        public static Fraction operator *(long a, Fraction b) => new Fraction(a) * b;
        public static Fraction operator /(Fraction a, long b) => a / new Fraction(b);
        public static Fraction operator /(long a, Fraction b) => new Fraction(a) / b;
        public static Fraction operator %(Fraction a, long b) => a % new Fraction(b);
        public static Fraction operator %(long a, Fraction b) => new Fraction(a) % b;

        private static long FloorDivInternal(Fraction a, Fraction b)
        {
            if (b.Numerator == BigInteger.Zero)
                throw new ZeroDivisionError("Fraction floor division by zero");
            var result = a.Numerator * b.Denominator;
            var divisor = a.Denominator * b.Numerator;
            BigInteger quotient = BigInteger.DivRem(result, divisor, out BigInteger remainder);
            if (remainder != BigInteger.Zero && (result < 0) != (divisor < 0))
                quotient -= BigInteger.One;
            return (long)quotient;
        }

        public long FloorDiv(Fraction other)
        {
            return FloorDivInternal(this, other);
        }

        public static Fraction Mod(Fraction a, Fraction b)
        {
            if (b.Numerator == BigInteger.Zero)
                throw new ZeroDivisionError("Fraction modulo by zero");
            long floor = FloorDivInternal(a, b);
            return a - new Fraction(floor) * b;
        }

        public Fraction Pow(int exponent)
        {
            if (exponent >= 0)
            {
                return new Fraction(
                    BigInteger.Pow(Numerator, exponent),
                    BigInteger.Pow(Denominator, exponent));
            }
            else
            {
                if (Numerator == BigInteger.Zero)
                    throw new ZeroDivisionError("0 cannot be raised to a negative power");
                return new Fraction(
                    BigInteger.Pow(Denominator, -exponent),
                    BigInteger.Pow(Numerator, -exponent));
            }
        }

        public Fraction Abs() => new Fraction(BigInteger.Abs(Numerator), Denominator);

        public long ToLong()
        {
            // Truncate toward zero, matching Python's int(Fraction)
            return (long)(Numerator / Denominator);
        }

        public double ToDouble()
        {
            return (double)Numerator / (double)Denominator;
        }

        // Comparison and equality

        public bool Equals(Fraction? other)
        {
            if (other is null)
                return false;
            return Numerator == other.Numerator && Denominator == other.Denominator;
        }

        public override bool Equals(object? obj)
        {
            if (obj is Fraction f)
                return Equals(f);
            if (obj is int i)
                return Equals(new Fraction(i));
            if (obj is long l)
                return Equals(new Fraction(l));
            if (obj is double d)
                return Equals(new Fraction(d));
            return false;
        }

        public override int GetHashCode()
        {
            if (Denominator == BigInteger.One)
                return Numerator.GetHashCode();
#if NET10_0_OR_GREATER
            return HashCode.Combine(Numerator, Denominator);
#else
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Numerator.GetHashCode();
                hash = hash * 31 + Denominator.GetHashCode();
                return hash;
            }
#endif
        }

        public int CompareTo(Fraction? other)
        {
            if (other is null)
                return 1;
            return (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
        }

        int IComparable.CompareTo(object? obj)
        {
            if (obj is Fraction f)
                return CompareTo(f);
            if (obj is int i)
                return CompareTo(new Fraction(i));
            if (obj is long l)
                return CompareTo(new Fraction(l));
            return 1;
        }

        public static bool operator ==(Fraction? a, Fraction? b)
        {
            if (a is null)
                return b is null;
            return a.Equals(b);
        }

        public static bool operator !=(Fraction? a, Fraction? b) => !(a == b);
        public static bool operator <(Fraction a, Fraction b) => a.CompareTo(b) < 0;
        public static bool operator >(Fraction a, Fraction b) => a.CompareTo(b) > 0;
        public static bool operator <=(Fraction a, Fraction b) => a.CompareTo(b) <= 0;
        public static bool operator >=(Fraction a, Fraction b) => a.CompareTo(b) >= 0;

        // Implicit conversions from integer types

        public static implicit operator Fraction(int value) => new Fraction(value);
        public static implicit operator Fraction(long value) => new Fraction(value);

        public override string ToString()
        {
            if (Denominator == BigInteger.One)
                return Numerator.ToString();
            return $"{Numerator}/{Denominator}";
        }
    }
}
