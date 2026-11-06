using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Differential;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// P77a D1: the Sharpy-AST → python translator. A Sharpy source is parsed with the compiler's own lexer and parser
/// (no semantic pass) and walked through ONE per-construct table, so a seed <c>.spy</c> and a generated cell share
/// one path and the seed's hand twins (D7) guard the table every generated twin uses. A node with no row THROWS: a
/// silently dropped construct makes the python arm "run" trivially (the plan's Adversarial Review (2)).
///
/// Rows beyond D1's list, each because a seed or the parser needs it: an uninitialized declaration <c>x: T</c> is
/// <c>x: object</c> (python's own "declares, binds nothing": inside a function it still makes the name local, which
/// dropping the line would not); <c>@static</c>/<c>const</c>/<c>property</c> inside a dataclass are python class
/// attributes, unannotated, because they are not constructor parameters; <c>@lru_cache</c> adds its import (C50);
/// a docstring is printed; a callable held in a type body is <c>staticmethod(…)</c> and a dataclass field with an
/// unhashable default is an unannotated class attribute, because python raises on the plain spelling of each.
/// </summary>
internal static class TwinRenderer
{
    // Mutation hooks (P77a D8): each is one row of D1's table, edited in place by the mutator. `static readonly`
    // rather than `const` because a constant condition makes the other arm CS0162, which is an error here.
    /// <summary>M2: <c>@static def</c> → <c>@staticmethod</c>; false drops the decorator line.</summary>
    private static readonly bool RowStaticDefToStaticmethod = true;

    /// <summary>M2b: <c>main()</c> appended when the module has a <c>def main</c>.</summary>
    private static readonly bool RowAppendMainCall = true;

    /// <summary>M6: every annotation stripped; false passes each through verbatim from the source.</summary>
    private static readonly bool RowStripAnnotations = true;

    /// <summary>The placeholder call carrying a <c>global</c>/<c>nonlocal</c> line through the parser.</summary>
    private const string ScopeStatementMarker = "__twin_scope_statement__";

    /// <summary>
    /// The one line-level row. The parser refuses <c>global</c>/<c>nonlocal</c> with SPY0134 and yields no node
    /// (<c>Parser.cs</c>'s statement dispatch; seed A20), so for this row alone the line is rewritten to a placeholder
    /// call before parsing and printed back verbatim from the AST. A keyword at the start of a statement line cannot
    /// mean anything else; the placeholder is a name no source declares.
    /// </summary>
    private static readonly Regex ScopeStatementLine = new(
        @"^(?<indent>[ \t]*)(?<statement>(?:global|nonlocal)[ \t]+[^#\r\n]*?)[ \t]*(?:#[^\r\n]*)?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>The python twin of <paramref name="spySource"/>. A parser refusal or a construct with no row throws.</summary>
    public static string Render(string spySource)
    {
        var source = ScopeStatementLine.Replace(
            spySource, m => $"{m.Groups["indent"].Value}{ScopeStatementMarker}(\"{m.Groups["statement"].Value}\")");
        var lexer = new SharpyLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new SharpyParser(tokens);
        var module = parser.ParseModule();
        var errors = lexer.Diagnostics.GetErrors().Concat(parser.Diagnostics.GetErrors()).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "TwinRenderer: the parser refused the source: "
                + string.Join(", ", errors.Select(e => $"{e.Code} (L{e.Line}): {e.Message}")));
        }

