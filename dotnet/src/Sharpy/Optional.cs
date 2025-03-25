namespace Sharpy
{
    public sealed partial class Optional<T> : Object where T : notnull
    {
        // Type-erased to remove default/null distinction for value and
        // reference types.
        private object? _value;

        public Optional() { }

        public Optional(T? value) { _value = value; }

        public T GetValue()
        {
            if (_value is null)
            {
                throw new ArgumentNullException($"Optional<${typeof(T).Name}> has no value.");
            }

            return (T)_value;
        }

        public void SetValue(T? value)
        {
            _value = value;
        }

        public bool HasValue()
        {
            return _value != null;
        }

        public void Reset()
        {
            _value = null;
        }

        public static bool operator true(Optional<T> optional)
        {
            return optional.__Bool__();
        }

        public static bool operator false(Optional<T> optional)
        {
            return !optional.__Bool__();
        }

        public static implicit operator Optional<T>(T? value)
        {
            return new Optional<T>(value);
        }

        public static bool operator ==(Optional<T> optional, T? value)
        {
            return ReferenceEquals(optional._value, value);
        }

        public static bool operator !=(Optional<T> optional, T? value)
        {
            return !(optional == value);
        }

        /// <remarks>
        /// Equality is only symmetrical if it is symmetrical for T.
        /// </remarks>
        public static bool operator ==(T? value, Optional<T> optional)
        {
            return ReferenceEquals(value, optional._value);
        }

        public static bool operator !=(T? value, Optional<T> optional)
        {
            return !(optional == value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (ReferenceEquals(obj, null))
            {
                return false;
            }

            throw new NotImplementedException();
        }
    }
}
