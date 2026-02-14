using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
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

    [Fact]
    public void ImportUsedAsStructInterface_NoWarning()
    {
        var code = @"
from protocols import IDrawable

struct Point(IDrawable):
    x: int
    y: int

    def draw(self) -> None:
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
    public void ImportUsedAsBaseInterface_NoWarning()
    {
        var code = @"
from protocols import ISerializable

interface IAdvanced(ISerializable):
    def process(self) -> None:
        ...
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
    public void ImportUsedWithAlias_NoWarning()
    {
        var code = @"
from math import sqrt as square_root

def main():
    print(square_root(4))
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
    public void MultipleUnusedImports_ReportsAll()
    {
        var code = @"
from math import sqrt, pi, ceil

def main():
    pass
";
        var (module, context) = Parse(code);
        var validator = new UnusedImportValidator();
        validator.Validate(module, context);

        var importWarnings = context.Diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Validation.UnusedImport)
            .ToList();
        Assert.Equal(3, importWarnings.Count);
    }

    [Fact]
    public void ImportUsedInTypeAlias_NoWarning()
    {
        var code = @"
from typing import Optional

type MaybeInt = Optional[int]

def main():
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
    public void ImportUsedAsDecorator_NoWarning()
    {
        var code = @"
from decorators import cached

class Foo:
    @cached
    def bar(self) -> int:
        return 42
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
    public void ImportUsedAsClassDecorator_NoWarning()
    {
        var code = @"
from decorators import singleton

@singleton
class Config:
    def get(self) -> str:
        return ""value""
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
    public void ImportUsedInWhileElse_NoWarning()
    {
        var code = @"
from math import sqrt

def main():
    while False:
        pass
    else:
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
    public void ImportUsedInEnumMemberValue_NoWarning()
    {
        var code = @"
from constants import MAX_ITEMS

enum Limit:
    LOW = 1
    MEDIUM = 5
    HIGH = MAX_ITEMS
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
    public void ImportNotUsedInEnum_ReportsWarning()
    {
        var code = @"
from constants import MAX_ITEMS

enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3
";
        var (module, context) = Parse(code);
        var validator = new UnusedImportValidator();
        validator.Validate(module, context);

        var importWarnings = context.Diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Validation.UnusedImport)
            .ToList();
        Assert.Single(importWarnings);
        Assert.Contains("'MAX_ITEMS'", importWarnings[0].Message);
    }
}
