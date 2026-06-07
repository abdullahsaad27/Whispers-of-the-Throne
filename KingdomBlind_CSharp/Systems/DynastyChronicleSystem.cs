using System;
using System.Linq;
using System.Text;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class DynastyChronicleSystem
    {
        public static DynastyChronicleEntry RecordEvent(
            GameState state,
            string category,
            string title,
            string description,
            int gloryChange,
            int severity = 1)
        {
            state.ReconcileOldSaves();
            int today = DiplomacySystem.GetCurrentDayNumber(state);
            var existing = state.DynastyChronicle
                .FirstOrDefault(e => e.DayNumber == today && e.Category == category && e.Title == title);
            if (existing != null)
                return existing;

            var entry = new DynastyChronicleEntry
            {
                DayNumber = today,
                DateText = state.Time?.GetDateString() ?? "",
                Category = category,
                Title = title,
                Description = description,
                GloryChange = gloryChange,
                Severity = Math.Clamp(severity, 1, 5)
            };

            state.DynastyChronicle.Add(entry);
            state.DynastyGlory = Math.Clamp(state.DynastyGlory + gloryChange, -1000, 5000);

            if (state.DynastyChronicle.Count > 160)
                state.DynastyChronicle.RemoveRange(0, state.DynastyChronicle.Count - 160);

            return entry;
        }

        public static string GetGloryRank(int glory)
        {
            if (glory < 0) return "سلالة منسية";
            if (glory < 100) return "بيت محلي";
            if (glory < 250) return "سلالة محترمة";
            if (glory < 500) return "سلالة عظيمة";
            return "سلالة أسطورية";
        }

        public static string GetChronicleReport(GameState state)
        {
            state.ReconcileOldSaves();
            var sb = new StringBuilder();
            sb.AppendLine("كتاب العرش وسجل السلالة");
            sb.AppendLine($"مجد السلالة: {state.DynastyGlory} - {GetGloryRank(state.DynastyGlory)}");
            sb.AppendLine();

            var entries = state.DynastyChronicle
                .OrderByDescending(e => e.DayNumber)
                .ThenByDescending(e => e.Severity)
                .Take(20)
                .ToList();

            if (entries.Count == 0)
            {
                sb.AppendLine("لم يسجل كتاب العرش أحداثاً كبرى بعد.");
                return sb.ToString().Trim();
            }

            foreach (var entry in entries)
            {
                string sign = entry.GloryChange >= 0 ? "+" : "";
                sb.AppendLine($"- {entry.DateText}: {entry.Title} ({sign}{entry.GloryChange} مجد)");
                sb.AppendLine($"  {entry.Description}");
            }

            return sb.ToString().Trim();
        }

        public static string GetLegacySummary(GameState state)
        {
            state.ReconcileOldSaves();
            int wars = state.DynastyChronicle.Count(e => e.Category == "War");
            int trade = state.DynastyChronicle.Count(e => e.Category == "Trade");
            int crisis = state.DynastyChronicle.Count(e => e.GloryChange < 0);
            int succession = state.DynastyChronicle.Count(e => e.Category == "Succession");

            return $"رتبة السلالة: {GetGloryRank(state.DynastyGlory)}. " +
                   $"الحروب المسجلة: {wars}. إنجازات التجارة: {trade}. أزمات السلالة: {crisis}. أحداث الخلافة: {succession}.";
        }
    }
}
