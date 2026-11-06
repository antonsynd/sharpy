namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>The READ family (N1 / N2 / N3 / N4 / N7): D3.d's rule, keyed on kind classes and never on a cell.</summary>
public partial class NameResolutionDifferentialTests
{
    private static readonly BindingKind[] TypeLevelTargets =
    {
        BindingKind.StaticField, BindingKind.StaticProperty, BindingKind.StaticMethod,
        BindingKind.ClassLiteralConst, BindingKind.ClassComputedConst, BindingKind.NestedType,
    };

    private static readonly BindingKind[] InstanceTargets =
    {
        BindingKind.InstanceField, BindingKind.InstanceProperty, BindingKind.InstanceMethod,
    };

    /// <summary>The module-level values of READ's outer axis: the roster's generated values, each one left out named there. The
    /// Builtin and ImportedName outers carry their own name, so each has its own arm.</summary>
    private static readonly BindingKind[] ReadModuleOuters = NameResolutionRosters.ReadOuter
        .Where(e => e.Value.Generated && NameResolutionRosters.ModuleLevelKinds.Contains(e.Key))
        .Select(e => e.Key)
        .OrderBy(k => k)
        .ToArray();

    private static bool IsModuleLevel(BindingKind k) => k is >= BindingKind.ModuleVar and <= BindingKind.ModuleTypeAlias;
    private static bool IsInstance(BindingKind k) => InstanceTargets.Contains(k);
    private static bool IsLocalOuter(BindingKind k) => k is BindingKind.EnclosingLocal or BindingKind.Parameter or BindingKind.Inner;

    private static Form FormFor(BindingKind target, BindingKind outer, Spelling spelling)
    {
        // The imported outer is a str value read through len (D2.9), whatever the same-named member is: its one read text.
        if (outer == BindingKind.ImportedName)
            return Form.Value;
        if (spelling != Spelling.Bare || outer == BindingKind.None || IsLocalOuter(outer))
            return IsLocalOuter(outer) && spelling == Spelling.Bare ? Form.Value : FormOf(target);
        return (Form)Math.Max((int)FormOf(target), (int)FormOf(outer));
    }

    private sealed record Expected(SharpyTag Tag, string? Model, PyOutcome Python, string Rule, string? Deviation = null);

