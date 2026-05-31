using System;

namespace Sharpy
{
    /// <summary>N-dimensional array with NumPy-compatible semantics.</summary>
    public partial class NdArray<T>
    {
        /// <summary>Elementwise addition with broadcasting.</summary>
        public static NdArray<T> operator +(NdArray<T> a, NdArray<T> b) =>
            BinaryOp(a, b, BinaryOpKind.Add);

        /// <summary>Elementwise subtraction with broadcasting.</summary>
        public static NdArray<T> operator -(NdArray<T> a, NdArray<T> b) =>
            BinaryOp(a, b, BinaryOpKind.Subtract);

        /// <summary>Elementwise multiplication with broadcasting.</summary>
        public static NdArray<T> operator *(NdArray<T> a, NdArray<T> b) =>
            BinaryOp(a, b, BinaryOpKind.Multiply);

        /// <summary>Elementwise division with broadcasting.</summary>
        public static NdArray<T> operator /(NdArray<T> a, NdArray<T> b) =>
            BinaryOp(a, b, BinaryOpKind.Divide);

        /// <summary>Add a scalar to every element.</summary>
        public static NdArray<T> operator +(NdArray<T> a, T b) => ScalarOp(a, b, BinaryOpKind.Add);

        /// <summary>Subtract a scalar from every element.</summary>
        public static NdArray<T> operator -(NdArray<T> a, T b) => ScalarOp(a, b, BinaryOpKind.Subtract);

        /// <summary>Multiply every element by a scalar.</summary>
        public static NdArray<T> operator *(NdArray<T> a, T b) => ScalarOp(a, b, BinaryOpKind.Multiply);

        /// <summary>Divide every element by a scalar.</summary>
        public static NdArray<T> operator /(NdArray<T> a, T b) => ScalarOp(a, b, BinaryOpKind.Divide);

        /// <summary>Add an array to a scalar (commutative form).</summary>
        public static NdArray<T> operator +(T a, NdArray<T> b) => ScalarOp(b, a, BinaryOpKind.Add);

        /// <summary>Subtract every element from a scalar.</summary>
        public static NdArray<T> operator -(T a, NdArray<T> b) => ScalarOpReverse(a, b, BinaryOpKind.Subtract);

        /// <summary>Multiply a scalar by every element (commutative form).</summary>
        public static NdArray<T> operator *(T a, NdArray<T> b) => ScalarOp(b, a, BinaryOpKind.Multiply);

        /// <summary>Divide a scalar by every element.</summary>
        public static NdArray<T> operator /(T a, NdArray<T> b) => ScalarOpReverse(a, b, BinaryOpKind.Divide);

        /// <summary>Elementwise negation.</summary>
        public static NdArray<T> operator -(NdArray<T> a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var data = new T[a.Size];
            int i = 0;
            var iter = new BroadcastedIterator<T>(a, a.Shape);
            while (!iter.IsDone)
            {
                data[i++] = Negate(iter.Current);
                iter.MoveNext();
            }

            return new NdArray<T>(data, a.Shape);
        }

        internal enum BinaryOpKind
        {
            Add,
            Subtract,
            Multiply,
            Divide
        }

        private static NdArray<T> BinaryOp(NdArray<T> a, NdArray<T> b, BinaryOpKind op)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (b == null)
            {
                throw new ArgumentNullException(nameof(b));
            }

            int[] shape = Broadcasting.BroadcastShapes(a._shape, b._shape);
            int total = 1;
            for (int i = 0; i < shape.Length; i++)
            {
                total = checked(total * shape[i]);
            }

            var data = new T[total];
            var ita = new BroadcastedIterator<T>(a, shape);
            var itb = new BroadcastedIterator<T>(b, shape);

            for (int i = 0; i < total; i++)
            {
                data[i] = Apply(ita.Current, itb.Current, op);
                ita.MoveNext();
                itb.MoveNext();
            }

            return new NdArray<T>(data, shape);
        }

        private static NdArray<T> ScalarOp(NdArray<T> a, T b, BinaryOpKind op)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var data = new T[a.Size];
            var iter = new BroadcastedIterator<T>(a, a.Shape);
            for (int i = 0; i < a.Size; i++)
            {
                data[i] = Apply(iter.Current, b, op);
                iter.MoveNext();
            }

            return new NdArray<T>(data, a.Shape);
        }

        private static NdArray<T> ScalarOpReverse(T a, NdArray<T> b, BinaryOpKind op)
        {
            if (b == null)
            {
                throw new ArgumentNullException(nameof(b));
            }

            var data = new T[b.Size];
            var iter = new BroadcastedIterator<T>(b, b.Shape);
            for (int i = 0; i < b.Size; i++)
            {
                data[i] = Apply(a, iter.Current, op);
                iter.MoveNext();
            }

            return new NdArray<T>(data, b.Shape);
        }

        /// <summary>
        /// Apply <paramref name="op"/> elementwise. Dispatched by T at runtime; currently
        /// supports <c>double</c>, <c>float</c>, <c>int</c>, and <c>long</c>.
        /// </summary>
        private static T Apply(T x, T y, BinaryOpKind op)
        {
            if (typeof(T) == typeof(double))
            {
                double xd = (double)(object)x!;
                double yd = (double)(object)y!;
                double r = op switch
                {
                    BinaryOpKind.Add => xd + yd,
                    BinaryOpKind.Subtract => xd - yd,
                    BinaryOpKind.Multiply => xd * yd,
                    BinaryOpKind.Divide => xd / yd,
                    _ => throw new NotSupportedException($"unsupported op {op}"),
                };
                return (T)(object)r;
            }

            if (typeof(T) == typeof(float))
            {
                float xf = (float)(object)x!;
                float yf = (float)(object)y!;
                float r = op switch
                {
                    BinaryOpKind.Add => xf + yf,
                    BinaryOpKind.Subtract => xf - yf,
                    BinaryOpKind.Multiply => xf * yf,
                    BinaryOpKind.Divide => xf / yf,
                    _ => throw new NotSupportedException($"unsupported op {op}"),
                };
                return (T)(object)r;
            }

            if (typeof(T) == typeof(int))
            {
                int xi = (int)(object)x!;
                int yi = (int)(object)y!;
                int r = op switch
                {
                    BinaryOpKind.Add => xi + yi,
                    BinaryOpKind.Subtract => xi - yi,
                    BinaryOpKind.Multiply => xi * yi,
                    BinaryOpKind.Divide => xi / yi,
                    _ => throw new NotSupportedException($"unsupported op {op}"),
                };
                return (T)(object)r;
            }

            if (typeof(T) == typeof(long))
            {
                long xl = (long)(object)x!;
                long yl = (long)(object)y!;
                long r = op switch
                {
                    BinaryOpKind.Add => xl + yl,
                    BinaryOpKind.Subtract => xl - yl,
                    BinaryOpKind.Multiply => xl * yl,
                    BinaryOpKind.Divide => xl / yl,
                    _ => throw new NotSupportedException($"unsupported op {op}"),
                };
                return (T)(object)r;
            }

            throw new NotSupportedException(
                $"arithmetic on NdArray<{typeof(T).Name}> is not supported");
        }

        private static T Negate(T x)
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)(-(double)(object)x!);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)(-(float)(object)x!);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)(-(int)(object)x!);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)(-(long)(object)x!);
            }

            throw new NotSupportedException(
                $"negation on NdArray<{typeof(T).Name}> is not supported");
        }
    }
}
