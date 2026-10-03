using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

internal sealed partial class UnparseVisitor
{
    #region Simple statements

    public override void VisitDecoratedStatement(DecoratedStatement node)
    {
        // Round-trip the @suppress decorator(s), then the inner statement (#1024).
        WriteDecorators(node.Decorators);
        Visit(node.Statement);
    }

    public override void VisitExpressionStatement(ExpressionStatement node)
    {
        Visit(node.Expression);
        _w.WriteLine();
    }

    public override void VisitAssignment(Assignment node)
    {
        if (node.IsLet)
            _w.Write("let ");
        Visit(node.Target);
        _w.Write(" ");
        _w.Write(AssignmentOperatorText(node.Operator));
        _w.Write(" ");
        Visit(node.Value);
        _w.WriteLine();
    }

    public override void VisitVariableDeclaration(VariableDeclaration node)
    {
        WriteDecorators(node.Decorators);
        if (node.IsConst)
            _w.Write("const ");
        if (node.IsLet)
            _w.Write("let ");
        WriteName(node.Name, node.IsNameBacktickEscaped);
        if (node.Type != null)
        {
            _w.Write(": ");
            WriteTypeAnnotation(node.Type);
        }
        if (node.InitialValue != null)
        {
            _w.Write(" = ");
            Visit(node.InitialValue);
        }
        _w.WriteLine();
    }

    public override void VisitAssertStatement(AssertStatement node)
    {
        _w.Write("assert ");
        Visit(node.Test);
        if (node.Message != null)
        {
            _w.Write(", ");
            Visit(node.Message);
        }
        _w.WriteLine();
    }

    public override void VisitPassStatement(PassStatement node)
    {
        _w.WriteLine("pass");
    }

    public override void VisitBreakStatement(BreakStatement node)
    {
        _w.WriteLine("break");
    }

    public override void VisitContinueStatement(ContinueStatement node)
    {
        _w.WriteLine("continue");
    }

    public override void VisitReturnStatement(ReturnStatement node)
    {
        if (node.Value != null)
        {
            _w.Write("return ");
            Visit(node.Value);
            _w.WriteLine();
        }
        else
        {
            _w.WriteLine("return");
        }
    }

    public override void VisitYieldStatement(YieldStatement node)
    {
        if (node.IsFrom)
        {
            _w.Write("yield from ");
        }
        else
        {
            _w.Write("yield ");
        }
        Visit(node.Value);
        _w.WriteLine();
    }

    public override void VisitRaiseStatement(RaiseStatement node)
    {
        _w.Write("raise");
        if (node.Exception != null)
        {
            _w.Write(" ");
            Visit(node.Exception);
            if (node.Cause != null)
            {
                _w.Write(" from ");
                Visit(node.Cause);
            }
        }
        _w.WriteLine();
    }

    #endregion

    #region Compound statements

    public override void VisitIfStatement(IfStatement node)
    {
        _w.Write("if ");
        Visit(node.Test);
        _w.Write(":");
        _w.WriteLine();
        WriteBody(node.ThenBody);
        foreach (var elif in node.ElifClauses)
        {
            WriteAnchored(elif.LineStart, elif.HeaderLineEnd, elif.Span?.Start ?? -1, elif.HeaderEndOffset, () =>
            {
                _w.Write("elif ");
                Visit(elif.Test);
                _w.Write(":");
                _w.WriteLine();
                WriteBody(elif.Body);
            });
        }
        WriteElseClause(node.ElseBody, node.ElseHeaderLine);
    }

    /// <summary>An <c>else:</c> clause (of <c>if</c>, a loop or <c>try</c>) at its anchor, the keyword's line.</summary>
    private void WriteElseClause(System.Collections.Immutable.ImmutableArray<Statement> body, int headerLine)
        => WriteKeywordClause("else:", body, headerLine);

    private void WriteKeywordClause(string header, System.Collections.Immutable.ImmutableArray<Statement> body, int headerLine)
    {
        if (body.IsEmpty)
            return;
        WriteAnchored(headerLine, headerLine, -1, -1, () =>
        {
            _w.Write(header);
            _w.WriteLine();
            WriteBody(body);
        });
    }

