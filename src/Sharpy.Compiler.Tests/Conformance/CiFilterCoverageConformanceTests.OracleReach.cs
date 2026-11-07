using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

public partial class CiFilterCoverageConformanceTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value);

    /// <summary>
    /// The test classes that reach the python oracle, read from the call graph of the compiled test assemblies. A source
    /// scan has to spell every route: a lambda, a qualified name, a using alias and a helper on another class all read
    /// differently there and are the same call here. The seeds are every static member of <see cref="PythonInterpreter"/>
    /// returning an interpreter, so a hand-written skip over <see cref="PythonInterpreter.TryLocate(int)"/> counts as well
    /// as the gate, and <c>TestPreconditions.RequireOrSkip</c>, which is a different type, does not. A method reaches the
    /// oracle when it calls a seed or a method that reaches it. Calling or reading any member of a type counts as calling
    /// that type's static initializer, where a <c>Lazy</c> field's lambda lives. A method is attributed to its innermost
    /// enclosing test class and to the classes deriving from it. <c>Unattributed</c> holds what reaches the oracle with no
    /// test class around it and no caller, which only a route this graph cannot follow (a call through an interface) leaves.
    /// </summary>
    private static (List<string> Classes, List<string> Unattributed) ScanPythonOracleClasses(IReadOnlyList<TestInfo> tests)
    {
        var testClasses = tests.Select(t => t.Fqn[..t.Fqn.LastIndexOf('.')]).ToHashSet(StringComparer.Ordinal);
        var callers = new Dictionary<(Module, int), List<MethodBase>>();
        var types = new[] { Assembly.GetExecutingAssembly(), typeof(IntegrationTestBase).Assembly }.SelectMany(a => a.GetTypes()).ToList();
        var targetsOfToken = new Dictionary<(Module, int), List<(Module, int)>>();
        var derived = new Dictionary<Type, List<Type>>();
        foreach (var type in types)
        {
            if (testClasses.Contains(ClassName(type)))
            {
                for (var b = type.BaseType; b != null && b != typeof(object); b = b.BaseType)
                {
                    var key = b.IsGenericType ? b.GetGenericTypeDefinition() : b;
                    if (!derived.TryGetValue(key, out var list))
                        derived[key] = list = new List<Type>();
                    list.Add(type);
                }
            }

            if (type == typeof(PythonInterpreter))
                continue;
            foreach (var method in type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
                AddCallEdges(method, callers, targetsOfToken);
        }

        var seeds = typeof(PythonInterpreter).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(PythonInterpreter))
            .ToList();
        Assert.Contains(seeds, m => m.Name == nameof(PythonInterpreter.RequireOrSkip));
        Assert.Contains(seeds, m => m.Name == nameof(PythonInterpreter.TryLocate));

        var reached = new HashSet<(Module, int)>();
        var work = new Queue<MethodBase>(seeds);
        var classes = new HashSet<string>(StringComparer.Ordinal);
        var unattributed = new List<string>();
        while (work.TryDequeue(out var method))
        {
            if (!reached.Add(MethodKey(method)))
                continue;

            var hasCaller = false;
            if (callers.TryGetValue(MethodKey(method), out var callersOfMethod))
            {
                hasCaller = true;
                foreach (var caller in callersOfMethod)
                    work.Enqueue(caller);
            }
            if (method is MethodInfo info && info.GetBaseDefinition() is var definition && MethodKey(definition) != MethodKey(info))
            {
                hasCaller = true;
                work.Enqueue(definition);
            }
            if (method.DeclaringType == typeof(PythonInterpreter))
                continue;

            var owners = OwningTestClasses(method.DeclaringType!, testClasses, derived);
            classes.UnionWith(owners);
            if (owners.Count == 0 && !hasCaller)
                unattributed.Add($"{method.DeclaringType!.FullName}.{method.Name}");
        }

        return (classes.OrderBy(c => c, StringComparer.Ordinal).ToList(), unattributed);
    }

    /// <summary>A class's name as <see cref="ReflectTests"/> spells it.</summary>
    private static string ClassName(Type type) => type.FullName!.Replace('+', '.');

    private static (Module, int) MethodKey(MethodBase method) => (method.Module, method.MetadataToken);

    /// <summary>The innermost type around <paramref name="type"/> that is a test class or a base of one.</summary>
    private static List<string> OwningTestClasses(Type type, HashSet<string> testClasses, Dictionary<Type, List<Type>> derived)
    {
        for (var t = type; t != null; t = t.DeclaringType)
        {
            var owners = new List<string>();
            if (testClasses.Contains(ClassName(t)))
                owners.Add(ClassName(t));
            if (derived.TryGetValue(t, out var subclasses))
                owners.AddRange(subclasses.Select(ClassName));
            if (owners.Count > 0)
                return owners;
        }

        return new List<string>();
    }

    private static void AddCallEdges(
        MethodBase method,
        Dictionary<(Module, int), List<MethodBase>> callers,
        Dictionary<(Module, int), List<(Module, int)>> targetsOfToken)
    {
        static void Add(Dictionary<(Module, int), List<MethodBase>> edges, (Module, int) target, MethodBase caller)
        {
            if (!edges.TryGetValue(target, out var list))
                edges[target] = list = new List<MethodBase>();
            list.Add(caller);
        }

        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il == null)
            return;

        foreach (var token in MemberTokens(il))
        {
            if (!targetsOfToken.TryGetValue((method.Module, token), out var targets))
                targetsOfToken[(method.Module, token)] = targets = ResolveTargets(method, token);
            foreach (var target in targets)
                Add(callers, target, method);
        }

        // An async or iterator body is a nested type's MoveNext, which the method reaches through a builder's generic Start.
        if (method.GetCustomAttribute<StateMachineAttribute>(inherit: false)?.StateMachineType.GetMethod("MoveNext", Declared) is { } moveNext)
            Add(callers, MethodKey(moveNext), method);
    }

    private static List<(Module, int)> ResolveTargets(MethodBase method, int token)
    {
        var typeArguments = method.DeclaringType is { IsGenericType: true } declaring ? declaring.GetGenericArguments() : null;
        var methodArguments = method is { IsGenericMethod: true, IsConstructor: false } ? method.GetGenericArguments() : null;
        var member = method.Module.ResolveMember(token, typeArguments, methodArguments);

        var targets = new List<(Module, int)>();
        if (member is MethodBase target)
            targets.Add(MethodKey(target));
        if (member is MethodBase or FieldInfo && member.DeclaringType?.TypeInitializer is { } initializer)
            targets.Add(MethodKey(initializer));
        return targets;
    }

    /// <summary>The metadata tokens of the members <paramref name="il"/> calls, loads a pointer to, reads or names.</summary>
    private static IEnumerable<int> MemberTokens(byte[] il)
    {
        for (var i = 0; i < il.Length;)
        {
            short value = il[i++];
            if (value == 0xFE)
                value = unchecked((short)(0xFE00 | il[i++]));

            var operand = OpCodesByValue[value].OperandType;
            if (operand is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineTok)
                yield return BitConverter.ToInt32(il, i);

            i += operand switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4,
            };
        }
    }
}
