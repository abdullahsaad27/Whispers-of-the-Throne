using System;
using System.Collections.Generic;

namespace KingdomBlind_CSharp.Models
{
    public class Governor
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string ProvinceId { get; set; }
        
        // Caching the name for easy display, but logic uses ProvinceId
        public string ProvinceName { get; set; } 
        
        public int Age { get; set; }
        
        // Political Stats
        public int Loyalty { get; set; } = 100; // 0 to 100
        public int OpinionOfKing { get; set; } = 0; // -100 to 100
        public int Ambition { get; set; } = 50; // 0 to 100
        public int Fear { get; set; } = 50; // 0 to 100
        
        // Power Stats
        public int Influence { get; set; } = 50; // 0 to 100
        public int MilitaryPower { get; set; } = 50; // 0 to 100
        public int Wealth { get; set; } = 100;
        
        // Status
        public bool IsRebellious { get; set; } = false;
        public bool IsImprisoned { get; set; } = false;
        
        public string CurrentMood { get; set; } = "Neutral"; // Loyal, Neutral, Angry, Afraid, Opportunist, Rebellious
        public string CurrentGoal { get; set; } = "";
        public string SecretPlan { get; set; } = "";
        public int DaysUntilNextMove { get; set; } = 30;
        
        public List<string> Traits { get; set; } = new List<string>();

        public void UpdateMood()
        {
            if (IsRebellious) CurrentMood = "Rebellious";
            else if (OpinionOfKing < -50 && Ambition > 70) CurrentMood = "Opportunist";
            else if (OpinionOfKing < -30) CurrentMood = "Angry";
            else if (Fear > 75) CurrentMood = "Afraid";
            else if (Loyalty > 80 && OpinionOfKing > 50) CurrentMood = "Loyal";
            else CurrentMood = "Neutral";
        }
    }
}
