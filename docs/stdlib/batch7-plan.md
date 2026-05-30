# Stdlib Batch 7: xml, html

## Context

Implement the two "markup" stdlib modules from the [Tier 2 roadmap](roadmap.md) Batch 7. These provide structured data processing — `xml` wraps `System.Xml.Linq` for an ElementTree-style API, and `html` provides HTML escaping/unescaping plus an event-driven HTML parser.

**GitHub issues:**
- [#751](https://github.com/antonsynd/sharpy/issues/751) — xml module (XML processing via ElementTree API)
- [#750](https://github.com/antonsynd/sharpy/issues/750) — html module (HTML processing and parsing)

## Current State

- **33+ stdlib modules** exist in `src/Sharpy.Stdlib/` (31 original + Toml + Yaml; earlier batches may add more by the time this plan executes)
- Neither xml nor html exists yet
- Module infrastructure is mature: `[SharpyModule]`/`[SharpyModuleType]` attributes, `ModuleRegistry` discovery, `.spy` source files, per-module `.csproj` files in `modules/`
- No NuGet dependencies needed — `System.Xml.Linq` and `System.Net.WebUtility` are BCL types
- `System.Xml.Linq` is available on both `net10.0` and `netstandard2.1`

## Design Decisions

1. **Both modules are hand-written C#** (not `.spy`-generated). Rationale: xml wraps `System.Xml.Linq` with XPath queries and namespace handling; html requires a custom event-driven parser with HTML5-specific edge cases. Both are cleaner in C#. Follow the pattern of Json/Toml.

2. **xml is implemented first** (more valuable standalone). html is implemented second (simpler in API surface, but the parser is custom). The two modules are independent — no dependency between them.

3. **Flat module name `xml`** (not `xml.etree.ElementTree`). Sharpy doesn't support dotted sub-module names. All ElementTree functionality is exposed directly from `import xml`. This matches the issue's proposed API.

4. **`Element` wraps `XElement`, `ElementTree` wraps `XDocument`**. These are the two core types:
   - `Element` — represents a single XML element. Properties: `Tag` (str), `Text` (str?), `Tail` (str?), `Attrib` (Dict[str, str]). Children are accessed via iteration, indexing, `Find()`/`FindAll()`.
   - `ElementTree` — represents a complete XML document with a root element. Provides `Parse()`/`Write()` for file I/O.
   - Both are `[SharpyModuleType("xml")]` sealed classes (C# 9.0 compat for netstandard2.1).

5. **XPath subset via `find()`/`findall()`/`iterfind()`**. Python's ElementTree supports a limited XPath subset — not full XPath 1.0. We support the same subset:
   - `tag` — direct children with matching tag
   - `{ns}tag` — namespaced tag
   - `.` — current element
   - `*` — all direct children
   - `.//tag` — all descendants with matching tag
   - `..` — parent (NOT supported — `XElement.Parent` exists but Python's impl is buggy/incomplete)
   - `[@attrib]` — elements with attribute
   - `[@attrib='value']` — elements with attribute equal to value
   - `[tag]` — elements with child named tag
   - `[position]` — element at position (1-based)
   
   Implementation: translate these patterns into `System.Xml.Linq` queries (combination of `Elements()`, `Descendants()`, and LINQ predicates). NOT using `XPathSelectElements` because Python's XPath subset doesn't map cleanly to full XPath 1.0 semantics.

6. **Namespace handling via `{uri}tag` notation** (matching Python). A namespaced tag like `<ns:child xmlns:ns="http://example.com">` is represented as `{http://example.com}child`. This matches both Python's convention and `XName.Namespace + XName.LocalName` in LINQ to XML.

7. **`html.escape()`/`unescape()` wrap `System.Net.WebUtility`**:
   - `escape(s, quote=True)` → `WebUtility.HtmlEncode(s)`. When `quote=False`, don't escape `"` and `'` (post-process the encoded string).
   - `unescape(s)` → `WebUtility.HtmlDecode(s)`. Handles named refs (`&amp;`), decimal (`&#60;`), and hex (`&#x3c;`).

8. **`HTMLParser` is a custom event-driven parser** (no .NET equivalent exists). Implementation approach:
   - Simple state machine that scans HTML character by character
   - Callback methods: `HandleStarttag`, `HandleEndtag`, `HandleData`, `HandleComment`, `HandleStartendtag`, `HandleEntityref`, `HandleCharref`, `HandleDecl`, `HandlePi`
   - Users subclass `HTMLParser` and override these virtual methods (matching Python's pattern)
   - `convert_charrefs` constructor parameter (default `true`, matching Python 3.4+) — when true, character references are automatically converted and delivered via `HandleData` instead of separate `HandleEntityref`/`HandleCharref` callbacks
   - Handles CDATA/RCDATA content elements (`script`, `style`, `textarea`) — content inside these is delivered as raw data, not parsed for tags
   - Malformed HTML is handled gracefully: unclosed tags, missing end tags, bare `<` in text are all tolerated

9. **No `html.entities` sub-module in v1.** Python's `html.entities` has 2,231 HTML5 named character references. `System.Net.WebUtility.HtmlDecode` already handles the standard named refs. If users need the entities dictionary, it can be added later.

10. **`ParseError` for xml, no special error type for html** (matching Python):
    - `xml.ParseError` extends `Exception`, wraps `System.Xml.Linq.XmlException`
    - html parser doesn't raise errors for malformed HTML (it's tolerant by design)

11. **C# 9.0 compatibility** for `netstandard2.1` target. No file-scoped namespaces, no record structs, no global usings. Use `#if NET10_0_OR_GREATER` where needed.

12. **No new NuGet dependencies.** xml uses `System.Xml.Linq` (BCL). html uses `System.Net.WebUtility` (BCL) + custom parser.

13. **xml module-level factory functions** match Python's API:
    - `xml.Element(tag, attrib=None)` — create element
    - `xml.SubElement(parent, tag, attrib=None)` — create and append child
    - `xml.parse(source)` — parse file, return ElementTree
    - `xml.fromstring(text)` — parse string, return root Element
    - `xml.tostring(element, encoding="unicode", method="xml")` — serialize to string
    - `xml.Comment(text)` — create comment element
    - `xml.ProcessingInstruction(target, text)` — create PI element
    - `xml.indent(tree_or_element, space="  ", level=0)` — in-place pretty-print
    - `xml.iselement(obj)` — type check

## Implementation

Module implementation order: xml (larger, more valuable) → html (simpler API surface, independent). Each module follows the standard stdlib pattern.

### Phase 1: xml Module — Core Types

**Goal:** Implement `Element` and `ElementTree` classes with construction and basic navigation.

#### Tasks

1. **Create xml module directory and registration** — `src/Sharpy.Stdlib/Xml/__Init__.cs`
   - Create `Xml/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("xml")]` on `public static partial class Xml`
   - Follow exact pattern from `src/Sharpy.Stdlib/Json/__Init__.cs`
   - Acceptance: `Xml` partial class compiles with `[SharpyModule]` attribute
   - Commit: `feat(stdlib): scaffold xml module registration`

2. **Implement ParseError exception** — `src/Sharpy.Stdlib/Xml/ParseError.cs`
   - Create `[SharpyModuleType("xml")]` class `ParseError : Exception`:
     - Properties: `int Position`, `int Line`, `int Column`
     - Constructor: `ParseError(string message, int position = 0, int line = 0, int column = 0) : base(message)`
     - Internal factory: `static ParseError FromXmlException(System.Xml.XmlException ex)` — extracts line/column from the .NET exception
   - Acceptance: ParseError compiles with properties matching Python's `xml.etree.ElementTree.ParseError`
   - Commit: `feat(stdlib): implement xml ParseError exception`

3. **Implement Element class** — `src/Sharpy.Stdlib/Xml/Element.cs`
   - Create `[SharpyModuleType("xml")]` sealed class `Element : IEnumerable<Element>`:
     - Internal storage: `XElement _element` (the backing LINQ to XML element)
     - Properties:
       - `string Tag { get; set; }` — get/set `_element.Name` with namespace conversion (`{uri}local` ↔ `XName`)
       - `string? Text { get; set; }` — `_element.Nodes().OfType<XText>().FirstOrDefault()?.Value` (first text node before any child), set replaces/adds first text node
       - `string? Tail { get; set; }` — text after this element's closing tag (stored as `XText` node after this element in parent). Get: `_element.NodesAfterSelf().OfType<XText>().FirstOrDefault()?.Value`. Set: add/replace text node after self in parent.
       - `Dict<string, string> Attrib { get; }` — wraps `_element.Attributes()` as a mutable Dict
     - Constructors:
       - `Element(string tag, Dict<string, string>? attrib = null)` — creates new `XElement` with converted tag and attributes
       - Internal: `Element(XElement element)` — wraps existing XElement
     - Child access:
       - `Element this[int index]` — get child element by index (negative indexing via `Sharpy.Core` pattern)
       - `int __len__()` → `_element.Elements().Count()` (enables `len(element)`)
       - `IEnumerator<Element> GetEnumerator()` — iterate direct child elements
     - Methods:
       - `void Append(Element child)` — `_element.Add(child._element)`
       - `void Extend(IEnumerable<Element> children)` — append multiple
       - `void Insert(int index, Element child)` — insert at position
       - `void Remove(Element child)` — `child._element.Remove()`
       - `void Clear()` — remove all children, text, and attributes
       - `string? Get(string key, string? defaultValue = null)` — get attribute value
       - `void Set(string key, string value)` — set attribute value
       - `List<string> Keys()` — attribute names
       - `List<(string, string)> Items()` — attribute name-value pairs as list of tuples
       - `Element? Find(string path, Dict<string, string>? namespaces = null)` — find first matching element
       - `List<Element> FindAll(string path, Dict<string, string>? namespaces = null)` — find all matching
       - `string? FindText(string path, string? defaultValue = null, Dict<string, string>? namespaces = null)` — find element, return its text
       - `IEnumerable<Element> Iter(string? tag = null)` — iterate all descendants (or filtered by tag)
       - `IEnumerable<Element> IterFind(string path, Dict<string, string>? namespaces = null)` — lazy findall
       - `IEnumerable<string> IterText()` — iterate all text content
       - `Element MakeElement(string tag, Dict<string, string>? attrib = null)` — factory (for subclassing)
     - Tag conversion helpers (private/internal):
       - `static string ToClarkNotation(XName name)` — `XName{ns}local` → `"{ns}local"` string
       - `static XName FromClarkNotation(string tag)` — `"{ns}local"` string → `XName`
       - Handle empty namespace: `"tag"` ↔ `XName.Get("tag")`
   - Acceptance: Element compiles with all properties and methods, wraps XElement correctly
   - Commit: `feat(stdlib): implement xml Element class`

4. **Implement XPath subset for find/findall** — `src/Sharpy.Stdlib/Xml/XPathMatcher.cs`
   - Create internal class `XPathMatcher` that translates Python ElementTree XPath patterns to LINQ queries:
     - `static IEnumerable<Element> FindAll(XElement root, string path, Dict<string, string>? namespaces)`:
       - Parse path into segments
       - Support: `tag`, `{ns}tag`, `.`, `*`, `.//tag`, `./tag`, `[@attrib]`, `[@attrib='value']`, `[tag]`, `[position]`
       - For `.//` prefix: use `Descendants()` instead of `Elements()`
       - For attribute predicates: filter with LINQ `.Where()`
       - Namespace dict maps prefixes to URIs: `{"ns": "http://example.com"}` allows `ns:tag` in path
     - `static Element? FindFirst(XElement root, string path, Dict<string, string>? namespaces)`:
       - Same as FindAll but returns first match or null
   - Implementation: simple recursive-descent parser for the path expression. NOT using regex — the patterns are simple enough for direct string parsing.
   - Acceptance: XPath patterns matching Python's subset work correctly
   - Commit: `feat(stdlib): implement xml XPath pattern matching`

5. **Implement ElementTree class** — `src/Sharpy.Stdlib/Xml/ElementTree.cs`
   - Create `[SharpyModuleType("xml")]` sealed class `ElementTree`:
     - Internal storage: `XDocument _document`
     - Constructor: `ElementTree(Element? root = null)` — creates XDocument with optional root
     - Properties:
       - `Element GetRoot()` — returns root element (throws if no root)
     - Methods:
       - `static ElementTree Parse(string source)` — parse from file path. Wraps `XDocument.Load()`, catches `XmlException` → `ParseError`
       - `static ElementTree ParseString(string text)` — parse from string. Wraps `XDocument.Parse()`, catches `XmlException` → `ParseError`
       - `Element? Find(string path, Dict<string, string>? namespaces = null)` — delegates to root
       - `List<Element> FindAll(string path, Dict<string, string>? namespaces = null)` — delegates to root
       - `IEnumerable<Element> Iter(string? tag = null)` — delegates to root
       - `IEnumerable<Element> IterFind(string path, Dict<string, string>? namespaces = null)` — delegates to root
       - `void Write(string filePath, bool xmlDeclaration = true, string encoding = "utf-8", string method = "xml")` — write to file
         - `xmlDeclaration=true` → prepend `<?xml version="1.0" encoding="..."?>`
         - `method="xml"` → standard XML serialization. `method="html"` → self-closing tags like `<br>` instead of `<br />`
   - Acceptance: ElementTree compiles, parse/write roundtrip works
   - Commit: `feat(stdlib): implement xml ElementTree class`

### Phase 2: xml Module — Module Functions and Completion

**Goal:** Add module-level factory functions and utilities.

#### Tasks

6. **Implement xml module-level functions** — `src/Sharpy.Stdlib/Xml/XmlFunctions.cs`
   - Implement as `public static partial class Xml`:
     - `Element Element(string tag, Dict<string, string>? attrib = null)` — create new Element
     - `Element SubElement(Element parent, string tag, Dict<string, string>? attrib = null)` — create element and append to parent
     - `ElementTree Parse(string source)` — parse file, return ElementTree (delegates to `ElementTree.Parse`)
     - `Element FromString(string text)` — parse string, return root Element. Wraps `XDocument.Parse()`, extracts root.
     - `string ToString(Element element, string encoding = "unicode", string method = "xml")` — serialize element to string
       - `encoding="unicode"` → return string directly
       - `encoding="utf-8"` → return string with `<?xml ...?>` declaration
       - `method="xml"` → standard. `method="html"` → no self-closing for void elements
     - `Element Comment(string text)` — create element with tag `Comment` and text (matching Python's `ET.Comment`)
     - `Element ProcessingInstruction(string target, string? text = null)` — create PI element
     - `void Indent(Element element, string space = "  ", int level = 0)` — in-place indentation by inserting text/tail whitespace
     - `void IndentTree(ElementTree tree, string space = "  ", int level = 0)` — indent the tree's root
     - `bool IsElement(object? obj)` — `obj is Element`
     - `void RegisterNamespace(string prefix, string uri)` — register prefix→namespace mapping for serialization
   - Implementation notes:
     - `Comment` and `ProcessingInstruction`: in Python, these are factory functions that return `Element` instances with special tags. `ET.Comment("text")` returns an Element with `tag` set to the `Comment` function itself (a callable). In Sharpy, use sentinel string tags: `tag = "<!--"` for comments, `tag = "<?"` for PIs. `ToString` detects these and serializes appropriately.
     - `Indent`: walk tree recursively, set `text` and `tail` of elements to add newlines and indentation. Match Python 3.9+ `ET.indent()` behavior exactly.
   - Acceptance: all functions compile and match Python behavior
   - Commit: `feat(stdlib): implement xml module-level functions`

7. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Xml.csproj`
   - Copy pattern from `Sharpy.Stdlib.Json.csproj`
   - Set `<AssemblyName>Sharpy.Stdlib.Xml</AssemblyName>`
   - Set `<Compile Include="../Xml/**/*.cs" />`
   - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Xml.csproj` succeeds
   - Commit: `build(stdlib): add Sharpy.Stdlib.Xml project file`

8. **Create spy stub file** — `src/Sharpy.Stdlib/spy/xml_module.spy`
   - Write Sharpy source defining the module-level function signatures and type exports
   - Types: `Element`, `ElementTree`, `ParseError`
   - Functions: `parse`, `fromstring`, `tostring`, `Element`, `SubElement`, `Comment`, `ProcessingInstruction`, `indent`, `iselement`, `register_namespace`
   - Acceptance: file defines all signatures with correct types
   - Commit: `feat(stdlib): add xml module spy source`

9. **Add xml module tests** — `src/Sharpy.Stdlib.Tests/XmlModuleTests.cs`
   - Test parsing:
     - `FromString("<root><child>text</child></root>")` → root.tag is "root", child.text is "text"
     - `FromString` with namespaces → tag is `{uri}tag`
     - `FromString` with attributes → attrib dict has correct key-value pairs
     - `Parse` from file path (write temp file, parse, verify)
     - Invalid XML → throws `ParseError`
   - Test Element navigation:
     - Iteration over children
     - `len(element)` returns child count
     - `element[0]`, `element[-1]` indexing
     - `Find("child")` returns first match
     - `FindAll("child")` returns all matches
     - `FindText("child")` returns text content
     - `Iter()` iterates all descendants
     - `Iter("tag")` filters by tag
     - `IterText()` iterates all text content
   - Test Element construction:
     - `Element("tag")` creates empty element
     - `SubElement(parent, "child")` creates and appends
     - `Append`, `Insert`, `Remove`, `Clear`
   - Test attributes:
     - `Get("attr")`, `Get("missing", "default")`
     - `Set("attr", "value")`
     - `Keys()`, `Items()`
     - `Attrib` dict read/write
   - Test text/tail:
     - `text` property read/write
     - `tail` property for mixed content: `<a><b/>tail</a>`
   - Test XPath patterns:
     - `find("child")` — direct child
     - `find("*")` — all direct children (first)
     - `findall(".//tag")` — all descendants
     - `findall("[@attr]")` — attribute presence
     - `findall("[@attr='value']")` — attribute value
     - `findall("[child]")` — has child element
   - Test serialization:
     - `ToString(element)` matches expected XML string
     - `Comment("text")` → `"<!--text-->"`
     - `ProcessingInstruction("target", "data")` → `"<?target data?>"`
     - Roundtrip: `FromString(ToString(element))` preserves structure
   - Test ElementTree:
     - `Parse` from file
     - `GetRoot()` returns root element
     - `Write()` to file with xml_declaration
     - `Find`/`FindAll` delegation
   - Test utilities:
     - `Indent()` adds proper whitespace
     - `IsElement(element)` → true
     - `IsElement("string")` → false
   - Test namespaces:
     - `{uri}tag` notation in find/findall
     - `RegisterNamespace` affects serialization prefix
   - Acceptance: all tests pass
   - Commit: `test(stdlib): add xml module tests`

### Phase 3: html Module

**Goal:** Implement `html` — HTML escaping/unescaping and event-driven parser. Medium module (~500 lines).

#### Tasks

10. **Create html module directory and registration** — `src/Sharpy.Stdlib/Html/__Init__.cs`
    - Create `Html/` directory under `src/Sharpy.Stdlib/`
    - Add `__Init__.cs` with `[SharpyModule("html")]` on `public static partial class Html`
    - Follow exact pattern from `src/Sharpy.Stdlib/Json/__Init__.cs`
    - Acceptance: `Html` partial class compiles with `[SharpyModule]` attribute
    - Commit: `feat(stdlib): scaffold html module registration`

11. **Implement html escape/unescape functions** — `src/Sharpy.Stdlib/Html/HtmlFunctions.cs`
    - Implement as `public static partial class Html`:
      - `string Escape(string s, bool quote = true)` → wraps `WebUtility.HtmlEncode(s)`:
        - `WebUtility.HtmlEncode` escapes `<`, `>`, `&`, `"` (as `&quot;`). It does NOT escape `'` by default.
        - When `quote=true` (default): additionally replace `'` with `&#x27;` (matching Python's behavior where `quote=True` escapes both `"` and `'`)
        - When `quote=false`: post-process to replace `&quot;` back to `"` (don't escape quotes at all, matching Python's `quote=False` which escapes neither)
        - Python reference (verified):
          - `html.escape('<script>')` → `'&lt;script&gt;'`
          - `html.escape('"hello"')` → `'&quot;hello&quot;'`
          - `html.escape('"hello"', quote=False)` → `'"hello"'`
      - `string Unescape(string s)` → wraps `WebUtility.HtmlDecode(s)`:
        - Handles named references (`&amp;`, `&lt;`, etc.)
        - Handles decimal numeric references (`&#60;`)
        - Handles hex numeric references (`&#x3c;`)
        - Unknown named references are left unchanged (matching Python)
        - Python reference (verified):
          - `html.unescape('&lt;b&gt; &amp; &#60; &#x3c;')` → `'<b> & < <'`
    - Acceptance: escape and unescape compile and match Python behavior
    - Commit: `feat(stdlib): implement html escape and unescape`

12. **Implement HTMLParser class** — `src/Sharpy.Stdlib/Html/HTMLParser.cs`
    - Create `[SharpyModuleType("html")]` class `HTMLParser`:
      - Constructor: `HTMLParser(bool convertCharrefs = true)`
        - `convertCharrefs=true` (default, matching Python 3.4+): automatically convert character references and deliver them as data via `HandleData`, instead of calling `HandleEntityref`/`HandleCharref`
      - Properties:
        - `(int, int) Getpos()` — return `(line, column)` of current position (1-based, matching Python)
      - Public methods:
        - `void Feed(string data)` — feed HTML string to the parser. Calls handle_* methods as tags are encountered.
        - `void Close()` — flush any remaining buffered data. Called when all data has been fed.
        - `void Reset()` — reset parser state (called by constructor, can be called manually)
        - `string? GetStarttagText()` — return the text of the most recently opened start tag
      - Virtual callback methods (users override these):
        - `virtual void HandleStarttag(string tag, List<(string, string?)> attrs)` — opening tag
        - `virtual void HandleEndtag(string tag)` — closing tag
        - `virtual void HandleStartendtag(string tag, List<(string, string?)> attrs)` — self-closing tag (e.g., `<br/>`)
        - `virtual void HandleData(string data)` — character data
        - `virtual void HandleComment(string data)` — comment (`<!-- ... -->`)
        - `virtual void HandleEntityref(string name)` — named entity reference (only when `convertCharrefs=false`)
        - `virtual void HandleCharref(string name)` — numeric character reference (only when `convertCharrefs=false`)
        - `virtual void HandleDecl(string decl)` — DOCTYPE declaration
        - `virtual void HandlePi(string data)` — processing instruction
      - Internal parsing:
        - State machine with states: `Data`, `TagOpen`, `TagName`, `BeforeAttrName`, `AttrName`, `AfterAttrName`, `BeforeAttrValue`, `AttrValueDoubleQuoted`, `AttrValueSingleQuoted`, `AttrValueUnquoted`, `AfterAttrValue`, `SelfClosingTag`, `EndTagOpen`, `EndTagName`, `Comment`, `CommentDash`, `CommentEnd`, `Declaration`, `CDataContent`, `ProcessingInstruction`
        - CDATA content elements: `script`, `style`, `textarea`, `title` — content inside these is delivered as raw data, tags are not parsed
        - Void elements (self-closing in HTML5): `area`, `base`, `br`, `col`, `embed`, `hr`, `img`, `input`, `link`, `meta`, `param`, `source`, `track`, `wbr` — `<br>` without `/` calls `HandleStarttag`, not `HandleStartendtag` (matching Python behavior where only explicit `<br/>` triggers startend)
        - Attribute parsing: unquoted values, single-quoted, double-quoted, valueless attributes (`<input disabled>` → `("disabled", None)`)
        - Entity handling (when `convertCharrefs=true`): `&amp;` in text → deliver `&` via `HandleData`. When `convertCharrefs=false`: deliver `amp` via `HandleEntityref`
        - Position tracking: maintain `_line` and `_column` counters for `Getpos()`
        - Tolerant parsing: bare `<` that doesn't start a valid tag is delivered as data. Unclosed tags are tolerated.
      - Python reference behaviors (verified):
        - `<br>` → `HandleStarttag("br", [])` (no self-closing in HTML mode)
        - `<br/>` → `HandleStartendtag("br", [])` (explicit self-close)
        - `<br />` → `HandleStartendtag("br", [])` (explicit self-close with space)
        - `<p class="main">` → `HandleStarttag("p", [("class", "main")])`
        - `<script>var x = 1 < 2;</script>` → `HandleStarttag("script", [])`, `HandleData("var x = 1 < 2;")`, `HandleEndtag("script")`
        - `<!-- comment -->` → `HandleComment(" comment ")`
        - `<!DOCTYPE html>` → `HandleDecl("DOCTYPE html")`
    - Acceptance: HTMLParser compiles, all callback methods work correctly
    - Commit: `feat(stdlib): implement html HTMLParser class`

13. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Html.csproj`
    - Copy pattern from `Sharpy.Stdlib.Json.csproj`
    - Set `<AssemblyName>Sharpy.Stdlib.Html</AssemblyName>`
    - Set `<Compile Include="../Html/**/*.cs" />`
    - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Html.csproj` succeeds
    - Commit: `build(stdlib): add Sharpy.Stdlib.Html project file`

14. **Create spy stub file** — `src/Sharpy.Stdlib/spy/html_module.spy`
    - Write Sharpy source defining the module-level function signatures and type exports
    - Types: `HTMLParser`
    - Functions: `escape(s: str, quote: bool = True) -> str`, `unescape(s: str) -> str`
    - Acceptance: file defines all signatures with correct types
    - Commit: `feat(stdlib): add html module spy source`

15. **Add html module tests** — `src/Sharpy.Stdlib.Tests/HtmlModuleTests.cs`
    - Test escape/unescape:
      - Basic escape: `"<script>alert(1)</script>"` → `"&lt;script&gt;alert(1)&lt;/script&gt;"`
      - Escape with quotes (default): `"\"hello\""` → `"&quot;hello&quot;"`
      - Escape with quote=false: `"\"hello\""` → `"\"hello\""`
      - Escape single quotes: `"it's"` → `"it&#x27;s"` (when quote=true)
      - Escape ampersand: `"a & b"` → `"a &amp; b"`
      - Unescape named: `"&lt;b&gt;"` → `"<b>"`
      - Unescape decimal: `"&#60;"` → `"<"`
      - Unescape hex: `"&#x3c;"` → `"<"`
      - Roundtrip: `unescape(escape(s))` ≈ `s` for safe strings
      - Empty string: `""` → `""`
      - No-op: string with no special chars passes through unchanged
    - Test HTMLParser callbacks:
      - Start tag: `<p class="main">` triggers HandleStarttag with correct attrs
      - End tag: `</p>` triggers HandleEndtag
      - Self-closing: `<br/>` triggers HandleStartendtag
      - Data: `"Hello"` triggers HandleData
      - Comment: `<!-- comment -->` triggers HandleComment
      - Entity ref (convertCharrefs=false): `&amp;` triggers HandleEntityref
      - Char ref (convertCharrefs=false): `&#60;` triggers HandleCharref
      - DOCTYPE: `<!DOCTYPE html>` triggers HandleDecl
      - Convert charrefs (default): `&amp;` in text delivered as `&` via HandleData
    - Test HTML edge cases:
      - Script content: `<script>1 < 2</script>` — `<` inside script is data, not tag
      - Style content: `<style>.a > .b {}</style>` — `>` inside style is data
      - Bare `<br>` (no closing slash): triggers HandleStarttag, NOT HandleStartendtag
      - Attributes: unquoted `<p class=main>`, single-quoted `<p class='main'>`, valueless `<input disabled>`
      - Malformed: bare `<` delivered as data
      - Multiple feeds: feeding in chunks produces same results as one feed
      - Unicode: non-ASCII content handled correctly
      - Empty tags: `<p></p>` triggers start then end
      - Nested tags: `<div><p>text</p></div>` triggers correct sequence
    - Test Getpos:
      - Position tracking returns correct (line, column) after parsing
    - Acceptance: all tests pass
    - Commit: `test(stdlib): add html module tests`

### Phase 4: Documentation

**Goal:** Add batch plan doc for reference.

#### Tasks

16. **Add Batch 7 plan to docs** — `docs/stdlib/batch7-plan.md`
    - Save this plan (cleaned up) as the batch plan document in the docs directory
    - Follow the same format as `docs/stdlib/batch1-plan.md` and `docs/stdlib/batch5-plan.md`
    - Acceptance: document exists with correct content
    - Commit: `docs(stdlib): add Batch 7 implementation plan for xml, html`

## Testing Strategy

### New test fixtures needed

- `src/Sharpy.Stdlib.Tests/XmlModuleTests.cs` — ~35 tests covering Element, ElementTree, module functions, XPath, namespaces
- `src/Sharpy.Stdlib.Tests/HtmlModuleTests.cs` — ~30 tests covering escape/unescape, HTMLParser callbacks, edge cases

### Edge cases to cover

**xml:**
- Empty elements (`<br/>` roundtrip)
- Deeply nested elements
- Namespace declarations and prefixed tags
- Mixed content (text interleaved with child elements)
- Text and tail property interaction
- Attributes with special characters (quotes, ampersands)
- Unicode tag names and text content
- Large document parsing
- XPath with multiple predicates
- Comment and ProcessingInstruction serialization

**html:**
- Script/style tag content with `<` and `>` characters
- CDATA sections
- Bare `<` not starting a valid tag
- Attribute values with entities inside
- Self-closing tags with/without space before `/>`
- Multiple feeds (chunked input)
- Empty feed
- Reset and re-feed
- Unicode content in attributes and text
- Nested quotes in attributes

### Negative test cases

- `xml.fromstring` with malformed XML → `ParseError`
- `xml.parse` with non-existent file → `FileNotFoundError` (or OS-appropriate error)
- `Element[out_of_range]` → `IndexError`
- `ElementTree.getroot()` on empty tree → appropriate error
- `Find` with invalid XPath pattern → clear error

## Issues to Close

- #751 — xml module (closed by Phase 2, Task 6 — full module implementation)
- #750 — html module (closed by Phase 3, Task 12 — full module implementation)

