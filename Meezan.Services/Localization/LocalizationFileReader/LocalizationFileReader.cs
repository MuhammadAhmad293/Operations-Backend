using Newtonsoft.Json;
using System.Globalization;
using System.Reflection;

namespace Meezan.Services.Localization.LocalizationFileReader
{
    public class LocalizationFileReader
    {
        private List<LocalizationFileDataDto> LocalizationDataList { get; set; }
        public LocalizationFileReader(string fileName)
        {
            LoadData(fileName);
        }
        private void LoadData(string fileName)
        {
            var rootDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            LocalizationDataList = JsonConvert.DeserializeObject<List<LocalizationFileDataDto>>(File.ReadAllText($@"{rootDir}\Localization\LocalizationFileReader\{fileName}.json"));
        }
        protected Dictionary<string, string> GetKeyValue(string key)
        {
            return LocalizationDataList.FirstOrDefault(k => k.Key == key).LocalizedValue;
        }

        protected string GetKeyValue(string key, string altValue)
        {
            string value = altValue;

            Dictionary<string, string> localizedData = LocalizationDataList.FirstOrDefault(k => k.Key.ToLower() == key.ToLower()).LocalizedValue;

            if (localizedData != null)
                value = localizedData[CultureInfo.CurrentCulture.Name];
            
            return value;
        }
    }
}
