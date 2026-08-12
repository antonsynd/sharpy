using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Computes CodeGenInfo for all symbols in a module.
/// This class runs after type checking to populate CodeGenInfo on symbols.
///
/// The computation mirrors what RoslynEmitter currently does at emission time,
/// but does it once during semantic analysis instead of dynamically during emission.
/// </summary>
internal class CodeGenInfoComputer
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticBinding _semanticBinding;
    private readonly DiagnosticBag _diagnostics;
    private readonly HashSet<string> _processedModuleLevelVars = new();
    private HashSet<string> _variablesWithExecutionOrderIssues = new();
    private string? _sourceFilePath;

    public CodeGenInfoComputer(SymbolTable symbolTable, SemanticBinding? semanticBinding = null,
        DiagnosticBag? diagnostics = null)
    {
        _symbolTable = symbolTable;
        _semanticBinding = semanticBinding ?? new SemanticBinding();
        _diagnostics = diagnostics ?? new DiagnosticBag();
    }

    /// <summary>
    /// Sets CodeGenInfo in SemanticBinding. Symbol properties are populated later
    /// by MaterializeCodeGenInfo() at the freeze point.
    /// </summary>
    private void SetCodeGenInfo(Symbol symbol, CodeGenInfo info)
    {
        _semanticBinding.SetCodeGenInfo(symbol, info);
    }

    /// <summary>
    /// Compute CodeGenInfo for all symbols in the module.
    /// </summary>
    public void ComputeForModule(Module module, string? sourceFilePath = null)
    {
        _sourceFilePath = sourceFilePath;

        // Run execution order analysis first to detect variables that need special handling
        var analyzer = new ExecutionOrderAnalyzer(_symbolTable);
        _variablesWithExecutionOrderIssues = analyzer.Analyze(module.Body);

        // First pass: Process module-level declarations (top-level statements)
        ProcessModuleLevelDeclarations(module);

        // Second pass: Process type declarations (classes, structs, interfaces, enums)
        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    ProcessClassDef(classDef);
                    break;
                case StructDef structDef:
                    ProcessStructDef(structDef);
                    break;
                case InterfaceDef interfaceDef:
                    ProcessInterfaceDef(interfaceDef);
                    break;
                case EnumDef enumDef:
                    ProcessEnumDef(enumDef);
                    break;
                case FunctionDef funcDef:
                    ProcessFunctionDef(funcDef, isModuleLevel: true);
                    break;
            }
        }

        // Third pass: Detect module-level name collisions
        DetectModuleLevelCollisions(module);
    }

    private void ProcessModuleLevelDeclarations(Module module)
    {
        foreach (var stmt in module.Body)
        {
            // Unwrap for classification only: a suppress-decorated import (#1124) must still
            // produce its module-level CodeGenInfo. Definitions/variables are never wrapped
            // (decorators attach directly), so unwrap is a no-op for the other cases.
            switch (stmt.UnwrapDecorated())
            {
                case VariableDeclaration varDecl when varDecl.IsConst:
                    ProcessModuleLevelConstant(varDecl);
                    break;
                case VariableDeclaration varDecl:
                    ProcessModuleLevelVariable(varDecl);
                    break;
                case ImportStatement import:
                    ProcessImport(import);
                    break;
                case FromImportStatement fromImport:
                    ProcessFromImport(fromImport);
                    break;
                case PropertyDef propDef:
                    ProcessModuleLevelProperty(propDef);
                    break;
            }
        }
    }

    private void ProcessModuleLevelProperty(PropertyDef propDef)
    {
        var symbol = _symbolTable.Lookup(propDef.Name);
        if (symbol is VariableSymbol { IsModuleProperty: true } varSymbol && varSymbol.CodeGenInfo == null)
        {
            var escaped = propDef.IsNameBacktickEscaped;
            var csharpName = NameCasing.ResolveField(propDef.Name, escaped);

            SetCodeGenInfo(varSymbol, new CodeGenInfo
            {
                CSharpName = csharpName,
                OriginalName = propDef.Name,
                Version = 0,
                IsModuleLevel = true,
                IsConstant = false,
                HasExecutionOrderIssues = false
            });
        }
    }

    private void ProcessModuleLevelVariable(VariableDeclaration varDecl)
    {
        var symbol = _symbolTable.Lookup(varDecl.Name);
        if (symbol is VariableSymbol varSymbol)
        {
            // Use execution order analysis result instead of simple initializer check
            var hasIssues = _variablesWithExecutionOrderIssues.Contains(varDecl.Name);

            // Variables with execution order issues become locals in Main() and use camelCase
            // ALL_CAPS names (Python-style constants) use ToConstantCase (preserves SCREAMING_SNAKE_CASE)
            // Other module-level fields use ToPascalCase
            var escaped = varDecl.IsNameBacktickEscaped;
            string csharpName;
            if (hasIssues)
                csharpName = NameCasing.ResolveVariable(varDecl.Name, escaped);
            else if (NameFormDetector.IsConstantCaseName(varDecl.Name))
                csharpName = NameCasing.ResolveConstant(varDecl.Name, escaped);
            else
                csharpName = NameCasing.ResolveField(varDecl.Name, escaped);

            SetCodeGenInfo(varSymbol, new CodeGenInfo
            {
                CSharpName = csharpName,
                OriginalName = varDecl.Name,
                Version = 0,
                IsModuleLevel = !hasIssues,  // Not module-level if has execution order issues
                IsConstant = false,
                HasExecutionOrderIssues = hasIssues
            });
            _processedModuleLevelVars.Add(varDecl.Name);
        }
    }

    private void ProcessModuleLevelConstant(VariableDeclaration constDecl)
    {
        var symbol = _symbolTable.Lookup(constDecl.Name);
        if (symbol is VariableSymbol varSymbol)
        {
            SetCodeGenInfo(varSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveConstant(constDecl.Name, constDecl.IsNameBacktickEscaped),
                OriginalName = constDecl.Name,
                Version = 0,
                IsModuleLevel = true,
                IsConstant = true,
                HasExecutionOrderIssues = false // Constants are always compile-time
            });
        }
    }

    private void ProcessImport(ImportStatement import)
    {
        foreach (var alias in import.Names)
        {
            var effectiveName = alias.AsName ?? alias.Name;
            var symbol = _symbolTable.Lookup(effectiveName);
            if (symbol is ModuleSymbol moduleSymbol)
            {
                SetCodeGenInfo(moduleSymbol, new CodeGenInfo
                {
                    CSharpName = effectiveName.Replace(".", "_", StringComparison.Ordinal),
                    OriginalName = effectiveName,
                    ImportKind = alias.AsName != null ? ImportKind.FromImportWithAlias : ImportKind.ModuleImport
                });
            }
        }
    }

    private void ProcessFromImport(FromImportStatement fromImport)
    {
        var isNetModule = _semanticBinding.IsNetModule(fromImport.Module);

        foreach (var imported in fromImport.Names)
        {
            var effectiveName = imported.AsName ?? imported.Name;
            var symbol = _symbolTable.Lookup(effectiveName);
            if (symbol != null)
            {
                var originalName = imported.AsName != null ? imported.Name : null;
                var csharpName = DetermineCSharpNameForFromImport(imported.Name, symbol, isNetModule);

                SetCodeGenInfo(symbol, new CodeGenInfo
                {
                    CSharpName = csharpName,
                    OriginalName = effectiveName,
                    ImportKind = imported.AsName != null ? ImportKind.FromImportWithAlias : ImportKind.FromImport,
                    OriginalImportName = originalName
                });
            }
        }
    }

    private string DetermineCSharpNameForFromImport(string name, Symbol symbol, bool isNetModule)
    {
        // For .NET module variable fields, SCREAMING_SNAKE_CASE names match C# verbatim
        // (e.g., csv.QUOTE_ALL). Other names need PascalCase conversion to match the
        // generated C# field names (e.g., string.digits → Digits, math.pi → Pi).
        if (isNetModule && symbol is VariableSymbol)
        {
            if (NameFormDetector.IsConstantCaseName(name))
                return name;
            return NameCasing.ResolveMethod(name, symbol.IsNameBacktickEscaped);
        }

        // Use the same logic as RoslynEmitter for from-imports:
        // - ALL_CAPS names (constants) use ToConstantCase (preserves SCREAMING_SNAKE_CASE)
        // - Other names become PascalCase
        if (NameFormDetector.IsConstantCaseName(name))
        {
            return NameCasing.ResolveConstant(name, symbol.IsNameBacktickEscaped);
        }
        return NameCasing.ResolveMethod(name, symbol.IsNameBacktickEscaped);
    }

    private void ProcessClassDef(ClassDef classDef)
    {
        var typeSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            SetCodeGenInfo(typeSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveType(classDef.Name, classDef.IsNameBacktickEscaped),
                OriginalName = classDef.Name
            });

            // Process class members
            ProcessTypeMembers(typeSymbol, classDef.Body);
            DetectMemberCollisions(typeSymbol, classDef.Body);
        }
    }

    private void ProcessStructDef(StructDef structDef)
    {
        var typeSymbol = _symbolTable.Lookup(structDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            SetCodeGenInfo(typeSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveType(structDef.Name, structDef.IsNameBacktickEscaped),
                OriginalName = structDef.Name
            });

            ProcessTypeMembers(typeSymbol, structDef.Body);
            DetectMemberCollisions(typeSymbol, structDef.Body);
        }
    }

    private void ProcessInterfaceDef(InterfaceDef interfaceDef)
    {
        var typeSymbol = _symbolTable.Lookup(interfaceDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            // Interfaces preserve their exact name (which should already have I prefix)
            SetCodeGenInfo(typeSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveInterface(interfaceDef.Name, interfaceDef.IsNameBacktickEscaped),
                OriginalName = interfaceDef.Name
            });

            ProcessTypeMembers(typeSymbol, interfaceDef.Body);
            DetectMemberCollisions(typeSymbol, interfaceDef.Body);
        }
    }

    private void ProcessEnumDef(EnumDef enumDef)
    {
        var typeSymbol = _symbolTable.Lookup(enumDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            // Determine if this is a string enum (has at least one string literal value)
            var isStringEnum = enumDef.Members.Any(m => m.Value is StringLiteral);

            SetCodeGenInfo(typeSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveType(enumDef.Name, enumDef.IsNameBacktickEscaped),
                OriginalName = enumDef.Name,
                IsStringEnum = isStringEnum
            });

            // Enum members keep their exact names - no CodeGenInfo needed
            // as they are emitted as-is in C#

            DetectEnumMemberCollisions(enumDef, isStringEnum);
        }
    }

    private void ProcessTypeMembers(TypeSymbol typeSymbol, IEnumerable<Statement> body)
    {
        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case VariableDeclaration fieldDecl:
                    ProcessField(typeSymbol, fieldDecl);
                    break;
                case FunctionDef funcDef:
                    ProcessMethodDef(typeSymbol, funcDef);
                    break;
            }
        }
    }

    private void ProcessField(TypeSymbol typeSymbol, VariableDeclaration fieldDecl)
    {
        var fieldSymbol = typeSymbol.Fields.FirstOrDefault(f => f.Name == fieldDecl.Name);
        if (fieldSymbol != null)
        {
            SetCodeGenInfo(fieldSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveField(fieldDecl.Name, fieldDecl.IsNameBacktickEscaped),
                OriginalName = fieldDecl.Name,
                IsModuleLevel = false,
                IsConstant = fieldDecl.IsConst
            });
        }
    }

    private void ProcessMethodDef(TypeSymbol typeSymbol, FunctionDef funcDef)
    {
        var methodSymbol = typeSymbol.Methods.FirstOrDefault(m => m.Name == funcDef.Name);
        if (methodSymbol != null)
        {
            SetCodeGenInfo(methodSymbol, new CodeGenInfo
            {
                CSharpName = DunderNameMapping.ResolveCSharpName(funcDef.Name)
                    ?? NameCasing.ResolveMethod(funcDef.Name, funcDef.IsNameBacktickEscaped),
                OriginalName = funcDef.Name,
                IsModuleLevel = false
            });
        }
    }

    private void ProcessFunctionDef(FunctionDef funcDef, bool isModuleLevel)
    {
        // For overloaded module-level functions, the symbol table only holds the
        // first overload, so resolve the specific symbol by declaration line.
        var overloads = _symbolTable.LookupFunctionOverloads(funcDef.Name);
        var funcSymbol = overloads is { Count: > 1 }
            ? overloads.FirstOrDefault(o => o.DeclarationLine == funcDef.LineStart)
            : _symbolTable.Lookup(funcDef.Name) as FunctionSymbol;
        if (funcSymbol != null)
        {
            SetCodeGenInfo(funcSymbol, new CodeGenInfo
            {
                CSharpName = DunderNameMapping.ResolveCSharpName(funcDef.Name)
                    ?? NameCasing.ResolveMethod(funcDef.Name, funcDef.IsNameBacktickEscaped),
                OriginalName = funcDef.Name,
                IsModuleLevel = isModuleLevel
            });
        }
    }

    /// <summary>
    /// C# member names synthesized by the iterator protocol (__next__).
    /// User-defined members must not mangle to any of these names.
    /// </summary>
    private static readonly HashSet<string> IteratorProtocolReservedNames = new()
    {
        "Current", "MoveNext", "Reset", "Dispose", "GetEnumerator"
    };

    /// <summary>
    /// Every member a type contributes to the generated C# type's single member namespace, paired
    /// with the C# name it compiles to. One enumeration rather than a walk per kind: a member kind
    /// missing here is a CS0102 the user only ever sees as SPY0908, which is exactly how properties,
    /// events and nested types escaped the check (#1385).
    ///
    /// <para>Two sources, because neither alone is complete. Fields and methods come from the
    /// symbol table — the only source that also holds implicitly declared fields (<c>self.x = ...</c>
    /// in <c>__init__</c>, dataclass fields), which have no statement in <paramref name="body"/>.
    /// Properties, events and nested types come from the AST: <see cref="PropertySymbol"/> and
    /// <see cref="EventSymbol"/> are standalone records carrying neither an escape flag nor a
    /// <c>CodeGenInfo</c>, and emission derives their C# names from the AST node itself
    /// (<c>NameCasing.ResolveMethod(def.Name, def.IsNameBacktickEscaped)</c> — see
    /// <c>RoslynEmitter.ClassMembers.Properties.cs</c> and <c>.Events.cs</c>), so the check reads
    /// the same source it has to agree with.</para>
    ///
    /// <para>The same Sharpy name may be yielded more than once — method overloads, a split
    /// <c>property get</c>/<c>property set</c> pair, an <c>event add</c>/<c>event remove</c> pair —
    /// because those intentionally compile to one C# member. Callers dedupe on the original name.</para>
    /// </summary>
    private IEnumerable<(string OriginalName, string CSharpName)> EnumerateMemberNames(
        TypeSymbol typeSymbol, IEnumerable<Statement> body)
    {
        foreach (var symbol in typeSymbol.Fields.Cast<Symbol>().Concat(typeSymbol.Methods))
        {
            var info = _semanticBinding.GetCodeGenInfo(symbol);
            if (info != null)
                yield return (symbol.Name, info.CSharpName);
        }

        foreach (var stmt in body)
        {
            switch (stmt.UnwrapDecorated())
            {
                case PropertyDef propDef:
                    yield return (propDef.Name,
                        NameCasing.ResolveMethod(propDef.Name, propDef.IsNameBacktickEscaped));
                    break;
                case EventDef eventDef:
                    yield return (eventDef.Name,
                        NameCasing.ResolveMethod(eventDef.Name, eventDef.IsNameBacktickEscaped));
                    break;
                // Nested types occupy the same member namespace as fields and methods.
                // TypeAlias is deliberately absent: it is compile-time only and emits nothing.
                case ClassDef nestedClass:
                    yield return (nestedClass.Name,
                        NameCasing.ResolveType(nestedClass.Name, nestedClass.IsNameBacktickEscaped));
                    break;
                case StructDef nestedStruct:
                    yield return (nestedStruct.Name,
                        NameCasing.ResolveType(nestedStruct.Name, nestedStruct.IsNameBacktickEscaped));
                    break;
                case EnumDef nestedEnum:
                    yield return (nestedEnum.Name,
                        NameCasing.ResolveType(nestedEnum.Name, nestedEnum.IsNameBacktickEscaped));
                    break;
                case InterfaceDef nestedInterface:
                    yield return (nestedInterface.Name,
                        NameCasing.ResolveInterface(nestedInterface.Name, nestedInterface.IsNameBacktickEscaped));
                    break;
            }
        }
    }

    /// <summary>
    /// Detects name collisions among the members of a type after mangling — fields, methods,
    /// properties, events and nested types, plus the type's own type parameters.
    /// </summary>
    private void DetectMemberCollisions(TypeSymbol typeSymbol, IEnumerable<Statement> body)
    {
        var seen = new Dictionary<string, (string originalName, int? line)>(); // CSharpName → (originalName, line)

        foreach (var tp in typeSymbol.TypeParameters)
        {
            // Key on the name the emitter writes, not the source spelling: type parameters go out
            // through CSharpKeywords.EscapeIfNeeded (`class` → `@class`), which is the same form a
            // member's CSharpName takes, so a raw key could not match one.
            seen[CSharpKeywords.EscapeIfNeeded(tp.Name)] = (tp.Name, tp.LineStart > 0 ? tp.LineStart : null);
        }

        foreach (var (originalName, csharpName) in EnumerateMemberNames(typeSymbol, body))
        {
            var line = FindMemberLine(body, originalName);

            if (seen.TryGetValue(csharpName, out var existing))
            {
                // One Sharpy name declared more than once — overloads, a split get/set property,
                // an add/remove event pair — intentionally compiles to a single C# member.
                // Mirrors the module-level arm's overload guard.
                if (string.Equals(originalName, existing.originalName, StringComparison.Ordinal))
                    continue;

                var firstPos = existing.line.HasValue ? $" (line {existing.line})" : "";
                _diagnostics.AddError(
                    $"Name collision: '{originalName}' and '{existing.originalName}'{firstPos} both compile to " +
                    $"'{csharpName}'. Rename one, or backtick-escape the name you need to keep.",
                    line: line,
                    code: DiagnosticCodes.CodeGen.MemberNameCollision,
                    phase: CompilerPhase.CodeGeneration);
            }
            else
            {
                seen[csharpName] = (originalName, line);
            }
        }

        // Check for collisions with synthesized iterator protocol members
        var hasNext = false;
        foreach (var method in typeSymbol.Methods)
        {
            if (method.Name == DunderNames.Next)
            {
                hasNext = true;
                break;
            }
        }

        if (hasNext)
        {
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (originalName, csharpName) in EnumerateMemberNames(typeSymbol, body))
            {
                // Skip __next__ and __iter__ — they are part of the iterator protocol, not collisions
                if (originalName == DunderNames.Next || originalName == DunderNames.Iter)
                    continue;

                if (IteratorProtocolReservedNames.Contains(csharpName) && reported.Add(originalName))
                {
                    var line = FindMemberLine(body, originalName);
                    _diagnostics.AddError(
                        $"Name collision: '{originalName}' compiles to '{csharpName}', " +
                        $"which conflicts with a synthesized iterator protocol member. " +
                        $"Rename the member to avoid the collision.",
                        line: line,
                        code: DiagnosticCodes.CodeGen.MemberNameCollision,
                        phase: CompilerPhase.CodeGeneration);
                }
            }
        }
    }

    /// <summary>
    /// The C# name an enum member compiles to. Two spellings, because an enum lowers two ways:
    /// an int-backed enum is a real C# enum whose members go through
    /// <see cref="NameMangler.ToEnumMemberName"/> (<c>GenerateEnumMember</c>), while a string-backed
    /// enum is a class of singleton fields written through <c>NameContext.Constant</c>
    /// (<c>GenerateStringEnumClass</c>, #1284). Keying the collision walk on the wrong one would
    /// make the check disagree with emission — <c>low</c>/<c>LOW</c> collide under one rule and not
    /// the other.
    /// </summary>
    private static string EnumMemberCSharpName(string memberName, bool isStringEnum)
        => isStringEnum
            ? NameMangler.ToConstantCase(memberName)
            : NameMangler.ToEnumMemberName(memberName);

    /// <summary>
    /// Detects name collisions among an enum's members after mangling. Enums have their own walk
    /// because their members are not symbols and their naming rule is neither a field's nor a
    /// method's (#1385).
    /// </summary>
    private void DetectEnumMemberCollisions(EnumDef enumDef, bool isStringEnum)
    {
        var seen = new Dictionary<string, (string originalName, int? line)>(); // CSharpName → (originalName, line)

        foreach (var member in enumDef.Members)
        {
            var csharpName = EnumMemberCSharpName(member.Name, isStringEnum);
            var line = member.LineStart > 0 ? member.LineStart : (int?)null;

            if (seen.TryGetValue(csharpName, out var existing))
            {
                // A member spelled identically twice is a duplicate declaration, not a mangling
                // collision — leave that to the declaration checks so it is reported once.
                if (string.Equals(member.Name, existing.originalName, StringComparison.Ordinal))
                    continue;

                var firstPos = existing.line.HasValue ? $" (line {existing.line})" : "";
                // No backtick remedy here: EnumMember carries no escape flag, so renaming is the
                // only fix available to the user.
                _diagnostics.AddError(
                    $"Name collision: enum members '{member.Name}' and '{existing.originalName}'{firstPos} " +
                    $"both compile to '{csharpName}'. Rename one.",
                    line: line,
                    code: DiagnosticCodes.CodeGen.MemberNameCollision,
                    phase: CompilerPhase.CodeGeneration);
            }
            else
            {
                seen[csharpName] = (member.Name, line);
            }
        }
    }

    /// <summary>
    /// Detects name collisions among module-level symbols (functions, variables, and types).
    /// All of these become members of the same module class in generated C#.
    /// </summary>
    private void DetectModuleLevelCollisions(Module module)
    {
        var moduleClassName = NameMangler.ComputeModuleClassName(_sourceFilePath);

        // Check each function/variable against the module class name
        if (moduleClassName != null)
        {
            foreach (var stmt in module.Body)
            {
                string? symbolName = null;
                int? stmtLine = null;

                switch (stmt)
                {
                    case FunctionDef funcDef:
                        symbolName = funcDef.Name;
                        stmtLine = funcDef.LineStart;
                        break;
                    case VariableDeclaration varDecl:
                        symbolName = varDecl.Name;
                        stmtLine = varDecl.LineStart;
                        break;
                }

                if (symbolName == null)
                    continue;

                var symbol = _symbolTable.Lookup(symbolName);
                if (symbol == null)
                    continue;

                var info = _semanticBinding.GetCodeGenInfo(symbol);
                if (info == null)
                    continue;

                if (info.CSharpName == moduleClassName)
                {
                    _diagnostics.AddError(
                        $"Function '{symbolName}' compiles to '{info.CSharpName}', which conflicts " +
                        $"with the module class name derived from the filename. Rename the function " +
                        $"to avoid the collision.",
                        line: stmtLine,
                        code: DiagnosticCodes.CodeGen.FunctionModuleClassCollision,
                        phase: CompilerPhase.CodeGeneration);
                }
            }
        }

        var seen = new Dictionary<string, (string originalName, int? line)>(); // CSharpName → (originalName, line)

        foreach (var stmt in module.Body)
        {
            string? symbolName = null;
            int? stmtLine = null;

            switch (stmt)
            {
                case FunctionDef funcDef:
                    symbolName = funcDef.Name;
                    stmtLine = funcDef.LineStart;
                    break;
                case VariableDeclaration varDecl:
                    symbolName = varDecl.Name;
                    stmtLine = varDecl.LineStart;
                    break;
                case ClassDef classDef:
                    symbolName = classDef.Name;
                    stmtLine = classDef.LineStart;
                    break;
                case StructDef structDef:
                    symbolName = structDef.Name;
                    stmtLine = structDef.LineStart;
                    break;
                case InterfaceDef interfaceDef:
                    symbolName = interfaceDef.Name;
                    stmtLine = interfaceDef.LineStart;
                    break;
                case EnumDef enumDef:
                    symbolName = enumDef.Name;
                    stmtLine = enumDef.LineStart;
                    break;
                case DelegateDef delegateDef:
                    symbolName = delegateDef.Name;
                    stmtLine = delegateDef.LineStart;
                    break;
            }

            if (symbolName == null)
                continue;

            var symbol = _symbolTable.Lookup(symbolName);
            if (symbol == null)
                continue;

            var info = _semanticBinding.GetCodeGenInfo(symbol);
            var csharpName = info?.CSharpName;
            if (csharpName == null && symbol is TypeSymbol { TypeKind: TypeKind.Delegate })
                csharpName = NameCasing.ResolveType(symbolName, symbol.IsNameBacktickEscaped);
            if (csharpName == null)
                continue;

            if (seen.TryGetValue(csharpName, out var existing))
            {
                // Overloads of the same module-level function share an identical
                // Python name and intentionally compile to the same C# name — this
                // is not a collision.
                if (symbolName == existing.originalName)
                    continue;

                var firstPos = existing.line.HasValue ? $" (line {existing.line})" : "";
                _diagnostics.AddError(
                    $"Name collision: '{symbolName}' and '{existing.originalName}'{firstPos} both compile to " +
                    $"'{csharpName}'. Rename one, or backtick-escape the name you need to keep.",
                    line: stmtLine,
                    code: DiagnosticCodes.CodeGen.MemberNameCollision,
                    phase: CompilerPhase.CodeGeneration);
            }
            else
            {
                seen[csharpName] = (symbolName, stmtLine);
            }
        }
    }

    /// <summary>
    /// Finds the line number for a member declaration in the type body. Covers every kind
    /// <see cref="EnumerateMemberNames"/> yields — a kind missing here reports the collision with
    /// no position at all.
    /// </summary>
    private static int? FindMemberLine(IEnumerable<Statement> body, string memberName)
    {
        foreach (var stmt in body)
        {
            switch (stmt.UnwrapDecorated())
            {
                case VariableDeclaration v when v.Name == memberName:
                    return v.LineStart;
                case FunctionDef f when f.Name == memberName:
                    return f.LineStart;
                case PropertyDef p when p.Name == memberName:
                    return p.LineStart;
                case EventDef e when e.Name == memberName:
                    return e.LineStart;
                case ClassDef c when c.Name == memberName:
                    return c.LineStart;
                case StructDef s when s.Name == memberName:
                    return s.LineStart;
                case EnumDef en when en.Name == memberName:
                    return en.LineStart;
                case InterfaceDef i when i.Name == memberName:
                    return i.LineStart;
            }
        }
        return null;
    }

    // Note: HasExecutionOrderIssues and ContainsRuntimeExpression methods were removed.
    // The ExecutionOrderAnalyzer class now handles execution order detection with
    // proper multi-pass analysis including assignment-before-declaration and
    // transitive dependency detection.
}
