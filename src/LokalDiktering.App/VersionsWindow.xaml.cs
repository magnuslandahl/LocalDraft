using System.Windows;
using LokalDiktering.Core;

namespace LokalDiktering.App;

public partial class VersionsWindow : Window
{
    public VersionsWindow(IReadOnlyList<VersionMetadata> versions)
    {
        InitializeComponent();
        VersionList.ItemsSource = versions;
        VersionList.SelectedIndex = versions.Count > 0 ? 0 : -1;
    }

    public VersionMetadata? SelectedVersion { get; private set; }

    private void VersionList_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e) =>
        RestoreButton.IsEnabled = VersionList.SelectedItem is VersionMetadata;

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedVersion = VersionList.SelectedItem as VersionMetadata;
        if (SelectedVersion is not null)
        {
            DialogResult = true;
        }
    }
}
