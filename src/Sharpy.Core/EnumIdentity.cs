using System;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Python's <c>left is right</c> where one operand may be an enum member (#2669, #2680). Enum
        /// members are singletons in Python, so two operands are identical exactly when they are the
        /// same member: the same enum type and the same value. A boxed int-backed member is a fresh
        /// box each time, so <see cref="object.ReferenceEquals"/> cannot see that; any other pair is
        /// compared by reference.
        /// </summary>
        /// <remarks>
        /// <c>is</c> never runs user code. This helper asks only the operands' runtime types and,
        /// for two members of one enum, <see cref="Enum.Equals(object)"/>, which no type can
        /// override; it never calls the operands' own <c>Equals</c>. <c>object.Equals(left, right)</c>
        /// would call <c>left.Equals(right)</c>, which runs a user <c>__eq__</c> when the enum is
        /// the right operand.
        /// A lowering target, not a python builtin: the compiler emits it for <c>is</c> /
        /// <c>is not</c> with an enum operand that is not paired with the same enum type. Both
        /// operands are evaluated by the call site, left first. Negation (<c>is not</c>) is the
        /// caller's.
        /// </remarks>
        /// <param name="left">The left operand of <c>is</c>; <c>null</c> is <c>None</c></param>
        /// <param name="right">The right operand of <c>is</c>; <c>null</c> is <c>None</c></param>
        /// <returns><c>true</c> when both are the same enum member, or both are the same object
        /// (two <c>None</c>s included); otherwise <c>false</c></returns>
        /// <example>
        /// <code>
        /// EnumIdentity(Color.Red, (object)Color.Red)   // true: the same member
        /// EnumIdentity(Color.Red, Size.S)              // false: different enums, equal values
        /// EnumIdentity(loud, Color.Red)                // false, and loud's __eq__ is not called
        /// </code>
        /// </example>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool EnumIdentity(object? left, object? right)
        {
            if (left is Enum || right is Enum)
            {
                return left != null && right != null
                    && left.GetType() == right.GetType()
                    && ((Enum)left).Equals(right);
            }

            return ReferenceEquals(left, right);
        }
    }
}
