namespace Sharpy {
    public sealed partial class Optional<T>
    {
        public override bool __Bool__()
        {
            return HasValue();
        }

        /// <remarks>
        /// Unlike other <see cref="Object"/> types, optionals are equivalent
        /// if they hold the same value.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public override bool __Eq__(Object other)
        {
            if (other is Optional<T> optional) {
                return __Eq__(optional);
            }

            return false;
        }

        public bool __Eq__(Optional<T> other)
        {
            if (_value is ValueType) {
                return _value.Equals(other);
            }

            return ReferenceEquals(_value, other._value);
        }

        public override string __Repr__() {
            if (_value == null) {
                return "None";
            }

            return Repr(_value);
        }
    }
}
