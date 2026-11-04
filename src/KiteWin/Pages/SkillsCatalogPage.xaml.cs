using KiteWin.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace KiteWin.Pages;

public sealed partial class SkillsCatalogPage : Page
{
    private readonly List<AntigravitySkillInfo> _allSkills = [.. SkillsRepository.CuratedSkills];
    private readonly List<AntigravityPluginInfo> _allPlugins = [.. SkillsRepository.Plugins];

    private List<AntigravitySkillInfo> _filteredSkills = [];
    private List<AntigravityPluginInfo> _filteredPlugins = [];

    private AntigravitySkillInfo? _selectedSkill;
    private AntigravityPluginInfo? _selectedPlugin;

    public SkillsCatalogPage()
    {
        InitializeComponent();
        PopulateCategoryFilter();
        ApplyFilter();

        if (_filteredSkills.Count > 0)
        {
            SkillsListView.SelectedIndex = 0;
        }
    }

    private void PopulateCategoryFilter()
    {
        CategoryFilterBox.Items.Clear();
        CategoryFilterBox.Items.Add(new ComboBoxItem { Content = "Все категории", Tag = "all" });

        var categories = _allSkills.Select(s => s.Category).Distinct().OrderBy(c => c);
        foreach (var cat in categories)
        {
            CategoryFilterBox.Items.Add(new ComboBoxItem { Content = cat, Tag = cat });
        }
        CategoryFilterBox.SelectedIndex = 0;
    }

