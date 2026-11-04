using KiteWin.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace KiteWin.Pages;

public sealed partial class SkillsCatalogPage : Page
{
    private readonly List<AntigravitySkillInfo> _allSkills = [.. SkillsRepository.CuratedSkills];
    private readonly List<AntigravityPluginInfo> _allPlugins = [.. SkillsRepository.Plugins];

    public SkillsCatalogPage()
    {
        InitializeComponent();
        ApplyFilter();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnCategoryTabChanged(object sender, RoutedEventArgs e)
    {
        var showPlugins = PluginsRadio?.IsChecked == true;
        if (SkillsList != null) SkillsList.Visibility = showPlugins ? Visibility.Collapsed : Visibility.Visible;
        if (PluginsList != null) PluginsList.Visibility = showPlugins ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchBox?.Text?.Trim().ToLowerInvariant() ?? "";

        if (PluginsRadio?.IsChecked == true)
        {
            var filteredPlugins = string.IsNullOrEmpty(query)
                ? _allPlugins
                : _allPlugins.Where(p =>
                    p.Name.ToLowerInvariant().Contains(query) ||
                    p.Summary.ToLowerInvariant().Contains(query) ||
                    p.Category.ToLowerInvariant().Contains(query) ||
                    p.Id.ToLowerInvariant().Contains(query)).ToList();

            if (PluginsList != null) PluginsList.ItemsSource = filteredPlugins;
        }
        else
        {
            var filteredSkills = string.IsNullOrEmpty(query)
                ? _allSkills
                : _allSkills.Where(s =>
                    s.Name.ToLowerInvariant().Contains(query) ||
                    s.Summary.ToLowerInvariant().Contains(query) ||
                    s.Category.ToLowerInvariant().Contains(query) ||
                    s.Description.ToLowerInvariant().Contains(query)).ToList();

            if (SkillsList != null) SkillsList.ItemsSource = filteredSkills;
        }
    }

    private void OnCopySkillNameClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string skillName })
        {
            var package = new DataPackage();
            package.SetText($"/skill {skillName}");
            Clipboard.SetContent(package);
        }
    }

    private void OnCopyPluginIdClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string pluginId })
        {
            var package = new DataPackage();
            package.SetText(pluginId);
            Clipboard.SetContent(package);
        }
    }
}
