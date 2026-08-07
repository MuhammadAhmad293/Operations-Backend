namespace Meezan.DataModel.Base
{
    public class BaseEntity
    {
        public bool IsDeleted { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastModificationTime { get; set; }
    }
}
