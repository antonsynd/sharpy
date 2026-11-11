using SCG = System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Sharpy.Stdlib.Tests.Conformance.ExceptionEscape;

/// <summary>
/// The exclusion rule of the exception-escape sweep, stated once and applied to every member: a member is
/// NOT invoked when its IL — followed transitively through every method declared in a <c>Sharpy*</c> assembly
/// that it calls, constructs or takes a delegate to, async and iterator state machines included — reaches a
/// method declared on a <see cref="IsSideEffectTarget">side-effect target</see>: the file system, the console,
/// processes, the network, sleeping or waiting, or process exit. BCL methods are leaves: the walk never descends
/// into them, so a clock read (<c>DateTime.Now</c>) or a pure <c>System.IO.Path</c> string helper is not an
/// exclusion. Abstract and interface methods have no body; the walk cannot see their overrides (a stated
/// limit: a Sharpy override reached only through a virtual call is not followed).
/// </summary>
internal static class SideEffectRule
{
    private static readonly Dictionary<short, OpCode> OneByte = new();
    private static readonly Dictionary<short, OpCode> TwoByte = new();
    private static readonly Dictionary<MethodBase, string?> Memo = new();
    private static readonly object Gate = new();

    /// <summary>In-memory <c>System.IO</c> types: no file, console or device behind them.</summary>
    private static readonly HashSet<string> InMemoryIo = new(StringComparer.Ordinal)
    {
        "System.IO.MemoryStream", "System.IO.StringReader", "System.IO.StringWriter", "System.IO.TextReader",
        "System.IO.TextWriter", "System.IO.Stream", "System.IO.BinaryReader", "System.IO.BinaryWriter",
        "System.IO.Path", "System.IO.SeekOrigin", "System.IO.IOException", "System.IO.EndOfStreamException",
        "System.IO.InvalidDataException", "System.IO.UnmanagedMemoryStream",
    };

    static SideEffectRule()
    {
        foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var op = (OpCode)f.GetValue(null)!;
            if (op.Size == 1)
                OneByte[(short)(op.Value & 0xFF)] = op;
            else
                TwoByte[(short)(op.Value & 0xFF)] = op;
        }
    }

    /// <summary>
    /// The BCL method that makes <paramref name="method"/> a side-effecting member, as
    /// <c>Declaring.Type::Name</c>, or null when the walk reaches none.
    /// </summary>
    public static string? SideEffectReached(MethodBase method)
    {
        lock (Gate)
        {
            if (Memo.TryGetValue(method, out var known))
                return known;
            // A negative is memoized only for a root walk: inside a walk a cycle cuts the search short.
            var hit = Walk(method, new HashSet<MethodBase>());
            Memo[method] = hit;
            return hit;
        }
    }

    /// <summary>The target list: a BCL method on one of these is a side effect the sweep must not trigger.</summary>
    internal static bool IsSideEffectTarget(MethodBase target)
    {
        var type = target.DeclaringType;
        if (type is null)
            return false;
        var full = (type.IsGenericType ? type.GetGenericTypeDefinition() : type).FullName ?? type.Name;
        var ns = type.Namespace ?? "";
        if (ns == "System.IO" || ns.StartsWith("System.IO.", StringComparison.Ordinal))
            return !InMemoryIo.Contains(full);
        if (ns == "System.Net" || ns.StartsWith("System.Net.", StringComparison.Ordinal))
            return true;
        if (full is "System.Console" or "System.Diagnostics.Process" or "System.Diagnostics.ProcessStartInfo")
            return true;
        if (full == "System.Environment")
            return target.Name is "Exit" or "FailFast" or "SetEnvironmentVariable" or "set_CurrentDirectory";
        if (full == "System.Threading.Thread")
            return target.Name is "Sleep" or ".ctor" or "Start" or "Join";
        if (full is "System.Threading.Tasks.Task" or "System.Threading.Tasks.Task`1")
            return target.Name is "Delay" or "Run" or "Wait" or "WaitAll" or "WaitAny" or "Start";
        if (ns == "System.Threading")
            return target.Name.StartsWith("Wait", StringComparison.Ordinal) || target.Name == "Sleep";
        return false;
    }

    private static string? Walk(MethodBase method, HashSet<MethodBase> seen)
    {
        if (!seen.Add(method))
            return null;
        if (Memo.TryGetValue(method, out var known))
            return known;

        string? hit = null;
        foreach (var callee in Callees(method))
        {
            if (IsSideEffectTarget(callee))
            {
                hit = $"{callee.DeclaringType!.FullName}::{callee.Name}";
                break;
            }
            if (IsSharpyAssembly(callee.DeclaringType?.Assembly))
            {
                hit = Walk(callee, seen);
                if (hit is not null)
                    break;
            }
        }

        if (hit is not null)
            Memo[method] = hit;
        return hit;
    }

    internal static bool IsSharpyAssembly(Assembly? assembly)
        => assembly?.GetName().Name?.StartsWith("Sharpy", StringComparison.Ordinal) == true;

    private static IEnumerable<MethodBase> Callees(MethodBase method)
    {
        var stateMachine = method.GetCustomAttribute<StateMachineAttribute>();
        if (stateMachine is not null)
        {
            var moveNext = stateMachine.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (moveNext is not null)
                yield return moveNext;
        }

        MethodBody? body;
        try
        {
            body = method.GetMethodBody();
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException)
        {
            yield break;
        }
        var il = body?.GetILAsByteArray();
        if (il is null)
            yield break;

        var typeArgs = method.DeclaringType is { IsGenericType: true } dt ? dt.GetGenericArguments() : null;
        var methodArgs = method is MethodInfo { IsGenericMethod: true } mi ? mi.GetGenericArguments() : null;
        int pc = 0;
        while (pc < il.Length)
        {
            OpCode op;
            if (il[pc] == 0xFE)
            {
                op = TwoByte[il[pc + 1]];
                pc += 2;
            }
            else
            {
                op = OneByte[il[pc]];
                pc += 1;
            }

            if (op.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, pc);
                MethodBase? callee = null;
                try
                {
                    callee = method.Module.ResolveMethod(token, typeArgs, methodArgs);
                }
                catch (Exception e) when (e is ArgumentException or BadImageFormatException or TypeLoadException or MissingMethodException)
                {
                }
                if (callee is not null)
                    yield return callee;
            }

            pc += OperandSize(op, il, pc);
        }
    }

    private static int OperandSize(OpCode op, byte[] il, int pc) => op.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, pc),
        _ => 4,
    };
}
