using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class ProtocolValidatorTests
{
    private (Module module, SemanticContext context) Parse(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        // Run name resolution
        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        // Run type checking to populate semantic info
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void ForLoop_IterableList_NoError()
    {
        var code = @"
def foo() -> None:
    items: list[int] = [1, 2, 3]
    for x in items:
        pass
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ForLoop_IterableString_NoError()
    {
        var code = @"
def foo() -> None:
    for c in ""hello"":
        pass
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ForLoop_IterableDict_NoError()
    {
        var code = @"
def foo() -> None:
    data: dict[str, int] = {}
    for key in data:
        pass
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ForLoop_IterableSet_NoError()
    {
        var code = @"
def foo() -> None:
    s: set[int] = {1, 2, 3}
    for x in s:
        pass
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void IndexAccess_List_NoError()
    {
        var code = @"
def foo() -> None:
    items: list[int] = [1, 2, 3]
    x: int = items[0]
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void IndexAccess_Dict_NoError()
    {
        var code = @"
def foo() -> None:
    data: dict[str, int] = {}
    x: int = data[""key""]
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void IndexAccess_String_NoError()
    {
        var code = @"
def foo() -> None:
    s: str = ""hello""
    c: str = s[0]
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void MembershipTest_List_NoError()
    {
        var code = @"
def foo() -> None:
    items: list[int] = [1, 2, 3]
    result: bool = 1 in items
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void MembershipTest_Dict_NoError()
    {
        var code = @"
def foo() -> None:
    data: dict[str, int] = {}
    result: bool = ""key"" in data
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void MembershipTest_Set_NoError()
    {
        var code = @"
def foo() -> None:
    s: set[int] = {1, 2, 3}
    result: bool = 1 in s
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void MembershipTest_String_NoError()
    {
        var code = @"
def foo() -> None:
    s: str = ""hello""
    result: bool = ""h"" in s
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void LenCall_List_NoError()
    {
        var code = @"
def foo() -> None:
    items: list[int] = [1, 2, 3]
    n: int = len(items)
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void LenCall_String_NoError()
    {
        var code = @"
def foo() -> None:
    s: str = ""hello""
    n: int = len(s)
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ListComprehension_NoError()
    {
        var code = @"
def foo() -> None:
    items: list[int] = [1, 2, 3]
    doubled: list[int] = [x * 2 for x in items]
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void NotIn_Operator_NoError()
    {
        var code = @"
def foo() -> None:
    items: list[int] = [1, 2, 3]
    result: bool = 5 not in items
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ClassWithIterMethod_NoError()
    {
        var code = @"
class MyIterable:
    def __iter__(self) -> MyIterable:
        return self

def foo() -> None:
    obj: MyIterable = MyIterable()
    for x in obj:
        pass
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ClassWithContainsMethod_NoError()
    {
        var code = @"
class MyContainer:
    def __contains__(self, item: int) -> bool:
        return True

def foo() -> None:
    obj: MyContainer = MyContainer()
    result: bool = 5 in obj
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ClassWithGetitemMethod_NoError()
    {
        var code = @"
class MySequence:
    def __getitem__(self, index: int) -> int:
        return 0

def foo() -> None:
    obj: MySequence = MySequence()
    x: int = obj[0]
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ClassWithLenMethod_NoError()
    {
        var code = @"
class MySized:
    def __len__(self) -> int:
        return 0

def foo() -> None:
    obj: MySized = MySized()
    n: int = len(obj)
";
        var (module, context) = Parse(code);

        var validator = new ProtocolValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }
}
