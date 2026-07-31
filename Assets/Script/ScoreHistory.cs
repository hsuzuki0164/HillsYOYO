using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PPYY
{
    [Serializable]
    public class ScoreEntry
    {
        public string label; // 記録したプレイヤー側。"1P" / "2P" を想定
        public int score;
    }

    [Serializable]
    class ScoreHistoryData
    {
        public List<ScoreEntry> entries = new List<ScoreEntry>();
    }

    // 過去の全プレイの最終スコアをPlayerPrefsに永続保存し、結果発表画面のランキング表示に使う。
    // ゲームを再起動しても記録は残る（端末ローカルの履歴）
    public static class ScoreHistory
    {
        const string PrefsKey = "PPYY_ScoreHistory";

        // 履歴に新しい記録を追加して保存し、追加したエントリを返す（GetRankにそのまま渡せる）
        public static ScoreEntry AddEntry(string label, int score)
        {
            var data = Load();
            var entry = new ScoreEntry { label = label, score = score };
            data.entries.Add(entry);
            Save(data);
            return entry;
        }

        // スコアの高い順にcount件だけ返す
        public static List<ScoreEntry> GetTopEntries(int count)
        {
            var data = Load();
            return data.entries.OrderByDescending(e => e.score).Take(count).ToList();
        }

        // 履歴全体の中での順位（1位始まり）。同点は同順位になる（1,2,2,4方式）
        public static int GetRank(ScoreEntry entry)
        {
            var data = Load();
            int higherCount = data.entries.Count(e => e.score > entry.score);
            return higherCount + 1;
        }

        static ScoreHistoryData Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(json)) return new ScoreHistoryData();

            var data = JsonUtility.FromJson<ScoreHistoryData>(json);
            return data ?? new ScoreHistoryData();
        }

        static void Save(ScoreHistoryData data)
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
