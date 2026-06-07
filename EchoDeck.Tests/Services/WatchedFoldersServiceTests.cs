using System.IO;
using System.Text.Json;
using EchoDeck.App.Services;

namespace EchoDeck.Tests.Services;

public sealed class WatchedFoldersServiceTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;

    public WatchedFoldersServiceTests(TestFixture fixture)
    {
        _fixture = fixture;
        DeleteDataFiles();
    }

    private void DeleteDataFiles()
    {
        foreach (var file in Directory.GetFiles(_fixture.Paths.DataFolder, "*.json"))
            try { File.Delete(file); } catch { }
    }

    [Fact]
    public async Task LoadAsync_NoFile_CreatesEmpty()
    {
        var service = new WatchedFoldersService(_fixture.Paths);
        await service.LoadAsync();

        Assert.Empty(service.WatchedFolders);
        Assert.True(File.Exists(_fixture.Paths.WatchedFoldersFilePath));
    }

    [Fact]
    public async Task LoadAsync_ExistingFile_LoadsCorrectly()
    {
        var folders = new List<string> { @"C:\test\folder1", @"C:\test\folder2" };
        var json = JsonSerializer.Serialize(folders, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_fixture.Paths.WatchedFoldersFilePath, json);

        var service = new WatchedFoldersService(_fixture.Paths);
        await service.LoadAsync();

        Assert.Equal(2, service.WatchedFolders.Count);
        Assert.Contains(service.WatchedFolders, f => f == @"C:\test\folder1");
    }

    [Fact]
    public async Task AddWatchedFolder_ValidPath_AddsAndReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SBVM_TestWF_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var service = new WatchedFoldersService(_fixture.Paths);
            var result = service.AddWatchedFolder(tempDir, out var stored);

            Assert.True(result);
            Assert.NotNull(stored);
            Assert.Contains(service.WatchedFolders, f => f == stored);
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public async Task AddWatchedFolder_NonExistentPath_ReturnsFalse()
    {
        var service = new WatchedFoldersService(_fixture.Paths);
        var result = service.AddWatchedFolder(@"C:\NonExistentFolder_XYZ123", out _);

        Assert.False(result);
    }

    [Fact]
    public async Task AddWatchedFolder_EmptyPath_ReturnsFalse()
    {
        var service = new WatchedFoldersService(_fixture.Paths);
        Assert.False(service.AddWatchedFolder("", out _));
        Assert.False(service.AddWatchedFolder("   ", out _));
    }

    [Fact]
    public async Task AddWatchedFolder_Duplicate_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SBVM_TestWF2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var service = new WatchedFoldersService(_fixture.Paths);
            service.AddWatchedFolder(tempDir, out _);
            var result = service.AddWatchedFolder(tempDir, out _);

            Assert.False(result);
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public async Task RemoveWatchedFolder_Existing_RemovesAndReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SBVM_TestWF3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var service = new WatchedFoldersService(_fixture.Paths);
            service.AddWatchedFolder(tempDir, out var stored);
            Assert.True(service.WatchedFolders.Count > 0);

            var result = service.RemoveWatchedFolder(stored!);
            Assert.True(result);
            Assert.Empty(service.WatchedFolders);
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public async Task RemoveWatchedFolder_NonExisting_ReturnsFalse()
    {
        var service = new WatchedFoldersService(_fixture.Paths);
        Assert.False(service.RemoveWatchedFolder(@"C:\nonexistent"));
    }

    [Fact]
    public async Task ClearWatchedFolders_EmptiesList()
    {
        var service = new WatchedFoldersService(_fixture.Paths);
        var tempDir = Path.Combine(Path.GetTempPath(), "SBVM_TestWF4_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            service.AddWatchedFolder(tempDir, out _);
            service.ClearWatchedFolders();
            Assert.Empty(service.WatchedFolders);
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public async Task SaveAndLoad_PersistsFolders()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SBVM_TestWF5_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var service1 = new WatchedFoldersService(_fixture.Paths);
            await service1.LoadAsync();
            service1.AddWatchedFolder(tempDir, out _);
            await service1.SaveAsync();

            var service2 = new WatchedFoldersService(_fixture.Paths);
            await service2.LoadAsync();

            Assert.Contains(service2.WatchedFolders, f => f.Equals(tempDir, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public async Task InitializeAsync_SetsStatus()
    {
        var service = new WatchedFoldersService(_fixture.Paths);
        await service.LoadAsync();
        await service.InitializeAsync();

        Assert.NotNull(service.StatusMessage);
    }
}
