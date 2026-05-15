extern alias SharpyRT;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

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
    private readonly SemanticInfo _semanticInfo;
    private readonly SemanticBinding _semanticBinding;
    private readonly ICompilerLogger _logger;

    public GeneratorContextBuilder(SemanticInfo semanticInfo, SemanticBinding semanticBinding, ICompilerLogger logger)
    {
        _semanticInfo = semanticInfo;
        _semanticBinding = semanticBinding;
        _logger = logger;
    }

    public GGeneratorContext Build(Statement declaration, Decorator trigger, string moduleName)
    {
        GClassInfo? classInfo = null;
        GFunctionInfo? functionInfo = null;

        if (declaration is ClassDef classDef)
            classInfo = BuildClassInfo(classDef);
        else if (declaration is FunctionDef funcDef)
            functionInfo = BuildFunctionInfo(funcDef);
        else if (declaration is StructDef structDef)
            classInfo = BuildStructInfo(structDef);

        var arguments = new List<object>();
        foreach (var arg in trigger.Arguments)
        {
            arguments.Add(ExtractLiteralValue(arg));
        }

        var keywordArgs = new Dictionary<string, object>();
        foreach (var kwarg in trigger.KeywordArguments)
        {
            keywordArgs[kwarg.Name] = ExtractLiteralValue(kwarg.Value);
        }

        return new GGeneratorContext(classInfo, functionInfo, arguments, keywordArgs, moduleName);
    }

    private GClassInfo BuildClassInfo(ClassDef classDef)
    {
        var fields = new List<GFieldInfo>();
        var methods = new List<GMethodInfo>();
        var baseClasses = new List<string>();
        var decorators = new List<GDecoratorInfo>();

        foreach (var stmt in classDef.Body)
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

        foreach (var baseAnnot in classDef.BaseClasses)
        {
            baseClasses.Add(baseAnnot.Name);
        }

        foreach (var dec in classDef.Decorators)
        {
            decorators.Add(BuildDecoratorInfo(dec));
        }

        var isDataclass = classDef.Decorators.Any(d => d.Name == "dataclass");

        return new GClassInfo(classDef.Name, fields, methods, baseClasses, decorators, isDataclass);
    }

    private GClassInfo BuildStructInfo(StructDef structDef)
    {
        var fields = new List<GFieldInfo>();
        var methods = new List<GMethodInfo>();
        var baseClasses = new List<string>();
        var decorators = new List<GDecoratorInfo>();

        foreach (var stmt in structDef.Body)
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

        foreach (var baseAnnot in structDef.BaseClasses)
        {
            baseClasses.Add(baseAnnot.Name);
        }

        foreach (var dec in structDef.Decorators)
        {
            decorators.Add(BuildDecoratorInfo(dec));
        }

        return new GClassInfo(structDef.Name, fields, methods, baseClasses, decorators, false);
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

        return new GDecoratorInfo(dec.Name, args, kwargs, dec.IsBracketAttribute);
    }

    private static object ExtractLiteralValue(Expression expr)
    {
        return expr switch
        {
            StringLiteral s => s.Value,
            IntegerLiteral i => i.Value,
            FloatLiteral f => f.Value,
            BooleanLiteral b => b.Value,
            NoneLiteral => null!,
            _ => expr.ToString() ?? ""
        };
    }
}
