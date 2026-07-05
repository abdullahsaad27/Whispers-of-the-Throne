using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KingdomBlind_CSharp.Models;

namespace KingdomBlind_CSharp.Systems
{
    public interface IAiDialogueProvider
    {
        AiDialogueResponse GenerateDialogue(GameState state, AiDialogueRequest request);
    }

    public interface IAiDecisionProvider
    {
        AiDecisionResponse EvaluateDecision(GameState state, AiDecisionRequest request);
    }

    public sealed class FallbackAiProvider : IAiDialogueProvider, IAiDecisionProvider
    {
        private readonly AiProviderSettings settings;

        public FallbackAiProvider(AiProviderSettings settings)
        {
            this.settings = settings ?? new AiProviderSettings();
        }

        public AiDialogueResponse GenerateDialogue(GameState state, AiDialogueRequest request)
        {
            if (CanTryOnline())
            {
                var online = TryGenerateOnlineDialogue(state, request);
                if (!string.IsNullOrWhiteSpace(online))
                {
                    return new AiDialogueResponse
                    {
                        Text = online,
                        UsedFallback = false,
                        ProviderName = settings.ProviderType.ToString()
                    };
                }
            }

            string ruler = string.IsNullOrWhiteSpace(request.RulerName) ? state.RulerName : request.RulerName;
            string context = request.Context?.ToLowerInvariant() ?? "";
            string memoryHint = request.PoliticalMemories.Count > 0
                ? " أتذكر أيضاً: " + request.PoliticalMemories.Last()
                : "";

            string text;
            if (settings.ProviderType == AiProviderType.Disabled)
            {
                text = GetStaticLine(request.CharacterRole, ruler, context);
            }
            else
            {
                text = "الحوار الذكي غير متاح حالياً. تم استخدام تقرير محلي مختصر بنظام fallback آمن. " +
                       GetStaticLine(request.CharacterRole, ruler, context);
            }

            return new AiDialogueResponse
            {
                Text = text + memoryHint,
                UsedFallback = true,
                ProviderName = settings.ProviderType.ToString()
            };
        }

        public AiDecisionResponse EvaluateDecision(GameState state, AiDecisionRequest request)
        {
            if (CanTryOnline())
            {
                var online = TryGenerateOnlineDecision(state, request);
                if (!string.IsNullOrWhiteSpace(online))
                {
                    return new AiDecisionResponse
                    {
                        DecisionKey = NormalizeDecisionKey(online),
                        Explanation = $"{request.ActorName} اختار: {NormalizeDecisionKey(online)} عبر {settings.ProviderType}.",
                        UsedFallback = false
                    };
                }
            }

            int fear = request.Factors.TryGetValue("Fear", out var f) ? f : 0;
            int ambition = request.Factors.TryGetValue("Ambition", out var a) ? a : 0;
            int weakness = request.Factors.TryGetValue("PlayerWeakness", out var w) ? w : 0;

            string decision = ambition + weakness > fear + 80 ? "Escalate" : "Wait";
            string explanation = decision == "Escalate"
                ? $"{request.ActorName} يميل للتصعيد لأن الطموح أو تقدير ضعفك أعلى من الخوف."
                : $"{request.ActorName} يفضّل الانتظار لأن الخوف أو كلفة القرار ما زالت أعلى من فائدته.";

            return new AiDecisionResponse
            {
                DecisionKey = decision,
                Explanation = explanation,
                UsedFallback = true
            };
        }

