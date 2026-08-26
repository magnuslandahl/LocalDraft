using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LokalDiktering.Core;

namespace LokalDiktering.App;

public partial class MainWindowViewModel(
    IDocumentRepository documents,
    IVersionService versions) : ObservableObject
{
    public ObservableCollection<DocumentSummary> Documents { get; } = [];

    [ObservableProperty]
    private DocumentSummary? selectedDocument;

    [ObservableProperty]
    private string status = "Alla ändringar är sparade";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string filter = string.Empty;

    public LocalDocument? Current { get; private set; }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
        if (Documents.Count == 0)
        {
            await CreateAsync();
        }
        else
        {
            await OpenAsync(Documents[0].Id);
        }
    }

    public async Task CreateAsync()
    {
        Current = await documents.CreateAsync();
        await versions.CommitAsync(Current.Metadata.Id, Current.Content, "Skapat dokument");
        await RefreshAsync(Current.Metadata.Id);
    }

    public async Task OpenAsync(Guid id)
    {
        Current = await documents.LoadAsync(id);
        SelectedDocument = Documents.FirstOrDefault(x => x.Id == id);
    }

    public async Task SaveAsync(
        Guid documentId,
        string title,
        DocumentContent content,
        string? versionReason = null)
    {
        if (Current is null || Current.Metadata.Id != documentId)
        {
            throw new InvalidOperationException("Dokumentet byttes innan det kunde sparas.");
        }

        Status = "Sparar…";
        await documents.SaveAsync(documentId, title, content);
        var saved = await documents.LoadAsync(documentId);
        if (Current?.Metadata.Id != documentId)
        {
            throw new InvalidOperationException("Dokumentet byttes medan det sparades.");
        }
        Current = saved;
        if (versionReason is not null)
        {
            await versions.CommitAsync(documentId, content, versionReason);
        }
        Status = "Alla ändringar är sparade";
        await RefreshAsync(documentId);
    }

    public async Task DeleteCurrentAsync()
    {
        if (Current is null)
        {
            return;
        }

        await documents.DeleteAsync(Current.Metadata.Id);
        Current = null;
        var remaining = await documents.ListAsync();
        Filter = string.Empty;
        if (remaining.Count == 0)
        {
            await CreateAsync();
            return;
        }

        await RefreshAsync(remaining[0].Id);
        await OpenAsync(remaining[0].Id);
    }

    public async Task RefreshAsync(Guid? selectedId = null)
    {
        var all = await documents.ListAsync();
        Documents.Clear();
        foreach (var item in all.Where(x =>
                     string.IsNullOrWhiteSpace(Filter) ||
                     x.Title.Contains(Filter, StringComparison.CurrentCultureIgnoreCase)))
        {
            Documents.Add(item);
        }

        var id = selectedId ?? Current?.Metadata.Id;
        SelectedDocument = id is null ? null : Documents.FirstOrDefault(x => x.Id == id);
    }
}
