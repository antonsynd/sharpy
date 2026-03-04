using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Represents a resolved type during semantic analysis.
///
/// <para><b>Closed Hierarchy:</b></para>
/// <para>
/// This type hierarchy is CLOSED — all leaf types are sealed to enable exhaustive
/// pattern matching. When switching on SemanticType, ensure all concrete types are
/// handled to avoid silent fallthrough bugs when new types are added.
/// </para>
///
/// <para><b>Design Invariants:</b></para>
/// <list type="bullet">
/// <item><description>
/// SemanticType is IMMUTABLE - once created, it never changes.
/// </description></item>
/// <item><description>
/// SemanticType represents TYPE USAGE, not TYPE DECLARATION.
/// For declarations, see TypeSymbol.
/// </description></item>
/// <item><description>
/// User-defined types (UserDefinedType) always reference their declaring TypeSymbol.
/// </description></item>
/// <item><description>
/// Generic types (GenericType) contain resolved type arguments, not parameters.
/// </description></item>
/// </list>
///
/// <para><b>Relationship to Other Types:</b></para>
/// <list type="bullet">
/// <item><description>
/// TypeAnnotation (AST) → resolved by TypeResolver → SemanticType
/// </description></item>
/// <item><description>
/// TypeSymbol (Symbol) → used by → UserDefinedType.Symbol
/// </description></item>
/// </list>
///
/// <para><b>Future Extensions (v0.2.x):</b></para>
/// <list type="bullet">
/// <item><description>
/// UnionType - for tagged unions / ADTs
/// </description></item>
/// <item><description>
/// TaskType - for async functions returning Task&lt;T&gt;
/// </description></item>
/// </list>
/// </summary>
public abstract record SemanticType : ITypeInfo
{
    // Singleton instances for common types
    public static readonly SemanticType Unknown = new UnknownType();
    public static readonly SemanticType Void = new VoidType();
    public static readonly SemanticType Int = new BuiltinType { Name = "int", ClrType = typeof(int) };
    public static readonly SemanticType Long = new BuiltinType { Name = "long", ClrType = typeof(long) };
    // Per spec: Sharpy 'float' maps to C# 'double' (64-bit), 'float32' maps to C# 'float' (32-bit)
    public static readonly SemanticType Float = new BuiltinType { Name = "float", ClrType = typeof(double) };
    public static readonly SemanticType Double = new BuiltinType { Name = "double", ClrType = typeof(double) };
    public static readonly SemanticType Float32 = new BuiltinType { Name = "float32", ClrType = typeof(float) };
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

    // ITypeInfo implementation
    string ITypeInfo.DisplayName => GetDisplayName();

    /// <summary>
    /// Whether this type can hold null values. Override in NullableType.
    /// </summary>
    public virtual bool IsNullable => false;

    /// <summary>
    /// Whether this is a value type. Override in BuiltinType and UserDefinedType.
    /// </summary>
    public virtual bool IsValueType => false;

    /// <summary>
    /// The CLR type, if known. Override in BuiltinType.
    /// </summary>
    public virtual Type? ClrType { get => null; }

    /// <summary>
    /// The declaring TypeSymbol. Override in UserDefinedType.
    /// </summary>
    public virtual TypeSymbol? DeclaringSymbol => null;

    /// <summary>
    /// Check if this type is assignable to another ITypeInfo.
    /// </summary>
    public virtual bool IsAssignableTo(ITypeInfo other)
    {
        if (other is SemanticType semanticType)
            return this.IsAssignableTo(semanticType);
        return false;
    }

    /// <summary>
    /// Create a nullable version of this type.
    /// </summary>
    public virtual ITypeInfo MakeNullable()
    {
        if (this is NullableType)
            return this;
        return new NullableType { UnderlyingType = this };
    }

    /// <summary>
    /// Unwrap nullable type. Returns the underlying type if nullable, otherwise this.
    /// </summary>
    public virtual ITypeInfo UnwrapNullable()
    {
        if (this is NullableType nullable)
            return nullable.UnderlyingType;
        return this;
    }
}