        private static string GetStaticLine(string role, string ruler, string context)
        {
            if (context.Contains("حرب") || context.Contains("war") || context.Contains("جيش"))
            {
                if (role.Contains("جواسيس") || role.Contains("استخ"))
                    return $"مولاي {ruler}، الحرب تبدأ قبل السيوف؛ أمهلني عيوناً على الطريق حتى لا نضرب ظلاً ونترك الخطر الحقيقي.";
                if (role.Contains("عسكري") || role.Contains("قائد"))
                    return $"مولاي {ruler}، إن أردت الحرب فلتسبقها المؤونة والحشد وحامية ثابتة في الظهر، وإلا صار النصر باباً لفوضى أطول.";
                if (role.Contains("دبلو"))
                    return $"مولاي {ruler}، قبل إعلان الحرب لنختبر باب الوعيد والضمانات؛ أحياناً تخاف الدولة إذا سمعت السيف قبل أن تراه.";
                if (role.Contains("دين") || role.Contains("قاض"))
                    return $"مولاي {ruler}، لا يثبت السيف بلا حجة عادلة؛ امنح الرعية سبباً يفهمونه قبل أن تطلب أبناءهم للجبهة.";
                if (role.Contains("زوج") || role.Contains("ملكة"))
                    return $"يا {ruler}، الحرب تدخل القصر كما تدخل الحدود؛ إن طال الغياب تنازع الورثة والنساء على الخبر قبل القرار.";
                if (role.Contains("دولة") || role.Contains("حاكم"))
                    return $"يا {ruler}، لا أسمع طبولك كوزير في مجلسك بل كحاكم على حدوده؛ إن أردت السلام فاجعل منفعة بقائي أوضح من خوف الحرب.";
                return $"مولاي {ruler}، ميزان القوة لا تحسمه الشجاعة وحدها؛ راقب المؤونة والولاء قبل طبول الحرب.";
            }

            if (context.Contains("مال") || context.Contains("خزان") || context.Contains("tax"))
            {
                if (role.Contains("جواسيس") || role.Contains("استخ"))
                    return $"مولاي {ruler}، إذا ضاقت الخزينة اتسعت الرشاوى؛ راقب عمال الجباية قبل أن يصير نقص المال باب خيانة.";
                if (role.Contains("عسكري") || role.Contains("قائد"))
                    return $"مولاي {ruler}، الجيش يأكل قبل أن يقاتل؛ أصلح الدخل لكن لا تقطع أرزاق الجند دفعة واحدة.";
                if (role.Contains("دبلو"))
                    return $"مولاي {ruler}، التجارة مع جار آمن أرخص من غنيمة عابرة؛ افتح سوقاً يحفظ وجه الدولة ولا يرهق الرعية.";
                if (role.Contains("تجار"))
                    return $"مولاي {ruler}، أمّن الطرق وخفف خوف القوافل، وستأتيك الخزينة من السوق قبل أن تطلبها بالسوط.";
                if (role.Contains("زوج") || role.Contains("ملكة"))
                    return $"يا {ruler}، المال إذا قسا على الناس عاد همساً في القصر؛ ابدأ بما يزيد الدخل ولا يهين البيوت.";
                return $"مولاي {ruler}، المال الهادئ أطول عمراً من الجباية القاسية؛ راقب رضا المقاطعات قبل رفع الضرائب.";
            }

            if (role.Contains("زوج") || role.Contains("ملكة"))
                return $"يا {ruler}، القصر لا يقل خطراً عن الحدود؛ الكلمة في البلاط قد تسبق السيف.";

            return $"مولاي {ruler}، القرار الحكيم يبدأ بسؤال: من سيكسب من هذا، ومن سيتذكره ضدنا؟";
        }

        private bool CanTryOnline()
        {
            if (settings.ProviderType == AiProviderType.Disabled || settings.ProviderType == AiProviderType.Local)
                return false;

            if (settings.ProviderType == AiProviderType.Ollama)
                return true;

            return settings.AllowOnlineRequests;
        }

        private string TryGenerateOnlineDialogue(GameState state, AiDialogueRequest request)
        {
            string prompt =
                "أنت تكتب حواراً قصيراً داخل لعبة استراتيجية تاريخية سنة 1071م عن الخلافة العباسية. " +
                "لا تغير أرقام اللعبة ولا تختر قراراً نهائياً. اكتب جملة أو جملتين فقط بصوت الشخصية.\n" +
                $"الشخصية: {request.CharacterName}\nالدور: {request.CharacterRole}\nالسياق: {request.Context}\nالخليفة: {request.RulerName}\n" +
                $"ذكريات سياسية: {string.Join(" | ", request.PoliticalMemories ?? new System.Collections.Generic.List<string>())}";

            return TryCompleteText(prompt);
        }

