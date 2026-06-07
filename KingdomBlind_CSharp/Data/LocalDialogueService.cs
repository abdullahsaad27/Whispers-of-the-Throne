using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KingdomBlind_CSharp.Data
{
    public class LocalDialogue
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("roleMatch")]
        public List<string> RoleMatch { get; set; }

        [JsonPropertyName("contextMatch")]
        public List<string> ContextMatch { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    public class LocalDialogueContainer
    {
        [JsonPropertyName("dialogues")]
        public List<LocalDialogue> Dialogues { get; set; }
    }

    public class LocalDialogueService
    {
        private readonly List<LocalDialogue> dialogues = new List<LocalDialogue>();
        private readonly string defaultLine = "مولاي {ruler}، القرار الحكيم يبدأ بسؤال: من سيكسب من هذا، ومن سيتذكره ضدنا؟";

        public LocalDialogueService()
        {
            try
            {
                string filePath = Path.Combine(AppContext.BaseDirectory, "Data", "LocalDialogue.json");
                if (!File.Exists(filePath))
                {
                    filePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "LocalDialogue.json");
                }

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var container = JsonSerializer.Deserialize<LocalDialogueContainer>(json);
                    if (container?.Dialogues != null)
                    {
                        dialogues = container.Dialogues;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading LocalDialogue.json: " + ex.Message);
            }
        }

        public string GetLine(string role, string context, string ruler)
        {
            role = role ?? "";
            context = context ?? "";

            foreach (var dialogue in dialogues)
            {
                bool contextMatches = dialogue.ContextMatch == null || 
                                      dialogue.ContextMatch.Count == 0 || 
                                      dialogue.ContextMatch.Any(c => context.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0);
                                      
                bool roleMatches = dialogue.RoleMatch == null || 
                                   dialogue.RoleMatch.Count == 0 || 
                                   dialogue.RoleMatch.Any(r => role.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0);

                if (contextMatches && roleMatches)
                {
                    return dialogue.Text.Replace("{ruler}", ruler);
                }
            }

            return defaultLine.Replace("{ruler}", ruler);
        }
    }
}
