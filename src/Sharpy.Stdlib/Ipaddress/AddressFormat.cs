using System;
using System.Numerics;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// CPython 3.12's <c>ipaddress._BaseAddress.__format__</c>, the <c>__format__</c> that
    /// <see cref="IPv4Address"/>, <see cref="IPv6Address"/> and — by inheritance in python —
    /// <see cref="IPv4Interface"/>/<see cref="IPv6Interface"/> own. Each implements
    /// <see cref="IFormattable"/> (the CLR spelling of <c>__format__</c>) through this, so
    /// <c>format(addr, spec)</c> on every route behaves as CPython's (#1988 regression, plan-bf0244
    /// audit R2).
    /// </summary>
    internal static class AddressFormat
    {
        /// <summary>
        /// An empty spec or one ending in <c>s</c> formats <paramref name="text"/> (<c>str(self)</c>)
        /// under str's rules — Core's one format engine. Otherwise the spec must fully match
        /// <c>(#?)(_?)([xbnX])</c> (<c>n</c> is <c>b</c> for IPv4 and <c>x</c> for IPv6): the
        /// address integer rendered in that radix, zero-padded to the full address width, grouped
        /// every four digits under <c>_</c>, prefixed <c>0b</c>/<c>0x</c>/<c>0X</c> under <c>#</c>.
        /// Anything else is <c>object.__format__</c>'s TypeError.
        /// </summary>
        internal static string Format(
            string text, BigInteger value, int version, int maxPrefixlen, string pyTypeName, string? spec)
        {
            spec = spec ?? "";
            if (spec.Length == 0 || spec[spec.Length - 1] == 's')
            {
                return PyFormat.Apply(text, spec);
            }

            int pos = 0;
            bool alternate = pos < spec.Length && spec[pos] == '#';
            if (alternate)
            {
                pos++;
            }
            bool grouping = pos < spec.Length && spec[pos] == '_';
            if (grouping)
            {
                pos++;
            }
            if (pos != spec.Length - 1 || "xbnX".IndexOf(spec[pos]) < 0)
            {
                throw new TypeError("unsupported format string passed to " + pyTypeName + ".__format__");
            }

            char radixType = spec[pos];
            if (radixType == 'n')
            {
                radixType = version == 4 ? 'b' : 'x';
            }

            // python: format(int(self), f'{alt}0{padlen}{grouping}{base}') with padlen sized for
            // exactly the address width in digits (plus separators and prefix); an address never
            // exceeds that width, so the result is the digits zero-padded to it.
            int radix = radixType == 'b' ? 2 : 16;
            int digitCount = radixType == 'b' ? maxPrefixlen : maxPrefixlen / 4;
            string digits = ToRadix(value, radix, radixType == 'X').PadLeft(digitCount, '0');
            if (grouping)
            {
                digits = GroupByFour(digits);
            }
            string prefix = alternate ? (radixType == 'b' ? "0b" : radixType == 'X' ? "0X" : "0x") : "";
            return prefix + digits;
        }

        private static string ToRadix(BigInteger value, int radix, bool upper)
        {
            if (value.IsZero)
            {
                return "0";
            }
            string alphabet = upper ? "0123456789ABCDEF" : "0123456789abcdef";
            var sb = new StringBuilder();
            while (value > 0)
            {
                sb.Insert(0, alphabet[(int)(value % radix)]);
                value /= radix;
            }
            return sb.ToString();
        }

        private static string GroupByFour(string digits)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 4 == 0)
                {
                    sb.Append('_');
                }
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }
    }
}