        private string TryGenerateOnlineDecision(GameState state, AiDecisionRequest request)
        {
            string factors = string.Join(", ", request.Factors.Select(kv => $"{kv.Key}={kv.Value}"));
            string prompt =
                "أنت ممثل قرار لشخصية داخل لعبة استراتيجية. اختر مفتاحاً واحداً فقط من هذه المفاتيح: Wait, Escalate, Negotiate, Support, Undermine.\n" +
                "إذا كان النموذج غير متأكد فليجب Wait فقط.\n" +
                $"الشخصية: {request.ActorName}\nالنوع: {request.ActorType}\nالسياق: {request.DecisionContext}\nالعوامل: {factors}";

            return TryCompleteText(prompt);
        }

        private string TryCompleteText(string prompt)
        {
            try
            {
                string model = string.IsNullOrWhiteSpace(settings.Model) ? GetDefaultModel(settings.ProviderType) : settings.Model;
                string endpoint = string.IsNullOrWhiteSpace(settings.Endpoint)
                    ? AiModelCatalogService.GetDefaultEndpoint(settings.ProviderType)
                    : settings.Endpoint.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(endpoint))
                    return "";

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 60)) };
                string apiKey = GetApiKey();
                if (RequiresBearer(settings.ProviderType) && !string.IsNullOrWhiteSpace(apiKey))
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                if (settings.ProviderType == AiProviderType.Gemini)
                    return CompleteGemini(http, endpoint, model, prompt, apiKey);
                if (settings.ProviderType == AiProviderType.Ollama || settings.ProviderType == AiProviderType.OllamaCloud)
                    return CompleteOllama(http, endpoint, model, prompt);

                return CompleteOpenAiCompatible(http, endpoint, model, prompt);
            }
            catch
            {
                return "";
            }
        }

        private static string CompleteOpenAiCompatible(HttpClient http, string endpoint, string model, string prompt)
        {
            string url = endpoint.EndsWith("/chat/completions") ? endpoint : endpoint + "/chat/completions";
            var body = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = "Respond concisely in Arabic unless asked for a decision key." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.6
            };
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = http.PostAsync(url, content).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return "";
            string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }

        private static string CompleteOllama(HttpClient http, string endpoint, string model, string prompt)
        {
            string url = endpoint.EndsWith("/api/chat") ? endpoint : endpoint + "/api/chat";
            var body = new
            {
                model,
                stream = false,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = http.PostAsync(url, content).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return "";
            string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }

        private static string CompleteGemini(HttpClient http, string endpoint, string model, string prompt, string apiKey)
        {
            string modelName = model.StartsWith("models/") ? model : "models/" + model;
            string url = endpoint.TrimEnd('/') + "/" + modelName + ":generateContent";
            if (!string.IsNullOrWhiteSpace(apiKey))
                url += "?key=" + Uri.EscapeDataString(apiKey);

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = http.PostAsync(url, content).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return "";
            string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "";
        }

        private static bool RequiresBearer(AiProviderType providerType)
        {
            return providerType is AiProviderType.OpenRouter or AiProviderType.Mistral or AiProviderType.Grok or AiProviderType.OpenAICompatible or AiProviderType.CustomHttp or AiProviderType.OllamaCloud;
        }

        private string GetApiKey()
        {
            return string.IsNullOrWhiteSpace(settings.ApiKeyEnvironmentVariable)
                ? ""
                : Environment.GetEnvironmentVariable(settings.ApiKeyEnvironmentVariable) ?? "";
        }

        private static string GetDefaultModel(AiProviderType providerType)
        {
            return AiModelCatalogService.GetDefaultModels(providerType).FirstOrDefault()?.Id ?? "";
        }

        private static string NormalizeDecisionKey(string text)
        {
            string normalized = (text ?? "").Trim();
            foreach (var key in new[] { "Escalate", "Negotiate", "Support", "Undermine", "Wait" })
                if (normalized.Contains(key, StringComparison.OrdinalIgnoreCase))
                    return key;
            return "Wait";
        }
    }

    public static class AiProviderFactory
    {
        public static FallbackAiProvider Create(AiProviderSettings settings)
        {
            return new FallbackAiProvider(settings);
        }
    }
}
