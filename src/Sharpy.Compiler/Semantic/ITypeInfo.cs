namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Common interface for all type representations in the compiler.
/// Provides a unified view regardless of whether the type is:
/// - A built-in primitive (BuiltinType)
/// - A user-defined class/struct (UserDefinedType)
/// - A generic instantiation (GenericType)
/// - A type parameter (TypeParameterType)
///
/// This is a TWO-WAY DOOR: Adding this interface doesn't change existing behavior.
/// It provides a common abstraction for future features.
/// </summary>
public interface ITypeInfo
{
    /// <summary>
    /// Human-readable name for diagnostics and display.
    /// Examples: "int", "list[str]", "MyClass", "T"
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this type can hold null values.
    /// True for nullable types (T?) and reference types.
    /// </summary>
    bool IsNullable { get; }

    /// <summary>
    /// Whether this is a value type (struct, primitive) or reference type (class).
    /// </summary>
    bool IsValueType { get; }

    /// <summary>
    /// The CLR type, if known. Null for type parameters and some user-defined types.
    /// </summary>
    Type? ClrType { get; }

    /// <summary>
    /// The declaring TypeSymbol, if this is a user-defined type.
    /// Null for built-in types and type parameters.
    /// </summary>
    TypeSymbol? DeclaringSymbol { get; }

    /// <summary>
    /// Check if this type is assignable to another type.
    /// Includes subtyping, interface implementation, and implicit conversions.
    /// </summary>
    bool IsAssignableTo(ITypeInfo other);

    /// <summary>
    /// Create a nullable version of this type (T?).
    /// </summary>
    ITypeInfo MakeNullable();

    /// <summary>
    /// If this is a nullable type, get the underlying type. Otherwise returns this.
    /// </summary>
    ITypeInfo UnwrapNullable();
}
