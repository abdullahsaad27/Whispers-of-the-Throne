using System;
using System.Collections.Generic;
using System.Linq;

namespace KingdomBlind_CSharp.Systems
{
    public enum AiRoutedCommand
    {
        None,
        Governance,
        Court,
        Economy,
        WarDiplomacy,
        AdvancedDiplomacy,
        Provinces,
        Council,
        DynastyChronicle,
        CurrentSummary,
        SuggestedDecision
    }

    public static class AiCommandRouterSystem
    {
        private static readonly Dictionary<AiRoutedCommand, string> Descriptions = new Dictionary<AiRoutedCommand, string>
        {
            { AiRoutedCommand.Governance, "الحكم والتقارير" },
            { AiRoutedCommand.Court, "القصر والمجلس والدين" },
            { AiRoutedCommand.Economy, "الاقتصاد والمقاطعات" },
            { AiRoutedCommand.WarDiplomacy, "الحرب والدبلوماسية والاستخبارات" },
            { AiRoutedCommand.AdvancedDiplomacy, "الدبلوماسية المتقدمة" },
            { AiRoutedCommand.Provinces, "شؤون المقاطعات والموارد" },
            { AiRoutedCommand.Council, "مجلس البلاط والمستشارين" },
            { AiRoutedCommand.DynastyChronicle, "كتاب العرش ومجد السلالة" },
            { AiRoutedCommand.CurrentSummary, "ملخص الملك الآن" },
            { AiRoutedCommand.SuggestedDecision, "اقتراح قرار" }
        };

        public static bool TryRoute(string rawCommand, out AiRoutedCommand command)
        {
            string normalized = Normalize(rawCommand);
            command = normalized switch
            {
                "1" or "واحد" or "one" or "governance" => AiRoutedCommand.Governance,
                "2" or "اثنان" or "اثنين" or "two" or "court" => AiRoutedCommand.Court,
                "3" or "ثلاثة" or "three" or "economy" => AiRoutedCommand.Economy,
                "4" or "اربعة" or "أربعة" or "four" or "war" => AiRoutedCommand.WarDiplomacy,
                "5" or "خمسة" or "five" or "diplomacy" => AiRoutedCommand.AdvancedDiplomacy,
                "6" or "ستة" or "six" or "provinces" => AiRoutedCommand.Provinces,
                "7" or "سبعة" or "seven" or "council" => AiRoutedCommand.Council,
                "8" or "ثمانية" or "eight" or "chronicle" => AiRoutedCommand.DynastyChronicle,
                "9" or "تسعة" or "nine" or "summary" => AiRoutedCommand.CurrentSummary,
                "10" or "عشرة" or "ten" or "advice" => AiRoutedCommand.SuggestedDecision,
                _ => AiRoutedCommand.None
            };

            return command != AiRoutedCommand.None;
        }

        public static string Describe(AiRoutedCommand command)
        {
            return Descriptions.TryGetValue(command, out var text) ? text : "لا يوجد مسار";
        }

        public static string GetProtocolPrompt()
        {
            return "بروتوكول النماذج الصغيرة:\n" +
                   "أجب برقم واحد فقط عندما تريد فتح شاشة داخل اللعبة.\n" +
                   "1 الحكم والتقارير\n" +
                   "2 القصر والمجلس والدين\n" +
                   "3 الاقتصاد والمقاطعات\n" +
                   "4 الحرب والدبلوماسية والاستخبارات\n" +
                   "5 الدبلوماسية المتقدمة\n" +
                   "6 المقاطعات والموارد\n" +
                   "7 مجلس المستشارين\n" +
                   "8 كتاب العرش\n" +
                   "9 ملخص الملك الآن\n" +
                   "10 اقترح قراراً\n" +
                   "إذا لم تكن متأكداً، أجب 9.";
        }

        private static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "";

            return raw.Trim()
                .Replace("إ", "ا")
                .Replace("أ", "ا")
                .Replace("آ", "ا")
                .ToLowerInvariant();
        }
    }
}
