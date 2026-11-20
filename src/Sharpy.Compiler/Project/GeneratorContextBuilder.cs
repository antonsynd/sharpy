extern alias SharpyRT;
using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Project;

using GClassInfo = SharpyRT::Sharpy.Generators.ClassInfo;
using GFunctionInfo = SharpyRT::Sharpy.Generators.FunctionInfo;
using GFieldInfo = SharpyRT::Sharpy.Generators.FieldInfo;
using GMethodInfo = SharpyRT::Sharpy.Generators.MethodInfo;
using GParameterInfo = SharpyRT::Sharpy.Generators.ParameterInfo;
using GDecoratorInfo = SharpyRT::Sharpy.Generators.DecoratorInfo;
using GGeneratorContext = SharpyRT::Sharpy.Generators.GeneratorContext;

internal class GeneratorContextBuilder
{
    public GGeneratorContext Build(Statement declaration, Decorator trigger, string moduleName)
    {
        GClassInfo? classInfo = null;
        GFunctionInfo? functionInfo = null;

        if (declaration is ClassDef classDef)
            classInfo = BuildTypeInfo(classDef.Name, classDef.Body, classDef.BaseClasses, classDef.Decorators,
                classDef.Decorators.Any(d => d.Name == "dataclass"));
        else if (declaration is FunctionDef funcDef)
            functionInfo = BuildFunctionInfo(funcDef);
        else if (declaration is StructDef structDef)
            classInfo = BuildTypeInfo(structDef.Name, structDef.Body, structDef.BaseClasses, structDef.Decorators, false);

        var (arguments, keywordArgs) = ExtractArguments(trigger);

        return new GGeneratorContext(classInfo, functionInfo, arguments, keywordArgs, moduleName);
    }

    /// <summary>Builds the generator ClassInfo for a class or struct declaration.</summary>
    private GClassInfo BuildTypeInfo(
        string name,
        ImmutableArray<Statement> body,
        ImmutableArray<TypeAnnotation> bases,
        ImmutableArray<Decorator> decoratorNodes,
        bool isDataclass)
    {
        var fields = new List<GFieldInfo>();
        var methods = new List<GMethodInfo>();
        var baseClasses = new List<string>();
        var decorators = new List<GDecoratorInfo>();

        foreach (var stmt in body)
        {
            if (stmt is VariableDeclaration varDecl)
            {
                fields.Add(new GFieldInfo(
                    varDecl.Name,
                    varDecl.Type?.ToString(),
                    varDecl.InitialValue != null,
                    varDecl.InitialValue?.ToString()));
            }
            else if (stmt is FunctionDef method)
            {
                methods.Add(BuildMethodInfo(method));
            }
        }

        foreach (var baseAnnot in bases)
        {
            baseClasses.Add(baseAnnot.Name);
        }

        foreach (var dec in decoratorNodes)
        {
            decorators.Add(BuildDecoratorInfo(dec));
        }

        return new GClassInfo(name, fields, methods, baseClasses, decorators, isDataclass);
    }

    private GFunctionInfo BuildFunctionInfo(FunctionDef funcDef)
    {
        var parameters = new List<GParameterInfo>();
        foreach (var param in funcDef.Parameters)
        {
            parameters.Add(new GParameterInfo(
                param.Name,
                param.Type?.ToString(),
                param.DefaultValue != null));
        }

        var decorators = new List<GDecoratorInfo>();
        foreach (var dec in funcDef.Decorators)
        {
            decorators.Add(BuildDecoratorInfo(dec));
        }

        return new GFunctionInfo(
            funcDef.Name,
            parameters,
            funcDef.ReturnType?.ToString(),
            decorators,
            funcDef.Decorators.Any(d => d.Name == "static"),
            funcDef.IsAsync);
    }

    private GMethodInfo BuildMethodInfo(FunctionDef method)
    {
        var parameters = new List<GParameterInfo>();
        foreach (var param in method.Parameters)
        {
            if (param.Name == "self")
                continue;
            parameters.Add(new GParameterInfo(
                param.Name,
                param.Type?.ToString(),
                param.DefaultValue != null));
        }

        return new GMethodInfo(
            method.Name,
            parameters,
            method.ReturnType?.ToString(),
            method.Decorators.Any(d => d.Name == "static"),
            method.Decorators.Any(d => d.Name == "abstract"),
            method.Decorators.Any(d => d.Name == "virtual"),
            method.IsAsync);
    }

    private static GDecoratorInfo BuildDecoratorInfo(Decorator dec)
    {
        var (args, kwargs) = ExtractArguments(dec);
        return new GDecoratorInfo(dec.Name, args, kwargs, dec.IsBracketAttribute);
    }

    /// <summary>Fresh positional and keyword argument collections for a decorator, one per call.</summary>
    private static (List<object> Args, Dictionary<string, object> Kwargs) ExtractArguments(Decorator dec)
    {
        var args = new List<object>();
        foreach (var arg in dec.Arguments)
        {
            args.Add(ExtractLiteralValue(arg));
        }

        var kwargs = new Dictionary<string, object>();
        foreach (var kwarg in dec.KeywordArguments)
        {
            kwargs[kwarg.Name] = ExtractLiteralValue(kwarg.Value);
        }

        return (args, kwargs);
    }

    /// <summary>
    /// What a source generator receives for a decorator argument — the contract pinned by
    /// <c>GeneratorContextBuilderLiteralValueTests</c>. Values come from the one literal
    /// classifier (<see cref="AstHelper.TryGetLiteralValue"/>): a string literal → its text; an
    /// integer literal → its source text (e.g. <c>"42"</c>, <c>"-42"</c> for a negated one); a
    /// float literal → a boxed <see cref="double"/> (negation applied); a boolean → <see cref="bool"/>;
    /// <c>None</c> → <c>null</c>; anything else → the AST record's <c>ToString()</c> (the
    /// pre-existing fallback, so a generator can at least see what it was given).
    /// </summary>
    internal static object ExtractLiteralValue(Expression expr)
    {
        var value = AstHelper.TryGetLiteralValue(expr);
        if (value == AstHelper.NoneValue)
            return null!;
        return value ?? expr.ToString() ?? "";
    }
}
