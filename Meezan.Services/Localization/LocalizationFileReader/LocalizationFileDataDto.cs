namespace Meezan.Services.Localization.LocalizationFileReader
{
    public class LocalizationFileDataDto
    {
        public string Key { get; set; }
        public Dictionary<string, string> LocalizedValue { get; set; }
    }
}
