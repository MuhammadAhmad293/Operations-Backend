namespace Meezan.Dto.DTOs.Attachment
{
    // Not persisted — the payload for GET /api/attachments/{id}'s file download.
    public class AttachmentContentDto
    {
        public Stream Content { get; set; }
        public string FileName { get; set; }
        public string MimeType { get; set; }
    }
}