    public override void VisitWhileStatement(WhileStatement node)
    {
        _w.Write("while ");
        Visit(node.Test);
        _w.Write(":");
        _w.WriteLine();
        WriteBody(node.Body);
        WriteElseClause(node.ElseBody, node.ElseHeaderLine);
    }

    public override void VisitForStatement(ForStatement node)
    {
        if (node.IsAsync)
            _w.Write("async ");
        _w.Write("for ");
        Visit(node.Target);
        _w.Write(" in ");
        Visit(node.Iterator);
        _w.Write(":");
        _w.WriteLine();
        WriteBody(node.Body);
        WriteElseClause(node.ElseBody, node.ElseHeaderLine);
    }

    public override void VisitTryStatement(TryStatement node)
    {
        _w.Write("try:");
        _w.WriteLine();
        WriteBody(node.Body);
        foreach (var handler in node.Handlers)
            WriteAnchored(handler.LineStart, handler.HeaderLineEnd, handler.Span?.Start ?? -1, handler.HeaderEndOffset, () => WriteExceptHandler(handler));
        WriteElseClause(node.ElseBody, node.ElseHeaderLine);
        WriteKeywordClause("finally:", node.FinallyBody, node.FinallyHeaderLine);
    }

    /// <summary>
    /// An except clause's exception types. A list of two or more (<c>except A, B:</c> or
    /// <c>except (A, B):</c>, which parse to the same tuple annotation) is written as Python spells
    /// it, <c>(A, B)</c> — never <c>tuple[A, B]</c>, a spelling no user writes in an except clause,
    /// which also re-spelled the token a header comment follows (P22b, lead ruling). An escaped,
    /// optional or named-element tuple keeps the annotation spelling.
    /// </summary>
    private void WriteExceptionTypes(TypeAnnotation type)
    {
        if (type is { Name: "tuple", IsOptional: false, IsNameBacktickEscaped: false } && type.TupleElementNames.IsEmpty
            && type.NameParts.Length <= 1 && type.TypeArguments.Length >= 2)
        {
            _w.Write("(");
            for (int i = 0; i < type.TypeArguments.Length; i++)
            {
                if (i > 0)
                    _w.Write(", ");
                WriteTypeAnnotation(type.TypeArguments[i]);
            }
            _w.Write(")");
            return;
        }
        WriteTypeAnnotation(type);
    }

    private void WriteExceptHandler(ExceptHandler handler)
    {
        if (handler.IsExceptStar)
            _w.Write("except*");
        else
            _w.Write("except");
        if (handler.ExceptionType != null)
        {
            _w.Write(" ");
            WriteExceptionTypes(handler.ExceptionType);
            if (handler.Name != null)
            {
                _w.Write(" as ");
                WriteName(handler.Name, handler.IsNameBacktickEscaped);
            }
        }
        if (handler.Filter != null)
        {
            _w.Write(" when ");
            Visit(handler.Filter);
        }
        _w.Write(":");
        _w.WriteLine();
        WriteBody(handler.Body);
    }

    public override void VisitWithStatement(WithStatement node)
    {
        if (node.IsAsync)
            _w.Write("async ");
        _w.Write("with ");
        for (int i = 0; i < node.Items.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");
            var item = node.Items[i];
            Visit(item.ContextExpression);
            if (item.Target != null)
            {
                _w.Write(" as ");
                Visit(item.Target);
            }
        }
        _w.Write(":");
        _w.WriteLine();
        WriteBody(node.Body);
    }

    public override void VisitDeferStatement(DeferStatement node)
    {
        // Inline form (`defer f.close()`) for a single-statement body; block form
        // (`defer:` + indented suite) otherwise. IsBlock is the authoritative signal set
        // by the parser, so a block-form defer with one statement still round-trips as a
        // block. (#1075)
        if (node.IsBlock)
        {
            _w.Write("defer:");
            _w.WriteLine();
            WriteBody(node.Body);
        }
        else
        {
            _w.Write("defer ");
            // The inline body is a single simple statement; its visitor writes the
            // trailing newline.
            Visit(node.Body[0]);
        }
    }

