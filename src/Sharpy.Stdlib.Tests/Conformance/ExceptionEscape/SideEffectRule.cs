using SCG = System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Sharpy.Stdlib.Tests.Conformance.ExceptionEscape;

/// <summary>What the IL walk from one member found.</summary>
internal sealed record Reach(string? SideEffect, string? Unfollowable, string? ReadsClock);

/// <summary>
/// The exclusion rule of the exception-escape sweep, stated once and applied to every member. From the member's IL
/// the walk follows, transitively, every method declared in a <c>Sharpy*</c> assembly that is called, constructed or
/// taken as a delegate (<c>call</c>, <c>callvirt</c>, <c>newobj</c>, <c>ldftn</c>, <c>ldvirtftn</c>), async and
/// iterator state machines, the static constructor of every Sharpy type whose field or member it touches, and — for
/// a dispatched call (a Sharpy interface, abstract or virtual method, or a closed BCL interface method) — every Sharpy
/// implementation the receiver can be at run time: one declared on (a base of) a Core type, a type of the swept
/// module (the sweep passes only those, and BCL values, as arguments), or a type the walk saw constructed. BCL methods
/// are leaves. Three facts come back (<see cref="Reach"/>):
/// <list type="bullet">
/// <item><b>Side effect</b>: a BCL method on the file system, console, processes, network, sleeping or waiting, or
/// process exit (<see cref="IsSideEffectTarget"/>). The member is NOT invoked.</item>
/// <item><b>Unfollowable</b>: a shape the walk cannot see through — a P/Invoke or runtime-implemented extern, a
/// <c>callvirt</c> through an open generic BCL interface (its Sharpy implementations cannot be enumerated), a delegate
/// <c>Invoke</c> in a method that created no delegate itself (a delegate parameter), reflection invocation, or a
/// token that does not resolve. The member is NOT invoked either: the rule cannot vouch for it.</item>
/// <item><b>Reads the clock</b>: the clock or the host time zone (<see cref="IsClockTarget"/>). The member IS
/// invoked, but what it returns never joins the sweep's instance pool, so the pool is the same on every machine,
/// time zone and time of day.</item>
/// </list>
/// A stated limit: a BCL class's virtual method (<c>object.ToString</c>) is a leaf, although a Sharpy override may
/// answer it.
/// </summary>
internal static class SideEffectRule
{
    private static readonly Dictionary<short, OpCode> OneByte = new();
    private static readonly Dictionary<short, OpCode> TwoByte = new();
    private static readonly Dictionary<(MethodBase, string), Reach> Memo = new();
    private static readonly Dictionary<MethodBase, Node> Nodes = new();
    private static readonly Dictionary<MethodBase, SCG.List<MethodBase>> Implementations = new();
    private static readonly object Gate = new();

    /// <summary>In-memory <c>System.IO</c> types: no file, console or device behind them.</summary>
    private static readonly HashSet<string> InMemoryIo = new(StringComparer.Ordinal)
    {
        "System.IO.MemoryStream", "System.IO.StringReader", "System.IO.StringWriter", "System.IO.TextReader",
        "System.IO.TextWriter", "System.IO.Stream", "System.IO.BinaryReader", "System.IO.BinaryWriter",
        "System.IO.Path", "System.IO.SeekOrigin", "System.IO.IOException", "System.IO.EndOfStreamException",
        "System.IO.InvalidDataException", "System.IO.UnmanagedMemoryStream",
    };

