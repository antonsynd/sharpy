namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Represents a type in the semantic analysis phase
/// </summary>
public abstract record SemanticType
{
    // Singleton instances for common types
    public static readonly SemanticType Unknown = new UnknownType();
    public static readonly SemanticType Void = new VoidType();
    public static readonly SemanticType Int = new BuiltinType { Name = "int", ClrType = typeof(int) };
    public static readonly SemanticType Long = new BuiltinType { Name = "long", ClrType = typeof(long) };
    public static readonly SemanticType Float = new BuiltinType { Name = "float", ClrType = typeof(float) };
    public static readonly SemanticType Double = new BuiltinType { Name = "double", ClrType = typeof(double) };
    public static readonly SemanticType Bool = new BuiltinType { Name = "bool", ClrType = typeof(bool) };
    public static readonly SemanticType Str = new BuiltinType { Name = "str", ClrType = typeof(string) };
    public static readonly SemanticType Object = new UserDefinedType { Name = "object" };

    /// <summary>
    /// Check if this type is assignable to another type
    /// </summary>
    public virtual bool IsAssignableTo(SemanticType other)
    {
        // All types are assignable to object
        if (other is UserDefinedType { Name: "object" })
            return true;

        return this.Equals(other);
    }

    /// <summary>
    /// Get a human-readable name for this type
    /// </summary>
    public abstract string GetDisplayName();
}

/// <summary>
/// Unknown type (used for error recovery)
/// </summary>
public record UnknownType : SemanticType
{
    public override string GetDisplayName() => "<?>";

    public override bool IsAssignableTo(SemanticType other) => true; // Allow anything to avoid cascading errors
}

/// <summary>
/// Void type (for functions that don't return a value)
/// </summary>
public record VoidType : SemanticType
{
    public override string GetDisplayName() => "None";

    public override bool IsAssignableTo(SemanticType other)
    {
        // None can be assigned to any nullable type
        if (other is NullableType)
            return true;

        return base.IsAssignableTo(other);
    }
}

/// <summary>
/// Built-in primitive type
/// </summary>
public record BuiltinType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public Type? ClrType { get; init; }

    public override string GetDisplayName() => Name;

    public override bool IsAssignableTo(SemanticType other)
    {
        if (base.IsAssignableTo(other)) return true;

        // Handle numeric conversions
        if (this == Int && other == Long) return true;
        if (this == Int && other == Float) return true;
        if (this == Int && other == Double) return true;
        if (this == Float && other == Double) return true;
        if (this == Long && other == Double) return true;

        return false;
    }
}

/// <summary>
/// Generic type with type arguments (e.g., list[int])
/// </summary>
public record GenericType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public List<SemanticType> TypeArguments { get; init; } = new();
    public TypeSymbol? GenericDefinition { get; init; }

    public override string GetDisplayName()
    {
        var args = string.Join(", ", TypeArguments.Select(t => t.GetDisplayName()));
        return $"{Name}[{args}]";
    }

    public override bool IsAssignableTo(SemanticType other)
    {
        if (other is GenericType otherGeneric
            && Name == otherGeneric.Name
            && TypeArguments.Count == otherGeneric.TypeArguments.Count)
        {
            // Check covariance/contravariance rules here in future
            // For now, check if type arguments match exactly
            for (int i = 0; i < TypeArguments.Count; i++)
            {
                if (!TypeArguments[i].Equals(otherGeneric.TypeArguments[i]))
                    return false;
            }
            return true;
        }

        return base.IsAssignableTo(other);
    }
}

/// <summary>
/// User-defined type (class, struct, interface)
/// </summary>
public record UserDefinedType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? Symbol { get; init; }

    public override string GetDisplayName() => Name;

    public override bool IsAssignableTo(SemanticType other)
    {
        if (base.IsAssignableTo(other)) return true;

        if (other is UserDefinedType otherUdt && Symbol != null)
        {
            // Same type
            if (Symbol == otherUdt.Symbol || Name == otherUdt.Name)
                return true;

            // Check inheritance chain
            var current = Symbol.BaseType;
            while (current != null)
            {
                if (current == otherUdt.Symbol || current.Name == otherUdt.Name)
                    return true;
                current = current.BaseType;
            }

            // Check interfaces
            return Symbol.Interfaces.Any(i => i == otherUdt.Symbol || i.Name == otherUdt.Name);
        }

        return false;
    }
}

/// <summary>
/// Nullable type (T?)
/// </summary>
public record NullableType : SemanticType
{
    public SemanticType UnderlyingType { get; init; } = SemanticType.Unknown;

    public override string GetDisplayName() => $"{UnderlyingType.GetDisplayName()}?";

    public override bool IsAssignableTo(SemanticType other)
    {
        // Nullable T is assignable to T (implicit unwrapping)
        if (UnderlyingType.IsAssignableTo(other))
            return true;

        // Nullable T is assignable to Nullable T
        if (other is NullableType otherNullable)
            return UnderlyingType.IsAssignableTo(otherNullable.UnderlyingType);

        return base.IsAssignableTo(other);
    }
}

/// <summary>
/// Function type (for lambdas and delegates)
/// </summary>
public record FunctionType : SemanticType
{
    public List<SemanticType> ParameterTypes { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Void;

    public override string GetDisplayName()
    {
        var params_ = string.Join(", ", ParameterTypes.Select(p => p.GetDisplayName()));
        return $"({params_}) -> {ReturnType.GetDisplayName()}";
    }
}

/// <summary>
/// Tuple type
/// </summary>
public record TupleType : SemanticType
{
    public List<SemanticType> ElementTypes { get; init; } = new();

    public override string GetDisplayName()
    {
        var elements = string.Join(", ", ElementTypes.Select(e => e.GetDisplayName()));
        return $"tuple[{elements}]";
    }
}