    #endregion

    #region Definitions

    /// <summary>A definition's docstring, one indent under its header, at its own anchor.</summary>
    private void WriteDocString(string? docString, Statement owner)
    {
        if (docString == null)
            return;
        _w.Indent();
        WriteDocStringAnchored(owner.HeaderEndOffset, () =>
        {
            _w.Write("\"\"\"");
            _w.WriteOpaque(EscapeTripleQuoted(docString));
            _w.Write("\"\"\"");
            _w.WriteLine();
        });
        _w.Dedent();
    }

    public override void VisitFunctionDef(FunctionDef node)
    {
        WriteDecorators(node.Decorators);
        if (node.IsAsync)
            _w.Write("async ");
        _w.Write("def ");
        WriteName(node.Name, node.IsNameBacktickEscaped);
        WriteTypeParameters(node.TypeParameters);
        WriteParameterList(node.Parameters);
        if (node.ReturnType != null)
        {
            _w.Write(" -> ");
            WriteTypeAnnotation(node.ReturnType);
        }
        _w.Write(":");
        _w.WriteLine();
        WriteDocString(node.DocString, node);
        WriteBody(node.Body);
    }

    public override void VisitClassDef(ClassDef node)
    {
        WriteDecorators(node.Decorators);
        _w.Write("class ");
        WriteName(node.Name, node.IsNameBacktickEscaped);
        WriteTypeParameters(node.TypeParameters);
        if (!node.BaseClasses.IsEmpty)
        {
            _w.Write("(");
            for (int i = 0; i < node.BaseClasses.Length; i++)
            {
                if (i > 0)
                    _w.Write(", ");
                WriteTypeAnnotation(node.BaseClasses[i]);
            }
            _w.Write(")");
        }
        _w.Write(":");
        _w.WriteLine();
        WriteDocString(node.DocString, node);
        WriteBody(node.Body);
    }

    public override void VisitStructDef(StructDef node)
    {
        WriteDecorators(node.Decorators);
        _w.Write("struct ");
        WriteName(node.Name, node.IsNameBacktickEscaped);
        WriteTypeParameters(node.TypeParameters);
        if (!node.BaseClasses.IsEmpty)
        {
            _w.Write("(");
            for (int i = 0; i < node.BaseClasses.Length; i++)
            {
                if (i > 0)
                    _w.Write(", ");
                WriteTypeAnnotation(node.BaseClasses[i]);
            }
            _w.Write(")");
        }
        _w.Write(":");
        _w.WriteLine();
        WriteDocString(node.DocString, node);
        WriteBody(node.Body);
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        WriteDecorators(node.Decorators);
        _w.Write("interface ");
        WriteName(node.Name, node.IsNameBacktickEscaped);
        WriteTypeParameters(node.TypeParameters);
        if (!node.BaseInterfaces.IsEmpty)
        {
            _w.Write("(");
            for (int i = 0; i < node.BaseInterfaces.Length; i++)
            {
                if (i > 0)
                    _w.Write(", ");
                WriteTypeAnnotation(node.BaseInterfaces[i]);
            }
            _w.Write(")");
        }
        _w.Write(":");
        _w.WriteLine();
        WriteDocString(node.DocString, node);
        WriteBody(node.Body);
    }

