using System.Collections.Generic;
using Sharpy.Generators;
using Xunit;

namespace Sharpy.Core.Tests.Generators;

public class GeneratorTypesTests
{
    // ---------- SourceGenerator ----------

    private sealed class TestGenerator : SourceGenerator
    {
        public override GeneratorOutput Generate(GeneratorContext context)
        {
            return new GeneratorOutput("# generated", new List<GeneratorDiagnostic>());
        }
    }

    [Fact]
    public void SourceGenerator_ConcreteSubclass_CanImplementGenerate()
    {
        SourceGenerator generator = new TestGenerator();
        var context = new GeneratorContext(
            targetClass: null,
            targetFunction: null,
            arguments: new List<object>(),
            keywordArguments: new Dictionary<string, object>(),
            moduleName: "test");

        var output = generator.Generate(context);

        Assert.Equal("# generated", output.Source);
        Assert.Empty(output.Diagnostics);
    }

    // ---------- GeneratorContext ----------

    [Fact]
    public void GeneratorContext_WithClassTarget_PropertiesAreSet()
    {
        var classInfo = new ClassInfo(
            "MyClass",
            new List<FieldInfo>(),
            new List<MethodInfo>(),
            new List<string>(),
            new List<DecoratorInfo>(),
            isDataclass: false);
        var args = new List<object> { 1, "two" };
        var kwargs = new Dictionary<string, object> { { "key", "value" } };

        var context = new GeneratorContext(classInfo, null, args, kwargs, "my.module");

        Assert.Same(classInfo, context.TargetClass);
        Assert.Null(context.TargetFunction);
        Assert.Same(args, context.Arguments);
        Assert.Same(kwargs, context.KeywordArguments);
        Assert.Equal("my.module", context.ModuleName);
    }

    [Fact]
    public void GeneratorContext_WithFunctionTarget_PropertiesAreSet()
    {
        var funcInfo = new FunctionInfo(
            "my_func",
            new List<ParameterInfo>(),
            returnType: "int",
            new List<DecoratorInfo>(),
            isStatic: false,
            isAsync: false);

        var context = new GeneratorContext(
            null,
            funcInfo,
            new List<object>(),
            new Dictionary<string, object>(),
            "module");

        Assert.Null(context.TargetClass);
        Assert.Same(funcInfo, context.TargetFunction);
        Assert.Equal("module", context.ModuleName);
    }

    [Fact]
    public void GeneratorContext_EmptyCollections_AreAccepted()
    {
        var context = new GeneratorContext(
            null,
            null,
            new List<object>(),
            new Dictionary<string, object>(),
            string.Empty);

        Assert.Empty(context.Arguments);
        Assert.Empty(context.KeywordArguments);
        Assert.Equal(string.Empty, context.ModuleName);
    }

    // ---------- GeneratorOutput ----------

    [Fact]
    public void GeneratorOutput_WithSourceAndDiagnostics_PropertiesAreSet()
    {
        var diags = new List<GeneratorDiagnostic>
        {
            new GeneratorDiagnostic("hello", GeneratorDiagnosticSeverity.Info)
        };

        var output = new GeneratorOutput("# code", diags);

        Assert.Equal("# code", output.Source);
        Assert.Same(diags, output.Diagnostics);
        Assert.Single(output.Diagnostics);
    }

    [Fact]
    public void GeneratorOutput_NullDiagnostics_DefaultsToEmptyList()
    {
        var output = new GeneratorOutput("# code");

        Assert.Equal("# code", output.Source);
        Assert.NotNull(output.Diagnostics);
        Assert.Empty(output.Diagnostics);
    }

    [Fact]
    public void GeneratorOutput_Empty_HasEmptySourceAndDiagnostics()
    {
        var empty = GeneratorOutput.Empty;

        Assert.Equal(string.Empty, empty.Source);
        Assert.Empty(empty.Diagnostics);
    }

    [Fact]
    public void GeneratorOutput_Empty_IsCachedSingleton()
    {
        var a = GeneratorOutput.Empty;
        var b = GeneratorOutput.Empty;

        Assert.Same(a, b);
    }

    // ---------- ClassInfo ----------

    [Fact]
    public void ClassInfo_AllProperties_AreSet()
    {
        var fields = new List<FieldInfo>
        {
            new FieldInfo("x", "int", hasDefault: false, defaultValue: null)
        };
        var methods = new List<MethodInfo>
        {
            new MethodInfo("foo", new List<ParameterInfo>(), null,
                isStatic: false, isAbstract: false, isVirtual: false, isAsync: false)
        };
        var bases = new List<string> { "Base", "IMixin" };
        var decorators = new List<DecoratorInfo>
        {
            new DecoratorInfo("dataclass", new List<object>(),
                new Dictionary<string, object>(), isBracketAttribute: false)
        };

        var classInfo = new ClassInfo("Point", fields, methods, bases, decorators, isDataclass: true);

        Assert.Equal("Point", classInfo.Name);
        Assert.Same(fields, classInfo.Fields);
        Assert.Same(methods, classInfo.Methods);
        Assert.Same(bases, classInfo.BaseClasses);
        Assert.Same(decorators, classInfo.Decorators);
        Assert.True(classInfo.IsDataclass);
    }

