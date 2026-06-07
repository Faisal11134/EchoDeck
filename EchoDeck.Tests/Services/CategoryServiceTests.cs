using System.IO;
using System.Text.Json;
using EchoDeck.App.Models;
using EchoDeck.App.Services;

namespace EchoDeck.Tests.Services;

public sealed class CategoryServiceTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;

    public CategoryServiceTests(TestFixture fixture)
    {
        _fixture = fixture;
        foreach (var file in Directory.GetFiles(_fixture.Paths.DataFolder, "*.json"))
            try { File.Delete(file); } catch { }
    }

    [Fact]
    public async Task LoadAsync_NoFile_CreatesDefaults()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.NotEmpty(service.Categories);
        Assert.Contains(service.CategoryNames, n => n == "Uncategorized");
        Assert.Contains(service.CategoryNames, n => n == "Memes");
        Assert.Contains(service.CategoryNames, n => n == "Gaming");
        Assert.Contains(service.Categories, c => c.Name == "Uncategorized" && c.SortOrder == 999);
    }

    [Fact]
    public async Task AddCategory_ValidName_AddsAndReturnsTrue()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        var result = service.AddCategory("TestCategory");

        Assert.True(result);
        Assert.Contains(service.CategoryNames, n => n == "TestCategory");
    }

    [Fact]
    public async Task AddCategory_DuplicateName_ReturnsFalse()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        service.AddCategory("TestCategory");
        var result = service.AddCategory("TestCategory");

        Assert.False(result);
    }

    [Fact]
    public async Task AddCategory_Uncategorized_ReturnsFalse()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        var result = service.AddCategory("Uncategorized");

        Assert.False(result);
    }

    [Fact]
    public async Task AddCategory_EmptyName_ReturnsFalse()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.False(service.AddCategory(""));
        Assert.False(service.AddCategory("   "));
        Assert.False(service.AddCategory(null!));
    }

    [Fact]
    public async Task RemoveCategory_Existing_RemovesAndReturnsTrue()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        service.AddCategory("ToRemove");
        var result = service.RemoveCategory("ToRemove");

        Assert.True(result);
        Assert.DoesNotContain(service.CategoryNames, n => n == "ToRemove");
    }

    [Fact]
    public async Task RemoveCategory_NonExisting_ReturnsFalse()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.False(service.RemoveCategory("NonExistent"));
    }

    [Fact]
    public async Task RemoveCategory_Uncategorized_ReturnsFalse()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.False(service.RemoveCategory("Uncategorized"));
    }

    [Fact]
    public async Task RenameCategory_Valid_UpdatesName()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        service.AddCategory("OldName");
        var result = service.RenameCategory("OldName", "NewName");

        Assert.True(result);
        Assert.DoesNotContain(service.CategoryNames, n => n == "OldName");
        Assert.Contains(service.CategoryNames, n => n == "NewName");
    }

    [Fact]
    public async Task RenameCategory_Uncategorized_ReturnsFalse()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.False(service.RenameCategory("Uncategorized", "RenamedUncat"));
    }

    [Fact]
    public async Task RenameCategory_DuplicateNewName_ReturnsFalse()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        service.AddCategory("CatA");
        service.AddCategory("CatB");
        var result = service.RenameCategory("CatA", "CatB");

        Assert.False(result);
    }

    [Fact]
    public async Task MoveCategoryUp_FirstItem_ReturnsFalse()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        var first = service.Categories[0];
        var result = service.MoveCategoryUp(first.Name);

        Assert.False(result);
        Assert.Equal(first, service.Categories[0]);
    }

    [Fact]
    public async Task MoveCategoryUp_SecondItem_Succeeds()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        var second = service.Categories[1];
        var result = service.MoveCategoryUp(second.Name);

        Assert.True(result);
        Assert.Equal(second, service.Categories[0]);
    }

    [Fact]
    public async Task MoveCategoryUp_NonExistent_ReturnsFalse()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.False(service.MoveCategoryUp("NonExistent"));
    }

    [Fact]
    public async Task MoveCategoryDown_LastItem_ReturnsFalse()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        var last = service.Categories[^1];
        var result = service.MoveCategoryDown(last.Name);

        Assert.False(result);
        Assert.Equal(last, service.Categories[^1]);
    }

    [Fact]
    public async Task MoveCategoryDown_MiddleItem_Succeeds()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        var memes = service.Categories[1];
        var result = service.MoveCategoryDown(memes.Name);

        Assert.True(result);
        Assert.Equal(memes, service.Categories[2]);
    }

    [Fact]
    public async Task MoveCategoryDown_NonExistent_ReturnsFalse()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.False(service.MoveCategoryDown("NonExistent"));
    }

    [Fact]
    public async Task SaveAndLoad_PersistsCategories()
    {
        var service1 = new CategoryService(_fixture.Paths);
        await service1.LoadAsync();
        service1.AddCategory("PersistedCat");
        await service1.SaveAsync();

        var service2 = new CategoryService(_fixture.Paths);
        await service2.LoadAsync();

        Assert.Contains(service2.CategoryNames, n => n == "PersistedCat");
    }

    [Fact]
    public async Task GetCategoryId_Found_ReturnsId()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        service.AddCategory("TestCat");
        var id = service.GetCategoryId("TestCat");

        Assert.NotNull(id);
        Assert.NotEmpty(id);
    }

    [Fact]
    public async Task GetCategoryId_NotFound_ReturnsNull()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.Null(service.GetCategoryId("NonExistent"));
        Assert.Null(service.GetCategoryId(""));
        Assert.Null(service.GetCategoryId(null!));
    }

    [Fact]
    public async Task GetCategoryName_Found_ReturnsName()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        var cat = service.Categories.First();
        var name = service.GetCategoryName(cat.Id);

        Assert.Equal(cat.Name, name);
    }

    [Fact]
    public async Task GetCategoryName_NotFound_ReturnsNull()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.Null(service.GetCategoryName("nonexistent-id"));
    }

    [Fact]
    public async Task CategoryNames_ReturnsAllNames()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        var names = service.CategoryNames;
        Assert.Equal(service.Categories.Count, names.Length);
        Assert.All(names, n => Assert.NotEmpty(n));
    }

    [Fact]
    public async Task Categories_AreInInsertionOrder()
    {
        var service = new CategoryService(_fixture.Paths);
        await service.LoadAsync();

        var countBefore = service.Categories.Count;
        service.AddCategory("First");
        service.AddCategory("Second");

        Assert.Equal("First", service.Categories[countBefore].Name);
        Assert.Equal("Second", service.Categories[countBefore + 1].Name);
    }
}