    public override void VisitEnumDef(EnumDef node)
    {
        WriteDecorators(node.Decorators);
        _w.Write("enum ");
        WriteName(node.Name, node.IsNameBacktickEscaped);
        _w.Write(":");
        _w.WriteLine();
        WriteDocString(node.DocString, node);
        if (node.Members.IsEmpty)
        {
            _w.Indent();
            _w.WriteLine("pass");
            _w.Dedent();
        }
        else
        {
            _w.Indent();
            OpenBody(BodyColumn(node.Members[0].Span, node.Members[0].ColumnStart));
            foreach (var member in node.Members)
            {
                // A member line is a non-statement body line: an anchor like a statement's.
                WriteAnchored(member.LineStart, member.LineEnd, member.Span?.Start ?? -1, member.Span?.End ?? -1, () =>
                {
                    WriteName(member.Name, member.IsNameBacktickEscaped);
                    if (member.Value != null)
                    {
                        _w.Write(" = ");
                        Visit(member.Value);
                    }
                    _w.WriteLine();
                });
            }
            var lastMember = node.Members[node.Members.Length - 1];
            CloseBody(LastLineOf(lastMember.Span, lastMember.LineEnd));
            _w.Dedent();
        }
    }

    public override void VisitTypeAlias(TypeAlias node)
    {
        _w.Write("type ");
        WriteName(node.Name, node.IsNameBacktickEscaped);
        WriteTypeParameters(node.TypeParameters);
        if (node.FunctionType != null)
        {
            _w.Write(" = (");
            for (int i = 0; i < node.FunctionType.ParameterTypes.Length; i++)
            {
                if (i > 0)
                    _w.Write(", ");
                WriteTypeAnnotation(node.FunctionType.ParameterTypes[i]);
            }
            _w.Write(") -> ");
            WriteTypeAnnotation(node.FunctionType.ReturnType);
        }
        else if (node.Type != null)
        {
            _w.Write(" = ");
            WriteTypeAnnotation(node.Type);
        }
        _w.WriteLine();
    }

    public override void VisitPropertyDef(PropertyDef node)
    {
        WriteDecorators(node.Decorators);
        if (node.IsFunctionStyle)
        {
            _w.Write("property ");
            if (node.Accessor != PropertyAccessor.None)
            {
                _w.Write(node.Accessor switch
                {
                    PropertyAccessor.Get => "get",
                    PropertyAccessor.Set => "set",
                    PropertyAccessor.Init => "init",
                    _ => ""
                });
                _w.Write(" ");
            }
            if (node.ExplicitInterface != null)
            {
                WriteName(node.ExplicitInterface, node.IsExplicitInterfaceBacktickEscaped);
                _w.Write(".");
            }
            WriteName(node.Name, node.IsNameBacktickEscaped);
            WriteParameterList(node.Parameters);
            if (node.ReturnType != null)
            {
                _w.Write(" -> ");
                WriteTypeAnnotation(node.ReturnType);
            }
            _w.Write(":");
            _w.WriteLine();
            WriteBody(node.Body);
        }
        else
        {
            _w.Write("property ");
            if (node.Accessor != PropertyAccessor.None)
            {
                _w.Write(node.Accessor switch
                {
                    PropertyAccessor.Get => "get",
                    PropertyAccessor.Set => "set",
                    PropertyAccessor.Init => "init",
                    _ => ""
                });
                _w.Write(" ");
            }
            // The auto-property arm needs the qualifier as much as the function-style one: dropping
            // it turns an explicit interface implementation into a public property.
            if (node.ExplicitInterface != null)
            {
                WriteName(node.ExplicitInterface, node.IsExplicitInterfaceBacktickEscaped);
                _w.Write(".");
            }
            WriteName(node.Name, node.IsNameBacktickEscaped);
            if (node.Type != null)
            {
                _w.Write(": ");
                WriteTypeAnnotation(node.Type);
            }
            if (node.DefaultValue != null)
            {
                _w.Write(" = ");
                Visit(node.DefaultValue);
            }
            _w.WriteLine();

            if (!node.Observers.IsEmpty)
            {
                // Observer clauses are one indent level under the auto-property header; each
                // body is indented a further level by WriteBody.
                _w.Indent();
                OpenBody(BodyColumn(node.Observers[0].Span, node.Observers[0].ColumnStart));
                foreach (var observer in node.Observers)
                {
                    // An observer header is a non-statement body line: an anchor like a clause's.
                    WriteAnchored(observer.LineStart, observer.HeaderLineEnd, observer.Span?.Start ?? -1, observer.HeaderEndOffset, () =>
                    {
                        _w.Write(observer.Kind == ObserverKind.BeforeSet ? "before_set(" : "after_set(");
                        WriteName(observer.ParamName, observer.IsParamNameBacktickEscaped);
                        _w.Write("):");
                        _w.WriteLine();
                        WriteBody(observer.Body);
                    });
                }
                var lastObserver = node.Observers[node.Observers.Length - 1];
                CloseBody(LastLineOf(lastObserver.Span, lastObserver.LineEnd));
                _w.Dedent();
            }
        }
    }