    [Fact]
    public void ClassInfo_EmptyCollections_AreAccepted()
    {
        var classInfo = new ClassInfo(
            "Empty",
            new List<FieldInfo>(),
            new List<MethodInfo>(),
            new List<string>(),
            new List<DecoratorInfo>(),
            isDataclass: false);

        Assert.Equal("Empty", classInfo.Name);
        Assert.Empty(classInfo.Fields);
        Assert.Empty(classInfo.Methods);
        Assert.Empty(classInfo.BaseClasses);
        Assert.Empty(classInfo.Decorators);
        Assert.False(classInfo.IsDataclass);
    }

    // ---------- FunctionInfo ----------

    [Fact]
    public void FunctionInfo_AllProperties_AreSet()
    {
        var parameters = new List<ParameterInfo>
        {
            new ParameterInfo("x", "int", hasDefault: false),
            new ParameterInfo("y", "str", hasDefault: true)
        };
        var decorators = new List<DecoratorInfo>
        {
            new DecoratorInfo("cache", new List<object>(),
                new Dictionary<string, object>(), isBracketAttribute: false)
        };

        var funcInfo = new FunctionInfo(
            "compute",
            parameters,
            returnType: "int",
            decorators,
            isStatic: true,
            isAsync: true);

        Assert.Equal("compute", funcInfo.Name);
        Assert.Same(parameters, funcInfo.Parameters);
        Assert.Equal("int", funcInfo.ReturnType);
        Assert.Same(decorators, funcInfo.Decorators);
        Assert.True(funcInfo.IsStatic);
        Assert.True(funcInfo.IsAsync);
    }

    [Fact]
    public void FunctionInfo_NullReturnType_IsAllowed()
    {
        var funcInfo = new FunctionInfo(
            "void_func",
            new List<ParameterInfo>(),
            returnType: null,
            new List<DecoratorInfo>(),
            isStatic: false,
            isAsync: false);

        Assert.Null(funcInfo.ReturnType);
        Assert.False(funcInfo.IsStatic);
        Assert.False(funcInfo.IsAsync);
    }

    // ---------- FieldInfo ----------

    [Fact]
    public void FieldInfo_WithDefault_PropertiesAreSet()
    {
        var field = new FieldInfo("count", "int", hasDefault: true, defaultValue: "0");

        Assert.Equal("count", field.Name);
        Assert.Equal("int", field.TypeName);
        Assert.True(field.HasDefault);
        Assert.Equal("0", field.DefaultValue);
    }

    [Fact]
    public void FieldInfo_WithoutDefault_HasNullDefaultValue()
    {
        var field = new FieldInfo("name", "str", hasDefault: false, defaultValue: null);

        Assert.Equal("name", field.Name);
        Assert.Equal("str", field.TypeName);
        Assert.False(field.HasDefault);
        Assert.Null(field.DefaultValue);
    }

    [Fact]
    public void FieldInfo_NullTypeName_IsAllowed()
    {
        var field = new FieldInfo("untyped", typeName: null, hasDefault: false, defaultValue: null);

        Assert.Equal("untyped", field.Name);
        Assert.Null(field.TypeName);
    }

    // ---------- MethodInfo ----------

    [Fact]
    public void MethodInfo_AllProperties_AreSet()
    {
        var parameters = new List<ParameterInfo>
        {
            new ParameterInfo("self", null, hasDefault: false)
        };

        var method = new MethodInfo(
            "do_thing",
            parameters,
            returnType: "bool",
            isStatic: false,
            isAbstract: true,
            isVirtual: true,
            isAsync: true);

        Assert.Equal("do_thing", method.Name);
        Assert.Same(parameters, method.Parameters);
        Assert.Equal("bool", method.ReturnType);
        Assert.False(method.IsStatic);
        Assert.True(method.IsAbstract);
        Assert.True(method.IsVirtual);
        Assert.True(method.IsAsync);
    }

    [Fact]
    public void MethodInfo_StaticMethod_FlagsAreCorrect()
    {
        var method = new MethodInfo(
            "factory",
            new List<ParameterInfo>(),
            returnType: "Self",
            isStatic: true,
            isAbstract: false,
            isVirtual: false,
            isAsync: false);

        Assert.True(method.IsStatic);
        Assert.False(method.IsAbstract);
        Assert.False(method.IsVirtual);
        Assert.False(method.IsAsync);
    }

