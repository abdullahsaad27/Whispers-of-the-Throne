using System;

namespace KingdomBlind_CSharp.Models
{
    public class Disaster
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string ProvinceId { get; set; } = "";
        public string ProvinceName { get; set; } = "";
        public int DaysRemaining { get; set; } = 30;
        
        // Effects
        public int IncomePenalty { get; set; } = 0;
        public int SatisfactionPenalty { get; set; } = 0;
    }
}
