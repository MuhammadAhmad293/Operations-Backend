using Meezan.DataModel.Base;

namespace Meezan.DataModel.Entities
{
    public class ProcessedMessage : BaseEntity
    {
        public int Id { get; set; }
        public string MessageId { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}