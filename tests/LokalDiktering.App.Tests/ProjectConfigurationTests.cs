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
        Assert.Contains("Bearbeta text", xaml);
        Assert.Contains("100 % lokalt", xaml);
    }

    [Fact]
    public void MainWorkspace_UsesProgressiveDisclosure()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "LokalDiktering.App", "MainWindow.xaml"));

        Assert.Contains("x:Name=\"AssistantPanel\" Visibility=\"Collapsed\"", xaml);
        Assert.Contains("x:Name=\"SelectionAssistantBar\" Visibility=\"Collapsed\"", xaml);
        Assert.Contains("Content=\"Bearbeta markerad text\"", xaml);
        Assert.Contains("Header=\"Tidigare förslag\"", xaml);
        Assert.Contains("Header=\"Fler alternativ\"", xaml);
        Assert.Contains("<MenuItem Header=\"Inspelningar\"", xaml);
        Assert.Contains("<MenuItem Header=\"Versioner\"", xaml);
        Assert.Contains("<MenuItem Header=\"Ta bort dokument…\"", xaml);
        Assert.DoesNotContain("<ColumnDefinition Width=\"320\"", xaml);
        Assert.DoesNotContain("Content=\"Kopiera allt\" Click=", xaml);
    }

    [Fact]
    public void IconOnlyMainWindowButtons_HaveAccessibleNamesAndTooltips()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "LokalDiktering.App", "MainWindow.xaml");
        var document = System.Xml.Linq.XDocument.Load(path);
        var presentation = System.Xml.Linq.XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");

        var iconButtons = document.Descendants(presentation + "Button")
            .Where(element => (string?)element.Attribute("Style") == "{StaticResource IconButton}")
            .ToArray();

        Assert.NotEmpty(iconButtons);
        foreach (var button in iconButtons)
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)button.Attribute("ToolTip")));
            Assert.False(string.IsNullOrWhiteSpace(
                (string?)button.Attributes().FirstOrDefault(attribute =>
                    attribute.Name.LocalName == "AutomationProperties.Name")));
        }
    }

    [Fact]
    public void FirstRun_DoesNotShutDownBeforeMainWindowOpens()
    {
        var root = FindRepositoryRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "src", "LokalDiktering.App", "App.xaml"));
        var startup = File.ReadAllText(Path.Combine(root, "src", "LokalDiktering.App", "App.xaml.cs"));

        Assert.Contains("ShutdownMode=\"OnExplicitShutdown\"", appXaml);
        var showIndex = startup.IndexOf("MainWindow.Show();", StringComparison.Ordinal);
        var shutdownModeIndex = startup.IndexOf(
            "ShutdownMode = ShutdownMode.OnMainWindowClose;",
            StringComparison.Ordinal);
        Assert.True(showIndex >= 0);
        Assert.True(shutdownModeIndex > showIndex);
    }

    [Fact]
    public void WpfBindings_UseSwedishFormatting()
    {
        var root = FindRepositoryRoot();
        var startup = File.ReadAllText(Path.Combine(root, "src", "LokalDiktering.App", "App.xaml.cs"));

        Assert.Contains("CultureInfo.GetCultureInfo(\"sv-SE\")", startup);
        Assert.Contains("FrameworkElement.LanguageProperty.OverrideMetadata", startup);
        Assert.Contains("XmlLanguage.GetLanguage(swedish.IetfLanguageTag)", startup);
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
