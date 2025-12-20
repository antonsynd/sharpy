# Identity Operators **[v0.1.0]**

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `is` | Identity comparison | `object.ReferenceEquals()` |
| `is not` | Negated identity | `!object.ReferenceEquals()` |
| `is None` | None check | `== null` |
| `is not None` | Non-None check | `!= null` |

*Implementation: ✅ Native for None checks; 🔄 Lowered for general identity.*