    private static Expected Run(PyOutcome python, int value, string rule) => new(SharpyTag.MustRun, Str(value), python, rule);
    private static Expected Run(int value, string rule) => Run(PyOutcome.Ran(value), value, rule);
    private static Expected Refuse(PyOutcome python, string rule) => new(SharpyTag.MustRefuse, null, python, rule);
    private static Expected Cycle(PyOutcome python, string rule) => new(SharpyTag.MustRefuseOrThrow, null, python, rule);
    private static string Str(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>D3.d for a type-level or instance target. Python's prediction comes from python's own scoping: class-body
    /// names are visible only at a class-body position, at load time, once bound.</summary>
    private static Expected ExpectClass(
        BindingKind t, ReadPosition p, Nesting nesting, Order order, Spelling s, BindingKind outer, ReadName n, Form form)
    {
        bool classVisible = IsInitializer(p) && (nesting == Nesting.Direct || nesting == Nesting.ComprehensionIterable);
        bool atLoad = IsInitializer(p) && nesting != Nesting.Lambda;
        int member = BindingValue(t, n);

        bool imported = outer == BindingKind.ImportedName;

        // `len(digits)` over the same-named member: `len(7)`, or `len` of a function. Both are python's TypeError.
        // A method's name in a class body is a plain function: calling it lacks self (TypeError), and `.RED` on it is an AttributeError.
        PyOutcome MemberPy() => imported || (t == BindingKind.InstanceMethod && form != Form.EnumValue)
            ? PyOutcome.Other("TypeError")
            : t == BindingKind.InstanceMethod ? PyOutcome.Other("AttributeError") : EvalPython(t, form, member);

        if (s != Spelling.Bare)
        {
            var name = s switch { Spelling.Alias => "A", _ => "H" };
            PyOutcome python;
            if (s == Spelling.SelfDot)
                python = EvalPython(t, form, member);
            else if (atLoad)
                python = PyOutcome.Name(name);
            else
                python = MemberPy();

            if (s == Spelling.SelfDot)
                return Run(python, member, "N7");
            if (IsInstance(t))
                return Refuse(python, "kind:SPY0290");
            if (order is Order.Self or Order.Cycle)
                return Cycle(python, "N3-cycle");
            if (IsConstantOnly(p) && (t != BindingKind.ClassLiteralConst || s != Spelling.Qualified))
                return Refuse(python, "SPY0401");
            return Run(python, member, "N7");
        }

        PyOutcome OuterPy()
        {
            if (outer == BindingKind.None)
                return PyOutcome.Name(n.Name);
            if (outer is BindingKind.Builtin or BindingKind.ImportedName)
                return PyOutcome.Ran(n.BuiltinResult!.Value);
            return EvalPython(outer, form, ValueOf(outer));
        }

        if (IsLocalOuter(outer))
            return Run(ValueOf(outer), "N1-innermost");

        if (!classVisible)
        {
            if (outer == BindingKind.None)
                return Refuse(PyOutcome.Name(n.Name), "N1-methods");
            var py = OuterPy();
            if (outer is BindingKind.Builtin or BindingKind.ImportedName)
                return Run(n.BuiltinResult!.Value, "R-Y");
            return py.Kind == PyKind.Ran
                ? Run(ValueOf(outer), "R-Y")
                : Refuse(py, "N1-type-error");
        }

        var visible = order == Order.Earlier ? MemberPy() : OuterPy();

        if (IsInstance(t))
            return Refuse(visible, "kind:instance-at-initializer");
        if (IsConstantOnly(p) && t != BindingKind.ClassLiteralConst)
            return Refuse(visible, "SPY0401");

        if (outer == BindingKind.None)
        {
            return order switch
            {
                Order.Earlier => Run(member, "N1"),
                Order.Later => Run(visible, member, "N1+N3"),
                _ => Cycle(visible, "N3-cycle"),
            };
        }

        if (order != Order.Earlier)
            return Refuse(visible, "N2");
        return !imported && FormOf(t) == form ? Run(member, "N1") : Refuse(visible, "N1-type-error");
    }

    private static NameCell ClassCell(
        BindingKind t, ReadPosition p, Nesting nesting, Order order, Spelling s, BindingKind outer, ReadName n)
    {
        var form = FormFor(t, outer, s);
        var call = n;
        string prefix = s switch
        {
            Spelling.Qualified => "H.",
            Spelling.Alias => "A.",
            Spelling.SelfDot => "self.",
            _ => "",
        };
        // A method read needs its arguments, a nested type its constructor; the read text is the one form.
        var e = s == Spelling.Call ? "get()" : outer == BindingKind.ImportedName ? ImportedReadText : ReadText(form, prefix, call);
        var helper = s == Spelling.Call
            ? new List<string> { "def get() -> int:", $"    return {ReadText(form, "H.", call)}" }
            : new List<string>();

        var placed = PlaceInClass(p, nesting, e);
        var members = new List<string>();
        var targetDecl = MemberDecl(t, n);
        switch (order)
        {
            case Order.Earlier:
                members.AddRange(targetDecl);
                members.AddRange(placed.Members);
                break;
            case Order.Later:
                members.AddRange(placed.Members);
                members.AddRange(targetDecl);
                break;
            case Order.Self:
                members.AddRange(MemberDecl(t, n, e));
                placed = placed with { Access = IsInstance(t) ? $"H().{n.Name}" : $"H.{n.Name}" };
                break;
            case Order.Cycle:
                members.AddRange(placed.Members);
                members.AddRange(MemberDecl(t, n, "r"));
                break;
        }

        var lines = new List<string>();
        if (outer is not (BindingKind.None or BindingKind.Builtin or BindingKind.ImportedName) && !IsLocalOuter(outer))
            lines.AddRange(ModuleDecl(outer, n));
        lines.AddRange(helper);
        lines.Add(HostHeader(p));
        lines.AddRange(Indent(members));
        if (s == Spelling.Alias)
            lines.Add("type A = H");
        var source = WithHelpers(Join(lines) + MainPrinting(placed.Access, placed.Setup));

        var expected = ExpectClass(t, p, nesting, order, s, outer, n, form);
        return new NameCell
        {
            Id = CellId("read", t, p, nesting == Nesting.Direct ? null : nesting, order, s, $"outer={outer}", n.IsBuiltin ? n.Name : null),
            Family = Family.Read,
            Source = source,
            Tag = expected.Tag,
            Model = expected.Model,
            Python = expected.Python,
            Rule = expected.Rule,
            Target = t,
            Outer = outer,
            Position = p,
        };
    }

    /// <summary>The innermost-binding arm: a parameter, an enclosing local or a comprehension / lambda variable of the
    /// member's name, inside a method-like body.</summary>
    private static NameCell ClassLocalOuterCell(BindingKind t, ReadPosition p, Order order, BindingKind outer)
    {
        var n = ReadName.Plain;
        Placed placed = (outer, p) switch
        {
            (BindingKind.Parameter, ReadPosition.MethodBody) => new(new[] { "def r(self, x: int) -> int:", "    return x" }, "H().r(21)"),
            (BindingKind.Parameter, ReadPosition.StaticMethodBody) => new(new[] { "@static", "def r(x: int) -> int:", "    return x" }, "H.r(21)"),
            (BindingKind.Parameter, ReadPosition.InitBody) => new(new[] { "r: int = 0", "def __init__(self, x: int) -> None:", "    self.r = x" }, "H(21).r"),
            (BindingKind.EnclosingLocal, ReadPosition.NestedDefInMethod) => new(new[] { "def r(self) -> int:", "    x: int = 13", "    def g() -> int:", "        return x", "    return g()" }, "H().r()"),
            (BindingKind.EnclosingLocal, ReadPosition.LambdaStored) => new(new[] { "def r(self) -> int:", "    x: int = 13", "    f: () -> int = lambda: x", "    return f()" }, "H().r()"),
            (BindingKind.Inner, ReadPosition.ComprehensionElement) => new(new[] { "def r(self) -> int:", "    return [x for x in [22]][0]" }, "H().r()"),
            (BindingKind.Inner, ReadPosition.LambdaImmediate) => new(new[] { "def r(self) -> int:", "    return (lambda x: int: x)(22)" }, "H().r()"),
            (BindingKind.Inner, ReadPosition.StaticInitializer) => new(new[] { "@static", "r: list[int] = [x for x in [22]]" }, "H.r[0]"),
            _ => throw new ArgumentOutOfRangeException(nameof(p), $"{outer} at {p}"),
        };
        var members = order == Order.Earlier
            ? MemberDecl(t, n).Concat(placed.Members)
            : placed.Members.Concat(MemberDecl(t, n));
        var source = WithHelpers(Join(new[] { "class H:" }.Concat(Indent(members))) + MainPrinting(placed.Access, placed.Setup));
        var value = ValueOf(outer);
        return new NameCell
        {
            Id = CellId("read", t, p, order, Spelling.Bare, $"outer={outer}"),
            Family = Family.Read,
            Source = source,
            Tag = SharpyTag.MustRun,
            Model = Str(value),
            Python = PyOutcome.Ran(value),
            Rule = "N1-innermost",
            Target = t,
            Outer = outer,
            Position = p,
        };
    }

    private static IEnumerable<NameCell> ReadClassCells()
    {
        var targets = TypeLevelTargets.Concat(InstanceTargets).ToArray();
        var earlierLater = new[] { Order.Earlier, Order.Later };
        var bareOuters = new[] { BindingKind.None }.Concat(ReadModuleOuters).ToArray();

        foreach (var t in targets)
        {
            // Bare × every class-body position × the whole module-level outer axis.
            foreach (var p in ClassBodyInitializers)
                foreach (var o in earlierLater)
                    foreach (var u in bareOuters)
                        yield return ClassCell(t, p, Nesting.Direct, o, Spelling.Bare, u, ReadName.Plain);

            // The initializer nestings ride the static initializer: iterable and element differ under python's rule.
            foreach (var nesting in new[] { Nesting.ComprehensionIterable, Nesting.ComprehensionElement, Nesting.Lambda })
                foreach (var o in earlierLater)
                    foreach (var u in new[] { BindingKind.None, BindingKind.ModuleVar, BindingKind.ModuleDef })
                        yield return ClassCell(t, ReadPosition.StaticInitializer, nesting, o, Spelling.Bare, u, ReadName.Plain);

            // Bare × body positions: R-Y gives one answer, so the module-level axis runs on the method body only
            // (D3.e's second pruning step) and the others take None + ModuleVar.
            foreach (var p in ClassBodies)
                foreach (var o in earlierLater)
                    foreach (var u in p == ReadPosition.MethodBody ? bareOuters : new[] { BindingKind.None, BindingKind.ModuleVar })
                        yield return ClassCell(t, p, Nesting.Direct, o, Spelling.Bare, u, ReadName.Plain);

            // The spellings. N7: the same read gets the same answer, so the outer axis is the None + ModuleVar control.
            foreach (var p in ClassBodyInitializers)
                foreach (var o in earlierLater)
                    foreach (var u in new[] { BindingKind.None, BindingKind.ModuleVar })
                        yield return ClassCell(t, p, Nesting.Direct, o, Spelling.Qualified, u, ReadName.Plain);
            foreach (var nesting in new[] { Nesting.ComprehensionElement, Nesting.Lambda })
                foreach (var o in earlierLater)
                    yield return ClassCell(t, ReadPosition.StaticInitializer, nesting, o, Spelling.Qualified, BindingKind.None, ReadName.Plain);
            foreach (var p in ClassBodies)
                foreach (var o in earlierLater)
                {
                    yield return ClassCell(t, p, Nesting.Direct, o, Spelling.Qualified, BindingKind.None, ReadName.Plain);
                    if (IsSelfBearing(p))
                        yield return ClassCell(t, p, Nesting.Direct, o, Spelling.SelfDot, BindingKind.None, ReadName.Plain);
                }
            foreach (var p in new[] { ReadPosition.StaticInitializer, ReadPosition.InstanceFieldInitializer, ReadPosition.ConstInitializer, ReadPosition.MethodBody })
                foreach (var o in earlierLater)
                {
                    yield return ClassCell(t, p, Nesting.Direct, o, Spelling.Alias, BindingKind.None, ReadName.Plain);
                    yield return ClassCell(t, p, Nesting.Direct, o, Spelling.Call, BindingKind.None, ReadName.Plain);
                }

            // A function body reads the class qualified at both orders (A4 and the green control).
            foreach (var o in earlierLater)
                foreach (var s in new[] { Spelling.Qualified, Spelling.Alias })
                    yield return QualifiedFromFunctionCell(t, o, s);

            // Self and cycle exist for the value kinds with an initializer of their own.
            var selfPosition = t switch
            {
                BindingKind.StaticField => ReadPosition.StaticInitializer,
                BindingKind.StaticProperty => ReadPosition.StaticPropertyDefault,
                BindingKind.ClassLiteralConst or BindingKind.ClassComputedConst => ReadPosition.ConstInitializer,
                BindingKind.InstanceField => ReadPosition.InstanceFieldInitializer,
                BindingKind.InstanceProperty => ReadPosition.InstancePropertyDefault,
                _ => (ReadPosition?)null,
            };
            if (selfPosition is ReadPosition position)
            {
                foreach (var u in bareOuters)
                    yield return ClassCell(t, position, Nesting.Direct, Order.Self, Spelling.Bare, u, ReadName.Plain);
                yield return ClassCell(t, position, Nesting.Direct, Order.Self, Spelling.Qualified, BindingKind.None, ReadName.Plain);
                yield return ClassCell(t, position, Nesting.Direct, Order.Self, Spelling.Alias, BindingKind.None, ReadName.Plain);
                if (t is not (BindingKind.ClassLiteralConst or BindingKind.InstanceField or BindingKind.InstanceProperty))
                {
                    foreach (var u in new[] { BindingKind.None, BindingKind.ModuleVar })
                        yield return ClassCell(t, position, Nesting.Direct, Order.Cycle, Spelling.Bare, u, ReadName.Plain);
                    yield return ClassCell(t, position, Nesting.Direct, Order.Cycle, Spelling.Alias, BindingKind.None, ReadName.Plain);
                }
            }

            // Innermost: a parameter, an enclosing local or an inner variable of the name.
            foreach (var o in earlierLater)
            {
                foreach (var p in new[] { ReadPosition.MethodBody, ReadPosition.StaticMethodBody, ReadPosition.InitBody })
                    yield return ClassLocalOuterCell(t, p, o, BindingKind.Parameter);
                foreach (var p in new[] { ReadPosition.NestedDefInMethod, ReadPosition.LambdaStored })
                    yield return ClassLocalOuterCell(t, p, o, BindingKind.EnclosingLocal);
                foreach (var p in new[] { ReadPosition.ComprehensionElement, ReadPosition.LambdaImmediate, ReadPosition.StaticInitializer })
                    yield return ClassLocalOuterCell(t, p, o, BindingKind.Inner);
            }

            // The Builtin outer value: type-level targets at the N2 positions × order, and ONE body position.
            if (t == BindingKind.NestedType)
                continue;
            var builtinPositions = new[]
            {
                ReadPosition.StaticInitializer, ReadPosition.InstanceFieldInitializer, ReadPosition.ConstInitializer,
                ReadPosition.StaticPropertyDefault, ReadPosition.InstancePropertyDefault, ReadPosition.ParameterDefault,
                ReadPosition.MethodBody,
            };
            foreach (var n in Builtins.Select(b => new ReadName(b.Name, b.Args, b.Params, b.Result)))
            {
                foreach (var p in builtinPositions)
                    foreach (var o in earlierLater)
                        yield return ClassCell(t, p, Nesting.Direct, o, Spelling.Bare, BindingKind.Builtin, n);
                if (selfPosition is ReadPosition self)
                    yield return ClassCell(t, self, Nesting.Direct, Order.Self, Spelling.Bare, BindingKind.Builtin, n);
            }
        }
    }

    private static NameCell QualifiedFromFunctionCell(BindingKind t, Order order, Spelling s)
    {
        var n = ReadName.Plain;
        var form = FormOf(t);
        var prefix = s == Spelling.Alias ? "A." : "H.";
        var reader = new List<string> { "def r() -> int:", $"    return {ReadText(form, prefix, n)}" };
        var cls = new List<string> { "class H:" };
        cls.AddRange(Indent(MemberDecl(t, n)));
        cls.Add("type A = H");
        var body = order == Order.Earlier ? cls.Concat(reader) : reader.Concat(cls);
        var source = WithHelpers(Join(body) + MainBlock("print(r())"));
        int value = ValueOf(t);
        var python = t == BindingKind.InstanceMethod ? PyOutcome.Other("TypeError") : EvalPython(t, form, value);
        var expected = IsInstance(t) ? Refuse(python, "kind:SPY0290") : Run(python, value, "N7");
        return new NameCell
        {
            Id = CellId("read", t, ReadPosition.FunctionBody, order, s, "outer=None"),
            Family = Family.Read,
            Source = source,
            Tag = expected.Tag,
            Model = expected.Model,
            Python = expected.Python,
            Rule = expected.Rule,
            Target = t,
            Outer = BindingKind.None,
            Position = ReadPosition.FunctionBody,
        };
    }

    // ------------------------------------------------------------------------------------------- //
    // Module targets
    // ------------------------------------------------------------------------------------------- //

    private static readonly BindingKind[] ModuleTargets =
    {
        BindingKind.ModuleVar, BindingKind.ModuleLet, BindingKind.ModuleLiteralConst, BindingKind.ModuleComputedConst,
        BindingKind.ModuleProperty, BindingKind.ModuleDef, BindingKind.ModuleClass, BindingKind.ModuleStruct,
        BindingKind.ModuleEnum, BindingKind.ModuleTypeAlias,
    };

    private static readonly ReadPosition[] ModuleHostPositions =
    {
        ReadPosition.FunctionBody, ReadPosition.NestedDefInFunction, ReadPosition.GeneratorBody, ReadPosition.LambdaStored,
        ReadPosition.LambdaImmediate, ReadPosition.LambdaPassed, ReadPosition.ComprehensionElement, ReadPosition.ComprehensionIterable,
        ReadPosition.ModulePropertyInitializer, ReadPosition.ModuleAccessorBody, ReadPosition.EnumMemberValue,
    };

    private static readonly ReadPosition[] ModuleTargetClassPositions =
    {
        ReadPosition.StaticInitializer, ReadPosition.InstanceFieldInitializer, ReadPosition.DataclassFieldInitializer,
        ReadPosition.StructFieldInitializer, ReadPosition.ConstInitializer, ReadPosition.StaticPropertyDefault,
        ReadPosition.InstancePropertyDefault, ReadPosition.ParameterDefault, ReadPosition.MethodBody, ReadPosition.StaticMethodBody,
        ReadPosition.InitBody, ReadPosition.AccessorGetBody, ReadPosition.AccessorSetBody, ReadPosition.NestedClassMethodBody,
        ReadPosition.NestedDefInMethod, ReadPosition.LambdaStored, ReadPosition.ComprehensionElement,
    };

    /// <summary>A position whose read runs while the module loads, so a later binding is python's order-only NameError.</summary>
    private static bool ModuleReadAtLoad(ReadPosition p, Nesting nesting) => nesting != Nesting.Lambda && (
        p is ReadPosition.ModuleInitializer or ReadPosition.ModulePropertyInitializer or ReadPosition.EnumMemberValue
        || IsInitializer(p));

    private static bool IsCompileTimeModule(BindingKind t) => t is BindingKind.ModuleLiteralConst;

    private static NameCell ModuleCell(BindingKind t, ReadPosition p, Nesting nesting, Order order, Spelling s)
    {
        var n = ReadName.Plain;
        var form = FormOf(t);
        var read = ReadText(form, "", n);
        var e = s == Spelling.Call ? "get()" : read;
        var helper = s == Spelling.Call
            ? new List<string> { "def get() -> int:", $"    return {read}" }
            : new List<string>();

        string Build(ReadPosition position)
        {
            List<string> reader;
            string access;
            string[]? setup = null;
            if (ClassBodies.Contains(position) && position is not (ReadPosition.GeneratorBody or ReadPosition.LambdaImmediate or ReadPosition.LambdaPassed or ReadPosition.ComprehensionIterable)
                && position != ReadPosition.LambdaStored && position != ReadPosition.ComprehensionElement
                || IsInitializer(position))
            {
                var placed = PlaceInClass(position, nesting, e);
                reader = new List<string>(HostHeader(position).Split('\n'));
                reader.AddRange(Indent(placed.Members));
                access = placed.Access;
                setup = placed.Setup;
            }
            else
            {
                (reader, access) = PlaceAtModule(position, nesting, e);
            }

            var target = order is Order.Self
                ? ModuleDecl(t, n, read)
                : order is Order.Cycle
                    ? ModuleDecl(t, n, "r")
                    : ModuleDecl(t, n);
            var lines = new List<string>(helper);
            if (order is Order.Self)
                lines.AddRange(target);
            else if (order == Order.Earlier)
                lines.AddRange(target.Concat(reader));
            else
                lines.AddRange(reader.Concat(target));
            if (order is Order.Self)
                access = n.Name;
            return WithHelpers(Join(lines) + MainPrinting(access, setup));
        }

        var source = Build(p);
        int value = ValueOf(t);
        bool atLoad = ModuleReadAtLoad(p, nesting);

        Expected expected;
        var pyValue = EvalPython(t, form, value);
        if (order is Order.Self or Order.Cycle)
            expected = Cycle(PyOutcome.Name(n.Name), "N3-cycle");
        else
        {
            var python = atLoad && order == Order.Later ? PyOutcome.Name(n.Name) : pyValue;
            var rule = atLoad && order == Order.Later ? "N3" : "N1";
            if (p == ReadPosition.ModuleInitializer && nesting != Nesting.Direct && nesting != Nesting.Lambda)
                rule = "N1-nesting";
            if (t == BindingKind.ModuleLet)
                expected = new Expected(SharpyTag.MayRefuse, Str(value), python, "kind:SPY0340");
            else if ((IsConstantOnly(p) || p == ReadPosition.EnumMemberValue) && !IsCompileTimeModule(t))
                expected = Refuse(python, p == ReadPosition.EnumMemberValue ? "kind:enum-member-constant" : "SPY0401");
            else
                expected = Run(python, value, rule);
        }

        return new NameCell
        {
            Id = CellId("read", t, p, nesting == Nesting.Direct ? null : nesting, order, s, "outer=None"),
            Family = Family.Read,
            Source = source,
            TwinSource = p == ReadPosition.ModuleAccessorBody ? Build(ReadPosition.FunctionBody).Replace("print(r)", "print(r())", StringComparison.Ordinal) : null,
            Tag = expected.Tag,
            Model = expected.Model,
            Python = expected.Python,
            Rule = expected.Rule,
            Target = t,
            Outer = BindingKind.None,
            Position = p,
        };
    }

    private static NameCell ModuleLocalOuterCell(BindingKind t, ReadPosition p, BindingKind outer)
    {
        var (reader, access) = (outer, p) switch
        {
            (BindingKind.EnclosingLocal, ReadPosition.NestedDefInFunction) => (new List<string> { "def r() -> int:", "    x: int = 13", "    def g() -> int:", "        return x", "    return g()" }, "r()"),
            (BindingKind.EnclosingLocal, ReadPosition.LambdaStored) => (new List<string> { "def r() -> int:", "    x: int = 13", "    f: () -> int = lambda: x", "    return f()" }, "r()"),
            (BindingKind.Parameter, ReadPosition.FunctionBody) => (new List<string> { "def r(x: int) -> int:", "    return x" }, "r(21)"),
            (BindingKind.Inner, ReadPosition.ComprehensionElement) => (new List<string> { "def r() -> int:", "    return [x for x in [22]][0]" }, "r()"),
            (BindingKind.Inner, ReadPosition.LambdaImmediate) => (new List<string> { "def r() -> int:", "    return (lambda x: int: x)(22)" }, "r()"),
            _ => throw new ArgumentOutOfRangeException(nameof(p)),
        };
        var source = WithHelpers(Join(ModuleDecl(t, ReadName.Plain).Concat(reader)) + MainBlock($"print({access})"));
        var value = ValueOf(outer);
        return new NameCell
        {
            Id = CellId("read", t, p, Order.Earlier, Spelling.Bare, $"outer={outer}"),
            Family = Family.Read,
            Source = source,
            Tag = t == BindingKind.ModuleLet ? SharpyTag.MayRefuse : SharpyTag.MustRun,
            Model = Str(value),
            Python = PyOutcome.Ran(value),
            Rule = t == BindingKind.ModuleLet ? "kind:SPY0340" : "N1-innermost",
            Target = t,
            Outer = outer,
            Position = p,
        };
    }

    private static IEnumerable<NameCell> ReadModuleCells()
    {
        var earlierLater = new[] { Order.Earlier, Order.Later };
        foreach (var t in ModuleTargets)
        {
            foreach (var o in earlierLater)
            {
                foreach (var nesting in new[] { Nesting.Direct, Nesting.ComprehensionIterable, Nesting.ComprehensionElement, Nesting.Lambda })
                    yield return ModuleCell(t, ReadPosition.ModuleInitializer, nesting, o, Spelling.Bare);
                foreach (var p in ModuleHostPositions)
                    yield return ModuleCell(t, p, Nesting.Direct, o, Spelling.Bare);
                foreach (var p in ModuleTargetClassPositions.Except(ModuleHostPositions))
                    yield return ModuleCell(t, p, Nesting.Direct, o, Spelling.Bare);
                foreach (var p in new[] { ReadPosition.ModuleInitializer, ReadPosition.FunctionBody, ReadPosition.StaticInitializer })
                    yield return ModuleCell(t, p, Nesting.Direct, o, Spelling.Call);
            }

            if (FormOf(t) == Form.Value)
            {
                yield return ModuleCell(t, ReadPosition.ModuleInitializer, Nesting.Direct, Order.Self, Spelling.Bare);
                if (t != BindingKind.ModuleLiteralConst)
                    yield return ModuleCell(t, ReadPosition.ModuleInitializer, Nesting.Direct, Order.Cycle, Spelling.Bare);
            }

            foreach (var (p, u) in new[]
            {
                (ReadPosition.NestedDefInFunction, BindingKind.EnclosingLocal),
                (ReadPosition.LambdaStored, BindingKind.EnclosingLocal),
                (ReadPosition.FunctionBody, BindingKind.Parameter),
                (ReadPosition.ComprehensionElement, BindingKind.Inner),
                (ReadPosition.LambdaImmediate, BindingKind.Inner),
            })
                yield return ModuleLocalOuterCell(t, p, u);
        }

        foreach (var cell in UnionAndInterfaceReadCells())
            yield return cell;
    }

    /// <summary>The value-type axis's second value, on the kinds whose int read at load prints a storage default. In <c>str</c> that
    /// default is a null the read dereferences, the one shape where a read before its value is ready throws at run time
    /// (D2's <c>RuntimeThrow</c>): the feeder of that class, which the int cells cannot reach.</summary>
    private static IEnumerable<NameCell> StrValueCells()
    {
        NameCell Cell(BindingKind t, ReadPosition p, string shape, Expected e, IEnumerable<string> lines, string print) => new()
        {
            Id = CellId("read", "str", t, p, shape),
            Family = Family.Read,
            Source = WithHelpers(Join(lines) + MainBlock(print)),
            Tag = e.Tag,
            Model = e.Model,
            Python = e.Python,
            Rule = e.Rule,
            Target = t,
            Outer = BindingKind.None,
            Position = p,
        };

        Expected Reads(string pythonName, string rule) => new(SharpyTag.MustRun, "2", PyOutcome.Name(pythonName), rule);

        var classKinds = new (BindingKind Kind, string[] Decl)[]
        {
            (BindingKind.StaticField, new[] { "    @static", "    x: str = \"ab\"" }),
            (BindingKind.StaticProperty, new[] { "    @static", "    property x: str = \"ab\"" }),
            (BindingKind.ClassComputedConst, new[] { "    const x: str = \"ab\".upper()" }),
        };
        foreach (var (kind, decl) in classKinds)
        {
            yield return Cell(kind, ReadPosition.StaticInitializer, "function", Reads("H", "N7"),
                new[] { "def get() -> int:", "    return len(H.x)", "class H:", "    @static", "    r: int = get()" }.Concat(decl), "print(H.r)");
            yield return Cell(kind, ReadPosition.StaticInitializer, "lambda", Reads("H", "N7"),
                new[] { "class H:", "    @static", "    r: int = (lambda: len(H.x))()" }.Concat(decl), "print(H.r)");
            yield return Cell(kind, ReadPosition.StaticInitializer, "deferred", Reads("H", "N7"),
                new[] { "def get() -> str:", "    return H.x", "class H:", "    @static", "    r: str = get()" }.Concat(decl), "print(len(H.r))");
        }

        var moduleKinds = new (BindingKind Kind, string Decl)[]
        {
            (BindingKind.ModuleComputedConst, "const x: str = \"ab\".upper()"),
            (BindingKind.ModuleProperty, "property x: str = \"ab\""),
        };
        foreach (var (kind, decl) in moduleKinds)
        {
            yield return Cell(kind, ReadPosition.ModuleInitializer, "direct", Reads("x", "N3"), new[] { "r: int = len(x)", decl }, "print(r)");
            yield return Cell(kind, ReadPosition.ModuleInitializer, "function", Reads("x", "N3"),
                new[] { "def get() -> int:", "    return len(x)", "r: int = get()", decl }, "print(r)");
            yield return Cell(kind, ReadPosition.ModuleInitializer, "lambda", Reads("x", "N3"), new[] { "r: int = (lambda: len(x))()", decl }, "print(r)");
            yield return Cell(kind, ReadPosition.ModuleInitializer, "deferred", Reads("x", "N3"),
                new[] { "def get() -> str:", "    return x", "r: str = get()", decl }, "print(len(r))");
        }

        // A cycle has no value to wait for, so its throw is agreement: MustRefuseOrThrow × RuntimeThrow's feeder.
        yield return Cell(BindingKind.StaticField, ReadPosition.StaticInitializer, "self", Cycle(PyOutcome.Name("H"), "N3-cycle"),
            new[] { "class H:", "    @static", "    r: str = (lambda: H.r.upper())()" }, "print(H.r)");
    }

    /// <summary>The two module kinds with no expression read: a union through its case constructor and a match, an
    /// interface through a parameter typed with it. Body positions only (D3.b).</summary>
    private static IEnumerable<NameCell> UnionAndInterfaceReadCells()
    {
        var bodies = new (ReadPosition P, Func<IReadOnlyList<string>, (List<string> Lines, string Access)> Host)[]
        {
            (ReadPosition.FunctionBody, b => (new List<string>(new[] { "def r() -> int:" }.Concat(Indent(b))), "r()")),
            (ReadPosition.NestedDefInFunction, b => (new List<string>(new[] { "def r() -> int:", "    def g() -> int:" }.Concat(Indent(b, 8)).Append("    return g()")), "r()")),
            (ReadPosition.MethodBody, b => (new List<string>(new[] { "class H:", "    def r(self) -> int:" }.Concat(Indent(b, 8))), "H().r()")),
        };
        var union = new (BindingKind T, string[] Decl, string[] Body, int Value)[]
        {
            (BindingKind.ModuleUnion,
                new[] { "union U:", "    case V(f: int)", "    case L()" },
                new[] { "match U.V(17):", "    case V(v):", "        return v", "    case _:", "        return 0" },
                17),
            (BindingKind.ModuleInterface,
                new[] { "interface I:", "    def m(self) -> int: ...", "class C(I):", "    def m(self) -> int:", "        return 18", "def via(i: I) -> int:", "    return i.m()" },
                new[] { "return via(C())" },
                18),
        };
        foreach (var (t, decl, body, value) in union)
            foreach (var (p, host) in bodies)
                foreach (var o in new[] { Order.Earlier, Order.Later })
                {
                    var (lines, access) = host(body);
                    var all = o == Order.Earlier ? decl.Concat(lines) : lines.Concat(decl);
                    yield return new NameCell
                    {
                        Id = CellId("read", t, p, o, Spelling.Bare, "outer=None"),
                        Family = Family.Read,
                        Source = Join(all) + MainBlock($"print({access})"),
                        Tag = SharpyTag.MustRun,
                        Model = Str(value),
                        Python = PyOutcome.Ran(value),
                        Rule = "N1",
                        Target = t,
                        Outer = BindingKind.None,
                        Position = p,
                    };
                }
    }

    // ------------------------------------------------------------------------------------------- //
    // A module binding spelled like a builtin
    // ------------------------------------------------------------------------------------------- //

    private static readonly ReadPosition[] BuiltinModuleBodies =
    {
        ReadPosition.FunctionBody, ReadPosition.LambdaStored, ReadPosition.NestedDefInFunction, ReadPosition.ComprehensionElement,
    };

    private static IEnumerable<NameCell> ReadBuiltinModuleCells()
    {
        var names = Builtins.Select(b => new ReadName(b.Name, b.Args, b.Params, b.Result)).ToArray();
        var kinds = new[] { BindingKind.ModuleVar, BindingKind.ModuleLiteralConst, BindingKind.ModuleProperty, BindingKind.ModuleDef };
        foreach (var n in names)
        {
            foreach (var t in kinds)
                foreach (var o in new[] { Order.Earlier, Order.Later })
                {
                    foreach (var p in BuiltinModuleBodies)
                        yield return BuiltinModuleCell(t, p, Nesting.Direct, o, n, Form.Call);
                    yield return BuiltinModuleCell(t, ReadPosition.MethodBody, Nesting.Direct, o, n, Form.Call);
                    yield return BuiltinModuleCell(t, ReadPosition.StaticInitializer, Nesting.Direct, o, n, Form.Call);
                    yield return BuiltinModuleCell(t, ReadPosition.ModuleInitializer, Nesting.Direct, o, n, Form.Call);
                }

            // The bare VALUE read: the module binding at every order from a body (a repr at load is not a name verdict).
            foreach (var t in new[] { BindingKind.ModuleVar, BindingKind.ModuleProperty, BindingKind.ModuleLiteralConst })
                foreach (var o in new[] { Order.Earlier, Order.Later })
                    yield return BuiltinModuleCell(t, ReadPosition.FunctionBody, Nesting.Direct, o, n, Form.Value);

            // The value printed directly: the argument position, a different route from `return abs` (CS8917 below its reader).
            foreach (var o in new[] { Order.Earlier, Order.Later })
            {
                var decl = $"{n.Name}: int = {BuiltinBindingValue}\n";
                var reader = $"def r() -> None:\n    print({n.Name})\n";
                yield return new NameCell
                {
                    Id = CellId("read", BindingKind.ModuleVar, ReadPosition.FunctionBody, o, "PrintedValue", "outer=Builtin", n.Name),
                    Family = Family.Read,
                    Source = (o == Order.Earlier ? decl + reader : reader + decl) + MainBlock("r()"),
                    Tag = SharpyTag.MustRun,
                    Model = Str(BuiltinBindingValue),
                    Python = PyOutcome.Ran(BuiltinBindingValue),
                    Rule = "N1",
                    Target = BindingKind.ModuleVar,
                    Outer = BindingKind.Builtin,
                    Position = ReadPosition.FunctionBody,
                };
            }

            // A module class spelled like the builtin: the constructor call × arity.
            foreach (var o in new[] { Order.Earlier, Order.Later })
                foreach (var arityMatches in new[] { false, true })
                    yield return BuiltinClassCell(n, o, arityMatches);
        }
    }

    private static NameCell BuiltinModuleCell(BindingKind t, ReadPosition p, Nesting nesting, Order order, ReadName n, Form form)
    {
        var e = ReadText(form, "", n);
        List<string> reader;
        string access;
        string[]? setup = null;
        if (p is ReadPosition.MethodBody or ReadPosition.StaticInitializer)
        {
            var placed = PlaceInClass(p, nesting, e);
            reader = new List<string> { "class H:" };
            reader.AddRange(Indent(placed.Members));
            access = placed.Access;
            setup = placed.Setup;
        }
        else
        {
            (reader, access) = PlaceAtModule(p, nesting, e);
        }
        var decl = ModuleDecl(t, n);
        var lines = order == Order.Earlier ? decl.Concat(reader) : reader.Concat(decl);
        var source = WithHelpers(Join(lines) + MainPrinting(access, setup));

        bool atLoad = ModuleReadAtLoad(p, nesting);
        Expected expected;
        if (form == Form.Value)
            expected = Run(BuiltinBindingValue, "N1");
        else if (t == BindingKind.ModuleDef)
        {
            expected = atLoad && order == Order.Later
                ? Refuse(PyOutcome.Ran(n.BuiltinResult!.Value), "N2-module")
                : Run(BuiltinBindingValue, "N1");
        }
        else
        {
            var python = atLoad && order == Order.Later ? PyOutcome.Ran(n.BuiltinResult!.Value) : PyOutcome.Other("TypeError");
            expected = Refuse(python, "N1-type-error");
        }

        return new NameCell
        {
            Id = CellId("read", t, p, order, form, "outer=Builtin", n.Name),
            Family = Family.Read,
            Source = source,
            Tag = expected.Tag,
            Model = expected.Model,
            Python = expected.Python,
            Rule = expected.Rule,
            Target = t,
            Outer = BindingKind.Builtin,
            Position = p,
        };
    }

    private static readonly string[] BuiltinTypeNames = { "int" };

    private static NameCell BuiltinClassCell(ReadName n, Order order, bool arityMatches)
    {
        var cls = new List<string> { $"class {n.Name}:", "    n0: int = 9" };
        if (arityMatches)
            cls.AddRange(new[] { $"    def __init__(self, {n.Params}) -> None:", "        pass" });
        var reader = new[] { "def r() -> int:", $"    return {n.Name}({n.CallArgs}).n0" };
        var lines = order == Order.Earlier ? cls.Concat(reader) : reader.Concat(cls);
        var python = arityMatches ? PyOutcome.Ran(9) : PyOutcome.Other("TypeError");
        // name_shadowing.md section 2: a type declaration may not take a builtin type's bare name, so this is SPY0212 at either arity.
        var expected = BuiltinTypeNames.Contains(n.Name)
            ? Refuse(python, "kind:SPY0212")
            : arityMatches ? Run(9, "N1") : Refuse(python, "N1-arity");
        return new NameCell
        {
            Id = CellId("read", BindingKind.ModuleClass, ReadPosition.FunctionBody, order, arityMatches ? "arity-match" : "arity-mismatch", "outer=Builtin", n.Name),
            Family = Family.Read,
            Source = Join(lines) + MainBlock("print(r())"),
            Tag = expected.Tag,
            Model = expected.Model,
            Python = expected.Python,
            Rule = expected.Rule,
            Target = BindingKind.ModuleClass,
            Outer = BindingKind.Builtin,
            Position = ReadPosition.FunctionBody,
        };
    }

    // ------------------------------------------------------------------------------------------- //
    // Declaring type: base, other, enclosing
    // ------------------------------------------------------------------------------------------- //

    /// <summary>A generic receiver, D3.f's `G[int].x` control: type arguments do not change what the member name means.</summary>
    private static IEnumerable<NameCell> GenericReceiverCells()
    {
        var kinds = new (BindingKind Kind, string[] Decl, string Read, int Value)[]
        {
            (BindingKind.StaticField, new[] { "x: int = 1" }, "G[int].x", 1),
            (BindingKind.StaticProperty, new[] { "property x: int = 4" }, "G[int].x", 4),
            (BindingKind.StaticMethod, new[] { "def x() -> int:", "    return 6" }, "G[int].x()", 6),
        };
        foreach (var (kind, decl, read, value) in kinds)
            foreach (var order in new[] { Order.Earlier, Order.Later })
            {
                var type = new List<string> { "class G[T]:", "    @static" };
                type.AddRange(Indent(decl));
                var main = MainBlock($"print({read})");
                yield return new NameCell
                {
                    Id = CellId("read", kind, ReadPosition.FunctionBody, order, "GenericReceiver", "outer=None"),
                    Family = Family.Read,
                    Source = order == Order.Earlier ? Join(type) + main : main + Join(type),
                    Tag = SharpyTag.MustRun,
                    Model = Str(value),
                    Python = PyOutcome.Ran(value),
                    Rule = "N7",
                    Target = kind,
                    Outer = BindingKind.None,
                    Position = ReadPosition.FunctionBody,
                };
            }
    }

    private static IEnumerable<NameCell> ReadDeclaringCells()
    {
        var n = ReadName.Plain;
        var positions = new[]
        {
            ReadPosition.StaticInitializer, ReadPosition.InstanceFieldInitializer, ReadPosition.ConstInitializer,
            ReadPosition.StaticPropertyDefault, ReadPosition.InstancePropertyDefault, ReadPosition.ParameterDefault,
            ReadPosition.MethodBody, ReadPosition.StaticMethodBody, ReadPosition.InitBody,
        };

        foreach (var t in TypeLevelTargets.Where(k => k != BindingKind.NestedType))
        {
            var form = FormOf(t);
            int value = ValueOf(t);

            // base × bare × outer {None, ModuleVar}, plus B.x / H.x / self.x.
            foreach (var p in positions)
            {
                foreach (var u in new[] { BindingKind.None, BindingKind.ModuleVar })
                {
                    var readForm = u == BindingKind.None ? form : (Form)Math.Max((int)form, (int)FormOf(u));
                    var e = ReadText(readForm, "", n);
                    var placed = PlaceInClass(p, Nesting.Direct, e);
                    var lines = new List<string>();
                    if (u != BindingKind.None)
                        lines.AddRange(ModuleDecl(u, n));
                    lines.Add("class B:");
                    lines.AddRange(Indent(MemberDecl(t, n)));
                    lines.Add("class H(B):");
                    lines.AddRange(Indent(placed.Members));
                    Expected expected;
                    if (u == BindingKind.None)
                    {
                        expected = Refuse(PyOutcome.Name(n.Name), "R1(b)");
                    }
                    else
                    {
                        // A3: the module is the name's scope; R1 (b) refuses only the outer-none arm.
                        var python = readForm == Form.Value ? PyOutcome.Ran(ValueOf(u)) : PyOutcome.Other("TypeError");
                        expected = python.Kind == PyKind.Ran && p != ReadPosition.ParameterDefault
                            ? Run(python, ValueOf(u), "A3")
                            : Refuse(python, p == ReadPosition.ParameterDefault ? "SPY0401" : "N1-type-error");
                    }
                    yield return new NameCell
                    {
                        Id = CellId("read", t, p, "base", Spelling.Bare, $"outer={u}"),
                        Family = Family.Read,
                        Source = WithHelpers(Join(lines) + MainPrinting(placed.Access, placed.Setup)),
                        Tag = expected.Tag,
                        Model = expected.Model,
                        Python = expected.Python,
                        Rule = expected.Rule,
                        Target = t,
                        Outer = u,
                        Position = p,
                    };
                }

                foreach (var (label, prefix) in new[] { ("B.x", "B."), ("H.x", "H."), ("self.x", "self.") })
                {
                    if (prefix == "self." && !IsSelfBearing(p))
                        continue;
                    var placed = PlaceInClass(p, Nesting.Direct, ReadText(form, prefix, n));
                    var lines = new List<string> { "class B:" };
                    lines.AddRange(Indent(MemberDecl(t, n)));
                    lines.Add("class H(B):");
                    lines.AddRange(Indent(placed.Members));
                    bool load = IsInitializer(p);
                    var python = prefix == "H." && load ? PyOutcome.Name("H") : PyOutcome.Ran(value);
                    var expected = IsConstantOnly(p) && (t != BindingKind.ClassLiteralConst || prefix != "B.")
                        ? Refuse(python, "SPY0401")
                        : Run(python, value, "N7");
                    yield return new NameCell
                    {
                        Id = CellId("read", t, p, "base", label),
                        Family = Family.Read,
                        Source = WithHelpers(Join(lines) + MainPrinting(placed.Access, placed.Setup)),
                        Tag = expected.Tag,
                        Model = expected.Model,
                        Python = expected.Python,
                        Rule = expected.Rule,
                        Target = t,
                        Outer = BindingKind.None,
                        Position = p,
                    };
                }
            }

            // other: an unrelated class read qualified × order.
            foreach (var p in new[] { ReadPosition.StaticInitializer, ReadPosition.InstanceFieldInitializer, ReadPosition.MethodBody })
                foreach (var o in new[] { Order.Earlier, Order.Later })
                {
                    var placed = PlaceInClass(p, Nesting.Direct, ReadText(form, "B.", n));
                    var b = new List<string> { "class B:" };
                    b.AddRange(Indent(MemberDecl(t, n)));
                    var h = new List<string> { "class H:" };
                    h.AddRange(Indent(placed.Members));
                    var lines = o == Order.Earlier ? b.Concat(h) : h.Concat(b);
                    var python = o == Order.Later && IsInitializer(p) ? PyOutcome.Name("B") : PyOutcome.Ran(value);
                    yield return new NameCell
                    {
                        Id = CellId("read", t, p, "other", o, "B.x"),
                        Family = Family.Read,
                        Source = WithHelpers(Join(lines) + MainPrinting(placed.Access, placed.Setup)),
                        Tag = SharpyTag.MustRun,
                        Model = Str(value),
                        Python = python,
                        Rule = "N1+N3",
                        Target = t,
                        Outer = BindingKind.None,
                        Position = p,
                    };
                }

            // enclosing: a nested class's initializer and method body reading the enclosing type (R-KG), with the nested
            // initializer's own outer-binding arm.
            foreach (var (label, inner, access, isInit) in new[]
            {
                ("static-init", new[] { "    @static", "    r: int = {E}" }, "H.I.r", true),
                ("field-init", new[] { "    r: int = {E}" }, "H.I().r", true),
                ("method", new[] { "    def r(self) -> int:", "        return {E}" }, "H.I().r()", false),
            })
            {
                foreach (var u in new[] { BindingKind.None, BindingKind.ModuleVar })
                {
                    var readForm = u == BindingKind.None ? form : (Form)Math.Max((int)form, (int)FormOf(u));
                    var e = ReadText(readForm, "", n);
                    var lines = new List<string>();
                    if (u != BindingKind.None)
                        lines.AddRange(ModuleDecl(u, n));
                    lines.Add("class H:");
                    lines.AddRange(Indent(MemberDecl(t, n)));
                    lines.Add("    class I:");
                    lines.AddRange(Indent(inner.Select(l => l.Replace("{E}", e, StringComparison.Ordinal))));
                    Expected expected;
                    if (u == BindingKind.None)
                        expected = Refuse(PyOutcome.Name(n.Name), "R-KG");
                    else if (readForm == Form.Value)
                        expected = Run(ValueOf(u), "N1");
                    else
                        expected = Refuse(PyOutcome.Other("TypeError"), "N1-type-error");
                    yield return new NameCell
                    {
                        Id = CellId("read", t, label, "enclosing", Spelling.Bare, $"outer={u}"),
                        Family = Family.Read,
                        Source = WithHelpers(Join(lines) + MainBlock($"print({access})")),
                        Tag = expected.Tag,
                        Model = expected.Model,
                        Python = expected.Python,
                        Rule = expected.Rule,
                        Target = t,
                        Outer = u,
                        Position = isInit ? ReadPosition.StaticInitializer : ReadPosition.NestedClassMethodBody,
                    };
                }

                {
                    var lines = new List<string> { "class H:" };
                    lines.AddRange(Indent(MemberDecl(t, n)));
                    lines.Add("    class I:");
                    lines.AddRange(Indent(inner.Select(l => l.Replace("{E}", ReadText(form, "H.", n), StringComparison.Ordinal))));
                    yield return new NameCell
                    {
                        Id = CellId("read", t, label, "enclosing", Spelling.Qualified),
                        Family = Family.Read,
                        Source = WithHelpers(Join(lines) + MainBlock($"print({access})")),
                        Tag = SharpyTag.MustRun,
                        Model = Str(value),
                        Python = isInit ? PyOutcome.Name("H") : PyOutcome.Ran(value),
                        Rule = "N7",
                        Target = t,
                        Outer = BindingKind.None,
                        Position = isInit ? ReadPosition.StaticInitializer : ReadPosition.NestedClassMethodBody,
                    };
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // Type positions (roster 6): a later class named in every annotation position
    // ------------------------------------------------------------------------------------------- //

    private static IEnumerable<NameCell> TypePositionCells()
    {
        const string Later = "class Later:\n    v: int = 3\n    def m(self) -> int:\n        return 6\n";
        const string LaterError = "class Later(Exception):\n    v: int = 3\n";

        // Twin: the reader in the python twin's spelling, where python has no spelling of the construct at all.
        var cells = new (string Label, TypePosition At, ReadPosition? Position, string Target, string Reader, string[] Main, string Model, bool Load, string? Twin)[]
        {
            ("variable-annotation", TypePosition.VariableAnnotation, null, Later, "x0: Later = Later()\n", new[] { "print(x0.v)" }, "3", true, null),
            ("class-field-annotation", TypePosition.VariableAnnotation, null, Later, "class H:\n    item: Later = Later()\n", new[] { "print(H().item.v)" }, "3", true, null),
            ("return-annotation", TypePosition.ReturnAnnotation, null, Later, "def r() -> Later:\n    return Later()\n", new[] { "print(r().v)" }, "3", false, null),
            ("parameter-annotation", TypePosition.ParameterAnnotation, null, Later, "def r(o: Later) -> int:\n    return o.v\n", new[] { "print(r(Later()))" }, "3", false, null),
            ("local-annotation", TypePosition.VariableAnnotation, null, Later, "def r() -> int:\n    o: Later = Later()\n    return o.v\n", new[] { "print(r())" }, "3", false, null),
            ("lambda-return", TypePosition.CallableReturn, null, Later, "def r() -> int:\n    f: () -> Later = lambda: Later()\n    return f().v\n", new[] { "print(r())" }, "3", false, null),
            ("arrow-lambda-return", TypePosition.LambdaReturn, null, Later, "def r() -> int:\n    f: () -> Later = () -> Later: Later()\n    return f().v\n", new[] { "print(r())" }, "3", false,
                "def r() -> int:\n    f: () -> Later = lambda: Later()\n    return f().v\n"),
            ("callable-parameter", TypePosition.CallableParameter, null, Later, "def r() -> int:\n    f: (Later) -> int = lambda o: o.v\n    return f(Later())\n", new[] { "print(r())" }, "3", false, null),
            ("isinstance", TypePosition.IsInstance, null, Later, "def r(o: object) -> bool:\n    return isinstance(o, Later)\n", new[] { "print(r(Later()))" }, "True", false, null),
            ("generic-argument", TypePosition.GenericArgument, null, Later, "def r() -> int:\n    xs: list[Later] = [Later()]\n    return xs[0].v\n", new[] { "print(r())" }, "3", false, null),
            ("method-reference", TypePosition.VariableAnnotation, null, Later, "def r() -> int:\n    f: () -> int = Later().m\n    return f()\n", new[] { "print(r())" }, "6", false, null),
            ("f-string", TypePosition.VariableAnnotation, null, Later, "def r() -> str:\n    return f\"v={Later().v}\"\n", new[] { "print(r())" }, "v=3", false, null),
            ("safe-cast", TypePosition.Coercion, null, Later, "def r(o: object) -> bool:\n    return (o as? Later) is not None\n", new[] { "print(r(Later()))" }, "True", false, null),
            ("checked-cast", TypePosition.Coercion, null, Later, "def r(o: object) -> int:\n    return (o as! Later).v\n", new[] { "print(r(Later()))" }, "3", false, null),
            ("property-annotation", TypePosition.PropertyAnnotation, null, Later, "class H:\n    property p: Later = Later()\n", new[] { "print(H().p.v)" }, "3", true, null),
            ("accessor-return", TypePosition.AccessorReturn, null, Later, "class H:\n    property get p(self) -> Later:\n        return Later()\n", new[] { "print(H().p.v)" }, "3", false, null),
            ("union-case-field", TypePosition.UnionCaseField, null, Later, "union U:\n    case V(f: Later)\n", new[] { "match U.V(Later()):", "    case V(f):", "        print(f.v)" }, "3", false, null),
            ("alias-target", TypePosition.AliasTarget, ReadPosition.TypeAliasTarget, Later, "type A = Later\n", new[] { "a: A = Later()", "print(a.v)" }, "3", true, null),
            ("base-list", TypePosition.BaseList, ReadPosition.BaseList, Later, "class C(Later):\n    pass\n", new[] { "print(C().v)" }, "3", true, null),
            ("out-inline-type", TypePosition.OutInlineType, null, Later, "def mk(o: out Later) -> None:\n    o = Later()\ndef r() -> int:\n    mk(out let o: Later)\n    return o.v\n", new[] { "print(r())" }, "3", false,
                "def mk() -> Later:\n    return Later()\ndef r() -> int:\n    o: Later = mk()\n    return o.v\n"),
            ("result-error", TypePosition.ResultError, null, LaterError, "def r() -> int !Later:\n    return Ok(Later().v)\n", new[] { "print(r().unwrap())" }, "3", false,
                "def r() -> int:\n    return Later().v\n"),
        };
        foreach (var (label, at, position, target, reader, main, model, load, twin) in cells)
            foreach (var o in new[] { Order.Earlier, Order.Later })
            {
                // A python twin of `T !E` has no result wrapper to unwrap, so its main reads the value.
                var twinMain = at == TypePosition.ResultError ? new[] { "print(r())" } : main;
                yield return new NameCell
                {
                    Id = CellId("read", "type-position", label, o),
                    Family = Family.Read,
                    Source = (o == Order.Earlier ? target + reader : reader + target) + MainBlock(main),
                    TwinSource = twin == null ? null : (o == Order.Earlier ? target + twin : twin + target) + MainBlock(twinMain),
                    Tag = SharpyTag.MustRun,
                    Model = model,
                    Python = load && o == Order.Later ? PyOutcome.Name("Later") : PyOutcome.Ran(model),
                    Rule = "N1",
                    Target = BindingKind.ModuleClass,
                    Outer = BindingKind.None,
                    Position = position,
                    TypeAt = at,
                };
            }

        const string Raising = "class Later(Exception):\n    pass\n";
        foreach (var o in new[] { Order.Earlier, Order.Later })
        {
            const string Reader = "def r() -> str:\n    try:\n        raise Later()\n    except Later as e:\n        return \"caught\"\n";
            yield return new NameCell
            {
                Id = CellId("read", "type-position", "except-clause", o),
                Family = Family.Read,
                Source = (o == Order.Earlier ? Raising + Reader : Reader + Raising) + MainBlock("print(r())"),
                Tag = SharpyTag.MustRun,
                Model = "caught",
                Python = PyOutcome.Ran("caught"),
                Rule = "N1",
                Target = BindingKind.ModuleClass,
                Outer = BindingKind.None,
                TypeAt = TypePosition.ExceptClause,
            };

            // `try[E]` has no python spelling, so the twin states what the expression's Err arm does.
            const string TryReader = "def boom() -> int:\n    raise Later()\ndef r() -> str:\n    x = try[Later] boom()\n    match x:\n        case Ok(v):\n            return str(v)\n        case Err(e):\n            return \"caught\"\n";
            yield return new NameCell
            {
                Id = CellId("read", "type-position", "try-exception-type", o),
                Family = Family.Read,
                Source = (o == Order.Earlier ? Raising + TryReader : TryReader + Raising) + MainBlock("print(r())"),
                TwinSource = "def r() -> str:\n    return \"caught\"\n" + MainBlock("print(r())"),
                Tag = SharpyTag.MustRun,
                Model = "caught",
                Python = PyOutcome.Ran("caught"),
                Rule = "N1",
                Target = BindingKind.ModuleClass,
                Outer = BindingKind.None,
                TypeAt = TypePosition.TryExceptionType,
            };
        }
    }
}
