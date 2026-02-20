using System.Text;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// Utility class for dumping AST nodes in a human-readable tree format
/// </summary>
internal class AstDumper
{
    private readonly StringBuilder _output;
    private const string IndentUnit = "  ";

    public AstDumper()
    {
        _output = new StringBuilder();
    }

    /// <summary>
    /// Dump a module to a human-readable string
    /// </summary>
    public string Dump(Module module)
    {
        _output.Clear();
        _output.AppendLine($"Module @ L{module.LineStart}:C{module.ColumnStart}");

        if (!string.IsNullOrEmpty(module.DocString))
        {
            _output.AppendLine($"{IndentUnit}DocString: \"{EscapeString(module.DocString)}\"");
        }

        _output.AppendLine($"{IndentUnit}Body: [{module.Body.Length} statement(s)]");

        for (int i = 0; i < module.Body.Length; i++)
        {
            DumpNode(module.Body[i], 2, i == module.Body.Length - 1);
        }

        return _output.ToString();
    }

    private void DumpNode(Node node, int depth, bool isLast)
    {
        var indent = new string(' ', depth * IndentUnit.Length);
        var prefix = isLast ? "└─ " : "├─ ";
        var childPrefix = isLast ? "   " : "│  ";

        switch (node)
        {
            // Statements
            case ExpressionStatement exprStmt:
                _output.AppendLine($"{indent}{prefix}ExpressionStatement @ L{node.LineStart}:C{node.ColumnStart}");
                DumpNode(exprStmt.Expression, depth + 1, true);
                break;

            case Assignment assignment:
                _output.AppendLine($"{indent}{prefix}Assignment @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Operator: {assignment.Operator}");
                _output.AppendLine($"{indent}{childPrefix}Target:");
                DumpNode(assignment.Target, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Value:");
                DumpNode(assignment.Value, depth + 2, true);
                break;

            case VariableDeclaration varDecl:
                _output.AppendLine($"{indent}{prefix}VariableDeclaration @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Name: {varDecl.Name}");
                _output.AppendLine($"{indent}{childPrefix}IsConst: {varDecl.IsConst}");
                if (varDecl.Type != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}Type:");
                    DumpTypeAnnotation(varDecl.Type, depth + 2, varDecl.InitialValue == null);
                }
                if (varDecl.InitialValue != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}InitialValue:");
                    DumpNode(varDecl.InitialValue, depth + 2, true);
                }
                break;

            case ReturnStatement returnStmt:
                _output.AppendLine($"{indent}{prefix}ReturnStatement @ L{node.LineStart}:C{node.ColumnStart}");
                if (returnStmt.Value != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}Value:");
                    DumpNode(returnStmt.Value, depth + 1, true);
                }
                break;

            case PassStatement:
                _output.AppendLine($"{indent}{prefix}PassStatement @ L{node.LineStart}:C{node.ColumnStart}");
                break;

            case BreakStatement:
                _output.AppendLine($"{indent}{prefix}BreakStatement @ L{node.LineStart}:C{node.ColumnStart}");
                break;

            case ContinueStatement:
                _output.AppendLine($"{indent}{prefix}ContinueStatement @ L{node.LineStart}:C{node.ColumnStart}");
                break;

