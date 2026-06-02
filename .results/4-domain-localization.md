# Domain Deep Dive: Localization

## Overview
Localization uses a custom JSON-file-based reader instead of .resx / `IStringLocalizer`. It supports English (`en`) and Arabic (`ar`). The active language is driven by the `Accept-Language` request header.

---

## File Structure

```
Operations.Services/
  Localization/
    ILocalizationService.cs
    LocalizationService.cs
    LocalizationFileReader/
      LocalizationFileReader.cs
      LocalizationFileDataDto.cs
      localizationFile.json
```

---

## localizationFile.json Schema

```json
[
  {
    "Key": "GeneralError",
    "LocalizedValue": {
      "en": "An error has occurred please try again",
      "ar": "حدث خطأ ، يرجى المحاولة مرة أخرى"
    }
  }
]
```

Every key must have both `en` and `ar` entries.

---

## LocalizationFileReader

Loads and deserialises the JSON file at construction time using the entry assembly's location:
```csharp
var rootDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
LocalizationDataList = JsonConvert.DeserializeObject<List<LocalizationFileDataDto>>(
    File.ReadAllText($@"{rootDir}\Localization\LocalizationFileReader\{fileName}.json"));
```

Returns a culture-resolved string via:
```csharp
protected string GetKeyValue(string key, string altValue)
{
    Dictionary<string, string> localizedData = LocalizationDataList
        .FirstOrDefault(k => k.Key.ToLower() == key.ToLower()).LocalizedValue;

    if (localizedData != null)
        value = localizedData[CultureInfo.CurrentCulture.Name];

    return value;
}
```

---

## ILocalizationService

```csharp
public interface ILocalizationService
{
    string GeneralError { get; }
    string GeneralSuccess { get; }
    string InvalidRequest { get; }
    string NoDataFound { get; }
}
```

`LocalizationService` extends `LocalizationFileReader` and implements each property as a `GetKeyValue` call:
```csharp
public string GeneralError => GetKeyValue("GeneralError", "altValue");
```

---

## Culture Middleware Setup

Cultures are configured in `Program.cs`:
```csharp
List<CultureInfo> cultures = new() { new CultureInfo("en"), new CultureInfo("ar") };
app.UseRequestLocalization(option =>
{
    option.DefaultRequestCulture = new RequestCulture("en");
    option.SupportedCultures = cultures;
    option.SupportedUICultures = cultures;
});
```

The `SwaggerHeaderFilter` adds `Accept-Language` to every Swagger operation so developers can test both languages from the UI.

---

## How to Add a New Localizable String

1. Add a new entry to `localizationFile.json` with both `en` and `ar` values.
2. Add a corresponding `string` property to `ILocalizationService`.
3. Implement the property in `LocalizationService` calling `GetKeyValue("NewKey", "altValue")`.
4. Use `Localization.NewKey` in services.

---

## Key Constraints
- Do not use `.resx` files or `IStringLocalizer` — only the custom JSON reader.
- Never hardcode user-facing message strings in services if a localization key exists.
- Both language entries are required for every key; missing entries cause a runtime `KeyNotFoundException`.
