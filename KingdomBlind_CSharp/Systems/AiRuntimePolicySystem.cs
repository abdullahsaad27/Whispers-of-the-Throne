using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class AiRuntimePolicySystem
    {
        public static bool IsEnabledForLabel(AiActorSettings settings, string label)
        {
            settings ??= new AiActorSettings();
            string text = label ?? "";

            if (text.Contains("زوج") || text.Contains("ملكة"))
                return settings.ApplyToSpouses;
            if (text.Contains("وزير") || text.Contains("مستشار") || text.Contains("قائد") || text.Contains("استخبارات") || text.Contains("قاضي"))
                return settings.ApplyToMinisters;
            if (text.Contains("وريث") || text.Contains("ولي العهد"))
                return settings.ApplyToHeirs;
            if (text.Contains("والي"))
                return settings.ApplyToGovernors;
            if (text.Contains("فصيل") || text.Contains("نبلاء") || text.Contains("تمرد"))
                return settings.ApplyToFactions;
            if (text.Contains("ملك") || text.Contains("سلطان") || text.Contains("خليفة") || text.Contains("جار") || text.Contains("دولة"))
                return settings.ApplyToNeighborRulers;

            return settings.ApplyToOtherCharacters;
        }

        public static bool AllowsDecisionsForSource(AiActorSettings settings, string sourceType)
        {
            settings ??= new AiActorSettings();
            return sourceType switch
            {
                "Councilor" => settings.AllowAiMinisterDecisions,
                "Spouse" => settings.AllowAiSpouseDecisions,
                "Neighbor" => settings.AllowAiNeighborDecisions,
                "Governor" => settings.AllowAiGovernorDecisions,
                "Faction" => settings.AllowAiFactionDecisions,
                _ => false
            };
        }

        public static string GetSummary(AiActorSettings settings)
        {
            settings ??= new AiActorSettings();
            return
                $"الحوارات الذكية: {(settings.SmartDialoguesEnabled ? "مفعلة" : "معطلة، تستخدم النصوص المحلية")}\n" +
                $"مستوى الإطالة: {GetLengthDisplay(settings.DialogueLengthLevel)}\n" +
                $"الأفعال التلقائية: {(settings.AllowAutonomousActions ? "مسموحة" : "معطلة")}\n" +
                $"حد ميزانية الأفعال التلقائية شهرياً: {settings.MaxAutonomousMonthlyBudget} ذهب\n" +
                $"SuperTonic لحوارات AI: {(settings.UseSuperTonicForAiDialogue ? "مفعل" : "معطل")}\n" +
                $"الزوجات: {(settings.ApplyToSpouses ? "نعم" : "لا")}\n" +
                $"الوزراء والمجلس: {(settings.ApplyToMinisters ? "نعم" : "لا")}\n" +
                $"الورثة: {(settings.ApplyToHeirs ? "نعم" : "لا")}\n" +
                $"الولاة: {(settings.ApplyToGovernors ? "نعم" : "لا")}\n" +
                $"الفصائل: {(settings.ApplyToFactions ? "نعم" : "لا")}\n" +
                $"حكام الدول المجاورة: {(settings.ApplyToNeighborRulers ? "نعم" : "لا")}\n" +
                $"الأعداء: {(settings.ApplyToEnemies ? "نعم" : "لا")}\n" +
                $"شخصيات أخرى: {(settings.ApplyToOtherCharacters ? "نعم" : "لا")}\n" +
                $"قرارات الوزراء بالـ AI: {(settings.AllowAiMinisterDecisions ? "نعم" : "لا")}\n" +
                $"قرارات الزوجات بالـ AI: {(settings.AllowAiSpouseDecisions ? "نعم" : "لا")}\n" +
                $"قرارات الحكام المجاورين تجاهك بالـ AI: {(settings.AllowAiNeighborDecisions ? "نعم" : "لا")}\n" +
                $"إدارة الدول المجاورة لشؤونها الداخلية بالـ AI: {(settings.AllowAiNeighborRealmManagement ? "نعم" : "لا")}\n" +
                $"قرارات الولاة الداخلية بالـ AI: {(settings.AllowAiGovernorDecisions ? "نعم" : "لا")}\n" +
                $"قرارات الفصائل بالـ AI: {(settings.AllowAiFactionDecisions ? "نعم" : "لا")}";
        }

        private static string GetLengthDisplay(AiDialogueLengthLevel level)
        {
            return level switch
            {
                AiDialogueLengthLevel.Brief => "مختصر",
                AiDialogueLengthLevel.Detailed => "تفصيلي",
                _ => "عادي"
            };
        }
    }
}
