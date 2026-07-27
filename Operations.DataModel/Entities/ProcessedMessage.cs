using Operations.DataModel.Base;

namespace Operations.DataModel.Entities
{
    public class ProcessedMessage : BaseEntity
    {
        public int Id { get; set; }
        public string MessageId { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}