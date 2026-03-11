# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T05:02:27.111881
**Type:** compilation_failed
**Feature Focus:** null_conditional
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex null conditional test: Document processing pipeline with null propagation

@abstract
class DocumentElement:
    @abstract
    def get_content(self) -> str: ...

class HeaderElement(DocumentElement):
    title: str?
    level: int

    def __init__(self, title: str?, level: int):
        self.title = title
        self.level = level

    @override
    def get_content(self) -> str:
        prefix: str = "#" * self.level
        return prefix + " " + (self.title ?? "Untitled")

class ParagraphElement(DocumentElement):
    text: str?

    def __init__(self, text: str?):
        self.text = text

    @override
    def get_content(self) -> str:
        return self.text ?? "(empty)"

class LinkElement(DocumentElement):
    url: str?
    label: str?

    def __init__(self, url: str?, label: str?):
        self.url = url
        self.label = label

    @override
    def get_content(self) -> str:
        link_text: str = self.label ?? self.url ?? "broken_link"
        return "[" + link_text + "]"

class Document:
    header: HeaderElement?
    body: list[DocumentElement]

    def __init__(self):
        self.header = None()
        self.body = []

    def summary_length(self) -> int:
        # Use explicit unwrap check instead of ?.len()
        title: str? = self.header?.title
        if title is not None:
            return title.len()
        return 0

def describe_document(doc: Document?) -> str:
    if doc is None:
        return "no document"
    # Avoid type narrowing issue: store result of ?. call
    temp_header: HeaderElement? = doc.header
    if temp_header is None:
        return "no header"
    header_line: str = temp_header.get_content()
    return header_line

def get_text_from_paragraph(elem: DocumentElement?) -> str:
    if elem is not None:
        result: str = elem.get_content()
        return result
    return "not_a_paragraph"

def main():
    doc1 = Document()
    doc1.header = HeaderElement("Chapter 1", 1)
    doc1.body.append(ParagraphElement("Once upon a time..."))
    doc1.body.append(LinkElement("http://example.com", None()))

    doc2 = Document()
    doc2.header = HeaderElement(None(), 2)

    doc3: Document? = None()

    print("=== Test: Deep Chains ===")
    length1: int = doc1.summary_length()
    print(length1)
    length2: int = doc2.summary_length()
    print(length2)
    length3: int = doc3?.summary_length() ?? -1
    print(length3)

    print("=== Test: Control Flow ===")
    desc1: str = describe_document(doc1)
    print(desc1)
    desc3: str = describe_document(doc3)
    print(desc3)

    print("=== Test: List Processing ===")
    docs: list[Document?] = []
    docs.append(doc1)
    docs.append(None())
    docs.append(doc2)

    for opt_doc in docs:
        info: str = opt_doc?.header?.title ?? "no_title"
        print(info)

    print("=== Test: Nested Optional ===")
    link_elem: LinkElement? = None()
    label_check: str? = link_elem?.label
    print(label_check is None)

    link_elem2: LinkElement? = LinkElement("http://test.com", "Test Site")
    label_val: str? = link_elem2?.label
    print(label_val)

    print("=== Test: Chained Methods ===")
    # Avoid covariance issue by not assigning LinkElement? to DocumentElement?
    content1: str? = link_elem2?.get_content()
    print(content1)

    elem2: DocumentElement? = None()
    content2: str? = elem2?.get_content()
    print(content2 is None)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'Optional<DogfoodTest.Document>' does not contain a definition for 'Header' and no accessible extension method 'Header' accepting a first argument of type 'Optional<DogfoodTest.Document>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7_yvvt0q/dogfood_test.spy:63:50
    |
 63 |     temp_header: HeaderElement? = doc.header
    |                                             ^
    |

error[CS1061]: 'Optional<DogfoodTest.HeaderElement>' does not contain a definition for 'GetContent' and no accessible extension method 'GetContent' accepting a first argument of type 'Optional<DogfoodTest.HeaderElement>' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7_yvvt0q/dogfood_test.spy:66:40
    |
 66 |     header_line: str = temp_header.get_content()
    |                                        ^
    |

error[CS1061]: 'string' does not contain a definition for 'Len' and no accessible extension method 'Len' accepting a first argument of type 'string' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmp7_yvvt0q/dogfood_test.spy:56:39
    |
 56 |             return title.len()
    |                               ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp7_yvvt0q/dogfood_test.cs

```

## Timing

- Generation: 610.03s
- Execution: 5.06s
