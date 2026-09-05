using Meezan.DataModel.Base;
using Meezan.DataModel.Enums;

namespace Meezan.DataModel.Entities
{
    public class Category : BaseEntity
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public int? ParentId { get; set; }
        public CategoryKind Kind { get; set; }
        public string Name { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsProtected { get; set; }
        public CategorySystemPurpose? SystemPurpose { get; set; }
        public Account Account { get; set; }
        public Category? Parent { get; set; }
        public ICollection<Category> Children { get; set; }
    }
}
