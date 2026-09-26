using System.Collections.Generic;
using System;
namespace Sharpy
{
    internal static class ComparerAdapter<T>
    {
        public static readonly IComparer<T> Instance = CreateComparer();

        private static IComparer<T> CreateComparer()
        {
            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            {
                return new TypedIComparableComparer();
            }

            if (typeof(IComparable).IsAssignableFrom(typeof(T)))
            {
                return new UntypedIComparableComparer();
            }

            // Not comparable: refuse at the first COMPARISON, as python does — sorting zero or one
            // element never compares, and the TypeError must be catchable, which a throw from this
            // static initializer is not (it surfaces as TypeInitializationException, #2085).
            return new RefusingComparer();
        }

        private class RefusingComparer : IComparer<T>
        {
            public int Compare(T? x, T? y)
            {
                throw TypeError.OpNotSupported("<", x, y);
            }
        }

        private class TypedIComparableComparer : IComparer<T>
        {
            public int Compare(T? x, T? y)
            {
                // None is refused BEFORE the same-object shortcut: python's `None < None` is a
                // TypeError, so two Nones never compare equal (#2085).
                if (x is null || y is null)
                {
                    throw TypeError.OpNotSupported("<", x, y);
                }

                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                return ((IComparable<T>)x).CompareTo(y);
            }
        }

        private class UntypedIComparableComparer : IComparer<T>
        {
            public int Compare(T? x, T? y)
            {
                // None before the same-object shortcut, as in TypedIComparableComparer (#2085).
                if (x is null || y is null)
                {
                    throw TypeError.OpNotSupported("<", x, y);
                }

                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                return ((IComparable)x).CompareTo(y);
            }
        }
    }
}
