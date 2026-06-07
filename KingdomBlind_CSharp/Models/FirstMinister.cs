using System;

namespace KingdomBlind_CSharp.Models
{
    public class FirstMinister
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "غير معين";
        
        public int Loyalty { get; set; } = 50;
        public int OpinionOfKing { get; set; } = 50;
        public int Ambition { get; set; } = 50;
        public int Influence { get; set; } = 50;
        
        public int AdministrativeSkill { get; set; } = 5;
        public int DiplomacySkill { get; set; } = 5;
        public int Integrity { get; set; } = 50; // نزاهة الوزير

        public bool IsAppointed { get; set; } = false;
        public string CurrentTask { get; set; } = "انتظار الأوامر";
        public string TaskTarget { get; set; } = "";
        public int TaskDaysRemaining { get; set; } = 0;
        public int MonthlyBudgetPercent { get; set; } = 0;
        
        // إذا كان طموحه مرتفعاً ونزاهته منخفضة قد يختلس أو يتآمر
        public bool IsCorrupt { get; set; } = false; 
    }
}