            case RaiseStatement raiseStmt:
                _output.AppendLine($"{indent}{prefix}RaiseStatement @ L{node.LineStart}:C{node.ColumnStart}");
                if (raiseStmt.Exception != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}Exception:");
                    DumpNode(raiseStmt.Exception, depth + 1, true);
                }
                break;

            case AssertStatement assertStmt:
                _output.AppendLine($"{indent}{prefix}AssertStatement @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Test:");
                DumpNode(assertStmt.Test, depth + 1, assertStmt.Message == null);
                if (assertStmt.Message != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}Message:");
                    DumpNode(assertStmt.Message, depth + 1, true);
                }
                break;

            case IfStatement ifStmt:
                _output.AppendLine($"{indent}{prefix}IfStatement @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Test:");
                DumpNode(ifStmt.Test, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}ThenBody: [{ifStmt.ThenBody.Length} statement(s)]");
                for (int i = 0; i < ifStmt.ThenBody.Length; i++)
                {
                    DumpNode(ifStmt.ThenBody[i], depth + 2, i == ifStmt.ThenBody.Length - 1);
                }
                if (ifStmt.ElifClauses.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}ElifClauses: [{ifStmt.ElifClauses.Length}]");
                    for (int i = 0; i < ifStmt.ElifClauses.Length; i++)
                    {
                        var elif = ifStmt.ElifClauses[i];
                        var elifIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                        var elifPrefix = i == ifStmt.ElifClauses.Length - 1 && ifStmt.ElseBody.Length == 0 ? "└─ " : "├─ ";
                        _output.AppendLine($"{elifIndent}{elifPrefix}ElifClause @ L{elif.LineStart}:C{elif.ColumnStart}");
                        _output.AppendLine($"{elifIndent}   Test:");
                        DumpNode(elif.Test, depth + 3, false);
                        _output.AppendLine($"{elifIndent}   Body: [{elif.Body.Length} statement(s)]");
                        for (int j = 0; j < elif.Body.Length; j++)
                        {
                            DumpNode(elif.Body[j], depth + 3, j == elif.Body.Length - 1);
                        }
                    }
                }
                if (ifStmt.ElseBody.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}ElseBody: [{ifStmt.ElseBody.Length} statement(s)]");
                    for (int i = 0; i < ifStmt.ElseBody.Length; i++)
                    {
                        DumpNode(ifStmt.ElseBody[i], depth + 2, i == ifStmt.ElseBody.Length - 1);
                    }
                }
                break;

            case WhileStatement whileStmt:
                _output.AppendLine($"{indent}{prefix}WhileStatement @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Test:");
                DumpNode(whileStmt.Test, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Body: [{whileStmt.Body.Length} statement(s)]");
                for (int i = 0; i < whileStmt.Body.Length; i++)
                {
                    DumpNode(whileStmt.Body[i], depth + 2, i == whileStmt.Body.Length - 1);
                }
                break;

            case ForStatement forStmt:
                _output.AppendLine($"{indent}{prefix}ForStatement @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Target:");
                DumpNode(forStmt.Target, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Iterator:");
                DumpNode(forStmt.Iterator, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Body: [{forStmt.Body.Length} statement(s)]");
                for (int i = 0; i < forStmt.Body.Length; i++)
                {
                    DumpNode(forStmt.Body[i], depth + 2, i == forStmt.Body.Length - 1);
                }
                break;

            case TryStatement tryStmt:
                _output.AppendLine($"{indent}{prefix}TryStatement @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Body: [{tryStmt.Body.Length} statement(s)]");
                for (int i = 0; i < tryStmt.Body.Length; i++)
                {
                    DumpNode(tryStmt.Body[i], depth + 2, i == tryStmt.Body.Length - 1);
                }
                if (tryStmt.Handlers.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}Handlers: [{tryStmt.Handlers.Length}]");
                    for (int i = 0; i < tryStmt.Handlers.Length; i++)
                    {
                        var handler = tryStmt.Handlers[i];
                        var handlerIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                        var handlerPrefix = i == tryStmt.Handlers.Length - 1 && tryStmt.ElseBody.Length == 0 && tryStmt.FinallyBody.Length == 0 ? "└─ " : "├─ ";
                        _output.AppendLine($"{handlerIndent}{handlerPrefix}ExceptHandler @ L{handler.LineStart}:C{handler.ColumnStart}");
                        if (handler.ExceptionType != null)
                        {
                            _output.AppendLine($"{handlerIndent}   ExceptionType:");
                            DumpTypeAnnotation(handler.ExceptionType, depth + 3, handler.Name == null);
                        }
                        if (handler.Name != null)
                        {
                            _output.AppendLine($"{handlerIndent}   Name: {handler.Name}");
                        }
                        _output.AppendLine($"{handlerIndent}   Body: [{handler.Body.Length} statement(s)]");
                        for (int j = 0; j < handler.Body.Length; j++)
                        {
                            DumpNode(handler.Body[j], depth + 3, j == handler.Body.Length - 1);
                        }
                    }
                }
                if (tryStmt.ElseBody.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}ElseBody: [{tryStmt.ElseBody.Length} statement(s)]");
                    for (int i = 0; i < tryStmt.ElseBody.Length; i++)
                    {
                        DumpNode(tryStmt.ElseBody[i], depth + 2, i == tryStmt.ElseBody.Length - 1 && tryStmt.FinallyBody.Length == 0);
                    }
                }
                if (tryStmt.FinallyBody.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}FinallyBody: [{tryStmt.FinallyBody.Length} statement(s)]");
                    for (int i = 0; i < tryStmt.FinallyBody.Length; i++)
                    {
                        DumpNode(tryStmt.FinallyBody[i], depth + 2, i == tryStmt.FinallyBody.Length - 1);
                    }
                }
                break;

            case FunctionDef funcDef:
                _output.AppendLine($"{indent}{prefix}FunctionDef @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Name: {funcDef.Name}");
                if (funcDef.DocString != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}DocString: \"{EscapeString(funcDef.DocString)}\"");
                }
                if (funcDef.Decorators.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}Decorators: [{funcDef.Decorators.Length}]");
                    for (int i = 0; i < funcDef.Decorators.Length; i++)
                    {
                        var decorator = funcDef.Decorators[i];
                        var decIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                        var decPrefix = i == funcDef.Decorators.Length - 1 ? "└─ " : "├─ ";
                        _output.AppendLine($"{decIndent}{decPrefix}@{decorator.Name} @ L{decorator.LineStart}:C{decorator.ColumnStart}");
                    }
                }
                if (funcDef.Parameters.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}Parameters: [{funcDef.Parameters.Length}]");
                    for (int i = 0; i < funcDef.Parameters.Length; i++)
                    {
                        DumpParameter(funcDef.Parameters[i], depth + 2, i == funcDef.Parameters.Length - 1);
                    }
                }
                if (funcDef.ReturnType != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}ReturnType:");
                    DumpTypeAnnotation(funcDef.ReturnType, depth + 2, false);
                }
                _output.AppendLine($"{indent}{childPrefix}Body: [{funcDef.Body.Length} statement(s)]");
                for (int i = 0; i < funcDef.Body.Length; i++)
                {
                    DumpNode(funcDef.Body[i], depth + 2, i == funcDef.Body.Length - 1);
                }
                break;

            case ClassDef classDef:
                _output.AppendLine($"{indent}{prefix}ClassDef @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Name: {classDef.Name}");
                if (classDef.DocString != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}DocString: \"{EscapeString(classDef.DocString)}\"");
                }
                if (classDef.TypeParameters.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}TypeParameters: [{string.Join(", ", classDef.TypeParameters.Select(tp => tp.Name))}]");
                }
                if (classDef.Decorators.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}Decorators: [{classDef.Decorators.Length}]");
                    for (int i = 0; i < classDef.Decorators.Length; i++)
                    {
                        var decorator = classDef.Decorators[i];
                        var decIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                        var decPrefix = i == classDef.Decorators.Length - 1 ? "└─ " : "├─ ";
                        _output.AppendLine($"{decIndent}{decPrefix}@{decorator.Name} @ L{decorator.LineStart}:C{decorator.ColumnStart}");
                    }
                }
                if (classDef.BaseClasses.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}BaseClasses: [{classDef.BaseClasses.Length}]");
                    for (int i = 0; i < classDef.BaseClasses.Length; i++)
                    {
                        DumpTypeAnnotation(classDef.BaseClasses[i], depth + 2, i == classDef.BaseClasses.Length - 1);
                    }
                }
                _output.AppendLine($"{indent}{childPrefix}Body: [{classDef.Body.Length} statement(s)]");
                for (int i = 0; i < classDef.Body.Length; i++)
                {
                    DumpNode(classDef.Body[i], depth + 2, i == classDef.Body.Length - 1);
                }
                break;

            case StructDef structDef:
                _output.AppendLine($"{indent}{prefix}StructDef @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Name: {structDef.Name}");
                if (structDef.DocString != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}DocString: \"{EscapeString(structDef.DocString)}\"");
                }
                if (structDef.TypeParameters.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}TypeParameters: [{string.Join(", ", structDef.TypeParameters.Select(tp => tp.Name))}]");
                }
                if (structDef.BaseClasses.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}BaseClasses: [{structDef.BaseClasses.Length}]");
                    for (int i = 0; i < structDef.BaseClasses.Length; i++)
                    {
                        DumpTypeAnnotation(structDef.BaseClasses[i], depth + 2, i == structDef.BaseClasses.Length - 1);
                    }
                }
                _output.AppendLine($"{indent}{childPrefix}Body: [{structDef.Body.Length} statement(s)]");
                for (int i = 0; i < structDef.Body.Length; i++)
                {
                    DumpNode(structDef.Body[i], depth + 2, i == structDef.Body.Length - 1);
                }
                break;

            case InterfaceDef interfaceDef:
                _output.AppendLine($"{indent}{prefix}InterfaceDef @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Name: {interfaceDef.Name}");
                if (interfaceDef.DocString != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}DocString: \"{EscapeString(interfaceDef.DocString)}\"");
                }
                if (interfaceDef.TypeParameters.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}TypeParameters: [{string.Join(", ", interfaceDef.TypeParameters.Select(tp => tp.Name))}]");
                }
                if (interfaceDef.BaseInterfaces.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}BaseInterfaces: [{interfaceDef.BaseInterfaces.Length}]");
                    for (int i = 0; i < interfaceDef.BaseInterfaces.Length; i++)
                    {
                        DumpTypeAnnotation(interfaceDef.BaseInterfaces[i], depth + 2, i == interfaceDef.BaseInterfaces.Length - 1);
                    }
                }
                _output.AppendLine($"{indent}{childPrefix}Body: [{interfaceDef.Body.Length} statement(s)]");
                for (int i = 0; i < interfaceDef.Body.Length; i++)
                {
                    DumpNode(interfaceDef.Body[i], depth + 2, i == interfaceDef.Body.Length - 1);
                }
                break;

            case EnumDef enumDef:
                _output.AppendLine($"{indent}{prefix}EnumDef @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Name: {enumDef.Name}");
                if (enumDef.DocString != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}DocString: \"{EscapeString(enumDef.DocString)}\"");
                }
                _output.AppendLine($"{indent}{childPrefix}Members: [{enumDef.Members.Length}]");
                for (int i = 0; i < enumDef.Members.Length; i++)
                {
                    var member = enumDef.Members[i];
                    var memIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                    var memPrefix = i == enumDef.Members.Length - 1 ? "└─ " : "├─ ";
                    if (member.Value != null)
                    {
                        _output.AppendLine($"{memIndent}{memPrefix}{member.Name} @ L{member.LineStart}:C{member.ColumnStart} =");
                        DumpNode(member.Value, depth + 3, true);
                    }
                    else
                    {
                        _output.AppendLine($"{memIndent}{memPrefix}{member.Name} @ L{member.LineStart}:C{member.ColumnStart}");
                    }
                }
                break;

            case PropertyDef propDef:
                _output.AppendLine($"{indent}{prefix}PropertyDef @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Name: {propDef.Name}");
                _output.AppendLine($"{indent}{childPrefix}Accessor: {propDef.Accessor}");
                _output.AppendLine($"{indent}{childPrefix}FunctionStyle: {propDef.IsFunctionStyle}");
                if (propDef.ExplicitInterface != null)
                    _output.AppendLine($"{indent}{childPrefix}ExplicitInterface: {propDef.ExplicitInterface}");
                if (propDef.Type != null)
                    _output.AppendLine($"{indent}{childPrefix}Type: {propDef.Type.Name}");
                if (propDef.ReturnType != null)
                    _output.AppendLine($"{indent}{childPrefix}ReturnType: {propDef.ReturnType.Name}");
                if (propDef.DefaultValue != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}DefaultValue:");
                    DumpNode(propDef.DefaultValue, depth + 2, true);
                }
                if (propDef.IsFunctionStyle && propDef.Body.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}Body: [{propDef.Body.Length}]");
                    for (int i = 0; i < propDef.Body.Length; i++)
                    {
                        DumpNode(propDef.Body[i], depth + 2, i == propDef.Body.Length - 1);
                    }
                }
                break;

            case ImportStatement importStmt:
                _output.AppendLine($"{indent}{prefix}ImportStatement @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Names: [{importStmt.Names.Length}]");
                for (int i = 0; i < importStmt.Names.Length; i++)
                {
                    var import = importStmt.Names[i];
                    var impIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                    var impPrefix = i == importStmt.Names.Length - 1 ? "└─ " : "├─ ";
                    if (import.AsName != null)
                    {
                        _output.AppendLine($"{impIndent}{impPrefix}{import.Name} as {import.AsName} @ L{import.LineStart}:C{import.ColumnStart}");
                    }
                    else
                    {
                        _output.AppendLine($"{impIndent}{impPrefix}{import.Name} @ L{import.LineStart}:C{import.ColumnStart}");
                    }
                }
                break;

            case FromImportStatement fromImportStmt:
                _output.AppendLine($"{indent}{prefix}FromImportStatement @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Module: {fromImportStmt.Module}");
                if (fromImportStmt.ImportAll)
                {
                    _output.AppendLine($"{indent}{childPrefix}ImportAll: true");
                }
                else
                {
                    _output.AppendLine($"{indent}{childPrefix}Names: [{fromImportStmt.Names.Length}]");
                    for (int i = 0; i < fromImportStmt.Names.Length; i++)
                    {
                        var import = fromImportStmt.Names[i];
                        var impIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                        var impPrefix = i == fromImportStmt.Names.Length - 1 ? "└─ " : "├─ ";
                        if (import.AsName != null)
                        {
                            _output.AppendLine($"{impIndent}{impPrefix}{import.Name} as {import.AsName} @ L{import.LineStart}:C{import.ColumnStart}");
                        }
                        else
                        {
                            _output.AppendLine($"{impIndent}{impPrefix}{import.Name} @ L{import.LineStart}:C{import.ColumnStart}");
                        }
                    }
                }
                break;

            // Expressions
            case IntegerLiteral intLit:
                var intSuffix = intLit.Suffix != null ? $" ({intLit.Suffix})" : "";
                _output.AppendLine($"{indent}{prefix}IntegerLiteral: {intLit.Value}{intSuffix} @ L{node.LineStart}:C{node.ColumnStart}");
                break;

            case FloatLiteral floatLit:
                var floatSuffix = floatLit.Suffix != null ? $" ({floatLit.Suffix})" : "";
                _output.AppendLine($"{indent}{prefix}FloatLiteral: {floatLit.Value}{floatSuffix} @ L{node.LineStart}:C{node.ColumnStart}");
                break;

            case StringLiteral strLit:
                var strPrefix = strLit.IsRaw ? "r" : "";
                _output.AppendLine($"{indent}{prefix}StringLiteral: {strPrefix}\"{EscapeString(strLit.Value)}\" @ L{node.LineStart}:C{node.ColumnStart}");
                break;

            case FStringLiteral fstrLit:
                _output.AppendLine($"{indent}{prefix}FStringLiteral @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Parts: [{fstrLit.Parts.Length}]");
                for (int i = 0; i < fstrLit.Parts.Length; i++)
                {
                    var part = fstrLit.Parts[i];
                    var partIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                    var partPrefix = i == fstrLit.Parts.Length - 1 ? "└─ " : "├─ ";
                    if (part.Text != null)
                    {
                        _output.AppendLine($"{partIndent}{partPrefix}Text: \"{EscapeString(part.Text)}\"");
                    }
                    else if (part.Expression != null)
                    {
                        _output.AppendLine($"{partIndent}{partPrefix}Expression:");
                        DumpNode(part.Expression, depth + 3, true);
                    }
                }
                break;

            case BooleanLiteral boolLit:
                _output.AppendLine($"{indent}{prefix}BooleanLiteral: {boolLit.Value} @ L{node.LineStart}:C{node.ColumnStart}");
                break;

            case NoneLiteral:
                _output.AppendLine($"{indent}{prefix}NoneLiteral @ L{node.LineStart}:C{node.ColumnStart}");
                break;

            case EllipsisLiteral:
                _output.AppendLine($"{indent}{prefix}EllipsisLiteral @ L{node.LineStart}:C{node.ColumnStart}");
                break;

            case ListLiteral listLit:
                _output.AppendLine($"{indent}{prefix}ListLiteral @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Elements: [{listLit.Elements.Length}]");
                for (int i = 0; i < listLit.Elements.Length; i++)
                {
                    DumpNode(listLit.Elements[i], depth + 2, i == listLit.Elements.Length - 1);
                }
                break;

            case DictLiteral dictLit:
                _output.AppendLine($"{indent}{prefix}DictLiteral @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Entries: [{dictLit.Entries.Length}]");
                for (int i = 0; i < dictLit.Entries.Length; i++)
                {
                    var entry = dictLit.Entries[i];
                    var entryIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                    var entryPrefix = i == dictLit.Entries.Length - 1 ? "└─ " : "├─ ";
                    _output.AppendLine($"{entryIndent}{entryPrefix}Entry:");
                    if (entry.Key != null)
                    {
                        _output.AppendLine($"{entryIndent}   Key:");
                        DumpNode(entry.Key, depth + 3, false);
                    }
                    _output.AppendLine($"{entryIndent}   Value:");
                    DumpNode(entry.Value, depth + 3, true);
                }
                break;

            case SetLiteral setLit:
                _output.AppendLine($"{indent}{prefix}SetLiteral @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Elements: [{setLit.Elements.Length}]");
                for (int i = 0; i < setLit.Elements.Length; i++)
                {
                    DumpNode(setLit.Elements[i], depth + 2, i == setLit.Elements.Length - 1);
                }
                break;

            case TupleLiteral tupleLit:
                _output.AppendLine($"{indent}{prefix}TupleLiteral @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Elements: [{tupleLit.Elements.Length}]");
                for (int i = 0; i < tupleLit.Elements.Length; i++)
                {
                    DumpNode(tupleLit.Elements[i], depth + 2, i == tupleLit.Elements.Length - 1);
                }
                break;

            case ListComprehension listComp:
                _output.AppendLine($"{indent}{prefix}ListComprehension @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Element:");
                DumpNode(listComp.Element, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Clauses: [{listComp.Clauses.Length}]");
                for (int i = 0; i < listComp.Clauses.Length; i++)
                {
                    DumpComprehensionClause(listComp.Clauses[i], depth + 2, i == listComp.Clauses.Length - 1);
                }
                break;

            case SetComprehension setComp:
                _output.AppendLine($"{indent}{prefix}SetComprehension @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Element:");
                DumpNode(setComp.Element, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Clauses: [{setComp.Clauses.Length}]");
                for (int i = 0; i < setComp.Clauses.Length; i++)
                {
                    DumpComprehensionClause(setComp.Clauses[i], depth + 2, i == setComp.Clauses.Length - 1);
                }
                break;

            case DictComprehension dictComp:
                _output.AppendLine($"{indent}{prefix}DictComprehension @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Key:");
                DumpNode(dictComp.Key, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Value:");
                DumpNode(dictComp.Value, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Clauses: [{dictComp.Clauses.Length}]");
                for (int i = 0; i < dictComp.Clauses.Length; i++)
                {
                    DumpComprehensionClause(dictComp.Clauses[i], depth + 2, i == dictComp.Clauses.Length - 1);
                }
                break;

            case Identifier ident:
                _output.AppendLine($"{indent}{prefix}Identifier: {ident.Name} @ L{node.LineStart}:C{node.ColumnStart}");
                break;

            case MemberAccess memberAccess:
                var nullCond = memberAccess.IsNullConditional ? "?." : ".";
                _output.AppendLine($"{indent}{prefix}MemberAccess ({nullCond}) @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Object:");
                DumpNode(memberAccess.Object, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Member: {memberAccess.Member}");
                break;

            case IndexAccess indexAccess:
                _output.AppendLine($"{indent}{prefix}IndexAccess @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Object:");
                DumpNode(indexAccess.Object, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Index:");
                DumpNode(indexAccess.Index, depth + 2, true);
                break;

            case SliceAccess sliceAccess:
                _output.AppendLine($"{indent}{prefix}SliceAccess @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Object:");
                DumpNode(sliceAccess.Object, depth + 2, false);
                if (sliceAccess.Start != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}Start:");
                    DumpNode(sliceAccess.Start, depth + 2, sliceAccess.Stop == null && sliceAccess.Step == null);
                }
                if (sliceAccess.Stop != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}Stop:");
                    DumpNode(sliceAccess.Stop, depth + 2, sliceAccess.Step == null);
                }
                if (sliceAccess.Step != null)
                {
                    _output.AppendLine($"{indent}{childPrefix}Step:");
                    DumpNode(sliceAccess.Step, depth + 2, true);
                }
                break;

            case FunctionCall funcCall:
                _output.AppendLine($"{indent}{prefix}FunctionCall @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Function:");
                DumpNode(funcCall.Function, depth + 2, false);
                if (funcCall.Arguments.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}Arguments: [{funcCall.Arguments.Length}]");
                    for (int i = 0; i < funcCall.Arguments.Length; i++)
                    {
                        DumpNode(funcCall.Arguments[i], depth + 2, i == funcCall.Arguments.Length - 1 && funcCall.KeywordArguments.Length == 0);
                    }
                }
                if (funcCall.KeywordArguments.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}KeywordArguments: [{funcCall.KeywordArguments.Length}]");
                    for (int i = 0; i < funcCall.KeywordArguments.Length; i++)
                    {
                        var kwarg = funcCall.KeywordArguments[i];
                        var kwIndent = new string(' ', (depth + 2) * IndentUnit.Length);
                        var kwPrefix = i == funcCall.KeywordArguments.Length - 1 ? "└─ " : "├─ ";
                        _output.AppendLine($"{kwIndent}{kwPrefix}{kwarg.Name} @ L{kwarg.LineStart}:C{kwarg.ColumnStart}:");
                        DumpNode(kwarg.Value, depth + 3, true);
                    }
                }
                break;

            case UnaryOp unaryOp:
                _output.AppendLine($"{indent}{prefix}UnaryOp: {unaryOp.Operator} @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Operand:");
                DumpNode(unaryOp.Operand, depth + 2, true);
                break;

            case BinaryOp binaryOp:
                _output.AppendLine($"{indent}{prefix}BinaryOp: {binaryOp.Operator} @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Left:");
                DumpNode(binaryOp.Left, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Right:");
                DumpNode(binaryOp.Right, depth + 2, true);
                break;

            case ComparisonChain compChain:
                _output.AppendLine($"{indent}{prefix}ComparisonChain @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Operands: [{compChain.Operands.Length}]");
                for (int i = 0; i < compChain.Operands.Length; i++)
                {
                    DumpNode(compChain.Operands[i], depth + 2, i == compChain.Operands.Length - 1);
                }
                _output.AppendLine($"{indent}{childPrefix}Operators: [{string.Join(", ", compChain.Operators)}]");
                break;

            case ConditionalExpression condExpr:
                _output.AppendLine($"{indent}{prefix}ConditionalExpression @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Test:");
                DumpNode(condExpr.Test, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}ThenValue:");
                DumpNode(condExpr.ThenValue, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}ElseValue:");
                DumpNode(condExpr.ElseValue, depth + 2, true);
                break;

            case LambdaExpression lambdaExpr:
                _output.AppendLine($"{indent}{prefix}LambdaExpression @ L{node.LineStart}:C{node.ColumnStart}");
                if (lambdaExpr.Parameters.Length > 0)
                {
                    _output.AppendLine($"{indent}{childPrefix}Parameters: [{lambdaExpr.Parameters.Length}]");
                    for (int i = 0; i < lambdaExpr.Parameters.Length; i++)
                    {
                        DumpParameter(lambdaExpr.Parameters[i], depth + 2, i == lambdaExpr.Parameters.Length - 1);
                    }
                }
                _output.AppendLine($"{indent}{childPrefix}Body:");
                DumpNode(lambdaExpr.Body, depth + 2, true);
                break;

            case TypeCast typeCast:
                _output.AppendLine($"{indent}{prefix}TypeCast @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Value:");
                DumpNode(typeCast.Value, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}TargetType:");
                DumpTypeAnnotation(typeCast.TargetType, depth + 2, true);
                break;

            case TypeCoercion typeCoercion:
                _output.AppendLine($"{indent}{prefix}TypeCoercion @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Value:");
                DumpNode(typeCoercion.Value, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}TargetType:");
                DumpTypeAnnotation(typeCoercion.TargetType, depth + 2, true);
                break;

            case TypeCheck typeCheck:
                _output.AppendLine($"{indent}{prefix}TypeCheck @ L{node.LineStart}:C{node.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Value:");
                DumpNode(typeCheck.Value, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}CheckType:");
                DumpTypeAnnotation(typeCheck.CheckType, depth + 2, true);
                break;

            case Parenthesized paren:
                _output.AppendLine($"{indent}{prefix}Parenthesized @ L{node.LineStart}:C{node.ColumnStart}");
                DumpNode(paren.Expression, depth + 1, true);
                break;

            default:
                _output.AppendLine($"{indent}{prefix}{node.GetType().Name} @ L{node.LineStart}:C{node.ColumnStart}");
                break;
        }
    }

    private void DumpParameter(Parameter param, int depth, bool isLast)
    {
        var indent = new string(' ', depth * IndentUnit.Length);
        var prefix = isLast ? "└─ " : "├─ ";
        var childPrefix = isLast ? "   " : "│  ";

        _output.Append($"{indent}{prefix}Parameter: {param.Name} @ L{param.LineStart}:C{param.ColumnStart}");
        if (param.Type != null)
        {
            _output.Append($" : {FormatType(param.Type)}");
        }
        if (param.DefaultValue != null)
        {
            _output.AppendLine(" =");
            DumpNode(param.DefaultValue, depth + 1, true);
        }
        else
        {
            _output.AppendLine();
        }
    }

    private void DumpTypeAnnotation(TypeAnnotation type, int depth, bool isLast)
    {
        var indent = new string(' ', depth * IndentUnit.Length);
        var prefix = isLast ? "└─ " : "├─ ";
        _output.AppendLine($"{indent}{prefix}{FormatType(type)} @ L{type.LineStart}:C{type.ColumnStart}");
    }

    private string FormatType(TypeAnnotation type)
    {
        var result = type.Name;
        if (type.TypeArguments.Length > 0)
        {
            var args = string.Join(", ", type.TypeArguments.Select(FormatType));
            result += $"[{args}]";
        }
        if (type.IsOptional)
        {
            result += "?";
        }
        return result;
    }

    private string EscapeString(string str)
    {
        return str
            .Replace("\\", "\\\\")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\"", "\\\"");
    }

    private void DumpComprehensionClause(ComprehensionClause clause, int depth, bool isLast)
    {
        var indent = new string(' ', depth * IndentUnit.Length);
        var prefix = isLast ? "└─ " : "├─ ";
        var childPrefix = isLast ? "   " : "│  ";

        switch (clause)
        {
            case ForClause forClause:
                _output.AppendLine($"{indent}{prefix}ForClause @ L{clause.LineStart}:C{clause.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Target:");
                DumpNode(forClause.Target, depth + 2, false);
                _output.AppendLine($"{indent}{childPrefix}Iterator:");
                DumpNode(forClause.Iterator, depth + 2, true);
                break;

            case IfClause ifClause:
                _output.AppendLine($"{indent}{prefix}IfClause @ L{clause.LineStart}:C{clause.ColumnStart}");
                _output.AppendLine($"{indent}{childPrefix}Condition:");
                DumpNode(ifClause.Condition, depth + 2, true);
                break;

            default:
                _output.AppendLine($"{indent}{prefix}{clause.GetType().Name} @ L{clause.LineStart}:C{clause.ColumnStart}");
                break;
        }
    }
}
