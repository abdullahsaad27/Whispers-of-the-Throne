using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public static class AiModelCatalogService
    {
        public static async Task<(bool Success, string Message, List<AiModelDescriptor> Models)> FetchModelsAsync(AiProviderSettings settings)
        {
            settings ??= new AiProviderSettings();
            var models = new List<AiModelDescriptor>();

            if (!settings.AllowOnlineRequests && settings.ProviderType != AiProviderType.Ollama)
            {
                models.AddRange(GetDefaultModels(settings.ProviderType));
                return (false, "طلبات الإنترنت غير مفعلة. عرضت نماذج افتراضية فقط.", models);
            }

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 60)) };
                string apiKey = GetApiKey(settings);
                string url = GetModelsUrl(settings);

                if (string.IsNullOrWhiteSpace(url))
                {
                    models.AddRange(GetDefaultModels(settings.ProviderType));
                    return (false, "لا يوجد endpoint مناسب لهذا المزود. عرضت نماذج افتراضية.", models);
                }

                if (RequiresBearer(settings.ProviderType) && !string.IsNullOrWhiteSpace(apiKey))
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                string json = await http.GetStringAsync(url);
                models = ParseModels(settings.ProviderType, json);
                if (models.Count == 0)
                {
                    models.AddRange(GetDefaultModels(settings.ProviderType));
                    return (false, "وصل الرد لكن لم أجد أسماء نماذج واضحة. عرضت نماذج افتراضية.", models);
                }

                return (true, $"تم جلب {models.Count} نموذج من {settings.ProviderType}.", models);
            }
            catch (Exception ex)
            {
                models.AddRange(GetDefaultModels(settings.ProviderType));
                return (false, $"تعذر جلب النماذج: {ex.Message}. عرضت نماذج افتراضية.", models);
            }
        }

        public static List<AiModelDescriptor> GetDefaultModels(AiProviderType providerType)
        {
            string[] ids = providerType switch
            {
                AiProviderType.Gemini => new[] { "models/gemini-2.5-flash", "models/gemini-2.5-pro", "models/gemini-2.0-flash" },
                AiProviderType.OpenRouter => new[] { "openrouter/auto", "google/gemini-2.5-flash", "mistralai/mistral-large" },
                AiProviderType.Mistral => new[] { "mistral-large-latest", "mistral-small-latest", "ministral-8b-latest" },
                AiProviderType.Ollama => new[] { "llama3.1", "qwen2.5", "gemma3" },
                AiProviderType.OllamaCloud => new[] { "gpt-oss:20b", "llama3.1", "qwen2.5" },
                AiProviderType.OpenAICompatible => new[] { "gpt-4.1-mini", "gpt-4.1", "o4-mini" },
                AiProviderType.Grok => new[] { "grok-latest", "grok-3", "grok-3-mini" },
                AiProviderType.CustomHttp => new[] { "custom-default" },
                _ => Array.Empty<string>()
            };

            return ids.Select(id => new AiModelDescriptor
            {
                Id = id,
                DisplayName = id,
                Provider = providerType.ToString(),
                IsLocal = providerType == AiProviderType.Ollama,
                Source = "Default"
            }).ToList();
        }

        public static string GetDefaultEndpoint(AiProviderType providerType)
        {
            return providerType switch
            {
                AiProviderType.Ollama => "http://localhost:11434",
                AiProviderType.OllamaCloud => "",
                AiProviderType.OpenAICompatible => "https://api.openai.com/v1",
                AiProviderType.OpenRouter => "https://openrouter.ai/api/v1",
                AiProviderType.Mistral => "https://api.mistral.ai/v1",
                AiProviderType.Gemini => "https://generativelanguage.googleapis.com/v1beta",
                AiProviderType.Grok => "https://api.x.ai/v1",
                _ => ""
            };
        }

        private static string GetModelsUrl(AiProviderSettings settings)
        {
            string endpoint = string.IsNullOrWhiteSpace(settings.Endpoint)
                ? GetDefaultEndpoint(settings.ProviderType)
                : settings.Endpoint.TrimEnd('/');

            return settings.ProviderType switch
            {
                AiProviderType.Gemini => BuildGeminiModelsUrl(endpoint, GetApiKey(settings)),
                AiProviderType.Ollama or AiProviderType.OllamaCloud => endpoint.EndsWith("/api/tags") ? endpoint : endpoint + "/api/tags",
                AiProviderType.OpenRouter or AiProviderType.Mistral or AiProviderType.Grok or AiProviderType.OpenAICompatible or AiProviderType.CustomHttp
                    => endpoint.EndsWith("/models") ? endpoint : endpoint + "/models",
                _ => ""
            };
        }

        private static string BuildGeminiModelsUrl(string endpoint, string apiKey)
        {
            string url = endpoint.EndsWith("/models") ? endpoint : endpoint.TrimEnd('/') + "/models";
            return string.IsNullOrWhiteSpace(apiKey) ? url : $"{url}?key={Uri.EscapeDataString(apiKey)}";
        }

        private static bool RequiresBearer(AiProviderType providerType)
        {
            return providerType is AiProviderType.OpenRouter or AiProviderType.Mistral or AiProviderType.Grok or AiProviderType.OpenAICompatible or AiProviderType.CustomHttp or AiProviderType.OllamaCloud;
        }

        private static string GetApiKey(AiProviderSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKeyEnvironmentVariable))
                return "";

            return Environment.GetEnvironmentVariable(settings.ApiKeyEnvironmentVariable) ?? "";
        }

        private static List<AiModelDescriptor> ParseModels(AiProviderType providerType, string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var models = new List<AiModelDescriptor>();

            if (providerType == AiProviderType.Ollama || providerType == AiProviderType.OllamaCloud)
            {
                if (root.TryGetProperty("models", out var array))
                    foreach (var item in array.EnumerateArray())
                    {
                        string id = GetString(item, "name");
                        if (string.IsNullOrWhiteSpace(id)) id = GetString(item, "model");
                        AddModel(models, providerType, id, id, providerType == AiProviderType.Ollama, "Ollama");
                    }
                return models;
            }

            if (providerType == AiProviderType.Gemini)
            {
                if (root.TryGetProperty("models", out var array))
                    foreach (var item in array.EnumerateArray())
                    {
                        string id = GetString(item, "name");
                        string display = GetString(item, "displayName");
                        AddModel(models, providerType, id, string.IsNullOrWhiteSpace(display) ? id : display, false, "Gemini");
                    }
                return models;
            }

            if (root.TryGetProperty("data", out var dataArray))
            {
                foreach (var item in dataArray.EnumerateArray())
                {
                    string id = GetString(item, "id");
                    string display = GetString(item, "name");
                    AddModel(models, providerType, id, string.IsNullOrWhiteSpace(display) ? id : display, false, providerType.ToString());
                }
            }
            else if (root.TryGetProperty("models", out var modelsArray))
            {
                foreach (var item in modelsArray.EnumerateArray())
                {
                    string id = GetString(item, "id");
                    if (string.IsNullOrWhiteSpace(id)) id = GetString(item, "name");
                    AddModel(models, providerType, id, id, false, providerType.ToString());
                }
            }

            return models;
        }

        private static void AddModel(List<AiModelDescriptor> models, AiProviderType providerType, string id, string displayName, bool isLocal, string source)
        {
            if (string.IsNullOrWhiteSpace(id) || models.Any(m => m.Id == id))
                return;

            models.Add(new AiModelDescriptor
            {
                Id = id,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName,
                Provider = providerType.ToString(),
                IsLocal = isLocal,
                Source = source
            });
        }

        private static string GetString(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
        }
    }
}
