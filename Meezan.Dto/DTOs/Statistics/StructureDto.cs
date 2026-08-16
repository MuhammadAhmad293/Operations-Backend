namespace Meezan.Dto.DTOs.Statistics
{
    // UC-20 Structure: donut data for one Income/Expense tab. Acceptance criterion: Categories'
    // Percent values sum to exactly 100 for the tab (see StatisticsService for the rounding rule).
    public class StructureDto
    {
        public string Kind { get; set; }
        public List<StructureCategoryDto> Categories { get; set; } = new();
    }
}
