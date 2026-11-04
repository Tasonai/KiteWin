using System.Globalization;
using Microsoft.Data.Sqlite;

namespace KiteWin.Core;

/// <summary>
/// SQLite database with WAL mode and PRAGMA user_version schema migrations.
/// Holds architectural items with full metadata and prompt compilation cache.
/// </summary>
public sealed class ItemStore : IDisposable
{
    private readonly SqliteConnection _db;

    public ItemStore(string path)
    {
        Path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        _db.Open();
        Exec("PRAGMA journal_mode=WAL;");
        Migrate();
    }

    public string Path { get; }

    /// <summary>
    /// Sequential schema migrations. New columns always have DEFAULT so old files keep loading.
    /// </summary>
    private static readonly string[] Migrations =
    [
        """
        CREATE TABLE items (
            id TEXT PRIMARY KEY,
            title TEXT NOT NULL DEFAULT '',
            concept TEXT NOT NULL DEFAULT '',
            notes TEXT NOT NULL DEFAULT '',
            platform TEXT NOT NULL DEFAULT 'Web',
            style TEXT NOT NULL DEFAULT 'Editorial & Swiss Modern',
            status TEXT NOT NULL DEFAULT 'backlog',
            tags TEXT NOT NULL DEFAULT '',
            generated_prompt TEXT NOT NULL DEFAULT '',
            stitch_prompt TEXT NOT NULL DEFAULT '',
            targeted_edit_prompt TEXT NOT NULL DEFAULT '',
            starred INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );
        """,
    ];

    private void Migrate()
    {
        var version = Convert.ToInt32(Scalar("PRAGMA user_version;"), CultureInfo.InvariantCulture);
        for (var step = version; step < Migrations.Length; step++)
        {
            using var tx = _db.BeginTransaction();
            Exec(Migrations[step]);
            Exec($"PRAGMA user_version = {step + 1};");
            tx.Commit();
        }
    }

    public List<Item> All()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT id, title, concept, notes, platform, style, status, tags, 
                   generated_prompt, stitch_prompt, targeted_edit_prompt, starred, created_at, updated_at 
            FROM items 
            ORDER BY created_at DESC;
            """;
        using var reader = cmd.ExecuteReader();
        var items = new List<Item>();
        while (reader.Read())
        {
            items.Add(new Item(reader.GetString(1))
            {
                Id = Guid.Parse(reader.GetString(0)),
                Concept = reader.GetString(2),
                Notes = reader.GetString(3),
                Platform = reader.GetString(4),
                Style = reader.GetString(5),
                Status = reader.GetString(6),
                Tags = reader.GetString(7),
                GeneratedPrompt = reader.GetString(8),
                StitchPrompt = reader.GetString(9),
                TargetedEditPrompt = reader.GetString(10),
                IsStarred = reader.GetInt64(11) != 0,
                CreatedAt = DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(13), CultureInfo.InvariantCulture),
            });
        }
        return items;
    }

    public int Count() => Convert.ToInt32(Scalar("SELECT COUNT(*) FROM items;"), CultureInfo.InvariantCulture);

    /// <summary>
    /// Inserts or updates one item atomically.
    /// </summary>
    public void Save(Item item)
    {
        item.UpdatedAt = DateTimeOffset.Now;
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO items (id, title, concept, notes, platform, style, status, tags, 
                               generated_prompt, stitch_prompt, targeted_edit_prompt, starred, created_at, updated_at) 
            VALUES ($id, $title, $concept, $notes, $platform, $style, $status, $tags, 
                    $gen, $stitch, $edit, $starred, $created, $updated)
            ON CONFLICT(id) DO UPDATE SET 
                title = $title, 
                concept = $concept, 
                notes = $notes, 
                platform = $platform, 
                style = $style, 
                status = $status, 
                tags = $tags, 
                generated_prompt = $gen, 
                stitch_prompt = $stitch, 
                targeted_edit_prompt = $edit, 
                starred = $starred, 
                updated_at = $updated;
            """;
        cmd.Parameters.AddWithValue("$id", item.Id.ToString());
        cmd.Parameters.AddWithValue("$title", item.Title);
        cmd.Parameters.AddWithValue("$concept", item.Concept);
        cmd.Parameters.AddWithValue("$notes", item.Notes);
        cmd.Parameters.AddWithValue("$platform", item.Platform);
        cmd.Parameters.AddWithValue("$style", item.Style);
        cmd.Parameters.AddWithValue("$status", item.Status);
        cmd.Parameters.AddWithValue("$tags", item.Tags);
        cmd.Parameters.AddWithValue("$gen", item.GeneratedPrompt);
        cmd.Parameters.AddWithValue("$stitch", item.StitchPrompt);
        cmd.Parameters.AddWithValue("$edit", item.TargetedEditPrompt);
        cmd.Parameters.AddWithValue("$starred", item.IsStarred ? 1 : 0);
        cmd.Parameters.AddWithValue("$created", item.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$updated", item.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    public void Delete(Guid id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.ExecuteNonQuery();
    }

    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
