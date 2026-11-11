using Windows.ApplicationModel.DataTransfer;
using KiteWin.Core;
using KiteWin.Core.Models;
using KiteWin.Core.Services;
using KiteWin.UI.Dialogs;
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
    private readonly HashSet<string> _selectedElementIds = [];
    private readonly Dictionary<string, FrameworkElement> _canvasElementMap = [];
    private double _zoom = 1.0;
    private bool _isMarqueeActive;
    private Windows.Foundation.Point _marqueeStart;
    private bool _isSimulationActive;
    private DispatcherTimer? _simulationTimer;
    private double _simulationPhase;

    public CanvasPage()
    {
        InitializeComponent();
        _appModel = (Application.Current as App)!.Model;
        _canvasStore = _appModel.CanvasStore;

        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        MainCanvas.PointerPressed += OnCanvasPointerPressed;
        MainCanvas.PointerMoved += OnCanvasPointerMoved;
        MainCanvas.PointerReleased += OnCanvasPointerReleased;

        KeyDown += OnCanvasKeyDown;

        KiteLANBeacon.Shared.PeersChanged += OnPeersChanged;
        UpdateLANPeers(KiteLANBeacon.Shared.ActivePeers);

        RenderCanvas();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _simulationTimer?.Stop();
        if (_simulationTimer != null) _simulationTimer.Tick -= OnSimulationTick;
        _simulationTimer = null;

        MainCanvas.PointerPressed -= OnCanvasPointerPressed;
        MainCanvas.PointerMoved -= OnCanvasPointerMoved;
        MainCanvas.PointerReleased -= OnCanvasPointerReleased;

        KeyDown -= OnCanvasKeyDown;

        KiteLANBeacon.Shared.PeersChanged -= OnPeersChanged;
    }

    private void OnPeersChanged(IReadOnlyList<KitePeer> peers)
    {
        DispatcherQueue.TryEnqueue(() => UpdateLANPeers(peers));
    }

    private void UpdateLANPeers(IReadOnlyList<KitePeer> peers)
    {
        LANPeersCountText.Text = $"Пиры: {peers.Count}";
        LANPeersFlyout.Items.Clear();

        if (peers.Count == 0)
        {
            LANPeersFlyout.Items.Add(new MenuFlyoutItem
            {
                Text = "Поиск устройств в локальной сети...",
                IsEnabled = false
            });
            return;
        }

        foreach (var peer in peers)
        {
            var item = new MenuFlyoutItem
            {
                Text = $"{peer.Hostname} ({peer.Platform}) - {peer.Ip}:{peer.Port}",
                Icon = new FontIcon { Glyph = "\uE770" }
            };
            item.Click += async (_, _) =>
            {
                await PullCanvasFromPeerAsync(peer);
            };
            LANPeersFlyout.Items.Add(item);
        }
    }

    private async Task PullCanvasFromPeerAsync(KitePeer peer)
    {
        try
        {
            var endpoint = $"http://{peer.Ip}:{peer.Port}";
            var success = await _appModel.SyncClient.SyncCanvasBidirectionalAsync(endpoint, _canvasStore);
            RenderCanvas();
            ShowToast(success ? $"Синхронизировано с {peer.Hostname}" : $"Не удалось подключиться к {peer.Hostname}");
        }
        catch (Exception ex)
        {
            ShowToast($"Ошибка синхронизации: {ex.Message}");
        }
    }

    private void RenderCanvas()
    {
        MainCanvas.Children.Clear();
        _canvasElementMap.Clear();

        if (MarqueeSelectionBorder != null && !MainCanvas.Children.Contains(MarqueeSelectionBorder))
        {
            MainCanvas.Children.Add(MarqueeSelectionBorder);
        }

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
            AttachDrag(container, $"frame-{frame.Id}", (x, y) => { frame.X = x; frame.Y = y; _canvasStore.Save(); });

            MainCanvas.Children.Add(container);
        }

        // 2. Draw Connections
        foreach (var conn in _canvasStore.Snapshot.Connections)
        {
            var centerFrom = GetNodeCenter(conn.FromId) ?? new Windows.Foundation.Point(340, 420);
            var centerTo = GetNodeCenter(conn.ToId) ?? new Windows.Foundation.Point(560, 350);

            double dx = centerTo.X - centerFrom.X;
            double dy = centerTo.Y - centerFrom.Y;

            Windows.Foundation.Point startPt;
            Windows.Foundation.Point endPt;

            if (Math.Abs(dx) > Math.Abs(dy))
            {
                startPt = new Windows.Foundation.Point(centerFrom.X + (dx > 0 ? 80 : -80), centerFrom.Y);
                endPt = new Windows.Foundation.Point(centerTo.X + (dx > 0 ? -80 : 80), centerTo.Y);
            }
            else
            {
                startPt = new Windows.Foundation.Point(centerFrom.X, centerFrom.Y + (dy > 0 ? 60 : -60));
                endPt = new Windows.Foundation.Point(centerTo.X, centerTo.Y + (dy > 0 ? -60 : 60));
            }

            double cdx = (endPt.X - startPt.X) * 0.5;
            var p0 = startPt;
            var p1 = new Windows.Foundation.Point(startPt.X + cdx, startPt.Y);
            var p2 = new Windows.Foundation.Point(endPt.X - cdx, endPt.Y);
            var p3 = endPt;

            var path = new Microsoft.UI.Xaml.Shapes.Path
            {
                Stroke = new SolidColorBrush(ColorFromHex(conn.ColorHex, 0.7)),
                StrokeThickness = 2,
                StrokeDashArray = _isSimulationActive ? [4, 4] : [6, 4]
            };

            var geom = new PathGeometry();
            var fig = new PathFigure { StartPoint = p0 };
            fig.Segments.Add(new BezierSegment { Point1 = p1, Point2 = p2, Point3 = p3 });
            geom.Figures.Add(fig);
            path.Data = geom;
            Canvas.SetZIndex(path, 2);
            MainCanvas.Children.Add(path);

            // Simulation Throughput Particle Stream
            if (_isSimulationActive)
            {
                for (int idx = 0; idx < 2; idx++)
                {
                    double phase = (idx * 0.5);
                    double t = (_simulationPhase + phase) % 1.0;
                    double u = 1.0 - t;
                    double tt = t * t;
                    double uu = u * u;
                    double uuu = uu * u;
                    double ttt = tt * t;

                    double px = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
                    double py = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;

                    var particle = new Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = new SolidColorBrush(ColorFromHex(conn.ColorHex, 0.95))
                    };
                    Canvas.SetLeft(particle, px - 4); // grid: ok simulation particle
                    Canvas.SetTop(particle, py - 4); // grid: ok simulation particle
                    Canvas.SetZIndex(particle, 4);
                    MainCanvas.Children.Add(particle);
                }
            }

            if (!string.IsNullOrEmpty(conn.Label) || _isSimulationActive)
            {
                var labelPill = new Border
                {
                    Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
                    BorderBrush = new SolidColorBrush(ColorFromHex(conn.ColorHex, 0.7)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 4, 8, 4)
                };

                var flyout = new MenuFlyout();
                foreach (var proto in CanvasConnectionProtocol.All)
                {
                    var item = new MenuFlyoutItem { Text = proto };
                    item.Click += (_, _) =>
                    {
                        conn.Label = proto;
                        conn.ColorHex = CanvasConnectionProtocol.Color(proto);
                        _canvasStore.Save();
                        RenderCanvas();
                        ShowToast($"Протокол связи изменен: {proto}");
                    };
                    flyout.Items.Add(item);
                }
                flyout.Items.Add(new MenuFlyoutSeparator());
                var deleteItem = new MenuFlyoutItem { Text = "Удалить связь" };
                deleteItem.Icon = new FontIcon { Glyph = "\uE74D" };
                deleteItem.Click += (_, _) =>
                {
                    _canvasStore.Snapshot.Connections.Remove(conn);
                    _canvasStore.Save();
                    RenderCanvas();
                    ShowToast("Связь удалена");
                };
                flyout.Items.Add(deleteItem);

                labelPill.ContextFlyout = flyout;
                labelPill.Tapped += (s, e) => flyout.ShowAt(labelPill);

                var badgePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4
                };

                if (_isSimulationActive)
                {
                    badgePanel.Children.Add(new Ellipse
                    {
                        Width = 4,
                        Height = 4,
                        Fill = new SolidColorBrush(Colors.Green),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    badgePanel.Children.Add(new TextBlock
                    {
                        Text = $"{conn.Label}  1.2k rps",
                        FontSize = 10,
                        FontFamily = new FontFamily("Cascadia Code, Consolas"),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.Green),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                else
                {
                    badgePanel.Children.Add(new TextBlock
                    {
                        Text = conn.Label,
                        FontSize = 10,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(ColorFromHex(conn.ColorHex, 1.0)),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                labelPill.Child = badgePanel;

                var midX = (startPt.X + endPt.X) / 2;
                var midY = (startPt.Y + endPt.Y) / 2;
                Canvas.SetLeft(labelPill, midX - 36); // grid: ok freeform canvas
                Canvas.SetTop(labelPill, midY - 12); // grid: ok freeform canvas
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
            AttachDrag(card, $"ref-{refCard.Id}", (x, y) => { refCard.X = x; refCard.Y = y; _canvasStore.Save(); });
            MainCanvas.Children.Add(card);
        }

        // 4. Draw Stickies
        foreach (var sticky in _canvasStore.Snapshot.Stickies)
        {
            var stickyEl = CreateStickyElement(sticky);
            Canvas.SetLeft(stickyEl, sticky.X); // grid: ok freeform canvas
            Canvas.SetTop(stickyEl, sticky.Y); // grid: ok freeform canvas
            Canvas.SetZIndex(stickyEl, 10);
            AttachDrag(stickyEl, $"sticky-{sticky.Id}", (x, y) => { sticky.X = x; sticky.Y = y; _canvasStore.Save(); });
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
            AttachDrag(ideaCard, $"idea-{firstIdea.Id}", (x, y) => { });
            MainCanvas.Children.Add(ideaCard);
        }

        UpdateSelectionVisuals();
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

    private void AttachDrag(FrameworkElement element, string elementId, Action<double, double> onMoved)
    {
        _canvasElementMap[elementId] = element;
        var isDragging = false;
        var startPoint = new Windows.Foundation.Point();
        var origPositions = new Dictionary<string, (double X, double Y)>();

        element.PointerPressed += (s, e) =>
        {
            var isShiftOrCtrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) ||
                                Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (!isShiftOrCtrl && !_selectedElementIds.Contains(elementId))
            {
                _selectedElementIds.Clear();
                _selectedElementIds.Add(elementId);
            }
            else if (isShiftOrCtrl)
            {
                if (_selectedElementIds.Contains(elementId))
                    _selectedElementIds.Remove(elementId);
                else
                    _selectedElementIds.Add(elementId);
            }
            UpdateSelectionVisuals();

            isDragging = true;
            element.CapturePointer(e.Pointer);
            startPoint = e.GetCurrentPoint(MainCanvas).Position;

            origPositions.Clear();
            foreach (var id in _selectedElementIds)
            {
                if (_canvasElementMap.TryGetValue(id, out var el))
                {
                    origPositions[id] = (Canvas.GetLeft(el), Canvas.GetTop(el));
                }
            }

            e.Handled = true;
        };

        element.PointerMoved += (s, e) =>
        {
            if (!isDragging) return;
            var current = e.GetCurrentPoint(MainCanvas).Position;
            var dx = current.X - startPoint.X;
            var dy = current.Y - startPoint.Y;

            foreach (var kvp in origPositions)
            {
                if (_canvasElementMap.TryGetValue(kvp.Key, out var el))
                {
                    var newX = Math.Max(20, kvp.Value.X + dx);
                    var newY = Math.Max(20, kvp.Value.Y + dy);
                    Canvas.SetLeft(el, newX); // grid: ok freeform canvas
                    Canvas.SetTop(el, newY); // grid: ok freeform canvas
                }
            }
            e.Handled = true;
        };

        element.PointerReleased += (s, e) =>
        {
            if (isDragging)
            {
                isDragging = false;
                element.ReleasePointerCapture(e.Pointer);

                foreach (var id in _selectedElementIds)
                {
                    if (_canvasElementMap.TryGetValue(id, out var el))
                    {
                        var curX = Canvas.GetLeft(el);
                        var curY = Canvas.GetTop(el);
                        UpdateModelPosition(id, curX, curY);
                    }
                }
                _canvasStore.Save();
                e.Handled = true;
            }
        };
    }

    private void UpdateModelPosition(string id, double x, double y)
    {
        if (id.StartsWith("frame-"))
        {
            var raw = id["frame-".Length..];
            var f = _canvasStore.Snapshot.Frames.FirstOrDefault(f => f.Id == raw);
            if (f != null) { f.X = x; f.Y = y; }
        }
        else if (id.StartsWith("sticky-"))
        {
            var raw = id["sticky-".Length..];
            var s = _canvasStore.Snapshot.Stickies.FirstOrDefault(s => s.Id == raw);
            if (s != null) { s.X = x; s.Y = y; }
        }
        else if (id.StartsWith("ref-"))
        {
            var raw = id["ref-".Length..];
            var r = _canvasStore.Snapshot.ReferenceCards.FirstOrDefault(r => r.Id == raw);
            if (r != null) { r.X = x; r.Y = y; }
        }
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var kvp in _canvasElementMap)
        {
            var isSelected = _selectedElementIds.Contains(kvp.Key);
            if (kvp.Value is Border b)
            {
                b.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
                if (isSelected)
                    b.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 59, 130, 246));
            }
            else if (kvp.Value is Grid g && g.Children.Count > 0 && g.Children[0] is Border fb)
            {
                fb.BorderThickness = isSelected ? new Thickness(4) : new Thickness(2);
                if (isSelected)
                    fb.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 59, 130, 246));
            }
        }
    }

    private void OnCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.OriginalSource is not Canvas) return;

        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (!shift)
        {
            _selectedElementIds.Clear();
            UpdateSelectionVisuals();
        }

        _marqueeStart = e.GetCurrentPoint(MainCanvas).Position;
        _isMarqueeActive = true;
        MarqueeSelectionBorder.Visibility = Visibility.Visible;
        Canvas.SetLeft(MarqueeSelectionBorder, _marqueeStart.X); // grid: ok freeform canvas
        Canvas.SetTop(MarqueeSelectionBorder, _marqueeStart.Y); // grid: ok freeform canvas
        MarqueeSelectionBorder.Width = 0; // grid: ok freeform canvas
        MarqueeSelectionBorder.Height = 0; // grid: ok freeform canvas
        MainCanvas.CapturePointer(e.Pointer);
    }

    private void OnCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isMarqueeActive) return;

        var pt = e.GetCurrentPoint(MainCanvas).Position;
        var minX = Math.Min(_marqueeStart.X, pt.X);
        var minY = Math.Min(_marqueeStart.Y, pt.Y);
        var w = Math.Abs(pt.X - _marqueeStart.X);
        var h = Math.Abs(pt.Y - _marqueeStart.Y);

        Canvas.SetLeft(MarqueeSelectionBorder, minX); // grid: ok freeform canvas
        Canvas.SetTop(MarqueeSelectionBorder, minY); // grid: ok freeform canvas
        MarqueeSelectionBorder.Width = Math.Max(0, w); // grid: ok freeform canvas
        MarqueeSelectionBorder.Height = Math.Max(0, h); // grid: ok freeform canvas

        var marqueeRect = new Windows.Foundation.Rect(minX, minY, w, h);
        foreach (var kvp in _canvasElementMap)
        {
            var elLeft = Canvas.GetLeft(kvp.Value);
            var elTop = Canvas.GetTop(kvp.Value);
            var elW = kvp.Value.ActualWidth > 0 ? kvp.Value.ActualWidth : 200;
            var elH = kvp.Value.ActualHeight > 0 ? kvp.Value.ActualHeight : 120;
            var elRect = new Windows.Foundation.Rect(elLeft, elTop, elW, elH);

            if (RectIntersects(marqueeRect, elRect))
            {
                _selectedElementIds.Add(kvp.Key);
            }
        }
        UpdateSelectionVisuals();
    }

    private void OnCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isMarqueeActive)
        {
            _isMarqueeActive = false;
            MarqueeSelectionBorder.Visibility = Visibility.Collapsed;
            MainCanvas.ReleasePointerCapture(e.Pointer);
        }
    }

    private static bool RectIntersects(Windows.Foundation.Rect r1, Windows.Foundation.Rect r2)
    {
        return !(r2.Left > r1.Right || r2.Right < r1.Left || r2.Top > r1.Bottom || r2.Bottom < r1.Top);
    }

    private void OnCanvasKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Delete || e.Key == Windows.System.VirtualKey.Back)
        {
            if (_selectedElementIds.Count > 0)
            {
                DeleteSelectedElements();
                e.Handled = true;
            }
        }
    }

    private void DeleteSelectedElements()
    {
        var count = _selectedElementIds.Count;
        foreach (var id in _selectedElementIds.ToList())
        {
            if (id.StartsWith("frame-"))
            {
                var raw = id["frame-".Length..];
                _canvasStore.Snapshot.Frames.RemoveAll(x => x.Id == raw);
            }
            else if (id.StartsWith("sticky-"))
            {
                var raw = id["sticky-".Length..];
                _canvasStore.Snapshot.Stickies.RemoveAll(x => x.Id == raw);
                _canvasStore.Snapshot.Connections.RemoveAll(x => x.FromId == id || x.ToId == id);
            }
            else if (id.StartsWith("ref-"))
            {
                var raw = id["ref-".Length..];
                _canvasStore.Snapshot.ReferenceCards.RemoveAll(x => x.Id == raw);
                _canvasStore.Snapshot.Connections.RemoveAll(x => x.FromId == id || x.ToId == id);
            }
        }

        _selectedElementIds.Clear();
        _canvasStore.Save();
        RenderCanvas();
        ShowToast($"Удалено элементов: {count}");
    }

    private void OnGroupSelectedClicked(object sender, RoutedEventArgs e)
    {
        if (_selectedElementIds.Count == 0)
        {
            ShowToast("Выберите элементы для объединения в подсистему");
            return;
        }

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var id in _selectedElementIds)
        {
            if (_canvasElementMap.TryGetValue(id, out var el))
            {
                var left = Canvas.GetLeft(el);
                var top = Canvas.GetTop(el);
                var w = el.ActualWidth > 0 ? el.ActualWidth : 200;
                var h = el.ActualHeight > 0 ? el.ActualHeight : 120;

                minX = Math.Min(minX, left);
                minY = Math.Min(minY, top);
                maxX = Math.Max(maxX, left + w);
                maxY = Math.Max(maxY, top + h);
            }
        }

        if (minX < double.MaxValue)
        {
            var newFrame = new CanvasFrame
            {
                Title = "Подсистема",
                ColorHex = "#3B82F6",
                X = Math.Max(20, minX - 24),
                Y = Math.Max(20, minY - 32),
                Width = Math.Max(200, maxX - minX + 48),
                Height = Math.Max(160, maxY - minY + 56)
            };
            _canvasStore.Snapshot.Frames.Add(newFrame);
            _canvasStore.Save();
            RenderCanvas();
            ShowToast("Элементы объединены в подсистему");
        }
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

    private async void OnTopologyClicked(object sender, RoutedEventArgs e)
    {
        var descriptors = _appModel.AllItems.Select(item => new TopologyNodeDescriptor(
            item.Id,
            item.Title,
            CanvasTopologyValidator.InferRole(item.Title)
        )).ToList();

        if (descriptors.Count == 0)
        {
            foreach (var frame in _canvasStore.Snapshot.Frames)
            {
                descriptors.Add(new TopologyNodeDescriptor(frame.Id, frame.Title, CanvasTopologyValidator.InferRole(frame.Title)));
            }
        }

        var report = CanvasTopologyValidator.Validate(descriptors, _canvasStore.Snapshot.Connections);
        var title = _canvasStore.Snapshot.Frames.FirstOrDefault()?.Title ?? (_appModel.AllItems.FirstOrDefault()?.Title ?? "Архитектура");
        var dialog = new TopologyValidatorDialog(XamlRoot, title, report);
        await dialog.ShowAsync();
    }

    private void OnAutoArchitectClicked(object sender, RoutedEventArgs e)
    {
        var openItem = _appModel.Selection ?? _appModel.Items.FirstOrDefault();
        var title = openItem?.Title ?? "Архитектура проекта";
        var concept = openItem?.Concept ?? "Полнофункциональный бэкенд и микросервисы";

        var res = CanvasAutoArchitect.Synthesize(title, concept, "Windows 11", "Fluent Design");

        _canvasStore.Snapshot.Frames.Clear();
        _canvasStore.Snapshot.Frames.AddRange(res.Frames);

        _canvasStore.Snapshot.Stickies.Clear();
        _canvasStore.Snapshot.Stickies.AddRange(res.Stickies);

        _canvasStore.Snapshot.Connections.Clear();
        _canvasStore.Snapshot.Connections.AddRange(res.Connections);

        _canvasStore.Save();
        RenderCanvas();

        ShowToast("Авто-Топология: синтезировано 5 фреймов, 10 сервисов и 9 протокольных связей");
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

    private Windows.Foundation.Point? GetNodeCenter(string nodeId)
    {
        var sticky = _canvasStore.Snapshot.Stickies.FirstOrDefault(s => s.Id == nodeId);
        if (sticky != null)
        {
            return new Windows.Foundation.Point(sticky.X + sticky.Width / 2, sticky.Y + sticky.Height / 2);
        }
        var frame = _canvasStore.Snapshot.Frames.FirstOrDefault(f => f.Id == nodeId);
        if (frame != null)
        {
            return new Windows.Foundation.Point(frame.X + frame.Width / 2, frame.Y + frame.Height / 2);
        }
        return null;
    }

    private void OnSimulationToggled(object sender, RoutedEventArgs e)
    {
        _isSimulationActive = SimulationBtn.IsChecked == true;
        SimulationText.Text = _isSimulationActive ? "Потоки" : "Симуляция";
        SimulationIcon.Glyph = _isSimulationActive ? "\uE769" : "\uE768";

        if (_isSimulationActive)
        {
            _simulationTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _simulationTimer.Tick += OnSimulationTick;
            _simulationTimer.Start();
            ShowToast("Запущена симуляция трафика и пропускной способности (RPS/QPS)");
        }
        else
        {
            _simulationTimer?.Stop();
            if (_simulationTimer != null) _simulationTimer.Tick -= OnSimulationTick;
            _simulationTimer = null;
            ShowToast("Симуляция остановлена");
        }
        RenderCanvas();
    }

    private void OnSimulationTick(object? sender, object e)
    {
        _simulationPhase = (_simulationPhase + 0.04) % 1.0;
        RenderCanvas();
    }

    private void OnHyperscaleInspectorClicked(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(HyperscalePage));
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
