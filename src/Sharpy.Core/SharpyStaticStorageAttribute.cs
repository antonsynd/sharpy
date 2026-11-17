using System;

namespace Sharpy
{
    /// <summary>How a Sharpy module or static value is stored (plan-775c50 D2, D10).</summary>
    public enum StaticStorageKind
    {
        /// <summary>A plain CLR field or property: no initializer runs on first use. The kind of every
        /// member that carries no <see cref="SharpyStaticStorageAttribute"/>.</summary>
        Plain = 0,

        /// <summary>A value with an initializer, run by its host's sweep or by its first use: the
        /// member is a <c>ref</c> (or <c>ref readonly</c>) property whose getter initializes it.</summary>
        Guarded = 1,
    }

    /// <summary>
    /// Records a generated value's <see cref="StaticStorageKind"/> on the member a consumer names
    /// (plan-775c50 D10). Discovery reads the storage of a module imported by reference from this
    /// attribute, never from the member's name or CLR shape: a hand-written <c>public static ref int X</c>
    /// with no attribute is <see cref="StaticStorageKind.Plain"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class SharpyStaticStorageAttribute : Attribute
    {
        /// <summary>The value's storage.</summary>
        public StaticStorageKind Kind { get; }

        /// <summary>Create the attribute with the value's storage.</summary>
        public SharpyStaticStorageAttribute(StaticStorageKind kind)
        {
            Kind = kind;
        }
    }

    /// <summary>
    /// Marks a Sharpy module's load entry, the method an importer calls at the import's position so the
    /// module's sweep runs there (plan-775c50 D5, D10). Discovery finds the entry by this attribute,
    /// never by its name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class SharpyLoadEntryAttribute : Attribute
    {
        /// <summary>Whether the module has a sweep an importer requires: it declares a Guarded value
        /// in any host, or imports a module that has one.</summary>
        public bool HasSweep { get; }

        /// <summary>Create the attribute with the module's sweep fact.</summary>
        public SharpyLoadEntryAttribute(bool hasSweep)
        {
            HasSweep = hasSweep;
        }
    }
}
