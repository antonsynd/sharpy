using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using Xunit;

namespace Sharpy.Compiler.Tests.Services;

public class SemanticQueryTests
{
    [Fact]
    public void SemanticInfo_Implements_ISemanticQuery()
    {
        var info = new SemanticInfo();
        Assert.IsAssignableFrom<ISemanticQuery>(info);
    }

    [Fact]
    public void GetExpressionType_ReturnsSetType()
    {
        var info = new SemanticInfo();
        var expr = new IntegerLiteral { Value = "42" };
        info.SetExpressionType(expr, SemanticType.Int);

        ISemanticQuery query = info;
        Assert.Equal(SemanticType.Int, query.GetExpressionType(expr));
    }

    [Fact]
    public void GetExpressionType_ReturnsNull_WhenNotSet()
    {
        ISemanticQuery query = new SemanticInfo();
        var expr = new IntegerLiteral { Value = "1" };
        Assert.Null(query.GetExpressionType(expr));
    }

    [Fact]
    public void GetNarrowedType_ReturnsNarrowedType()
    {
        var info = new SemanticInfo();
        var expr = new Identifier { Name = "x" };
        info.SetNarrowedType(expr, SemanticType.Str);

        ISemanticQuery query = info;
        Assert.Equal(SemanticType.Str, query.GetNarrowedType(expr));
    }

    [Fact]
    public void GetEffectiveType_PrefersNarrowedType()
    {
        var info = new SemanticInfo();
        var expr = new Identifier { Name = "x" };
        info.SetExpressionType(expr, SemanticType.Unknown);
        info.SetNarrowedType(expr, SemanticType.Int);

        ISemanticQuery query = info;
        Assert.Equal(SemanticType.Int, query.GetEffectiveType(expr));
    }

    [Fact]
    public void GetEffectiveType_FallsBackToExpressionType()
    {
        var info = new SemanticInfo();
        var expr = new Identifier { Name = "x" };
        info.SetExpressionType(expr, SemanticType.Bool);

        ISemanticQuery query = info;
        Assert.Equal(SemanticType.Bool, query.GetEffectiveType(expr));
    }

    [Fact]
    public void GetIdentifierSymbol_ReturnsSetSymbol()
    {
        var info = new SemanticInfo();
        var id = new Identifier { Name = "myVar" };
        var symbol = new VariableSymbol { Name = "myVar", Kind = SymbolKind.Variable };
        info.SetIdentifierSymbol(id, symbol);

        ISemanticQuery query = info;
        var result = query.GetIdentifierSymbol(id);
        Assert.NotNull(result);
        Assert.Equal("myVar", result.Name);
    }

    [Fact]
    public void GetCallTarget_ReturnsResolvedTarget()
    {
        var info = new SemanticInfo();
        var call = new FunctionCall { Function = new Identifier { Name = "foo" } };
        var target = new FunctionSymbol { Name = "foo", Kind = SymbolKind.Function };
        info.SetCallTarget(call, target);

        ISemanticQuery query = info;
        var result = query.GetCallTarget(call);
        Assert.NotNull(result);
        Assert.Equal("foo", result.Name);
    }

    [Fact]
    public void GetTypeAnnotation_ReturnsResolvedType()
    {
        var info = new SemanticInfo();
        var annotation = new TypeAnnotation { Name = "int" };
        info.SetTypeAnnotation(annotation, SemanticType.Int);

        ISemanticQuery query = info;
        Assert.Equal(SemanticType.Int, query.GetTypeAnnotation(annotation));
    }

    [Fact]
    public void GetInferredTypeArguments_ReturnsInferredArgs()
    {
        var info = new SemanticInfo();
        var call = new FunctionCall { Function = new Identifier { Name = "identity" } };
        var typeArgs = new List<SemanticType> { SemanticType.Int };
        info.SetInferredTypeArguments(call, typeArgs);

        ISemanticQuery query = info;
        var result = query.GetInferredTypeArguments(call);
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(SemanticType.Int, result[0]);
    }

    [Fact]
    public void GetInferredTypeArguments_ReturnsNull_WhenNotSet()
    {
        ISemanticQuery query = new SemanticInfo();
        var call = new FunctionCall { Function = new Identifier { Name = "foo" } };
        Assert.Null(query.GetInferredTypeArguments(call));
    }

    [Fact]
    public void CompileResult_ExposesSemanticQuery()
    {
        var info = new SemanticInfo();
        var result = new CompileResult { SemanticInfo = info };
        Assert.NotNull(result.SemanticQuery);
        Assert.Same(info, result.SemanticQuery);
    }

    [Fact]
    public void CompileResult_SemanticQuery_IsNull_WhenNoSemanticInfo()
    {
        var result = new CompileResult();
        Assert.Null(result.SemanticQuery);
    }

    [Fact]
    public void SemanticResult_ExposesSemanticQuery()
    {
        var info = new SemanticInfo();
        var result = new SemanticResult { SemanticInfo = info };
        Assert.NotNull(result.SemanticQuery);
        Assert.Same(info, result.SemanticQuery);
    }
}
