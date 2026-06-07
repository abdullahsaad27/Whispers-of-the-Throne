using System.Collections.Generic;

namespace KingdomBlind_CSharp.Models
{
    public class GameActionResult
    {
        public bool Success { get; set; }
        public string Title { get; set; }
        public string MainMessage { get; set; }
        public Dictionary<string, int> ResourceChanges { get; set; } = new Dictionary<string, int>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string SoundEffectKey { get; set; }
        public bool ShouldNarrate { get; set; } = true;
        public bool ShouldPauseTime { get; set; } = false;
        public bool ShowAnnexationMenu { get; set; } = false;
        public int AnnexedNeighborIdx { get; set; } = -1;

        public override string ToString()
        {
            return MainMessage;
        }
    }
}
