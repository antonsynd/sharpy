namespace Sharpy
{
    /// <summary>
    /// The unittest module provides a Pythonic testing API that the Sharpy compiler
    /// transforms into xUnit test infrastructure during code generation.
    /// </summary>
    /// <remarks>
    /// Functions like assert_raises and assert_almost_equal are marker methods —
    /// the compiler recognizes their calls and rewrites them to xUnit assertions.
    /// They should not be called at runtime directly.
    /// </remarks>
    [SharpyModule("unittest")]
    public static class Unittest
    {
        /// <summary>
        /// Marker for assert_raises context manager. The compiler transforms
        /// <c>with assert_raises(ExceptionType): body</c> into
        /// <c>Xunit.Assert.Throws&lt;ExceptionType&gt;(() =&gt; { body })</c>.
        /// </summary>
        /// <remarks>
        /// This method exists for type resolution only. It should never be called at runtime.
        /// If called outside a compiler-transformed context, it throws NotSupportedException.
        /// </remarks>
        /// <param name="exceptionType">The expected exception type.</param>
        /// <param name="match">
        /// Optional regular expression applied to the exception message with
        /// <c>re.search</c> semantics. When provided, the compiler appends a
        /// <c>Xunit.Assert.Matches(match, exception.Message)</c> check after the
        /// <c>Xunit.Assert.Throws&lt;ExceptionType&gt;</c> call.
        /// </param>
        public static AssertRaisesMarker AssertRaises(System.Type exceptionType, string? match = null)
        {
            throw new System.NotSupportedException(
                "assert_raises must be used as a context manager: 'with assert_raises(ExceptionType): ...'");
        }

        /// <summary>
        /// Marker for approx. The compiler transforms
        /// <c>assert x == approx(y)</c> into a tolerance-based
        /// <c>Xunit.Assert.Equal(expected, actual, precision)</c> (when
        /// <c>places</c> is used) or <c>Xunit.Assert.Equal(expected, actual, tolerance)</c>
        /// (when <c>abs</c> is used).
        /// </summary>
        /// <remarks>
        /// This method exists for type resolution only — it returns <see cref="double"/>
        /// so that <c>x == approx(y)</c> type-checks as numeric equality. It should never
        /// be called at runtime. Defaults mirror <see cref="AssertAlmostEqual"/>:
        /// <c>places=7</c>; if both <c>places</c> and <c>abs</c> are supplied,
        /// <c>abs</c> takes precedence.
        /// </remarks>
        public static double Approx(double expected, int places = 7, double abs = 0.0)
        {
            throw new System.NotSupportedException(
                "approx is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Marker for assert_count_equal. The compiler transforms calls to this method
        /// into an order-insensitive comparison
        /// <c>Xunit.Assert.Equal(global::Sharpy.Builtins.Sorted(second), global::Sharpy.Builtins.Sorted(first))</c>,
        /// which preserves element multiplicity (matching Python's
        /// <c>unittest.TestCase.assertCountEqual</c>).
        /// </summary>
        /// <remarks>
        /// This method exists for type resolution only. It should never be called at runtime.
        /// Requires comparable elements; non-comparable elements fail at runtime when the
        /// sorted comparison executes.
        /// </remarks>
        public static void AssertCountEqual(object first, object second)
        {
            throw new System.NotSupportedException(
                "assert_count_equal is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Marker for assert_regex. The compiler transforms calls to this method
        /// into <c>Xunit.Assert.Matches(pattern, text)</c> (note the argument swap:
        /// Sharpy follows Python's <c>assertRegex(text, pattern)</c> order, while xUnit
        /// takes the pattern first).
        /// </summary>
        /// <remarks>
        /// This method exists for type resolution only. It should never be called at runtime.
        /// </remarks>
        public static void AssertRegex(string text, string pattern)
        {
            throw new System.NotSupportedException(
                "assert_regex is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Marker for assert_almost_equal. The compiler transforms calls to this method
        /// into <c>Xunit.Assert.Equal(expected, actual, precision)</c> (digits) or, when
        /// the <c>delta</c> keyword is provided, into an absolute-tolerance check.
        /// </summary>
        /// <remarks>
        /// This method exists for type resolution only. Both <c>places</c> and <c>delta</c>
        /// are accepted; if both are passed at the same call site, <c>delta</c> takes
        /// precedence.
        /// </remarks>
        public static void AssertAlmostEqual(double actual, double expected, int places = 7, double delta = 0.0)
        {
            throw new System.NotSupportedException(
                "assert_almost_equal is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Marker for assert_true. The compiler transforms calls to
        /// <c>Xunit.Assert.True(value)</c>.
        /// </summary>
        public static void AssertTrue(object value)
        {
            throw new System.NotSupportedException(
                "assert_true is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Marker for assert_false. The compiler transforms calls to
        /// <c>Xunit.Assert.False(value)</c>.
        /// </summary>
        public static void AssertFalse(object value)
        {
            throw new System.NotSupportedException(
                "assert_false is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Marker for assert_is_none. The compiler transforms calls to
        /// <c>Xunit.Assert.Null(value)</c>.
        /// </summary>
        public static void AssertIsNone(object value)
        {
            throw new System.NotSupportedException(
                "assert_is_none is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Marker for assert_is_not_none. The compiler transforms calls to
        /// <c>Xunit.Assert.NotNull(value)</c>.
        /// </summary>
        public static void AssertIsNotNone(object value)
        {
            throw new System.NotSupportedException(
                "assert_is_not_none is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Marker for assert_greater. The compiler transforms calls to
        /// <c>Xunit.Assert.True(a &gt; b, ...)</c>.
        /// </summary>
        public static void AssertGreater(object a, object b)
        {
            throw new System.NotSupportedException(
                "assert_greater is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Marker for assert_less. The compiler transforms calls to
        /// <c>Xunit.Assert.True(a &lt; b, ...)</c>.
        /// </summary>
        public static void AssertLess(object a, object b)
        {
            throw new System.NotSupportedException(
                "assert_less is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Marker for assert_in. The compiler transforms calls to
        /// <c>Xunit.Assert.Contains(item, collection)</c>.
        /// </summary>
        public static void AssertIn(object item, object collection)
        {
            throw new System.NotSupportedException(
                "assert_in is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Marker for assert_not_in. The compiler transforms calls to
        /// <c>Xunit.Assert.DoesNotContain(item, collection)</c>.
        /// </summary>
        public static void AssertNotIn(object item, object collection)
        {
            throw new System.NotSupportedException(
                "assert_not_in is a compiler-transformed function and should not be called directly.");
        }

        /// <summary>
        /// Create a <see cref="Sharpy.CapturedOutput"/> context manager that captures
        /// everything written to the console while active. Exposed to Sharpy as
        /// <c>captured_output()</c> and intended for use in a <c>with</c> statement:
        /// <c>with captured_output() as out: ...</c>.
        /// </summary>
        /// <remarks>
        /// Unlike the assertion markers, this is a real runtime helper — it is not
        /// rewritten by the compiler and may be called directly.
        /// </remarks>
        public static CapturedOutput CapturedOutput()
        {
            return new CapturedOutput();
        }
    }
}
