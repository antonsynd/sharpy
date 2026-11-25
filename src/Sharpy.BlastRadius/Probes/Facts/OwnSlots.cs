using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.BlastRadius.Probes;

/// <summary>An instance member a constructor has to give a value: a field, or an auto-property.</summary>
internal sealed record Slot(string Name, string Type, bool IsProperty, bool HasDefault)
{
    public string Describe() => $"{(IsProperty ? "property " : "")}{Name}: {Type}";
}

internal static class OwnSlots
{
    /// <summary>
    /// The instance slots the class itself declares with no initializer: fields, and the
    /// auto-properties that must be given a value (R-RP, R-RU). A slot whose type did not resolve
    /// is left out.
    /// </summary>
    public static List<Slot> Of(ClassDef classDef, TypeSymbol type)
    {
        var slots = new List<Slot>();
        foreach (var member in SyntaxFacts.Members(classDef))
        {
            switch (member)
            {
                case VariableDeclaration { InitialValue: null, IsConst: false, Type: not null } field:
                    if (type.Fields.FirstOrDefault(f => f.Name == field.Name) is { IsStatic: false } symbol
                        && HasDefault(symbol.Type) is { } fieldDefault)
                    {
                        slots.Add(new Slot(field.Name, symbol.Type.GetDisplayName(),
                            IsProperty: false, HasDefault: fieldDefault));
                    }
                    break;

                case PropertyDef { IsFunctionStyle: false, DefaultValue: null, ExplicitInterface: null } property
                    when property.Body.Length == 0:
                    if (type.FindOwnProperty(property.Name) is { IsStatic: false, IsAbstract: false } propertySymbol
                        && HasDefault(propertySymbol.Type) is { } typeDefault)
                    {
                        // A get-only or init auto-property takes no zero default (R-RU, SPY0426);
                        // a plain one does when its type has one (R-RP), and is then no slot.
                        var readOnly = property.Accessor is PropertyAccessor.Get or PropertyAccessor.Init;
                        if (readOnly || !typeDefault)
                        {
                            slots.Add(new Slot(property.Name, propertySymbol.Type.GetDisplayName(),
                                IsProperty: true, HasDefault: false));
                        }
                    }
                    break;
            }
        }

        return slots;
    }

    /// <summary>
    /// Whether a slot of this type may rest on a default (R-RQ): a value type rests on zero,
    /// <c>T | None</c> on <c>None</c>, <c>T?</c> on <c>None()</c>. Null when the type did not resolve.
    /// </summary>
    public static bool? HasDefault(SemanticType type) => type switch
    {
        UnknownType => null,
        OptionalType or NullableType => true,
        // A bare type parameter has no default the rulings name: T may be a reference type.
        TypeParameterType => false,
        // SemanticType.IsValueType knows a struct, and not an enum or a struct with type arguments.
        GenericType { GenericDefinition: { } definition } => IsValueType(definition),
        UserDefinedType { Symbol: { } symbol } => IsValueType(symbol),
        _ => type.IsValueType,
    };

    private static bool IsValueType(TypeSymbol symbol)
        => symbol.ClrType?.IsValueType ?? symbol.TypeKind is TypeKind.Struct or TypeKind.Enum;
}
