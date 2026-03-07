# Successful Dogfood Run

**Timestamp:** 2026-03-07T02:50:26.556716
**Feature Focus:** union_declaration
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Union declaration for HTTP response handling with semantic field names
union HttpResponse:
    case Success(body: str, status_code: int)
    case Redirect(location: str, permanent: bool)
    case ClientError(message: str, code: int)
    case ServerError(message: str, code: int)

def categorize(response: HttpResponse) -> str:
    result: str = ""
    match response:
        case Success(body, status_code):
            if status_code == 200:
                result = "ok"
            else:
                result = "created"
        case Redirect(loc, permanent):
            if permanent:
                result = "moved_permanently"
            else:
                result = "found"
        case ClientError(msg, code):
            result = "client_fault"
        case ServerError(msg, code):
            result = "server_fault"
    return result

def main():
    r1 = HttpResponse.Success("data", 200)
    r2 = HttpResponse.Success("created", 201)
    r3 = HttpResponse.Redirect("/new", True)
    r4 = HttpResponse.Redirect("/temp", False)
    r5 = HttpResponse.ClientError("bad request", 400)
    r6 = HttpResponse.ServerError("internal error", 500)
    print(categorize(r1))
    print(categorize(r2))
    print(categorize(r3))
    print(categorize(r4))
    print(categorize(r5))
    print(categorize(r6))

```

## Output

```
ok
created
moved_permanently
found
client_fault
server_fault
```

## Timing

- Generation: 462.63s
- Execution: 4.57s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
