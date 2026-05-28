using System;
using System.Collections.Generic;
using CsCheck;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Core.Tests.Properties;

/// <summary>
/// Property tests verifying that Sharpy.Core collections behave identically to
/// their .NET equivalents over randomized operation sequences (#725).
/// </summary>
[Trait("Category", "Property")]
public class CollectionParityPropertyTests
{
    private readonly ITestOutputHelper _output;

    public CollectionParityPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void List_MatchesDotNetAfterOperations()
    {
        // Operations: 0=Append, 1=Pop(last), 2=Insert(0,x), 3=read indexer
        Gen.Int[5, 30].SelectMany(n =>
            Gen.Select(Gen.Int[0, 3].Array[n], Gen.Int[0, 100].Array[n])
        ).Sample((ops, vals) =>
        {
            var sharpy = new Sharpy.List<int>();
            var dotnet = new System.Collections.Generic.List<int>();

            // Sharpy.List exposes Count(T) as a method, so the size property is
            // the explicit ISized.Count implementation. Use this helper to read it.
            int SharpyCount() => ((Sharpy.ISized)sharpy).Count;

            for (int i = 0; i < ops.Length; i++)
            {
                switch (ops[i])
                {
                    case 0: // Append
                        sharpy.Append(vals[i]);
                        dotnet.Add(vals[i]);
                        break;
                    case 1: // Pop last (only if non-empty)
                        if (SharpyCount() > 0 && dotnet.Count > 0)
                        {
                            var sp = sharpy.Pop();
                            var dp = dotnet[dotnet.Count - 1];
                            dotnet.RemoveAt(dotnet.Count - 1);
                            if (sp != dp)
                                throw new Exception($"Pop mismatch: sharpy={sp}, dotnet={dp}");
                        }
                        break;
                    case 2: // Insert at 0
                        if (SharpyCount() < 100) // avoid huge lists
                        {
                            sharpy.Insert(0, vals[i]);
                            dotnet.Insert(0, vals[i]);
                        }
                        break;
                    case 3: // Read indexer (only if non-empty)
                        if (SharpyCount() > 0)
                        {
                            int idx = vals[i] % SharpyCount();
                            if (sharpy[idx] != dotnet[idx])
                                throw new Exception($"Index {idx} mismatch: sharpy={sharpy[idx]}, dotnet={dotnet[idx]}");
                        }
                        break;
                }

                if (SharpyCount() != dotnet.Count)
                    throw new Exception($"Count mismatch after op {ops[i]}: sharpy={SharpyCount()}, dotnet={dotnet.Count}");
            }
        }, iter: 100);
    }

    [Fact]
    public void Dict_MatchesDotNetAfterOperations()
    {
        // Operations: 0=set, 1=remove, 2=containsKey, 3=get
        Gen.Int[5, 30].SelectMany(n =>
            Gen.Select(Gen.Int[0, 3].Array[n], Gen.Int[0, 10].Array[n], Gen.Int[0, 100].Array[n])
        ).Sample((ops, keys, vals) =>
        {
            var sharpy = new Sharpy.Dict<int, int>();
            var dotnet = new Dictionary<int, int>();

            for (int i = 0; i < ops.Length; i++)
            {
                switch (ops[i])
                {
                    case 0: // Set
                        sharpy[keys[i]] = vals[i];
                        dotnet[keys[i]] = vals[i];
                        break;
                    case 1: // Remove (only if key exists)
                        if (dotnet.ContainsKey(keys[i]))
                        {
                            sharpy.Remove(keys[i]);
                            dotnet.Remove(keys[i]);
                        }
                        break;
                    case 2: // ContainsKey
                        if (sharpy.ContainsKey(keys[i]) != dotnet.ContainsKey(keys[i]))
                            throw new Exception($"ContainsKey({keys[i]}) mismatch");
                        break;
                    case 3: // Get (only if key exists)
                        if (dotnet.ContainsKey(keys[i]))
                        {
                            if (sharpy[keys[i]] != dotnet[keys[i]])
                                throw new Exception($"Get({keys[i]}) mismatch: sharpy={sharpy[keys[i]]}, dotnet={dotnet[keys[i]]}");
                        }
                        break;
                }

                if (sharpy.Count != dotnet.Count)
                    throw new Exception($"Count mismatch: sharpy={sharpy.Count}, dotnet={dotnet.Count}");
            }
        }, iter: 100);
    }

    [Fact]
    public void Set_MatchesDotNetAfterOperations()
    {
        // Operations: 0=add, 1=discard, 2=contains
        Gen.Int[5, 30].SelectMany(n =>
            Gen.Select(Gen.Int[0, 2].Array[n], Gen.Int[0, 20].Array[n])
        ).Sample((ops, vals) =>
        {
            var sharpy = new Sharpy.Set<int>();
            var dotnet = new HashSet<int>();

            for (int i = 0; i < ops.Length; i++)
            {
                switch (ops[i])
                {
                    case 0: // Add
                        sharpy.Add(vals[i]);
                        dotnet.Add(vals[i]);
                        break;
                    case 1: // Discard (safe remove)
                        sharpy.Discard(vals[i]);
                        dotnet.Remove(vals[i]);
                        break;
                    case 2: // Contains
                        if (sharpy.Contains(vals[i]) != dotnet.Contains(vals[i]))
                            throw new Exception($"Contains({vals[i]}) mismatch");
                        break;
                }

                if (sharpy.Count != dotnet.Count)
                    throw new Exception($"Count mismatch: sharpy={sharpy.Count}, dotnet={dotnet.Count}");
            }
        }, iter: 100);
    }
}
