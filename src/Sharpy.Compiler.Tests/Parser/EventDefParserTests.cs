using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Parser tests for EventDef: auto-events, function-style events, decorators, and error cases.
/// </summary>
public class EventDefParserTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = new List<LexerNs.Token>();
        while (true)
        {
            var token = lexer.NextToken();
            tokens.Add(token);
            if (token.Type == LexerNs.TokenType.Eof)
                break;
        }
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    private static string ParseExpectingError(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        parser.ParseModule();

        var allErrors = lexer.Diagnostics.GetErrors()
            .Concat(parser.Diagnostics.GetErrors())
            .ToList();

        allErrors.Should().NotBeEmpty("Expected parser to report an error for input: " + source);
        return string.Join("\n", allErrors.Select(d => d.Message));
    }

    private static EventDef GetFirstEventFromClass(string source)
    {
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        return classDef.Body.OfType<EventDef>().First();
    }

    #region Auto-Event Parsing

    [Fact]
    public void ParseAutoEvent_SimpleType()
    {
        var source = @"
class Button:
    event on_click: EventHandler
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_click");
        eventDef.IsFunctionStyle.Should().BeFalse();
        eventDef.Accessor.Should().Be(EventAccessor.None);
        eventDef.Type.Should().NotBeNull();
        eventDef.Type!.Name.Should().Be("EventHandler");
        eventDef.Parameters.Should().BeEmpty();
        eventDef.Body.Should().BeEmpty();
        eventDef.Decorators.Should().BeEmpty();
    }

    [Fact]
    public void ParseAutoEvent_GenericType()
    {
        var source = @"
class Button:
    event on_change: EventHandler[ChangeEventArgs]
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_change");
        eventDef.IsFunctionStyle.Should().BeFalse();
        eventDef.Accessor.Should().Be(EventAccessor.None);
        eventDef.Type.Should().NotBeNull();
        eventDef.Type!.Name.Should().Be("EventHandler");
        eventDef.Type.TypeArguments.Should().HaveCount(1);
        eventDef.Type.TypeArguments[0].Name.Should().Be("ChangeEventArgs");
    }

    [Fact]
    public void ParseAutoEvent_CustomDelegateType()
    {
        var source = @"
class DataStream:
    event on_data_received: DataReceivedHandler
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_data_received");
        eventDef.IsFunctionStyle.Should().BeFalse();
        eventDef.Type.Should().NotBeNull();
        eventDef.Type!.Name.Should().Be("DataReceivedHandler");
        eventDef.Type.TypeArguments.Should().BeEmpty();
    }

    [Fact]
    public void ParseAutoEvent_MultipleEventsInClass()
    {
        var source = @"
class Application:
    event on_startup: EventHandler
    event on_shutdown: EventHandler
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        var events = classDef.Body.OfType<EventDef>().ToList();
        events.Should().HaveCount(2);
        events[0].Name.Should().Be("on_startup");
        events[1].Name.Should().Be("on_shutdown");
    }

    #endregion

    #region Function-Style Event Parsing

    [Fact]
    public void ParseFunctionStyleEvent_Add()
    {
        var source = @"
class Button:
    event add on_click(self, handler: EventHandler):
        self._handlers.append(handler)
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_click");
        eventDef.IsFunctionStyle.Should().BeTrue();
        eventDef.Accessor.Should().Be(EventAccessor.Add);
        eventDef.Type.Should().BeNull();
        eventDef.Parameters.Should().HaveCount(2);
        eventDef.Parameters[0].Name.Should().Be("self");
        eventDef.Parameters[1].Name.Should().Be("handler");
        eventDef.Parameters[1].Type.Should().NotBeNull();
        eventDef.Parameters[1].Type!.Name.Should().Be("EventHandler");
        eventDef.Body.Should().NotBeEmpty();
    }

    [Fact]
    public void ParseFunctionStyleEvent_Remove()
    {
        var source = @"
class Button:
    event remove on_click(self, handler: EventHandler):
        self._handlers.remove(handler)
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_click");
        eventDef.IsFunctionStyle.Should().BeTrue();
        eventDef.Accessor.Should().Be(EventAccessor.Remove);
        eventDef.Parameters.Should().HaveCount(2);
        eventDef.Parameters[0].Name.Should().Be("self");
        eventDef.Parameters[1].Name.Should().Be("handler");
        eventDef.Body.Should().NotBeEmpty();
    }

    [Fact]
    public void ParseFunctionStyleEvent_AddAndRemovePair()
    {
        var source = @"
class SecureButton:
    event add on_click(self, handler: EventHandler):
        self._handlers.append(handler)

    event remove on_click(self, handler: EventHandler):
        self._handlers.remove(handler)
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        var events = classDef.Body.OfType<EventDef>().ToList();
        events.Should().HaveCount(2);
        events[0].Accessor.Should().Be(EventAccessor.Add);
        events[0].Name.Should().Be("on_click");
        events[1].Accessor.Should().Be(EventAccessor.Remove);
        events[1].Name.Should().Be("on_click");
    }

    [Fact]
    public void ParseFunctionStyleEvent_EllipsisBody()
    {
        var source = @"
class Button:
    event add on_click(self, handler: EventHandler): ...
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_click");
        eventDef.IsFunctionStyle.Should().BeTrue();
        eventDef.Accessor.Should().Be(EventAccessor.Add);
        eventDef.Body.Should().HaveCount(1);
        eventDef.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<EllipsisLiteral>();
    }

    [Fact]
    public void ParseFunctionStyleEvent_GenericHandlerType()
    {
        var source = @"
class Control:
    event add on_paint(self, handler: EventHandler[PaintEventArgs]):
        self._paint_handlers.append(handler)
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_paint");
        eventDef.IsFunctionStyle.Should().BeTrue();
        eventDef.Accessor.Should().Be(EventAccessor.Add);
        eventDef.Parameters[1].Type!.Name.Should().Be("EventHandler");
        eventDef.Parameters[1].Type.TypeArguments.Should().HaveCount(1);
        eventDef.Parameters[1].Type.TypeArguments[0].Name.Should().Be("PaintEventArgs");
    }

    #endregion

    #region Decorated Events

    [Fact]
    public void ParseDecoratedEvent_Virtual()
    {
        var source = @"
class BaseControl:
    @virtual
    event on_paint: EventHandler[PaintEventArgs]
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_paint");
        eventDef.IsFunctionStyle.Should().BeFalse();
        eventDef.Decorators.Should().HaveCount(1);
        eventDef.Decorators[0].Name.Should().Be("virtual");
        eventDef.Type.Should().NotBeNull();
        eventDef.Type!.Name.Should().Be("EventHandler");
        eventDef.Type.TypeArguments.Should().HaveCount(1);
        eventDef.Type.TypeArguments[0].Name.Should().Be("PaintEventArgs");
    }

    [Fact]
    public void ParseDecoratedEvent_Abstract()
    {
        var source = @"
class BasePublisher:
    @abstract
    event on_update: EventHandler[UpdateEventArgs]
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_update");
        eventDef.Decorators.Should().HaveCount(1);
        eventDef.Decorators[0].Name.Should().Be("abstract");
    }

    [Fact]
    public void ParseDecoratedEvent_Static()
    {
        var source = @"
class Application:
    @static
    event on_startup: EventHandler
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_startup");
        eventDef.Decorators.Should().HaveCount(1);
        eventDef.Decorators[0].Name.Should().Be("static");
    }

    [Fact]
    public void ParseDecoratedEvent_Override_FunctionStyle()
    {
        var source = @"
class CustomControl:
    @override
    event add on_paint(self, handler: EventHandler[PaintEventArgs]):
        self._handlers.append(handler)
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_paint");
        eventDef.IsFunctionStyle.Should().BeTrue();
        eventDef.Accessor.Should().Be(EventAccessor.Add);
        eventDef.Decorators.Should().HaveCount(1);
        eventDef.Decorators[0].Name.Should().Be("override");
    }

    [Fact]
    public void ParseDecoratedEvent_Public()
    {
        var source = @"
class Publisher:
    @public
    event on_change: EventHandler
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Name.Should().Be("on_change");
        eventDef.Decorators.Should().HaveCount(1);
        eventDef.Decorators[0].Name.Should().Be("public");
    }

    #endregion

    #region Events Mixed with Other Class Members

    [Fact]
    public void ParseEventWithMethodsInClass()
    {
        var source = @"
class Button:
    event on_click: EventHandler

    def click(self):
        pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Body.Should().HaveCount(2);
        classDef.Body[0].Should().BeOfType<EventDef>().Which.Name.Should().Be("on_click");
        classDef.Body[1].Should().BeOfType<FunctionDef>().Which.Name.Should().Be("click");
    }

    [Fact]
    public void ParseEventWithFieldsAndMethods()
    {
        var source = @"
class Timer:
    _count: int = 0
    event on_tick: EventHandler

    def tick(self):
        pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Body.Should().HaveCount(3);
        classDef.Body[0].Should().BeOfType<VariableDeclaration>();
        classDef.Body[1].Should().BeOfType<EventDef>().Which.Name.Should().Be("on_tick");
        classDef.Body[2].Should().BeOfType<FunctionDef>();
    }

    #endregion

    #region Interface Events

    [Fact]
    public void ParseInterfaceAutoEvent()
    {
        var source = @"
interface INotifyPropertyChanged:
    event property_changed: EventHandler[PropertyChangedEventArgs]
";
        var module = Parse(source);
        var interfaceDef = module.Body[0].Should().BeOfType<InterfaceDef>().Subject;
        var eventDef = interfaceDef.Body.OfType<EventDef>().First();
        eventDef.Name.Should().Be("property_changed");
        eventDef.IsFunctionStyle.Should().BeFalse();
        eventDef.Type!.Name.Should().Be("EventHandler");
    }

    #endregion

    #region Parser Error Cases

    [Fact]
    public void RejectsFunctionStyleEvent_WithoutAccessor()
    {
        var source = @"
class Button:
    event on_click(self, handler: EventHandler):
        pass
";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Function-style event requires 'add' or 'remove' accessor keyword");
    }

    [Fact]
    public void RejectsAutoEvent_WithAccessorKeyword()
    {
        var source = @"
class Button:
    event add on_click: EventHandler
";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("Auto-event must not have an accessor keyword");
    }

    #endregion

    #region AST Structure Verification

    [Fact]
    public void AutoEvent_HasCorrectSpanInfo()
    {
        var source = @"
class Button:
    event on_click: EventHandler
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.LineStart.Should().BeGreaterThan(0);
        eventDef.ColumnStart.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FunctionStyleEvent_HasCorrectSpanInfo()
    {
        var source = @"
class Button:
    event add on_click(self, handler: EventHandler):
        pass
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.LineStart.Should().BeGreaterThan(0);
        eventDef.ColumnStart.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AutoEvent_TypeAnnotationHasTypeArguments()
    {
        var source = @"
class Button:
    event on_hover: EventHandler[MouseEventArgs]
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Type.Should().NotBeNull();
        eventDef.Type!.TypeArguments.Should().HaveCount(1);
        eventDef.Type.TypeArguments[0].Should().NotBeNull();
        eventDef.Type.TypeArguments[0].Name.Should().Be("MouseEventArgs");
    }

    [Fact]
    public void FunctionStyleEvent_BodyContainsStatements()
    {
        var source = @"
class Button:
    event add on_click(self, handler: EventHandler):
        self._handlers.append(handler)
        print(handler)
";
        var eventDef = GetFirstEventFromClass(source);
        eventDef.Body.Should().HaveCount(2);
    }

    #endregion
}