    #endregion

    #region Imports

    public override void VisitImportStatement(ImportStatement node)
    {
        _w.Write("import ");
        for (int i = 0; i < node.Names.Length; i++)
        {
            if (i > 0)
                _w.Write(", ");
            WriteImportAlias(node.Names[i]);
        }
        _w.WriteLine();
    }

    private void WriteImportAlias(ImportAlias alias)
    {
        // NameParts is empty only on an alias not built by the parser; Name is then the spelling.
        if (alias.NameParts.IsDefaultOrEmpty)
            _w.Write(alias.Name);
        else
            WriteDottedName(alias.NameParts, alias.BacktickEscapedParts);
        if (alias.AsName != null)
        {
            _w.Write(" as ");
            WriteName(alias.AsName, alias.IsAsNameBacktickEscaped);
        }
    }

    public override void VisitFromImportStatement(FromImportStatement node)
    {
        _w.Write("from ");
        if (node.ModuleParts.IsDefaultOrEmpty)
        {
            // A dots-only relative module (`from . import x`), or a node not built by the parser.
            _w.Write(node.Module);
        }
        else
        {
            // Module = leading relative dots + the joined parts; write the dots, then each part
            // through WriteName so its escape survives.
            _w.Write(new string('.', node.Module.Length - node.Module.TrimStart('.').Length));
            WriteDottedName(node.ModuleParts, node.BacktickEscapedParts);
        }
        _w.Write(" import ");
        if (node.ImportAll)
        {
            _w.Write("*");
        }
        else
        {
            // The written spelling is kept (#2188, R-DS): parenthesized stays parenthesized. Like
            // every bracketed construct the unparser writes, the names go on one line; a comment
            // inside the parentheses makes the statement verbatim (WriteAnchored), keeping its layout.
            if (node.IsParenthesized)
                _w.Write("(");
            for (int i = 0; i < node.Names.Length; i++)
            {
                if (i > 0)
                    _w.Write(", ");
                WriteImportAlias(node.Names[i]);
            }
            if (node.IsParenthesized)
                _w.Write(")");
        }
        _w.WriteLine();
    }

    #endregion

    #region Future statements

    public override void VisitMatchStatement(MatchStatement node)
    {
        _w.Write("match ");
        Visit(node.Scrutinee);
        _w.Write(":");
        _w.WriteLine();
        _w.Indent();
        if (!node.Cases.IsEmpty)
            OpenBody(BodyColumn(node.Cases[0].Span, node.Cases[0].ColumnStart));
        foreach (var c in node.Cases)
            WriteAnchored(c.LineStart, c.HeaderLineEnd, c.Span?.Start ?? -1, c.HeaderEndOffset, () => WriteMatchCase(c));
        if (!node.Cases.IsEmpty)
        {
            var lastCase = node.Cases[node.Cases.Length - 1];
            CloseBody(LastLineOf(lastCase.Span, lastCase.LineEnd));
        }
        _w.Dedent();
    }

    private void WriteMatchCase(MatchCase c)
    {
        _w.Write("case ");
        var pattern = c.Pattern;
        Expression? guard = c.Guard;
        if (pattern is GuardPattern gp && guard == null)
        {
            pattern = gp.Inner;
            guard = gp.Guard;
        }
        Visit(pattern);
        if (guard != null)
        {
            _w.Write(" if ");
            Visit(guard);
        }
        _w.Write(":");
        _w.WriteLine();
        WriteBody(c.Body);
    }

