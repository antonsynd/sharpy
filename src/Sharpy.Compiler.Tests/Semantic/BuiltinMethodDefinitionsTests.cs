using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class BuiltinMethodDefinitionsTests
{
    private static List<TypeParameterDef> MakeTypeParams(params string[] names)
    {
        return names.Select(n => new TypeParameterDef { Name = n }).ToList();
    }

    // ---- Dict method definitions ----

    [Fact]
    public void Dict_Methods_Returns_Five_Methods()
    {
        var typeParams = MakeTypeParams("T0", "T1");
        var methods = BuiltinMethodDefinitions.GetMethods("dict", typeParams);
        Assert.Equal(5, methods.Count);
    }

    [Fact]
    public void Dict_Has_Get_Overloads()
    {
        var typeParams = MakeTypeParams("T0", "T1");
        var methods = BuiltinMethodDefinitions.GetMethods("dict", typeParams);
        var getMethods = methods.Where(m => m.Name == "get").ToList();
        Assert.Equal(2, getMethods.Count);

        // 1-arg get returns Optional[T1]
        var get1 = getMethods.First(m => m.Parameters.Count == 1);
        Assert.IsType<OptionalType>(get1.ReturnType);
        Assert.IsType<TypeParameterType>(((OptionalType)get1.ReturnType).UnderlyingType);
        Assert.Equal("T1", ((TypeParameterType)((OptionalType)get1.ReturnType).UnderlyingType).Name);

        // 2-arg get returns T1
        var get2 = getMethods.First(m => m.Parameters.Count == 2);
        Assert.IsType<TypeParameterType>(get2.ReturnType);
        Assert.Equal("T1", ((TypeParameterType)get2.ReturnType).Name);
    }

    [Fact]
    public void Dict_Items_Returns_DictItemsView()
    {
        var typeParams = MakeTypeParams("T0", "T1");
        var methods = BuiltinMethodDefinitions.GetMethods("dict", typeParams);
        var items = methods.First(m => m.Name == "items");
        Assert.IsType<GenericType>(items.ReturnType);
        Assert.Equal(BuiltinNames.DictItemsView, ((GenericType)items.ReturnType).Name);
        Assert.Equal(2, ((GenericType)items.ReturnType).TypeArguments.Count);
    }

    [Fact]
    public void Dict_Keys_Returns_DictKeyView()
    {
        var typeParams = MakeTypeParams("T0", "T1");
        var methods = BuiltinMethodDefinitions.GetMethods("dict", typeParams);
        var keys = methods.First(m => m.Name == "keys");
        Assert.IsType<GenericType>(keys.ReturnType);
        Assert.Equal(BuiltinNames.DictKeyView, ((GenericType)keys.ReturnType).Name);
    }

    [Fact]
    public void Dict_Values_Returns_DictValuesView()
    {
        var typeParams = MakeTypeParams("T0", "T1");
        var methods = BuiltinMethodDefinitions.GetMethods("dict", typeParams);
        var values = methods.First(m => m.Name == "values");
        Assert.IsType<GenericType>(values.ReturnType);
        Assert.Equal(BuiltinNames.DictValuesView, ((GenericType)values.ReturnType).Name);
    }

    [Fact]
    public void Dict_OperatorMethods_Has_Or_Eq_Ne()
    {
        var typeParams = MakeTypeParams("T0", "T1");
        var ops = BuiltinMethodDefinitions.GetOperatorMethods("dict", typeParams);
        Assert.Contains(DunderNames.Or, ops.Keys);
        Assert.Contains(DunderNames.Eq, ops.Keys);
        Assert.Contains(DunderNames.Ne, ops.Keys);
        Assert.Equal(3, ops.Count);
    }

    [Fact]
    public void Dict_ProtocolMethods_Has_Expected_Protocols()
    {
        var typeParams = MakeTypeParams("T0", "T1");
        var protocols = BuiltinMethodDefinitions.GetProtocolMethods("dict", typeParams);
        Assert.Contains(DunderNames.Len, protocols.Keys);
        Assert.Contains(DunderNames.Iter, protocols.Keys);
        Assert.Contains(DunderNames.Contains, protocols.Keys);
        Assert.Contains(DunderNames.GetItem, protocols.Keys);
        Assert.Contains(DunderNames.SetItem, protocols.Keys);
        Assert.Equal(5, protocols.Count);
    }

    // ---- List method definitions ----

    [Fact]
    public void List_Methods_Returns_Expected_Count()
    {
        var typeParams = MakeTypeParams("T0");
        var methods = BuiltinMethodDefinitions.GetMethods("list", typeParams);
        Assert.Equal(12, methods.Count);
    }

    [Fact]
    public void List_Has_Pop_Overloads()
    {
        var typeParams = MakeTypeParams("T0");
        var methods = BuiltinMethodDefinitions.GetMethods("list", typeParams);
        var popMethods = methods.Where(m => m.Name == "pop").ToList();
        Assert.Equal(2, popMethods.Count);
        Assert.Contains(popMethods, m => m.Parameters.Count == 0);
        Assert.Contains(popMethods, m => m.Parameters.Count == 1);
    }

    [Fact]
    public void List_Append_Takes_T0_Returns_Void()
    {
        var typeParams = MakeTypeParams("T0");
        var methods = BuiltinMethodDefinitions.GetMethods("list", typeParams);
        var append = methods.First(m => m.Name == "append");
        Assert.Single(append.Parameters);
        Assert.IsType<TypeParameterType>(append.Parameters[0].Type);
        Assert.Equal("T0", ((TypeParameterType)append.Parameters[0].Type).Name);
        Assert.IsType<VoidType>(append.ReturnType);
    }

    [Fact]
    public void List_Copy_Returns_List_Of_T0()
    {
        var typeParams = MakeTypeParams("T0");
        var methods = BuiltinMethodDefinitions.GetMethods("list", typeParams);
        var copy = methods.First(m => m.Name == "copy");
        Assert.IsType<GenericType>(copy.ReturnType);
        var gt = (GenericType)copy.ReturnType;
        Assert.Equal(BuiltinNames.List, gt.Name);
    }

    [Fact]
    public void List_OperatorMethods_Has_Expected_Operators()
    {
        var typeParams = MakeTypeParams("T0");
        var ops = BuiltinMethodDefinitions.GetOperatorMethods("list", typeParams);
        Assert.Contains(DunderNames.Add, ops.Keys);
        Assert.Contains(DunderNames.Mul, ops.Keys);
        Assert.Contains(DunderNames.Eq, ops.Keys);
        Assert.Contains(DunderNames.Ne, ops.Keys);
        Assert.Equal(4, ops.Count);
    }

    [Fact]
    public void List_ProtocolMethods_Has_Expected_Protocols()
    {
        var typeParams = MakeTypeParams("T0");
        var protocols = BuiltinMethodDefinitions.GetProtocolMethods("list", typeParams);
        Assert.Contains(DunderNames.Len, protocols.Keys);
        Assert.Contains(DunderNames.Iter, protocols.Keys);
        Assert.Contains(DunderNames.Contains, protocols.Keys);
        Assert.Contains(DunderNames.GetItem, protocols.Keys);
        Assert.Contains(DunderNames.SetItem, protocols.Keys);
    }

    // ---- Set method definitions ----

    [Fact]
    public void Set_Methods_Returns_Expected_Count()
    {
        var typeParams = MakeTypeParams("T0");
        var methods = BuiltinMethodDefinitions.GetMethods("set", typeParams);
        Assert.Equal(11, methods.Count);
    }

    [Fact]
    public void Set_Union_Returns_Set_Of_T0()
    {
        var typeParams = MakeTypeParams("T0");
        var methods = BuiltinMethodDefinitions.GetMethods("set", typeParams);
        var union = methods.First(m => m.Name == "union");
        Assert.IsType<GenericType>(union.ReturnType);
        var gt = (GenericType)union.ReturnType;
        Assert.Equal(BuiltinNames.Set, gt.Name);
    }

    [Fact]
    public void Set_OperatorMethods_Has_Expected_Operators()
    {
        var typeParams = MakeTypeParams("T0");
        var ops = BuiltinMethodDefinitions.GetOperatorMethods("set", typeParams);
        Assert.Contains(DunderNames.Or, ops.Keys);
        Assert.Contains(DunderNames.And, ops.Keys);
        Assert.Contains(DunderNames.Sub, ops.Keys);
        Assert.Contains(DunderNames.Xor, ops.Keys);
        Assert.Contains(DunderNames.Eq, ops.Keys);
        Assert.Contains(DunderNames.Ne, ops.Keys);
        Assert.Equal(6, ops.Count);
    }

    // ---- Tuple definitions ----

    [Fact]
    public void Tuple_Has_No_Methods()
    {
        var typeParams = MakeTypeParams("T0");
        var methods = BuiltinMethodDefinitions.GetMethods("tuple", typeParams);
        Assert.Empty(methods);
    }

    [Fact]
    public void Tuple_OperatorMethods_Has_Expected_Operators()
    {
        var typeParams = MakeTypeParams("T0");
        var ops = BuiltinMethodDefinitions.GetOperatorMethods("tuple", typeParams);
        Assert.Contains(DunderNames.Add, ops.Keys);
        Assert.Contains(DunderNames.Mul, ops.Keys);
        Assert.Contains(DunderNames.Eq, ops.Keys);
        Assert.Contains(DunderNames.Ne, ops.Keys);
    }

    [Fact]
    public void Tuple_ProtocolMethods_Has_Expected_Protocols()
    {
        var typeParams = MakeTypeParams("T0");
        var protocols = BuiltinMethodDefinitions.GetProtocolMethods("tuple", typeParams);
        Assert.Contains(DunderNames.Len, protocols.Keys);
        Assert.Contains(DunderNames.Iter, protocols.Keys);
        Assert.Contains(DunderNames.GetItem, protocols.Keys);
    }

    // ---- View and iterator types ----

    [Fact]
    public void DictItemsView_Has_Iter_And_Len_Protocols()
    {
        var typeParams = new List<TypeParameterDef>();
        var protocols = BuiltinMethodDefinitions.GetProtocolMethods(BuiltinNames.DictItemsView, typeParams);
        Assert.Contains(DunderNames.Iter, protocols.Keys);
        Assert.Contains(DunderNames.Len, protocols.Keys);
        Assert.Equal(2, protocols.Count);
    }

    [Fact]
    public void Iterator_Has_Iter_Protocol()
    {
        var typeParams = new List<TypeParameterDef>();
        var protocols = BuiltinMethodDefinitions.GetProtocolMethods(BuiltinNames.Iterator, typeParams);
        Assert.Contains(DunderNames.Iter, protocols.Keys);
        Assert.Single(protocols);
    }

    [Fact]
    public void IEnumerable_Has_Iter_Protocol()
    {
        var typeParams = new List<TypeParameterDef>();
        var protocols = BuiltinMethodDefinitions.GetProtocolMethods("IEnumerable", typeParams);
        Assert.Contains(DunderNames.Iter, protocols.Keys);
        Assert.Single(protocols);
    }

    // ---- IsCovariant ----

    [Theory]
    [InlineData("list", true)]
    [InlineData("set", true)]
    [InlineData("dict", false)]
    [InlineData("tuple", false)]
    [InlineData("str", false)]
    public void IsCovariant_Returns_Expected(string typeName, bool expected)
    {
        Assert.Equal(expected, BuiltinMethodDefinitions.IsCovariant(typeName));
    }

    // ---- Unknown type returns empty ----

    [Fact]
    public void Unknown_Type_Returns_Empty_Methods()
    {
        var typeParams = MakeTypeParams("T0");
        Assert.Empty(BuiltinMethodDefinitions.GetMethods("unknown_type", typeParams));
        Assert.Empty(BuiltinMethodDefinitions.GetOperatorMethods("unknown_type", typeParams));
        Assert.Empty(BuiltinMethodDefinitions.GetProtocolMethods("unknown_type", typeParams));
    }

    // ---- BuiltinRegistry integration ----

    [Fact]
    public void BuiltinRegistry_Dict_Has_Populated_Methods()
    {
        var registry = new BuiltinRegistry();
        var dictType = registry.GetType("dict");
        Assert.NotNull(dictType);
        Assert.True(dictType!.Methods.Count > 0, "dict TypeSymbol should have methods populated");
        Assert.Contains(dictType.Methods, m => m.Name == "get");
        Assert.Contains(dictType.Methods, m => m.Name == "items");
        Assert.Contains(dictType.Methods, m => m.Name == "keys");
        Assert.Contains(dictType.Methods, m => m.Name == "values");
    }

    [Fact]
    public void BuiltinRegistry_Dict_Has_Populated_OperatorMethods()
    {
        var registry = new BuiltinRegistry();
        var dictType = registry.GetType("dict");
        Assert.NotNull(dictType);
        Assert.Contains(DunderNames.Or, dictType!.OperatorMethods.Keys);
        Assert.Contains(DunderNames.Eq, dictType.OperatorMethods.Keys);
    }

    [Fact]
    public void BuiltinRegistry_Dict_Has_Populated_ProtocolMethods()
    {
        var registry = new BuiltinRegistry();
        var dictType = registry.GetType("dict");
        Assert.NotNull(dictType);
        Assert.Contains(DunderNames.Len, dictType!.ProtocolMethods.Keys);
        Assert.Contains(DunderNames.Iter, dictType.ProtocolMethods.Keys);
    }

    [Fact]
    public void BuiltinRegistry_Dict_Has_MethodOverloads_For_Get()
    {
        var registry = new BuiltinRegistry();
        var dictType = registry.GetType("dict");
        Assert.NotNull(dictType);
        Assert.True(dictType!.MethodOverloads.ContainsKey("get"), "dict should have method overloads for 'get'");
        Assert.Equal(2, dictType.MethodOverloads["get"].Count);
    }

    [Fact]
    public void BuiltinRegistry_List_Has_Populated_Methods()
    {
        var registry = new BuiltinRegistry();
        var listType = registry.GetType("list");
        Assert.NotNull(listType);
        Assert.True(listType!.Methods.Count > 0);
        Assert.Contains(listType.Methods, m => m.Name == "append");
        Assert.Contains(listType.Methods, m => m.Name == "pop");
    }

    [Fact]
    public void BuiltinRegistry_Set_Has_Populated_Methods()
    {
        var registry = new BuiltinRegistry();
        var setType = registry.GetType("set");
        Assert.NotNull(setType);
        Assert.True(setType!.Methods.Count > 0);
        Assert.Contains(setType.Methods, m => m.Name == "add");
        Assert.Contains(setType.Methods, m => m.Name == "union");
    }

    // ---- PopulateMethodOverloads ----

    [Fact]
    public void PopulateMethodOverloads_Groups_Same_Name_Methods()
    {
        var typeParams = MakeTypeParams("T0");
        var typeSymbol = new TypeSymbol
        {
            Name = "test",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Methods = BuiltinMethodDefinitions.GetMethods("list", typeParams),
        };

        BuiltinMethodDefinitions.PopulateMethodOverloads(typeSymbol);

        // list has pop() and pop(index) overloads
        Assert.True(typeSymbol.MethodOverloads.ContainsKey("pop"));
        Assert.Equal(2, typeSymbol.MethodOverloads["pop"].Count);
    }
}
