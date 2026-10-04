using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

// Dev/testing tool: lets you populate a REAL guild (created or joined by
// an actual player — as opposed to the 30 procedural "fake_" browse
// guilds) with 1-4 randomly generated fake members, for visual testing
// without needing real additional player accounts. Persists via
// PlayerPrefs, keyed by guildId, so populated guilds stay populated
// across app restarts.
public static class GuildDevTools
{
    private const string KeyPrefix = "RabbitUniverse_GuildFakeMembers_";

    // Wrapper classes purely for JsonUtility serialization (it can't
    // serialize a List<RowData> or tuples directly).
    [Serializable]
    private class SerializableRow
    {
        public int rank;
        public string playerId;
        public string playerName;
        public int bigBangCount;
        public long lifetimeRabbits;
        public string country;
    }

    [Serializable]
    private class SerializableRowList
    {
        public List<SerializableRow> rows = new List<SerializableRow>();
    }

    public static List<(int rank, LeaderboardUI.RowData row)> GetPersistedFakeMembers(string guildId)
    {
        List<(int rank, LeaderboardUI.RowData row)> result = new List<(int, LeaderboardUI.RowData)>();
        if (string.IsNullOrEmpty(guildId)) return result;

        string json = PlayerPrefs.GetString(KeyPrefix + guildId, "");
        if (string.IsNullOrEmpty(json)) return result;

        SerializableRowList parsed;
        try { parsed = JsonUtility.FromJson<SerializableRowList>(json); }
        catch { return result; }

        if (parsed?.rows == null) return result;

        foreach (SerializableRow r in parsed.rows)
        {
            result.Add((r.rank, new LeaderboardUI.RowData
            {
                playerId = r.playerId,
                playerName = r.playerName,
                bigBangCount = r.bigBangCount,
                lifetimeRabbits = r.lifetimeRabbits,
                country = r.country,
                equippedNickname = "",
                equippedBackground = "",
                guildId = guildId
            }));
        }

        return result;
    }

    // Overwrites this guild's persisted fake members with a fresh random
    // set of 1-4. Call this from the dev tool button.
    public static List<(int rank, LeaderboardUI.RowData row)> PopulateGuildWithFakePlayers(string guildId)
    {
        if (string.IsNullOrEmpty(guildId)) return new List<(int, LeaderboardUI.RowData)>();

        System.Random rng = new System.Random();
        string[] fakeFirstNames = { "Alex", "Sam", "Jordan", "Casey", "Riley", "Morgan", "Taylor", "Quinn" };
        string[] countries = { "US", "FR", "CA", "GB", "DE", "BR", "JP", "AU" };

        int count = rng.Next(1, 5); // 1 to 4 inclusive
        SerializableRowList toSave = new SerializableRowList();

        for (int i = 0; i < count; i++)
        {
            toSave.rows.Add(new SerializableRow
            {
                rank = rng.Next(1, 999),
                playerId = $"fake_dev_{guildId}_{i}_{rng.Next(10000)}",
                playerName = fakeFirstNames[rng.Next(fakeFirstNames.Length)] + rng.Next(10, 999),
                bigBangCount = rng.Next(5, 60),
                lifetimeRabbits = rng.Next(1000, 500_000),
                country = countries[rng.Next(countries.Length)]
            });
        }

        string json = JsonUtility.ToJson(toSave);
        PlayerPrefs.SetString(KeyPrefix + guildId, json);
        PlayerPrefs.Save();

        return GetPersistedFakeMembers(guildId);
    }

    public static void ClearFakeMembers(string guildId)
    {
        if (string.IsNullOrEmpty(guildId)) return;
        PlayerPrefs.DeleteKey(KeyPrefix + guildId);
        PlayerPrefs.Save();
    }
}
