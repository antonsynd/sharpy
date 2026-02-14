namespace Sharpy
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Runtime helper for exception chaining (raise X from Y).
    /// Sets the inner exception via reflection when the exception
    /// is already constructed (not a constructor call).
    /// </summary>
    public static class ExceptionHelper
    {
        private static readonly FieldInfo? InnerExceptionField =
            typeof(Exception).GetField("_innerException", BindingFlags.NonPublic | BindingFlags.Instance);

        public static T WithCause<T>(T exception, Exception cause) where T : Exception
        {
            InnerExceptionField?.SetValue(exception, cause);
            return exception;
        }
    }
}
