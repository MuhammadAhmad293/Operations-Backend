using Meezan.DataModel.Base;
using Meezan.DataModel.Enums;

namespace Meezan.DataModel.Entities
{
    public class Currency : BaseMultilingualTextEntity
    {
        public string Code { get; set; }
        public CurrencyType Type { get; set; }
        public string Symbol { get; set; }
        public int Decimals { get; set; }
    }
}
