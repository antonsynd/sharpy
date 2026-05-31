// Generated from src/Sharpy.Stdlib/spy/bisect_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/bisect_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Array bisection algorithm for maintaining sorted lists.
    /// </summary>
    public static partial class BisectModule
    {
        /// <summary>
        /// Locate the insertion point for x in a to maintain sorted order.
        /// </summary>
        public static int BisectLeft<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            if (hi == -1)
            {
                hi = global::Sharpy.Builtins.Len(a);
            }

            while (lo < hi)
            {
                int mid = lo + (2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)((hi - lo)) / 2)));
                if (a[mid].CompareTo(x) < 0)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo;
        }

        /// <summary>
        /// Like bisect_left, but returns an insertion point which comes after any existing entries of x.
        /// </summary>
        public static int BisectRight<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            if (hi == -1)
            {
                hi = global::Sharpy.Builtins.Len(a);
            }

            while (lo < hi)
            {
                int mid = lo + (2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)global::System.Math.Floor((double)((double)((hi - lo)) / 2)));
                if (a[mid].CompareTo(x) > 0)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            return lo;
        }

        /// <summary>
        /// Alias for bisect_right.
        /// </summary>
        public static int Bisect<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            return BisectRight(a, x, lo, hi);
        }

        /// <summary>
        /// Insert x in a in sorted order, inserting to the left of any existing entries of x.
        /// </summary>
        public static void InsortLeft<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            int idx = BisectLeft(a, x, lo, hi);
            a.Insert(idx, x);
        }

        /// <summary>
        /// Insert x in a in sorted order, inserting to the right of any existing entries of x.
        /// </summary>
        public static void InsortRight<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            int idx = BisectRight(a, x, lo, hi);
            a.Insert(idx, x);
        }

        /// <summary>
        /// Alias for insort_right.
        /// </summary>
        public static void Insort<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            InsortRight(a, x, lo, hi);
        }
    }
}
