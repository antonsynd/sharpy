using System;

namespace Sharpy
{
    /// <summary>
    /// Marks the class the compiler emits for a string-backed enum (enums.md: CPython's
    /// <c>StrEnum</c>). An integer enum is a CLR <c>enum</c>, which the runtime recognises by
    /// <see cref="Type.IsEnum"/>; a string enum is an ordinary sealed class, and this marker is the CLR
    /// fact that says "enum" about it. It makes the type an enum class to
    /// <see cref="PyFormat"/>: <c>str(type(Mood.HAPPY))</c> is <c>&lt;enum 'Mood'&gt;</c>, and a member
    /// formats as a <c>str</c> subclass, whose refused spec names the class
    /// (<c>Unknown format code 'd' for object of type 'Mood'</c>). A plain <c>Enum</c> member formats
    /// as <c>str(self)</c> instead, and its message names <c>str</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SharpyStrEnumAttribute : Attribute
    {
    }
}
