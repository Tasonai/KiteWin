using Windows.ApplicationModel.DataTransfer;
using KiteWin.Core;
using KiteWin.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace KiteWin.Pages;

public sealed partial class CanvasPage : Page
{
    private readonly AppModel _appModel;
    private readonly CanvasStore _canvasStore;
    private double _zoom = 1.0;

    public CanvasPage()
    {
        InitializeComponent();
        _appModel = (Application.Current as App)!.Model;
        _canvasStore = _appModel.CanvasStore;

        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        RenderCanvas();
    }

    private void RenderCanvas()
    {
        MainCanvas.Children.Clear();

        // 1. Draw Frames
        foreach (var frame in _canvasStore.Snapshot.Frames)
        {
            var frameBorder = new Border
            {
                Width = frame.Width,
                Height = frame.Height,
                BorderBrush = new SolidColorBrush(ColorFromHex(frame.ColorHex, 0.4)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(ColorFromHex(frame.ColorHex, 0.04))
            };

            var header = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(ColorFromHex(frame.ColorHex, 0.9)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(8, -12, 0, 0),
                Child = new TextBlock
                {
                    Text = frame.Title,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.White)
                }
            };

            var container = new Grid();
            container.Children.Add(frameBorder);
            container.Children.Add(header);

            Canvas.SetLeft(container, frame.X); // grid: ok freeform canvas
            Canvas.SetTop(container, frame.Y); // grid: ok freeform canvas
            Canvas.SetZIndex(container, 1);
            AttachDrag(container, (x, y) => { frame.X = x; frame.Y = y; _canvasStore.Save(); });

            MainCanvas.Children.Add(container);
        }

        // 2. Draw Connections
        foreach (var conn in _canvasStore.Snapshot.Connections)
        {
            var path = new Microsoft.UI.Xaml.Shapes.Path
            {
                Stroke = new SolidColorBrush(ColorFromHex(conn.ColorHex, 0.6)),
                StrokeThickness = 2,
                StrokeDashArray = [4, 2]
            };

            var geom = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Windows.Foundation.Point(340, 420) };
            fig.Segments.Add(new BezierSegment
            {
                Point1 = new Windows.Foundation.Point(450, 420),
                Point2 = new Windows.Foundation.Point(490, 360),
                Point3 = new Windows.Foundation.Point(560, 350)
            });
            geom.Figures.Add(fig);
            path.Data = geom;
            Canvas.SetZIndex(path, 2);
            MainCanvas.Children.Add(path);

            if (!string.IsNullOrEmpty(conn.Label))
            {
                var labelPill = new Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(ColorFromHex(conn.ColorHex, 0.4)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 2, 8, 2),
                    Child = new TextBlock
                    {
                        Text = conn.Label,
                        FontSize = 10,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(ColorFromHex(conn.ColorHex, 1.0))
                    }
                };
                Canvas.SetLeft(labelPill, 435); // grid: ok freeform canvas
                Canvas.SetTop(labelPill, 385); // grid: ok freeform canvas
                Canvas.SetZIndex(labelPill, 3);
                MainCanvas.Children.Add(labelPill);
            }
        }

        // 3. Draw Reference Cards
        foreach (var refCard in _canvasStore.Snapshot.ReferenceCards)
        {
            var card = CreateReferenceCardElement(refCard);
            Canvas.SetLeft(card, refCard.X); // grid: ok freeform canvas
            Canvas.SetTop(card, refCard.Y); // grid: ok freeform canvas
            Canvas.SetZIndex(card, 10);
            AttachDrag(card, (x, y) => { refCard.X = x; refCard.Y = y; _canvasStore.Save(); });
            MainCanvas.Children.Add(card);
        }

        // 4. Draw Stickies
        foreach (var sticky in _canvasStore.Snapshot.Stickies)
        {
            var stickyEl = CreateStickyElement(sticky);
            Canvas.SetLeft(stickyEl, sticky.X); // grid: ok freeform canvas
            Canvas.SetTop(stickyEl, sticky.Y); // grid: ok freeform canvas
            Canvas.SetZIndex(stickyEl, 10);
            AttachDrag(stickyEl, (x, y) => { sticky.X = x; sticky.Y = y; _canvasStore.Save(); });
            MainCanvas.Children.Add(stickyEl);
        }

        // 5. Draw Ideas
        var firstIdea = _appModel.Items.FirstOrDefault();
        if (firstIdea != null)
        {
            var ideaCard = CreateIdeaCardElement(firstIdea);
            Canvas.SetLeft(ideaCard, 560); // grid: ok freeform canvas
            Canvas.SetTop(ideaCard, 200); // grid: ok freeform canvas
            Canvas.SetZIndex(ideaCard, 10);
            MainCanvas.Children.Add(ideaCard);
        }
    }

    private FrameworkElement CreateStickyElement(CanvasSticky sticky)
    {
        var border = new Border
        {
            Width = 190,
            MinHeight = 110,
            Background = new SolidColorBrush(ColorFromHex(sticky.ColorHex, 0.95)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(ColorFromHex(sticky.ColorHex, 0.4))
        };

        var stack = new StackPanel { Spacing = 6 };
        var text = new TextBlock
        {
            Text = sticky.Text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 30, 30, 30))
        };
        stack.Children.Add(text);
        border.Child = stack;
        return border;
    }

    private FrameworkElement CreateReferenceCardElement(CanvasReferenceCard refCard)
    {
        var border = new Border
        {
            Width = 232,
            Background = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 225, 225, 230)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };

        var stack = new StackPanel { Spacing = 8 };

        var topRow = new Grid();
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = new Border
        {
            Background = new SolidColorBrush(ColorFromHex(refCard.ColorHex, 0.15)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = refCard.Source,
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(ColorFromHex(refCard.ColorHex, 1.0))
            }
        };
        Grid.SetColumn(badge, 0);
        topRow.Children.Add(badge);

        stack.Children.Add(topRow);

        var title = new TextBlock
        {
            Text = refCard.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(title);

        var desc = new TextBlock
        {
            Text = refCard.Description,
            FontSize = 11,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 120, 120, 130)),
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(desc);

        border.Child = stack;
        return border;
    }

    private FrameworkElement CreateIdeaCardElement(Item item)
    {
        var border = new Border
        {
            Width = 240,
            Background = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 220, 220, 230)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };

        var stack = new StackPanel { Spacing = 8 };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new FontIcon { Glyph = "\uE734", FontSize = 12, Foreground = new SolidColorBrush(Colors.Gold) });
        header.Children.Add(new TextBlock
        {
            Text = item.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            TextWrapping = TextWrapping.NoWrap
        });
        stack.Children.Add(header);

        var tagsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var platformBadge = new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 240, 240, 245)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            Child = new TextBlock { Text = item.Platform, FontSize = 10, Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 100, 100, 110)) }
        };
        tagsRow.Children.Add(platformBadge);

        var styleBadge = new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 240, 245, 255)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            Child = new TextBlock { Text = item.Style, FontSize = 10, Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 30, 100, 220)) }
        };
        tagsRow.Children.Add(styleBadge);
        stack.Children.Add(tagsRow);

        border.Child = stack;
        return border;
    }

    private void AttachDrag(FrameworkElement element, Action<double, double> onMoved)
    {
        var isDragging = false;
        var startPoint = new Windows.Foundation.Point();
        var origX = 0.0;
        var origY = 0.0;

        element.PointerPressed += (s, e) =>
        {
            isDragging = true;
            element.CapturePointer(e.Pointer);
            startPoint = e.GetCurrentPoint(MainCanvas).Position;
            origX = Canvas.GetLeft(element);
            origY = Canvas.GetTop(element);
            e.Handled = true;
        };

        element.PointerMoved += (s, e) =>
        {
            if (!isDragging) return;
            var current = e.GetCurrentPoint(MainCanvas).Position;
            var newX = Math.Max(20, origX + (current.X - startPoint.X));
            var newY = Math.Max(20, origY + (current.Y - startPoint.Y));
            Canvas.SetLeft(element, newX); // grid: ok freeform canvas
            Canvas.SetTop(element, newY); // grid: ok freeform canvas
            e.Handled = true;
        };

        element.PointerReleased += (s, e) =>
        {
            if (isDragging)
            {
                isDragging = false;
                element.ReleasePointerCapture(e.Pointer);
                onMoved(Canvas.GetLeft(element), Canvas.GetTop(element));
                e.Handled = true;
            }
        };
    }

    private void OnAddIdeaClicked(object sender, RoutedEventArgs e)
    {
        _appModel.Add("Новая идея для холста");
        RenderCanvas();
    }

    private void OnAddStickyClicked(object sender, RoutedEventArgs e)
    {
        _canvasStore.Snapshot.Stickies.Add(new CanvasSticky
        {
            Text = "Заметка / Принцип архитектуры",
            ColorHex = "#FEF08A",
            X = 240,
            Y = 240
        });
        _canvasStore.Save();
        RenderCanvas();
    }

    private void OnAddFrameClicked(object sender, RoutedEventArgs e)
    {
        _canvasStore.Snapshot.Frames.Add(new CanvasFrame
        {
            Title = "Новый модуль системы",
            ColorHex = "#6366F1",
            X = 200,
            Y = 100,
            Width = 400,
            Height = 350
        });
        _canvasStore.Save();
        RenderCanvas();
    }

    private void OnCoArchitectClicked(object sender, RoutedEventArgs e)
    {
        var count = CanvasCoArchitect.ApplyGaps(_canvasStore.Snapshot);
        if (count > 0)
        {
            _canvasStore.Save();
            RenderCanvas();
            ShowToast($"AI Архитектор: добавлено {count} критических модулей со связями");
        }
        else
        {
            ShowToast("Архитектура сбалансирована, критических пробелов не обнаружено");
        }
    }

    private void OnRepoIngestionClicked(object sender, RoutedEventArgs e)
    {
        var arch = CodebaseIngestion.Analyze(AppDomain.CurrentDomain.BaseDirectory);
        _canvasStore.Snapshot.Frames.Clear();
        _canvasStore.Snapshot.Frames.AddRange(arch.Frames);
        _canvasStore.Snapshot.Stickies.Clear();
        _canvasStore.Snapshot.Stickies.AddRange(arch.Stickies);
        _canvasStore.Snapshot.Connections.Clear();
        _canvasStore.Snapshot.Connections.AddRange(arch.Connections);
        _canvasStore.Save();
        RenderCanvas();
        ShowToast($"Импорт структуры: {arch.Name} ({arch.ProjectType}) размещен на холсте");
    }

    private void OnRoadmapHUDClicked(object sender, RoutedEventArgs e)
    {
        var (frames, stickies, connections) = RoadmapTracker.GenerateWaves();
        _canvasStore.Snapshot.Frames.Clear();
        _canvasStore.Snapshot.Frames.AddRange(frames);
        _canvasStore.Snapshot.Stickies.Clear();
        _canvasStore.Snapshot.Stickies.AddRange(stickies);
        _canvasStore.Snapshot.Connections.Clear();
        _canvasStore.Snapshot.Connections.AddRange(connections);
        _canvasStore.Save();
        RenderCanvas();
        ShowToast("Дорожная карта: волны выполнения и статус задач отображены");
    }

    private async void OnSyncCanvasClicked(object sender, RoutedEventArgs e)
    {
        SyncCanvasBtn.IsEnabled = false;
        try
        {
            var success = await _appModel.SyncClient.SyncCanvasBidirectionalAsync(_appModel.MacEndpoint, _canvasStore);
            RenderCanvas();
            ShowToast(success ? "Холст синхронизирован с Mac!" : "Ошибка связи с сервером Mac (порт 24242)");
        }
        catch (Exception ex)
        {
            ShowToast($"Ошибка: {ex.Message}");
        }
        finally
        {
            SyncCanvasBtn.IsEnabled = true;
        }
    }

    private void OnZoomInClicked(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Min(1.6, _zoom + 0.1);
        ApplyZoom();
    }

    private void OnZoomOutClicked(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Max(0.6, _zoom - 0.1);
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        ZoomText.Text = $"{(int)(_zoom * 100)}%";
        MainCanvas.RenderTransform = new ScaleTransform { ScaleX = _zoom, ScaleY = _zoom };
    }

    private void OnExportStitchClicked(object sender, RoutedEventArgs e)
    {
        var md = DesignExporter.GenerateStitchDesignMd(
            title: "Архитектура холста Kite",
            style: "Editorial & Swiss Modern",
            platform: "Windows",
            concept: "Интерактивный холст идей и дизайн-токенов с двусторонней синхронизацией"
        );
        var pkg = new DataPackage();
        pkg.SetText(md);
        Clipboard.SetContent(pkg);
        ShowToast("DESIGN.md скопирован в буфер обмена для Google Stitch");
    }

    private void OnExportHtmlClicked(object sender, RoutedEventArgs e)
    {
        var html = CanvasExporter.ExportHtml(_store.Snapshot, Model.Items);
        var pkg = new DataPackage();
        pkg.SetText(html);
        Clipboard.SetContent(pkg);
        ShowToast("Интерактивный HTML скопирован в буфер обмена");
    }

    private void OnExportSvgClicked(object sender, RoutedEventArgs e)
    {
        var svg = CanvasExporter.ExportSvg(_store.Snapshot, Model.Items);
        var pkg = new DataPackage();
        pkg.SetText(svg);
        Clipboard.SetContent(pkg);
        ShowToast("Векторный SVG скопирован в буфер обмена");
    }

    private void OnExportMermaidClicked(object sender, RoutedEventArgs e)
    {
        var mmd = CanvasExporter.ExportMermaid(_store.Snapshot, Model.Items);
        var pkg = new DataPackage();
        pkg.SetText(mmd);
        Clipboard.SetContent(pkg);
        ShowToast("Диаграмма Mermaid скопирована в буфер обмена");
    }

    private void OnExportFigmaClicked(object sender, RoutedEventArgs e)
    {
        var json = DesignExporter.GenerateFigmaTokensJson(
            title: "Архитектура холста Kite",
            style: "Editorial & Swiss Modern"
        );
        var pkg = new DataPackage();
        pkg.SetText(json);
        Clipboard.SetContent(pkg);
        ShowToast("Figma Tokens JSON скопирован в буфер обмена");
    }

    private void ShowToast(string message)
    {
        CanvasToast.Message = message;
        CanvasToast.IsOpen = true;
    }

    private static Windows.UI.Color ColorFromHex(string hex, double opacity = 1.0)
    {
        var clean = hex.TrimStart('#');
        if (clean.Length == 6)
        {
            var r = Convert.ToByte(clean[..2], 16);
            var g = Convert.ToByte(clean.Substring(2, 2), 16);
            var b = Convert.ToByte(clean.Substring(4, 2), 16);
            var a = (byte)(opacity * 255);
            return ColorHelper.FromArgb(a, r, g, b);
        }
        return Colors.DodgerBlue;
    }
}
