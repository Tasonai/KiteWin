using System.Globalization;
using Microsoft.Data.Sqlite;

namespace KiteWin.Core;

/// The database: one SQLite file with WAL. The app owns the only connection.
/// Schema changes go in Migrations, one step per version, and never edit a step that shipped.
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

    /// Each step runs once, in order. A new column always has a DEFAULT, so old files keep loading.
    private static readonly string[] Migrations =
    [
        """
        CREATE TABLE items (
            id TEXT PRIMARY KEY,
            title TEXT NOT NULL DEFAULT '',
            notes TEXT NOT NULL DEFAULT '',
            starred INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL
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
        cmd.CommandText = "SELECT id, title, notes, starred, created_at FROM items ORDER BY created_at DESC;";
        using var reader = cmd.ExecuteReader();
        var items = new List<Item>();
        while (reader.Read())
        {
            items.Add(new Item(reader.GetString(1))
            {
                Id = Guid.Parse(reader.GetString(0)),
                Notes = reader.GetString(2),
                IsStarred = reader.GetInt64(3) != 0,
                CreatedAt = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
            });
        }
        return items;
    }

    public int Count() => Convert.ToInt32(Scalar("SELECT COUNT(*) FROM items;"), CultureInfo.InvariantCulture);

    /// Inserts or updates one item.
    public void Save(Item item)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO items (id, title, notes, starred, created_at) VALUES ($id, $title, $notes, $starred, $created)
            ON CONFLICT(id) DO UPDATE SET title = $title, notes = $notes, starred = $starred;
            """;
        cmd.Parameters.AddWithValue("$id", item.Id.ToString());
        cmd.Parameters.AddWithValue("$title", item.Title);
        cmd.Parameters.AddWithValue("$notes", item.Notes);
        cmd.Parameters.AddWithValue("$starred", item.IsStarred ? 1 : 0);
        cmd.Parameters.AddWithValue("$created", item.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
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

    public void Dispose() => _db.Dispose();
}
