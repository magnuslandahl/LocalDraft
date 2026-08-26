namespace LokalDiktering.App.Tests;

public sealed class ProjectConfigurationTests
{
    [Fact]
    public void AppProject_UsesRequiredPortableSettings()
    {
        var root = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(root, "src", "LokalDiktering.App", "LokalDiktering.App.csproj"));
        Assert.Contains("<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>", project);
        Assert.Contains("<RuntimeIdentifier>win-x64</RuntimeIdentifier>", project);
        Assert.Contains("<SelfContained>true</SelfContained>", project);
        Assert.Contains("<PublishSingleFile>false</PublishSingleFile>", project);
        Assert.Contains("<PublishTrimmed>false</PublishTrimmed>", project);
    }

    [Fact]
    public async Task DeletingFilteredCurrentDocument_SelectsRemainingDocument()
    {
        var root = Path.Combine(Path.GetTempPath(), "LokalDikteringAppTests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LokalDiktering.Infrastructure.AppPathService(root);
            var repository = new LokalDiktering.Infrastructure.DocumentRepository(paths);
            var viewModel = new LokalDiktering.App.MainWindowViewModel(
                repository,
                new LokalDiktering.Infrastructure.VersionService(paths));
            await repository.CreateAsync("Behåll");
            var deleted = await repository.CreateAsync("Ta bort");
            await viewModel.RefreshAsync(deleted.Metadata.Id);
            await viewModel.OpenAsync(deleted.Metadata.Id);
            viewModel.Filter = "inget synligt";

            await viewModel.DeleteCurrentAsync();

            Assert.NotNull(viewModel.Current);
            Assert.Equal("Behåll", viewModel.Current.Metadata.Title);
            Assert.Empty(viewModel.Filter);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void NormalWorkflow_DoesNotUseEnglishMainLabels()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "LokalDiktering.App", "MainWindow.xaml"));
        Assert.Contains("Nytt dokument", xaml);
        Assert.Contains("Diktera", xaml);
        Assert.Contains("Textassistent", xaml);
        Assert.Contains("100 % lokalt", xaml);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LokalDiktering.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
