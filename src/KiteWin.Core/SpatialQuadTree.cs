using System;
using System.Collections.Generic;

namespace KiteWin.Core;

/// <summary>
/// 2D axis-aligned bounding box for spatial indexing.
/// </summary>
public readonly struct SpatialRect : IEquatable<SpatialRect>
{
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }

    public SpatialRect(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double MinX => X;
    public double MaxX => X + Width;
    public double MinY => Y;
    public double MaxY => Y + Height;

    public bool Contains(double px, double py) =>
        px >= MinX && px <= MaxX && py >= MinY && py <= MaxY;

    public bool Intersects(SpatialRect other) =>
        !(other.MinX > MaxX || other.MaxX < MinX || other.MinY > MaxY || other.MaxY < MinY);

    public bool Contains(SpatialRect other) =>
        other.MinX >= MinX && other.MaxX <= MaxX && other.MinY >= MinY && other.MaxY <= MaxY;

    public bool Equals(SpatialRect other) =>
        X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);

    public override bool Equals(object? obj) => obj is SpatialRect other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public static bool operator ==(SpatialRect left, SpatialRect right) => left.Equals(right);
    public static bool operator !=(SpatialRect left, SpatialRect right) => !left.Equals(right);
}

/// <summary>
/// Generic item stored within the QuadTree index.
/// </summary>
public sealed class SpatialItem
{
    public string Id { get; }
    public SpatialRect Bounds { get; }
    public string Kind { get; }

    public SpatialItem(string id, SpatialRect bounds, string kind = "node")
    {
        Id = id;
        Bounds = bounds;
        Kind = kind;
    }
}

/// <summary>
/// High-performance 2D spatial partitioning QuadTree for canvas view frustum culling.
/// Enables 120 FPS rendering on massive architecture graphs.
/// </summary>
public sealed class SpatialQuadTree
{
    public SpatialRect Boundary { get; }
    public int Capacity { get; }
    public int MaxDepth { get; }
    public int Depth { get; }

    private readonly List<SpatialItem> _items = new();
    private SpatialQuadTree[]? _children;

    public SpatialQuadTree(SpatialRect boundary, int capacity = 16, int maxDepth = 8, int depth = 0)
    {
        Boundary = boundary;
        Capacity = capacity;
        MaxDepth = maxDepth;
        Depth = depth;
    }

    public int Count
    {
        get
        {
            int total = _items.Count;
            if (_children != null)
            {
                foreach (var child in _children)
                {
                    total += child.Count;
                }
            }
            return total;
        }
    }

    public bool Insert(SpatialItem item)
    {
        if (!Boundary.Intersects(item.Bounds))
        {
            return false;
        }

        if (_children != null)
        {
            foreach (var child in _children)
            {
                if (child.Boundary.Contains(item.Bounds))
                {
                    return child.Insert(item);
                }
            }
            _items.Add(item);
            return true;
        }

        _items.Add(item);

        if (_items.Count > Capacity && Depth < MaxDepth)
        {
            Subdivide();
        }

        return true;
    }

    private void Subdivide()
    {
        double halfW = Boundary.Width / 2.0;
        double halfH = Boundary.Height / 2.0;
        double x = Boundary.X;
        double y = Boundary.Y;
        int nextDepth = Depth + 1;

        var newChildren = new SpatialQuadTree[4]
        {
            new SpatialQuadTree(new SpatialRect(x, y, halfW, halfH), Capacity, MaxDepth, nextDepth),
            new SpatialQuadTree(new SpatialRect(x + halfW, y, halfW, halfH), Capacity, MaxDepth, nextDepth),
            new SpatialQuadTree(new SpatialRect(x, y + halfH, halfW, halfH), Capacity, MaxDepth, nextDepth),
            new SpatialQuadTree(new SpatialRect(x + halfW, y + halfH, halfW, halfH), Capacity, MaxDepth, nextDepth)
        };
        _children = newChildren;

        var remaining = new List<SpatialItem>();
        foreach (var it in _items)
        {
            bool placed = false;
            foreach (var child in newChildren)
            {
                if (child.Boundary.Contains(it.Bounds))
                {
                    child.Insert(it);
                    placed = true;
                    break;
                }
            }
            if (!placed)
            {
                remaining.Add(it);
            }
        }
        _items.Clear();
        _items.AddRange(remaining);
    }

    public List<SpatialItem> Query(SpatialRect range)
    {
        var results = new List<SpatialItem>();
        QueryInternal(range, results);
        return results;
    }

    private void QueryInternal(SpatialRect range, List<SpatialItem> results)
    {
        if (!Boundary.Intersects(range))
        {
            return;
        }

        foreach (var it in _items)
        {
            if (it.Bounds.Intersects(range))
            {
                results.Add(it);
            }
        }

        if (_children != null)
        {
            foreach (var child in _children)
            {
                child.QueryInternal(range, results);
            }
        }
    }

    public List<SpatialItem> QueryFrustum(SpatialRect viewport, double margin = 100.0)
    {
        var bufferedRange = new SpatialRect(
            viewport.X - margin,
            viewport.Y - margin,
            viewport.Width + (margin * 2.0),
            viewport.Height + (margin * 2.0)
        );
        return Query(bufferedRange);
    }

    public void Clear()
    {
        _items.Clear();
        _children = null;
    }
}
