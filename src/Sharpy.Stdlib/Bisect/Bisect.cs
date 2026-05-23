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
    public static partial class BisectModule
    {
        public static int BisectLeft<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            if (hi == -1)
            {
                hi = global::Sharpy.Builtins.Len(a);
            }

            while (lo < hi)
            {
                int mid = lo + (2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)System.Math.Floor((double)((double)((hi - lo)) / 2)));
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

        public static int BisectRight<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            if (hi == -1)
            {
                hi = global::Sharpy.Builtins.Len(a);
            }

            while (lo < hi)
            {
                int mid = lo + (2 == 0 ? throw new global::Sharpy.ZeroDivisionError("integer division or modulo by zero") : (int)System.Math.Floor((double)((double)((hi - lo)) / 2)));
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

        public static int Bisect<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            return BisectRight(a, x, lo, hi);
        }

        public static void InsortLeft<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            int idx = BisectLeft(a, x, lo, hi);
            a.Insert(idx, x);
        }

        public static void InsortRight<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            int idx = BisectRight(a, x, lo, hi);
            a.Insert(idx, x);
        }

        public static void Insort<T>(Sharpy.List<T> a, T x, int lo = 0, int hi = -1)
            where T : global::System.IComparable<T>
        {
            InsortRight(a, x, lo, hi);
        }
    }
}
