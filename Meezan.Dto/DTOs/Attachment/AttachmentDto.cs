namespace Meezan.Dto.DTOs.Attachment
{
    public class AttachmentDto
    {
        public int Id { get; set; }
        public int TransactionId { get; set; }
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public int SizeBytes { get; set; }
    }
}
