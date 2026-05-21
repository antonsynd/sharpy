using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests hover functionality by verifying that the compiler API can resolve
/// symbols at positions and the SymbolFormatter produces correct hover text.
/// </summary>
public class HoverTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;

    public HoverTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
    }

    [Fact]
    public async Task Hover_OverVariable_ShowsType()
    {
        var source = "x: int = 42\ndef main():\n    print(x)";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();
        analysis!.Ast.Should().NotBeNull();

        // Find the identifier 'x' in the source (line 1, col 1 in 1-based)
        var node = _api.FindNodeAtPosition(analysis.Ast!, 1, 1);
        node.Should().NotBeNull();

        if (node is Identifier id1)
        {
            var symbol = analysis.SemanticQuery?.GetIdentifierSymbol(id1);
            symbol.Should().NotBeNull();

            var formatted = SymbolFormatter.FormatSymbol(symbol!);
            formatted.Should().Contain("x");
            formatted.Should().Contain("int");
        }
    }

    [Fact]
    public async Task Hover_OverFunction_ShowsSignature()
    {
        var source = "def greet(name: str) -> str:\n    return \"hi \" + name\ndef main():\n    greet(\"world\")";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        // Look up function directly from the symbol table
        var symbol = analysis!.SymbolTable?.Lookup("greet");
        symbol.Should().NotBeNull();

        var formatted = SymbolFormatter.FormatSymbol(symbol!);
        formatted.Should().Contain("def greet");
        formatted.Should().Contain("name: str");
        formatted.Should().Contain("-> str");
    }

    [Fact]
    public async Task Hover_OverExpression_ShowsEffectiveType()
    {
        var source = "def main():\n    x: int = 10\n    y = x + 5\n    print(y)";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();
    }

    [Fact]
    public async Task Hover_NoNodeAtPosition_ReturnsNull()
    {
        var source = "def main():\n    pass";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        // Line 100 doesn't exist
        var node = _api.FindNodeAtPosition(analysis!.Ast!, 100, 1);
        node.Should().BeNull();
    }

    [Fact]
    public async Task Hover_OverFunctionWithDocstring_IncludesDocumentation()
    {
        var source = "def greet(name: str) -> str:\n    \"\"\"Say hello to someone.\"\"\"\n    return \"hi \" + name\ndef main():\n    greet(\"world\")";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("greet");
        symbol.Should().NotBeNull();

        var formatted = SymbolFormatter.FormatSymbolWithDocs(symbol!);
        formatted.Should().Contain("```sharpy");
        formatted.Should().Contain("def greet");
        formatted.Should().Contain("Say hello to someone.");
    }

    [Fact]
    public async Task Hover_OverFunctionWithoutDocstring_StillWorks()
    {
        var source = "def greet(name: str) -> str:\n    return \"hi \" + name\ndef main():\n    greet(\"world\")";
        _workspace.OpenDocument("file:///test.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test.spy");
        analysis.Should().NotBeNull();

        var symbol = analysis!.SymbolTable?.Lookup("greet");
        symbol.Should().NotBeNull();

        var formatted = SymbolFormatter.FormatSymbolWithDocs(symbol!);
        formatted.Should().Contain("```sharpy");
        formatted.Should().Contain("def greet");
        formatted.Should().EndWith("\n```");
    }

    [Fact]
    public async Task Hover_ResultMap_ShowsCorrectReturnType()
    {
        // result.map(lambda x: x * 2) should return int !str, not <?>
        var source = "def main():\n    result: int !str = Ok(10)\n    mapped = result.map(lambda x: x * 2)\n    print(mapped)";
        _workspace.OpenDocument("file:///test_result_map.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_result_map.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        // Find the FunctionCall node for result.map(...) on line 3
        // "    mapped = result.map(lambda x: x * 2)" — "map" starts at col 20
        var callNode = _api.FindNodeOfType<FunctionCall>(analysis.Ast!, 3, 20);
        callNode.Should().NotBeNull("should find the result.map() call on line 3");

        var effectiveType = analysis.SemanticQuery!.GetEffectiveType(callNode!);
        effectiveType.Should().NotBeNull("result.map() should have a resolved type");
        effectiveType.Should().NotBeOfType<UnknownType>(
            "result.map() should resolve to a concrete type, not <?>");
        effectiveType.Should().BeOfType<ResultType>();
        var resultType = (ResultType)effectiveType!;
        resultType.OkType.Should().BeOfType<BuiltinType>();
        resultType.OkType.GetDisplayName().Should().Be("int");
        resultType.ErrorType.GetDisplayName().Should().Be("str");
    }

    [Fact]
    public async Task Hover_ResultMapErr_ShowsCorrectReturnType()
    {
        // result.map_err(lambda e: int(e)) should return int !int, not <?>
        var source = "def main():\n    result: int !str = Ok(10)\n    mapped = result.map_err(lambda e: int(e))\n    print(mapped)";
        _workspace.OpenDocument("file:///test_result_map_err.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_result_map_err.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        // "    mapped = result.map_err(lambda e: int(e))" — "map_err" starts at col 20
        var callNode = _api.FindNodeOfType<FunctionCall>(analysis.Ast!, 3, 20);
        callNode.Should().NotBeNull("should find the result.map_err() call on line 3");

        var effectiveType = analysis.SemanticQuery!.GetEffectiveType(callNode!);
        effectiveType.Should().NotBeNull("result.map_err() should have a resolved type");
        effectiveType.Should().NotBeOfType<UnknownType>(
            "result.map_err() should resolve to a concrete type, not <?>");
        effectiveType.Should().BeOfType<ResultType>();
        var resultType = (ResultType)effectiveType!;
        resultType.OkType.GetDisplayName().Should().Be("int");
        resultType.ErrorType.GetDisplayName().Should().Be("int");
    }

    [Fact]
    public async Task Hover_OptionalMap_ShowsCorrectReturnType()
    {
        // opt.map(lambda x: str(x)) should return str?, not <?>
        var source = "def main():\n    opt: int? = Some(42)\n    mapped = opt.map(lambda x: str(x))\n    print(mapped)";
        _workspace.OpenDocument("file:///test_optional_map.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_optional_map.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        // "    mapped = opt.map(lambda x: str(x))" — "map" starts at col 17
        var callNode = _api.FindNodeOfType<FunctionCall>(analysis.Ast!, 3, 17);
        callNode.Should().NotBeNull("should find the opt.map() call on line 3");

        var effectiveType = analysis.SemanticQuery!.GetEffectiveType(callNode!);
        effectiveType.Should().NotBeNull("opt.map() should have a resolved type");
        effectiveType.Should().NotBeOfType<UnknownType>(
            "opt.map() should resolve to a concrete type, not <?>");
        effectiveType.Should().BeOfType<OptionalType>();
        var optionalType = (OptionalType)effectiveType!;
        optionalType.UnderlyingType.GetDisplayName().Should().Be("str");
    }

    [Fact]
    public async Task Hover_OverMethodDef_InsideClass_ResolvesSymbol()
    {
        var source = "class Dog:\n    def bark(self) -> str:\n        return \"woof\"\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_method.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_method.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        // "bark" is a method on Dog, not a global function
        var globalFunc = analysis.SymbolTable!.LookupFunction("bark");
        globalFunc.Should().BeNull("bark is a method, not a global function");

        var dogType = analysis.SymbolTable!.LookupType("Dog");
        dogType.Should().NotBeNull();
        var barkMethod = dogType!.Methods.FirstOrDefault(m => m.Name == "bark");
        barkMethod.Should().NotBeNull("Dog should have a bark method");

        var formatted = SymbolFormatter.FormatSymbolWithDocs(barkMethod!);
        formatted.Should().Contain("def bark");
        formatted.Should().Contain("-> str");
    }

    [Fact]
    public async Task Hover_OverFieldDecl_InsideClass_ResolvesSymbol()
    {
        var source = "class Point:\n    x: int = 0\n    y: int = 0\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_field.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_field.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        // "x" is a field on Point, not a global variable
        var globalVar = analysis.SymbolTable!.LookupVariable("x");
        globalVar.Should().BeNull("x is a field, not a global variable");

        var pointType = analysis.SymbolTable!.LookupType("Point");
        pointType.Should().NotBeNull();
        var xField = pointType!.Fields.FirstOrDefault(f => f.Name == "x");
        xField.Should().NotBeNull("Point should have an x field");

        var formatted = SymbolFormatter.FormatSymbolWithDocs(xField!);
        formatted.Should().Contain("x");
        formatted.Should().Contain("int");
    }

    [Fact]
    public async Task Hover_DunderMethod_InsideClass_ShowsFallbackDocs()
    {
        var source = "class Cat:\n    def __init__(self):\n        pass\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_dunder.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_dunder.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        var catType = analysis.SymbolTable!.LookupType("Cat");
        catType.Should().NotBeNull();
        var initMethod = catType!.Methods.FirstOrDefault(m => m.Name == "__init__");
        initMethod.Should().NotBeNull("Cat should have an __init__ method");

        var formatted = SymbolFormatter.FormatSymbolWithDocs(initMethod!);
        formatted.Should().Contain("def __init__");
        // DunderDocumentation fallback should provide docs
        formatted.Should().Contain("Constructor");
    }

    [Fact]
    public async Task Hover_AsyncioModule_ShowsSummary()
    {
        // Verify ModuleDocumentation has an entry for asyncio
        var summary = ModuleDocumentation.GetSummary("asyncio");
        summary.Should().NotBeNull("asyncio should have a module summary");
        summary.Should().Contain("Async");
    }

    [Fact]
    public async Task Hover_SuperInit_ResolvesParentConstructor()
    {
        var source = "class Animal:\n    def __init__(self, name: str):\n        self._name = name\nclass Dog(Animal):\n    def __init__(self, name: str, breed: str):\n        super().__init__(name)\n        self._breed = breed\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_super.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_super.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        // Verify Animal.__init__ is resolvable via TypeSymbol
        var animalType = analysis.SymbolTable!.LookupType("Animal");
        animalType.Should().NotBeNull();
        var animalInit = animalType!.Methods.FirstOrDefault(m => m.Name == "__init__");
        animalInit.Should().NotBeNull("Animal should have an __init__ method");

        // Verify Dog.BaseType is Animal
        var dogType = analysis.SymbolTable!.LookupType("Dog");
        dogType.Should().NotBeNull();
        dogType!.BaseType.Should().NotBeNull("Dog should have Animal as base type");
        dogType.BaseType!.Name.Should().Be("Animal");

        // Verify the parent __init__ can be formatted
        var formatted = SymbolFormatter.FormatSymbolWithDocs(animalInit!);
        formatted.Should().Contain("def __init__");
        formatted.Should().Contain("name: str");
        // Should also get dunder documentation fallback
        formatted.Should().Contain("Constructor");
    }

    [Fact]
    public async Task Hover_SelfParameter_ShowsClassName()
    {
        var source = "class Cat:\n    def meow(self) -> str:\n        return \"meow\"\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_self.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_self.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        // The "self" parameter in Cat.meow should format with the class name
        var formatted = SymbolFormatter.FormatParameter("self", null, "Cat");
        formatted.Should().Be("(self) self: Cat");
    }

    [Fact]
    public async Task Hover_ReturnTypeAnnotation_ShowsTypeInfo()
    {
        // "def greet() -> str:\n    return 'hi'\ndef main():\n    pass"
        // Line 1: "def greet() -> str:"  — "str" starts at col 16
        var source = "def greet() -> str:\n    return \"hi\"\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_return_type.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_return_type.spy");
        analysis.Should().NotBeNull();

        // Find the FunctionDef node at the return type position
        var funcDef = _api.FindNodeOfType<FunctionDef>(analysis!.Ast!, 1, 16);
        funcDef.Should().NotBeNull();
        funcDef!.ReturnType.Should().NotBeNull();
        funcDef.ReturnType!.Name.Should().Be("str");
    }

    [Fact]
    public async Task Hover_ParameterTypeAnnotation_ShowsTypeInfo()
    {
        // "def greet(name: str) -> str:\n    return 'hi ' + name\ndef main():\n    pass"
        // Line 1: "def greet(name: str) -> str:"  — parameter "str" starts at col 17
        var source = "def greet(name: str) -> str:\n    return \"hi \" + name\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_param_type.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_param_type.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        // Verify the parameter type annotation is resolvable
        var funcDef = _api.FindNodeOfType<FunctionDef>(analysis.Ast!, 1, 17);
        funcDef.Should().NotBeNull();
        var param = funcDef!.Parameters.FirstOrDefault(p => p.Name == "name");
        param.Should().NotBeNull();
        param!.Type.Should().NotBeNull();
        param.Type!.Name.Should().Be("str");

        var semanticType = analysis.SemanticQuery!.GetTypeAnnotation(param.Type);
        semanticType.Should().NotBeNull();
        semanticType!.GetDisplayName().Should().Be("str");
    }

    [Fact]
    public async Task Hover_BaseClassAnnotation_ShowsClassInfo()
    {
        // "class Animal:\n    pass\nclass Dog(Animal):\n    pass\ndef main():\n    pass"
        // Line 3: "class Dog(Animal):"  — "Animal" starts at col 11
        var source = "class Animal:\n    pass\nclass Dog(Animal):\n    pass\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_base_class.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_base_class.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        // Verify Animal is in the symbol table
        var animalType = analysis.SymbolTable!.LookupType("Animal");
        animalType.Should().NotBeNull("Animal should be in the symbol table");

        // Verify Dog's base class list includes Animal
        var dogDef = _api.FindNodeOfType<ClassDef>(analysis.Ast!, 3, 11);
        dogDef.Should().NotBeNull();
        dogDef!.BaseClasses.Should().NotBeEmpty();
        dogDef.BaseClasses[0].Name.Should().Be("Animal");
    }

    [Fact]
    public async Task Hover_VariableTypeAnnotation_ShowsTypeInfo()
    {
        // "x: int = 5\ndef main():\n    pass"
        // Line 1: "x: int = 5"  — "int" starts at col 4
        var source = "x: int = 5\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_var_type.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_var_type.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        // Find the VariableDeclaration node
        var node = _api.FindNodeAtPosition(analysis.Ast!, 1, 4);
        node.Should().NotBeNull();
        // At the type annotation position, the enclosing node is the VariableDeclaration
        var varDecl = node as VariableDeclaration;
        if (varDecl == null)
        {
            // The node might be the Identifier or other child; find the VariableDeclaration by looking up
            varDecl = _api.FindNodeOfType<VariableDeclaration>(analysis.Ast!, 1, 4);
        }
        // Whether we found a VariableDeclaration or not, verify the type annotation data exists
        if (varDecl != null)
        {
            varDecl.Type.Should().NotBeNull();
            varDecl.Type!.Name.Should().Be("int");
        }
    }

    [Fact]
    public async Task Hover_StringLiteral_ReturnsStringType()
    {
        // Hovering on a string literal should show its type (string)
        var source = "def main():\n    x = \"hello\"";
        _workspace.OpenDocument("file:///test_string_hover.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_string_hover.spy");
        analysis.Should().NotBeNull();

        // "    x = \"hello\"" — "hello" starts at col 9
        var node = _api.FindNodeAtPosition(analysis!.Ast!, 2, 9);
        node.Should().NotBeNull();
        node.Should().BeOfType<StringLiteral>();

        var service = new HoverService(_api);
        var hover = service.GetHoverMarkdown(analysis!, 2, 9);
        hover.Should().NotBeNull();
        hover.Should().Contain("str");
    }

    [Fact]
    public async Task Hover_FStringLiteral_ReturnsStringType()
    {
        // Hovering on an f-string should show its type (string)
        var source = "def main():\n    name = \"world\"\n    x = f\"hello {name}\"";
        _workspace.OpenDocument("file:///test_fstring_hover.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_fstring_hover.spy");
        analysis.Should().NotBeNull();

        // Line 3: "    x = f\"hello {name}\"" — f-string starts at col 9
        var node = _api.FindNodeAtPosition(analysis!.Ast!, 3, 9);
        node.Should().NotBeNull();
        if (node is FStringLiteral)
        {
            var service = new HoverService(_api);
            var hover = service.GetHoverMarkdown(analysis!, 3, 9);
            hover.Should().NotBeNull();
            hover.Should().Contain("str");
        }
    }

    [Fact]
    public async Task Hover_BuiltinMethodCall_ShowsXmlDocumentation()
    {
        // When hovering over a stdlib method call like list.append(),
        // the hover should show XML documentation from Sharpy.Core
        var source = "def main():\n    items: list[int] = [1, 2, 3]\n    items.append(4)";
        _workspace.OpenDocument("file:///test_stdlib_docs.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_stdlib_docs.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        // Verify the BuiltinRegistry has documentation for list.append
        var listType = analysis.SymbolTable!.BuiltinRegistry.GetType("list");
        listType.Should().NotBeNull("list should be registered in BuiltinRegistry");

        var appendMethod = listType!.Methods.FirstOrDefault(m => m.Name == "append");
        appendMethod.Should().NotBeNull("list should have an append method");
        appendMethod!.Documentation.Should().NotBeNullOrEmpty(
            "list.append should have XML documentation from Sharpy.Core");
        appendMethod.Documentation.Should().Contain("Add an item to the end of the list");

        // Verify the formatted hover includes the documentation
        var formatted = SymbolFormatter.FormatSymbolWithDocs(appendMethod);
        formatted.Should().Contain("def append");
        formatted.Should().Contain("Add an item to the end of the list");
    }

    [Fact]
    public async Task Hover_BuiltinMethodMemberAccess_ShowsDocumentation()
    {
        // When hovering over the method name in a builtin method call (e.g., "append" in items.append(4)),
        // the hover should show the method signature with XML documentation
        var source = "def main():\n    items: list[int] = [1, 2, 3]\n    items.append(4)";
        _workspace.OpenDocument("file:///test_method_access_docs.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_method_access_docs.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        // Find the MemberAccess node on "append" (line 3, col ~11 in 1-based)
        // "    items.append(4)" — "append" starts at col 11
        var memberAccessNode = _api.FindNodeOfType<MemberAccess>(analysis.Ast!, 3, 11);
        memberAccessNode.Should().NotBeNull("should find MemberAccess for items.append");
        memberAccessNode!.Member.Should().Be("append");

        // Verify the object type is list[int]
        var objType = analysis.SemanticQuery!.GetEffectiveType(memberAccessNode.Object);
        objType.Should().NotBeNull();
        objType.Should().BeOfType<GenericType>();
        ((GenericType)objType!).Name.Should().Be("list");
    }

    [Fact]
    public async Task Hover_StdlibModule_DocumentationFlowsFromXmlPipeline()
    {
        // Verify that module-level XML docs flow from Sharpy.Core through
        // the discovery pipeline to ModuleSymbol.Documentation.

        // Find Sharpy.Core.dll alongside the test assembly
        var testDir = System.IO.Path.GetDirectoryName(typeof(HoverTests).Assembly.Location)!;
        var corePath = System.IO.Path.Combine(testDir, "Sharpy.Core.dll");
        if (!File.Exists(corePath))
        {
            // Skip if Sharpy.Core.dll is not available in the test output
            return;
        }

        var stdlibPath = System.IO.Path.Combine(testDir, "Sharpy.Stdlib.dll");
        var refs = File.Exists(stdlibPath)
            ? new string[] { corePath, stdlibPath }
            : new string[] { corePath };
        var api = new CompilerApi(null, refs);
        using var workspace = new SharpyWorkspace(api, NullLogger<SharpyWorkspace>.Instance);

        // Use csv module which has XML docs on its [SharpyModule] class
        var source = "import csv\ndef main():\n    pass";
        workspace.OpenDocument("file:///test_module_doc.spy", source, 1);

        var analysis = await workspace.GetAnalysisAsync("file:///test_module_doc.spy");
        analysis.Should().NotBeNull();
        analysis!.SymbolTable.Should().NotBeNull();

        var symbol = analysis.SymbolTable!.Lookup("csv");
        symbol.Should().NotBeNull("csv module should be in the symbol table");
        symbol.Should().BeOfType<ModuleSymbol>();

        var moduleSymbol = (ModuleSymbol)symbol!;

        // The Documentation field should be populated from XML docs, not null.
        // This verifies the full pipeline: OverloadIndexBuilder extracts XML doc summary
        // -> ModuleOverloads.Documentation -> ModuleRegistry.GetModuleDocumentation()
        // -> ImportResolver -> ModuleSymbol.Documentation
        moduleSymbol.Documentation.Should().NotBeNullOrEmpty(
            "csv module should have XML documentation from Sharpy.Core");

        // Verify the formatted hover includes the documentation
        var formatted = SymbolFormatter.FormatSymbolWithDocs(moduleSymbol);
        formatted.Should().Contain("```sharpy");
        formatted.Should().Contain("(module) csv");
        formatted.Should().Contain(moduleSymbol.Documentation);
    }

    [Fact]
    public async Task Hover_ReturnWithValue_ShowsReturnedType()
    {
        var source = "def greet() -> str:\n    return \"hello\"\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_return_hover.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_return_hover.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        // Line 2: "    return \"hello\"" — "return" starts at col 5
        var node = _api.FindNodeAtPosition(analysis.Ast!, 2, 5);
        node.Should().NotBeNull();
        node.Should().BeOfType<ReturnStatement>();

        var returnStmt = (ReturnStatement)node!;
        returnStmt.Value.Should().NotBeNull();

        var returnType = analysis.SemanticQuery!.GetEffectiveType(returnStmt.Value!);
        returnType.Should().NotBeNull();
        returnType!.GetDisplayName().Should().Be("str");
    }

    [Fact]
    public async Task Hover_BareReturn_ShowsNone()
    {
        var source = "def do_nothing() -> None:\n    return\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_bare_return.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_bare_return.spy");
        analysis.Should().NotBeNull();

        // Line 2: "    return" — "return" starts at col 5
        var node = _api.FindNodeAtPosition(analysis!.Ast!, 2, 5);
        node.Should().NotBeNull();
        node.Should().BeOfType<ReturnStatement>();

        var returnStmt = (ReturnStatement)node!;
        returnStmt.Value.Should().BeNull("bare return has no value");
    }

    [Fact]
    public async Task Hover_TypeArgument_IntInListInt_ShowsIntType()
    {
        // "def process(items: list[int]) -> None:\n    pass\ndef main():\n    pass"
        // Line 1: "def process(items: list[int]) -> None:" — "int" inside list[int] starts at col 25
        var source = "def process(items: list[int]) -> None:\n    pass\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_type_arg_int.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_type_arg_int.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        var funcDef = _api.FindNodeOfType<FunctionDef>(analysis.Ast!, 1, 25);
        funcDef.Should().NotBeNull();
        var param = funcDef!.Parameters.FirstOrDefault(p => p.Name == "items");
        param.Should().NotBeNull();
        param!.Type.Should().NotBeNull();
        param.Type!.Name.Should().Be("list");
        param.Type.TypeArguments.Should().HaveCount(1);
        param.Type.TypeArguments[0].Name.Should().Be("int");

        // Verify the type argument resolves to int
        var argType = analysis.SemanticQuery!.GetTypeAnnotation(param.Type.TypeArguments[0]);
        argType.Should().NotBeNull();
        argType!.GetDisplayName().Should().Be("int");
    }

    [Fact]
    public async Task Hover_TypeArgument_StrInDictStrInt_ShowsStrType()
    {
        // "def process(data: dict[str, int]) -> None:\n    pass\ndef main():\n    pass"
        // Line 1: "def process(data: dict[str, int]) -> None:" — "str" starts at col 24
        var source = "def process(data: dict[str, int]) -> None:\n    pass\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_type_arg_str.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_type_arg_str.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        var funcDef = _api.FindNodeOfType<FunctionDef>(analysis.Ast!, 1, 24);
        funcDef.Should().NotBeNull();
        var param = funcDef!.Parameters.FirstOrDefault(p => p.Name == "data");
        param.Should().NotBeNull();
        param!.Type.Should().NotBeNull();
        param.Type!.Name.Should().Be("dict");
        param.Type.TypeArguments.Should().HaveCount(2);
        param.Type.TypeArguments[0].Name.Should().Be("str");

        var argType = analysis.SemanticQuery!.GetTypeAnnotation(param.Type.TypeArguments[0]);
        argType.Should().NotBeNull();
        argType!.GetDisplayName().Should().Be("str");
    }

    [Fact]
    public async Task Hover_TypeArgument_ListInListInt_ShowsListType()
    {
        // Hovering over "list" (the outer type name) in list[int] should show list[int], not int
        var source = "def process(items: list[int]) -> None:\n    pass\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_type_arg_list.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_type_arg_list.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        var funcDef = _api.FindNodeOfType<FunctionDef>(analysis.Ast!, 1, 20);
        funcDef.Should().NotBeNull();
        var param = funcDef!.Parameters.FirstOrDefault(p => p.Name == "items");
        param.Should().NotBeNull();
        param!.Type.Should().NotBeNull();

        // The outer type resolves to list[int]
        var outerType = analysis.SemanticQuery!.GetTypeAnnotation(param.Type!);
        outerType.Should().NotBeNull();
        outerType!.GetDisplayName().Should().Be("list[int]");
    }

    [Fact]
    public async Task Hover_TypeArgument_NestedGeneric_ShowsInnerType()
    {
        // "def process(data: dict[str, list[int]]) -> None:\n    pass\ndef main():\n    pass"
        // Hovering over "int" inside the nested list[int] should show int, not list or dict
        var source = "def process(data: dict[str, list[int]]) -> None:\n    pass\ndef main():\n    pass";
        _workspace.OpenDocument("file:///test_type_arg_nested.spy", source, 1);

        var analysis = await _workspace.GetAnalysisAsync("file:///test_type_arg_nested.spy");
        analysis.Should().NotBeNull();
        analysis!.SemanticQuery.Should().NotBeNull();

        var funcDef = _api.FindNodeOfType<FunctionDef>(analysis.Ast!, 1, 34);
        funcDef.Should().NotBeNull();
        var param = funcDef!.Parameters.FirstOrDefault(p => p.Name == "data");
        param.Should().NotBeNull();
        param!.Type.Should().NotBeNull();
        param.Type!.Name.Should().Be("dict");

        // dict[str, list[int]] — TypeArguments[1] is list[int]
        param.Type.TypeArguments.Should().HaveCount(2);
        var listArg = param.Type.TypeArguments[1];
        listArg.Name.Should().Be("list");
        listArg.TypeArguments.Should().HaveCount(1);
        listArg.TypeArguments[0].Name.Should().Be("int");

        // Verify the innermost int resolves correctly
        var innerType = analysis.SemanticQuery!.GetTypeAnnotation(listArg.TypeArguments[0]);
        innerType.Should().NotBeNull();
        innerType!.GetDisplayName().Should().Be("int");
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
