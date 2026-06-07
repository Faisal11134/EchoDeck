namespace EchoDeck.App.Models;

public sealed class Category
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
}