    private void OnCategoryFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnCategoryTabChanged(object sender, RoutedEventArgs e)
    {
        var showPlugins = PluginsRadio?.IsChecked == true;
        if (SkillsListView != null) SkillsListView.Visibility = showPlugins ? Visibility.Collapsed : Visibility.Visible;
        if (PluginsListView != null) PluginsListView.Visibility = showPlugins ? Visibility.Visible : Visibility.Collapsed;

        ApplyFilter();

        if (showPlugins && _filteredPlugins.Count > 0)
        {
            PluginsListView.SelectedIndex = 0;
        }
        else if (!showPlugins && _filteredSkills.Count > 0)
        {
            SkillsListView.SelectedIndex = 0;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchBox?.Text?.Trim().ToLowerInvariant() ?? "";
        var selectedCatItem = CategoryFilterBox?.SelectedItem as ComboBoxItem;
        var selectedCat = selectedCatItem?.Tag as string ?? "all";

        if (PluginsRadio?.IsChecked == true)
        {
            _filteredPlugins = _allPlugins.Where(p =>
                (selectedCat == "all" || p.Category.Equals(selectedCat, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(query) ||
                 p.Name.ToLowerInvariant().Contains(query) ||
                 p.Summary.ToLowerInvariant().Contains(query) ||
                 p.Category.ToLowerInvariant().Contains(query) ||
                 p.Id.ToLowerInvariant().Contains(query))).ToList();

            if (PluginsListView != null)
            {
                PluginsListView.ItemsSource = _filteredPlugins;
                if (_filteredPlugins.Count > 0 && PluginsListView.SelectedIndex < 0)
                    PluginsListView.SelectedIndex = 0;
            }
        }
        else
        {
            _filteredSkills = _allSkills.Where(s =>
                (selectedCat == "all" || s.Category.Equals(selectedCat, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(query) ||
                 s.Name.ToLowerInvariant().Contains(query) ||
                 s.Summary.ToLowerInvariant().Contains(query) ||
                 s.Category.ToLowerInvariant().Contains(query) ||
                 s.Description.ToLowerInvariant().Contains(query) ||
                 (s.Triggers != null && s.Triggers.Any(t => t.ToLowerInvariant().Contains(query))))).ToList();

            if (SkillsListView != null)
            {
                SkillsListView.ItemsSource = _filteredSkills;
                if (_filteredSkills.Count > 0 && SkillsListView.SelectedIndex < 0)
                    SkillsListView.SelectedIndex = 0;
            }
        }
    }

    private void OnSkillSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SkillsListView.SelectedItem is AntigravitySkillInfo skill)
        {
            _selectedSkill = skill;
            DisplaySkillDetails(skill);
        }
    }

    private void OnPluginSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PluginsListView.SelectedItem is AntigravityPluginInfo plugin)
        {
            _selectedPlugin = plugin;
            DisplayPluginDetails(plugin);
        }
    }

    private void DisplaySkillDetails(AntigravitySkillInfo skill)
    {
        DetailSkillName.Text = skill.Name;
        DetailSkillCommand.Text = $"/{skill.Name}";
        DetailSkillCategory.Text = skill.Category;

        if (!string.IsNullOrEmpty(skill.PluginId))
        {
            DetailSkillPlugin.Text = $"Плагин: {skill.PluginId}";
            DetailSkillPlugin.Visibility = Visibility.Visible;
            DetailPluginBullet.Visibility = Visibility.Visible;
        }
        else
        {
            DetailSkillPlugin.Visibility = Visibility.Collapsed;
            DetailPluginBullet.Visibility = Visibility.Collapsed;
        }

        DetailSkillSummary.Text = skill.Summary;
        DetailSkillWhenToUse.Text = skill.WhenToUse;
        DetailSkillDescription.Text = skill.Description;

        CapabilitiesList.ItemsSource = skill.Capabilities ?? [];
        CapabilitiesSection.Visibility = (skill.Capabilities != null && skill.Capabilities.Length > 0)
            ? Visibility.Visible : Visibility.Collapsed;

        DetailSkillExamplePrompt.Text = skill.ExamplePrompt;

        PrinciplesList.ItemsSource = skill.Principles ?? [];
        PrinciplesSection.Visibility = (skill.Principles != null && skill.Principles.Length > 0)
            ? Visibility.Visible : Visibility.Collapsed;

        TriggersList.ItemsSource = skill.Triggers ?? [];
        TriggersSection.Visibility = (skill.Triggers != null && skill.Triggers.Length > 0)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DisplayPluginDetails(AntigravityPluginInfo plugin)
    {
        DetailSkillName.Text = plugin.Name;
        DetailSkillCommand.Text = plugin.Id;
        DetailSkillCategory.Text = plugin.Category;
        DetailSkillPlugin.Text = $"v{plugin.Version} от {plugin.Author}";
        DetailSkillPlugin.Visibility = Visibility.Visible;
        DetailPluginBullet.Visibility = Visibility.Visible;

        DetailSkillSummary.Text = plugin.Summary;
        DetailSkillWhenToUse.Text = $"Включает {plugin.SkillsCount} специализированных навыков. Поддерживает MCP-сервер: {plugin.McpServer ?? "нет"} и CLI: {plugin.CliCommand ?? "нет"}.";
        DetailSkillDescription.Text = $"Официальный модуль экосистемы Antigravity. Предоставляет доступ к интеграционным инструментам: {string.Join(", ", plugin.Skills)}.";

        CapabilitiesList.ItemsSource = plugin.Skills;
        CapabilitiesSection.Visibility = Visibility.Visible;

        DetailSkillExamplePrompt.Text = plugin.CliCommand != null ? $"{plugin.CliCommand} --help" : $"call_mcp_tool: {plugin.McpServer}";

        PrinciplesList.ItemsSource = new[] { "Официальный проверенный API", "Интеграция с MCP-хостом", "Типобезопасная передача данных" };
        PrinciplesSection.Visibility = Visibility.Visible;

        TriggersList.ItemsSource = plugin.Skills;
        TriggersSection.Visibility = Visibility.Visible;
    }

    private void OnCopySkillCommandClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSkill != null)
        {
            var package = new DataPackage();
            package.SetText($"/{_selectedSkill.Name}");
            Clipboard.SetContent(package);
        }
        else if (_selectedPlugin != null)
        {
            var package = new DataPackage();
            package.SetText(_selectedPlugin.Id);
            Clipboard.SetContent(package);
        }
    }

    private void OnCopyExamplePromptClick(object sender, RoutedEventArgs e)
    {
        var text = DetailSkillExamplePrompt.Text;
        if (!string.IsNullOrEmpty(text))
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }
    }
}
