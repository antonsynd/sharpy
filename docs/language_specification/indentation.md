# Indentation

Like Python, Sharpy uses indentation instead of curly braces to demarcate
blocks.

## Indentation Rules
- **Exactly 4 spaces per indentation level** (enforced)
- Tabs are **not allowed** for indentation
- Mixed spaces and tabs cause a lexical error
- Indentation must be consistent within a file

*Implementation*
- *🔄 Lowered - The lexer tracks indentation levels via an indentation stack, emitting INDENT/DEDENT tokens. These are converted to C# braces `{ }` during code generation.*
