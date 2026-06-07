using System;
using System.IO;
using System.Text.Json;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Data
{
    public enum LoadGameStatus
    {
        NoSaveFound,
        Loaded,
        Failed
    }

    public enum SaveGameStatus
    {
        Saved,
        Failed
    }

    public sealed class LoadGameResult
    {
        public LoadGameStatus Status { get; set; }
        public GameState? State { get; set; }
        public string Message { get; set; } = "";
        public string FilePath { get; set; } = "";
        public bool LoadedFromLegacyPath { get; set; }
        public bool Success => Status == LoadGameStatus.Loaded;
    }

    public sealed class SaveGameResult
    {
        public SaveGameStatus Status { get; set; }
        public string Message { get; set; } = "";
        public string FilePath { get; set; } = "";
        public bool Success => Status == SaveGameStatus.Saved;
    }

    public static class SaveManager
    {
        private static readonly string SaveFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhispersOfTheThrone", "savegame.json");
        private static readonly string LegacySaveFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KingdomBlind", "savegame.json");

        public static SaveGameResult SaveGame(GameState state)
        {
            try
            {
                string dir = Path.GetDirectoryName(SaveFile) ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                state.ReconcileOldSaves();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(state, options);
                File.WriteAllText(SaveFile, json);

                return new SaveGameResult
                {
                    Status = SaveGameStatus.Saved,
                    FilePath = SaveFile,
                    Message = "تم حفظ اللعبة بنجاح."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Save error: " + ex.Message);
                return new SaveGameResult
                {
                    Status = SaveGameStatus.Failed,
                    FilePath = SaveFile,
                    Message = "فشل حفظ اللعبة: " + ex.Message
                };
            }
        }

        public static LoadGameResult LoadGame()
        {
            if (File.Exists(SaveFile))
                return TryLoadFromPath(SaveFile, false);

            if (File.Exists(LegacySaveFile))
                return TryLoadFromPath(LegacySaveFile, true);

            return new LoadGameResult
            {
                Status = LoadGameStatus.NoSaveFound,
                Message = "لا يوجد ملف حفظ سابق.",
                FilePath = SaveFile
            };
        }

        private static LoadGameResult TryLoadFromPath(string path, bool legacy)
        {
            try
            {
                string json = File.ReadAllText(path);
                var state = JsonSerializer.Deserialize<GameState>(json);
                if (state == null)
                {
                    return new LoadGameResult
                    {
                        Status = LoadGameStatus.Failed,
                        FilePath = path,
                        LoadedFromLegacyPath = legacy,
                        Message = "فشل تحميل اللعبة: ملف الحفظ فارغ أو غير صالح."
                    };
                }

                state.ReconcileOldSaves();
                return new LoadGameResult
                {
                    Status = LoadGameStatus.Loaded,
                    State = state,
                    FilePath = path,
                    LoadedFromLegacyPath = legacy,
                    Message = legacy ? "تم تحميل حفظ قديم من KingdomBlind." : "تم تحميل اللعبة بنجاح."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Load error: " + ex.Message);
                return new LoadGameResult
                {
                    Status = LoadGameStatus.Failed,
                    FilePath = path,
                    LoadedFromLegacyPath = legacy,
                    Message = "فشل تحميل اللعبة: " + ex.Message
                };
            }
        }
    }
}
