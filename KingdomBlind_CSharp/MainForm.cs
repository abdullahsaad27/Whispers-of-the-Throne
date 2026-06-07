using System;
using System.Drawing;
using System.Windows.Forms;
using KingdomBlind_CSharp.Models;
using KingdomBlind_CSharp.Systems;
using KingdomBlind_CSharp.Audio;
using KingdomBlind_CSharp.Data;
using KingdomBlind_CSharp.UI;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace KingdomBlind_CSharp
{
    public class MainForm : Form
    {
        private GameState state;
        private AppConfig config;
        private SapiEngine sapi;
        private EnemySystem enemySystem;
        private Character vizier;
        private Character queen;
        private AudioManager audio;
        private INarrationService narration;
        private ScreenManager screenManager;
        private GameEventBus eventBus;
        
        private Label statusLabel;
        private TextBox txtDisplay;
        private MenuStrip menuStrip;
        private FlowLayoutPanel dynamicPanel;
        private System.Windows.Forms.Timer timeTimer;
        
        private bool IsSuperTonicSpeaking = false;
        private DateTime suppressFocusSpeechUntil = DateTime.MinValue;

        public MainForm()
        {
            sapi = new SapiEngine();
            enemySystem = new EnemySystem();
            vizier = new Character("Vizier");
            queen = new Character("Queen");
            audio = new AudioManager();
            narration = new NarrationService(sapi);
            screenManager = new ScreenManager();
            eventBus = new GameEventBus();
            
            InitializeBaseUI();
            
            // Initialize a default state for the menu to hold settings
            state = new GameState();
            config = AppConfig.Load();
            ApplyConfigToState();
            
            LoadMainMenu();
        }

        private void ApplyConfigToState()
        {
            state.SpeechProvider = config.SpeechProvider;
            state.UseSuperTonic = config.UseSuperTonic;
            state.SuperTonicSpeed = config.SuperTonicSpeed;
            state.SapiReadsEvents = config.SapiReadsEvents;
            state.SapiReadsNPCs = config.SapiReadsNPCs;
            state.SapiVoiceName = config.SapiVoiceName;
            config.AiProvider ??= new AiProviderSettings();
            config.AiActors ??= new AiActorSettings();
            state.UseSuperTonic = config.AiActors.UseSuperTonicForAiDialogue || config.UseSuperTonic;
            state.ReconcileOldSaves();
            state.DelegatedAuthoritySettings.AllowAutonomousActions = config.AiActors.AllowAutonomousActions;
            state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget = Math.Max(1, config.AiActors.MaxAutonomousMonthlyBudget);
            
            sapi.IsEnabled = state.SpeechProvider == "sapi5";
            if (!string.IsNullOrEmpty(state.SapiVoiceName)) sapi.SetVoice(state.SapiVoiceName);
        }

        private void SaveConfig()
        {
            config.SpeechProvider = state.SpeechProvider;
            config.UseSuperTonic = state.UseSuperTonic;
            config.SuperTonicSpeed = state.SuperTonicSpeed;
            config.SapiReadsEvents = state.SapiReadsEvents;
            config.SapiReadsNPCs = state.SapiReadsNPCs;
            config.SapiVoiceName = state.SapiVoiceName;
            config.AiActors ??= new AiActorSettings();
            state.ReconcileOldSaves();
            config.AiActors.AllowAutonomousActions = state.DelegatedAuthoritySettings.AllowAutonomousActions;
            config.AiActors.MaxAutonomousMonthlyBudget = Math.Max(1, state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget);
            config.UseSuperTonic = config.AiActors.UseSuperTonicForAiDialogue;
            config.Save();
        }

        private void InitializeBaseUI()
        {
            this.Text = "Whispers of the Throne - النسخة C#";
            this.Size = new Size(800, 700);
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            menuStrip = new MenuStrip();
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("ملف");
            ToolStripMenuItem saveItem = new ToolStripMenuItem("حفظ اللعبة (Ctrl+S)", null, (s, e) => SaveGame());
            ToolStripMenuItem loadItem = new ToolStripMenuItem("تحميل اللعبة (Ctrl+L)", null, (s, e) => LoadGame());
            fileMenu.DropDownItems.Add(saveItem);
            fileMenu.DropDownItems.Add(loadItem);
            menuStrip.Items.Add(fileMenu);
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            statusLabel = new Label();
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(20, 30);
            statusLabel.Font = new Font("Arial", 12, FontStyle.Bold);
            this.Controls.Add(statusLabel);

            txtDisplay = new TextBox();
            txtDisplay.Location = new Point(20, 60);
            txtDisplay.Size = new Size(740, 250);
            txtDisplay.Font = new Font("Arial", 12);
            txtDisplay.Multiline = true;
            txtDisplay.ReadOnly = true;
            txtDisplay.TabStop = true;
            txtDisplay.ScrollBars = ScrollBars.Vertical;
            txtDisplay.AccessibleName = "نص التقرير والأحداث";
            txtDisplay.AccessibleRole = AccessibleRole.Text;
            this.Controls.Add(txtDisplay);

            dynamicPanel = new FlowLayoutPanel();
            dynamicPanel.Location = new Point(20, 330);
            dynamicPanel.Size = new Size(740, 300);
            dynamicPanel.AutoScroll = true;
            dynamicPanel.AccessibleName = "أوامر الشاشة الحالية";
            this.Controls.Add(dynamicPanel);

            timeTimer = new System.Windows.Forms.Timer();
            timeTimer.Interval = 5000;
            timeTimer.Tick += TimeTimer_Tick;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timeTimer?.Stop();
            narration?.Stop();
            audio?.Dispose();
            sapi?.Dispose();
            base.OnFormClosed(e);
        }

        private void TimeTimer_Tick(object sender, EventArgs e)
        {
            if (state == null || state.Time == null || state.Time.IsPaused) return;
            
            var res = CalendarTimeSystem.AdvanceDay(state);
            if (res.ShouldPauseTime)
            {
                state.Time.IsPaused = true;
                if (audio != null && !string.IsNullOrEmpty(res.SoundEffectKey)) audio.Play(res.SoundEffectKey.Replace(".wav", ""));
                SetNarrativeText("تنبيه هام!\n" + res.MainMessage);
                
                if (res.MainMessage.Contains("نهاية السلالة"))
                {
                    ClearDynamicPanel();
                    AddActionButton("نهاية اللعبة - العودة للقائمة الرئيسية", (s, ev) => LoadMainMenu());
                    return;
                }
                
                RenderSandboxButtons();
            }
            else if (res.ShouldNarrate)
            {
                SetNarrativeText(res.MainMessage);
                if (state.GameMode == "sandbox") UpdateUI();
            }
            else
            {
                if (state.GameMode == "sandbox") UpdateUI();
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (state != null && state.GameMode.StartsWith("sandbox"))
            {
                if (e.Control && e.KeyCode == Keys.S) SaveGame();
                if (e.Control && e.KeyCode == Keys.L) LoadGame();
                if (e.Alt && e.KeyCode == Keys.D1) { ShowGovernanceHub(null, null); e.Handled = true; }
                if (e.Alt && e.KeyCode == Keys.D2) { ShowCourtHub(null, null); e.Handled = true; }
                if (e.Alt && e.KeyCode == Keys.D3) { ShowEconomyHub(null, null); e.Handled = true; }
                if (e.Alt && e.KeyCode == Keys.D4) { ShowWarDiplomacyHub(null, null); e.Handled = true; }
            }
        }

        private void SpeakToActiveReader(string text, bool isNpcDialog = false)
        {
            if (state == null || IsSuperTonicSpeaking) return;
            narration.Speak(state, text, isNpcDialog);
        }

        private void SetNarrativeText(string msg, bool speak = true, bool isNpcDialog = false)
        {
            if (string.IsNullOrWhiteSpace(txtDisplay.Text))
                txtDisplay.Text = msg;
            else
                txtDisplay.AppendText(Environment.NewLine + "---" + Environment.NewLine + msg);

            if (speak)
            {
                suppressFocusSpeechUntil = DateTime.Now.AddMilliseconds(1200);
                SpeakToActiveReader(msg, isNpcDialog);
            }
        }

        private void ClearDynamicPanel()
        {
            foreach (Control control in dynamicPanel.Controls)
                control.Dispose();

            dynamicPanel.Controls.Clear();
        }

        private void SetScreenTitle(string title)
        {
            statusLabel.Text = title;
            this.Text = $"Whispers of the Throne - {title}";
            dynamicPanel.AccessibleName = "أوامر " + title;
        }

        private static string ToAccessibleText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var sb = new StringBuilder();
            foreach (char ch in text)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (char.IsSurrogate(ch) ||
                    category == UnicodeCategory.OtherSymbol ||
                    category == UnicodeCategory.ModifierSymbol ||
                    ch == '\ufe0f')
                {
                    continue;
                }

                sb.Append(ch);
            }

            return sb.ToString().Replace("  ", " ").Trim();
        }

        private void AddActionButton(string text, EventHandler onClick, Color? backColor = null, Color? foreColor = null)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.AccessibleName = ToAccessibleText(text);
            btn.AccessibleRole = AccessibleRole.PushButton;
            btn.Size = new Size(350, 40);
            btn.Font = new Font("Arial", 10, FontStyle.Bold);
            if (backColor.HasValue) btn.BackColor = backColor.Value;
            if (foreColor.HasValue) btn.ForeColor = foreColor.Value;
            
            btn.Click += onClick;
            btn.GotFocus += (s, e) => {
                if (audio != null) audio.PlayTick();
                if (DateTime.Now < suppressFocusSpeechUntil) return;
                if (state != null && state.SpeechProvider == "sapi5" && !IsSuperTonicSpeaking)
                    sapi.Speak(btn.Text);
            };
            
            dynamicPanel.Controls.Add(btn);
        }

        // --- UI STATES ---

        private void LoadMainMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "menu";
            
            SetScreenTitle("القائمة الرئيسية");
            txtDisplay.Clear();
            SetNarrativeText("مرحباً بك في Whispers of the Throne. احكم عبر الهمسات والمكائد، واستمع لما لا يُقال.", false);
            
            AddActionButton("بدء لعبة جديدة", (s, e) => StartCharacterCreation());
            AddActionButton("استكمال اللعبة المحفوظة", (s, e) => LoadGame());
            AddActionButton("الإعدادات ⚙️", (s, e) => OpenSettings());
            AddActionButton("الخروج من اللعبة", (s, e) => this.Close());
            
            if (dynamicPanel.Controls.Count > 0)
                dynamicPanel.Controls[0].Focus();
        }

        // --- SETTINGS MODULE ---
        private void OpenSettings()
        {
            ClearDynamicPanel();
            state.GameMode = "settings";
            SetScreenTitle("الإعدادات العامة");
            txtDisplay.Clear();
            
            string providerName = state.SpeechProvider == "sapi5" ? "SAPI 5 (محرك الويندوز)" : "NVDA (قارئ الشاشة الخارجي)";
            SetNarrativeText($"الإعدادات الحالية:\nالناطق الفعال: {providerName}", false);
            
            string switchText = state.SpeechProvider == "sapi5" ? "التبديل إلى NVDA" : "التبديل إلى SAPI 5";
            AddActionButton(switchText, (s, e) => {
                state.SpeechProvider = state.SpeechProvider == "sapi5" ? "nvda" : "sapi5";
                sapi.IsEnabled = state.SpeechProvider == "sapi5";
                if (s is Button button)
                {
                    string newText = state.SpeechProvider == "sapi5" ? "التبديل إلى NVDA" : "التبديل إلى SAPI 5";
                    button.Text = newText;
                    button.AccessibleName = ToAccessibleText(newText);
                }
                string newProviderName = state.SpeechProvider == "sapi5" ? "SAPI 5 (محرك الويندوز)" : "NVDA (قارئ الشاشة الخارجي)";
                SetNarrativeText($"تم تغيير الناطق إلى {newProviderName}.");
            });
            
            AddActionButton("إعدادات أصوات SAPI 5 المتقدمة", (s, e) => OpenSapiSettings());
            AddActionButton("مزودات الذكاء الاصطناعي العامة", (s, e) => OpenSuperTonicManager());
            AddActionButton("حفظ هذه الإعدادات كافتراضية دائمًا 💾", (s, e) => { SaveConfig(); SetNarrativeText("تم حفظ الإعدادات! عند إعادة تشغيل اللعبة سيتم تطبيقها تلقائياً."); });
            AddActionButton("العودة للقائمة الرئيسية 🕌", (s, e) => LoadMainMenu());
            
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void OpenSapiSettings()
        {
            ClearDynamicPanel();
            state.GameMode = "settings_sapi";
            statusLabel.Text = "إعدادات SAPI 5";
            
            string eventsStatus = state.SapiReadsEvents ? "مفعل" : "معطل";
            string npcStatus = state.SapiReadsNPCs ? "مفعل" : "معطل";
            SetNarrativeText($"الصوت الحالي: {(string.IsNullOrEmpty(state.SapiVoiceName) ? "الافتراضي" : state.SapiVoiceName)}\nقراءة الأحداث: {eventsStatus}\nقراءة حوارات الشخصيات: {npcStatus}", false);
            
            AddActionButton($"تغيير الصوت (الحالي: {(string.IsNullOrEmpty(state.SapiVoiceName) ? "الافتراضي" : state.SapiVoiceName)})", (s, e) => {
                var voices = sapi.GetAvailableVoices();
                if (voices.Count == 0) { SetNarrativeText("لا يوجد أصوات إضافية.", true); return; }
                int idx = string.IsNullOrEmpty(state.SapiVoiceName) ? 0 : voices.IndexOf(state.SapiVoiceName);
                idx = (idx + 1) % voices.Count;
                state.SapiVoiceName = voices[idx];
                sapi.SetVoice(state.SapiVoiceName);
                OpenSapiSettings();
            });

            AddActionButton($"قراءة الأحداث والتقارير: {eventsStatus}", (s, e) => {
                state.SapiReadsEvents = !state.SapiReadsEvents;
                if (s is Button button)
                {
                    string text = $"قراءة الأحداث والتقارير: {(state.SapiReadsEvents ? "مفعل" : "معطل")}";
                    button.Text = text;
                    button.AccessibleName = ToAccessibleText(text);
                }
                SetNarrativeText(state.SapiReadsEvents ? "تم تفعيل قراءة الأحداث." : "تم تعطيل قراءة الأحداث.");
            });

            AddActionButton($"قراءة حوارات الشخصيات: {npcStatus}", (s, e) => {
                state.SapiReadsNPCs = !state.SapiReadsNPCs;
                if (s is Button button)
                {
                    string text = $"قراءة حوارات الشخصيات: {(state.SapiReadsNPCs ? "مفعل" : "معطل")}";
                    button.Text = text;
                    button.AccessibleName = ToAccessibleText(text);
                }
                SetNarrativeText(state.SapiReadsNPCs ? "تم تفعيل قراءة حوارات الشخصيات." : "تم تعطيل قراءة حوارات الشخصيات.");
            });

            AddActionButton("حفظ هذه الإعدادات كافتراضية دائمًا 💾", (s, e) => { SaveConfig(); SetNarrativeText("تم حفظ الإعدادات بنجاح!"); });
            AddActionButton("عودة 🔙", (s, e) => OpenSettings());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void OpenSuperTonicManager()
        {
            ClearDynamicPanel();
            state.GameMode = "settings_ai";
            SetScreenTitle("مزودات الذكاء الاصطناعي");
            EnsureAiConfig();

            SetNarrativeText(
                "إعداد الذكاء الاصطناعي العام وSuperTonic.\n" +
                $"المزود الحالي: {config.AiProvider.ProviderType}\n" +
                $"endpoint: {(string.IsNullOrWhiteSpace(config.AiProvider.Endpoint) ? "افتراضي حسب المزود" : config.AiProvider.Endpoint)}\n" +
                $"النموذج: {(string.IsNullOrWhiteSpace(config.AiProvider.Model) ? "غير محدد" : config.AiProvider.Model)}\n" +
                $"السماح بالاتصال عبر الإنترنت: {(config.AiProvider.AllowOnlineRequests ? "نعم" : "لا")}\n" +
                $"اسم متغير مفتاح API: {(string.IsNullOrWhiteSpace(config.AiProvider.ApiKeyEnvironmentVariable) ? "غير محدد" : config.AiProvider.ApiKeyEnvironmentVariable)}\n\n" +
                AiRuntimePolicySystem.GetSummary(config.AiActors) + "\n\n" +
                "إذا فشل المزود أو كان النموذج صغيراً، تستخدم اللعبة fallback محلياً آمناً.",
                false);

            foreach (AiProviderType providerType in Enum.GetValues(typeof(AiProviderType)))
            {
                AddActionButton($"اختيار مزود: {providerType}", (s, e) => {
                    config.AiProvider.ProviderType = providerType;
                    config.AiProvider.Endpoint = AiModelCatalogService.GetDefaultEndpoint(providerType);
                    config.AiProvider.ApiKeyEnvironmentVariable = GetDefaultApiKeyEnvironment(providerType);
                    config.AiProvider.Model = AiModelCatalogService.GetDefaultModels(providerType).FirstOrDefault()?.Id ?? "";
                    OpenSuperTonicManager();
                });
            }

            AddActionButton($"السماح بطلبات الإنترنت: {(config.AiProvider.AllowOnlineRequests ? "نعم" : "لا")}", (s, e) => {
                config.AiProvider.AllowOnlineRequests = !config.AiProvider.AllowOnlineRequests;
                OpenSuperTonicManager();
            });

            AddActionButton($"الحوارات الذكية: {(config.AiActors.SmartDialoguesEnabled ? "مفعلة" : "نصوص محلية فقط")}", (s, e) => {
                config.AiActors.SmartDialoguesEnabled = !config.AiActors.SmartDialoguesEnabled;
                OpenSuperTonicManager();
            });

            AddActionButton($"مستوى الإطالة: {GetAiDialogueLengthDisplay(config.AiActors.DialogueLengthLevel)}", (s, e) => {
                config.AiActors.DialogueLengthLevel = config.AiActors.DialogueLengthLevel switch
                {
                    AiDialogueLengthLevel.Brief => AiDialogueLengthLevel.Normal,
                    AiDialogueLengthLevel.Normal => AiDialogueLengthLevel.Detailed,
                    _ => AiDialogueLengthLevel.Brief
                };
                OpenSuperTonicManager();
            });

            AddActionButton($"السماح بالأفعال التلقائية المفوضة: {(config.AiActors.AllowAutonomousActions ? "نعم" : "لا")}", (s, e) => {
                config.AiActors.AllowAutonomousActions = !config.AiActors.AllowAutonomousActions;
                state.DelegatedAuthoritySettings.AllowAutonomousActions = config.AiActors.AllowAutonomousActions;
                OpenSuperTonicManager();
            });

            AddActionButton($"حد ميزانية الأفعال التلقائية: {config.AiActors.MaxAutonomousMonthlyBudget} ذهب", (s, e) => {
                config.AiActors.MaxAutonomousMonthlyBudget = config.AiActors.MaxAutonomousMonthlyBudget switch
                {
                    < 200 => 200,
                    < 500 => 500,
                    < 1000 => 1000,
                    _ => 100
                };
                state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget = config.AiActors.MaxAutonomousMonthlyBudget;
                OpenSuperTonicManager();
            });

            AddActionButton($"SuperTonic لحوارات AI: {(config.AiActors.UseSuperTonicForAiDialogue ? "مفعل" : "معطل")}", (s, e) => {
                config.AiActors.UseSuperTonicForAiDialogue = !config.AiActors.UseSuperTonicForAiDialogue;
                state.UseSuperTonic = config.AiActors.UseSuperTonicForAiDialogue;
                OpenSuperTonicManager();
            });

            AddActionButton("إعداد الشخصيات التي يعمل عليها AI", (s, e) => OpenAiActorSettings());
            AddActionButton("تعديل endpoint للمزود الحالي", (s, e) => OpenAiEndpointEditor());
            AddActionButton("إدخال اسم نموذج يدوياً", (s, e) => OpenAiManualModelEditor());
            AddActionButton("تعيين نموذج افتراضي حسب المزود", (s, e) => {
                config.AiProvider.Model = AiModelCatalogService.GetDefaultModels(config.AiProvider.ProviderType).FirstOrDefault()?.Id ?? "";
                OpenSuperTonicManager();
            });
            AddActionButton("تعيين اسم متغير مفتاح API افتراضي", (s, e) => {
                config.AiProvider.ApiKeyEnvironmentVariable = GetDefaultApiKeyEnvironment(config.AiProvider.ProviderType);
                OpenSuperTonicManager();
            });
            AddActionButton("جلب النماذج المتاحة من المزود", async (s, e) => await FetchAiModelsForCurrentProvider());
            AddActionButton("بروتوكول أوامر النماذج الصغيرة", (s, e) => OpenAiSmallModelProtocol());
            
            AddActionButton("حفظ هذه الإعدادات كافتراضية دائمًا 💾", (s, e) => { SaveConfig(); SetNarrativeText("تم حفظ الإعدادات بنجاح!"); });
            AddActionButton("عودة 🔙", (s, e) => OpenSettings());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void EnsureAiConfig()
        {
            config.AiProvider ??= new AiProviderSettings();
            config.AiProvider.CharacterModelOverrides ??= new System.Collections.Generic.Dictionary<string, string>();
            config.AiActors ??= new AiActorSettings();
            config.AiActors.RoleModelOverrides ??= new System.Collections.Generic.Dictionary<string, string>();
            if (config.AiActors.MaxAutonomousMonthlyBudget <= 0)
                config.AiActors.MaxAutonomousMonthlyBudget = 200;
            state.ReconcileOldSaves();
            state.DelegatedAuthoritySettings.AllowAutonomousActions = config.AiActors.AllowAutonomousActions;
            state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget = config.AiActors.MaxAutonomousMonthlyBudget;
        }

        private static string GetAiDialogueLengthDisplay(AiDialogueLengthLevel level)
        {
            return level switch
            {
                AiDialogueLengthLevel.Brief => "مختصر",
                AiDialogueLengthLevel.Detailed => "تفصيلي",
                _ => "عادي"
            };
        }

        private static string GetDefaultApiKeyEnvironment(AiProviderType providerType)
        {
            return providerType switch
            {
                AiProviderType.Gemini => "GEMINI_API_KEY",
                AiProviderType.OpenRouter => "OPENROUTER_API_KEY",
                AiProviderType.Mistral => "MISTRAL_API_KEY",
                AiProviderType.Grok => "GROK_API_KEY",
                AiProviderType.OpenAICompatible => "OPENAI_API_KEY",
                AiProviderType.OllamaCloud => "OLLAMA_API_KEY",
                AiProviderType.CustomHttp => "CUSTOM_AI_API_KEY",
                _ => ""
            };
        }

        private void OpenAiActorSettings()
        {
            ClearDynamicPanel();
            state.GameMode = "settings_ai_actors";
            SetScreenTitle("شخصيات الذكاء الاصطناعي");
            EnsureAiConfig();
            SetNarrativeText("اختر أنواع الشخصيات التي تستخدم صياغة AI أو قرارات AI.\n" + AiRuntimePolicySystem.GetSummary(config.AiActors), false);

            AddAiCheckBox("الزوجات", () => config.AiActors.ApplyToSpouses, v => config.AiActors.ApplyToSpouses = v);
            AddAiCheckBox("الوزراء والمجلس", () => config.AiActors.ApplyToMinisters, v => config.AiActors.ApplyToMinisters = v);
            AddAiCheckBox("الورثة", () => config.AiActors.ApplyToHeirs, v => config.AiActors.ApplyToHeirs = v);
            AddAiCheckBox("الولاة", () => config.AiActors.ApplyToGovernors, v => config.AiActors.ApplyToGovernors = v);
            AddAiCheckBox("الفصائل", () => config.AiActors.ApplyToFactions, v => config.AiActors.ApplyToFactions = v);
            AddAiCheckBox("حكام الدول المجاورة", () => config.AiActors.ApplyToNeighborRulers, v => config.AiActors.ApplyToNeighborRulers = v);
            AddAiCheckBox("الأعداء", () => config.AiActors.ApplyToEnemies, v => config.AiActors.ApplyToEnemies = v);
            AddAiCheckBox("شخصيات أخرى", () => config.AiActors.ApplyToOtherCharacters, v => config.AiActors.ApplyToOtherCharacters = v);
            AddAiCheckBox("السماح بقرارات الوزراء عبر AI", () => config.AiActors.AllowAiMinisterDecisions, v => config.AiActors.AllowAiMinisterDecisions = v);
            AddAiCheckBox("السماح بقرارات الزوجات عبر AI", () => config.AiActors.AllowAiSpouseDecisions, v => config.AiActors.AllowAiSpouseDecisions = v);
            AddAiCheckBox("السماح بقرارات حكام الدول المجاورة تجاهك عبر AI", () => config.AiActors.AllowAiNeighborDecisions, v => config.AiActors.AllowAiNeighborDecisions = v);
            AddAiCheckBox("السماح للدول المجاورة بإدارة شؤونها الداخلية عبر AI", () => config.AiActors.AllowAiNeighborRealmManagement, v => config.AiActors.AllowAiNeighborRealmManagement = v);
            AddAiCheckBox("السماح بقرارات الولاة الداخلية عبر AI", () => config.AiActors.AllowAiGovernorDecisions, v => config.AiActors.AllowAiGovernorDecisions = v);
            AddAiCheckBox("السماح بقرارات الفصائل عبر AI", () => config.AiActors.AllowAiFactionDecisions, v => config.AiActors.AllowAiFactionDecisions = v);

            AddActionButton("عودة لمزودات الذكاء الاصطناعي", (s, e) => OpenSuperTonicManager());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void AddAiCheckBox(string label, Func<bool> getter, Action<bool> setter)
        {
            var checkbox = new CheckBox
            {
                Text = label,
                Checked = getter(),
                Width = 350,
                Height = 32,
                Font = new Font("Arial", 10, FontStyle.Bold),
                AccessibleName = label,
                AccessibleRole = AccessibleRole.CheckButton
            };
            checkbox.CheckedChanged += (s, e) => setter(checkbox.Checked);
            dynamicPanel.Controls.Add(checkbox);
        }

        private void OpenAiEndpointEditor()
        {
            ClearDynamicPanel();
            state.GameMode = "settings_ai_endpoint";
            SetScreenTitle("تعديل endpoint");
            EnsureAiConfig();
            SetNarrativeText("اكتب endpoint للمزود الحالي. مثال Ollama المحلي: http://localhost:11434", false);
            var input = new TextBox
            {
                Width = 500,
                Font = new Font("Arial", 12),
                Text = string.IsNullOrWhiteSpace(config.AiProvider.Endpoint) ? AiModelCatalogService.GetDefaultEndpoint(config.AiProvider.ProviderType) : config.AiProvider.Endpoint,
                AccessibleName = "رابط endpoint للذكاء الاصطناعي",
                AccessibleRole = AccessibleRole.Text
            };
            dynamicPanel.Controls.Add(input);
            AddActionButton("حفظ endpoint", (s, e) => {
                config.AiProvider.Endpoint = input.Text.Trim();
                OpenSuperTonicManager();
            });
            AddActionButton("استخدام endpoint الافتراضي", (s, e) => {
                config.AiProvider.Endpoint = AiModelCatalogService.GetDefaultEndpoint(config.AiProvider.ProviderType);
                OpenSuperTonicManager();
            });
            AddActionButton("إلغاء", (s, e) => OpenSuperTonicManager());
            input.Focus();
        }

        private void OpenAiManualModelEditor()
        {
            ClearDynamicPanel();
            state.GameMode = "settings_ai_model_manual";
            SetScreenTitle("إدخال نموذج AI");
            EnsureAiConfig();
            SetNarrativeText("اكتب اسم النموذج كما يظهر لدى المزود.", false);
            var input = new TextBox
            {
                Width = 500,
                Font = new Font("Arial", 12),
                Text = config.AiProvider.Model,
                AccessibleName = "اسم نموذج الذكاء الاصطناعي",
                AccessibleRole = AccessibleRole.Text
            };
            dynamicPanel.Controls.Add(input);
            AddActionButton("حفظ النموذج", (s, e) => {
                config.AiProvider.Model = input.Text.Trim();
                config.AiActors.DefaultModel = config.AiProvider.Model;
                OpenSuperTonicManager();
            });
            AddActionButton("إلغاء", (s, e) => OpenSuperTonicManager());
            input.Focus();
        }

        private async System.Threading.Tasks.Task FetchAiModelsForCurrentProvider()
        {
            EnsureAiConfig();
            SetNarrativeText("جاري جلب النماذج من المزود...");
            var result = await AiModelCatalogService.FetchModelsAsync(config.AiProvider);
            OpenAiModelList(result.Message, result.Models);
        }

        private void OpenAiModelList(string message, System.Collections.Generic.List<AiModelDescriptor> models)
        {
            ClearDynamicPanel();
            state.GameMode = "settings_ai_models";
            SetScreenTitle("نماذج الذكاء الاصطناعي");
            SetNarrativeText(message + "\n\n" + (models.Count == 0 ? "لا توجد نماذج." : string.Join("\n", models.Take(25).Select((m, i) => $"{i + 1}. {m.Id} - {m.Source}"))), false);

            foreach (var model in models.Take(20))
            {
                AddActionButton($"اختيار نموذج: {model.DisplayName}", (s, e) => {
                    config.AiProvider.Model = model.Id;
                    config.AiActors.DefaultModel = model.Id;
                    OpenSuperTonicManager();
                });
            }

            AddActionButton("عودة لمزودات الذكاء الاصطناعي", (s, e) => OpenSuperTonicManager());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void OpenAiSmallModelProtocol()
        {
            ClearDynamicPanel();
            state.GameMode = "settings_ai_protocol";
            SetScreenTitle("بروتوكول النماذج الصغيرة");
            SetNarrativeText(AiCommandRouterSystem.GetProtocolPrompt(), false);

            var input = new TextBox
            {
                Width = 220,
                Font = new Font("Arial", 12),
                AccessibleName = "اختبار أمر رقمي من نموذج صغير",
                AccessibleRole = AccessibleRole.Text
            };
            dynamicPanel.Controls.Add(input);

            AddActionButton("اختبار الأمر داخل اللعبة", (s, e) => {
                if (AiCommandRouterSystem.TryRoute(input.Text, out var command))
                {
                    SetNarrativeText($"سيتم تنفيذ المسار: {AiCommandRouterSystem.Describe(command)}");
                    ExecuteAiRoutedCommand(command);
                }
                else
                {
                    SetNarrativeText("لم أفهم الأمر. استخدم رقماً من 1 إلى 10.");
                }
            });
            AddActionButton("عودة لمزودات الذكاء الاصطناعي", (s, e) => OpenSuperTonicManager());
            input.Focus();
        }

        private void ExecuteAiRoutedCommand(AiRoutedCommand command)
        {
            if (command == AiRoutedCommand.None)
                return;

            switch (command)
            {
                case AiRoutedCommand.Governance:
                    ShowGovernanceHub(this, EventArgs.Empty);
                    break;
                case AiRoutedCommand.Court:
                    ShowCourtHub(this, EventArgs.Empty);
                    break;
                case AiRoutedCommand.Economy:
                    ShowEconomyHub(this, EventArgs.Empty);
                    break;
                case AiRoutedCommand.WarDiplomacy:
                    ShowWarDiplomacyHub(this, EventArgs.Empty);
                    break;
                case AiRoutedCommand.AdvancedDiplomacy:
                    ShowAdvancedDiplomacyMenu(this, EventArgs.Empty);
                    break;
                case AiRoutedCommand.Provinces:
                    ShowResourceManagement(this, EventArgs.Empty);
                    break;
                case AiRoutedCommand.Council:
                    ShowCouncilScreen(this, EventArgs.Empty);
                    break;
                case AiRoutedCommand.DynastyChronicle:
                    ShowDynastyChronicleMenu();
                    break;
                case AiRoutedCommand.CurrentSummary:
                    ShowCurrentKingSummary();
                    break;
                case AiRoutedCommand.SuggestedDecision:
                    ShowSuggestedDecision();
                    break;
            }
        }

        private bool CheckSuperTonicInstalled()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "-c \"import sys\"", // Minimal check
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit(1500);
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        // --- CHARACTER CREATION FLOW ---
        private void StartCharacterCreation()
        {
            state = new GameState(); 
            state.GameMode = "creation";
            ShowNameSelection();
        }

        private void ShowNameSelection()
        {
            ClearDynamicPanel();
            SetNarrativeText("اختر اسم الخليفة العباسي الجديد:", false);
            
            var nameInput = new TextBox { Width = 300, Font = new Font("Arial", 16), AccessibleName = "أدخل اسم الخليفة هنا", AccessibleRole = AccessibleRole.Text };
            dynamicPanel.Controls.Add(nameInput);
            
            AddActionButton("تأكيد الاسم", (s, e) => {
                if (!string.IsNullOrWhiteSpace(nameInput.Text))
                {
                    state.RulerName = nameInput.Text;
                    ShowAgeSelection();
                }
                else
                {
                    SpeakToActiveReader("الرجاء إدخال اسم صحيح.");
                }
            });
            
            nameInput.Focus();
        }

        private void ShowAgeSelection()
        {
            ClearDynamicPanel();
            SetNarrativeText($"في أي مرحلة عمرية تولى الخليفة {state.RulerName} الحكم؟", false);
            
            AddActionButton("شاب (20 عاماً) - حيوية ولكن خبرة أقل", (s, e) => { state.RulerAge = 20; ShowMaritalSelection(); });
            AddActionButton("ناضج (30 عاماً) - توازن بين الشباب والخبرة", (s, e) => { state.RulerAge = 30; ShowMaritalSelection(); });
            AddActionButton("مخضرم (40 عاماً) - حكمة في الإدارة", (s, e) => { state.RulerAge = 40; ShowMaritalSelection(); });
            AddActionButton("متقدم في السن (50 عاماً) - هيبة كبرى ولكن صحة أضعف", (s, e) => { state.RulerAge = 50; ShowMaritalSelection(); });
            
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowMaritalSelection()
        {
            ClearDynamicPanel();
            SetNarrativeText($"هل تولى الخليفة {state.RulerName} الحكم وهو متزوج؟", false);
            
            AddActionButton("نعم، متزوج", (s, e) => { 
                state.Wives.Add(new Spouse { Id = Guid.NewGuid().ToString(), Name = "الخيزران", OriginType = "NobleFamily", OpinionOfKing = 60, Influence = 30, PregnancyDaysLeft = 0 });
                ShowHeirSelection(); 
            });
            AddActionButton("لا، لا يزال أعزباً", (s, e) => { 
                StartSandbox(); 
            });
            
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowHeirSelection()
        {
            ClearDynamicPanel();
            SetNarrativeText($"هل رُزق الخليفة {state.RulerName} بولد ليكون ولياً للعهد؟", false);
            
            AddActionButton("نعم، رزق بولد", (s, e) => { 
                state.Children.Add(new Child { Name = "ولي العهد", Age = 1, IsHeir = true });
                state.HeirName = "ولي العهد";
                state.HeirAge = 1;
                StartSandbox(); 
            });
            AddActionButton("لا، ليس بعد", (s, e) => { 
                StartSandbox(); 
            });
            
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        // --- GAMEPLAY MODULE ---
        private void StartSandbox()
        {
            ApplyConfigToState();
            state.ReconcileOldSaves();
            DiplomacySystem.SynchronizeDiplomacyState(state);
            
            state.GameMode = "sandbox";
            state.Time.IsPaused = true;
            
            audio.StopAmbient();
            audio.Play("ambient_nature");
            
            txtDisplay.Clear();
            SetNarrativeText($"سنة 1071م، بعد أن غيّر انتصار ملاذكرد ميزان القوى في المشرق، تبدأ حكمك من بغداد باسم الخلافة العباسية. لتبدأ أوامرك أيها الخليفة {state.RulerName}.");
            DynastyChronicleSystem.RecordEvent(state, "Succession", "تولي الخليفة الحكم", $"تولى الخليفة {state.RulerName} الحكم في بغداد سنة 1071م بعد أن تغير ميزان القوى في المشرق.", 10, 3);
            
            RenderSandboxButtons();
            UpdateUI();
        }

        private void RenderSandboxButtons()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox";
            SetScreenTitle("الأوامر الملكية");
            
            SetNarrativeText("اختر بوابة إدارة كبرى. الاختصارات: Alt+1 للحكم، Alt+2 للبلاط، Alt+3 للاقتصاد، Alt+4 للحرب والدبلوماسية.", false);
            AddActionButton("Alt+1 الحكم والتقارير", ShowGovernanceHub);
            AddActionButton("Alt+2 القصر والمجلس والدين", ShowCourtHub);
            AddActionButton("Alt+3 الاقتصاد والمقاطعات", ShowEconomyHub);
            AddActionButton("Alt+4 الحرب والدبلوماسية والاستخبارات", ShowWarDiplomacyHub);
            AddActionButton("حفظ التقدم الحالي (Save Game) 💾", (s, e) => SaveGame());
            
            if (dynamicPanel.Controls.Count > 0)
                dynamicPanel.Controls[0].Focus();
        }

        private void ShowGovernanceHub(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_governance";
            SetScreenTitle("الحكم والتقارير");
            SetNarrativeText("بوابة الحكم تجمع التقارير، لوحة المخاطر، الزمن، الذاكرة السياسية، وأهداف عهد الملك.", false);

            AddActionButton("لوحة الملك السريعة", (s, evt) => ShowRoyalDashboard());
            AddActionButton("ملخص الملك الآن", (s, evt) => ShowCurrentKingSummary());
            AddActionButton("أهم 5 أخطار", (s, evt) => ShowTopRisks());
            AddActionButton("أفضل 5 فرص", (s, evt) => ShowTopOpportunities());
            AddActionButton("اقترح علي قراراً", (s, evt) => ShowSuggestedDecision());
            AddActionButton("آخر الأحداث باختصار", (s, evt) => ShowRecentEventsSummary());
            AddActionButton("تقرير المملكة الموحد", (s, evt) => ShowKingdomReport());
            AddActionButton("العالم السياسي الحي والذاكرة", ShowLivingRealmMenu);
            AddActionButton("أهداف الشخصيات والطموحات", (s, evt) => ShowCharacterObjectivesMenu());
            AddActionButton("كتاب العرش ومجد السلالة", (s, evt) => ShowDynastyChronicleMenu());
            AddActionButton("أهداف عهد الملك", (s, evt) => ShowReignObjectivesMenu());
            AddActionButton($"إدارة الزمن والتقويم [{state.Time.GetDateString()}]", ShowTimeManagementMenu);
            AddActionButton("إدارة وتغيير الأسماء", ShowRenameMenu);
            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());

            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowCourtHub(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_court_hub";
            SetScreenTitle("القصر والمجلس والدين");
            SetNarrativeText("بوابة البلاط تجمع الأسرة الحاكمة، المجلس، الوزير الأول، والدين.", false);

            AddActionButton("إدارة الأسرة الحاكمة", ShowRoyalPalace);
            AddActionButton("مجلس البلاط والمستشارين", ShowCouncilScreen);
            AddActionButton("التفويض الملكي", (s, evt) => ShowRoyalDelegationMenu());
            AddActionButton("الجلسات والاجتماعات الذكية", (s, evt) => ShowAiMeetingHub());
            AddActionButton("اجتماع المجلس الملكي", ShowCouncilMeetingMenu);
            AddActionButton("الدين ورجل الدين", ShowReligionMenu);
            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());

            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowRoyalDelegationMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_royal_delegation";
            SetScreenTitle("التفويض الملكي");
            AiAgentSystem.EnsureAgents(state);
            config.AiActors.AllowAutonomousActions = state.DelegatedAuthoritySettings.AllowAutonomousActions;
            config.AiActors.MaxAutonomousMonthlyBudget = state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget;

            SetNarrativeText(AiAgentSystem.GetDelegationReport(state), false);

            AddActionButton("عرض الشخصيات المفوضة", (s, evt) => ShowDelegatedAgents());
            AddActionButton("ضبط صلاحيات الوزير الأول", (s, evt) => ShowAuthorityOptions(AiAgentRole.FirstMinister));
            AddActionButton("ضبط صلاحيات مسؤول الجواسيس", (s, evt) => ShowAuthorityOptions(AiAgentRole.Spymaster));
            AddActionButton("ضبط صلاحيات القائد العسكري", (s, evt) => ShowAuthorityOptions(AiAgentRole.MilitaryCommander));
            AddActionButton("ضبط صلاحيات الزوجة أو الملكة", (s, evt) => ShowAuthorityOptions(AiAgentRole.SpouseQueen));
            AddActionButton("ضبط استقلالية الولاة", (s, evt) => ShowAuthorityOptions(AiAgentRole.Governor));
            AddActionButton("عرض المقترحات المعلقة", (s, evt) => ShowPendingAiProposals());
            AddActionButton("عرض سجل الأفعال التلقائية", (s, evt) => ShowAiActionLog());
            AddActionButton($"الأفعال التلقائية: {(state.DelegatedAuthoritySettings.AllowAutonomousActions ? "مسموحة" : "معطلة")}", (s, evt) =>
            {
                state.DelegatedAuthoritySettings.AllowAutonomousActions = !state.DelegatedAuthoritySettings.AllowAutonomousActions;
                config.AiActors.AllowAutonomousActions = state.DelegatedAuthoritySettings.AllowAutonomousActions;
                AiAgentSystem.EnsureAgents(state);
                ShowRoyalDelegationMenu();
            });
            AddActionButton($"حد ميزانية التفويض الشهرية: {state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget} ذهب", (s, evt) =>
            {
                state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget = state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget switch
                {
                    < 200 => 200,
                    < 500 => 500,
                    < 1000 => 1000,
                    _ => 100
                };
                config.AiActors.MaxAutonomousMonthlyBudget = state.DelegatedAuthoritySettings.MaxAutonomousMonthlyBudget;
                ShowRoyalDelegationMenu();
            });
            AddActionButton("العودة للقصر والمجلس", ShowCourtHub);

            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowDelegatedAgents()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_delegated_agents";
            SetScreenTitle("الشخصيات المفوضة");
            SetNarrativeText(AiAgentSystem.GetAgentsReport(state));
            AddActionButton("العودة للتفويض الملكي", (s, evt) => ShowRoyalDelegationMenu());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowAuthorityOptions(AiAgentRole role)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_authority_options";
            SetScreenTitle("ضبط الصلاحيات");
            AiAgentSystem.EnsureAgents(state);

            string roleName = AiAgentSystem.GetRoleDisplayName(role);
            string current = AiAgentSystem.GetAuthorityDisplayName(AiAgentSystem.GetAuthorityForRole(state, role));
            SetNarrativeText($"ضبط صلاحيات {roleName}.\nالصلاحية الحالية: {current}.\nكل الصلاحيات لا تسمح بتعديل GameState مباشرة؛ التنفيذ يمر عبر التحقق والأنظمة القائمة.", false);

            AddAuthorityButton(role, AiAuthorityLevel.None);
            AddAuthorityButton(role, AiAuthorityLevel.Advisor);
            AddAuthorityButton(role, AiAuthorityLevel.LimitedDelegate);
            AddAuthorityButton(role, AiAuthorityLevel.TrustedDelegate);
            AddAuthorityButton(role, AiAuthorityLevel.RoyalRightHand);
            AddActionButton("عودة للتفويض الملكي", (s, evt) => ShowRoyalDelegationMenu());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void AddAuthorityButton(AiAgentRole role, AiAuthorityLevel authority)
        {
            AddActionButton(AiAgentSystem.GetAuthorityDisplayName(authority), (s, evt) =>
            {
                AiAgentSystem.SetAuthorityForRole(state, role, authority);
                SetNarrativeText($"تم ضبط {AiAgentSystem.GetRoleDisplayName(role)} على: {AiAgentSystem.GetAuthorityDisplayName(authority)}.");
                ShowRoyalDelegationMenu();
            });
        }

        private void ShowPendingAiProposals()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_ai_proposals";
            SetScreenTitle("المقترحات المعلقة");
            AiAgentSystem.EnsureAgents(state);
            SetNarrativeText(AiProposalQueue.GetPendingProposalReport(state, false));

            AddActionButton("طلب مقترحات جديدة الآن", (s, evt) =>
                HandleActionResult(AiProposalQueue.GenerateMonthlyProposals(state, 5), () => ShowPendingAiProposals()));

            var pending = state.AiProposalQueue
                .Where(p => p.Status == AiProposalStatus.Pending)
                .OrderByDescending(p => p.CreatedDay)
                .Take(12)
                .ToList();

            foreach (var proposal in pending)
            {
                string label = $"مقترح: {proposal.AgentName} - {AiAgentSystem.GetActionDisplayName(proposal.ActionType)}";
                AddActionButton(label, (s, evt) => ShowAiProposalDetails(proposal.Id));
            }

            AddActionButton("العودة للتفويض الملكي", (s, evt) => ShowRoyalDelegationMenu());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowAiProposalDetails(string proposalId)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_ai_proposal_details";
            SetScreenTitle("تفاصيل مقترح");
            var request = state.AiProposalQueue.FirstOrDefault(p => p.Id == proposalId);
            if (request == null)
            {
                SetNarrativeText("لم يتم العثور على المقترح.");
                AddActionButton("عودة للمقترحات", (s, evt) => ShowPendingAiProposals());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                return;
            }

            SetNarrativeText(AiActionValidator.FormatProposal(state, request, false));
            AddActionButton("قراءة التفاصيل", (s, evt) => SetNarrativeText(AiActionValidator.FormatProposal(state, request, true)));
            AddActionButton("طلب تفسير بصوت الشخصية", (s, evt) => ShowAiProposalExplanation(request.Id));
            AddActionButton("الموافقة على المقترح", (s, evt) =>
                HandleActionResult(AiProposalQueue.ApproveProposal(state, request.Id), () => ShowPendingAiProposals()));
            AddActionButton("رفض المقترح", (s, evt) =>
                HandleActionResult(AiProposalQueue.RejectProposal(state, request.Id), () => ShowPendingAiProposals()));
            AddActionButton("تأجيل المقترح", (s, evt) =>
                HandleActionResult(AiProposalQueue.DeferProposal(state, request.Id), () => ShowPendingAiProposals()));
            AddActionButton("تعديل المقترح إلى مراجعة مجلس بلا تنفيذ مباشر", (s, evt) =>
                HandleActionResult(ConvertProposalToCouncilReview(request), () => ShowAiProposalDetails(request.Id)));
            AddActionButton("تعطيل مقترحات مشابهة", (s, evt) =>
                HandleActionResult(AiProposalQueue.DisableSimilarProposal(state, request.Id), () => ShowPendingAiProposals()));
            AddActionButton("عودة للمقترحات", (s, evt) => ShowPendingAiProposals());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowAiProposalExplanation(string proposalId)
        {
            var request = state.AiProposalQueue.FirstOrDefault(p => p.Id == proposalId);
            if (request == null)
            {
                SetNarrativeText("لم يتم العثور على المقترح.");
                return;
            }

            var profile = AiAgentSystem.GetProfile(state, request.AgentCharacterId);
            if (profile == null)
            {
                SetNarrativeText("لا يوجد وكيل مرتبط بالمقترح.");
                return;
            }

            string context = AiContextBuilder.BuildDialogueContext(state, profile, request);
            string line = SuperTonicAI.GenerateDialogue(profile.CharacterName, state, context);
            SetNarrativeText($"{profile.CharacterName} يفسر المقترح:\n{line}", true, true);
        }

        private GameActionResult ConvertProposalToCouncilReview(AiActionRequest request)
        {
            request.ActionType = AiActionType.RequestCouncilMeeting;
            request.TargetType = AiActionTargetType.Council;
            request.TargetId = "council";
            request.TargetName = "المجلس";
            request.GoldCost = 0;
            request.TimeCostDays = 0;
            request.EstimatedRisk = 5;
            request.RequiresKingApproval = true;
            request.Reason = "عدّل الملك المقترح ليصبح مراجعة سياسية دون تنفيذ مباشر.";
            request.ExpectedBenefit = "فهم الموقف قبل الإنفاق أو المخاطرة.";
            request.SpokenJustification = "ملخص: سأعرض المسألة على المجلس أولاً، ولا أنفذ شيئاً قبل أمرك.";
            request.SimilarityKey = $"{request.Role}:{request.ActionType}:{request.TargetType}:{request.TargetId}";

            return new GameActionResult
            {
                Success = true,
                Title = "تعديل مقترح",
                MainMessage = "تم تعديل المقترح إلى مراجعة مجلس بلا تنفيذ مباشر."
            };
        }

        private void ShowAiActionLog()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_ai_action_log";
            SetScreenTitle("سجل قرارات المجلس");
            SetNarrativeText(AiProposalQueue.GetActionLogReport(state));
            AddActionButton("عرض المقترحات المعلقة", (s, evt) => ShowPendingAiProposals());
            AddActionButton("العودة للتفويض الملكي", (s, evt) => ShowRoyalDelegationMenu());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowEconomyHub(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_economy_hub";
            SetScreenTitle("الاقتصاد والمقاطعات");
            SetNarrativeText("بوابة الاقتصاد تجمع المقاطعات، الموارد، الخزينة، العقود، والأطلس النصي.", false);

            AddActionButton("شؤون المقاطعات وإدارة الموارد", ShowResourceManagement);
            AddActionButton("الخزينة والقروض", ShowTreasuryMenu);
            AddActionButton("تنمية التجارة والأسواق", (s, evt) => ShowTradeDevelopmentMenu());
            AddActionButton("الأطلس الاستراتيجي النصي", (s, evt) => ShowStrategicAtlasMenu());
            AddActionButton("أهداف عهد الملك", (s, evt) => ShowReignObjectivesMenu());
            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());

            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowWarDiplomacyHub(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_war_diplomacy_hub";
            SetScreenTitle("الحرب والدبلوماسية والاستخبارات");
            SetNarrativeText("بوابة الحرب تجمع الجيوش، الحصار، الدبلوماسية، الاستخبارات، الفصائل، وتفسير قرارات الشخصيات.", false);

            AddActionButton("القيادة العسكرية للجيوش", ShowArmyCommandMenu);
            AddActionButton("الخريطة السياسية والحصار", ShowPoliticalMap);
            AddActionButton("الدبلوماسية المتقدمة", ShowAdvancedDiplomacyMenu);
            AddActionButton("الاستخبارات الملكية", ShowIntelligenceMenu);
            AddActionButton("الأسرار والخطافات والمكائد", (s, evt) => ShowHooksAndSchemesMenu());
            AddActionButton("الولاة والفصائل", ShowPoliticalAffairsMenu);
            AddActionButton("لماذا حدث هذا؟", (s, evt) => ShowWhyDidThisHappenMenu());
            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());

            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowRoyalDashboard()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_dashboard";
            SetScreenTitle("لوحة الملك السريعة");
            SetNarrativeText(GrandStrategySystem.GetRoyalDashboard(state));
            AddActionButton("العودة للحكم والتقارير", ShowGovernanceHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowCurrentKingSummary()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_king_summary";
            SetScreenTitle("ملخص الملك الآن");
            SetNarrativeText(GrandStrategySystem.GetCurrentKingSummary(state));
            AddActionButton("اقترح علي قراراً", (s, evt) => ShowSuggestedDecision());
            AddActionButton("العودة للحكم والتقارير", ShowGovernanceHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowTopRisks()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_top_risks";
            SetScreenTitle("أهم الأخطار");
            SetNarrativeText(GrandStrategySystem.GetTopRisksReport(state));
            AddActionButton("ملخص الملك الآن", (s, evt) => ShowCurrentKingSummary());
            AddActionButton("العودة للحكم والتقارير", ShowGovernanceHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowTopOpportunities()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_top_opportunities";
            SetScreenTitle("أفضل الفرص");
            SetNarrativeText(GrandStrategySystem.GetTopOpportunitiesReport(state));
            AddActionButton("تنمية التجارة والأسواق", (s, evt) => ShowTradeDevelopmentMenu());
            AddActionButton("العودة للحكم والتقارير", ShowGovernanceHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowSuggestedDecision()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_suggested_decision";
            SetScreenTitle("اقتراح قرار");
            SetNarrativeText("اقتراح القرار الآن:\n" + GrandStrategySystem.GetSuggestedDecision(state));
            AddActionButton("أهم 5 أخطار", (s, evt) => ShowTopRisks());
            AddActionButton("أفضل 5 فرص", (s, evt) => ShowTopOpportunities());
            AddActionButton("العودة للحكم والتقارير", ShowGovernanceHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowRecentEventsSummary()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_recent_events";
            SetScreenTitle("آخر الأحداث");
            SetNarrativeText(GrandStrategySystem.GetRecentEventsSummary(state));
            AddActionButton("قراءة كتاب العرش", (s, evt) => ShowDynastyChronicleMenu());
            AddActionButton("العودة للحكم والتقارير", ShowGovernanceHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowCharacterObjectivesMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_character_objectives";
            SetScreenTitle("أهداف الشخصيات");
            PersonalObjectiveSystem.EnsurePersonalObjectives(state);
            SetNarrativeText(PersonalObjectiveSystem.GetObjectivesReport(state));
            AddActionButton("تحديث التقرير", (s, evt) => ShowCharacterObjectivesMenu());
            AddActionButton("العودة للحكم والتقارير", ShowGovernanceHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowDynastyChronicleMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_dynasty_chronicle";
            SetScreenTitle("كتاب العرش");
            SetNarrativeText(DynastyChronicleSystem.GetChronicleReport(state));
            AddActionButton("ملخص المجد السلالي", (s, evt) => SetNarrativeText(DynastyChronicleSystem.GetLegacySummary(state)));
            AddActionButton("العودة للحكم والتقارير", ShowGovernanceHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowReignObjectivesMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_reign_objectives";
            SetScreenTitle("أهداف عهد الملك");
            SetNarrativeText(GrandStrategySystem.GetReignObjectivesReport(state));
            AddActionButton("العودة للحكم والتقارير", ShowGovernanceHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowStrategicAtlasMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_atlas";
            SetScreenTitle("الأطلس الاستراتيجي النصي");
            SetNarrativeText("اختر مقاطعة من القائمة أو اعرض الأطلس الكامل.", false);

            var provinceCombo = new ComboBox
            {
                Width = 350,
                Font = new Font("Arial", 12),
                DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = "اختر مقاطعة لعرضها في الأطلس",
                AccessibleRole = AccessibleRole.ComboBox
            };
            foreach (var province in state.Provinces)
                provinceCombo.Items.Add(province.Name);
            if (provinceCombo.Items.Count > 0)
                provinceCombo.SelectedIndex = 0;
            dynamicPanel.Controls.Add(provinceCombo);

            AddActionButton("عرض المقاطعة المختارة", (s, evt) => {
                string selected = provinceCombo.SelectedItem?.ToString() ?? "";
                SetNarrativeText(GrandStrategySystem.GetStrategicAtlas(state, selected));
            });
            AddActionButton("عرض الأطلس الكامل", (s, evt) => SetNarrativeText(GrandStrategySystem.GetStrategicAtlas(state)));
            AddActionButton("العودة للاقتصاد والمقاطعات", ShowEconomyHub);

            provinceCombo.Focus();
        }

        private void ShowWhyDidThisHappenMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_why";
            SetScreenTitle("لماذا حدث هذا؟");
            SetNarrativeText("اختر شخصية أو جهة سياسية لعرض تفسير مختصر لعوامل قرارها: الخوف، الطموح، ضعفك، والذكريات السياسية.", false);

            foreach (var governor in state.Governors.Take(8))
            {
                AddActionButton($"الوالي {governor.Name}", (s, evt) =>
                    SetNarrativeText(GrandStrategySystem.ExplainPoliticalDecision(state, "Governor", governor.Id)));
            }

            foreach (var wife in state.Wives.Where(w => !w.IsDead).Take(4))
            {
                AddActionButton($"الملكة {wife.Name}", (s, evt) =>
                    SetNarrativeText(GrandStrategySystem.ExplainPoliticalDecision(state, "Spouse", wife.Id)));
            }

            foreach (var neighbor in state.Neighbors.Take(5))
            {
                AddActionButton($"حاكم {neighbor.Name}", (s, evt) =>
                    SetNarrativeText(GrandStrategySystem.ExplainPoliticalDecision(state, "Neighbor", neighbor.Id)));
            }

            AddActionButton("العودة للحرب والدبلوماسية", ShowWarDiplomacyHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowHooksAndSchemesMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_hooks_schemes";
            SetScreenTitle("الأسرار والخطافات والمكائد");
            state.ReconcileOldSaves();

            var report = new StringBuilder();
            report.AppendLine("الأسرار والخطافات والمكائد");
            report.AppendLine();
            var hooks = state.PoliticalHooks.Where(h => !h.IsUsed).Take(5).ToList();
            report.AppendLine(hooks.Count == 0
                ? "لا توجد خطافات سياسية نشطة."
                : string.Join("\n", hooks.Select(h => $"- {h.TargetName}: {h.Strength}")));
            report.AppendLine();
            var schemes = state.ActiveSchemes.Where(s => !s.IsResolved).Take(5).ToList();
            report.AppendLine(schemes.Count == 0
                ? "لا توجد مكائد نشطة."
                : string.Join("\n", schemes.Select(s => $"- {s.TargetName}: {s.Type}، المرحلة {s.Stage}، التقدم {s.Progress}%")));
            SetNarrativeText(report.ToString().Trim(), false);

            AddActionButton("البحث عن سر لدى والٍ", (s, evt) => ShowDiscoverGovernorSecretMenu());
            AddActionButton("بدء مكيدة ضد شخصية", (s, evt) => ShowStartSchemeMenu());
            AddActionButton("العودة للحرب والدبلوماسية", ShowWarDiplomacyHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowDiscoverGovernorSecretMenu()
        {
            ClearDynamicPanel();
            SetScreenTitle("البحث عن سر");
            SetNarrativeText("اختر والياً ليحاول جهاز الاستخبارات اكتشاف سر يمكن استخدامه كخطاف.", false);
            foreach (var governor in state.Governors)
            {
                AddActionButton(governor.Name, (s, evt) =>
                    HandleActionResult(GrandStrategySystem.DiscoverSecret(state, "Governor", governor.Id), ShowHooksAndSchemesMenu));
            }
            AddActionButton("عودة", (s, evt) => ShowHooksAndSchemesMenu());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowStartSchemeMenu()
        {
            ClearDynamicPanel();
            SetScreenTitle("بدء مكيدة");
            SetNarrativeText("اختر هدفاً. المكائد تتقدم مع الأيام وقد تنكشف أو تنجح لاحقاً.", false);

            foreach (var character in state.RealmCharacters.Where(c => c.SourceType == "Governor" || c.SourceType == "Neighbor").Take(12))
            {
                AddActionButton($"اختلاق خطاف ضد {character.Name}", (s, evt) =>
                    HandleActionResult(GrandStrategySystem.StartScheme(state, SchemeType.FabricateHook, character.Id), ShowHooksAndSchemesMenu));
                AddActionButton($"اغتيال {character.Name}", (s, evt) =>
                    HandleActionResult(GrandStrategySystem.StartScheme(state, SchemeType.Murder, character.Id), ShowHooksAndSchemesMenu));
            }

            AddActionButton("عودة", (s, evt) => ShowHooksAndSchemesMenu());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowTimeManagementMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_time";
            string pauseStatus = state.Time.IsPaused ? "متوقف" : "مستمر";
            SetNarrativeText($"الزمن الحالي: {state.Time.GetDateString()}.\nحالة الزمن: {pauseStatus}");

            if (state.Time.IsPaused)
            {
                AddActionButton("تشغيل الزمن المستمر ⏳", (s, evt) => {
                    state.Time.IsPaused = false; timeTimer.Start();
                    ShowTimeManagementMenu(null, null);
                });
            }
            else
            {
                AddActionButton("إيقاف الزمن ⏸️", (s, evt) => {
                    state.Time.IsPaused = true; timeTimer.Stop();
                    ShowTimeManagementMenu(null, null);
                });
            }

            AddActionButton("تقديم أسبوع ⏩", (s, evt) => {
                state.Time.IsPaused = true; timeTimer.Stop();
                var res = CalendarTimeSystem.AdvanceWeek(state);
                HandleActionResult(res, () => ShowTimeManagementMenu(null, null));
            });

            AddActionButton("تقديم شهر كامل ⏭️", (s, evt) => {
                state.Time.IsPaused = true; timeTimer.Stop();
                var res = CalendarTimeSystem.AdvanceMonth(state);
                HandleActionResult(res, () => ShowTimeManagementMenu(null, null));
            });

            AddActionButton("العودة 🔙", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowArmyCommandMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_army";
            audio.StopAmbient();
            audio.Play("ambient_nature");
            audio.PlayPaper();
            SetNarrativeText("قائمة الجيوش الميدانية. اختر جيشاً لإعطاء أوامر.");

            foreach (var army in state.Armies)
            {
                AddActionButton($"{army.Name} (القوة: {army.TotalSoldiers}) في {army.CurrentProvince}", (s, evt) => {
                    ClearDynamicPanel();
                    SetNarrativeText($"تقرير {army.Name}:\nالقائد: {army.CommanderName}\nالعدد: {army.TotalSoldiers}\nالموقع: {army.CurrentProvince}\nالمعنويات: {army.Morale}\nالمؤونة: {army.Supply}");
                    
                    AddActionButton("إرسال إلى مقاطعة", (ns, ne) => {
                        ClearDynamicPanel();
                        SetNarrativeText("اختر الوجهة:");
                        foreach(var prov in state.Provinces)
                        {
                            if (prov.Name != army.CurrentProvince)
                            {
                                AddActionButton($"إلى {prov.Name}", (nns, nne) => {
                                    var res = ArmyCommandSystem.SendArmy(state, army.Id, prov.Name);
                                    HandleActionResult(res, () => ShowArmyCommandMenu(null, null));
                                });
                            }
                        }
                        foreach(var prov in state.Neighbors.SelectMany(n => n.ClaimableProvinces))
                        {
                            AddActionButton($"حملة على {prov.Name} (خارج المملكة)", (nns, nne) => {
                                var res = ArmyCommandSystem.SendArmy(state, army.Id, prov.Name);
                                HandleActionResult(res, () => ShowArmyCommandMenu(null, null));
                            });
                        }
                        AddActionButton("إلغاء", (nns, nne) => ShowArmyCommandMenu(null, null));
                    });

                    AddActionButton("عودة للقائمة العسكرية", (ns, ne) => ShowArmyCommandMenu(null, null));
                });
            }

            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowResourceManagement(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_resources";
            audio.StopAmbient();
            audio.Play("ambient_nature");
            audio.PlayPaper();
            
            int fmBudget = state.MinisterBudgets != null && state.MinisterBudgets.ContainsKey("first_minister") ? state.MinisterBudgets["first_minister"] : 0;
            int stBudget = state.MinisterBudgets != null && state.MinisterBudgets.ContainsKey("steward") ? state.MinisterBudgets["steward"] : 0;
            int maBudget = state.MinisterBudgets != null && state.MinisterBudgets.ContainsKey("marshal") ? state.MinisterBudgets["marshal"] : 0;
            int spBudget = state.MinisterBudgets != null && state.MinisterBudgets.ContainsKey("spymaster") ? state.MinisterBudgets["spymaster"] : 0;
            
            string info = $"إدارة الموارد واقتصاد الدولة\n" +
                          $"الذهب الحالي: {state.Gold} | الفضة الحالية: {state.SilverCoins}\n\n" +
                          $"ميزانيات الوزراء الحالية (نسبة من الدخل الشهري):\n" +
                          $"- الوزير الأول: {fmBudget}% (تدار من المجلس الاستشاري)\n" +
                          $"- وزير المالية: {stBudget}%\n" +
                          $"- قائد الجند: {maBudget}%\n" +
                          $"- مدير الاستخبارات: {spBudget}%\n";
            SetNarrativeText(info);

            AddActionButton("تعديل ميزانية الوزراء", (s, evt) => ShowMinisterBudgetsMenu());
            AddActionButton("التجارة بالفضة (شراء موارد وتجارة)", (s, evt) => ShowSilverTradeMenu());
            AddActionButton("تطوير البنية التحتية والمباني", (s, evt) => ShowInfrastructureMenu());
            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowMinisterBudgetsMenu()
        {
            ClearDynamicPanel();
            SetNarrativeText("اختر الوزير الذي تود تعديل ميزانيته الشهرية:");
            
            AddActionButton("ميزانية وزير المالية", (s, evt) => ShowBudgetOptions("steward", "وزير المالية"));
            AddActionButton("ميزانية قائد الجند", (s, evt) => ShowBudgetOptions("marshal", "قائد الجند"));
            AddActionButton("ميزانية مدير الاستخبارات", (s, evt) => ShowBudgetOptions("spymaster", "مدير الاستخبارات"));
            AddActionButton("العودة لإدارة الموارد", (s, evt) => ShowResourceManagement(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowBudgetOptions(string ministerKey, string ministerName)
        {
            ClearDynamicPanel();
            SetNarrativeText($"تحديد ميزانية {ministerName} (يتم خصمها من دخل المقاطعات شهرياً):");
            
            int[] percentages = { 0, 10, 25, 50 };
            foreach (int p in percentages)
            {
                AddActionButton($"تحديد الميزانية إلى {p}%", (s, evt) => {
                    if (state.MinisterBudgets == null) state.MinisterBudgets = new Dictionary<string, int>();
                    state.MinisterBudgets[ministerKey] = p;
                    audio.PlaySuccess();
                    ShowResourceManagement(null, null);
                });
            }
            AddActionButton("إلغاء", (s, evt) => ShowMinisterBudgetsMenu());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowSilverTradeMenu()
        {
            ClearDynamicPanel();
            SetNarrativeText($"التجارة بالفضة\nرصيد الفضة: {state.SilverCoins}\nاختر نوع التجارة:");

            AddActionButton("شراء مواد غذائية (50 فضة = 200 طعام)", (s, evt) => ExecuteSilverTrade("food", 50, 200));
            AddActionButton("شراء أسلحة ومعدات (100 فضة = 50 قوة عسكرية إضافية)", (s, evt) => ExecuteSilverTrade("weapons", 100, 50));
            AddActionButton("تمويل المرتزقة (150 فضة = 300 جندي إضافي)", (s, evt) => ExecuteSilverTrade("mercenaries", 150, 300));
            AddActionButton("إرسال قافلة تجارية للبيزنطيين (تكلف 200 فضة، تزيد العلاقات والذهب)", (s, evt) => ExecuteSilverTrade("byzantine_trade", 200, 0));
            AddActionButton("العودة لإدارة الموارد", (s, evt) => ShowResourceManagement(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ExecuteSilverTrade(string type, int cost, int amount)
        {
            if (state.SilverCoins < cost)
            {
                audio.PlayError();
                MessageBox.Show("لا تملك ما يكفي من الفضة!", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            state.SilverCoins -= cost;
            string result = "";
            switch (type)
            {
                case "food":
                    state.Food += amount;
                    result = $"تم شراء {amount} من المواد الغذائية بنجاح.";
                    break;
                case "weapons":
                    state.Army += amount; 
                    result = $"تم استيراد معدات وأسلحة لتجهيز {amount} جندي إضافي.";
                    break;
                case "mercenaries":
                    state.Army += amount;
                    result = $"تم تمويل وتوظيف {amount} من المرتزقة بنجاح.";
                    break;
                case "byzantine_trade":
                    state.Gold += 300;
                    var byz = state.Neighbors.Find(n => n.Name.Contains("البيزنطية"));
                    if (byz != null) byz.Opinion += 10;
                    result = "عادت القافلة من الدولة البيزنطية بأرباح ذهبية، وتحسنت العلاقات معهم.";
                    break;
            }
            audio.Play("coin");
            MessageBox.Show(result, "تمت التجارة", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ShowSilverTradeMenu();
        }

        
        private void ShowDisasterMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_disaster";
            audio.StopAmbient();
            audio.Play("ambient_storm", true, true);
            audio.PlayPaper();
            SetNarrativeText("قائمة الأزمات والكوارث الحالية في المقاطعات:");
            
            foreach(var d in state.ActiveDisasters)
            {
                AddActionButton($"{d.Name} في {d.ProvinceName} (متبقي {d.DaysRemaining} يوماً)", (s, evt) => {
                    ClearDynamicPanel();
                    SetNarrativeText($"تفاصيل الكارثة: {d.Name}\nالمقاطعة المتضررة: {d.ProvinceName}\nالأيام المتبقية: {d.DaysRemaining}\n\nتأثيرها: تقلل الدخل والرضا. يمكنك إرسال إغاثة عاجلة لتقليل مدتها.");
                    
                    AddActionButton("إرسال إغاثة عاجلة (100 ذهب)", (ns, ne) => {
                        var res = KingdomBlind_CSharp.Systems.DisasterSystem.ProvideRelief(state, d.Id, 100);
                        HandleActionResult(res, () => ShowDisasterMenu());
                    });
                    
                    AddActionButton("العودة لقائمة الكوارث", (ns, ne) => ShowDisasterMenu());
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }
            AddActionButton("العودة لإدارة الموارد", (s, evt) => ShowResourceManagement(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

private void ShowInfrastructureMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_infrastructure";
            SetNarrativeText("اختر المقاطعة التي تود تطويرها:");

            string BuildOrUpgradeLabel(string buildingName, int currentLevel, int cost, string benefit)
            {
                if (currentLevel <= 0)
                    return $"بناء {buildingName} جديد ({cost} ذهب) - {benefit}";

                return $"ترقية {buildingName} من المستوى {currentLevel} إلى المستوى {currentLevel + 1} ({cost} ذهب) - {benefit}";
            }

            for (int i = 0; i < state.Provinces.Count; i++)
            {
                int index = i;
                AddActionButton($"تطوير {state.Provinces[i].Name}", (s, evt) => {
                    ClearDynamicPanel();
                    var prov = state.Provinces[index];
                    
                    int marketLevel = prov.Buildings.FirstOrDefault(b => b.BuildingType == "سوق")?.Level ?? 0;
                    int barracksLevel = prov.Buildings.FirstOrDefault(b => b.BuildingType == "ثكنة")?.Level ?? 0;
                    int farmLevel = prov.Buildings.FirstOrDefault(b => b.BuildingType == "مزرعة")?.Level ?? 0;
                    int mineLevel = prov.Buildings.FirstOrDefault(b => b.BuildingType == "منجم")?.Level ?? 0;

                    int marketCost = 100 + (marketLevel * 50);
                    int barracksCost = 150 + (barracksLevel * 50);
                    int farmCost = 100 + (farmLevel * 30);
                    int mineCost = 200 + (mineLevel * 100);

                    SetNarrativeText($"ماذا تود أن تبني أو ترقي في {prov.Name}؟");
                    
                    AddActionButton($"تعبئة جيش ميداني (يستهلك 200 من الحامية)", (ns, ne) => {
                        var res = EconomySystem.MobilizeArmy(state, index);
                        HandleActionResult(res, ShowInfrastructureMenu);
                    });
                    
                    if (marketLevel < 20)
                    {
                        AddActionButton(BuildOrUpgradeLabel("سوق", marketLevel, marketCost, "زيادة الدخل التجاري"), (ns, ne) => {
                            var res = EconomySystem.UpgradeBuilding(state, index, "سوق");
                            HandleActionResult(res, ShowInfrastructureMenu);
                        });
                    }
                    
                    if (barracksLevel < 30)
                    {
                        AddActionButton(BuildOrUpgradeLabel("ثكنة", barracksLevel, barracksCost, "زيادة الجيش والحامية"), (ns, ne) => {
                            var res = EconomySystem.UpgradeBuilding(state, index, "ثكنة");
                            HandleActionResult(res, ShowInfrastructureMenu);
                        });
                    }
                    
                    if (farmLevel < 20)
                    {
                        AddActionButton(BuildOrUpgradeLabel("مزرعة", farmLevel, farmCost, "زيادة المؤونة"), (ns, ne) => {
                            var res = EconomySystem.UpgradeBuilding(state, index, "مزرعة");
                            HandleActionResult(res, ShowInfrastructureMenu);
                        });
                    }

                    if (mineLevel < 10)
                    {
                        AddActionButton(BuildOrUpgradeLabel("منجم", mineLevel, mineCost, "زيادة الفضة والدخل"), (ns, ne) => {
                            var res = EconomySystem.UpgradeBuilding(state, index, "منجم");
                            HandleActionResult(res, ShowInfrastructureMenu);
                        });
                    }

                    AddActionButton("تعديل الضرائب", (ns, ne) => {
                        ClearDynamicPanel();
                        SetNarrativeText("اختر مستوى الضرائب للمملكة:");
                        AddActionButton("منخفض (زيادة رضا، تقليل دخل)", (nns, nne) => HandleActionResult(EconomySystem.SetTaxLevel(state, "منخفض"), ShowInfrastructureMenu));
                        AddActionButton("متوسط (استقرار)", (nns, nne) => HandleActionResult(EconomySystem.SetTaxLevel(state, "متوسط"), ShowInfrastructureMenu));
                        AddActionButton("مرتفع (نقص رضا، زيادة دخل)", (nns, nne) => HandleActionResult(EconomySystem.SetTaxLevel(state, "مرتفع"), ShowInfrastructureMenu));
                        AddActionButton("عودة", (nns, nne) => ShowInfrastructureMenu());
                        if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                    });
                    
                    AddActionButton("عودة", (ns, ne) => ShowInfrastructureMenu());
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }
            AddActionButton("العودة للإدارة", (s, evt) => ShowResourceManagement(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowPoliticalAffairsMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_politics";
            audio.StopAmbient();
            audio.Play("ambient_council", true, true);
            audio.PlayPaper();
            SetNarrativeText("الشؤون السياسية والتمرد الداخلي.\nراقب ولاة المقاطعات وطموحهم، وتعامل مع الفصائل التي قد تتمرد عليك.");

            AddActionButton("إدارة الولاة والنبلاء 🎩", ShowGovernorsMenu);
            
            int activeFactions = state.Factions.Count(f => f.IsActive);
            AddActionButton($"الفصائل والتمرد ⚔️ (يوجد {activeFactions} فصائل نشطة)", ShowFactionsMenu);
            
            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowGovernorsMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            SetNarrativeText("قائمة ولاة المقاطعات. استعرض تقاريرهم وتخذ الإجراءات المناسبة.");

            foreach (var gov in state.Governors)
            {
                string warning = (gov.CurrentMood == "Rebellious" || gov.CurrentMood == "Opportunist") ? " ⚠️ خطير" : "";
                AddActionButton($"الوالي {gov.Name} (حاكم {gov.ProvinceName}) - {PoliticalSystem.GetArabicMood(gov.CurrentMood)}{warning}", (s, evt) => {
                    ClearDynamicPanel();
                    string report = $"والي {gov.ProvinceName}: {gov.Name}.\n" +
                                    $"الولاء: {gov.Loyalty} من 100.\n" +
                                    $"الرأي بالملك: {gov.OpinionOfKing}.\n" +
                                    $"الطموح: {gov.Ambition}.\n" +
                                    $"الخوف: {gov.Fear}.\n" +
                                    $"النفوذ السياسي: {gov.Influence}.\n" +
                                    $"القوة العسكرية المحلية: {gov.MilitaryPower}.\n" +
                                    $"الموقف: {PoliticalSystem.GetArabicMood(gov.CurrentMood)}.";
                    SetNarrativeText(report);

                    AddActionButton("منح شرف أو لقب (+ولاء و نفوذ، يستهلك هيبة)", (ns, ne) => {
                        var res = PoliticalSystem.GrantTitle(state, gov.Id);
                        HandleActionResult(res, () => ShowGovernorsMenu(null, null));
                    });
                    AddActionButton("إرسال رشوة سياسية (200 ذهب)", (ns, ne) => {
                        var res = PoliticalSystem.SendBribe(state, gov.Id);
                        HandleActionResult(res, () => ShowGovernorsMenu(null, null));
                    });
                    AddActionButton("تهديد الوالي بالقوة (+خوف، -رأي)", (ns, ne) => {
                        var res = PoliticalSystem.Threaten(state, gov.Id);
                        HandleActionResult(res, () => ShowGovernorsMenu(null, null));
                    });
                    AddActionButton("عزل من المنصب ⚠️ (يغضب الآخرين)", (ns, ne) => {
                        var res = PoliticalSystem.DismissGovernor(state, gov.Id);
                        HandleActionResult(res, () => ShowGovernorsMenu(null, null));
                    });
                    
                    AddActionButton("العودة لقائمة الولاة", (ns, ne) => ShowGovernorsMenu(null, null));
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }

            AddActionButton("العودة للشؤون السياسية", ShowPoliticalAffairsMenu);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowFactionsMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            SetNarrativeText("قائمة الفصائل السياسية في المملكة.");

            var activeFactions = state.Factions.Where(f => f.IsActive).ToList();
            var rebelArmies = state.EnemyArmies.Where(a => (a.Id ?? "").StartsWith("rebel_")).ToList();
            if (activeFactions.Count == 0 && rebelArmies.Count == 0)
            {
                SetNarrativeText("لا توجد فصائل نشطة تهدد استقرار المملكة حالياً.");
            }

            foreach (var rebelArmy in rebelArmies)
            {
                AddActionButton($"تمرد مسلح في {rebelArmy.CurrentProvince} ({rebelArmy.TotalSoldiers} مقاتل)", (s, evt) => {
                    ClearDynamicPanel();
                    SetNarrativeText($"تمرد مسلح في {rebelArmy.CurrentProvince}.\nالقائد: {rebelArmy.CommanderName}\nالقوة: {rebelArmy.TotalSoldiers} مقاتل.\nيمكن إنهاؤه بالقمع إذا كان جيش ملكي في المقاطعة، أو بالعفو والتفاوض.");

                    AddActionButton("قمع التمرد عسكرياً", (ns, ne) => {
                        var res = WarfareSystem.SuppressRebellion(state, rebelArmy.Id);
                        HandleActionResult(res, () => ShowFactionsMenu(null, null));
                    });

                    AddActionButton("عفو وتفاوض (100 ذهب، 25 هيبة)", (ns, ne) => {
                        var res = WarfareSystem.NegotiateRebellion(state, rebelArmy.Id);
                        HandleActionResult(res, () => ShowFactionsMenu(null, null));
                    });

                    AddActionButton("العودة للفصائل", (ns, ne) => ShowFactionsMenu(null, null));
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }

            foreach (var faction in activeFactions)
            {
                var leader = state.Governors.FirstOrDefault(g => g.Id == faction.LeaderGovernorId);
                string leaderName = leader != null ? leader.Name : "غير معروف";
                string danger = faction.PowerPercent > 50 ? " ⚠️ خطر عالٍ" : "";
                
                AddActionButton($"{faction.Name}{danger}", (s, evt) => {
                    ClearDynamicPanel();
                    string report = $"{faction.Name}.\n" +
                                    $"القائد: {leaderName}.\n" +
                                    $"عدد الأعضاء: {faction.MemberGovernorIds.Count} ولاة.\n" +
                                    $"قوة الفصيل: {faction.PowerPercent} بالمئة.\n" +
                                    $"مستوى السخط: {faction.Discontent} من 100.\n" +
                                    $"المطلب الرئيسي: {faction.DemandText}.\n" +
                                    $"السبب الرئيسي: {faction.MainReason}.\n";
                    if (faction.DaysUntilUltimatum > 0)
                        report += $"تبقى {faction.DaysUntilUltimatum} يوماً على المهلة النهائية.";
                    
                    SetNarrativeText(report);
                    
                    if (faction.DaysUntilUltimatum > 0)
                    {
                        AddActionButton("التعامل مع الإنذار النهائي", (ns, ne) => ShowUltimatumOptions(faction));
                    }
                    else
                    {
                        AddActionButton("دفع رشوة للقائد لتخفيف السخط (500 ذهب)", (ns, ne) => {
                            var res = FactionSystem.HandleUltimatum(state, faction.Id, "Bribe");
                            HandleActionResult(res, () => ShowFactionsMenu(null, null));
                        });
                    }
                    
                    AddActionButton("العودة للفصائل", (ns, ne) => ShowFactionsMenu(null, null));
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }

            AddActionButton("العودة للشؤون السياسية", ShowPoliticalAffairsMenu);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowUltimatumOptions(Faction faction)
        {
            ClearDynamicPanel();
            SetNarrativeText($"إنذار نهائي من {faction.Name}!\nالمطلب: {faction.DemandText}\nإذا رفضت أو انتهت المهلة ({faction.DaysUntilUltimatum} أيام) سيبدأ التمرد المسلح.");
            
            AddActionButton("قبول المطلب (انخفاض الهيبة)", (s, e) => {
                var res = FactionSystem.HandleUltimatum(state, faction.Id, "Accept");
                HandleActionResult(res, () => ShowFactionsMenu(null, null));
            });
            AddActionButton("رفض المطلب بغضب (بدء التمرد فوراً)", (s, e) => {
                var res = FactionSystem.HandleUltimatum(state, faction.Id, "Reject");
                HandleActionResult(res, () => RenderSandboxButtons());
            });
            AddActionButton("دفع رشوة وتأجيل الإنذار (500 ذهب)", (s, e) => {
                var res = FactionSystem.HandleUltimatum(state, faction.Id, "Bribe");
                HandleActionResult(res, () => ShowFactionsMenu(null, null));
            });
            
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void UpdateUI()
        {
            if (state.GameMode.StartsWith("sandbox"))
            {
                statusLabel.Text = $"الدور {state.Turn} | الذهب: {state.Gold} | المؤونة: {state.Food} | الجيش: {(state.Armies != null ? state.Armies.Sum(a => a.TotalSoldiers) : 0)} | السعادة: {state.Satisfaction} | الهيبة: {state.Prestige}";
            }
        }

        private void PlaySuperTonic(string text, string characterLabel = "")
        {
            EnsureAiConfig();
            if(ShouldUseSuperTonicForCharacter(characterLabel))
            {
                IsSuperTonicSpeaking = true;
                narration.Speak(state, text, isNpcDialog: true, interrupt: false);
                IsSuperTonicSpeaking = false;
            }
        }

        private bool ShouldUseSuperTonicForCharacter(string characterLabel)
        {
            EnsureAiConfig();
            return state.UseSuperTonic && AiRuntimePolicySystem.IsEnabledForLabel(config.AiActors, characterLabel);
        }

        // --- SANDBOX ACTIONS ---
        
        private void ActionTaxes(object sender, EventArgs e)
        {
            audio.Play("coin");
            state.Gold += 150;
            state.Satisfaction = Math.Max(0, state.Satisfaction - 15);
            SetNarrativeText("تم جمع الضرائب بنجاح. زاد ذهب المملكة بمقدار 150، ولكن انخفض رضا الشعب.");
            RenderSandboxButtons();
            UpdateUI();
        }

        private void ActionFood(object sender, EventArgs e)
        {
            audio.Play("coin");
            state.Food -= 100;
            state.Satisfaction = Math.Min(100, state.Satisfaction + 20);
            SetNarrativeText("تم توزيع المؤن على الفقراء والمزارعين. تحسن رضا الشعب بشكل ملحوظ.");
            RenderSandboxButtons();
            UpdateUI();
        }

        private void ActionArmy(object sender, EventArgs e)
        {
            audio.Play("sword");
            state.Gold -= 120;
            if (state.Armies.Count > 0) state.Armies[0].TotalSoldiers += 25;
            SetNarrativeText("تم تدريب وتجهيز 25 فارساً جديداً. زادت قوة جيشك الدفاعية بتكلفة 120 قطع ذهبية.");
            RenderSandboxButtons();
            UpdateUI();
        }

        private void ShowPoliticalMap(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_map";
            audio.StopAmbient();
            audio.Play("ambient_nature", true, true);
            audio.PlayPaper();

            string mapInfo = "الخريطة السياسية للمنطقة\n\nالمقاطعات التابعة لك:\n";
            foreach (var p in state.Provinces)
            {
                if (p.Occupied)
                    mapInfo += $"- {p.Name} (محتلة من قبل {p.OccupiedBy}! الوالي المخلوع: {p.Vassal})\n";
                else
                    mapInfo += $"- {p.Name} (الوالي: {p.Vassal}، الدخل: {p.Income} ذهب، الرضا: {p.Satisfaction}%، الولاء: {p.Opinion}، الديانة: {p.Religion})\n";
            }

            if (state.ActiveWar != null)
                mapInfo += $"\nحصار نشط: {state.ActiveWar.TargetProvince}، الحامية المتبقية: {state.SiegeData?.TargetGarrison ?? state.ActiveWar.Garrison}.\n";

            mapInfo += "\nالدول المجاورة تدار من شاشة الدبلوماسية المتقدمة فقط.";
            SetNarrativeText(mapInfo);

            if (state.ActiveWar != null)
            {
                AddActionButton($"إدارة حصار {state.ActiveWar.TargetProvince} ⚔️", (s, evt) => ShowSiegeMenu());
            }

            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        
        private void ShowAnnexationMenu(int neighborIndex)
        {
            ClearDynamicPanel();
            var n = state.Neighbors[neighborIndex];
            state.GameMode = "conquest_complete";
            audio.StopAmbient();
            audio.Play("ambient_magic", true, true);
            
            SetNarrativeText($"لقد سقطت آخر معاقل {n.Name} وانتهت المقاومة بالكامل!\nكيف تود التعامل مع هذه الدولة المنهزمة؟");
            
            AddActionButton("ضم بالكامل (دمج المقاطعات وإعدام الحاكم)", (s, evt) => {
                n.Relation = "مضمومة";
                n.Army = 0;
                n.Opinion = -100;
                state.Prestige += 50;
                audio.Play("sword");
                HandleActionResult(new GameActionResult { Success=true, MainMessage=$"تم ضم {n.Name} بالكامل إلى مملكتك وتم إعدام قادتهم!" }, RenderSandboxButtons);
            });
            
            AddActionButton("فرض الولاء والخراج (دولة تابعة)", (s, evt) => {
                ShowTributeSelectionMenu(neighborIndex);
            });
            
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowTributeSelectionMenu(int neighborIndex)
        {
            ClearDynamicPanel();
            var n = state.Neighbors[neighborIndex];
            SetNarrativeText("حدد نسبة الخراج السنوي الذي سيدفعه الحاكم التابع من دخله (25% إلى 70%):");
            
            AddActionButton("خراج خفيف (25%)", (s, evt) => ImposeVassalage(neighborIndex, 25));
            AddActionButton("خراج متوسط (50%)", (s, evt) => ImposeVassalage(neighborIndex, 50));
            AddActionButton("خراج ثقيل (70%)", (s, evt) => ImposeVassalage(neighborIndex, 70));
            
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ImposeVassalage(int neighborIndex, int tributePercent)
        {
            var n = state.Neighbors[neighborIndex];
            n.Relation = "تابع";
            n.TributePercent = tributePercent;
            n.Opinion = (100 - tributePercent); // High tribute lowers opinion
            state.Prestige += 20;
            audio.Play("coin");
            HandleActionResult(new GameActionResult { Success=true, MainMessage=$"أقسم حاكم {n.Name} الولاء لك كدولة تابعة، وسيدفع خراجاً بنسبة {tributePercent}% من دخله!" }, RenderSandboxButtons);
        }

        private void ShowSiegeMenu()
        {
            if (state.ActiveWar == null) return;
            ClearDynamicPanel();
            state.GameMode = "sandbox_siege";
            
            SetNarrativeText($"أنت تقوم بحصار {state.ActiveWar.TargetProvince}. حامية العدو: {state.SiegeData.TargetGarrison}، نتيجة الحرب: {state.ActiveWar.WarScore}.\n(يتقدم الحصار تلقائياً مع الزمن)");
            
            AddActionButton("اقتحام الأسوار فوراً (خسائر فادحة وحسم سريع)", (s, evt) => {
                var res = WarfareSystem.ProcessSiegeCommand(state, "اقتحام");
                HandleActionResult(res, () => { if (state.ActiveWar != null) ShowSiegeMenu(); else RenderSandboxButtons(); });
            });
            
            AddActionButton("انسحاب تكتيكي وفك الحصار (هزيمة)", (s, evt) => {
                var res = WarfareSystem.ProcessSiegeCommand(state, "انسحاب");
                HandleActionResult(res, RenderSandboxButtons);
            });

            AddActionButton("فرض المطالب إذا كانت نتيجة الحرب كافية", (s, evt) => {
                var res = WarfareSystem.NegotiatePeace(state, "EnforceDemands");
                HandleActionResult(res, () => { if (state.ActiveWar != null) ShowSiegeMenu(); else RenderSandboxButtons(); });
            });

            AddActionButton("اقتراح صلح أبيض", (s, evt) => {
                var res = WarfareSystem.NegotiatePeace(state, "WhitePeace");
                HandleActionResult(res, () => { if (state.ActiveWar != null) ShowSiegeMenu(); else RenderSandboxButtons(); });
            });

            AddActionButton("دفع تعويضات لإنهاء الحرب", (s, evt) => {
                var res = WarfareSystem.NegotiatePeace(state, "PayReparations");
                HandleActionResult(res, () => { if (state.ActiveWar != null) ShowSiegeMenu(); else RenderSandboxButtons(); });
            });

            AddActionButton("العودة للخريطة", (s, evt) => ShowPoliticalMap(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void InteractNeighbor(int neighborIdx)
        {
            ClearDynamicPanel();
            state.GameMode = "diplomacy_neighbor";
            
            SetNarrativeText(Systems.DiplomacySystem.GetNeighborInfo(state, neighborIdx));
            var n = state.Neighbors[neighborIdx];

            AddActionButton($"إرسال مبعوث دبلوماسي (50 ذهب) لتحسين العلاقة", (s, e) => {
                string res = Systems.DiplomacySystem.SendEnvoy(state, neighborIdx);
                SetNarrativeText(res);
            });

            if (!n.TradeTreaty)
            {
                AddActionButton($"اقتراح معاهدة تجارية (يتطلب 25 رأي إيجابي)", (s, e) => {
                    string res = Systems.DiplomacySystem.OfferTrade(state, neighborIdx);
                    SetNarrativeText(res);
                });
            }
            else
            {
                AddActionButton($"إلغاء المعاهدة التجارية الحالية", (s, e) => {
                    string res = Systems.DiplomacySystem.CancelTrade(state, neighborIdx);
                    SetNarrativeText(res);
                });
            }

            if (!n.HasClaim && n.ClaimableProvinces != null && n.ClaimableProvinces.Count > 0)
            {
                AddActionButton($"تزوير مطالبة شرعية للغزو (100 ذهب, 15 هيبة)", (s, e) => {
                    ClearDynamicPanel();
                    SetNarrativeText($"أرسل جواسيسك لتزوير صكوك ملكية لإحدى مقاطعات {n.Name}");
                    foreach(var p in n.ClaimableProvinces)
                    {
                        AddActionButton($"تزوير مطالبة على مقاطعة {p.Name} ({p.Religion})", (ns, ne) => {
                            var res = IntrigueSystem.ForgeClaim(state, neighborIdx, p.Name);
                            HandleActionResult(res, () => InteractNeighbor(neighborIdx));
                        });
                    }
                    AddActionButton("إلغاء الأمر", (ns, ne) => InteractNeighbor(neighborIdx));
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }

            if (n.HasClaim && n.Relation != "حرب")
            {
                AddActionButton($"إعلان الحرب للسيطرة على {n.ClaimedProvince} ⚔️", (s, e) => {
                    var res = WarfareSystem.DeclareWar(state, neighborIdx, false);
                    HandleActionResult(res, () => ShowPoliticalMap(null, null));
                });
            }
            else if (n.Relation != "حرب")
            {
                AddActionButton($"إعلان حرب ظالمة (غزو بدون مطالبة شرعية) ⚔️", (s, e) => {
                    var res = WarfareSystem.DeclareWar(state, neighborIdx, true);
                    HandleActionResult(res, () => ShowPoliticalMap(null, null));
                });
            }

            if (!n.Alliance && n.Opinion >= 0)
            {
                AddActionButton("طلب زواج دبلوماسي لإنشاء تحالف (50 هيبة)", (s, e) => {
                    var res = DynastySystem.ArrangeMarriage(state, neighborIdx);
                    HandleActionResult(res, () => InteractNeighbor(neighborIdx));
                });
            }

            AddActionButton($"محاولة اغتيال {n.Ruler} (150 ذهب) 🗡️", (s, e) => {
                var res = IntrigueSystem.AttemptAssassination(state, neighborIdx);
                HandleActionResult(res, () => InteractNeighbor(neighborIdx));
            });

            AddActionButton("عودة للخريطة السياسية", ShowPoliticalMap);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        // --- CONSULTATIONS ---

        private void ConsultQueen(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_consult";
            
            string spouse = string.IsNullOrEmpty(state.SpouseName) ? "الملكة" : state.SpouseName;
            state.QueenHappiness = Math.Min(100, state.QueenHappiness + 5);
            
            string dialog = "";
            if (state.Satisfaction < 40)
                dialog = "مولاي العزيز، الشعب غاضب والأسواق تشتكي. أرجوك قم بتوزيع المؤن لتهدئتهم قبل أن تندلع ثورة في شوارع العاصمة!";
            else if (state.Prestige < 50)
                dialog = "حبيبي الملك، هيبتنا بين الأمم تتراجع. يجب أن تقيم احتفالاً أو ترسل المبعوثين لإظهار قوتنا أمام الممالك المجاورة.";
            else
                dialog = "مولاي، المملكة في سلام واستقرار. رعايانا يحبونك، والقصور عامرة بالخيرات. استمر في حكمك العادل واهتمامك بالرعية.";
                
            bool useSuperTonic = ShouldUseSuperTonicForCharacter(spouse);
            SetNarrativeText($"{spouse}:\n\n\"{dialog}\"", speak: !useSuperTonic, isNpcDialog: true);
            PlaySuperTonic(dialog, spouse);

            AddActionButton($"تقديم هدية ثمينة لـ {spouse} (-50 ذهب، +20 سعادة)", (s, evt) => {
                if (state.Gold >= 50) {
                    state.Gold -= 50; state.QueenHappiness = Math.Min(100, state.QueenHappiness + 20);
                    SetNarrativeText($"قدمت هدية لـ {spouse}. زادت سعادتها إلى {state.QueenHappiness}% ودفعت 50 ذهبية.");
                    ShowRoyalPalace(null, null);
                } else {
                    SetNarrativeText("لا تملك الذهب الكافي لتقديم هدية!");
                }
            });
            AddActionButton("العودة للقصر الحاكم", (s, evt) => ShowRoyalPalace(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ConsultVizier(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_consult";
            var c = state.Council["steward"];
            
            string dialog = "";
            if (state.Gold < 200)
                dialog = "مولاي الملك، بيت مال المسلمين يعاني من نقص حاد! خزائننا تكاد تفرغ. أقترح عليك جمع الضرائب فوراً أو تكليفي بالجباية من المقاطعات.";
            else if (state.Food < 200)
                dialog = "الجفاف يهددنا يا مولاي! مخازن الغلال شبه فارغة. يجب فرض قانون زراعي سريعاً لزيادة المؤونة وإلا هلكت الرعية.";
            else
                dialog = "خزائننا عامرة ومخازننا ممتلئة بفضل الله ثم بحكمتكم. الاقتصاد في أزهى عصوره.";
            
            bool useSuperTonic = ShouldUseSuperTonicForCharacter(c.Title);
            SetNarrativeText($"الوزير {c.Name}:\n\n\"{dialog}\"", speak: !useSuperTonic, isNpcDialog: true);
            PlaySuperTonic(dialog, c.Title);

            AddActionButton("فرض قانون زراعي سريع لتوليد المؤونة (+150 مؤونة، -50 ذهب)", (s, evt) => {
                if (state.Gold >= 50) {
                    state.Gold -= 50; state.Food += 150;
                    SetNarrativeText("فرضت قانوناً زراعياً جديداً. زادت مؤونة المملكة بمقدار 150.");
                    ManageCouncilor("steward");
                } else {
                    SetNarrativeText("لا تملك الذهب الكافي!");
                }
            });
            AddActionButton("العودة لخيارات المستشار", (s, evt) => ManageCouncilor("steward"));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ConsultCommander(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_consult";
            var c = state.Council["marshal"];
            
            string dialog = "";
            if (state.Army < 300)
                dialog = "أيها الملك المهاب، أعداد الجيش متدنية جداً! نحن مكشوفون أمام الأعداء. أرجوك اسمح لي بتمويل حملات تطويع جديدة فوراً لإنقاذ الموقف.";
            else
                dialog = "سيوفنا مشحوذة وخيولنا مسرجة يا مولاي! الجيش في أتم الجاهزية للدفاع عن المملكة أو تسيير الفتوحات متى ما أمرت.";
            
            bool useSuperTonic = ShouldUseSuperTonicForCharacter(c.Title);
            SetNarrativeText($"القائد {c.Name}:\n\n\"{dialog}\"", speak: !useSuperTonic, isNpcDialog: true);
            PlaySuperTonic(dialog, c.Title);

            AddActionButton("تمويل دوريات وتجنيد طارئ (-80 ذهب، +20 جيش)", (s, evt) => {
                if (state.Gold >= 80) {
                    state.Gold -= 80; state.Army += 20;
                    SetNarrativeText("تم تمويل الدوريات الحدودية وحملة التجنيد. زاد الجيش بمقدار 20.");
                    ManageCouncilor("marshal");
                } else {
                    SetNarrativeText("الذهب غير كافٍ!");
                }
            });
            AddActionButton("العودة لخيارات القائد", (s, evt) => ManageCouncilor("marshal"));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        // --- SCREENS ---

        
        private void ShowActivitiesMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_activities";
            SetNarrativeText("الأنشطة الملكية: يمكنك إقامة فعاليات لتحسين المزاج والعلاقات.");
            
            AddActionButton("إقامة مأدبة ملكية كبرى (200 ذهب، 100 مؤونة) 🍷", (s, evt) => {
                var res = KingdomBlind_CSharp.Systems.DynastySystem.HoldRoyalBanquet(state);
                HandleActionResult(res, ShowActivitiesMenu);
            });
            
            AddActionButton("رحلة صيد ملكية (150 ذهب) 🏹", (s, evt) => {
                var res = KingdomBlind_CSharp.Systems.ActivitiesSystem.HoldHuntingTrip(state);
                HandleActionResult(res, ShowActivitiesMenu);
            });
            
            AddActionButton("إقامة بطولة فروسية كبرى (400 ذهب) ⚔️", (s, evt) => {
                var res = KingdomBlind_CSharp.Systems.ActivitiesSystem.HoldGrandTournament(state);
                HandleActionResult(res, ShowActivitiesMenu);
            });
            
            AddActionButton("العودة لإدارة الأسرة الحاكمة", (s, evt) => ShowRoyalPalace(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

private void ShowRoyalPalace(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "family";
            string spouseStatus = state.Wives.Count == 0 ? "أعزب" : $"{state.Wives.Count} زوجة";
            string heirLine = "";
            if (string.IsNullOrEmpty(state.HeirName))
            {
                heirLine = "تنبيه: لا يوجد وريث للعرش حتى الآن!";
            }
            else if (state.HeirAge < 6)
            {
                heirLine = $"الوريث الحالي: {state.HeirName} (طفل - {state.HeirAge} سنة).";
            }
            else
            {
                heirLine = $"ولي العهد: {state.HeirName} ({state.HeirAge} سنة).";
            }

            string familyText = $"👑 القصر الحاكم لشؤون سلالة {state.DynastyName}:\n\nالملك: {state.RulerName} ({state.RulerAge} سنة).\nالوضع العائلي: {spouseStatus} (سعادة الملكة: {state.QueenHappiness}%).\n{heirLine}";
            
            SetNarrativeText(familyText);

            if (state.Wives.Count == 0)
            {
                AddActionButton("البحث عن زوجة وتشكيل تحالف سياسي", (s, evt) => {
                    ClearDynamicPanel();
                    SetNarrativeText("تم إرسال مبعوثين للممالك. اختر من تتزوج:");
                    foreach(var n in state.Neighbors.Where(x => x.Relation != "عدائية"))
                    {
                        AddActionButton($"الزواج من أميرة {n.Name} (تحالف دائم)", (ns, ne) => {
                            Spouse newWife = new Spouse
                            {
                                Name = $"أميرة {n.Name}",
                                OriginType = "ForeignKingdom",
                                OriginId = n.Name,
                                OpinionOfKing = 80,
                                Trust = 50
                            };
                            state.Wives.Add(newWife);
                            state.QueenHappiness = 80;
                            n.Relation = "موالية";
                            n.Opinion = 100;
                            n.Alliance = true;
                            SetNarrativeText($"تم الزواج المبارك من {newWife.Name}! أصبح هناك تحالف قوي.");
                            ShowRoyalPalace(null, null);
                        });
                    }
                    AddActionButton("إلغاء والعودة للقصر الحاكم", (ns, ne) => ShowRoyalPalace(null, null));
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }
            else
            {
                AddActionButton("جناح الملكات (الزوجات) 👑", (s, evt) => ShowWivesMenu());
            }
            
            AddActionButton("الأنشطة الملكية والفعاليات 🎭", (s, evt) => ShowActivitiesMenu());



            
            if (!string.IsNullOrEmpty(state.HeirName) && state.HeirAge >= 6)
            {
                AddActionButton("تعليم وتدريب ولي العهد 📚", (s, evt) => {
                    ClearDynamicPanel();
                    SetNarrativeText("اختر مسار التعليم لولي العهد:");
                    AddActionButton("تعليم عسكري (مهارة عسكرية)", (ns, ne) => {
                        var res = DynastySystem.EducateHeir(state, "عسكري");
                        HandleActionResult(res, () => ShowRoyalPalace(null, null));
                    });
                    AddActionButton("تعليم اقتصادي (مهارة اقتصادية)", (ns, ne) => {
                        var res = DynastySystem.EducateHeir(state, "اقتصادي");
                        HandleActionResult(res, () => ShowRoyalPalace(null, null));
                    });
                    AddActionButton("تعليم دبلوماسي (مهارة دبلوماسية)", (ns, ne) => {
                        var res = DynastySystem.EducateHeir(state, "دبلوماسي");
                        HandleActionResult(res, () => ShowRoyalPalace(null, null));
                    });
                    AddActionButton("عودة للقصر", (ns, ne) => ShowRoyalPalace(null, null));
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }
            
            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowCouncilScreen(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "council";
            
            var c_chap = state.Council["chaplain"];
            var c_stew = state.Council["steward"];
            var c_marsh = state.Council["marshal"];
            var c_spy = state.Council["spymaster"];
            var c_chan = state.Council["chancellor"];
            
            string chapTarget = !string.IsNullOrEmpty(c_chap.Target) ? $" في {c_chap.Target}" : "";
            string chapTurns = c_chap.TurnsLeft > 0 ? $" (متبقي {c_chap.TurnsLeft} دور)" : "";
            
            string stewTarget = !string.IsNullOrEmpty(c_stew.Target) ? $" في {c_stew.Target}" : "";
            string stewTurns = c_stew.TurnsLeft > 0 ? $" (متبقي {c_stew.TurnsLeft} دور)" : "";
            
            string marshTarget = !string.IsNullOrEmpty(c_marsh.Target) ? $" في {c_marsh.Target}" : "";
            string marshTurns = c_marsh.TurnsLeft > 0 ? $" (متبقي {c_marsh.TurnsLeft} دور)" : "";
            
            string spyTarget = !string.IsNullOrEmpty(c_spy.Target) ? $" في {c_spy.Target}" : "";
            string spyTurns = c_spy.TurnsLeft > 0 ? $" (متبقي {c_spy.TurnsLeft} دور)" : "";
            
            string chanTarget = !string.IsNullOrEmpty(c_chan.Target) ? $" في {c_chan.Target}" : "";
            string chanTurns = c_chan.TurnsLeft > 0 ? $" (متبقي {c_chan.TurnsLeft} دور)" : "";
            string firstMinisterLine = state.FirstMinister != null && state.FirstMinister.IsAppointed
                ? $"الوزير الأول: {state.FirstMinister.Name}\n   - المهمة الحالية: {state.FirstMinister.CurrentTask}" +
                  (state.FirstMinister.TaskDaysRemaining > 0 ? $" (متبقي {state.FirstMinister.TaskDaysRemaining} يوم)" : "") +
                  $"\n   - الميزانية الشهرية: {state.FirstMinister.MonthlyBudgetPercent}%\n\n"
                : "الوزير الأول: غير معين\n   - لا توجد إدارة عليا للدواوين بعد.\n\n";

            string councilText = $"🏛️ المجلس الاستشاري:\n\n" +
                firstMinisterLine +
                $"1. {c_chap.Title}: {c_chap.Name}\n" +
                $"   - المهمة الحالية: {c_chap.Task}{chapTarget}{chapTurns}\n\n" +
                $"2. {c_stew.Title}: {c_stew.Name}\n" +
                $"   - المهمة الحالية: {c_stew.Task}{stewTarget}{stewTurns}\n\n" +
                $"3. {c_marsh.Title}: {c_marsh.Name}\n" +
                $"   - المهمة الحالية: {c_marsh.Task}{marshTarget}{marshTurns}\n\n" +
                $"4. {c_spy.Title}: {c_spy.Name}\n" +
                $"   - المهمة الحالية: {c_spy.Task}{spyTarget}{spyTurns}\n\n" +
                $"5. {c_chan.Title}: {c_chan.Name}\n" +
                $"   - المهمة الحالية: {c_chan.Task}{chanTarget}{chanTurns}\n\n" +
                "اختر مستشاراً لإصدار تكليفات ملكية جديدة له:";
                
            SetNarrativeText(councilText);

            AddActionButton($"🏛️ تكليف {c_chap.Name}", (s, evt) => ManageCouncilor("chaplain"));
            AddActionButton($"💰 تكليف {c_stew.Name}", (s, evt) => ManageCouncilor("steward"));
            AddActionButton($"⚔️ تكليف {c_marsh.Name}", (s, evt) => ManageCouncilor("marshal"));
            AddActionButton($"👁️ تكليف {c_spy.Name}", (s, evt) => ManageCouncilor("spymaster"));
            AddActionButton($"🌍 تكليف {c_chan.Name}", (s, evt) => ManageCouncilor("chancellor"));
            
            AddActionButton("إدارة الوزير الأول داخل المجلس", ShowFirstMinisterScreen);
            AddActionButton("عقد اجتماع لجميع الوزراء 🏛️", ShowCouncilMeetingMenu);
            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ManageCouncilor(string role)
        {
            ClearDynamicPanel();
            var c = state.Council[role];
            SetNarrativeText($"قائمة أوامر وتكليفات {c.Title} ({c.Name}):\nالمهمة الحالية: {c.Task}");
            
            if (role == "chaplain")
            {
                AddActionButton($"استشارة {c.Name} في شؤون الولاة 📜", (s, evt) => ConsultPolitics(role));
                AddActionButton("توطيد العلاقات الدينية (+10 تقوى ورضا)", (s, evt) => AssignTaskProvince(role, "توطيد العلاقات الدينية"));
                AddActionButton("نشر عقيدة البلاط (-50 ذهب)", (s, evt) => AssignTaskProvince(role, "نشر عقيدة البلاط", 50));
            }
            else if (role == "steward")
            {
                AddActionButton($"استشارة المستشار {c.Name} في شؤون الاقتصاد ⚖️", ConsultVizier);
                AddActionButton($"استشارة المستشار {c.Name} في شؤون الولاة والتمرد 💰", (s, evt) => ConsultPolitics(role));
                AddActionButton("جمع الضرائب (+25% ضرائب إضافية)", (s, evt) => AssignTaskInstant(role, "جمع الضرائب"));
                AddActionButton("إعمار الأراضي (-120 ذهب)", (s, evt) => AssignTaskProvince(role, "إعمار الأراضي", 120));
            }
            else if (role == "marshal")
            {
                AddActionButton($"استشارة القائد {c.Name} في شؤون الجندية ⚔️", ConsultCommander);
                AddActionButton($"استشارة القائد {c.Name} في شؤون الولاة والتمرد 🛡️", (s, evt) => ConsultPolitics(role));
                AddActionButton("حشد العسكر (+15 جيش)", (s, evt) => AssignTaskInstant(role, "حشد العسكر"));
                AddActionButton("تعزيز الحاميات", (s, evt) => AssignTaskProvince(role, "تعزيز الحاميات"));
            }
            else if (role == "spymaster")
            {
                AddActionButton($"استشارة {c.Name} في شؤون الولاة والتمرد 👁️", (s, evt) => ConsultPolitics(role));
                AddActionButton("كشف المؤامرات (-10 ضغط نفسي)", (s, evt) => AssignTaskInstant(role, "كشف المؤامرات"));
                AddActionButton("دعم الجواسيس", (s, evt) => AssignTaskInstant(role, "دعم الجواسيس"));
                AddActionButton("تلفيق تهمة خيانة", (s, evt) => AssignTaskProvince(role, "تلفيق تهمة خيانة"));
            }
            else if (role == "chancellor")
            {
                AddActionButton($"استشارة {c.Name} في شؤون الولاة والتمرد 📜", (s, evt) => ConsultPolitics(role));
                AddActionButton("تحسين العلاقات الداخلية (مقاطعة)", (s, evt) => AssignTaskProvince(role, "تحسين العلاقات"));
                AddActionButton("تحسين العلاقات الدبلوماسية (دولة مجاورة)", (s, evt) => AssignTaskNeighbor(role, "تحسين العلاقات الدبلوماسية"));
                AddActionButton("تعزيز هيبة الدولة (+10 هيبة)", (s, evt) => AssignTaskInstant(role, "تعزيز هيبة الدولة"));
            }
            
            AddActionButton("إلغاء والعودة", (s, evt) => ShowCouncilScreen(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ConsultPolitics(string role)
        {
            ClearDynamicPanel();
            var c = state.Council[role];
            string msg = $"أجاب {c.Name}:\n";
            
            var dangerousFactions = state.Factions.Where(f => f.IsActive && f.PowerPercent > 50).ToList();
            var rebelliousGovs = state.Governors.Where(g => g.CurrentMood == "Rebellious" || g.CurrentMood == "Opportunist").ToList();

            if (role == "steward")
            {
                if (dangerousFactions.Any(f => f.Type == "LowerTaxes"))
                    msg += "مولاي، الضرائب ترهق النبلاء. هناك فصيل يطالب بخفضها وقوته تتعاظم، قد يكون من الحكمة الاستماع إليهم أو رشوة قائدهم لإنعاش الخزينة لاحقاً.";
                else
                    msg += "طالما أن الخزينة ممتلئة، يمكننا رشوة أي والٍ تسول له نفسه التمرد يا مولاي.";
            }
            else if (role == "marshal")
            {
                if (dangerousFactions.Count > 0)
                    msg += $"سيدي! الفصائل تجمع قواها! قوة فصيل {dangerousFactions[0].Name} تبلغ {dangerousFactions[0].PowerPercent}%. يجب حشد الجيوش تحسباً لأي تمرد مسلح.";
                else if (rebelliousGovs.Count > 0)
                    msg += $"أرى أن {rebelliousGovs[0].Name} يتململ في مقاطعته. إذا حاول الخيانة، سيجد سيفي بانتظاره.";
                else
                    msg += "الولاة يخافون من جيوشنا يا مولاي، لا أحد يجرؤ على التمرد حالياً.";
            }
            else if (role == "spymaster")
            {
                if (rebelliousGovs.Count > 0)
                    msg += $"عيوني تخبرني أن {rebelliousGovs[0].Name} يخطط لشيء ما في الظلام. موقفه خطير، أنصح بمراقبته أو تهديده قبل أن يتحرك.";
                else if (dangerousFactions.Count > 0)
                    msg += $"الهمسات في الظلام تقول إن فصيل {dangerousFactions[0].Name} يقترب من حد الانفجار. يجب أن نغتال قائدهم أو نفككهم من الداخل.";
                else
                    msg += "لا توجد مؤامرات سياسية واضحة، لكنني سأبقي عيني مفتوحتين.";
            }
            else if (role == "chancellor")
            {
                if (dangerousFactions.Count > 0)
                    msg += $"يا مولاي، فصيل {dangerousFactions[0].Name} غاضب. أنصح بمنح قائد الفصيل لقباً شرفياً لامتصاص غضبه بالطرق الدبلوماسية.";
                else
                    msg += "العلاقات بينك وبين الولاة مستقرة، استمر في منح الألقاب والهدايا لضمان ولائهم الدائم.";
            }
            else if (role == "chaplain")
            {
                if (rebelliousGovs.Count > 0)
                    msg += $"إن {rebelliousGovs[0].Name} يبتعد عن طريق الصواب. يجب أن نرسل له من يعظه أو نهدده بغضب السماء.";
                else
                    msg += "طالما أنك تحكم بالعدل والتقوى، سيبارك الله ملكك ويحفظ ولاء رعاياك ونبلائك.";
            }

            SetNarrativeText(msg);
            AddActionButton("العودة إلى المستشار", (s, evt) => ManageCouncilor(role));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void AssignTaskInstant(string role, string taskName)
        {
            state.Council[role].Task = taskName;
            state.Council[role].Target = null;
            state.Council[role].TurnsLeft = 0;
            SpeakToActiveReader($"تم تكليف {state.Council[role].Name} بمهمة: {taskName}.");
            ShowCouncilScreen(null, null);
        }

        private void AssignTaskProvince(string role, string taskName, int goldCost = 0)
        {
            ClearDynamicPanel();
            SetNarrativeText($"اختر المقاطعة المستهدفة لمهمة \"{taskName}\":");
            
            foreach (var p in state.Provinces)
            {
                AddActionButton($"مقاطعة {p.Name}", (s, evt) => ConfirmProvinceTask(role, taskName, p.Name, goldCost));
            }
            
            AddActionButton("إلغاء واختيار مهمة أخرى", (s, evt) => ManageCouncilor(role));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ConfirmProvinceTask(string role, string taskName, string provName, int goldCost)
        {
            if (state.Gold < goldCost)
            {
                SpeakToActiveReader("ليس لديك ما يكفي من الذهب لهذه المهمة.");
                AssignTaskProvince(role, taskName, goldCost);
                return;
            }
            state.Gold -= goldCost;
            
            state.Council[role].Task = taskName;
            state.Council[role].Target = provName;
            state.Council[role].TurnsLeft = 3;
            if (taskName == "توطيد العلاقات الدينية" || taskName == "تحسين العلاقات") state.Council[role].TurnsLeft = 0;

            SpeakToActiveReader($"تم توجيه {state.Council[role].Name} إلى {provName} للقيام بمهمة: {taskName}.");
            ShowCouncilScreen(null, null);
        }

        private void AssignTaskNeighbor(string role, string taskName)
        {
            ClearDynamicPanel();
            SetNarrativeText($"اختر المملكة المجاورة المستهدفة لمهمة \"{taskName}\":");
            
            foreach (var n in state.Neighbors)
            {
                AddActionButton($"مملكة {n.Name}", (s, evt) => {
                    state.Council[role].Task = taskName;
                    state.Council[role].Target = n.Name;
                    state.Council[role].TurnsLeft = 2;
                    if (taskName == "تحسين العلاقات الدبلوماسية") state.Council[role].TurnsLeft = 0;
                    SpeakToActiveReader($"تم توجيه {state.Council[role].Name} إلى {n.Name} للقيام بمهمة: {taskName}.");
                    ShowCouncilScreen(null, null);
                });
            }
            
            AddActionButton("إلغاء واختيار مهمة أخرى", (s, evt) => ManageCouncilor(role));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }



        private void ShowRenameMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "rename_menu";
            SetNarrativeText("قائمة تغيير أسماء الشخصيات في المملكة.");

            AddActionButton($"تغيير اسم الملك ({state.RulerName})", (s, evt) => {
                using (RenameForm rf = new RenameForm("تغيير الاسم", "أدخل اسم الملك الجديد:", state.RulerName, narration, state))
                {
                    if (rf.ShowDialog() == DialogResult.OK) { state.RulerName = rf.InputText; ShowRenameMenu(null, null); }
                }
            });

            if (!string.IsNullOrEmpty(state.SpouseName))
            {
                AddActionButton($"تغيير اسم الزوجة ({state.SpouseName})", (s, evt) => {
                    using (RenameForm rf = new RenameForm("تغيير الاسم", "أدخل اسم الزوجة الجديد:", state.SpouseName, narration, state))
                    {
                        if (rf.ShowDialog() == DialogResult.OK) { state.SpouseName = rf.InputText; ShowRenameMenu(null, null); }
                    }
                });
            }

            if (!string.IsNullOrEmpty(state.HeirName))
            {
                AddActionButton($"تغيير اسم ولي العهد ({state.HeirName})", (s, evt) => {
                    using (RenameForm rf = new RenameForm("تغيير الاسم", "أدخل اسم ولي العهد الجديد:", state.HeirName, narration, state))
                    {
                        if (rf.ShowDialog() == DialogResult.OK) { state.HeirName = rf.InputText; ShowRenameMenu(null, null); }
                    }
                });
            }

            foreach (var child in state.Children)
            {
                if (child.Name == state.HeirName) continue;
                AddActionButton($"تغيير اسم الابن ({child.Name})", (s, evt) => {
                    using (RenameForm rf = new RenameForm("تغيير الاسم", "أدخل اسم الابن الجديد:", child.Name, narration, state))
                    {
                        if (rf.ShowDialog() == DialogResult.OK) { child.Name = rf.InputText; ShowRenameMenu(null, null); }
                    }
                });
            }

            AddActionButton("تغيير أسماء مجلس المستشارين", (s, evt) => {
                ClearDynamicPanel();
                SetNarrativeText("اختر المستشار لتغيير اسمه:");
                foreach(var kvp in state.Council)
                {
                    AddActionButton($"تغيير اسم {kvp.Value.Title} ({kvp.Value.Name})", (ns, ne) => {
                        using (RenameForm rf = new RenameForm("تغيير الاسم", "أدخل الاسم الجديد للمستشار:", kvp.Value.Name, narration, state))
                        {
                            if (rf.ShowDialog() == DialogResult.OK) { kvp.Value.Name = rf.InputText; ShowRenameMenu(null, null); }
                        }
                    });
                }
                AddActionButton("عودة", (ns, ne) => ShowRenameMenu(null, null));
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("تغيير أسماء قادة المقاطعات (الولاة)", (s, evt) => {
                ClearDynamicPanel();
                SetNarrativeText("اختر الوالي لتغيير اسمه:");
                foreach(var p in state.Provinces)
                {
                    AddActionButton($"تغيير اسم والي {p.Name} ({p.Vassal})", (ns, ne) => {
                        using (RenameForm rf = new RenameForm("تغيير الاسم", "أدخل الاسم الجديد للوالي:", p.Vassal, narration, state))
                        {
                            if (rf.ShowDialog() == DialogResult.OK) { p.Vassal = rf.InputText; ShowRenameMenu(null, null); }
                        }
                    });
                }
                AddActionButton("عودة", (ns, ne) => ShowRenameMenu(null, null));
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        // --- END TURN LOGIC ---

        private string ProcessCouncilTasks()
        {
            string msg = "";
            var rnd = new Random();
            
            var cChap = state.Council["chaplain"];
            if (cChap.Task == "توطيد العلاقات الدينية" && !string.IsNullOrEmpty(cChap.Target))
            {
                var p = state.Provinces.FirstOrDefault(x => x.Name == cChap.Target);
                if (p != null)
                {
                    p.Satisfaction = Math.Min(100, p.Satisfaction + 15);
                    state.Piety += 10;
                    msg += $"🕌 {cChap.Name} عقد مجلساً وعظياً في {p.Name}. ارتفع رضا الشعب هناك وكسبت +10 تقوى.\n";
                }
            }
            else if (cChap.Task == "نشر عقيدة البلاط" && !string.IsNullOrEmpty(cChap.Target))
            {
                cChap.TurnsLeft--;
                if (cChap.TurnsLeft <= 0)
                {
                    var p = state.Provinces.FirstOrDefault(x => x.Name == cChap.Target);
                    if (p != null)
                    {
                        p.Religion = "سُني أشعري";
                        cChap.Task = "توطيد العلاقات الدينية";
                        msg += $"🕌 نجح {cChap.Name} في نشر العقيدة في {p.Name} بالكامل!\n";
                    }
                }
            }

            var cStew = state.Council["steward"];
            if (cStew.Task == "جمع الضرائب")
            {
                var p = state.Provinces[rnd.Next(state.Provinces.Count)];
                p.Satisfaction = Math.Max(10, p.Satisfaction - 10);
                msg += $"💰 فرض {cStew.Name} جباية استثنائية (ضرائب +25%)، ولكن تراجع رضا {p.Name}.\n";
            }
            else if (cStew.Task == "إعمار الأراضي" && !string.IsNullOrEmpty(cStew.Target))
            {
                cStew.TurnsLeft--;
                if (cStew.TurnsLeft <= 0)
                {
                    var p = state.Provinces.FirstOrDefault(x => x.Name == cStew.Target);
                    if (p != null)
                    {
                        p.Income += 15;
                        cStew.Task = "جمع الضرائب";
                        cStew.Target = null;
                        msg += $"💰 أنهى الوزير جعفر إعمار {p.Name}. زاد الدخل +15 دائم.\n";
                    }
                }
            }

            var cMarsh = state.Council["marshal"];
            if (cMarsh.Task == "حشد العسكر")
            {
                state.Army += 15;
                msg += $"⚔️ حشد {cMarsh.Name} كتائب فرسان متطوعين (+15 جيش).\n";
            }
            else if (cMarsh.Task == "تعزيز الحاميات" && !string.IsNullOrEmpty(cMarsh.Target))
            {
                var p = state.Provinces.FirstOrDefault(x => x.Name == cMarsh.Target);
                if (p != null)
                {
                    p.Garrison += 20;
                    cMarsh.Task = "حشد العسكر";
                    cMarsh.Target = null;
                    msg += $"⚔️ أنهى {cMarsh.Name} تعزيز حامية {p.Name} بـ 20 جندي.\n";
                }
            }

            var cSpy = state.Council["spymaster"];
            if (cSpy.Task == "كشف المؤامرات")
            {
                state.RulerStress = Math.Max(0, state.RulerStress - 10);
                msg += $"👁️ قام {cSpy.Name} بتأمين القصر (-10% ضغط نفسي).\n";
            }
            else if (cSpy.Task == "تلفيق تهمة خيانة" && !string.IsNullOrEmpty(cSpy.Target))
            {
                var p = state.Provinces.FirstOrDefault(x => x.Name == cSpy.Target);
                if (p != null)
                {
                    p.HasRevocationReason = true;
                    cSpy.Task = "كشف المؤامرات";
                    cSpy.Target = null;
                    msg += $"👁️ نجح {cSpy.Name} في تلفيق تهمة ضد والي {p.Name}.\n";
                }
            }

            var cChan = state.Council["chancellor"];
            if (cChan.Task == "تحسين العلاقات" && !string.IsNullOrEmpty(cChan.Target))
            {
                var p = state.Provinces.FirstOrDefault(x => x.Name == cChan.Target);
                if (p != null)
                {
                    p.Opinion = Math.Min(100, p.Opinion + 20);
                    p.Satisfaction = Math.Min(100, p.Satisfaction + 10);
                    cChan.Task = "تعزيز هيبة الدولة";
                    cChan.Target = null;
                    msg += $"🌍 قام {cChan.Name} بتحسين العلاقات في {p.Name} (+20 ولاء).\n";
                }
            }
            else if (cChan.Task == "تحسين العلاقات الدبلوماسية" && !string.IsNullOrEmpty(cChan.Target))
            {
                var n = state.Neighbors.FirstOrDefault(x => x.Name == cChan.Target);
                if (n != null)
                {
                    n.Opinion = Math.Min(100, n.Opinion + 15);
                    cChan.Task = "تعزيز هيبة الدولة";
                    cChan.Target = null;
                    msg += $"🌍 نجحت السفارة الدبلوماسية لـ {n.Name} (+15 رأي).\n";
                }
            }
            else if (cChan.Task == "تعزيز هيبة الدولة")
            {
                state.Prestige += 10;
                msg += $"🌍 عزز الأمير خالد هيبة السلالة (+10 هيبة).\n";
            }

            return msg;
        }

        
        private void ShowChildrenMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "family_children";
            SetNarrativeText("قائمة الأبناء والورثة. اختر طفلاً لمعرفة تفاصيله.");
            
            if (state.Children == null || state.Children.Count == 0)
            {
                AddActionButton("لا يوجد أبناء حالياً.", (s, evt) => ShowRoyalPalace(null, null));
            }
            else
            {
                foreach(var child in state.Children)
                {
                    string heirMark = child.IsHeir ? " (ولي العهد)" : "";
                    string deadMark = child.IsDead ? " (متوفي)" : "";
                    AddActionButton($"{child.Name}{heirMark}{deadMark} - العمر: {child.Age}", (s, evt) => {
                        ClearDynamicPanel();
                        SetNarrativeText($"تقرير عن {child.Name}:\nالعمر: {child.Age}\nالأم: {child.MotherName}\nالمهارة العسكرية: {child.MilitarySkill}\nالمهارة السياسية: {child.DiplomaticSkill}");
                        
                        if (!child.IsDead && !child.IsHeir)
                        {
                            AddActionButton("تعيين كولي للعهد", (ns, ne) => {
                                foreach(var c in state.Children) c.IsHeir = false;
                                child.IsHeir = true;
                                state.HeirName = child.Name;
                                state.HeirAge = child.Age;
                                SetNarrativeText($"تم تعيين {child.Name} كولي للعهد!");
                                ShowChildrenMenu();
                            });
                        }
                        AddActionButton("العودة", (ns, ne) => ShowChildrenMenu());
                        if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                    });
                }
            }
            AddActionButton("العودة لإدارة الأسرة", (s, evt) => ShowRoyalPalace(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }
private void ShowWivesMenu()
        {
            ClearDynamicPanel();
            if (state.Wives == null || state.Wives.Count == 0)
            {
                SetNarrativeText("لا يوجد للملك زوجة حالياً.");
                AddActionButton("العودة للقصر", ShowRoyalPalace);
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                return;
            }

            SetNarrativeText("جناح الملكات. اختر الزوجة للتفاعل معها:");
            foreach (var wife in state.Wives)
            {
                AddActionButton($"{wife.Name} (ثقة: {wife.Trust}، نفوذ: {wife.Influence})", (s, evt) => {
                    ClearDynamicPanel();
                    string details = $"الزوجة: {wife.Name}\nأصلها: {wife.OriginType}\nولاؤها ورأيها: {wife.OpinionOfKing}\nالثقة: {wife.Trust}\nالنفوذ: {wife.Influence}\nالطموح: {wife.Ambition}\nالمهارة السياسية: {wife.PoliticalSkill}\n";
                    if (wife.IsPregnant) details += $"[حامل: بقي {wife.PregnancyDaysLeft} يوم]\n";
                    if (wife.DutyDaysRemaining > 0)
                    {
                        string target = string.IsNullOrWhiteSpace(wife.DutyTargetName) ? "" : $" نحو {wife.DutyTargetName}";
                        details += $"المهمة الحالية: {wife.CurrentTask}{target}، بقي {wife.DutyDaysRemaining} يوم.\n";
                    }
                    SetNarrativeText(details);

                    AddActionButton("استشارتها في شؤون البلاط", (ns, ne) => {
                        var res = DynastySystem.ConsultWife(state, wife.Id);
                        HandleActionResult(res, () => ShowWivesMenu());
                    });

                    if (wife.OriginType == "GovernorFamily" || wife.OriginType == "LocalProvince")
                    {
                        AddActionButton("تكليفها بتهدئة مقاطعتها", (ns, ne) => {
                            var res = DynastySystem.CalmProvinceByWife(state, wife.Id);
                            HandleActionResult(res, () => ShowWivesMenu());
                        });
                    }

                    AddActionButton("قضاء وقت خاص معها", (ns, ne) => {
                        var res = DynastySystem.SpendPrivateTime(state, wife.Id);
                        HandleActionResult(res, () => ShowWivesMenu());
                    });

                    AddActionButton("العودة لجناح الملكات", (ns, ne) => ShowWivesMenu());
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }

            AddActionButton("العودة للقصر", ShowRoyalPalace);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowFirstMinisterScreen(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_firstminister";

            if (state.FirstMinister == null || !state.FirstMinister.IsAppointed)
            {
                SetNarrativeText("لا يوجد وزير أول معين حالياً. هل ترغب بتعيين أحد النبلاء في هذا المنصب؟");
                AddActionButton("تعيين وزير أول (100 ذهب)", (ns, ne) => {
                    if (state.Gold >= 100)
                    {
                        state.Gold -= 100;
                        var res = FirstMinisterSystem.AppointMinister(state, "المستشار بهاء الدين", 7, 8, 80);
                        HandleActionResult(res, () => ShowFirstMinisterScreen(this, EventArgs.Empty));
                    }
                    else
                    {
                        SetNarrativeText("الذهب لا يكفي لتعيين وزير أول.");
                        AddActionButton("العودة للمجلس الاستشاري", (s2, evt2) => ShowCouncilScreen(this, EventArgs.Empty));
                    }
                });
                AddActionButton("العودة للمجلس الاستشاري", (s, evt) => ShowCouncilScreen(this, EventArgs.Empty));
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                return;
            }

            SetNarrativeText($"الوزير الأول {state.FirstMinister.Name} في خدمتك يا مولاي.\nولاء: {state.FirstMinister.Loyalty}، نفوذ: {state.FirstMinister.Influence}\nالمهمة الحالية: {state.FirstMinister.CurrentTask}" +
                (state.FirstMinister.TaskDaysRemaining > 0 ? $"، متبقٍ {state.FirstMinister.TaskDaysRemaining} يوم" : "") +
                $"\nالميزانية الشهرية: {state.FirstMinister.MonthlyBudgetPercent}%");

            AddActionButton("تقرير شامل عن المملكة", (ns, ne) => {
                var res = FirstMinisterSystem.GenerateComprehensiveReport(state);
                HandleActionResult(res, () => ShowFirstMinisterScreen(this, EventArgs.Empty));
            });

            AddActionButton("مراجعة سجلات الجباية (100 ذهب، 20 يوم)", (ns, ne) =>
                HandleActionResult(FirstMinisterSystem.AssignTask(state, "AuditTaxes"), () => ShowFirstMinisterScreen(this, EventArgs.Empty)));
            AddActionButton("تنسيق أعمال المجلس (80 ذهب، 15 يوم)", (ns, ne) =>
                HandleActionResult(FirstMinisterSystem.AssignTask(state, "CoordinateCouncil"), () => ShowFirstMinisterScreen(this, EventArgs.Empty)));
            AddActionButton("تهدئة الولاة الكبار (120 ذهب، 25 يوم)", (ns, ne) =>
                HandleActionResult(FirstMinisterSystem.AssignTask(state, "AppeaseGovernors"), () => ShowFirstMinisterScreen(this, EventArgs.Empty)));
            AddActionButton("مركزة الدواوين العباسية (150 ذهب، 35 يوم)", (ns, ne) =>
                HandleActionResult(FirstMinisterSystem.AssignTask(state, "CentralizeDiwans"), () => ShowFirstMinisterScreen(this, EventArgs.Empty)));
            AddActionButton("تفتيش الفساد في الدواوين (100 ذهب، 20 يوم)", (ns, ne) =>
                HandleActionResult(FirstMinisterSystem.AssignTask(state, "AntiCorruption"), () => ShowFirstMinisterScreen(this, EventArgs.Empty)));
            AddActionButton("تنظيم مكوس الطرق والقوافل (120 ذهب، 25 يوم)", (ns, ne) =>
                HandleActionResult(FirstMinisterSystem.AssignTask(state, "RoadTaxReform"), () => ShowFirstMinisterScreen(this, EventArgs.Empty)));

            AddActionButton("تحديد ميزانية الوزير الأول", (ns, ne) => ShowFirstMinisterBudgetMenu());
            
            AddActionButton("العودة للمجلس الاستشاري", (s, evt) => ShowCouncilScreen(this, EventArgs.Empty));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowFirstMinisterBudgetMenu()
        {
            ClearDynamicPanel();
            SetNarrativeText("اختر نسبة الميزانية الشهرية للوزير الأول. هذه النسبة تُقتطع من دخل الشهر وتزيد أثره الإداري.");
            foreach (var percent in new[] { 0, 10, 20, 30, 40 })
            {
                AddActionButton($"{percent}% من الدخل الشهري", (s, evt) =>
                    HandleActionResult(FirstMinisterSystem.SetMonthlyBudget(state, percent), () => ShowFirstMinisterScreen(this, EventArgs.Empty)));
            }
            AddActionButton("عودة للوزير الأول", (s, evt) => ShowFirstMinisterScreen(this, EventArgs.Empty));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowTradeDevelopmentMenu()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_trade_development";
            SetScreenTitle("تنمية التجارة والأسواق");
            SetNarrativeText($"إدارة الدخل التجاري.\nثقة التجار: {state.MerchantsTrust}\nالسوق الموسمي: {(state.SeasonalMarketDaysLeft > 0 ? $"نشط، متبقٍ {state.SeasonalMarketDaysLeft} يوم" : "غير نشط")}\nعقود التجار النشطة: {state.ActiveSupplyContracts}\nالطرق المحمية: {state.ProtectedTradeRoutes.Count}");

            AddActionButton("إقامة سوق موسمي في بغداد والطرق الكبرى (200 ذهب، 100 مؤونة)", (s, evt) =>
                HandleActionResult(EconomySystem.StartSeasonalMarket(state), () => ShowTradeDevelopmentMenu()));

            AddActionButton("تحديث سجلات التجار ومنح تسهيلات (150 ذهب)", (s, evt) =>
                HandleActionResult(EconomySystem.GrantMerchantPrivileges(state), () => ShowTradeDevelopmentMenu()));

            var routes = state.Neighbors
                .Where(n => n.TradeTreaty)
                .Select(n => $"طريق التجارة مع {n.Name}")
                .Concat(new[] { "طريق بغداد-دمشق", "طريق بغداد-حلب", "طريق دمشق-القدس" })
                .Distinct()
                .ToList();

            foreach (var route in routes)
            {
                AddActionButton($"إرسال قوات لحماية {route} (100 ذهب، 50 مؤونة)", (s, evt) =>
                    HandleActionResult(EconomySystem.ProtectTradeRoute(state, route), () => ShowTradeDevelopmentMenu()));
            }

            AddActionButton("العودة للاقتصاد والمقاطعات", ShowEconomyHub);
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowTreasuryMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_treasury";
            audio.StopAmbient();
            audio.Play("ambient_dungeon", true, true);
            audio.PlayPaper();

            SetNarrativeText($"الخزينة الملكية:\nالذهب المتوفر: {state.Gold}\nثقة التجار: {state.MerchantsTrust}\nعدد القروض النشطة: {state.Loans.Count}");

            AddActionButton("تنمية التجارة والأسواق", (ns, ne) => ShowTradeDevelopmentMenu());

            AddActionButton("طلب قرض من نقابة التجار (1000 ذهب)", (ns, ne) => {
                var res = EconomySystem.RequestMerchantLoan(state, 1000);
                HandleActionResult(res, () => ShowTreasuryMenu(null, null));
            });

            if (state.Neighbors.Any(n => n.Relation == "تحالف" || n.Opinion >= 50))
            {
                var richNeighbor = state.Neighbors.First(n => n.Relation == "تحالف" || n.Opinion >= 50);
                AddActionButton($"طلب قرض أجنبي من {richNeighbor.Name} (2000 ذهب)", (ns, ne) => {
                    var res = EconomySystem.RequestForeignLoan(state, richNeighbor.Name);
                    HandleActionResult(res, () => ShowTreasuryMenu(null, null));
                });
            }

            foreach (var loan in state.Loans)
            {
                string status = loan.IsDefaulted ? "[متعثر]" : "[مستمر]";
                AddActionButton($"سداد قرض {loan.LenderName} {status} (المتبقي {loan.RemainingAmount})", (ns, ne) => {
                    var res = EconomySystem.RepayLoan(state, loan.Id, loan.RemainingAmount);
                    HandleActionResult(res, () => ShowTreasuryMenu(null, null));
                });
            }
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        // --- SAVE / LOAD ---
                private void HandleActionResult(GameActionResult res, Action returnAction)
        {
            ClearDynamicPanel();
            SetNarrativeText(FormatActionResult(res));
            
            if (!string.IsNullOrEmpty(res.SoundEffectKey))
            {
                audio.Play(res.SoundEffectKey.Replace(".wav", ""));
            }
            else
            {
                if (res.Success) audio.Play("success");
                else audio.Play("error");
            }

            UpdateUI();
            if (res.ShowAnnexationMenu && res.AnnexedNeighborIdx != -1)
            {
                AddActionButton("تحديد مصير العدو المنهزم", (s, e) => ShowAnnexationMenu(res.AnnexedNeighborIdx));
            }
            else
            {
                AddActionButton("عودة", (s, e) => returnAction());
            }
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private string FormatActionResult(GameActionResult res)
        {
            if (res == null)
                return "لم يصدر تقرير من النظام.";

            var parts = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(res.Title))
                parts.AppendLine(res.Title);

            if (!string.IsNullOrWhiteSpace(res.MainMessage))
                parts.AppendLine(res.MainMessage);
            else if (!res.Success)
                parts.AppendLine("لم ينجح الأمر.");

            if (res.ResourceChanges.Count > 0)
            {
                parts.AppendLine();
                parts.AppendLine("التغيرات:");
                foreach (var change in res.ResourceChanges)
                {
                    string sign = change.Value > 0 ? "+" : "";
                    parts.AppendLine($"{change.Key}: {sign}{change.Value}");
                }
            }

            if (res.Warnings.Count > 0)
            {
                parts.AppendLine();
                parts.AppendLine("تحذيرات:");
                foreach (var warning in res.Warnings)
                    parts.AppendLine(warning);
            }

            return parts.ToString().Trim();
        }

        private void ShowKingdomReport()
        {
            ClearDynamicPanel();
            SetNarrativeText("تقرير المملكة الشامل. يمكنك اختيار القسم الذي تود معرفة تفاصيله.");
            
            AddActionButton("التقرير العام", (s, e) => {
                ClearDynamicPanel();
                string msg = $"الذهب: {state.Gold}\nالمؤونة: {state.Food}\nالجيش: {(state.Armies != null ? state.Armies.Sum(a => a.TotalSoldiers) : 0)}\nالرضا: {state.Satisfaction}/100\nالهيبة: {state.Prestige}\nالتقوى: {state.Piety}\nمستوى الضرائب: {state.TaxLevel}";
                SetNarrativeText(msg);
                AddActionButton("عودة للتقارير", (ns, ne) => ShowKingdomReport());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("التقرير الاقتصادي", (s, e) => {
                ClearDynamicPanel();
                DiplomacySystem.SynchronizeDiplomacyState(state);
                int totalInc = state.Provinces.Where(p => !p.Occupied).Sum(p => p.Income);
                int tradeInc = state.Neighbors.Count(n => n.TradeTreaty) * 30;
                string bq = string.Join("\n", state.BuildingQueue.Select(b => $"- {b.BuildingType} في {b.ProvinceName} ({b.TurnsRemaining} أدوار متبقية)"));
                string msg = $"الدخل الأساسي للمقاطعات: {totalInc}\nدخل التجارة الشهري: {tradeInc}\nالمشاريع قيد البناء:\n{(string.IsNullOrEmpty(bq) ? "لا توجد مشاريع حالياً" : bq)}";
                SetNarrativeText(msg);
                AddActionButton("عودة للتقارير", (ns, ne) => ShowKingdomReport());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("التقرير الدبلوماسي والمؤامرات", (s, e) => {
                ClearDynamicPanel();
                int wars = state.ActiveWar != null ? 1 : 0;
                string allies = string.Join("، ", state.Neighbors.Where(n => n.Alliance).Select(n => n.Name));
                string msg = $"الحروب الجارية: {wars}\nالتحالفات: {(string.IsNullOrEmpty(allies) ? "لا يوجد" : allies)}";
                SetNarrativeText(msg);
                AddActionButton("عودة للتقارير", (ns, ne) => ShowKingdomReport());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("العودة للأوامر الملكية", (s, e) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowIntelligenceMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_intelligence";
            audio.StopAmbient();
            audio.Play("ambient_dungeon", true, true);
            audio.PlayPaper();
            SetNarrativeText("الاستخبارات الملكية.\nهنا تدير شبكات التجسس، تخطط للمكائد، وتكشف أسرار أعدائك وولاتك.");

            AddActionButton("الاستخبارات الداخلية", (s, evt) => ShowInternalIntelligence());
            AddActionButton("الاستخبارات الخارجية", (s, evt) => ShowExternalIntelligence());
            AddActionButton("شبكات الجواسيس الميدانية", (s, evt) => ShowSpyNetworks());
            AddActionButton("تطوير شبكة", (s, evt) => ShowUpgradeSpyNetworks());
            
            int opsCount = state.IntelligenceOperations.Count(o => o.Status == "Active");
            AddActionButton($"العمليات الجارية ({opsCount})", (s, evt) => ShowActiveOperations());
            
            AddActionButton("التقارير السرية", (s, evt) => ShowSecretReports());
            AddActionButton("مكافحة الاستخبارات وحماية القصر", (s, evt) => ShowCounterIntelligence());
            AddActionButton("الأسرار والخطافات والمكائد", (s, evt) => ShowHooksAndSchemesMenu());
            
            AddActionButton("العودة للأوامر الملكية", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowInternalIntelligence()
        {
            ClearDynamicPanel();
            SetNarrativeText("الاستخبارات الداخلية: راقب ولاتك، وفكك الفصائل المعارضة.");
            
            AddActionButton("تأسيس شبكة تجسس في مقاطعة (150 ذهب)", (s, evt) => {
                ClearDynamicPanel();
                SetNarrativeText("اختر المقاطعة لتأسيس الشبكة فيها:");
                foreach(var prov in state.Provinces)
                {
                    AddActionButton(prov.Name, (ns, ne) => {
                        var res = IntelligenceSystem.EstablishNetwork(state, $"شبكة {prov.Name}", "InternalProvince", prov.Name);
                        HandleActionResult(res, ShowInternalIntelligence);
                    });
                }
                AddActionButton("عودة", (ns, ne) => ShowInternalIntelligence());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("مراقبة والٍ (50 ذهب، 3 أيام)", (s, evt) => {
                ClearDynamicPanel();
                SetNarrativeText("اختر الوالي للمراقبة:");
                foreach(var gov in state.Governors)
                {
                    var net = state.SpyNetworks.FirstOrDefault(n => n.TargetType == "InternalProvince" && n.TargetId == gov.ProvinceName);
                    if (net != null)
                    {
                        AddActionButton(gov.Name, (ns, ne) => {
                            var res = IntelligenceSystem.StartOperation(state, $"مراقبة {gov.Name}", "مراقبة والٍ", "InternalProvince", gov.ProvinceName, net.Id, 50, 3);
                            HandleActionResult(res, ShowInternalIntelligence);
                        });
                    }
                }
                AddActionButton("عودة", (ns, ne) => ShowInternalIntelligence());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("اغتيال والٍ (500 ذهب، 10 أيام)", (s, evt) => {
                ClearDynamicPanel();
                SetNarrativeText("اختر الوالي المستهدف بالاغتيال:");
                foreach(var gov in state.Governors)
                {
                    var net = state.SpyNetworks.FirstOrDefault(n => n.TargetType == "InternalProvince" && n.TargetId == gov.ProvinceName);
                    if (net != null)
                    {
                        AddActionButton(gov.Name, (ns, ne) => {
                            var res = IntelligenceSystem.StartOperation(state, $"اغتيال {gov.Name}", "اغتيال والي", "InternalProvince", gov.ProvinceName, net.Id, 500, 10);
                            HandleActionResult(res, ShowInternalIntelligence);
                        });
                    }
                }
                AddActionButton("عودة", (ns, ne) => ShowInternalIntelligence());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("البحث عن فضائح (150 ذهب، 7 أيام)", (s, evt) => {
                ClearDynamicPanel();
                SetNarrativeText("اختر الوالي للبحث عن فضائحه لابتزازه:");
                foreach(var gov in state.Governors)
                {
                    var net = state.SpyNetworks.FirstOrDefault(n => n.TargetType == "InternalProvince" && n.TargetId == gov.ProvinceName);
                    if (net != null)
                    {
                        AddActionButton(gov.Name, (ns, ne) => {
                            var res = IntelligenceSystem.StartOperation(state, $"فضائح {gov.Name}", "بحث عن فضائح", "InternalProvince", gov.ProvinceName, net.Id, 150, 7);
                            HandleActionResult(res, ShowInternalIntelligence);
                        });
                    }
                }
                AddActionButton("عودة", (ns, ne) => ShowInternalIntelligence());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("تفكيك فصيل (200 ذهب، 7 أيام)", (s, evt) => {
                ClearDynamicPanel();
                SetNarrativeText("اختر الفصيل لتفكيكه:");
                foreach(var fac in state.Factions.Where(f => f.IsActive))
                {
                    var leader = state.Governors.FirstOrDefault(g => g.Id == fac.LeaderGovernorId);
                    if (leader != null)
                    {
                        var net = state.SpyNetworks.FirstOrDefault(n => n.TargetType == "InternalProvince" && n.TargetId == leader.ProvinceName);
                        if (net != null)
                        {
                            AddActionButton(fac.Name, (ns, ne) => {
                                var res = IntelligenceSystem.StartOperation(state, $"تفكيك {fac.Name}", "تفكيك فصيل", "Faction", fac.Id, net.Id, 200, 7);
                                HandleActionResult(res, ShowInternalIntelligence);
                            });
                        }
                    }
                }
                AddActionButton("عودة", (ns, ne) => ShowInternalIntelligence());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("العودة للاستخبارات", (s, evt) => ShowIntelligenceMenu(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowExternalIntelligence()
        {
            ClearDynamicPanel();
            SetNarrativeText("الاستخبارات الخارجية: اخترق الممالك المجاورة، واعرف نواياهم.");
            
            AddActionButton("تأسيس شبكة تجسس في مملكة مجاورة (150 ذهب)", (s, evt) => {
                ClearDynamicPanel();
                SetNarrativeText("اختر المملكة لتأسيس الشبكة فيها:");
                foreach(var neigh in state.Neighbors)
                {
                    AddActionButton(neigh.Name, (ns, ne) => {
                        var res = IntelligenceSystem.EstablishNetwork(state, $"شبكة {neigh.Name}", "ForeignKingdom", neigh.Name);
                        HandleActionResult(res, ShowExternalIntelligence);
                    });
                }
                AddActionButton("عودة", (ns, ne) => ShowExternalIntelligence());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("استطلاع مملكة مجاورة (100 ذهب، 5 أيام)", (s, evt) => {
                ClearDynamicPanel();
                SetNarrativeText("اختر المملكة لمعرفة جيشها ونواياها:");
                foreach(var neigh in state.Neighbors)
                {
                    var net = state.SpyNetworks.FirstOrDefault(n => n.TargetType == "ForeignKingdom" && n.TargetId == neigh.Name);
                    if (net != null)
                    {
                        AddActionButton(neigh.Name, (ns, ne) => {
                            var res = IntelligenceSystem.StartOperation(state, $"استطلاع {neigh.Name}", "استطلاع مملكة", "ForeignKingdom", neigh.Name, net.Id, 100, 5);
                            HandleActionResult(res, ShowExternalIntelligence);
                        });
                    }
                }
                AddActionButton("عودة", (ns, ne) => ShowExternalIntelligence());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });
            
            AddActionButton("اغتيال الحاكم العدو (1000 ذهب، 20 يوماً)", (s, evt) => {
                ClearDynamicPanel();
                SetNarrativeText("اختر المملكة المستهدفة لاغتيال حاكمها:");
                foreach(var neigh in state.Neighbors)
                {
                    var net = state.SpyNetworks.FirstOrDefault(n => n.TargetType == "ForeignKingdom" && n.TargetId == neigh.Name);
                    if (net != null)
                    {
                        AddActionButton(neigh.Name, (ns, ne) => {
                            var res = IntelligenceSystem.StartOperation(state, $"اغتيال حاكم {neigh.Name}", "اغتيال الحاكم العدو", "ForeignKingdom", neigh.Name, net.Id, 1000, 20);
                            HandleActionResult(res, ShowExternalIntelligence);
                        });
                    }
                }
                AddActionButton("عودة", (ns, ne) => ShowExternalIntelligence());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("تخريب مؤونة العدو (300 ذهب، 10 أيام)", (s, evt) => {
                ClearDynamicPanel();
                SetNarrativeText("اختر المملكة المستهدفة بالتخريب:");
                foreach(var neigh in state.Neighbors)
                {
                    var net = state.SpyNetworks.FirstOrDefault(n => n.TargetType == "ForeignKingdom" && n.TargetId == neigh.Name);
                    if (net != null)
                    {
                        AddActionButton(neigh.Name, (ns, ne) => {
                            var res = IntelligenceSystem.StartOperation(state, $"تخريب {neigh.Name}", "تخريب مؤونة العدو", "ForeignKingdom", neigh.Name, net.Id, 300, 10);
                            HandleActionResult(res, ShowExternalIntelligence);
                        });
                    }
                }
                AddActionButton("عودة", (ns, ne) => ShowExternalIntelligence());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("العودة للاستخبارات", (s, evt) => ShowIntelligenceMenu(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowSpyNetworks()
        {
            ClearDynamicPanel();
            SetNarrativeText("استعراض شبكات الجواسيس النشطة وتفاصيلها.");
            
            if (state.SpyNetworks.Count == 0)
            {
                SetNarrativeText("لا توجد شبكات تجسس مؤسسة بعد.");
            }

            foreach(var net in state.SpyNetworks)
            {
                AddActionButton($"{net.Name} (قوة: {net.Strength}، تحليل: {net.Analysis})", (s, evt) => {
                    ClearDynamicPanel();
                    string info = $"{net.Name}\nالقوة: {net.Strength} من 100.\nالسرية: {net.Secrecy} من 100.\nالاختراق: {net.Infiltration} من 100.\nالتحليل: {net.Analysis} من 100.\nخطر الانكشاف: {net.ExposureRisk}.\nآخر تقرير: {net.LastReport}";
                    SetNarrativeText(info);
                    AddActionButton("العودة للشبكات", (ns, ne) => ShowSpyNetworks());
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }
            
            AddActionButton("العودة للاستخبارات", (s, evt) => ShowIntelligenceMenu(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowUpgradeSpyNetworks()
        {
            ClearDynamicPanel();
            SetNarrativeText("اختر الشبكة التي تود تطويرها لزيادة كفاءتها:");
            
            foreach(var net in state.SpyNetworks)
            {
                AddActionButton($"تطوير {net.Name}", (s, evt) => {
                    ClearDynamicPanel();
                    SetNarrativeText($"تطوير {net.Name}. اختر الإجراء:");
                    
                    AddActionButton("تجنيد مخبرين محليين (100 ذهب) [+قوة -سرية]", (ns, ne) => {
                        var res = IntelligenceSystem.UpgradeNetwork(state, net.Id, "تجنيد مخبرين محليين");
                        HandleActionResult(res, ShowUpgradeSpyNetworks);
                    });
                    AddActionButton("زرع جاسوس داخل البلاط (200 ذهب) [+اختراق +خطر]", (ns, ne) => {
                        var res = IntelligenceSystem.UpgradeNetwork(state, net.Id, "زرع جاسوس داخل البلاط");
                        HandleActionResult(res, ShowUpgradeSpyNetworks);
                    });
                    AddActionButton("شراء ولاء الخدم (150 ذهب) [+تحليل +اختراق]", (ns, ne) => {
                        var res = IntelligenceSystem.UpgradeNetwork(state, net.Id, "شراء ولاء الخدم");
                        HandleActionResult(res, ShowUpgradeSpyNetworks);
                    });
                    AddActionButton("بناء بيت رسائل سري (300 ذهب) [+قوة +سرية]", (ns, ne) => {
                        var res = IntelligenceSystem.UpgradeNetwork(state, net.Id, "بناء بيت رسائل سري");
                        HandleActionResult(res, ShowUpgradeSpyNetworks);
                    });
                    
                    AddActionButton("العودة", (ns, ne) => ShowUpgradeSpyNetworks());
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }
            
            AddActionButton("العودة للاستخبارات", (s, evt) => ShowIntelligenceMenu(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowActiveOperations()
        {
            ClearDynamicPanel();
            var active = state.IntelligenceOperations.Where(o => o.Status == "Active").ToList();
            
            if (active.Count == 0)
            {
                SetNarrativeText("لا توجد عمليات استخباراتية جارية حالياً.");
            }
            else
            {
                string info = "العمليات الجارية:\n";
                foreach(var op in active)
                {
                    info += $"- {op.Name} ({op.DaysRemaining} أيام متبقية)\n";
                }
                SetNarrativeText(info);
            }
            
            AddActionButton("العودة للاستخبارات", (s, evt) => ShowIntelligenceMenu(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowSecretReports()
        {
            ClearDynamicPanel();
            if (state.SecretReports.Count == 0)
            {
                SetNarrativeText("لا توجد تقارير سرية في الأرشيف.");
            }
            else
            {
                string rep = "أحدث التقارير السرية:\n" + string.Join("\n\n", state.SecretReports.Skip(Math.Max(0, state.SecretReports.Count - 5)));
                SetNarrativeText(rep);
            }
            
            AddActionButton("العودة للاستخبارات", (s, evt) => ShowIntelligenceMenu(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowCounterIntelligence()
        {
            ClearDynamicPanel();
            SetNarrativeText($"مكافحة الاستخبارات (المستوى: {state.CounterIntelligenceLevel}).\nتحمي القصر وتخفض من فرص نجاح مؤامرات العدو واغتيالاتهم.");
            
            AddActionButton("تطهير البلاط من الجواسيس (200 ذهب) [+مستوى المكافحة]", (s, evt) => {
                if (state.Gold >= 200)
                {
                    state.Gold -= 200;
                    state.CounterIntelligenceLevel += 5;
                    SetNarrativeText("تم نشر الحرس المخلصين والتحقيق مع الخدم. زادت قوة مكافحة الاستخبارات في القصر.");
                    audio.Play("coin");
                }
                else
                {
                    SetNarrativeText("الذهب لا يكفي لتطهير البلاط.");
                }
                ShowCounterIntelligence();
            });

            if (state.Council.ContainsKey("spymaster"))
            {
                var spy = state.Council["spymaster"];
                AddActionButton($"تعيين مراقب سري لمسؤول الجواسيس (100 ذهب) - {(spy.HasSecretMonitor ? "معين" : "غير معين")}", (ns, ne) => {
                    var res = IntelligenceSystem.AppointSecretMonitor(state);
                    HandleActionResult(res, () => ShowCounterIntelligence());
                });

                AddActionButton("استجواب مسؤول الجواسيس بحدة (-ولاء)", (ns, ne) => {
                    var res = IntelligenceSystem.InterrogateSpymaster(state);
                    HandleActionResult(res, () => ShowCounterIntelligence());
                });

                AddActionButton($"إعلان الدعم لـ {spy.Name} ليصبح يد الملك اليمنى", (ns, ne) => {
                    var res = IntelligenceSystem.SupportSpymaster(state);
                    HandleActionResult(res, () => ShowCounterIntelligence());
                });
            }
            
            AddActionButton("العودة للاستخبارات", (s, evt) => ShowIntelligenceMenu(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void SaveGame()
        {
            if (state == null || !state.GameMode.StartsWith("sandbox")) return;
            DiplomacySystem.SynchronizeDiplomacyState(state);
            var result = SaveManager.SaveGame(state);
            SetNarrativeText(result.Message);
        }

        private void LoadGame()
        {
            // Ensure we don't overwrite settings when loading
            string prevProvider = state.SpeechProvider;
            bool prevST = state.UseSuperTonic;
            double prevSTSpeed = state.SuperTonicSpeed;
            bool prevSapiEvents = state.SapiReadsEvents;
            bool prevSapiNPC = state.SapiReadsNPCs;
            string prevSapiVoice = state.SapiVoiceName;

            var loadResult = SaveManager.LoadGame();
            if (loadResult.Status == LoadGameStatus.Failed)
            {
                SetNarrativeText(loadResult.Message);
                return;
            }

            state = loadResult.Success && loadResult.State != null ? loadResult.State : new GameState();

            // Reapply settings
            state.SpeechProvider = prevProvider;
            state.UseSuperTonic = prevST;
            state.SuperTonicSpeed = prevSTSpeed;
            state.SapiReadsEvents = prevSapiEvents;
            state.SapiReadsNPCs = prevSapiNPC;
            state.SapiVoiceName = prevSapiVoice;
            state.ReconcileOldSaves();
            DiplomacySystem.SynchronizeDiplomacyState(state);

            sapi.IsEnabled = state.SpeechProvider == "sapi5";
            if (!string.IsNullOrEmpty(state.SapiVoiceName)) sapi.SetVoice(state.SapiVoiceName);

            state.GameMode = "sandbox";
            audio.StopAmbient();
            audio.Play("ambient_nature", true, true);
            RenderSandboxButtons();
            SetNarrativeText(loadResult.Success
                ? loadResult.Message
                : "لا يوجد ملف حفظ سابق. تم بدء لعبة جديدة.");
        }

        private void ShowLivingRealmMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_living_realm";
            state.ReconcileOldSaves();
            SetNarrativeText(LivingRealmSystem.GetLivingRealmReport(state));

            var pendingEvents = state.LivingRealmLog
                .Where(ev => !ev.IsResolved && ev.RequiresDecision)
                .OrderByDescending(ev => ev.CreatedDay)
                .Take(8)
                .ToList();

            foreach (var realmEvent in pendingEvents)
            {
                AddActionButton($"قرار: {realmEvent.Title}", (s, evt) => ShowLivingRealmEvent(realmEvent.Id));
            }

            AddActionButton("عرض الوعود النشطة", (s, evt) => {
                ClearDynamicPanel();
                var promises = state.RoyalPromises.Where(p => !p.IsFulfilled && !p.IsBroken).OrderBy(p => p.DueDay).ToList();
                string text = promises.Count == 0
                    ? "لا توجد وعود نشطة."
                    : "الوعود النشطة:\n" + string.Join("\n\n", promises.Select(p => $"{p.Description}\nالهدف: {p.TargetName}\nطريقة الوفاء: {p.FulfillmentHint}"));
                SetNarrativeText(text);
                AddActionButton("عودة للعالم الحي", (ns, ne) => ShowLivingRealmMenu(null, null));
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("عرض الذكريات السياسية", (s, evt) => {
                ClearDynamicPanel();
                var memories = state.PoliticalMemories.Where(m => !m.IsArchived).OrderByDescending(m => m.CreatedDay).Take(15).ToList();
                string text = memories.Count == 0
                    ? "لا توجد ذاكرة سياسية محفوظة بعد."
                    : "الذكريات السياسية:\n" + string.Join("\n\n", memories.Select(m => $"{m.ActorName}: {m.Summary}"));
                SetNarrativeText(text);
                AddActionButton("عودة للعالم الحي", (ns, ne) => ShowLivingRealmMenu(null, null));
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("عرض أهداف الممالك والولاة", (s, evt) => {
                ClearDynamicPanel();
                string neighbors = string.Join("\n", state.Neighbors.Select(n => $"- {n.Name}: {n.PoliticalGoal}. التركيز الداخلي: {n.DevelopmentFocus}. استقرار البلاط: {n.CourtStability}. كفاءة الوزراء: {n.CouncilCompetence}. الطموح العسكري: {n.MilitaryAmbition}. الخطة: {n.SecretPlan}."));
                string governors = string.Join("\n", state.Governors.Select(g => $"- {g.Name} في {g.ProvinceName}: {g.CurrentGoal}. الخطة: {g.SecretPlan}."));
                SetNarrativeText($"أهداف الممالك:\n{neighbors}\n\nأهداف الولاة:\n{governors}");
                AddActionButton("عودة للعالم الحي", (ns, ne) => ShowLivingRealmMenu(null, null));
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
            });

            AddActionButton("عودة للقائمة", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowLivingRealmEvent(string eventId)
        {
            ClearDynamicPanel();
            var realmEvent = state.LivingRealmLog.FirstOrDefault(ev => ev.Id == eventId);
            if (realmEvent == null)
            {
                SetNarrativeText("لم يتم العثور على الحدث.");
                AddActionButton("عودة للعالم الحي", (s, evt) => ShowLivingRealmMenu(null, null));
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                return;
            }

            string report = $"{realmEvent.Title}\n\n{realmEvent.Description}\n\nرأي المجلس:\n{realmEvent.CouncilAdvice}";
            SetNarrativeText(report);

            AddLivingRealmDecisionButtons(realmEvent);
            AddActionButton("عودة للعالم الحي", (s, evt) => ShowLivingRealmMenu(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void AddLivingRealmDecisionButtons(LivingRealmEvent realmEvent)
        {
            void Resolve(string label, string choice)
            {
                AddActionButton(label, (s, evt) => {
                    var res = LivingRealmSystem.ResolveLivingEvent(state, realmEvent.Id, choice);
                    HandleActionResult(res, () => ShowLivingRealmMenu(null, null));
                });
            }

            switch (realmEvent.EventType)
            {
                case "EconomicAidRequest":
                    Resolve("إرسال إغاثة الآن (100 ذهب و150 مؤونة)", "Aid");
                    Resolve("قطع وعد بإرسال الإغاثة", "PromiseAid");
                    Resolve("رفض الطلب", "Refuse");
                    break;
                case "ForeignMarriageProposal":
                    Resolve("قبول الزواج الدبلوماسي", "Accept");
                    Resolve("رفض العرض", "Decline");
                    break;
                case "SecretFundingFaction":
                    Resolve("تمويل تحقيق سري (80 ذهب)", "Investigate");
                    Resolve("اتهام علني الآن", "Accuse");
                    Resolve("تجاهل الشبهات", "Ignore");
                    break;
                case "GovernorDemand":
                    Resolve("خفض الضرائب فوراً", "GrantNow");
                    Resolve("وعد بخفض الضرائب لاحقاً", "PromiseLowerTaxes");
                    Resolve("رفض الطلب", "Refuse");
                    break;
                case "BorderWarPreparation":
                    Resolve("إرسال سفارة عاجلة (50 ذهب)", "SendEnvoy");
                    Resolve("إعلان تعبئة دفاعية", "PrepareArmy");
                    Resolve("ترك الأمر بلا رد", "Ignore");
                    break;
                case "WifeInfluenceRequest":
                    Resolve("دعمها داخل القصر", "Support");
                    Resolve("تأجيل الطلب مع وعد", "Delay");
                    Resolve("رفض الطلب", "Refuse");
                    break;
                case "TradeRouteCrisis":
                    Resolve("إرسال حراسة للطريق (100 ذهب)", "SendGuards");
                    Resolve("وعد بحماية الطريق", "PromiseProtection");
                    Resolve("تجاهل الأزمة", "Ignore");
                    break;
                case "DirectorAmbitiousPlot":
                    Resolve("مراقبة هادئة للطموح (80 ذهب)", "Watch");
                    Resolve("احتواء بتكريم محدود", "Honor");
                    Resolve("تجاهل الهمس مؤقتاً", "Ignore");
                    break;
                case "DirectorPostWarDemands":
                    Resolve("عطايا وتنازلات محدودة (120 ذهب)", "Concede");
                    Resolve("قطع وعد بتخفيف آثار الحرب", "Promise");
                    Resolve("رفض المطالب", "Refuse");
                    break;
                case "DirectorTradeOpportunity":
                    Resolve("رعاية القافلة رسمياً (150 ذهب)", "Fund");
                    Resolve("إرسال حراسة للطريق", "Escort");
                    Resolve("ترك القافلة بلا رعاية", "Decline");
                    break;
                case "DirectorSuccessionPressure":
                    Resolve("إعلان دعم الوريث علناً", "Proclaim");
                    Resolve("جمع المجلس حول ملف الخلافة", "Council");
                    Resolve("تأجيل الحديث عن الخلافة", "Delay");
                    break;
                case "DirectorSpymasterShadowWar":
                    Resolve("تحديد صلاحيات مدير الاستخبارات", "Limit");
                    Resolve("تمويل رقابة مضادة (120 ذهب)", "FundCounter");
                    Resolve("ترك شبكة الظل تعمل", "Ignore");
                    break;
                case "DirectorBorderEnvy":
                    Resolve("إرسال وفد حازم (60 ذهب)", "Envoy");
                    Resolve("تقوية أضعف حامية حدودية (100 ذهب)", "Fortify");
                    Resolve("عدم التحرك الآن", "Ignore");
                    break;
                case "AiMinisterCouncilProposal":
                    Resolve("قبول اقتراح الوزير", "Accept");
                    Resolve("طلب تفاصيل إضافية", "Details");
                    Resolve("رفض الاقتراح", "Reject");
                    break;
                case "AiSpouseCourtProposal":
                    Resolve("دعم طلبها داخل القصر", "Support");
                    Resolve("تأجيل الطلب", "Delay");
                    Resolve("رفض الطلب", "Reject");
                    break;
                case "AiNeighborAudienceInvitation":
                    Resolve("قبول الحوار", "Accept");
                    Resolve("إرسال مبعوث بدلاً منك (50 ذهب)", "Envoy");
                    Resolve("رفض الدعوة", "Decline");
                    break;
                default:
                    Resolve("إغلاق الحدث", "Close");
                    break;
            }
        }

        private void ShowAdvancedDiplomacyMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_diplomacy";
            audio.StopAmbient();
            audio.Play("ambient_council", true, true);
            audio.PlayPaper();
            DiplomacySystem.SynchronizeDiplomacyState(state);
            SetNarrativeText("الدبلوماسية المتقدمة.\nيمكنك هنا إدارة علاقاتك الخارجية وتوقيع المعاهدات أو فرض النفوذ.");

            foreach (var neighbor in state.Neighbors)
            {
                AddActionButton($"تقرير: {neighbor.Name}", (s, evt) => ShowNeighborReport(neighbor));
            }
            
            AddActionButton("عودة للقائمة", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowNeighborReport(Neighbor neighbor)
        {
            ClearDynamicPanel();
            audio.StopAmbient();
            audio.Play("ambient_council", true, true);
            audio.PlayPaper();
            DiplomacySystem.SynchronizeDiplomacyState(state);
            neighbor = state.Neighbors.FirstOrDefault(n => n.Id == neighbor.Id) ?? neighbor;
            int neighborIndex = state.Neighbors.FindIndex(n => n.Id == neighbor.Id);
            
            string report = $"مملكة {neighbor.Name}.\n" +
                            $"الحاكم: {neighbor.RulerName ?? neighbor.Ruler}.\n" +
                            $"الرأي بك: {neighbor.Opinion}.\n" +
                            $"الموقف: {neighbor.DiplomaticStance}.\n" +
                            $"القوة العسكرية: {neighbor.MilitaryStrength}.\n" +
                            $"التركيز الداخلي: {neighbor.DevelopmentFocus}.\n" +
                            $"استقرار البلاط: {neighbor.CourtStability}/100.\n" +
                            $"كفاءة الوزراء: {neighbor.CouncilCompetence}/100.\n" +
                            $"آخر قرارات داخلية معروفة: {(neighbor.InternalDecisionLog == null || neighbor.InternalDecisionLog.Count == 0 ? "لا توجد إشارات مؤكدة" : string.Join("، ", neighbor.InternalDecisionLog.TakeLast(3)))}.\n" +
                            $"الثقة: {neighbor.Trust}.\n" +
                            $"معاهدة عدم اعتداء: {(neighbor.HasNonAggressionPact ? "نعم" : "لا")}\n" +
                            $"تحالف: {(neighbor.IsAlly ? "نعم" : "لا")}\n" +
                            $"معاهدة تجارية: {(neighbor.TradeTreaty ? "نعم" : "لا")}\n";
                            
            SetNarrativeText(report);

            AddActionButton("مخاطبة الحاكم بجلسة ذكية", (s, evt) => ShowNeighborAudienceComposer(neighbor.Id, () => ShowNeighborReport(neighbor)));

            if (neighborIndex >= 0)
            {
                if (!neighbor.TradeTreaty)
                {
                    AddActionButton("اقتراح معاهدة تجارية", (s, evt) => {
                        string msg = DiplomacySystem.OfferTrade(state, neighborIndex);
                        HandleActionResult(new GameActionResult { Success = true, Title = "معاهدة تجارية", MainMessage = msg }, () => ShowNeighborReport(neighbor));
                    });
                }
                else
                {
                    AddActionButton("إلغاء المعاهدة التجارية", (s, evt) => {
                        string msg = DiplomacySystem.CancelTrade(state, neighborIndex);
                        HandleActionResult(new GameActionResult { Success = true, Title = "إلغاء التجارة", MainMessage = msg }, () => ShowNeighborReport(neighbor));
                    });
                }
            }

            if (!neighbor.HasNonAggressionPact)
                AddActionButton("توقيع اتفاق عدم اعتداء", (s, evt) => HandleActionResult(DiplomacySystem.SignNonAggressionPact(state, neighbor.Id), () => ShowNeighborReport(neighbor)));
            
            if (!neighbor.IsAlly)
            {
                AddActionButton("توقيع تحالف دفاعي", (s, evt) => HandleActionResult(DiplomacySystem.SignAlliance(state, neighbor.Id, false), () => ShowNeighborReport(neighbor)));
                AddActionButton("توقيع تحالف هجومي", (s, evt) => HandleActionResult(DiplomacySystem.SignAlliance(state, neighbor.Id, true), () => ShowNeighborReport(neighbor)));
            }

            AddActionButton("إرسال هدية (100 ذهب)", (s, evt) => HandleActionResult(DiplomacySystem.SendGift(state, neighbor.Id, 100), () => ShowNeighborReport(neighbor)));
            AddActionButton("طلب قرض سياسي (1000 ذهب)", (s, evt) => HandleActionResult(DiplomacySystem.RequestPoliticalLoan(state, neighbor.Id, 1000), () => ShowNeighborReport(neighbor)));
            AddActionButton("اتهام بدعم متمردين", (s, evt) => HandleActionResult(DiplomacySystem.AccuseOfEspionage(state, neighbor.Id), () => ShowNeighborReport(neighbor)));

            if (neighborIndex >= 0 && !neighbor.HasClaim && neighbor.ClaimableProvinces != null && neighbor.ClaimableProvinces.Count > 0)
            {
                AddActionButton("تزوير مطالبة شرعية", (s, evt) => {
                    ClearDynamicPanel();
                    SetNarrativeText($"اختر المقاطعة التابعة لـ {neighbor.Name} لتزوير مطالبة عليها.");
                    foreach (var province in neighbor.ClaimableProvinces)
                    {
                        AddActionButton($"مطالبة على {province.Name}", (ns, ne) => {
                            var res = IntrigueSystem.ForgeClaim(state, neighborIndex, province.Name);
                            HandleActionResult(res, () => ShowNeighborReport(neighbor));
                        });
                    }
                    AddActionButton("عودة", (ns, ne) => ShowNeighborReport(neighbor));
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                });
            }

            if (neighborIndex >= 0 && neighbor.HasClaim && neighbor.Relation != "حرب")
            {
                AddActionButton($"إعلان الحرب للسيطرة على {neighbor.ClaimedProvince}", (s, evt) => {
                    var res = WarfareSystem.DeclareWar(state, neighborIndex, false);
                    HandleActionResult(res, () => ShowNeighborReport(neighbor));
                });
            }
            else if (neighborIndex >= 0 && neighbor.Relation != "حرب")
            {
                AddActionButton("إعلان حرب ظالمة", (s, evt) => {
                    var res = WarfareSystem.DeclareWar(state, neighborIndex, true);
                    HandleActionResult(res, () => ShowNeighborReport(neighbor));
                });
            }

            if (neighborIndex >= 0 && !neighbor.IsAlly && neighbor.Opinion >= 0)
            {
                AddActionButton("طلب زواج دبلوماسي لإنشاء تحالف (50 هيبة)", (s, evt) => {
                    var res = DynastySystem.ArrangeMarriage(state, neighborIndex);
                    HandleActionResult(res, () => ShowNeighborReport(neighbor));
                });
            }

            var activeTreaties = state.Treaties
                .Where(t => t.IsActive && t.KingdomBId == neighbor.Id)
                .ToList();
            foreach (var treaty in activeTreaties)
            {
                AddActionButton($"كسر معاهدة: {treaty.TreatyType}", (s, evt) => {
                    var res = DiplomacySystem.BreakTreaty(state, treaty.Id);
                    HandleActionResult(res, () => ShowNeighborReport(neighbor));
                });
            }
            
            AddActionButton("عودة", (s, evt) => ShowAdvancedDiplomacyMenu(null, null));
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowReligionMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_religion";
            audio.StopAmbient();
            audio.Play("ambient_magic", true, true);
            audio.PlayPaper();
            
            if (state.HeadCleric == null) state.HeadCleric = new Cleric();
            
            string report = $"الدين ورجل الدين.\n" +
                            $"الشرعية الدينية: {state.ReligiousLegitimacy}/100.\n" +
                            $"اسم الأسقف: {state.HeadCleric.Name}.\n" +
                            $"رأيه بك: {state.HeadCleric.OpinionOfKing}.\n" +
                            $"نفوذه: {state.HeadCleric.Influence}.\n" +
                            $"التوتر الديني: {state.ReligiousTension}/100.";
                            
            SetNarrativeText(report);

            AddActionButton("طلب مباركة حرب من مملكة مجاورة", (s, evt) => {
                var target = state.Neighbors.FirstOrDefault(n => n.Relation == "عدائي" || n.Opinion < 0) ?? state.Neighbors.FirstOrDefault();
                if (target != null)
                    HandleActionResult(ReligionSystem.RequestWarBlessing(state, target.Name), () => ShowReligionMenu(null, null));
                else
                {
                    SetNarrativeText("لا توجد ممالك مناسبة للحرب الآن.");
                    AddActionButton("عودة", (ns, ne) => ShowReligionMenu(null, null));
                    if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                }
            });
            
            AddActionButton("طلب دعم الوريث (150 ذهب)", (s, evt) => HandleActionResult(ReligionSystem.SupportHeir(state), () => ShowReligionMenu(null, null)));
            AddActionButton("دعم الفقراء (200 ذهب)", (s, evt) => HandleActionResult(ReligionSystem.SupportPoor(state), () => ShowReligionMenu(null, null)));
            AddActionButton("تمويل مؤسسة دينية (500 ذهب)", (s, evt) => HandleActionResult(ReligionSystem.FundReligiousInstitution(state), () => ShowReligionMenu(null, null)));
            
            AddActionButton("عودة للقائمة", (s, evt) => RenderSandboxButtons());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowAiMeetingHub()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_ai_meetings";
            state.ReconcileOldSaves();
            AiAgentSystem.EnsureAgents(state);
            SetScreenTitle("الجلسات والاجتماعات الذكية");

            string provider = config.AiProvider == null ? "Disabled" : config.AiProvider.ProviderType.ToString();
            string model = config.AiProvider == null || string.IsNullOrWhiteSpace(config.AiProvider.Model) ? "افتراضي" : config.AiProvider.Model;
            string report =
                "الجلسات الذكية تحفظ ذاكرة منفصلة لكل شخصية، حتى لو استخدمت نموذجاً محلياً واحداً.\n" +
                "في الاجتماع يتحدث الخليفة أولاً، ثم يرد كل وزير أو زوجة أو والٍ أو حاكم جار حسب دوره ومعرفته.\n" +
                "هذه محاضر رأي فقط ولا تغيّر الخزينة أو الحرب أو المعاهدات حتى تختار أمراً تنفيذياً من شاشة النظام المناسبة.\n\n" +
                $"المزود الحالي: {provider}. النموذج: {model}.\n" +
                $"عدد الجلسات المحفوظة: {state.AiConversationSessions.Count}. محاضر الاجتماعات: {state.AiMeetingHistory.Count}.";
            SetNarrativeText(report, false);

            AddActionButton("اجتماع مجلس الحرب والدفاع", (s, evt) => ShowAiMeetingComposer(
                "الحرب والدفاع",
                "أفكر في الاستعداد للحرب أو حماية الحدود بعد تغيّر ميزان القوى سنة 1071. ما رأيكم؟",
                "council",
                () => ShowAiMeetingHub()));
            AddActionButton("اجتماع الاقتصاد والخزينة", (s, evt) => ShowAiMeetingComposer(
                "الاقتصاد والخزينة",
                "أريد زيادة دخل الخلافة دون كسر رضا الرعية أو ثقة التجار. ما الطريق الأفضل؟",
                "council",
                () => ShowAiMeetingHub()));
            AddActionButton("اجتماع السياسة والخلافة", (s, evt) => ShowAiMeetingComposer(
                "السياسة والخلافة",
                "أريد تثبيت شرعية العرش والوريث وتهدئة الولاة. ما الذي ترونه؟",
                "council",
                () => ShowAiMeetingHub()));
            AddActionButton("اجتماع الأمن والاستخبارات", (s, evt) => ShowAiMeetingComposer(
                "الأمن والاستخبارات",
                "أخشى أن تتحرك الأسرار والفصائل ضد العرش. ما الذي يجب مراقبته أولاً؟",
                "council",
                () => ShowAiMeetingHub()));
            AddActionButton("اجتماع مع الزوجات والقصر", (s, evt) => ShowAiMeetingComposer(
                "القصر والورثة",
                "أريد سماع رأيكن في القصر والورثة والتحالفات العائلية قبل أن أقرر.",
                "spouses",
                () => ShowAiMeetingHub()));
            AddActionButton("جلسة مع الولاة", (s, evt) => ShowAiMeetingComposer(
                "الولايات والضرائب",
                "أريد من الولاة أن يقولوا بصراحة ما الذي يهدد ولاء الولايات واستقرارها.",
                "governors",
                () => ShowAiMeetingHub()));
            AddActionButton("جلسة مع الفصائل", (s, evt) => ShowAiMeetingComposer(
                "مطالب الفصائل",
                "أريد سماع مطالبكم قبل أن تتحول العريضة إلى تمرد. تكلموا بوضوح.",
                "factions",
                () => ShowAiMeetingHub()));
            AddActionButton("مخاطبة دولة مجاورة", (s, evt) => ShowNeighborAudienceSelector());
            AddActionButton("عرض سجل الجلسات الفردية", (s, evt) => ShowAiSessionArchive());
            AddActionButton("عرض محاضر الاجتماعات", (s, evt) => ShowAiMeetingHistory());
            AddActionButton("العودة للقصر والمجلس", ShowCourtHub);

            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowAiMeetingComposer(string topic, string presetStatement, string meetingKind, Action returnAction)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_ai_meeting_compose";
            SetScreenTitle("صياغة كلام الخليفة");
            SetNarrativeText($"اكتب ما يريد الخليفة قوله في اجتماع: {topic}.", false);

            var input = new TextBox
            {
                Width = 700,
                Height = 90,
                Multiline = true,
                Font = new Font("Arial", 12),
                Text = presetStatement,
                AccessibleName = "كلام الخليفة في الاجتماع",
                AccessibleRole = AccessibleRole.Text
            };
            dynamicPanel.Controls.Add(input);

            AddActionButton("بدء الاجتماع وقراءة الردود", (s, evt) =>
            {
                var res = RunConfiguredAiMeeting(meetingKind, topic, input.Text);
                HandleActionResult(res, () => ShowAiMeetingHub());
            });
            AddActionButton("إلغاء والعودة", (s, evt) => returnAction());

            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private GameActionResult RunConfiguredAiMeeting(string meetingKind, string topic, string statement)
        {
            config.AiProvider ??= new AiProviderSettings();
            config.AiActors ??= new AiActorSettings();

            return meetingKind switch
            {
                "spouses" => AiMeetingSystem.RunSpouseMeeting(state, topic, statement, config.AiProvider, config.AiActors),
                "governors" => AiMeetingSystem.RunGovernorMeeting(state, topic, statement, config.AiProvider, config.AiActors),
                "factions" => AiMeetingSystem.RunFactionMeeting(state, topic, statement, config.AiProvider, config.AiActors),
                _ => AiMeetingSystem.RunCouncilMeeting(state, topic, statement, config.AiProvider, config.AiActors)
            };
        }

        private void ShowNeighborAudienceSelector()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_ai_neighbor_audience";
            SetScreenTitle("مخاطبة دولة مجاورة");
            SetNarrativeText("اختر الدولة التي تريد مخاطبة حاكمها. ستكون له جلسة مستقلة وذاكرة حوار خاصة به.", false);

            foreach (var neighbor in state.Neighbors)
                AddActionButton(neighbor.Name, (s, evt) => ShowNeighborAudienceComposer(neighbor.Id, () => ShowNeighborAudienceSelector()));

            AddActionButton("العودة للجلسات الذكية", (s, evt) => ShowAiMeetingHub());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowNeighborAudienceComposer(string neighborId, Action returnAction)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_ai_neighbor_audience_compose";
            state.ReconcileOldSaves();
            var neighbor = state.Neighbors.FirstOrDefault(n => n.Id == neighborId || n.Name == neighborId);
            if (neighbor == null)
            {
                SetNarrativeText("لم يتم العثور على الدولة المجاورة.");
                AddActionButton("عودة", (s, evt) => returnAction());
                if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
                return;
            }

            SetScreenTitle("مخاطبة " + neighbor.Name);
            SetNarrativeText($"اكتب رسالة الخليفة إلى {neighbor.Name}. سيرد الحاكم من مصلحة دولته وذاكرته الخاصة.", false);

            var input = new TextBox
            {
                Width = 700,
                Height = 90,
                Multiline = true,
                Font = new Font("Arial", 12),
                Text = $"أيها الحاكم، أريد حديثاً صريحاً بين بغداد و{neighbor.Name}: ما الذي تطلبه لتبقى الحدود آمنة؟",
                AccessibleName = "رسالة الخليفة للحاكم المجاور",
                AccessibleRole = AccessibleRole.Text
            };
            dynamicPanel.Controls.Add(input);

            AddActionButton("إرسال المخاطبة وقراءة الرد", (s, evt) =>
            {
                config.AiProvider ??= new AiProviderSettings();
                config.AiActors ??= new AiActorSettings();
                var res = AiMeetingSystem.RunNeighborAudience(state, neighbor.Id, input.Text, config.AiProvider, config.AiActors);
                HandleActionResult(res, returnAction);
            });
            AddActionButton("إلغاء والعودة", (s, evt) => returnAction());

            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowAiSessionArchive()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_ai_session_archive";
            SetScreenTitle("سجل الجلسات الفردية");
            SetNarrativeText(AiSessionSystem.GetSessionReport(state), false);

            foreach (var session in state.AiConversationSessions.OrderByDescending(s => s.LastDayNumber).Take(24).ToList())
                AddActionButton($"جلسة {session.CharacterName}", (s, evt) => ShowAiSessionDetail(session.Id));

            AddActionButton("العودة للجلسات الذكية", (s, evt) => ShowAiMeetingHub());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowAiSessionDetail(string sessionId)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_ai_session_detail";
            SetScreenTitle("تفاصيل جلسة شخصية");
            SetNarrativeText(AiSessionSystem.GetSessionDetail(state, sessionId), false);

            AddActionButton("مسح هذه الجلسة فقط", (s, evt) =>
            {
                bool removed = AiSessionSystem.ResetSession(state, sessionId);
                SetNarrativeText(removed ? "تم مسح الجلسة. ستبدأ الشخصية جلسة جديدة عند الحوار القادم." : "لم يتم العثور على الجلسة.");
                ShowAiSessionArchive();
            });
            AddActionButton("العودة لسجل الجلسات", (s, evt) => ShowAiSessionArchive());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowAiMeetingHistory()
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_ai_meeting_history";
            SetScreenTitle("محاضر الاجتماعات");
            SetNarrativeText(AiMeetingSystem.GetMeetingHistoryReport(state), false);

            foreach (var meeting in state.AiMeetingHistory.OrderByDescending(m => m.DayNumber).Take(20).ToList())
                AddActionButton($"{meeting.Scope}: {meeting.Topic}", (s, evt) => ShowAiMeetingRecord(meeting.Id));

            AddActionButton("العودة للجلسات الذكية", (s, evt) => ShowAiMeetingHub());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowAiMeetingRecord(string meetingId)
        {
            ClearDynamicPanel();
            state.GameMode = "sandbox_ai_meeting_record";
            SetScreenTitle("محضر اجتماع");
            var meeting = state.AiMeetingHistory.FirstOrDefault(m => m.Id == meetingId);
            SetNarrativeText(meeting == null ? "لم يتم العثور على محضر الاجتماع." : meeting.Transcript, false);
            AddActionButton("العودة للمحاضر", (s, evt) => ShowAiMeetingHistory());
            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void ShowCouncilMeetingMenu(object sender, EventArgs e)
        {
            ClearDynamicPanel();
            state.GameMode = "council_meeting";
            audio.StopAmbient();
            audio.Play("ambient_council", true, true); // Play once

            SetNarrativeText("لقد اجتمع وزراء ومستشارو البلاط الملكي. يرجى اختيار الموضوع الذي تود مناقشته:");

            AddActionButton("مناقشة الشؤون الاقتصادية 💰", (s, evt) => HandleCouncilMeetingTopic("الاقتصاد"));
            AddActionButton("مناقشة الشؤون العسكرية والدفاع ⚔️", (s, evt) => HandleCouncilMeetingTopic("العسكرية"));
            AddActionButton("مناقشة الشؤون الدبلوماسية والداخلية 📜", (s, evt) => HandleCouncilMeetingTopic("السياسة"));
            AddActionButton("مناقشة الاستخبارات والأمن الداخلي 👁️", (s, evt) => HandleCouncilMeetingTopic("الاستخبارات"));
            AddActionButton("إنهاء الاجتماع والعودة", ShowCouncilScreen);

            if (dynamicPanel.Controls.Count > 0) dynamicPanel.Controls[0].Focus();
        }

        private void HandleCouncilMeetingTopic(string topic)
        {
            string preset = topic switch
            {
                "الاقتصاد" => "أريد زيادة دخل الخلافة وتنظيم السوق الموسمي وحماية طرق القوافل دون إثقال الرعية. ما رأيكم؟",
                "العسكرية" => "أفكر في رفع جاهزية الجيش وحماية الحدود، وربما الحرب إن اضطررنا. ما رأي المجلس؟",
                "السياسة" => "أريد تهدئة الولاة وتثبيت شرعية العرش والوريث. ما المخاطر التي ترونها؟",
                "الاستخبارات" => "أريد معرفة ما يخفيه البلاط والولاة والدول المجاورة عن العرش. أين نبدأ؟",
                _ => "أريد رأي المجلس قبل أن أتخذ القرار."
            };

            string title = topic switch
            {
                "العسكرية" => "الشؤون العسكرية والدفاع",
                "السياسة" => "الشؤون الدبلوماسية والداخلية",
                "الاستخبارات" => "الاستخبارات والأمن الداخلي",
                _ => "الشؤون الاقتصادية"
            };

            ShowAiMeetingComposer(title, preset, "council", () => ShowCouncilMeetingMenu(null, null));
        }
    }
}


