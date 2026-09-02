using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Family guard for member-kind dispatch sites in <c>EventValidator</c>,
/// <c>PropertyValidator</c>, and <c>FinalFieldValidator</c>.
///
/// These validators dispatch on <c>Statement</c> members inside a type body to collect
/// or validate specific member kinds (events, properties, final fields). Each site handles
/// a strict subset of Statement types and silently skips the rest — the skip is contractual
/// (unknown member kinds are not this validator's concern), but the handled subset must not
/// drift: a new member-carrying Statement kind that should be validated by a particular
/// validator would silently pass if its arm is missing.
///
/// Each fact pins one site's arm set via <see cref="SwitchArmScan"/>; a deleted arm fails red.
/// Sites with identical arm sets document that relationship.
/// </summary>
public class MemberKindValidatorTotalityTests
{
    private readonly ITestOutputHelper _output;

    public MemberKindValidatorTotalityTests(ITestOutputHelper output) => _output = output;

    private void AssertArmsMatch(
        string file, string method, HashSet<string> expected, string label)
    {
        var arms = SwitchArmScan.CaseTypeNames(file, method);
        Assert.NotEmpty(arms);
        _output.WriteLine($"{label} arms: {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(expected),
            $"{label}: arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(expected))}\n" +
            $"  Missing: {string.Join(", ", expected.Except(arms))}");
    }

    // ══════════════════════════════════════════════════════════════════════
    // EventValidator — collects and validates event definitions
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <c>EnumerateAllEvents</c> walks <c>stmt.UnwrapDecorated()</c> to collect every
    /// <see cref="Sharpy.Compiler.Parser.Ast.EventDef"/> at any nesting depth.
    /// Arms: the event kind itself plus every type-container that can host nested events.
    /// </summary>
    [Fact]
    public void EnumerateAllEvents_Arms()
    {
        AssertArmsMatch(
            "src/Sharpy.Compiler/Semantic/Validation/EventValidator.cs",
            "EnumerateAllEvents",
            new HashSet<string> { "EventDef", "ClassDef", "StructDef", "InterfaceDef" },
            "EventValidator.EnumerateAllEvents");
    }

    /// <summary>
    /// <c>ValidateInterfaceEvents</c> dispatches on <c>member</c> to collect fields, methods,
    /// and events for interface-level event validation. Same arm set as
    /// <see cref="EventValidator_ValidateTypeBody_Arms"/> — both collect the member triple.
    /// </summary>
    [Fact]
    public void EventValidator_ValidateInterfaceEvents_Arms()
    {
        AssertArmsMatch(
            "src/Sharpy.Compiler/Semantic/Validation/EventValidator.cs",
            "ValidateInterfaceEvents",
            new HashSet<string> { "VariableDeclaration", "FunctionDef", "EventDef" },
            "EventValidator.ValidateInterfaceEvents");
    }

    /// <summary>
    /// <c>ValidateTypeBody</c> in EventValidator dispatches on <c>member</c> to collect fields,
    /// methods, and events for class/struct-level event validation.
    /// </summary>
    [Fact]
    public void EventValidator_ValidateTypeBody_Arms()
    {
        AssertArmsMatch(
            "src/Sharpy.Compiler/Semantic/Validation/EventValidator.cs",
            "ValidateTypeBody",
            new HashSet<string> { "VariableDeclaration", "FunctionDef", "EventDef" },
            "EventValidator.ValidateTypeBody");
    }

    // ══════════════════════════════════════════════════════════════════════
    // FinalFieldValidator — validates @final field assignment rules
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <c>ValidateTypeBody</c> in FinalFieldValidator dispatches on <c>member</c> to walk
    /// method/property/event bodies for @final field assignments and to recurse into nested
    /// class/struct declarations. Does not handle InterfaceDef — interfaces cannot have @final
    /// fields (no instance state).
    /// </summary>
    [Fact]
    public void FinalFieldValidator_ValidateTypeBody_Arms()
    {
        AssertArmsMatch(
            "src/Sharpy.Compiler/Semantic/Validation/FinalFieldValidator.cs",
            "ValidateTypeBody",
            new HashSet<string> { "FunctionDef", "PropertyDef", "EventDef", "ClassDef", "StructDef" },
            "FinalFieldValidator.ValidateTypeBody");
    }

    // ══════════════════════════════════════════════════════════════════════
    // PropertyValidator — collects and validates property definitions
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <c>EnumerateAllProperties</c> walks <c>stmt.UnwrapDecorated()</c> to collect every
    /// <see cref="Sharpy.Compiler.Parser.Ast.PropertyDef"/> at any nesting depth.
    /// Arms: the property kind itself plus every type-container that can host nested properties.
    /// </summary>
    [Fact]
    public void EnumerateAllProperties_Arms()
    {
        AssertArmsMatch(
            "src/Sharpy.Compiler/Semantic/Validation/PropertyValidator.cs",
            "EnumerateAllProperties",
            new HashSet<string> { "PropertyDef", "ClassDef", "StructDef", "InterfaceDef" },
            "PropertyValidator.EnumerateAllProperties");
    }

    /// <summary>
    /// <c>ValidateTypeBody</c> in PropertyValidator dispatches on <c>member</c> to collect
    /// fields, methods, and properties for property validation.
    /// </summary>
    [Fact]
    public void PropertyValidator_ValidateTypeBody_Arms()
    {
        AssertArmsMatch(
            "src/Sharpy.Compiler/Semantic/Validation/PropertyValidator.cs",
            "ValidateTypeBody",
            new HashSet<string> { "VariableDeclaration", "FunctionDef", "PropertyDef" },
            "PropertyValidator.ValidateTypeBody");
    }

    /// <summary>
    /// <c>ValidateTypeStatement</c> dispatches on <c>stmt.UnwrapDecorated()</c> to select
    /// type containers for property validation recursion — the same three container kinds
    /// as <see cref="EnumerateAllProperties_Arms"/> minus the property kind itself.
    /// </summary>
    [Fact]
    public void PropertyValidator_ValidateTypeStatement_Arms()
    {
        AssertArmsMatch(
            "src/Sharpy.Compiler/Semantic/Validation/PropertyValidator.cs",
            "ValidateTypeStatement",
            new HashSet<string> { "ClassDef", "StructDef", "InterfaceDef" },
            "PropertyValidator.ValidateTypeStatement");
    }
}
