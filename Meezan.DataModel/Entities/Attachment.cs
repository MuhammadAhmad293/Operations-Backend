using Meezan.DataModel.Base;

namespace Meezan.DataModel.Entities
{
    public class Attachment : BaseEntity
    {
        public int Id { get; set; }
        public int TransactionId { get; set; }
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public int SizeBytes { get; set; }
        public string StoragePath { get; set; }
        public Transaction Transaction { get; set; }
    }
}
