namespace LocalDraft.Infrastructure.Tests;

public sealed class PrivacyRegressionTests
{
    [Fact]
    public void RuntimeProjects_DoNotContainNetworkOrTelemetryApis()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runtimeFiles = Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, "src"),
                "*.cs",
                SearchOption.AllDirectories)
            .ToArray();
        var forbidden = new[]
        {
            "HttpClient",
            "System.Net.Sockets",
            "TcpListener",
            "TelemetryClient",
            "ApplicationInsights",
            "Sentry",
            "OpenTelemetry"
        };

        var violations = runtimeFiles
            .SelectMany(file => forbidden
                .Where(term => File.ReadAllText(file).Contains(term, StringComparison.Ordinal))
                .Select(term => $"{Path.GetRelativePath(repositoryRoot, file)}: {term}"))
            .ToArray();

        Assert.Empty(violations);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LocalDraft.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repositoryroten hittades inte.");
    }
}
