namespace SportsCenterManagement.DTOs.Package;

public class PackageResponseDto
{
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationInDays { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> Benefits { get; set; } = new();
    public bool IsActive { get; set; }
}