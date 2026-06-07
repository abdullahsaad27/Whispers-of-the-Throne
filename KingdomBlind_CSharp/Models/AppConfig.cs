using System;
using System.IO;
using System.Text.Json;

namespace KingdomBlind_CSharp.Models
{
    public class AppConfig
    {
        public string SpeechProvider { get; set; } = "sapi5";
        public bool UseSuperTonic { get; set; } = false;
        public double SuperTonicSpeed { get; set; } = 1.0;
        public bool SapiReadsEvents { get; set; } = true;
        public bool SapiReadsNPCs { get; set; } = false;
        public string SapiVoiceName { get; set; } = "";
        public AiProviderSettings AiProvider { get; set; } = new AiProviderSettings();
        public AiActorSettings AiActors { get; set; } = new AiActorSettings();

        public static AppConfig Load()
        {
            string path = @"assets\settings.json";
            if (File.Exists(path))
            {
                try
                {
                    var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path)) ?? new AppConfig();
                    config.AiProvider ??= new AiProviderSettings();
                    config.AiProvider.CharacterModelOverrides ??= new System.Collections.Generic.Dictionary<string, string>();
                    config.AiActors ??= new AiActorSettings();
                    config.AiActors.RoleModelOverrides ??= new System.Collections.Generic.Dictionary<string, string>();
                    if (config.AiActors.MaxAutonomousMonthlyBudget <= 0)
                        config.AiActors.MaxAutonomousMonthlyBudget = 200;
                    return config;
                }
                catch { return new AppConfig(); }
            }
            return new AppConfig();
        }

        public void Save()
        {
            Directory.CreateDirectory("assets");
            File.WriteAllText(@"assets\settings.json", JsonSerializer.Serialize(this));
        }
    }
}
