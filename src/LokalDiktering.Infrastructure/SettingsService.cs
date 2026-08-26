using System.Text.Json;
using LokalDiktering.Core;

namespace LokalDiktering.Infrastructure;

public sealed class SettingsService(IAppPathService paths) : ISettingsService
{
    private string SettingsPath => Path.Combine(paths.DataRoot, "Settings", "settings.json");

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(SettingsPath);
        return await JsonSerializer.DeserializeAsync<AppSettings>(
                   stream,
                   AtomicFile.JsonOptions,
                   cancellationToken)
               ?? new AppSettings();
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        AtomicFile.WriteJsonAsync(SettingsPath, settings, paths.DataRoot, cancellationToken);
}
