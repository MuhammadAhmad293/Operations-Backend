namespace Meezan.Dto.DTOs.Category
{
    public class CreateCategoryDto
    {
        public string Name { get; set; }
        public string? Kind { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public int? ParentId { get; set; }
    }
}
