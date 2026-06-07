using System;
using System.Collections.Generic;

namespace KingdomBlind_CSharp.Models
{
    public class Army
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string CommanderName { get; set; }
        public string CurrentProvince { get; set; }
        public string DestinationProvince { get; set; }
        public int TotalSoldiers { get; set; }
        public int Morale { get; set; } = 100;
        public int Supply { get; set; } = 100;
        public string CurrentOrder { get; set; } = "Idle"; // Idle, Move, Defend, Siege, Retreat
        public int DaysToDestination { get; set; }
    }

    public class ReinforcementOrder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourceProvince { get; set; }
        public string TargetProvince { get; set; }
        public string TargetArmyId { get; set; }
        public int Soldiers { get; set; }
        public int Food { get; set; }
        public int DaysRemaining { get; set; }
    }
    
    public class LocalBuilding
    {
        public string BuildingType { get; set; }
        public int Level { get; set; } = 1;
    }
}
