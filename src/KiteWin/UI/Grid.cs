using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace KiteWin.UI;

/// The spacing scale for code, on the 4 epx grid. XAML uses the same values from UI/Space.xaml.
public static class Space
{
    public const double Xs = 4, Sm = 8, Md = 12, Lg = 16, Xl = 24, Xxl = 36, Section = 48;
}

/// Marks a container that probes are measured against, and its inset.
///   <StackPanel ui:GridArea.Name="page" ui:GridArea.Inset="36">
public static class GridArea
{
    public static readonly DependencyProperty NameProperty =
        DependencyProperty.RegisterAttached("Name", typeof(string), typeof(GridArea), new PropertyMetadata(null));
    public static readonly DependencyProperty InsetProperty =
        DependencyProperty.RegisterAttached("Inset", typeof(double), typeof(GridArea), new PropertyMetadata(0.0));

    public static string? GetName(DependencyObject o) => (string?)o.GetValue(NameProperty);
    public static void SetName(DependencyObject o, string? v) => o.SetValue(NameProperty, v);
    public static double GetInset(DependencyObject o) => (double)o.GetValue(InsetProperty);
    public static void SetInset(DependencyObject o, double v) => o.SetValue(InsetProperty, v);
}

/// Marks a view inside an area. Probes with the same Edge share one leading edge.
/// Edge "" for views that sit side by side.
///   <TextBlock ui:GridProbe.Name="title" ui:GridProbe.Area="page" />
public static class GridProbe
{
    public static readonly DependencyProperty NameProperty =
        DependencyProperty.RegisterAttached("Name", typeof(string), typeof(GridProbe), new PropertyMetadata(null));
    public static readonly DependencyProperty AreaProperty =
        DependencyProperty.RegisterAttached("Area", typeof(string), typeof(GridProbe), new PropertyMetadata(null));
    public static readonly DependencyProperty EdgeProperty =
        DependencyProperty.RegisterAttached("Edge", typeof(string), typeof(GridProbe), new PropertyMetadata("lead"));

    public static string? GetName(DependencyObject o) => (string?)o.GetValue(NameProperty);
    public static void SetName(DependencyObject o, string? v) => o.SetValue(NameProperty, v);
    public static string? GetArea(DependencyObject o) => (string?)o.GetValue(AreaProperty);
    public static void SetArea(DependencyObject o, string? v) => o.SetValue(AreaProperty, v);
    public static string GetEdge(DependencyObject o) => (string)o.GetValue(EdgeProperty);
    public static void SetEdge(DependencyObject o, string v) => o.SetValue(EdgeProperty, v);
}

public sealed record ProbeRect(string Name, string Area, string Edge, double X, double Y, double Width, double Height);
public sealed record AreaRect(string Name, double Inset, double X, double Y, double Width, double Height);
/// Scale is physical pixels per epx (1.25 at 125%). Edges snap to physical pixels, so at 125% they sit on multiples of 0.8 epx.
public sealed record LayoutFile(double Scale, double WindowWidth, double WindowHeight, List<AreaRect> Areas, List<ProbeRect> Probes);

/// Measures every marked area and probe in the window, in epx from the window's top left.
public static class GridProbes
{
    public static LayoutFile Collect(FrameworkElement root)
    {
        var areas = new List<AreaRect>();
        var probes = new List<ProbeRect>();
        foreach (var element in Descendants(root))
        {
            if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0) continue;
            var r = element.TransformToVisual(root).TransformBounds(new(0, 0, element.ActualWidth, element.ActualHeight));
            if (GridArea.GetName(element) is { } area)
                areas.Add(new(area, GridArea.GetInset(element), r.X, r.Y, r.Width, r.Height));
            if (GridProbe.GetName(element) is { } probe)
                probes.Add(new(probe, GridProbe.GetArea(element) ?? "", GridProbe.GetEdge(element), r.X, r.Y, r.Width, r.Height));
        }
        return new(root.XamlRoot?.RasterizationScale ?? 1, root.ActualWidth, root.ActualHeight, areas, probes);
    }

    public static void Write(FrameworkElement root, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(Collect(root), AppJson.Default.LayoutFile));

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe) yield return fe;
            foreach (var d in Descendants(child)) yield return d;
        }
    }
}

/// APP_GRID=1 draws an 8 epx grid, each area in blue with its inset dashed, and each probe in pink.
public static class GridOverlay
{
    public static bool Enabled => Environment.GetEnvironmentVariable("APP_GRID") == "1";

    public static void Draw(Canvas canvas, FrameworkElement root)
    {
        canvas.Children.Clear();
        var line = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 0, 128));
        for (double x = 0; x < root.ActualWidth; x += Space.Sm)
            canvas.Children.Add(new Line { X1 = x, X2 = x, Y2 = root.ActualHeight, Stroke = line, StrokeThickness = 0.5 });
        for (double y = 0; y < root.ActualHeight; y += Space.Sm)
            canvas.Children.Add(new Line { Y1 = y, Y2 = y, X2 = root.ActualWidth, Stroke = line, StrokeThickness = 0.5 });

        var layout = GridProbes.Collect(root);
        foreach (var a in layout.Areas)
        {
            Add(canvas, a.X, a.Y, a.Width, a.Height, Colors.DodgerBlue, null, a.Name);
            if (a.Inset > 0)
                Add(canvas, a.X + a.Inset, a.Y + a.Inset, a.Width - 2 * a.Inset, a.Height - 2 * a.Inset, Colors.DodgerBlue, [4, 4], null);
        }
        foreach (var p in layout.Probes) Add(canvas, p.X, p.Y, p.Width, p.Height, Colors.DeepPink, null, p.Name);
    }

    private static void Add(Canvas canvas, double x, double y, double w, double h, Windows.UI.Color color, double[]? dash, string? label)
    {
        if (w <= 0 || h <= 0) return;
        var rect = new Rectangle { Width = w, Height = h, Stroke = new SolidColorBrush(color), StrokeThickness = 1 };
        if (dash is not null) rect.StrokeDashArray = [.. dash];
        Canvas.SetLeft(rect, x); // grid: ok overlay draws at measured positions
        Canvas.SetTop(rect, y);  // grid: ok
        canvas.Children.Add(rect);
        if (label is null) return;
        var text = new TextBlock { Text = label, FontSize = 10, Foreground = new SolidColorBrush(color) };
        Canvas.SetLeft(text, x + 2); // grid: ok
        Canvas.SetTop(text, y);      // grid: ok
        canvas.Children.Add(text);
    }
}
