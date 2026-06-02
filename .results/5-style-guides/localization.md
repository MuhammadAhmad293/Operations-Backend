# Style Guide: localization

## Unique Conventions

### JSON File as Source of Truth
All localizable strings live in `localizationFile.json`. Every entry has exactly two keys: `en` and `ar`:
```json
{ "Key": "GeneralError", "LocalizedValue": { "en": "...", "ar": "..." } }
```

### Service Properties, Not String Keys
Services always call `Localization.GeneralError` (a named property), never `GetKeyValue("GeneralError", ...)` directly. The `ILocalizationService` interface is the consumer-facing API:
```csharp
return response.GetErrorResponse(Localization.GeneralError);
```

### AltValue Parameter
`GetKeyValue(string key, string altValue)` takes a fallback `altValue` parameter; in practice it is always `"altValue"` (a literal placeholder string, not a real fallback message). This is the project-specific convention:
```csharp
public string GeneralError => GetKeyValue("GeneralError", "altValue");
```

### File Loading from Entry Assembly Location
The JSON file path is resolved at runtime relative to the entry assembly's directory:
```csharp
var rootDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
File.ReadAllText($@"{rootDir}\Localization\LocalizationFileReader\{fileName}.json");
```

### Culture Resolution
The culture is read from `CultureInfo.CurrentCulture.Name`, which is set by ASP.NET Core's `UseRequestLocalization` middleware based on the `Accept-Language` header.
