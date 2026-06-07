using System;

namespace KingdomBlind_CSharp.Models
{
    public class Child
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string MotherName { get; set; } = "";
        public string MotherSpouseId { get; set; } = "";
        public int Age { get; set; } = 0;
        public bool IsDead { get; set; } = false;
        public bool IsHeir { get; set; } = false;
        
        // Skills
        public int MilitarySkill { get; set; } = 1;
        public int DiplomaticSkill { get; set; } = 1;
        public int EconomicSkill { get; set; } = 1;
        public int IntrigueSkill { get; set; } = 1;
    }
}
