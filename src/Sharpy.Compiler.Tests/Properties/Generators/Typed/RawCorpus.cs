using System.Reflection;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Module = Sharpy.Compiler.Parser.Ast.Module;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// The roster's RAW positive control's runner (#2288 Task 3; moved into the seam by P63b, #2324 — the
/// oracle scan admits no <c>catch</c> outside it): draws <c>count</c> values from a boxed <c>Gen&lt;T&gt;</c> at a
/// fixed seed, unparses every <see cref="Node"/> / <c>string</c> a sample carries (a tuple's items each) and
/// re-parses it with the real parser. A throw anywhere (generator, unparser, parser) or a parse error no
/// <see cref="KnownGeneratorPins"/> pin explains is returned as the failure — never thrown, never skipped.
/// </summary>
internal static class RawCorpus
{
    private sealed class RawSampleException(string message) : Exception(message);

    /// <summary>The re-parsed corpus, or (empty, why not).</summary>
    public static (List<Module> Corpus, string? Failure) Draw(object gen, int count, ulong seed,
        Dictionary<KnownGeneratorPins.ParsePin, int> pinHits, System.Security.Cryptography.IncrementalHash corpusHash)
    {
        try
        {
            return (Collect(gen, count, seed, pinHits, corpusHash), null);
        }
        catch (RawSampleException e)
        {
            return (new List<Module>(), e.Message);
        }
        catch (Exception e)
        {
            return (new List<Module>(), $"generator threw {Unwrap(e)}");
        }
    }

    private static string Unwrap(Exception e) =>
        e is TargetInvocationException { InnerException: { } inner } ? Unwrap(inner) : $"{e.GetType().Name}: {e.Message}";

    private static List<Module> Collect(object gen, int count, ulong seed, Dictionary<KnownGeneratorPins.ParsePin, int> pinHits,
        System.Security.Cryptography.IncrementalHash corpusHash)
    {
        var elementType = GenElementType(gen.GetType())
            ?? throw new RawSampleException($"generator is {gen.GetType().Name}, not a CsCheck Gen<T>");
        var draw = typeof(RawCorpus)
            .GetMethod(nameof(DrawSamples), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(elementType);
        var samples = (List<object>)draw.Invoke(null, new object[] { gen, count, seed })!;

        var modules = new List<Module>();
        foreach (var sample in samples)
        {
            foreach (var (source, origin) in SourcesOf(sample))
            {
                corpusHash.AppendData(System.Text.Encoding.UTF8.GetBytes(source + "\u0000"));
                if (Reparse(source, origin, pinHits) is { } module)
                    modules.Add(module);
            }
        }
        return modules;
    }

    private static Type? GenElementType(Type? type)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Gen<>))
                return t.GetGenericArguments()[0];
        }
        return null;
    }

    private static List<object> DrawSamples<T>(Gen<T> gen, int count, ulong seed)
    {
        var pcg = new PCG(1u, seed);
        var samples = new List<object>(count);
        for (int i = 0; i < count; i++)
            samples.Add(gen.Generate(pcg, null, out _)!);
        return samples;
    }

    /// <summary>The source texts a sample carries, each with the AST it was unparsed from (null for a
    /// source-text generator).</summary>
    private static IEnumerable<(string Source, Node? Origin)> SourcesOf(object sample)
    {
        switch (sample)
        {
            case string s:
                yield return (s, null);
                break;
            case Node node:
                string text;
                try
                {
                    text = node is Module m
                        ? Sharpy.Compiler.Pretty.Unparser.Unparse(m)
                        : Sharpy.Compiler.Pretty.Unparser.Unparse(node);
                }
                catch (Exception e)
                {
                    throw new RawSampleException($"Unparser threw {Unwrap(e)} on a {node.GetType().Name}");
                }
                yield return (text, node);
                break;
            case System.Runtime.CompilerServices.ITuple tuple:
                for (int i = 0; i < tuple.Length; i++)
                {
                    if (tuple[i] is string or Node)
                    {
                        foreach (var item in SourcesOf(tuple[i]!))
                            yield return item;
                    }
                }
                break;
            default:
                throw new RawSampleException($"sample of type {sample.GetType().Name} carries no Node or source text");
        }
    }

    private static Module? Reparse(string source, Node? origin, Dictionary<KnownGeneratorPins.ParsePin, int> pinHits)
    {
        try
        {
            var parser = new Sharpy.Compiler.Parser.Parser(new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll());
            var module = parser.ParseModule();
            if (parser.Diagnostics.HasErrors)
            {
                var errors = parser.Diagnostics.GetErrors().ToList();
                var pin = origin == null ? null : KnownGeneratorPins.Match(origin, errors);
                if (pin != null)
                {
                    pinHits[pin]++;
                    return null;
                }
                throw new RawSampleException("the generated source does not parse ("
                    + string.Join("; ", errors.Select(d => $"{d.Code}: {d.Message}"))
                    + "):\n" + source);
            }
            return module;
        }
        catch (RawSampleException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new RawSampleException($"the parser threw {Unwrap(e)} on:\n{source}");
        }
    }
}