        return new Walker(source).RenderModule(module);
    }

    private enum BodyKind
    {
        Module,
        Class,
        Dataclass,
        Function,
    }

    private sealed class Walker
    {
        private readonly string _source;
        private readonly StringBuilder _body = new();
        private bool _usesDataclass;
        private bool _usesEnum;
        private bool _usesLruCache;
        private bool _usesSome;

        public Walker(string source) => _source = source;

        public string RenderModule(Module module)
        {
            WriteDocString(module.DocString, 0);
            foreach (var statement in module.Body)
                WriteStatement(statement, 0, BodyKind.Module);

            var output = new StringBuilder();
            if (_usesDataclass)
                output.Append("from dataclasses import dataclass\n");
            if (_usesEnum)
                output.Append("from enum import Enum\n");
            if (_usesLruCache)
                output.Append("from functools import lru_cache\n");
            if (_usesSome && !module.Body.OfType<ClassDef>().Any(c => c.Name == "Some"))
            {
                output.Append("class Some:\n");
                output.Append("    __match_args__ = (\"value\",)\n");
                output.Append("    def __init__(self, value):\n");
                output.Append("        self.value = value\n");
            }

            output.Append(_body);
            if (RowAppendMainCall && module.Body.OfType<FunctionDef>().Any(f => f.Name == "main"))
                output.Append("main()\n");
            return output.ToString();
        }

        // ---- statements ----------------------------------------------------------------------------------------

        private void WriteStatement(Statement statement, int indent, BodyKind body)
        {
            switch (statement)
            {
                case ExpressionStatement { Expression: FunctionCall { Function: Identifier { Name: ScopeStatementMarker } } scope }:
                    Line(indent, ((StringLiteral)scope.Arguments[0]).Value);
                    break;
                case ExpressionStatement { Expression: FunctionCall call } when call.Arguments.Any(a => a is ModifiedArgument):
                    WriteOutCall(call, indent);
                    break;
                case ExpressionStatement expression:
                    Line(indent, Expr(expression.Expression));
                    break;
                case VariableDeclaration declaration:
                    WriteVariableDeclaration(declaration, indent, body);
                    break;
                case Assignment assignment:
                    Line(indent, $"{Expr(assignment.Target)} {AssignmentOp(assignment.Operator)} {Expr(assignment.Value)}");
                    break;
                case AssertStatement assertion:
                    Line(indent, assertion.Message == null
                        ? $"assert {Expr(assertion.Test)}"
                        : $"assert {Expr(assertion.Test)}, {Expr(assertion.Message)}");
                    break;
                case PassStatement:
                    Line(indent, "pass");
                    break;
                case BreakStatement:
                    Line(indent, "break");
                    break;
                case ContinueStatement:
                    Line(indent, "continue");
                    break;
                case ReturnStatement ret:
                    Line(indent, ret.Value == null ? "return" : $"return {Expr(ret.Value)}");
                    break;
                case YieldStatement yield:
                    Line(indent, $"{(yield.IsFrom ? "yield from" : "yield")} {Expr(yield.Value)}");
                    break;
                case RaiseStatement raise:
                    Line(indent, raise switch
                    {
                        { Exception: null } => "raise",
                        { Cause: null } => $"raise {Expr(raise.Exception)}",
                        _ => $"raise {Expr(raise.Exception)} from {Expr(raise.Cause)}",
                    });
                    break;
                case IfStatement branch:
                    WriteIf(branch, indent, body);
                    break;
                case WhileStatement loop:
                    Line(indent, $"while {Expr(loop.Test)}:");
                    WriteBlock(loop.Body, indent + 1, body);
                    WriteElse(loop.ElseBody, indent, body);
                    break;
                case ForStatement loop:
                    Line(indent, $"{(loop.IsAsync ? "async for" : "for")} {Target(loop.Target)} in {Expr(loop.Iterator)}:");
                    WriteBlock(loop.Body, indent + 1, body);
                    WriteElse(loop.ElseBody, indent, body);
                    break;
                case TryStatement attempt:
                    WriteTry(attempt, indent, body);
                    break;
                case WithStatement with:
                    Line(indent, $"{(with.IsAsync ? "async with" : "with")} {string.Join(", ", with.Items.Select(WithItemText))}:");
                    WriteBlock(with.Body, indent + 1, body);
                    break;
                case MatchStatement match:
                    WriteMatch(match, indent, body);
                    break;
                case FunctionDef function:
                    WriteFunction(function, indent, body);
                    break;
                case ClassDef classDef:
                    WriteClass(classDef, indent);
                    break;
                case StructDef structDef:
                    WriteStruct(structDef, indent);
                    break;
                case InterfaceDef interfaceDef:
                    WriteInterface(interfaceDef, indent);
                    break;
                case EnumDef enumDef:
                    WriteEnum(enumDef, indent);
                    break;
                case UnionDef unionDef:
                    WriteUnion(unionDef, indent);
                    break;
                case PropertyDef property:
                    WriteProperty(property, indent, body);
                    break;
                case TypeAlias alias:
                    WriteTypeAlias(alias, indent);
                    break;
                case ImportStatement import:
                    Line(indent, $"import {string.Join(", ", import.Names.Select(ImportAliasText))}");
                    break;
                case FromImportStatement from:
                    Line(indent, from.ImportAll
                        ? $"from {from.Module} import *"
                        : $"from {from.Module} import {string.Join(", ", from.Names.Select(ImportAliasText))}");
                    break;
                default:
                    throw NoRow(statement);
            }
        }

        /// <summary><c>const</c>/<c>let</c> stripped, <c>@static</c> dropped, the annotation stripped except a dataclass field's.</summary>
        private void WriteVariableDeclaration(VariableDeclaration declaration, int indent, BodyKind body)
        {
            var isStatic = HasStaticOnly(declaration.Decorators, declaration);
            string annotation;
            if (body == BodyKind.Dataclass && !isStatic && !declaration.IsConst && !IsUnhashableDefault(declaration))
                annotation = ": " + DataclassFieldType(declaration.Type);
            else if (!RowStripAnnotations && declaration.Type != null)
                annotation = ": " + Slice(declaration.Type);
            else if (declaration.InitialValue == null)
                annotation = ": object"; // `x: T` declares and binds nothing; `x: object` is python's same statement
            else
                annotation = "";

            Line(indent, declaration.InitialValue == null
                ? $"{declaration.Name}{annotation}"
                : $"{declaration.Name}{annotation} = {FieldValue(declaration.Type, declaration.InitialValue, body)}");
        }

        /// <summary>
        /// A callable held in a type body must not bind <c>self</c>: python's <c>H().r()</c> over a bare
        /// <c>r = lambda: e</c> is a TypeError (0 positional, 1 given), so the field is python's non-binding callable.
        /// </summary>
        private string FieldValue(TypeAnnotation? type, Expression value, BodyKind body) =>
            body is BodyKind.Class or BodyKind.Dataclass && (value is LambdaExpression || type?.Name == "function")
                ? $"staticmethod({Expr(value)})"
                : Expr(value);

        /// <summary>
        /// Python's dataclass refuses an unhashable default at class creation (<c>ValueError: mutable default</c>),
        /// so such a field is an unannotated class attribute: same eager evaluation, same class-body scope.
        /// </summary>
        private static bool IsUnhashableDefault(VariableDeclaration declaration) =>
            declaration.InitialValue is ListLiteral or SetLiteral or DictLiteral
                or ListComprehension or SetComprehension or DictComprehension
            || declaration is { InitialValue: not null, Type.Name: "list" or "dict" or "set" };

        /// <summary>D1's dataclass field mapping: the four python-spelled scalars verbatim, anything else <c>object</c>.</summary>
        private static string DataclassFieldType(TypeAnnotation? type)
        {
            if (type is { IsOptional: false, IsCSharpNullable: false, IsResult: false, TypeArguments.IsEmpty: true }
                && type.Name is "int" or "float" or "str" or "bool")
            {
                return type!.Name;
            }

            return "object";
        }

        private void WriteIf(IfStatement branch, int indent, BodyKind body)
        {
            Line(indent, $"if {Expr(branch.Test)}:");
            WriteBlock(branch.ThenBody, indent + 1, body);
            foreach (var elif in branch.ElifClauses)
            {
                Line(indent, $"elif {Expr(elif.Test)}:");
                WriteBlock(elif.Body, indent + 1, body);
            }

            WriteElse(branch.ElseBody, indent, body);
        }

        private void WriteElse(ImmutableArray<Statement> elseBody, int indent, BodyKind body)
        {
            if (elseBody.IsEmpty)
                return;
            Line(indent, "else:");
            WriteBlock(elseBody, indent + 1, body);
        }

        private void WriteTry(TryStatement attempt, int indent, BodyKind body)
        {
            Line(indent, "try:");
            WriteBlock(attempt.Body, indent + 1, body);
            foreach (var handler in attempt.Handlers)
            {
                if (handler.Filter != null)
                    throw new NotSupportedException($"TwinRenderer: no table row for an except filter (L{handler.LineStart}).");
                var keyword = handler.IsExceptStar ? "except*" : "except";
                var type = handler.ExceptionType == null ? "" : " " + TypeName(handler.ExceptionType);
                var name = handler.Name == null ? "" : " as " + handler.Name;
                Line(indent, $"{keyword}{type}{name}:");
                WriteBlock(handler.Body, indent + 1, body);
            }

            WriteElse(attempt.ElseBody, indent, body);
            if (!attempt.FinallyBody.IsEmpty)
            {
                Line(indent, "finally:");
                WriteBlock(attempt.FinallyBody, indent + 1, body);
            }
        }

        private string WithItemText(WithItem item) =>
            item.Target == null ? Expr(item.ContextExpression) : $"{Expr(item.ContextExpression)} as {Target(item.Target)}";

        private void WriteMatch(MatchStatement match, int indent, BodyKind body)
        {
            Line(indent, $"match {Expr(match.Scrutinee)}:");
            foreach (var matchCase in match.Cases)
            {
                // A top-level parenthesized guard is python's case guard; python has no nested one (PatternText throws).
                var (pattern, guard) = matchCase is { Pattern: GuardPattern top, Guard: null }
                    ? (top.Inner, top.Guard)
                    : (matchCase.Pattern, matchCase.Guard);
                Line(indent + 1, guard == null
                    ? $"case {PatternText(pattern)}:"
                    : $"case {PatternText(pattern)} if {Expr(guard)}:");
                WriteBlock(matchCase.Body, indent + 2, body);
            }
        }

        private void WriteFunction(FunctionDef function, int indent, BodyKind body)
        {
            foreach (var decorator in function.Decorators)
            {
                if (decorator.Name == "static")
                {
                    if (RowStaticDefToStaticmethod)
                        Line(indent, "@staticmethod");
                }
                else if (decorator.Name == "lru_cache")
                {
                    _usesLruCache = true;
                    Line(indent, DecoratorText(decorator));
                }
                else
                {
                    throw NoDecoratorRow(decorator, function.Name);
                }
            }

            var outParameters = function.Parameters.Where(p => p.Modifier == ParameterModifier.Out).ToList();
            var parameters = function.Parameters
                .Where(p => p.Modifier != ParameterModifier.Out)
                .Select(ParameterText)
                .ToList();

            // context_managers.md: Sharpy's __exit__ takes no arguments, python passes three.
            if (function.Name == "__exit__" && parameters.Count == 1 && body is not BodyKind.Module and not BodyKind.Function)
                parameters.AddRange(new[] { "exc_type", "exc", "tb" });

            var returnAnnotation = !RowStripAnnotations && function.ReturnType != null ? " -> " + Slice(function.ReturnType) : "";
            var keyword = function.IsAsync ? "async def" : "def";
            Line(indent, $"{keyword} {function.Name}{TypeParameters(function.TypeParameters)}({string.Join(", ", parameters)}){returnAnnotation}:");
            WriteDocString(function.DocString, indent + 1);
            if (outParameters.Count == 0)
                WriteBlock(function.Body, indent + 1, BodyKind.Function);
            else
                WriteOutBody(function, outParameters, indent + 1);
        }

        /// <summary>The <c>out</c> pair, callee half: python's idiom is a return, so the final <c>v = E</c> is <c>return E</c>.</summary>
        private void WriteOutBody(FunctionDef function, List<Parameter> outParameters, int indent)
        {
            var last = function.Body.IsEmpty ? null : function.Body[^1];
            if (outParameters.Count != 1
                || last is not Assignment { Operator: AssignmentOperator.Assign, Target: Identifier target } assignment
                || target.Name != outParameters[0].Name)
            {
                throw new NotSupportedException(
                    $"TwinRenderer: the out row needs one out parameter whose final statement assigns it ('{function.Name}', L{function.LineStart}).");
            }

            foreach (var statement in function.Body.RemoveAt(function.Body.Length - 1))
                WriteStatement(statement, indent, BodyKind.Function);
            Line(indent, $"return {Expr(assignment.Value)}");
        }

        /// <summary>The <c>out</c> pair, call half: <c>f(out let v)</c> → <c>v = f()</c>, in statement position only.</summary>
        private void WriteOutCall(FunctionCall call, int indent)
        {
            var modified = call.Arguments.OfType<ModifiedArgument>().ToList();
            if (modified.Count != 1 || modified[0].Modifier != ParameterModifier.Out)
                throw NoRow(modified[0]);
            var name = modified[0].InlineName ?? Expr(modified[0].Argument);
            var rest = call.Arguments.Where(a => a is not ModifiedArgument).Select(Expr)
                .Concat(call.KeywordArguments.Select(KeywordText));
            Line(indent, $"{name} = {Expr(call.Function)}({string.Join(", ", rest)})");
        }

        private void WriteClass(ClassDef classDef, int indent)
        {
            var isDataclass = false;
            foreach (var decorator in classDef.Decorators)
            {
                if (decorator.Name != "dataclass")
                    throw NoDecoratorRow(decorator, classDef.Name);
                isDataclass = true;
                _usesDataclass = true;
                Line(indent, DecoratorText(decorator));
            }

            Line(indent, $"class {classDef.Name}{TypeParameters(classDef.TypeParameters)}{Bases(classDef.BaseClasses)}:");
            WriteDocString(classDef.DocString, indent + 1);
            WriteBlock(classDef.Body, indent + 1, isDataclass ? BodyKind.Dataclass : BodyKind.Class);
        }

        /// <summary>A struct's fields are constructor parameters with defaults (L13): python's twin that constructs the same way is a dataclass.</summary>
        private void WriteStruct(StructDef structDef, int indent)
        {
            if (!structDef.Decorators.IsEmpty)
                throw NoDecoratorRow(structDef.Decorators[0], structDef.Name);
            _usesDataclass = true;
            Line(indent, "@dataclass");
            Line(indent, $"class {structDef.Name}{TypeParameters(structDef.TypeParameters)}{Bases(structDef.BaseClasses)}:");
            WriteDocString(structDef.DocString, indent + 1);
            WriteBlock(structDef.Body, indent + 1, BodyKind.Dataclass);
        }

        private void WriteInterface(InterfaceDef interfaceDef, int indent)
        {
            if (!interfaceDef.Decorators.IsEmpty)
                throw NoDecoratorRow(interfaceDef.Decorators[0], interfaceDef.Name);
            Line(indent, $"class {interfaceDef.Name}{TypeParameters(interfaceDef.TypeParameters)}{Bases(interfaceDef.BaseInterfaces)}:");
            WriteDocString(interfaceDef.DocString, indent + 1);
            WriteBlock(interfaceDef.Body, indent + 1, BodyKind.Class);
        }

        private void WriteEnum(EnumDef enumDef, int indent)
        {
            if (!enumDef.Decorators.IsEmpty)
                throw NoDecoratorRow(enumDef.Decorators[0], enumDef.Name);
            _usesEnum = true;
            Line(indent, $"class {enumDef.Name}(Enum):");
            WriteDocString(enumDef.DocString, indent + 1);
            foreach (var member in enumDef.Members)
            {
                if (member.Value == null)
                    throw new NotSupportedException($"TwinRenderer: no table row for the valueless enum case '{member.Name}' (L{member.LineStart}).");
                Line(indent + 1, $"{member.Name} = {Expr(member.Value)}");
            }
        }

        /// <summary><c>class U: pass</c> + one <c>class V(U)</c> per case, then <c>U.V = V</c> so <c>U.V(3)</c> and <c>case V(v):</c> both work.</summary>
        private void WriteUnion(UnionDef unionDef, int indent)
        {
            if (!unionDef.Decorators.IsEmpty)
                throw NoDecoratorRow(unionDef.Decorators[0], unionDef.Name);
            if (!unionDef.TypeParameters.IsEmpty)
                throw new NotSupportedException($"TwinRenderer: no table row for the generic union '{unionDef.Name}' (L{unionDef.LineStart}).");

            Line(indent, $"class {unionDef.Name}:");
            WriteDocString(unionDef.DocString, indent + 1);
            WriteBlock(unionDef.Body, indent + 1, BodyKind.Class);
            foreach (var unionCase in unionDef.Cases)
            {
                Line(indent, $"class {unionCase.Name}({unionDef.Name}):");
                if (unionCase.Fields.IsEmpty)
                {
                    Line(indent + 1, "pass");
                    continue;
                }

                var names = unionCase.Fields.Select(f => f.Name).ToList();
                var matchArgs = names.Count == 1
                    ? $"(\"{names[0]}\",)"
                    : $"({string.Join(", ", names.Select(n => $"\"{n}\""))})";
                Line(indent + 1, $"__match_args__ = {matchArgs}");
                Line(indent + 1, $"def __init__(self, {string.Join(", ", names)}):");
                foreach (var name in names)
                    Line(indent + 2, $"self.{name} = {name}");
            }

            foreach (var unionCase in unionDef.Cases)
                Line(indent, $"{unionDef.Name}.{unionCase.Name} = {unionCase.Name}");
        }

        /// <summary>
        /// <c>property NAME: T = v</c> → <c>NAME = v</c>; <c>property get/set NAME(self, …):</c> → <c>@property</c> /
        /// <c>@NAME.setter</c> (<c>properties_function_style.md</c>). A function-style property with no <c>self</c>
        /// (module level, <c>@static</c>) has no python twin that reads the same: python's <c>@property</c> needs an
        /// instance, so it throws rather than print a property object.
        /// </summary>
        private void WriteProperty(PropertyDef property, int indent, BodyKind body)
        {
            var isStatic = HasStaticOnly(property.Decorators, property);
            if (!property.Observers.IsEmpty || property.ExplicitInterface != null)
                throw new NotSupportedException($"TwinRenderer: no table row for the observed or explicit-interface property '{property.Name}' (L{property.LineStart}).");

            if (!property.IsFunctionStyle)
            {
                var annotation = !RowStripAnnotations && property.Type != null ? ": " + Slice(property.Type) : "";
                if (property.DefaultValue != null)
                    Line(indent, $"{property.Name}{annotation} = {FieldValue(property.Type, property.DefaultValue, body)}");
                else
                    Line(indent, $"{property.Name}{(annotation.Length > 0 ? annotation : ": object")}");
                return;
            }

            if (isStatic || body is BodyKind.Module or BodyKind.Function || property.Parameters.IsEmpty || property.Parameters[0].Name != "self")
                throw new NotSupportedException($"TwinRenderer: no table row for the selfless function-style property '{property.Name}' (L{property.LineStart}).");

            Line(indent, property.Accessor switch
            {
                PropertyAccessor.Get => "@property",
                PropertyAccessor.Set => $"@{property.Name}.setter",
                _ => throw new NotSupportedException($"TwinRenderer: no table row for the '{property.Accessor}' accessor of '{property.Name}' (L{property.LineStart})."),
            });
            var returnAnnotation = !RowStripAnnotations && property.ReturnType != null ? " -> " + Slice(property.ReturnType) : "";
            Line(indent, $"def {property.Name}({string.Join(", ", property.Parameters.Select(ParameterText))}){returnAnnotation}:");
            WriteBlock(property.Body, indent + 1, BodyKind.Function);
        }

        private void WriteTypeAlias(TypeAlias alias, int indent)
        {
            if (alias.Type == null || !alias.TypeParameters.IsEmpty)
                throw new NotSupportedException($"TwinRenderer: no table row for the function-typed or generic alias '{alias.Name}' (L{alias.LineStart}).");
            Line(indent, $"{alias.Name} = {TypeName(alias.Type)}");
        }

        private void WriteBlock(ImmutableArray<Statement> statements, int indent, BodyKind body)
        {
            if (statements.IsEmpty)
            {
                Line(indent, "pass");
                return;
            }

            foreach (var statement in statements)
                WriteStatement(statement, indent, body);
        }

        private void WriteDocString(string? docString, int indent)
        {
            if (docString != null)
                Line(indent, PythonString(docString));
        }

        private void Line(int indent, string text) => _body.Append(' ', indent * 4).Append(text).Append('\n');

        // ---- expressions ---------------------------------------------------------------------------------------

        private string Expr(Expression? expression) => expression switch
        {
            IntegerLiteral literal => literal.Value, // a Sharpy width suffix has no python spelling and no name verdict
            FloatLiteral literal => literal.Value,
            StringLiteral or BytesLiteralExpression or FStringLiteral => Slice(expression), // python's own syntax, verbatim
            BooleanLiteral literal => literal.Value ? "True" : "False",
            NoneLiteral => "None",
            EllipsisLiteral => "...",
            ListLiteral list => $"[{Exprs(list.Elements)}]",
            SetLiteral set => $"{{{Exprs(set.Elements)}}}",
            DictLiteral dict => $"{{{string.Join(", ", dict.Entries.Select(DictEntryText))}}}",
            TupleLiteral tuple => TupleText(tuple),
            ListComprehension comp => $"[{Expr(comp.Element)} {Clauses(comp.Clauses)}]",
            SetComprehension comp => $"{{{Expr(comp.Element)} {Clauses(comp.Clauses)}}}",
            DictComprehension comp => $"{{{Expr(comp.Key)}: {Expr(comp.Value)} {Clauses(comp.Clauses)}}}",
            GeneratorExpression comp => $"({Expr(comp.Element)} {Clauses(comp.Clauses)})",
            Identifier identifier => identifier.Name,
            MemberAccess { IsNullConditional: false } member => $"{Expr(member.Object)}.{member.Member}",
            IndexAccess index => $"{Expr(index.Object)}[{Expr(index.Index)}]",
            SliceAccess slice => $"{Expr(slice.Object)}[{Bound(slice.Start)}:{Bound(slice.Stop)}{(slice.Step == null ? "" : ":" + Expr(slice.Step))}]",
            FunctionCall call => CallText(call),
            UnaryOp unary => unary.Operator switch
            {
                UnaryOperator.Plus => $"+{Expr(unary.Operand)}",
                UnaryOperator.Minus => $"-{Expr(unary.Operand)}",
                UnaryOperator.Not => $"not {Expr(unary.Operand)}",
                UnaryOperator.BitwiseNot => $"~{Expr(unary.Operand)}",
                _ => throw NoRow(unary),
            },
            BinaryOp binary => $"{Expr(binary.Left)} {BinaryOperatorText(binary)} {Expr(binary.Right)}",
            ComparisonChain chain => ChainText(chain),
            ConditionalExpression conditional => $"{Expr(conditional.ThenValue)} if {Expr(conditional.Test)} else {Expr(conditional.ElseValue)}",
            LambdaExpression lambda => lambda.Parameters.IsEmpty
                ? $"lambda: {Expr(lambda.Body)}"
                : $"lambda {string.Join(", ", lambda.Parameters.Select(ParameterText))}: {Expr(lambda.Body)}",
            // A cast carries no name verdict beyond the type position; the twin keeps the value (D1).
            TypeCoercion { Mode: CastFailureMode.Throw } cast => Expr(cast.Value),
            TypeCoercion { Mode: CastFailureMode.Null } cast => $"({Expr(cast.Value)} if isinstance({Expr(cast.Value)}, {TypeName(cast.TargetType)}) else None)",
            Parenthesized parenthesized => $"({Expr(parenthesized.Expression)})",
            SuperExpression => "super()",
            WalrusExpression walrus => $"{walrus.Target} := {Expr(walrus.Value)}",
            StarExpression star => $"*{Expr(star.Operand)}",
            SpreadElement spread => $"*{Expr(spread.Value)}",
            null => throw new ArgumentNullException(nameof(expression)),
            _ => throw NoRow(expression),
        };

        private string Exprs(IEnumerable<Expression> expressions) => string.Join(", ", expressions.Select(Expr));

        private string Bound(Expression? expression) => expression == null ? "" : Expr(expression);

        private string DictEntryText(DictEntry entry) =>
            entry.Key == null ? $"**{Expr(entry.Value)}" : $"{Expr(entry.Key)}: {Expr(entry.Value)}";

        private string TupleText(TupleLiteral tuple)
        {
            if (tuple.ElementNames.Any(n => n != null))
                throw NoRow(tuple);
            if (tuple.IsListDisplay)
                return $"[{Exprs(tuple.Elements)}]";
            return tuple.Elements.Length == 1 ? $"({Expr(tuple.Elements[0])},)" : $"({Exprs(tuple.Elements)})";
        }

        /// <summary>A <c>for</c> target: a tuple prints bare, as the source and python spell it.</summary>
        private string Target(Expression target) =>
            target is TupleLiteral { IsListDisplay: false } tuple ? Exprs(tuple.Elements) : Expr(target);

        private string Clauses(ImmutableArray<ComprehensionClause> clauses) => string.Join(" ", clauses.Select(clause => clause switch
        {
            ForClause forClause => $"{(forClause.IsAsync ? "async for" : "for")} {Target(forClause.Target)} in {Expr(forClause.Iterator)}",
            IfClause ifClause => $"if {Expr(ifClause.Condition)}",
            _ => throw NoRow(clause),
        }));

        private string CallText(FunctionCall call)
        {
            if (call.Function is Identifier { Name: "Some" })
                _usesSome = true;
            var arguments = call.Arguments.Select(Expr).Concat(call.KeywordArguments.Select(KeywordText));
            return $"{Expr(call.Function)}({string.Join(", ", arguments)})";
        }

        private string KeywordText(KeywordArgument keyword) => $"{keyword.Name}={Expr(keyword.Value)}";

        private string ChainText(ComparisonChain chain)
        {
            var text = new StringBuilder(Expr(chain.Operands[0]));
            for (var i = 0; i < chain.Operators.Length; i++)
                text.Append(' ').Append(ComparisonOperatorText(chain.Operators[i])).Append(' ').Append(Expr(chain.Operands[i + 1]));
            return text.ToString();
        }

        private static string ComparisonOperatorText(ComparisonOperator op) => op switch
        {
            ComparisonOperator.Equal => "==",
            ComparisonOperator.NotEqual => "!=",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "<=",
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.GreaterThanOrEqual => ">=",
            ComparisonOperator.In => "in",
            ComparisonOperator.NotIn => "not in",
            ComparisonOperator.Is => "is",
            ComparisonOperator.IsNot => "is not",
            _ => throw new NotSupportedException($"TwinRenderer: no table row for the comparison operator {op}."),
        };

        private static string BinaryOperatorText(BinaryOp binary) => binary.Operator switch
        {
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.FloorDivide => "//",
            BinaryOperator.Modulo => "%",
            BinaryOperator.Power => "**",
            BinaryOperator.MatMul => "@",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.LessThan => "<",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.And => "and",
            BinaryOperator.Or => "or",
            BinaryOperator.BitwiseAnd => "&",
            BinaryOperator.BitwiseOr => "|",
            BinaryOperator.BitwiseXor => "^",
            BinaryOperator.LeftShift => "<<",
            BinaryOperator.RightShift => ">>",
            BinaryOperator.In => "in",
            BinaryOperator.NotIn => "not in",
            BinaryOperator.Is => "is",
            BinaryOperator.IsNot => "is not",
            _ => throw new NotSupportedException($"TwinRenderer: no table row for the binary operator {binary.Operator} (L{binary.LineStart})."),
        };

        private static string AssignmentOp(AssignmentOperator op) => op switch
        {
            AssignmentOperator.Assign => "=",
            AssignmentOperator.PlusAssign => "+=",
            AssignmentOperator.MinusAssign => "-=",
            AssignmentOperator.StarAssign => "*=",
            AssignmentOperator.SlashAssign => "/=",
            AssignmentOperator.DoubleSlashAssign => "//=",
            AssignmentOperator.PercentAssign => "%=",
            AssignmentOperator.PowerAssign => "**=",
            AssignmentOperator.AndAssign => "&=",
            AssignmentOperator.OrAssign => "|=",
            AssignmentOperator.XorAssign => "^=",
            AssignmentOperator.LeftShiftAssign => "<<=",
            AssignmentOperator.RightShiftAssign => ">>=",
            AssignmentOperator.MatMulAssign => "@=",
            _ => throw new NotSupportedException($"TwinRenderer: no table row for the assignment operator {op}."),
        };

        // ---- patterns ------------------------------------------------------------------------------------------

        /// <summary>Verbatim python 3.10 pattern syntax; a Sharpy-only pattern (<c>and</c>, relational, a nested guard) throws.</summary>
        private string PatternText(Pattern pattern)
        {
            switch (pattern)
            {
                case WildcardPattern:
                    return "_";
                case BindingPattern { Type: null } binding:
                    return binding.Name.Name;
                case LiteralPattern literal:
                    return Expr(literal.Literal);
                case TypePattern type:
                    return $"{PatternTypeName(type.Type)}()";
                case MemberAccessPattern member:
                    return string.Join(".", member.Parts);
                case TuplePattern tuple:
                    return tuple.Elements.Length == 1 ? $"({PatternText(tuple.Elements[0])},)" : $"({Patterns(tuple.Elements)})";
                case ListPattern list:
                    return $"[{Patterns(list.Elements)}]";
                case StarPattern star:
                    return star.Capture == null ? "*_" : $"*{PatternText(star.Capture)}";
                case PropertyPattern { Type: not null } property:
                    return $"{PatternTypeName(property.Type)}({string.Join(", ", property.Fields.Select(f => $"{f.Name}={PatternText(f.Pattern)}"))})";
                case PositionalPattern { Type: not null } positional:
                    return $"{PatternTypeName(positional.Type)}({Patterns(positional.Elements)})";
                case OrPattern or:
                    return string.Join(" | ", or.Alternatives.Select(PatternText));
                case AsPattern alias:
                    return $"{PatternText(alias.Inner)} as {alias.Name.Name}";
                default:
                    throw NoRow(pattern);
            }
        }

        private string Patterns(ImmutableArray<Pattern> patterns) => string.Join(", ", patterns.Select(PatternText));

        private string PatternTypeName(TypeAnnotation type)
        {
            if (type.Name == "Some")
                _usesSome = true;
            return TypeName(type);
        }

        // ---- shared pieces -------------------------------------------------------------------------------------

        private string ParameterText(Parameter parameter)
        {
            if (parameter.Modifier != ParameterModifier.None || parameter.IsLateBound || parameter.Kind != ParameterKind.Normal)
                throw new NotSupportedException($"TwinRenderer: no table row for the parameter form of '{parameter.Name}' (L{parameter.LineStart}).");
            var name = parameter.IsVariadic ? "*" + parameter.Name : parameter.Name;
            var annotation = !RowStripAnnotations && parameter.Type != null ? ": " + Slice(parameter.Type) : "";
            return parameter.DefaultValue == null ? $"{name}{annotation}" : $"{name}{annotation}={Expr(parameter.DefaultValue)}";
        }

        /// <summary>Names only: python 3.12's own syntax, needed so <c>G[int].K</c> runs. Constraints are annotations and strip.</summary>
        private static string TypeParameters(ImmutableArray<TypeParameterDef> parameters) =>
            parameters.IsEmpty ? "" : $"[{string.Join(", ", parameters.Select(p => p.Name))}]";

        private static string Bases(ImmutableArray<TypeAnnotation> bases) =>
            bases.IsEmpty ? "" : $"({string.Join(", ", bases.Select(TypeName))})";

        /// <summary>A type in a VALUE position of the twin (a base, a cast target, an alias target, a pattern head).</summary>
        private static string TypeName(TypeAnnotation type)
        {
            if (type.IsOptional || type.IsCSharpNullable || type.IsResult)
                throw new NotSupportedException($"TwinRenderer: no table row for the type '{type.Name}' in a value position (L{type.LineStart}).");
            var name = type.NameParts.IsEmpty ? type.Name : string.Join(".", type.NameParts);
            return type.TypeArguments.IsEmpty ? name : $"{name}[{string.Join(", ", type.TypeArguments.Select(TypeName))}]";
        }

        private static string ImportAliasText(ImportAlias alias) =>
            alias.AsName == null ? alias.Name : $"{alias.Name} as {alias.AsName}";

        private string DecoratorText(Decorator decorator)
        {
            if (decorator.Arguments.IsEmpty && decorator.KeywordArguments.IsEmpty)
                return $"@{decorator.Name}";
            var arguments = decorator.Arguments.Select(Expr).Concat(decorator.KeywordArguments.Select(KeywordText));
            return $"@{decorator.Name}({string.Join(", ", arguments)})";
        }

        /// <summary>Whether a field or property is <c>@static</c>; that decorator's line is dropped, any other has no row.</summary>
        private static bool HasStaticOnly(ImmutableArray<Decorator> decorators, Statement owner)
        {
            foreach (var decorator in decorators)
            {
                if (decorator.Name != "static")
                    throw NoDecoratorRow(decorator, owner.GetType().Name);
            }

            return !decorators.IsEmpty;
        }

        private string Slice(Node node)
        {
            if (node.Span is not { } span)
                throw new NotSupportedException($"TwinRenderer: {node.GetType().Name} (L{node.LineStart}) has no source span to print verbatim.");
            return _source.Substring(span.Start, span.Length);
        }

        private static string PythonString(string text) =>
            "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

        private static NotSupportedException NoRow(Node node) =>
            new($"TwinRenderer: no table row for {node.GetType().Name} (L{node.LineStart}); a dropped construct would make the python arm run trivially.");

        private static NotSupportedException NoDecoratorRow(Decorator decorator, string owner) =>
            new($"TwinRenderer: no table row for the decorator @{decorator.Name} on {owner} (L{decorator.LineStart}).");
    }
}