/// <summary>
/// Unknown type (used for error recovery)
/// </summary>
public sealed record UnknownType : SemanticType
{
    public override string GetDisplayName() => "<?>";

    public override bool IsAssignableTo(SemanticType other) => true; // Allow anything to avoid cascading errors
}

/// <summary>
/// Void type (for functions that don't return a value)
/// </summary>
public sealed record VoidType : SemanticType
{
    public override string GetDisplayName() => "None";

    public override bool IsAssignableTo(SemanticType other)
    {
        // Bare None is the C# null literal — only valid for nullable types (T | None)
        // For optional types (T?), use None() instead
        if (other is NullableType)
            return true;

        return base.IsAssignableTo(other);
    }
}

/// <summary>
/// Built-in primitive type
/// </summary>
public sealed record BuiltinType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public new Type? ClrType { get; init; }

    public override string GetDisplayName() => Name;

    public override bool IsValueType => ClrType?.IsValueType ?? false;

    public override bool IsAssignableTo(SemanticType other)
    {
        if (base.IsAssignableTo(other))
            return true;

        // Use PrimitiveCatalog for implicit conversion rules
        var thisInfo = PrimitiveCatalog.GetPrimitiveInfo(this);
        var otherInfo = PrimitiveCatalog.GetPrimitiveInfo(other);

        if (thisInfo != null && otherInfo != null)
        {
            return PrimitiveCatalog.CanImplicitlyConvert(thisInfo, otherInfo);
        }

        return false;
    }
}

/// <summary>
/// Generic type with type arguments (e.g., list[int])
/// </summary>
public sealed record GenericType : SemanticType
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
                // UnknownType acts as wildcard — allows empty collection literals
                // (dict[<?>,<?>], list[<?>], set[<?>]) to be assigned to any
                // compatible annotated type.
                if (TypeArguments[i] is UnknownType || otherGeneric.TypeArguments[i] is UnknownType)
                    continue;
                if (!TypeArguments[i].Equals(otherGeneric.TypeArguments[i]))
                    return false;
            }
            return true;
        }

        return base.IsAssignableTo(other);
    }

    // Override Equals and GetHashCode to compare TypeArguments by content
    // This improves cache effectiveness in operator validation
    public bool Equals(GenericType? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        if (Name != other.Name || TypeArguments.Count != other.TypeArguments.Count)
            return false;

        if (!EqualityComparer<TypeSymbol?>.Default.Equals(GenericDefinition, other.GenericDefinition))
            return false;

        for (int i = 0; i < TypeArguments.Count; i++)
        {
            if (!TypeArguments[i].Equals(other.TypeArguments[i]))
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name);
        hash.Add(GenericDefinition);
        foreach (var arg in TypeArguments)
        {
            hash.Add(arg);
        }
        return hash.ToHashCode();
    }
}

