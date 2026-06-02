# Style Guide: common-utilities

## Unique Conventions

### Interface + Implementation Pairs
Every utility in `Common` has a matching interface:
- `IPasswordHash` / `PasswordHash`
- `IFileHelper` / `FileHelper`
- `IHttpClientHelper` / `HttpClientHelper`
- `IValidatorHelper` / `ValidatorHelper`

The interface is always in the same folder as the implementation.

### PBKDF2 Password Hash Format
`PasswordHash.CreateHash` returns a colon-delimited string: `{iterations}:{base64salt}:{base64hash}`. This format is validated and split by `ValidatePassword`:
```csharp
return PBKDF2_ITERATIONS + ":" +
    Convert.ToBase64String(salt) + ":" +
    Convert.ToBase64String(hash);
```

### SlowEquals for Timing-Safe Comparison
Password comparison uses a constant-time byte comparison to resist timing attacks:
```csharp
private static bool SlowEquals(byte[] a, byte[] b)
{
    uint diff = (uint)a.Length ^ (uint)b.Length;
    for (int i = 0; i < a.Length && i < b.Length; i++)
        diff |= (uint)(a[i] ^ b[i]);
    return diff == 0;
}
```

### Registered in CommonResolver
All common utilities are registered as Scoped in `CommonResolver.ResolveCommonServices`. `IPasswordHash` is the exception — it is Singleton (registered directly in `Program.cs`).