    public override void VisitUnionDef(UnionDef node)
    {
        WriteDecorators(node.Decorators);
        _w.Write("union ");
        WriteName(node.Name, node.IsNameBacktickEscaped);
        WriteTypeParameters(node.TypeParameters);
        _w.Write(":");
        _w.WriteLine();
        WriteDocString(node.DocString, node);
        if (node.Cases.IsEmpty && node.Body.IsEmpty)
        {
            _w.Indent();
            _w.WriteLine("pass");
            _w.Dedent();
        }
        else
        {
            _w.Indent();
            OpenBody(!node.Cases.IsEmpty
                ? BodyColumn(node.Cases[0].Span, node.Cases[0].ColumnStart)
                : BodyColumn(node.Body[0]));
            foreach (var caseDef in node.Cases)
            {
                // A union case line is a non-statement body line: an anchor like a statement's.
                WriteAnchored(caseDef.LineStart, caseDef.LineEnd, caseDef.Span?.Start ?? -1, caseDef.Span?.End ?? -1, () => WriteUnionCase(caseDef));
            }
            // The union's methods (UnionDef.Body) follow its cases — the parser collects them
            // apart from the cases, so writing them here keeps the AST; dropping them deleted every
            // method (a `__str__` override silently fell back to the default repr).
            var fmt = _options.Formatting;
            for (int i = 0; i < node.Body.Length; i++)
            {
                if (fmt != null && (i > 0 || !node.Cases.IsEmpty))
                {
                    for (int b = 0; b < fmt.BlankLinesBetweenClassMembers; b++)
                        _w.WriteLine();
                }
                VisitStatementWithTrivia(node.Body[i]);
            }
            var lastLine = Math.Max(
                node.Cases.IsEmpty ? 0 : LastLineOf(node.Cases[node.Cases.Length - 1].Span, node.Cases[node.Cases.Length - 1].LineEnd),
                node.Body.IsEmpty ? 0 : LastLineOf(node.Body[node.Body.Length - 1]));
            CloseBody(lastLine);
            _w.Dedent();
        }
    }

    private void WriteUnionCase(UnionCaseDef caseDef)
    {
        _w.Write("case ");
        WriteName(caseDef.Name, caseDef.IsNameBacktickEscaped);
        if (!caseDef.Fields.IsEmpty)
        {
            _w.Write("(");
            for (int i = 0; i < caseDef.Fields.Length; i++)
            {
                if (i > 0)
                    _w.Write(", ");
                WriteName(caseDef.Fields[i].Name, caseDef.Fields[i].IsNameBacktickEscaped);
                _w.Write(": ");
                WriteTypeAnnotation(caseDef.Fields[i].Type);
            }
            _w.Write(")");
        }
        _w.WriteLine();
    }

    public override void VisitDelegateDef(DelegateDef node)
    {
        _w.Write("delegate ");
        WriteName(node.Name, node.IsNameBacktickEscaped);
        WriteTypeParameters(node.TypeParameters);
        WriteParameterList(node.Parameters);
        if (node.ReturnType != null)
        {
            _w.Write(" -> ");
            WriteTypeAnnotation(node.ReturnType);
        }
        _w.WriteLine();
    }

    public override void VisitEventDef(EventDef node)
    {
        WriteDecorators(node.Decorators);
        if (node.IsFunctionStyle)
        {
            _w.Write("event ");
            if (node.Accessor != EventAccessor.None)
            {
                _w.Write(node.Accessor switch
                {
                    EventAccessor.Add => "add",
                    EventAccessor.Remove => "remove",
                    _ => ""
                });
                _w.Write(" ");
            }
            WriteName(node.Name, node.IsNameBacktickEscaped);
            WriteParameterList(node.Parameters);
            _w.Write(":");
            _w.WriteLine();
            WriteBody(node.Body);
        }
        else
        {
            _w.Write("event ");
            WriteName(node.Name, node.IsNameBacktickEscaped);
            if (node.Type != null)
            {
                _w.Write(": ");
                WriteTypeAnnotation(node.Type);
            }
            _w.WriteLine();
        }
    }

    #endregion
}
