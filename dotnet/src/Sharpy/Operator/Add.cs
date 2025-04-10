namespace Sharpy.Operator
{
    public static partial class Exports
    {
        public static T Add<T>(T left, T right) where T : IAddable<T>
        {
            return left.__Add__(right);
        }

        public static T Add<T, U>(T left, U right) where T : IAddable<T, U>
        {
            return left.__Add__(right);
        }

        public static T __Add__<T>(T left, T right) where T : IAddable<T> => Add<T>(left, right);
        public static T __Add__<T, U>(T left, U right) where T : IAddable<T, U> => Add(left, right);
    }
}