public partial class NameResolutionDifferentialTests
{
    /// <summary>The seed folder's files, and the normaliser that makes a batch verdict comparable with the inventory's.</summary>
    private static class TwinSeed
    {
        public const int SpyCount = 168;
        public const int HandTwinCount = 32;

        private static readonly Regex Address = new("0x[0-9a-f]+", RegexOptions.CultureInvariant);

        public static string Folder => Path.Combine(AppContext.BaseDirectory, "Conformance", "NameResolutionSeed");

        public static IReadOnlyList<string> Ids()
        {
            var ids = Directory.GetFiles(Folder, "*.spy").Select(f => Path.GetFileNameWithoutExtension(f)!)
                .OrderBy(id => id, StringComparer.Ordinal).ToList();
            Assert.Equal(SpyCount, ids.Count);
            return ids;
        }

        public static IReadOnlyList<string> HandTwinIds()
        {
            var ids = Directory.GetFiles(Folder, "*.py").Select(f => Path.GetFileNameWithoutExtension(f)!)
                .OrderBy(id => id, StringComparer.Ordinal).ToList();
            Assert.Equal(HandTwinCount, ids.Count);
            return ids;
        }

        public static string Spy(string id) => File.ReadAllText(Path.Combine(Folder, id + ".spy"));

        public static string? HandTwin(string id)
        {
            var path = Path.Combine(Folder, id + ".py");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public static IReadOnlyDictionary<string, string> RecordedVerdicts()
        {
            var verdicts = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(Path.Combine(Folder, "seed-python-verdicts.tsv")))
            {
                var tab = line.IndexOf('\t', StringComparison.Ordinal);
                verdicts.Add(line[..tab], line[(tab + 1)..]);
            }

            Assert.Equal(SpyCount, verdicts.Count);
            return verdicts;
        }