    // ---------- ParameterInfo ----------

    [Fact]
    public void ParameterInfo_WithDefault_PropertiesAreSet()
    {
        var param = new ParameterInfo("count", "int", hasDefault: true);

        Assert.Equal("count", param.Name);
        Assert.Equal("int", param.TypeName);
        Assert.True(param.HasDefault);
    }

    [Fact]
    public void ParameterInfo_WithoutDefault_HasDefaultIsFalse()
    {
        var param = new ParameterInfo("x", "str", hasDefault: false);

        Assert.Equal("x", param.Name);
        Assert.Equal("str", param.TypeName);
        Assert.False(param.HasDefault);
    }

    [Fact]
    public void ParameterInfo_NullTypeName_IsAllowed()
    {
        var param = new ParameterInfo("untyped", typeName: null, hasDefault: false);

        Assert.Equal("untyped", param.Name);
        Assert.Null(param.TypeName);
        Assert.False(param.HasDefault);
    }

    // ---------- DecoratorInfo ----------

    [Fact]
    public void DecoratorInfo_AllProperties_AreSet()
    {
        var args = new List<object> { 1, "x" };
        var kwargs = new Dictionary<string, object> { { "k", "v" } };

        var decorator = new DecoratorInfo("my_decorator", args, kwargs, isBracketAttribute: false);

        Assert.Equal("my_decorator", decorator.Name);
        Assert.Same(args, decorator.Arguments);
        Assert.Same(kwargs, decorator.KeywordArguments);
        Assert.False(decorator.IsBracketAttribute);
    }

    [Fact]
    public void DecoratorInfo_BracketAttribute_IsBracketAttributeIsTrue()
    {
        var decorator = new DecoratorInfo(
            "MyAttr",
            new List<object>(),
            new Dictionary<string, object>(),
            isBracketAttribute: true);

        Assert.Equal("MyAttr", decorator.Name);
        Assert.True(decorator.IsBracketAttribute);
    }

    [Fact]
    public void DecoratorInfo_EmptyArgs_AreAccepted()
    {
        var decorator = new DecoratorInfo(
            "noop",
            new List<object>(),
            new Dictionary<string, object>(),
            isBracketAttribute: false);

        Assert.Empty(decorator.Arguments);
        Assert.Empty(decorator.KeywordArguments);
    }

    // ---------- GeneratorDiagnostic ----------

    [Fact]
    public void GeneratorDiagnostic_PropertiesAreSet()
    {
        var diag = new GeneratorDiagnostic("something went wrong", GeneratorDiagnosticSeverity.Error);

        Assert.Equal("something went wrong", diag.Message);
        Assert.Equal(GeneratorDiagnosticSeverity.Error, diag.Severity);
    }

    [Theory]
    [InlineData(GeneratorDiagnosticSeverity.Info)]
    [InlineData(GeneratorDiagnosticSeverity.Warning)]
    [InlineData(GeneratorDiagnosticSeverity.Error)]
    public void GeneratorDiagnostic_AcceptsAllSeverities(GeneratorDiagnosticSeverity severity)
    {
        var diag = new GeneratorDiagnostic("msg", severity);

        Assert.Equal(severity, diag.Severity);
        Assert.Equal("msg", diag.Message);
    }

    [Fact]
    public void GeneratorDiagnostic_EmptyMessage_IsAllowed()
    {
        var diag = new GeneratorDiagnostic(string.Empty, GeneratorDiagnosticSeverity.Info);

        Assert.Equal(string.Empty, diag.Message);
    }

    // ---------- GeneratorDiagnosticSeverity ----------

    [Fact]
    public void GeneratorDiagnosticSeverity_HasExpectedValues()
    {
        Assert.Equal(0, (int)GeneratorDiagnosticSeverity.Info);
        Assert.Equal(1, (int)GeneratorDiagnosticSeverity.Warning);
        Assert.Equal(2, (int)GeneratorDiagnosticSeverity.Error);
    }

    [Fact]
    public void GeneratorDiagnosticSeverity_IsDefinedForAllNamedValues()
    {
        Assert.True(System.Enum.IsDefined(typeof(GeneratorDiagnosticSeverity), GeneratorDiagnosticSeverity.Info));
        Assert.True(System.Enum.IsDefined(typeof(GeneratorDiagnosticSeverity), GeneratorDiagnosticSeverity.Warning));
        Assert.True(System.Enum.IsDefined(typeof(GeneratorDiagnosticSeverity), GeneratorDiagnosticSeverity.Error));
    }
}
