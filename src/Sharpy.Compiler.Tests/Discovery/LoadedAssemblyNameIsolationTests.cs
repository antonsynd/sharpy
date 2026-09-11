using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Sharpy.Compiler.Discovery;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// One assembly in the process that cannot describe itself must not fail a compilation (#1840).
/// <c>ClrAttributeResolver</c> enumerates the loaded assemblies and reads each simple name; a
/// transient assembly whose file has been replaced throws <see cref="BadImageFormatException"/>
/// ("Index not found") from <see cref="Assembly.GetName()"/>, and that exception escaped into
/// <c>DecoratorValidator</c> as an internal compiler error — nondeterministically, depending on what
/// else the test host had loaded.
///
/// <para>
/// The fault is injected rather than waited for: a test double whose <c>GetName()</c> throws is the
/// only way to make the condition deterministic, and the READABLE double beside it is the positive
/// control — without it, a guard that returned an empty set would pass the isolation assertion.
/// </para>
/// </summary>
public class LoadedAssemblyNameIsolationTests
{
    /// <summary>An assembly that cannot answer <see cref="Assembly.GetName()"/>.</summary>
    private sealed class UnreadableAssembly : Assembly
    {
        private readonly Exception _fault;

        internal UnreadableAssembly(Exception fault) => _fault = fault;

        public override AssemblyName GetName() => throw _fault;

        public override AssemblyName GetName(bool copiedName) => throw _fault;
    }

    /// <summary>An assembly that answers with the name it was given.</summary>
    private sealed class NamedAssembly : Assembly
    {
        private readonly string _name;

        internal NamedAssembly(string name) => _name = name;

        public override AssemblyName GetName() => new(_name);

        public override AssemblyName GetName(bool copiedName) => new(_name);
    }

    public static TheoryData<string, Exception> Faults() => new()
    {
        { "BadImageFormatException", new BadImageFormatException("Index not found. (0x80131124)") },
        { "FileNotFoundException", new FileNotFoundException("gone") },
        { "ReflectionTypeLoadException", new ReflectionTypeLoadException(Array.Empty<Type>(), Array.Empty<Exception?>()) },
        { "NotSupportedException", new NotSupportedException("dynamic") }
    };

    [Theory]
    [MemberData(nameof(Faults))]
    public void AnUnreadableAssembly_IsSkipped_AndTheReadableOnesAreStillCollected(string label, Exception fault)
    {
        var assemblies = new List<Assembly>
        {
            new NamedAssembly("Before.Unreadable"),
            new UnreadableAssembly(fault),
            new NamedAssembly("After.Unreadable")
        };

        var names = ClrAttributeResolver.LoadedSimpleNames(assemblies);

        // The isolation itself: no exception escapes. `label` names the injected fault in the
        // failure message, so a red says WHICH exception type the guard let through.
        Assert.True(names.Count == 2, $"{label}: expected the two readable assemblies, got {names.Count}");

        // The positive control — BOTH sides of the unreadable assembly are collected, so the guard
        // skips one assembly rather than abandoning the enumeration.
        Assert.Contains("Before.Unreadable", names);
        Assert.Contains("After.Unreadable", names);
    }

    /// <summary>
    /// The instrument check: the double really does throw, so the test above is measuring the guard
    /// and not an enumeration that never met a fault.
    /// </summary>
    [Fact]
    public void TheInjectedFault_IsReal()
    {
        var unreadable = new UnreadableAssembly(new BadImageFormatException("Index not found."));

        Assert.Throws<BadImageFormatException>(() => unreadable.GetName());
    }

    /// <summary>
    /// The real process's assemblies still produce a non-empty set: a guard that swallowed everything
    /// would satisfy the injection test and silently stop the namespace-to-assembly matching that
    /// <c>EnsureFrameworkAssembliesLoaded</c> depends on.
    /// </summary>
    [Fact]
    public void TheProcessAssemblies_StillAnswer()
    {
        var names = ClrAttributeResolver.LoadedSimpleNames(AppDomain.CurrentDomain.GetAssemblies());

        Assert.NotEmpty(names);
        Assert.Contains("Sharpy.Compiler", names);
    }
}
