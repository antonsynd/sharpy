# string

String constants matching Python's string module.
Provides character classification constants for ASCII characters.

```python
import string
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `ascii_lowercase` | `str` | The lowercase letters 'abcdefghijklmnopqrstuvwxyz'. |
| `ascii_uppercase` | `str` | The uppercase letters 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'. |
| `ascii_letters` | `str` | The concatenation of \`ascii_lowercase\` and \`ascii_uppercase\`. |
| `digits` | `str` | The string '0123456789'. |
| `hexdigits` | `str` | The string '0123456789abcdefABCDEF'. |
| `octdigits` | `str` | The string '01234567'. |
| `punctuation` | `str` | String of ASCII characters which are considered punctuation characters in the C locale: !"#$%&'()*+,-./:;<=>?@[\\]^_\`{\|}~ |
| `whitespace` | `str` | A string containing whitespace characters: space, tab, linefeed, return, formfeed, and vertical tab. |
| `printable` | `str` | String of ASCII characters which are considered printable. This is a combination of \`digits\`, \`ascii_letters\`, \`punctuation\`, and \`whitespace\`. |
