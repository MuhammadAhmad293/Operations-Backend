namespace Meezan.Dto.DTOs.Category
{
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsProtected { get; set; }
        public List<CategoryDto> Children { get; set; } = new();
    }
}
