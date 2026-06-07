using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using EchoDeck.App.Infrastructure;
using EchoDeck.App.Models;

namespace EchoDeck.App.Services;

public sealed class CategoryService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppPaths _paths;
    private readonly string _categoriesFilePath;

    public ObservableCollection<Category> Categories { get; } = new();
    public string[] CategoryNames => Categories.Select(c => c.Name).ToArray();

    public CategoryService(AppPaths paths)
    {
        _paths = paths;
        _categoriesFilePath = Path.Combine(_paths.DataFolder, "categories.json");
    }

    public async Task LoadAsync()
    {
        Categories.Clear();

        if (!File.Exists(_categoriesFilePath))
        {
            SeedDefaults();
            await SaveAsync();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_categoriesFilePath);
            var doc = JsonSerializer.Deserialize<CategoriesDocument>(json, SerializerOptions);
            if (doc?.Categories is not null)
            {
                foreach (var cat in doc.Categories.OrderBy(c => c.SortOrder))
                {
                    Categories.Add(cat);
                }
            }

            if (!Categories.Any(c => string.Equals(c.Name, "Uncategorized", StringComparison.OrdinalIgnoreCase)))
            {
                Categories.Add(new Category { Id = "uncategorized", Name = "Uncategorized", SortOrder = 999 });
            }
        }
        catch (Exception)
        {
            Categories.Clear();
            SeedDefaults();
            await SaveAsync();
        }
    }

    public async Task SaveAsync()
    {
        var doc = new CategoriesDocument
        {
            SchemaVersion = 1,
            Categories = Categories.ToList()
        };
        var json = JsonSerializer.Serialize(doc, SerializerOptions);
        var tempPath = _categoriesFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _categoriesFilePath, overwrite: true);
    }

    public string? GetCategoryId(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return Categories
            .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    public string? GetCategoryName(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        return Categories
            .FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
            ?.Name;
    }

    public bool AddCategory(string name)
    {
        if (!IsUsableCategory(name))
            return false;

        if (Categories.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            return false;

        var maxOrder = Categories.Count > 0 ? Categories.Max(c => c.SortOrder) : 0;
        Categories.Add(new Category
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            SortOrder = maxOrder + 1
        });
        return true;
    }

    public bool RemoveCategory(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "Uncategorized", StringComparison.OrdinalIgnoreCase))
            return false;

        var category = Categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (category is null)
            return false;

        return Categories.Remove(category);
    }

    public bool RenameCategory(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            return false;

        if (string.Equals(oldName, "Uncategorized", StringComparison.OrdinalIgnoreCase))
            return false;

        if (Categories.Any(c => string.Equals(c.Name, newName, StringComparison.OrdinalIgnoreCase)))
            return false;

        var category = Categories.FirstOrDefault(c => string.Equals(c.Name, oldName, StringComparison.OrdinalIgnoreCase));
        if (category is null)
            return false;

        category.Name = newName.Trim();
        return true;
    }

    public bool MoveCategoryUp(string name)
    {
        var index = IndexOfCategory(name);
        if (index < 1)
            return false;

        (Categories[index - 1], Categories[index]) = (Categories[index], Categories[index - 1]);
        NormalizeSortOrders();
        return true;
    }

    public bool MoveCategoryDown(string name)
    {
        var index = IndexOfCategory(name);
        if (index < 1 || index >= Categories.Count - 1)
            return false;

        (Categories[index + 1], Categories[index]) = (Categories[index], Categories[index + 1]);
        NormalizeSortOrders();
        return true;
    }

    private void NormalizeSortOrders()
    {
        for (var i = 0; i < Categories.Count; i++)
        {
            Categories[i].SortOrder = Categories[i].Name == "Uncategorized" ? 999 : i;
        }
    }

    private static bool IsUsableCategory(string? category)
        => !string.IsNullOrWhiteSpace(category) && !string.Equals(category, "Uncategorized", StringComparison.OrdinalIgnoreCase);

    private int IndexOfCategory(string name)
    {
        for (var i = 0; i < Categories.Count; i++)
        {
            if (string.Equals(Categories[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private void SeedDefaults()
    {
        Categories.Add(new Category { Id = "uncategorized", Name = "Uncategorized", SortOrder = 999 });
        Categories.Add(new Category { Id = Guid.NewGuid().ToString("N"), Name = "Memes", SortOrder = 0 });
        Categories.Add(new Category { Id = Guid.NewGuid().ToString("N"), Name = "Gaming", SortOrder = 1 });
        Categories.Add(new Category { Id = Guid.NewGuid().ToString("N"), Name = "Anime", SortOrder = 2 });
        Categories.Add(new Category { Id = Guid.NewGuid().ToString("N"), Name = "Arabic", SortOrder = 3 });
        Categories.Add(new Category { Id = Guid.NewGuid().ToString("N"), Name = "Music", SortOrder = 4 });
    }
}

internal sealed class CategoriesDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<Category> Categories { get; set; } = new();
}