using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class UnusedImportValidatorTests
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

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void UnusedFromImport_ReportsWarning()
    {
        var code = @"
from math import sqrt, pi

def main():
    print(sqrt(4))
";
        var (module, context) = Parse(code);
        var validator = new UnusedImportValidator();
        validator.Validate(module, context);

        Assert.Contains(context.Diagnostics.GetWarnings(),
            w => w.Message.Contains("'pi' is never used") &&
                 w.Code == DiagnosticCodes.Validation.UnusedImport);
    }

    [Fact]
    public void UsedFromImport_NoWarning()
    {
        var code = @"
from math import sqrt

def main():
    print(sqrt(4))
";
        var (module, context) = Parse(code);
        var validator = new UnusedImportValidator();
        validator.Validate(module, context);

        var importWarnings = context.Diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Validation.UnusedImport)
            .ToList();
        Assert.Empty(importWarnings);
    }

    [Fact]
    public void WildcardImport_NoWarning()
    {
        var code = @"
from math import *

def main():
    print(sqrt(4))
";
        var (module, context) = Parse(code);
        var validator = new UnusedImportValidator();
        validator.Validate(module, context);

        var importWarnings = context.Diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Validation.UnusedImport)
            .ToList();
        Assert.Empty(importWarnings);
    }

    [Fact]
    public void ImportUsedInTypeAnnotation_NoWarning()
    {
        var code = @"
from typing import Optional

def foo(x: Optional[int]) -> None:
    pass
";
        var (module, context) = Parse(code);
        var validator = new UnusedImportValidator();
        validator.Validate(module, context);

        var importWarnings = context.Diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Validation.UnusedImport)
            .ToList();
        Assert.Empty(importWarnings);
    }

    [Fact]
    public void ImportUsedAsBaseClass_NoWarning()
    {
        var code = @"
from base_module import BaseClass

class Child(BaseClass):
    def foo(self) -> None:
        pass
";
        var (module, context) = Parse(code);
        var validator = new UnusedImportValidator();
        validator.Validate(module, context);

        var importWarnings = context.Diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Validation.UnusedImport)
            .ToList();
        Assert.Empty(importWarnings);
    }
}
