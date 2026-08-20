extern alias SharpyRT;
using System;
using System.Linq;
using System.Reflection;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance guard: every public instance member on System.Exception that is not
/// redeclared by a Sharpy exception class must be refused by BuiltinExceptionSurface.
/// Enumerated by reflection so it cannot go stale (#1515).
/// </summary>
public class BuiltinExceptionSurfaceConformanceTests
{
    private static readonly Type[] TestReceiverTypes = new[]
    {
        typeof(SharpyRT::Sharpy.ValueError),
        typeof(Exception),
    };

    private static string[] GetSystemExceptionPropertyNames()
    {
        return typeof(Exception)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => p.Name)
            .Distinct()
            .ToArray();
    }

    private static string[] GetSystemExceptionMethodNames()
    {
        return typeof(Exception)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .Where(m => m.DeclaringType != typeof(object) || m.Name == "ToString" || m.Name == "GetType" || m.Name == "GetHashCode")
            .Select(m => m.Name)
            .Distinct()
            .ToArray();
    }

    [Fact]
    public void AllSystemExceptionProperties_RefusedOnValueError_SnakeCase()
    {
        var type = typeof(SharpyRT::Sharpy.ValueError);
        foreach (var propName in GetSystemExceptionPropertyNames())
        {
            var sharpyName = NameMangler.ToSharpyName(propName, ReverseNameContext.Property);
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(type, sharpyName),
                $"Expected '{sharpyName}' (from '{propName}') to be refused on ValueError");
        }
    }

    [Fact]
    public void AllSystemExceptionProperties_RefusedOnValueError_Verbatim()
    {
        var type = typeof(SharpyRT::Sharpy.ValueError);
        foreach (var propName in GetSystemExceptionPropertyNames())
        {
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(type, propName),
                $"Expected verbatim '{propName}' to be refused on ValueError");
        }
    }

    [Fact]
    public void AllSystemExceptionMethods_RefusedOnValueError_SnakeCase()
    {
        var type = typeof(SharpyRT::Sharpy.ValueError);
        foreach (var methodName in GetSystemExceptionMethodNames())
        {
            var sharpyName = NameMangler.ToSharpyName(methodName, ReverseNameContext.Method);
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(type, sharpyName),
                $"Expected '{sharpyName}' (from '{methodName}') to be refused on ValueError");
        }
    }

    [Fact]
    public void AllSystemExceptionProperties_RefusedOnSystemException_SnakeCase()
    {
        foreach (var propName in GetSystemExceptionPropertyNames())
        {
            var sharpyName = NameMangler.ToSharpyName(propName, ReverseNameContext.Property);
            Assert.True(
                BuiltinExceptionSurface.IsRefusedMember(typeof(Exception), sharpyName),
                $"Expected '{sharpyName}' to be refused on System.Exception");
        }
    }

    // --- Positive controls: ExceptionGroup's Python surface resolves ---

    [Theory]
    [InlineData("message")]
    [InlineData("Message")]
    [InlineData("exceptions")]
    [InlineData("Exceptions")]
    [InlineData("subgroup")]
    [InlineData("Subgroup")]
    [InlineData("split")]
    [InlineData("Split")]
    [InlineData("derive")]
    [InlineData("Derive")]
    public void ExceptionGroup_PythonSurface_IsAllowed(string memberName)
    {
        Assert.False(
            BuiltinExceptionSurface.IsRefusedMember(typeof(SharpyRT::Sharpy.ExceptionGroup), memberName),
            $"ExceptionGroup's Python member '{memberName}' must NOT be refused");
    }

    // --- Negative control: non-builtin CLR exception is never filtered ---

    [Theory]
    [InlineData("message")]
    [InlineData("Message")]
    [InlineData("stack_trace")]
    [InlineData("StackTrace")]
    public void IOException_NeverRefused(string memberName)
    {
        Assert.False(
            BuiltinExceptionSurface.IsRefusedMember(typeof(System.IO.IOException), memberName),
            $"IOException is not a builtin exception — '{memberName}' must not be refused");
    }

    // --- SystemExit's declared Code property resolves ---

    [Fact]
    public void SystemExit_DeclaredCode_IsAllowed()
    {
        Assert.False(BuiltinExceptionSurface.IsRefusedMember(
            typeof(SharpyRT::Sharpy.SystemExit), "code"));
        Assert.False(BuiltinExceptionSurface.IsRefusedMember(
            typeof(SharpyRT::Sharpy.SystemExit), "Code"));
    }
}