        public static string[] Lines(string text) => text.TrimEnd().Split('\n').Select(l => l.TrimEnd()).ToArray();

        /// <summary>The inventory's <c>runner.py run_py</c>, over a batch verdict: stdout lines joined with <c> / </c>, then <c>EXC &lt;last stderr line&gt;</c>.</summary>
        public static string Verdict(PyResult result)
        {
            var stdout = string.Join(" / ", result.Stdout.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)));
            if (result.Ok)
                return stdout.Length > 200 ? stdout[..200] : stdout;
            var last = result.Stderr.Trim().Split('\n').LastOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "?";
            return (stdout.Length > 0 ? stdout + " / " : "") + "EXC " + (last.Length > 150 ? last[..150] : last);
        }

        /// <summary>A function repr's address (A14, B28) differs per run; both sides of a comparison mask it.</summary>
        public static string MaskAddresses(string verdict) => Address.Replace(verdict, "0x…");

        public static IReadOnlyDictionary<int, PyResult> RunBatch(IReadOnlyList<string> sources)
        {
            var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 12));
            return oracle.RunBatch(sources.Select((source, index) => new PyRequest(index, source)).ToList());
        }
    }

    /// <summary>One row of D1's table no seed exercises: a tiny source, its exact twin, the stdout python prints for it.</summary>
    private sealed record TwinRow(string Spy, string Python, string Stdout);

    private static readonly IReadOnlyDictionary<string, TwinRow> TwinRows = new Dictionary<string, TwinRow>
    {
        ["elif"] = new(
            """
            def main() -> None:
                if False:
                    print(1)
                elif True:
                    print(2)
                else:
                    print(3)
            """,
            """
            def main():
                if False:
                    print(1)
                elif True:
                    print(2)
                else:
                    print(3)
            main()
            """,
            "2\n"),
        ["enum"] = new(
            """
            enum E:
                RED = 16
            def main() -> None:
                print(E.RED.value, E.RED.name)
            """,
            """
            from enum import Enum
            class E(Enum):
                RED = 16
            def main():
                print(E.RED.value, E.RED.name)
            main()
            """,
            "16 RED\n"),
        ["union"] = new(
            """
            union U:
                case V(f: int)
                case L()
            def main() -> None:
                match U.V(3):
                    case V(v):
                        print(v)
                    case L():
                        print(0)
            """,
            """
            class U:
                pass
            class V(U):
                __match_args__ = ("f",)
                def __init__(self, f):
                    self.f = f
            class L(U):
                pass
            U.V = V
            U.L = L
            def main():
                match U.V(3):
                    case V(v):
                        print(v)
                    case L():
                        print(0)
            main()
            """,
            "3\n"),
        ["interface"] = new(
            """
            interface I:
                def m(self) -> int:
                    ...
            class C(I):
                def m(self) -> int:
                    return 18
            def use(i: I) -> int:
                return i.m()
            def main() -> None:
                print(use(C()))
            """,
            """
            class I:
                def m(self):
                    ...
            class C(I):
                def m(self):
                    return 18
            def use(i):
                return i.m()
            def main():
                print(use(C()))
            main()
            """,
            "18\n"),
        ["accessors"] = new(
            """
            class C:
                def __init__(self) -> None:
                    self.w = 0
                property get p(self) -> int:
                    return self.w
                property set p(self, v: int):
                    self.w = v
            def main() -> None:
                c = C()
                c.p = 8
                print(c.p)
            """,
            """
            class C:
                def __init__(self):
                    self.w = 0
                @property
                def p(self):
                    return self.w
                @p.setter
                def p(self, v):
                    self.w = v
            def main():
                c = C()
                c.p = 8
                print(c.p)
            main()
            """,
            "8\n"),
        ["context_manager_exit"] = new(
            """
            class CM:
                def __enter__(self) -> int:
                    return 1
                def __exit__(self) -> None:
                    print("exit")
            def main() -> None:
                with CM() as v:
                    print(v)
            """,
            """
            class CM:
                def __enter__(self):
                    return 1
                def __exit__(self, exc_type, exc, tb):
                    print("exit")
            def main():
                with CM() as v:
                    print(v)
            main()
            """,
            "1\nexit\n"),
        ["out_let"] = new(
            """
            def f(v: out int) -> None:
                v = 15
            def main() -> None:
                f(out let v)
                f(out let w: int)
                print(v, w)
            """,
            """
            def f():
                return 15
            def main():
                v = f()
                w = f()
                print(v, w)
            main()
            """,
            "15 15\n"),
        ["type_body_fields"] = new(
            """
            e: int = 5
            def seven() -> int:
                return 7
            def get() -> int:
                return 6
            class H:
                const K: int = seven()
                r: () -> int = lambda: e
                c: list[int] = [e for v0 in [1]]
                g: () -> int = get
                @static
                f: () -> int = lambda: e
                property p: () -> int = lambda: e
            struct P:
                r: () -> int = lambda: e
                c: list[int] = [e for v0 in [1]]
                v: int = 19
                def __init__(self) -> None:
                    pass
            @dataclass
            class D:
                r: () -> int = lambda: e
                c: list[int] = [e for v0 in [1]]
                v: int = 14
                def __init__(self) -> None:
                    pass
            type A = H
            def main() -> None:
                (x := 1)
                print(x, A.K, H().r(), H().c, H().g(), H.f(), H().f(), H().p())
                print(P().r(), P().c, P().v, D().r(), D().c, D().v)
            """,
            """
            from dataclasses import dataclass
            e = 5
            def seven():
                return 7
            def get():
                return 6
            class H:
                K = seven()
                r = staticmethod(lambda: e)
                c = [e for v0 in [1]]
                g = staticmethod(get)
                f = staticmethod(lambda: e)
                p = staticmethod(lambda: e)
            @dataclass
            class P:
                r: object = staticmethod(lambda: e)
                c = [e for v0 in [1]]
                v: int = 19
                def __init__(self):
                    pass
            @dataclass
            class D:
                r: object = staticmethod(lambda: e)
                c = [e for v0 in [1]]
                v: int = 14
                def __init__(self):
                    pass
            A = H
            def main():
                (x := 1)
                print(x, A.K, H().r(), H().c, H().g(), H.f(), H().f(), H().p())
                print(P().r(), P().c, P().v, D().r(), D().c, D().v)
            main()
            """,
            "1 7 5 [5] 6 5 5 5\n5 [5] 19 5 [5] 14\n"),
        ["cast"] = new(
            """
            class Later:
                pass
            def main() -> None:
                o = Later()
                a = o as? Later
                b = o as! Later
                c = 1 as? Later
                print(a is o, b is o, c)
            """,
            """
            class Later:
                pass
            def main():
                o = Later()
                a = (o if isinstance(o, Later) else None)
                b = o
                c = (1 if isinstance(1, Later) else None)
                print(a is o, b is o, c)
            main()
            """,
            "True True None\n"),
        ["struct_dataclass_field_map"] = new(
            """
            struct P:
                v: int = 3
                f: float = 1.5
                s: str = "a"
                b: bool = True
                g: () -> int = lambda: 1
                n: int? = None
                @static
                k: int = 4
                const c: int = 5
            def main() -> None:
                print(P().v, P(9).v)
            """,
            """
            from dataclasses import dataclass
            @dataclass
            class P:
                v: int = 3
                f: float = 1.5
                s: str = "a"
                b: bool = True
                g: object = staticmethod(lambda: 1)
                n: object = None
                k = 4
                c = 5
            def main():
                print(P().v, P(9).v)
            main()
            """,
            "3 9\n"),
        ["some_shim"] = new(
            """
            class H:
                def get(self) -> int?:
                    return Some(1)
            def main() -> None:
                match H().get():
                    case Some(v):
                        print(v)
                    case _:
                        print(0)
            """,
            """
            class Some:
                __match_args__ = ("value",)
                def __init__(self, value):
                    self.value = value
            class H:
                def get(self):
                    return Some(1)
            def main():
                match H().get():
                    case Some(v):
                        print(v)
                    case _:
                        print(0)
            main()
            """,
            "1\n"),
        ["type_alias"] = new(
            """
            class H:
                v: int = 14
            type A = H
            def main() -> None:
                print(A().v)
            """,
            """
            class H:
                v = 14
            A = H
            def main():
                print(A().v)
            main()
            """,
            "14\n"),
        ["nonlocal"] = new(
            """
            def main() -> None:
                n: int = 1
                def g() -> None:
                    nonlocal n
                    n = 2
                g()
                print(n)
            """,
            """
            def main():
                n = 1
                def g():
                    nonlocal n
                    n = 2
                g()
                print(n)
            main()
            """,
            "2\n"),
        ["patterns"] = new(
            """
            class Later:
                v: int = 3
            enum E:
                RED = 16
            def main() -> None:
                o = Later()
                match o:
                    case Later() as l:
                        print(l.v)
                match o:
                    case Later(v=3):
                        print(1)
                match E.RED:
                    case E.RED:
                        print(2)
                match 5:
                    case 1 | 2:
                        print(0)
                    case _ if True:
                        print(4)
                match (1, 2):
                    case (a, _):
                        print(a)
                match [1, 2]:
                    case [_, *r]:
                        print(r)
            """,
            """
            from enum import Enum
            class Later:
                v = 3
            class E(Enum):
                RED = 16
            def main():
                o = Later()
                match o:
                    case Later() as l:
                        print(l.v)
                match o:
                    case Later(v=3):
                        print(1)
                match E.RED:
                    case E.RED:
                        print(2)
                match 5:
                    case 1 | 2:
                        print(0)
                    case _ if True:
                        print(4)
                match (1, 2):
                    case (a, _):
                        print(a)
                match [1, 2]:
                    case [_, *r]:
                        print(r)
            main()
            """,
            "3\n1\n2\n4\n1\n[2]\n"),
        ["expressions"] = new(
            """
            def main() -> None:
                xs: list[int] = [1, 2, 3]
                t = (1, 2)
                d = {"a": 1}
                s = {4}
                u = (7,)
                f = lambda p, k=2: p * k
                print(xs[0], xs[-1], xs[1:], xs[:1], xs[::2], t, d, s, u)
                print(1.5, 1_000, 0xFF, 'q', "w", True, False, None)
                print(f(2), f(2, k=3), int("4"), 1 if xs else 2, not xs, -xs[0], ~xs[0], +xs[0])
                print(1 < 2 < 3, 1 == 1, 1 != 2, 1 in xs, 9 not in xs, xs is None, xs is not None)
                print(7 // 2, 7 % 2, 2 ** 3, 7 / 2, 1 + 2 * 3, (1 + 2) * 3, 6 & 3, 6 | 3, 6 ^ 3, 1 << 2, 8 >> 1)
                print(True and False, True or False)
                print([x * 2 for x in xs if x > 1], {x for x in [1]}, {x: 1 for x in [1]}, sum(x for x in xs))
                print((y := 5), y)
                print(f"a{xs[0]!r:>4}b {t[1]}")
                a, b = 1, 2
                a += 1
                for i, j in [(a, b)]:
                    print(i, j)
            """,
            """
            def main():
                xs = [1, 2, 3]
                t = (1, 2)
                d = {"a": 1}
                s = {4}
                u = (7,)
                f = lambda p, k=2: p * k
                print(xs[0], xs[-1], xs[1:], xs[:1], xs[::2], t, d, s, u)
                print(1.5, 1000, 0xFF, 'q', "w", True, False, None)
                print(f(2), f(2, k=3), int("4"), 1 if xs else 2, not xs, -xs[0], ~xs[0], +xs[0])
                print(1 < 2 < 3, 1 == 1, 1 != 2, 1 in xs, 9 not in xs, xs is None, xs is not None)
                print(7 // 2, 7 % 2, 2 ** 3, 7 / 2, 1 + 2 * 3, (1 + 2) * 3, 6 & 3, 6 | 3, 6 ^ 3, 1 << 2, 8 >> 1)
                print(True and False, True or False)
                print([x * 2 for x in xs if x > 1], {x for x in [1]}, {x: 1 for x in [1]}, sum((x for x in xs)))
                print((y := 5), y)
                print(f"a{xs[0]!r:>4}b {t[1]}")
                (a, b) = (1, 2)
                a += 1
                for i, j in [(a, b)]:
                    print(i, j)
            main()
            """,
            """
            1 3 [2, 3] [1] [1, 3] (1, 2) {'a': 1} {4} (7,)
            1.5 1000 255 q w True False None
            4 6 4 1 False -1 -2 1
            True True True True True False True
            3 1 8 3.5 7 9 2 7 5 4 4
            False True
            [4, 6] {1} {1: 1} 6
            5 5
            a   1b 2
            2 2

            """),
    };

    public static TheoryData<string> TwinRowNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in TwinRows.Keys)
            data.Add(name);
        return data;
    }

    /// <summary>Acceptance 4: every hand twin equals the walk's output, so the twins guard the table for every generated cell.</summary>
    [Fact]
    public void TwinRenderer_SeedHandTwins_MatchByteForByte()
    {
        var mismatches = new List<string>();
        var ids = TwinSeed.HandTwinIds();
        foreach (var id in ids)
        {
            var expected = TwinSeed.Lines(TwinSeed.HandTwin(id)!);
            var actual = TwinSeed.Lines(TwinRenderer.Render(TwinSeed.Spy(id)));
            var first = Enumerable.Range(0, Math.Max(expected.Length, actual.Length))
                .Where(i => i >= expected.Length || i >= actual.Length || expected[i] != actual[i])
                .Select(i => (int?)i)
                .FirstOrDefault();
            if (first is { } line)
            {
                var hand = line < expected.Length ? expected[line] : "<end of file>";
                var rendered = line < actual.Length ? actual[line] : "<end of file>";
                mismatches.Add($"{id} line {line + 1}: hand `{hand}`, renderer `{rendered}`");
            }
        }

        Assert.True(mismatches.Count == 0,
            $"{mismatches.Count} of {ids.Count} hand twins differ from the renderer:\n{string.Join("\n", mismatches)}");
    }

    /// <summary>Adversarial Review (2): a twin python cannot parse is a renderer bug, never a skip.</summary>
    [SkippableFact]
    public void TwinRenderer_EverySeedRenders_AndPythonParses()
    {
        var ids = TwinSeed.Ids();
        var rendered = ids.Select(id => TwinRenderer.Render(TwinSeed.Spy(id))).ToList();
        var results = TwinSeed.RunBatch(rendered);

        Assert.Equal(ids.Count, results.Count);
        var syntaxErrors = results.Where(r => r.Value.SyntaxError).OrderBy(r => r.Key)
            .Select(r => $"{ids[r.Key]}: {r.Value.Stderr.Trim().Split('\n').Last()}").ToList();
        Assert.True(syntaxErrors.Count == 0,
            $"{syntaxErrors.Count} of {ids.Count} rendered twins are a python SyntaxError:\n{string.Join("\n", syntaxErrors)}");
    }

    /// <summary>D7's control: a twin whose python verdict moved from the inventory's recorded one is a seeding error.</summary>
    [SkippableFact]
    public void TwinRenderer_SeedPythonVerdicts_Reproduce()
    {
        var ids = TwinSeed.Ids();
        var recorded = TwinSeed.RecordedVerdicts();
        var twins = ids.Select(id => TwinSeed.HandTwin(id) ?? TwinRenderer.Render(TwinSeed.Spy(id))).ToList();
        var results = TwinSeed.RunBatch(twins);

        Assert.Equal(ids.Count, results.Count);
        var moved = new List<string>();
        for (var i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            var actual = TwinSeed.MaskAddresses(TwinSeed.Verdict(results[i]));
            var expected = TwinSeed.MaskAddresses(recorded[id]);
            if (actual != expected)
                moved.Add($"{id}: recorded `{expected}`, twin `{actual}`");
        }

        Assert.True(moved.Count == 0,
            $"{moved.Count} of {ids.Count} seed python verdicts moved (a seeding error, never allowlistable):\n{string.Join("\n", moved)}");
    }

    [Theory]
    [MemberData(nameof(TwinRowNames))]
    public void TwinRenderer_TableRow_RendersExactPython(string row)
    {
        var rendered = TwinRenderer.Render(TwinRows[row].Spy);

        Assert.DoesNotContain("\n\n", rendered);
        Assert.Equal(TwinRows[row].Python.TrimEnd() + "\n", rendered);
    }

    /// <summary>The same rows through CPython, so a row's exact string is also a program with the model's stdout.</summary>
    [SkippableFact]
    public void TwinRenderer_TableRows_ExecuteToTheModelsStdout()
    {
        var names = TwinRows.Keys.ToList();
        var results = TwinSeed.RunBatch(names.Select(n => TwinRenderer.Render(TwinRows[n].Spy)).ToList());

        Assert.Equal(names.Count, results.Count);
        var wrong = new List<string>();
        for (var i = 0; i < names.Count; i++)
        {
            var expected = TwinRows[names[i]].Stdout.TrimEnd() + "\n";
            if (!results[i].Ok || results[i].Stdout != expected)
                wrong.Add($"{names[i]}: expected `{expected}`, stdout `{results[i].Stdout}`, stderr `{results[i].Stderr}`");
        }

        Assert.True(wrong.Count == 0, $"{wrong.Count} of {names.Count} rows:\n{string.Join("\n", wrong)}");
    }

    /// <summary>The "no silent drop" rule: each shape is a node the table has no row for, so a default arm that dropped it is red here.</summary>
    [Theory]
    [InlineData("def main() -> None:\n    x = a ?? 1\n", "NullCoalesce")]
    [InlineData("def main() -> None:\n    x = maybe f()\n", "MaybeExpression")]
    [InlineData("def main() -> None:\n    defer f()\n", "DeferStatement")]
    [InlineData("class C:\n    event e: int\n", "EventDef")]
    [InlineData("def main() -> None:\n    match 1:\n        case > 0:\n            pass\n", "RelationalPattern")]
    [InlineData("class C:\n    @virtual\n    def m(self) -> int:\n        return 1\n", "@virtual")]
    public void TwinRenderer_UnsupportedNode_Throws(string source, string named)
    {
        var refused = Assert.Throws<NotSupportedException>(() => TwinRenderer.Render(source));

        Assert.Contains(named, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>A parser refusal is an exception carrying the code, never an empty twin.</summary>
    [Fact]
    public void TwinRenderer_ParserRefusal_ThrowsWithTheCode()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => TwinRenderer.Render("def f(out v: int) -> None:\n    v = 15\n"));

        Assert.Contains("SPY0104", refused.Message, StringComparison.Ordinal);
    }
}