/// <summary>
/// User-defined type (class, struct, interface)
/// </summary>
public sealed record UserDefinedType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? Symbol { get; init; }

    public override string GetDisplayName() => Name;

    public override bool IsValueType => Symbol?.TypeKind == TypeKind.Struct;

    public override TypeSymbol? DeclaringSymbol => Symbol;

    public override Type? ClrType => Symbol?.ClrType;

    /// <remarks>
    /// Note: This method reads Symbol.BaseType and Symbol.Interfaces directly rather than
    /// going through SemanticBinding, because SemanticType is a data type without access to
    /// the binding store. This is safe because materialization (MaterializeInheritance) copies
    /// data from SemanticBinding onto Symbol properties at the inheritance freeze point, which
    /// occurs before type checking when this method is called.
    /// </remarks>
    public override bool IsAssignableTo(SemanticType other)
    {
        if (base.IsAssignableTo(other))
            return true;

        if (other is UserDefinedType otherUdt)
        {
            // Name-based comparison works even when Symbol is null
            // (e.g., imported function return types don't have Symbol resolved)
            if (Name == otherUdt.Name)
                return true;

            if (Symbol != null)
            {
                // Reference-based same-type check
                if (Symbol == otherUdt.Symbol)
                    return true;

                // Check inheritance chain
                var current = Symbol.BaseType;
                while (current != null)
                {
                    if (current == otherUdt.Symbol || current.Name == otherUdt.Name)
                        return true;
                    current = current.BaseType;
                }

                // Check all interfaces (including inherited from base classes)
                return ImplementsInterface(Symbol, otherUdt);
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a type implements an interface, including interfaces
    /// inherited from base classes and interface inheritance chains.
    /// </summary>
    /// <remarks>
    /// Reads Symbol.BaseType/Interfaces directly (see IsAssignableTo remarks for rationale).
    /// </remarks>
    private static bool ImplementsInterface(TypeSymbol type, UserDefinedType targetInterface)
    {
        // Use BFS to search all interfaces in the hierarchy
        var visited = new HashSet<string>();
        var queue = new Queue<TypeSymbol>();

        // Add direct interfaces of the type and all its base classes
        var currentType = type;
        while (currentType != null)
        {
            foreach (var ifaceRef in currentType.Interfaces)
            {
                queue.Enqueue(ifaceRef.Definition);
            }
            currentType = currentType.BaseType;
        }

        // BFS through interface inheritance
        while (queue.Count > 0)
        {
            var iface = queue.Dequeue();
            if (!visited.Add(iface.Name))
                continue;

            // Check if this is the target interface
            if (iface == targetInterface.Symbol || iface.Name == targetInterface.Name)
                return true;

            // Add base interfaces to the queue
            foreach (var baseIfaceRef in iface.Interfaces)
            {
                queue.Enqueue(baseIfaceRef.Definition);
            }
        }

        return false;
    }
}

/// <summary>
/// Optional type (T? → Optional[T]).
/// This is a SAFE tagged union, distinct from NullableType (C# nullable interop).
///
/// <para><b>Semantic Meaning:</b></para>
/// <list type="bullet">
/// <item><description>Represents Sharpy's native optional value</description></item>
/// <item><description>Maps to Sharpy.Optional&lt;T&gt; struct</description></item>
/// <item><description>Zero heap allocation</description></item>
/// <item><description>Uses Some(value) / None() cases</description></item>
/// </list>
/// </summary>
public sealed record OptionalType : SemanticType
{
    /// <summary>
    /// The underlying type T in Optional[T].
    /// </summary>
    public SemanticType UnderlyingType { get; init; } = SemanticType.Unknown;

    public override string GetDisplayName() => $"{UnderlyingType.GetDisplayName()}?";

    /// <summary>
    /// Optional types can hold an empty value (None) which is conceptually similar to null.
    /// </summary>
    public override bool IsNullable => true;

    public override bool IsValueType => true; // Optional<T> is a struct

    public override bool IsAssignableTo(SemanticType other)
    {
        // OptionalType is assignable to same OptionalType
        if (other is OptionalType otherOpt)
            return UnderlyingType.IsAssignableTo(otherOpt.UnderlyingType);

        // OptionalType is NOT assignable to NullableType or raw type
        // (explicit conversion needed)

        return base.IsAssignableTo(other);
    }

    public override ITypeInfo MakeNullable()
    {
        // Optional<T> | None → NullableType wrapping OptionalType
        return new NullableType { UnderlyingType = this };
    }

    public override ITypeInfo UnwrapNullable()
    {
        // OptionalType is not a nullable type in the C# sense
        return this;
    }
}

/// <summary>
/// Result type (T !E → Result[T, E]).
/// This is a SAFE tagged union for error handling.
///
/// <para><b>Semantic Meaning:</b></para>
/// <list type="bullet">
/// <item><description>Represents Sharpy's native result/error type</description></item>
/// <item><description>Maps to Sharpy.Result&lt;T, E&gt; struct</description></item>
/// <item><description>Zero heap allocation</description></item>
/// <item><description>Uses Ok(value) / Err(error) cases</description></item>
/// </list>
/// </summary>
public sealed record ResultType : SemanticType
{
    /// <summary>
    /// The success type T in Result[T, E].
    /// </summary>
    public SemanticType OkType { get; init; } = SemanticType.Unknown;

    /// <summary>
    /// The error type E in Result[T, E].
    /// </summary>
    public SemanticType ErrorType { get; init; } = SemanticType.Unknown;

    public override string GetDisplayName() => $"{OkType.GetDisplayName()} !{ErrorType.GetDisplayName()}";

    public override bool IsValueType => true; // Result<T, E> is a struct

    public override bool IsAssignableTo(SemanticType other)
    {
        // ResultType is assignable to same ResultType with compatible types
        if (other is ResultType otherResult)
            return OkType.IsAssignableTo(otherResult.OkType)
                && ErrorType.IsAssignableTo(otherResult.ErrorType);

        return base.IsAssignableTo(other);
    }

    public override ITypeInfo MakeNullable()
    {
        // Result<T, E> | None → NullableType wrapping ResultType
        return new NullableType { UnderlyingType = this };
    }
}

/// <summary>
/// C# nullable type (T | None).
/// This represents .NET nullable reference types or Nullable&lt;T&gt; for value types.
///
/// <para><b>Semantic Meaning:</b></para>
/// <list type="bullet">
/// <item><description>Used for .NET interop when APIs return/accept null</description></item>
/// <item><description>Maps to C# T? (nullable reference) or Nullable&lt;T&gt;</description></item>
/// <item><description>NOT the same as OptionalType (T? Sharpy syntax)</description></item>
/// </list>
///
/// <para><b>Distinction from OptionalType:</b></para>
/// <list type="bullet">
/// <item><description>NullableType: C# null semantics, for .NET interop</description></item>
/// <item><description>OptionalType: Safe tagged union, for Sharpy-native code</description></item>
/// </list>
/// </summary>
public sealed record NullableType : SemanticType
{
    public SemanticType UnderlyingType { get; init; } = SemanticType.Unknown;

    public override string GetDisplayName() => $"{UnderlyingType.GetDisplayName()}?";

    public override bool IsNullable => true;

    public override bool IsValueType => UnderlyingType is SemanticType st && st.IsValueType;

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
public sealed record FunctionType : SemanticType
{
    public List<SemanticType> ParameterTypes { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Void;

    /// <summary>
    /// Number of trailing parameters that have default values and can be omitted at call sites.
    /// For example, a lambda <c>(x: int, y: int = 10) -> x + y</c> has OptionalParameterCount = 1.
    /// The required parameter count is <c>ParameterTypes.Count - OptionalParameterCount</c>.
    /// </summary>
    public int OptionalParameterCount { get; init; } = 0;

    /// <summary>
    /// When true, argument validation is skipped (used for .NET types with multiple
    /// constructor overloads where we can't do proper overload resolution).
    /// The C# compiler will handle overload resolution at compile time.
    /// </summary>
    public bool SkipArgumentValidation { get; init; } = false;

    public override string GetDisplayName()
    {
        var params_ = string.Join(", ", ParameterTypes.Select(p => p.GetDisplayName()));
        return $"({params_}) -> {ReturnType.GetDisplayName()}";
    }

    public override bool IsAssignableTo(SemanticType other)
    {
        if (other is FunctionType otherFunc
            && ParameterTypes.Count == otherFunc.ParameterTypes.Count)
        {
            // Parameter types: contravariant (other's param assignable to this param)
            for (int i = 0; i < ParameterTypes.Count; i++)
            {
                if (!ParameterTypes[i].IsAssignableTo(otherFunc.ParameterTypes[i])
                    && !otherFunc.ParameterTypes[i].IsAssignableTo(ParameterTypes[i]))
                    return false;
            }
            // Return type: covariant
            if (!ReturnType.IsAssignableTo(otherFunc.ReturnType))
                return false;
            return true;
        }
        return base.IsAssignableTo(other);
    }

    // Override Equals and GetHashCode to compare ParameterTypes by content
    public bool Equals(FunctionType? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        if (ParameterTypes.Count != other.ParameterTypes.Count)
            return false;

        for (int i = 0; i < ParameterTypes.Count; i++)
        {
            if (!ParameterTypes[i].Equals(other.ParameterTypes[i]))
                return false;
        }

        return ReturnType.Equals(other.ReturnType)
            && SkipArgumentValidation == other.SkipArgumentValidation;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var param in ParameterTypes)
        {
            hash.Add(param);
        }
        hash.Add(ReturnType);
        hash.Add(SkipArgumentValidation);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Returns true if any parameter type or the return type is UnknownType,
    /// indicating the type was not fully resolved (e.g., bare lambda without context).
    /// </summary>
    public bool HasUnresolvedTypes()
    {
        return ParameterTypes.Any(p => p is UnknownType)
            || ReturnType is UnknownType;
    }
}

/// <summary>
/// Tuple type
/// </summary>
public sealed record TupleType : SemanticType
{
    public List<SemanticType> ElementTypes { get; init; } = new();

    /// <summary>
    /// Element names for named tuples. Null means unnamed (backward compatible).
    /// When present, must have the same count as ElementTypes.
    /// </summary>
    public ImmutableArray<string?>? ElementNames { get; init; }

    /// <summary>
    /// Whether this is a named tuple (has element names).
    /// </summary>
    public bool IsNamed => ElementNames != null && ElementNames.Value.Length > 0;

    public override string GetDisplayName()
    {
        if (IsNamed)
        {
            var elements = ElementTypes.Select((e, i) =>
                $"{ElementNames!.Value[i]}: {e.GetDisplayName()}");
            return $"tuple[{string.Join(", ", elements)}]";
        }
        var unnamed = string.Join(", ", ElementTypes.Select(e => e.GetDisplayName()));
        return $"tuple[{unnamed}]";
    }

    public override bool IsAssignableTo(SemanticType other)
    {
        if (other is TupleType otherTuple && ElementTypes.Count == otherTuple.ElementTypes.Count)
        {
            for (int i = 0; i < ElementTypes.Count; i++)
            {
                if (!ElementTypes[i].IsAssignableTo(otherTuple.ElementTypes[i]))
                    return false;
            }
            return true;
        }
        return base.IsAssignableTo(other);
    }

    // Override Equals and GetHashCode to compare ElementTypes by content
    public bool Equals(TupleType? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;

        if (ElementTypes.Count != other.ElementTypes.Count)
            return false;

        for (int i = 0; i < ElementTypes.Count; i++)
        {
            if (!ElementTypes[i].Equals(other.ElementTypes[i]))
                return false;
        }

        // Compare element names
        if (IsNamed != other.IsNamed)
            return false;
        if (IsNamed && other.IsNamed)
        {
            for (int i = 0; i < ElementNames!.Value.Length; i++)
            {
                if (ElementNames.Value[i] != other.ElementNames!.Value[i])
                    return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var elem in ElementTypes)
        {
            hash.Add(elem);
        }
        if (IsNamed)
        {
            foreach (var name in ElementNames!.Value)
            {
                hash.Add(name);
            }
        }
        return hash.ToHashCode();
    }
}

/// <summary>
/// Module type (for imported modules used as namespaces)
/// </summary>
public sealed record ModuleType : SemanticType
{
    public ModuleSymbol Symbol { get; init; } = null!;

    public override string GetDisplayName() => $"module '{Symbol.Name}'";
}

/// <summary>
/// Type parameter type (e.g., T in class Box[T])
/// Used during type checking within generic classes/structs
/// </summary>
public sealed record TypeParameterType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? DeclaringType { get; init; }

    /// <summary>
    /// Constraint clauses for this type parameter (e.g., T: IComparable)
    /// </summary>
    public ImmutableArray<ConstraintClause> Constraints { get; init; }
        = ImmutableArray<ConstraintClause>.Empty;

    /// <summary>
    /// Variance annotation (None, Covariant, Contravariant) for delegate/interface type parameters.
    /// </summary>
    public TypeParameterVariance Variance { get; init; } = TypeParameterVariance.None;

    public override string GetDisplayName() => Name;

    public override bool IsAssignableTo(SemanticType other)
    {
        // Type parameters can be assigned to themselves
        if (other is TypeParameterType otherParam && Name == otherParam.Name)
            return true;

        // Type parameters can be assigned to object
        return base.IsAssignableTo(other);
    }
}

/// <summary>
/// Represents a generic function that has been instantiated with type arguments
/// (e.g., identity[int] from def identity[T](value: T) -> T)
/// This is an internal type used to pass type arguments from IndexAccess to FunctionCall
/// </summary>
public sealed record GenericFunctionType : SemanticType
{
    public FunctionSymbol FunctionSymbol { get; init; } = null!;
    public List<SemanticType> TypeArguments { get; init; } = new();

    public override string GetDisplayName()
    {
        var typeArgs = string.Join(", ", TypeArguments.Select(t => t.GetDisplayName()));
        var paramTypes = string.Join(", ", FunctionSymbol.Parameters.Select(p => p.Type.GetDisplayName()));
        return $"{FunctionSymbol.Name}[{typeArgs}]({paramTypes}) -> {FunctionSymbol.ReturnType.GetDisplayName()}";
    }
}

/// <summary>
/// Represents a tagged union type (v0.2.x feature).
/// Example: Result[T, E] with cases Ok(T) and Err(E)
///
/// <para><b>This is a placeholder for future implementation.</b></para>
///
/// Usage pattern (v0.2.x):
/// <code>
/// type Result[T, E]:
///     Ok(value: T)
///     Err(error: E)
///
/// def divide(a: int, b: int) -> Result[int, str]:
///     if b == 0:
///         return Err("division by zero")
///     return Ok(a / b)
/// </code>
/// </summary>
public sealed record UnionType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? Symbol { get; init; }

    /// <summary>
    /// The union case types. Each case is itself a type.
    /// </summary>
    public List<SemanticType> CaseTypes { get; init; } = new();

    public override string GetDisplayName() => Name;

    public override TypeSymbol? DeclaringSymbol => Symbol;

    public override bool IsAssignableTo(SemanticType other)
    {
        if (other is UnionType otherUnion && Name == otherUnion.Name)
            return true;
        return base.IsAssignableTo(other);
    }
}

/// <summary>
/// Represents an async Task type (v0.2.x feature).
/// Wraps the return type of async functions.
///
/// <para><b>This is a placeholder for future implementation.</b></para>
///
/// Usage pattern (v0.2.x):
/// <code>
/// async def fetch_data(url: str) -> str:
///     response = await http_get(url)
///     return response.body
/// </code>
/// </summary>
public sealed record TaskType : SemanticType
{
    /// <summary>
    /// The result type (T in Task&lt;T&gt;). Null for Task (void return).
    /// </summary>
    public SemanticType? ResultType { get; init; }

    public override string GetDisplayName()
    {
        if (ResultType == null)
            return "Task";
        return $"Task[{ResultType.GetDisplayName()}]";
    }

    /// <summary>
    /// Task&lt;T&gt; is assignable to Task (mirrors C# where Task&lt;T&gt; inherits Task).
    /// </summary>
    public override bool IsAssignableTo(SemanticType other)
    {
        if (other is TaskType otherTask)
        {
            // Task<T> → Task (non-generic) is always valid (C# inheritance)
            if (otherTask.ResultType == null)
                return true;
            // Task<T> → Task<U> requires T assignable to U
            if (ResultType != null)
                return ResultType.IsAssignableTo(otherTask.ResultType);
        }
        return base.IsAssignableTo(other);
    }

    public override Type? ClrType =>
        ResultType == null
            ? typeof(System.Threading.Tasks.Task)
            : null; // Generic Task<T> needs runtime resolution
}
