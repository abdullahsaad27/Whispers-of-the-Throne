using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System;

namespace KingdomBlind_CSharp.Systems
{
    public static class LocalizationSystem
    {
        private static Dictionary<string, string>? cache;

        public static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "";

            EnsureLoaded();
            return cache != null && cache.TryGetValue(key, out var value) ? value : key;
        }

        private static void EnsureLoaded()
        {
            if (cache != null)
                return;

            cache = new Dictionary<string, string>();
            string path = Path.Combine(AppContext.BaseDirectory, "assets", "Story_ar.json");
            if (!File.Exists(path))
                path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "assets", "Story_ar.json"));

            if (!File.Exists(path))
                return;

            try
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                if (loaded != null)
                    cache = loaded;
            }
            catch
            {
                cache = new Dictionary<string, string>();
            }
        }
    }
}
