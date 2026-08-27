using System.Reflection;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests.Parser.Ast;

[Trait("Category", "Property")]
public class GetChildNodesCompletenessTests
{
    private static readonly Type NodeType = typeof(Node);
    private static readonly Type TypeAnnotationType = typeof(TypeAnnotation);

    private static readonly string[] AstSourceFiles =
    [
        "Node.cs", "Expression.cs", "Expression.Future.cs",
        "Statement.cs", "Statement.Future.cs", "Pattern.cs", "Types.cs"
    ];

    /// <summary>
    /// Every concrete Node subtype that declares Node-typed properties must override
    /// GetChildNodes and reference each such property in the override body.
    ///
    /// TypeAnnotation-typed properties are exempt: TypeAnnotation derives from Node (#1235)
    /// but is deliberately excluded from GetChildNodes traversal (Types.cs:17-24).
    /// </summary>
    [Fact]
    public void AllNodeTypedPropertiesAreYieldedByGetChildNodes()
    {
        var assembly = NodeType.Assembly;

        var concreteNodeTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(NodeType) && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();

        Assert.True(concreteNodeTypes.Count > 0, "Should find at least one concrete Node type");

        var astSourceDir = FindAstSourceDirectory();
        var allSource = string.Join("\n\n",
            AstSourceFiles.Select(f => File.ReadAllText(Path.Combine(astSourceDir, f))));

        var failures = new List<string>();
        int nonLeafCount = 0;

        foreach (var type in concreteNodeTypes)
        {
            var nodeProps = GetNodeTypedPropertyNames(type);

            if (nodeProps.Count == 0)
                continue;

            nonLeafCount++;

            var declaredOverride = type.GetMethod("GetChildNodes",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (declaredOverride == null)
            {
                failures.Add($"{type.Name}: has Node-typed properties " +
                    $"[{string.Join(", ", nodeProps)}] but does not override GetChildNodes");
                continue;
            }

            var body = ExtractGetChildNodesBody(allSource, type.Name);
            if (body == null)
            {
                failures.Add($"{type.Name}: could not locate GetChildNodes body in source");
                continue;
            }

            var activeCode = StripComments(body);

            foreach (var prop in nodeProps)
            {
                if (!Regex.IsMatch(activeCode, $@"\b{Regex.Escape(prop)}\b"))
                {
                    failures.Add(
                        $"{type.Name}.GetChildNodes() does not reference property '{prop}'");
                }
            }
        }

        Assert.True(nonLeafCount > 10,
            $"Expected to check at least 10 non-leaf types, but only found {nonLeafCount}");

        Assert.True(failures.Count == 0,
            $"GetChildNodes completeness failures:\n{string.Join("\n", failures)}");
    }

    private static List<string> GetNodeTypedPropertyNames(Type type)
    {
        var result = new List<string>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.DeclaringType == NodeType)
                continue;

            var propType = prop.PropertyType;

            if (IsNodeNotAnnotation(propType))
            {
                result.Add(prop.Name);
                continue;
            }

            var elementType = GetNodeCollectionElementType(propType);
            if (elementType != null && IsNodeNotAnnotation(elementType))
            {
                result.Add(prop.Name);
            }
        }

        return result;
    }

    private static bool IsNodeNotAnnotation(Type t) =>
        (t == NodeType || t.IsSubclassOf(NodeType))
        && t != TypeAnnotationType
        && !t.IsSubclassOf(TypeAnnotationType);

    private static Type? GetNodeCollectionElementType(Type t)
    {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return t.GetGenericArguments()[0];

        foreach (var iface in t.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return iface.GetGenericArguments()[0];
        }

        return null;
    }

    private static string StripComments(string code)
    {
        code = Regex.Replace(code, @"//[^\n]*", "");
        code = Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline);
        return code;
    }

    private static string? ExtractGetChildNodesBody(string allSource, string typeName)
    {
        var typeMatch = Regex.Match(allSource, $@"\brecord\s+{Regex.Escape(typeName)}\b");
        if (!typeMatch.Success)
            return null;

        var afterTypeName = allSource.AsSpan(typeMatch.Index + typeMatch.Length);

        int scopeStart = -1;
        int parenDepth = 0;
        for (int i = 0; i < afterTypeName.Length; i++)
        {
            char c = afterTypeName[i];
            if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;
            else if (parenDepth == 0)
            {
                if (c == '{') { scopeStart = i; break; }
                if (c == ';') return null;
            }
        }
        if (scopeStart < 0)
            return null;

        int depth = 0;
        int scopeEnd = -1;
        for (int i = scopeStart; i < afterTypeName.Length; i++)
        {
            if (afterTypeName[i] == '{') depth++;
            else if (afterTypeName[i] == '}')
            {
                depth--;
                if (depth == 0) { scopeEnd = i; break; }
            }
        }
        if (scopeEnd < 0)
            return null;

        var scopeText = afterTypeName[scopeStart..(scopeEnd + 1)].ToString();

        const string sig = "GetChildNodes()";
        int sigIdx = scopeText.IndexOf(sig, StringComparison.Ordinal);
        if (sigIdx < 0)
            return null;

        var afterSig = scopeText[(sigIdx + sig.Length)..].TrimStart();

        if (afterSig.StartsWith("=>"))
        {
            int semi = afterSig.IndexOf(';');
            return semi >= 0 ? afterSig[..semi] : null;
        }

        if (afterSig.StartsWith("{"))
        {
            depth = 0;
            for (int i = 0; i < afterSig.Length; i++)
            {
                if (afterSig[i] == '{') depth++;
                else if (afterSig[i] == '}')
                {
                    depth--;
                    if (depth == 0) return afterSig[..(i + 1)];
                }
            }
        }

        return null;
    }

    private static string FindAstSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var astPath = Path.Combine(current, "src", "Sharpy.Compiler", "Parser", "Ast");
            if (Directory.Exists(astPath))
                return astPath;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Sharpy.Compiler", "Parser", "Ast"));
    }
}