    private sealed class Node
    {
        public SCG.List<MethodBase> Next { get; } = new();
        public SCG.List<string> Shapes { get; } = new();
        public SCG.List<MethodBase> Leaves { get; } = new();
        public SCG.List<MethodBase> Dispatch { get; } = new();
        public SCG.List<Type> Constructs { get; } = new();
    }

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
    /// The facts the walk from <paramref name="method"/> (a member's body) finds, when the sweep's arguments come from
    /// <paramref name="scope"/> (the swept module's types).
    /// </summary>
    public static Reach Walk(MethodBase method, IReadOnlyCollection<Type> scope)
    {
        lock (Gate)
        {
            var scopeKey = string.Join(",", scope.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal));
            if (Memo.TryGetValue((method, scopeKey), out var known))
                return known;
            string? sideEffect = null, unfollowable = null, clock = null;
            var receivers = new HashSet<Type>(scope);
            var dispatched = new SCG.List<MethodBase>();
            var seen = new HashSet<MethodBase> { method };
            var queue = new Queue<MethodBase>();
            void Visit(MethodBase next)
            {
                if (seen.Add(next))
                    queue.Enqueue(next);
            }
            void Dispatch(MethodBase callee, IEnumerable<Type> types)
            {
                foreach (var impl in ImplementationsOf(callee))
                {
                    var owner = impl.DeclaringType!;
                    if (owner.Assembly == CoreAssembly || types.Any(owner.IsAssignableFrom))
                        Visit(impl);
                }
            }

            queue.Enqueue(method);
            while (queue.Count > 0)
            {
                var node = NodeOf(queue.Dequeue());
                unfollowable ??= node.Shapes.FirstOrDefault();
                foreach (var leaf in node.Leaves)
                {
                    if (sideEffect is null && IsSideEffectTarget(leaf))
                        sideEffect = Name(leaf);
                    if (clock is null && IsClockTarget(leaf))
                        clock = Name(leaf);
                }
                foreach (var next in node.Next)
                    Visit(next);
                foreach (var type in node.Constructs)
                {
                    // A newly constructed receiver type: the dispatches already seen may now land on it.
                    if (receivers.Add(type))
                    {
                        foreach (var callee in dispatched)
                            Dispatch(callee, new[] { type });
                    }
                }
                foreach (var callee in node.Dispatch)
                {
                    dispatched.Add(callee);
                    Dispatch(callee, receivers);
                }
            }
            var reach = new Reach(sideEffect, unfollowable, clock);
            Memo[(method, scopeKey)] = reach;
            return reach;
        }
    }

    private static readonly Assembly CoreAssembly = typeof(KeyError).Assembly;

    /// <summary>The side effect <paramref name="method"/> reaches, or null (the positive controls' view).</summary>
    public static string? SideEffectReached(MethodBase method) => Walk(method, Array.Empty<Type>()).SideEffect;

    private static string Name(MethodBase m) => $"{m.DeclaringType?.FullName}::{m.Name}";

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

    /// <summary>The clock and host-time-zone reads: a member reaching one returns machine- and time-dependent values.</summary>
    internal static bool IsClockTarget(MethodBase target)
    {
        var full = target.DeclaringType?.FullName ?? "";
        return full switch
        {
            "System.DateTime" => target.Name is "get_Now" or "get_UtcNow" or "get_Today" or "ToLocalTime" or "ToUniversalTime",
            "System.DateTimeOffset" => target.Name is "get_Now" or "get_UtcNow" or "ToLocalTime",
            "System.TimeZoneInfo" or "System.Diagnostics.Stopwatch" => true,
            "System.Environment" => target.Name.StartsWith("get_TickCount", StringComparison.Ordinal),
            "System.Guid" => target.Name == "NewGuid",
            "System.Random" => target.Name is ".ctor" or "get_Shared",
            _ => false,
        };
    }

    /// <summary>The reflection entry points that call code the walk cannot name.</summary>
    private static bool IsReflectionInvoke(MethodBase target)
    {
        var type = target.DeclaringType;
        if (type is null)
            return false;
        if (type == typeof(Activator))
            return target.Name.StartsWith("CreateInstance", StringComparison.Ordinal);
        if (typeof(Delegate).IsAssignableFrom(type) && target.Name == "DynamicInvoke")
            return true;
        if (type.Namespace == "System.Reflection" || type == typeof(Type))
            return target.Name is "Invoke" or "InvokeMember" or "CreateInstance";
        return false;
    }

    internal static bool IsSharpyAssembly(Assembly? assembly)
        => assembly?.GetName().Name?.StartsWith("Sharpy", StringComparison.Ordinal) == true;

    private static Node NodeOf(MethodBase method)
    {
        if (Nodes.TryGetValue(method, out var cached))
            return cached;
        var node = new Node();
        Nodes[method] = node;

        if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0
            || (method.MethodImplementationFlags & (MethodImplAttributes.InternalCall | MethodImplAttributes.Runtime)) != 0)
        {
            node.Shapes.Add($"extern {Name(method)}: no IL to follow");
            return node;
        }

        AddTypeInitializer(node, method.DeclaringType);
        var stateMachine = method.GetCustomAttribute<StateMachineAttribute>();
        if (stateMachine?.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is { } moveNext)
            node.Next.Add(moveNext);

        byte[]? il;
        try
        {
            il = method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException)
        {
            il = null;
        }
        if (il is null)
            return node;

        var typeArgs = method.DeclaringType is { IsGenericType: true } dt ? dt.GetGenericArguments() : null;
        var methodArgs = method is MethodInfo { IsGenericMethod: true } mi ? mi.GetGenericArguments() : null;
        bool createsDelegate = false;
        var invokes = new SCG.List<MethodBase>();
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
                MethodBase? callee = null;
                try
                {
                    callee = method.Module.ResolveMethod(BitConverter.ToInt32(il, pc), typeArgs, methodArgs);
                }
                catch (Exception e) when (e is ArgumentException or BadImageFormatException or TypeLoadException or MissingMethodException)
                {
                    node.Shapes.Add($"unresolvable method token in {Name(method)}");
                }
                if (callee is not null)
                {
                    if (op == OpCodes.Ldftn || op == OpCodes.Ldvirtftn)
                        createsDelegate = true;
                    Classify(node, callee, op, invokes);
                }
            }
            else if (op.OperandType == OperandType.InlineField)
            {
                try
                {
                    AddTypeInitializer(node, method.Module.ResolveField(BitConverter.ToInt32(il, pc), typeArgs, methodArgs)?.DeclaringType);
                }
                catch (Exception e) when (e is ArgumentException or BadImageFormatException or TypeLoadException or MissingFieldException)
                {
                    node.Shapes.Add($"unresolvable field token in {Name(method)}");
                }
            }

            pc += OperandSize(op, il, pc);
        }

        if (!createsDelegate)
        {
            foreach (var invoke in invokes)
                node.Shapes.Add($"delegate invoke {Name(invoke)} in {Name(method)}: the delegate comes from outside, so its target is unknown");
        }
        return node;
    }

    private static void Classify(Node node, MethodBase callee, OpCode op, SCG.List<MethodBase> invokes)
    {
        var declaring = callee.DeclaringType;
        if (declaring is not null && typeof(Delegate).IsAssignableFrom(declaring) && callee.Name == "Invoke")
        {
            invokes.Add(callee);
            return;
        }
        if (IsReflectionInvoke(callee))
        {
            node.Shapes.Add($"reflection {Name(callee)}: the invoked member is chosen at run time");
            return;
        }
        if (!IsSharpyAssembly(declaring?.Assembly))
        {
            node.Leaves.Add(callee);
            if (op == OpCodes.Callvirt && declaring is { IsInterface: true })
            {
                // A BCL interface: its BCL implementations are leaves like any BCL method; its Sharpy ones are followed.
                if (declaring.ContainsGenericParameters)
                    node.Shapes.Add($"interface dispatch {Name(callee)}: an open generic BCL interface, whose implementations cannot be enumerated");
                else
                    node.Dispatch.Add(callee);
            }
            return;
        }
        node.Next.Add(callee);
        AddTypeInitializer(node, declaring);
        if (op == OpCodes.Newobj && declaring is not null)
            node.Constructs.Add(declaring);
        if (callee.IsAbstract || (callee.IsVirtual && !callee.IsFinal && op == OpCodes.Callvirt))
            node.Dispatch.Add(callee);
    }

    private static void AddTypeInitializer(Node node, Type? type)
    {
        if (type is not null && IsSharpyAssembly(type.Assembly) && type.TypeInitializer is { } cctor)
            node.Next.Add(cctor);
    }

    /// <summary>The Sharpy implementations of an interface, abstract or virtual Sharpy method.</summary>
    private static SCG.List<MethodBase> ImplementationsOf(MethodBase method)
    {
        if (Implementations.TryGetValue(method, out var known))
            return known;
        var found = new SCG.List<MethodBase>();
        Implementations[method] = found;
        var declaring = method.DeclaringType!;
        if (declaring.ContainsGenericParameters)
            return found;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => IsSharpyAssembly(a) && !a.IsDynamic))
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t is not null).ToArray()!;
            }
            foreach (var type in types)
            {
                if (type == declaring || type.IsInterface || type.IsGenericTypeDefinition || !declaring.IsAssignableFrom(type))
                    continue;
                if (declaring.IsInterface)
                {
                    var map = type.GetInterfaceMap(declaring);
                    int index = Array.IndexOf(map.InterfaceMethods, method);
                    if (index >= 0 && map.TargetMethods[index].DeclaringType == type)
                        found.Add(map.TargetMethods[index]);
                }
                else if (method is MethodInfo mi)
                {
                    var baseDefinition = mi.GetBaseDefinition();
                    found.AddRange(type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .Where(m => m.GetBaseDefinition() == baseDefinition));
                }
            }
        }
        return found;
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
