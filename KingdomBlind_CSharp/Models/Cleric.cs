using System;

namespace KingdomBlind_CSharp.Models
{
    public class Cleric
    {
        public string Name { get; set; } = "الأسقف الأكبر";
        public int Loyalty { get; set; } = 50;
        public int OpinionOfKing { get; set; } = 50;
        public int Influence { get; set; } = 50;
        public int LearningSkill { get; set; } = 10;
        public int Piety { get; set; } = 50; // Or ReligiousAuthority
        public int Ambition { get; set; } = 50;
        public bool IsSupportive { get; set; } = true;
        public bool IsOpposingKing { get; set; } = false;
    }
}
