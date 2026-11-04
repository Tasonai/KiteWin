using KiteWin.Core;

namespace KiteWin;

/// Sample data for snapshots: APP_SEED=1 with APP_STORE in %TEMP% fills an empty
/// store with realistic items (short and long titles, starred and not).
/// Replace these with your app's own types. Seed only what a person can
/// create in the app, or the snapshots show features no one can use.
public static class SeedData
{
    public static void InsertIfRequested(ItemStore store)
    {
        if (Environment.GetEnvironmentVariable("APP_SEED") != "1") return;
        if (!AppPaths.IsTestStore)
        {
            AgentLog.Line("APP_SEED ignored: APP_STORE must be a file in %TEMP%");
            return;
        }
        if (store.Count() > 0) return;
        (string Title, bool Starred)[] samples =
        [
            ("Book the dentist", false),
            ("Read the article about local-first software and write down three ideas worth trying", true),
            ("Renew passport", false),
            ("Plan the weekend trip", true),
            ("Call Anna", false),
            ("Back up the photo library to the external drive before the trip", false),
            ("Groceries", false),
            ("Draft the quarterly review", true),
        ];
        for (var i = 0; i < samples.Length; i++)
        {
            store.Save(new Item(samples[i].Title)
            {
                IsStarred = samples[i].Starred,
                CreatedAt = DateTimeOffset.Now.AddHours(-i),
            });
        }
    }
}
