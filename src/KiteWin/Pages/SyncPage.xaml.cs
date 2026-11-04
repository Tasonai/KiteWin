using System.Collections.ObjectModel;
using KiteWin.Core.Sync;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KiteWin.Pages;

public sealed partial class SyncPage : Page
{
    private readonly ObservableCollection<string> _logs = [];

    public SyncPage()
    {
        InitializeComponent();
        LogList.ItemsSource = _logs;
        AddLog("P2P Модуль синхронизации инициализирован.");
        AddLog($"Локальный сервер слушает порт {KiteSyncServer.DefaultPort}.");
        AddLog($"Адрес Mac по умолчанию: {Model.MacEndpoint}");
    }

    public AppModel Model => ((App)Application.Current).Model;

    private async void OnTestConnectionClick(object sender, RoutedEventArgs e)
    {
        var endpoint = EndpointBox.Text.Trim();
        if (string.IsNullOrEmpty(endpoint))
        {
            ConnectionInfoBar.Severity = InfoBarSeverity.Warning;
            ConnectionInfoBar.Title = "Предупреждение";
            ConnectionInfoBar.Message = "Введите валидный URL сервера Kite macOS (например, http://127.0.0.1:24242).";
            ConnectionInfoBar.IsOpen = true;
            return;
        }

        AddLog($"Проверка связи с {endpoint}...");
        ConnectionInfoBar.Severity = InfoBarSeverity.Informational;
        ConnectionInfoBar.Title = "Подключение";
        ConnectionInfoBar.Message = "Отправка пинга на Kite macOS...";
        ConnectionInfoBar.IsOpen = true;

        var isOnline = await Model.SyncClient.CheckConnectionAsync(endpoint);
        if (isOnline)
        {
            ConnectionInfoBar.Severity = InfoBarSeverity.Success;
            ConnectionInfoBar.Title = "Соединение установлено";
            ConnectionInfoBar.Message = $"Kite macOS активен и готов к синхронизации ({endpoint}).";
            AddLog($"Успешное рукопожатие с Kite macOS ({endpoint}). Статус: online.");
        }
        else
        {
            ConnectionInfoBar.Severity = InfoBarSeverity.Error;
            ConnectionInfoBar.Title = "Нет связи";
            ConnectionInfoBar.Message = $"Не удалось связаться с {endpoint}. Проверьте, что Kite запущен на Mac и порт 24242 не блокируется фаерволом.";
            AddLog($"Ошибка связи с {endpoint}. Хост недоступен.");
        }
    }

    private async void OnSyncNowClick(object sender, RoutedEventArgs e)
    {
        AddLog("Запуск ручной двусторонней P2P-синхронизации...");

        var result = await Model.TriggerSyncAsync();

        if (result.Success)
        {
            AddedFromMacText.Text = $"+{result.PulledCount}";
            PushedToMacText.Text = $"{result.PushedCount}";
            TotalIdeasText.Text = $"{result.TotalCount}";

            AddLog($"Синхронизация завершена успешно: +{result.PulledCount} с Mac, {result.PushedCount} на Mac. Всего в базе: {result.TotalCount}.");
            ConnectionInfoBar.Severity = InfoBarSeverity.Success;
            ConnectionInfoBar.Title = "Синхронизация успешна";
            ConnectionInfoBar.Message = result.Message;
            ConnectionInfoBar.IsOpen = true;
        }
        else
        {
            AddLog($"Синхронизация не удалась: {result.Message}");
            ConnectionInfoBar.Severity = InfoBarSeverity.Error;
            ConnectionInfoBar.Title = "Ошибка репликации";
            ConnectionInfoBar.Message = result.Message;
            ConnectionInfoBar.IsOpen = true;
        }
    }

    private void OnAutoSyncToggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            Prefs.Current.AutoSync = toggle.IsOn;
            Prefs.Current.Save();
            AddLog($"Авто-синхронизация каждые 30 секунд: {(toggle.IsOn ? "Включена" : "Отключена")}.");
        }
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logs.Insert(0, $"[{timestamp}] {message}");
    }
}
