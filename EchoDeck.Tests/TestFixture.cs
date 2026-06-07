using System.IO;
using EchoDeck.App.Infrastructure;

namespace EchoDeck.Tests;

public sealed class TestFixture : IDisposable
{
    public string TempRoot { get; }
    public AppPaths Paths { get; }

    public TestFixture()
    {
        TempRoot = Path.Combine(Path.GetTempPath(), "SoundboardVM_Tests_" + Guid.NewGuid().ToString("N"));
        Paths = new AppPaths(TempRoot);
        Paths.EnsureCreated();
    }

    public void Dispose()
    {
        try { Directory.Delete(TempRoot, recursive: true); } catch { }
    }
}
