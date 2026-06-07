using System;
using System.Collections.Generic;
using KingdomBlind_CSharp.Data;

namespace KingdomBlind_CSharp.Models
{
    public class Province
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Vassal { get; set; }
        public string VassalReligion { get; set; }
        public int Income { get; set; }
        
        // OLD Garrison (Kept for backward compat, but we use LocalGarrison now)
        public int Garrison { get; set; } 
        
        // NEW PROVINCE MILITARY & ECONOMY
        public int LocalGarrison { get; set; } = 200;
        public int RecruitableLevy { get; set; } = 500;
        public int DiplomacySkill { get; set; } = 5;
        public int AdministrativeSkill { get; set; } = 5;


        public int SupplyLimit { get; set; } = 5000;
        public int FortLevel { get; set; } = 1;
        public List<LocalBuilding> Buildings { get; set; } = new List<LocalBuilding>();
        public List<string> ConnectedProvinces { get; set; } = new List<string>();

        public int Satisfaction { get; set; }
        public int Opinion { get; set; }
        public string Religion { get; set; }
        public string Minorities { get; set; }
        public string HolySite { get; set; }
        public string GovernorId { get; set; } = "";
        public string GovernorName { get; set; } = "";
        public bool Occupied { get; set; }
        public string OccupiedBy { get; set; }
        public bool HasRevocationReason { get; set; }
    }

    public class NeighborProvince
    {
        public string Name { get; set; }
        public string Religion { get; set; }
        public string Minorities { get; set; }
        public int Garrison { get; set; }
        public int Income { get; set; }
        public List<string> ConnectedProvinces { get; set; } = new List<string>();
    }

    public class Neighbor
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Capital { get; set; }
        public string Ruler { get; set; }
        public string RulerName { get; set; } // Alias for new requirement
        public int Army { get; set; }
        public int MilitaryStrength { get; set; } = 50;
        public int EconomicStrength { get; set; } = 50;
        public string Religion { get; set; }
        public int Opinion { get; set; }
        public int OpinionOfKing { get; set; }
        public int Trust { get; set; } = 50;
        public int Fear { get; set; } = 0;
        public string Relation { get; set; }
        public string DiplomaticStance { get; set; } = "Neutral"; // Friendly, Neutral, Suspicious, Hostile, Afraid, Opportunistic, Allied, Rival
        public string PoliticalGoal { get; set; } = "";
        public int MilitaryAmbition { get; set; }
        public int FearOfPlayer { get; set; }
        public int AllianceDesire { get; set; }
        public string InternalCrisis { get; set; } = "";
        public int EconomicTrouble { get; set; }
        public string SecretPlan { get; set; } = "";
        public int DaysUntilNextMove { get; set; }
        public int CourtStability { get; set; } = 50;
        public int CouncilCompetence { get; set; } = 50;
        public string DevelopmentFocus { get; set; } = "";
        public List<string> InternalDecisionLog { get; set; } = new List<string>();
        public bool HasClaim { get; set; }
        public bool TradeTreaty { get; set; }
        public bool Alliance { get; set; }
        public int TributePercent { get; set; } = 0;
        public bool IsAtWarWithPlayer { get; set; }
        public bool IsAlly { get; set; }
        public bool IsRival { get; set; }
        public bool HasNonAggressionPact { get; set; }
        public bool IsSupportingPlayerFaction { get; set; }
        public bool IsSuspectedOfEspionage { get; set; }
        public string BorderTarget { get; set; }
        public string ClaimedProvince { get; set; }
        public List<NeighborProvince> ClaimableProvinces { get; set; } = new List<NeighborProvince>();
        public List<string> ActiveTreaties { get; set; } = new List<string>();
        public List<string> ActiveClaims { get; set; } = new List<string>();
    }

    public class CouncilMember
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Task { get; set; }
        public string Target { get; set; }
        public int TurnsLeft { get; set; }
        
        // SPYMASTER SPECIFIC
        public int Loyalty { get; set; } = 50;
        public int Trust { get; set; } = 50;
        public int Ambition { get; set; } = 50;
        public int Influence { get; set; } = 50;
        public int SecrecySkill { get; set; } = 5;
        public int IntrigueSkill { get; set; } = 5;
        public bool IsRightHandOfKing { get; set; } = false;
        public bool IsUnderSuspicion { get; set; } = false;
        public bool HasSecretMonitor { get; set; } = false;
        public bool IsCorrupt { get; set; } = false;
        public int HiddenCorruptionRate { get; set; } = 0;
        public bool CorruptionDiscovered { get; set; } = false;
    }

    public class SiegeData
    {
        public string TargetName { get; set; }
        public int TargetGarrison { get; set; }
        public int PlayerArmy { get; set; }
        public int Turns { get; set; }
        public int Catapults { get; set; } = 0;
        public bool SortieExpected { get; set; } = false; // Deprecated/repurposed to Days
        public string BesiegingArmyId { get; set; }
    }

    public class ActiveWar
    {
        public string Type { get; set; } 
        public int NeighborIdx { get; set; }
        public string TargetProvince { get; set; }
        public int Garrison { get; set; }
        public int Turns { get; set; }
        public int Catapults { get; set; } = 0;
        public bool SortieExpected { get; set; } = false; // Deprecated/repurposed to Days
        public bool AllyCalled { get; set; }
        public int WarScore { get; set; } = 0; // -100 to 100
    }

    public class BuildingTask
    {
        public string ProvinceName { get; set; }
        public string BuildingType { get; set; }
        public int TurnsRemaining { get; set; } // Repurposed to DaysRemaining
    }

    public class ActivePlot
    {
        public string Type { get; set; }
        public string TargetName { get; set; }
        public string TargetProvince { get; set; }
        public int NeighborIdx { get; set; }
        public int Progress { get; set; }
        public int Cost { get; set; }
    }

    public class GameState
    {
        // GLOBAL RESOURCES
        public int Gold { get; set; } = 1000;
        public int SilverCoins { get; set; } = 500;
        public int Workers { get; set; } = 100;
        public int Food { get; set; } = 1000;
        public int Satisfaction { get; set; } = 100;
        public int QueenHappiness { get; set; } = 100;
        
        // TIME SYSTEM
        public int Turn { get; set; } = 1; // Kept for backwards compat
        public TimeState Time { get; set; } = new TimeState();
        
        public string GameMode { get; set; } = "menu";
        public bool SuppressRandomMajorEvents { get; set; } = false;

        public SiegeData SiegeData { get; set; } = null;
        public ActiveWar ActiveWar { get; set; } = null;

        // RULER & DYNASTY
        public string DynastyName { get; set; } = "السلجوقي";
        public string RulerName { get; set; } = "السلطان ملك شاه";
        public int RulerAge { get; set; } = 18;
        public bool RulerIsDead { get; set; } = false;
        public int RulerStress { get; set; } = 0;
        public List<string> RulerTraits { get; set; } = new List<string> { "عادل", "شجاع" };
        public int Piety { get; set; } = 100;
        public int Prestige { get; set; } = 100;
        public int DynastyLevel { get; set; } = 1;
        public List<Spouse> Wives { get; set; } = new List<Spouse>();
        
        // Backward compatibility
        public string SpouseName 
        { 
            get { return Wives != null && Wives.Count > 0 ? Wives[0].Name : null; }
            set {
                if (value != null && Wives == null)
                {
                    Wives = new List<Spouse>();
                }

                if (value != null && Wives.Count == 0)
                {
                    Wives.Add(new Spouse { Name = value, OriginType = "Unknown" });
                }
                else if (value != null && Wives.Count > 0)
                {
                    Wives[0].Name = value;
                }
            }
        }
        public int SpouseOpinion { get; set; } = 100;
        public string HeirName { get; set; } = null;
        public int HeirAge { get; set; } = 0;
        
        public List<Disaster> ActiveDisasters { get; set; } = new List<Disaster>();
        public List<Child> Children { get; set; } = new List<Child>();
        public List<Province> Provinces { get; set; } = new List<Province>();
        public List<Neighbor> Neighbors { get; set; } = new List<Neighbor>();
        public Dictionary<string, CouncilMember> Council { get; set; } = new Dictionary<string, CouncilMember>();
        public FirstMinister FirstMinister { get; set; } = new FirstMinister();
        public Cleric HeadCleric { get; set; } = new Cleric();
        public Dictionary<string, int> MinisterBudgets { get; set; } = new Dictionary<string, int> { { "first_minister", 0 }, { "steward", 0 }, { "marshal", 0 }, { "spymaster", 0 } };

        // RELIGION
        public int ReligiousLegitimacy { get; set; } = 50;
        public int ClergyOpinion { get; set; } = 50;
        public int FaithStability { get; set; } = 50;
        public int ClericInfluence { get; set; } = 20;
        public int ReligiousTension { get; set; } = 0;
        public List<string> ActiveReligiousEvents { get; set; } = new List<string>();
        public List<string> ReligiousLaws { get; set; } = new List<string>();

        // TREATIES
        public List<DiplomaticTreaty> Treaties { get; set; } = new List<DiplomaticTreaty>();
        
        // TRACKING SYSTEMS
        public List<BuildingTask> BuildingQueue { get; set; } = new List<BuildingTask>();
        public List<ActivePlot> ActivePlots { get; set; } = new List<ActivePlot>();
        public string TaxLevel { get; set; } = "متوسط"; 
        public Dictionary<string, int> HeirSkills { get; set; } = new Dictionary<string, int> { {"عسكري", 0}, {"اقتصادي", 0}, {"دبلوماسي", 0}, {"ديني", 0} };
        public List<string> TurnWarnings { get; set; } = new List<string>();

        // LIVING REALM
        public List<PoliticalMemory> PoliticalMemories { get; set; } = new List<PoliticalMemory>();
        public List<RoyalPromise> RoyalPromises { get; set; } = new List<RoyalPromise>();
        public List<LivingRealmEvent> LivingRealmLog { get; set; } = new List<LivingRealmEvent>();
        public Dictionary<string, int> RoyalReputationScores { get; set; } = new Dictionary<string, int>
        {
            { "Just", 0 },
            { "Deceptive", 0 },
            { "Warrior", 0 },
            { "Cruel", 0 },
            { "Generous", 0 },
            { "Pious", 0 },
            { "PromiseKeeper", 0 },
            { "OathBreaker", 0 },
            { "TradeProtector", 0 }
        };

        // GRAND STRATEGY LAYER
        public List<RealmCharacter> RealmCharacters { get; set; } = new List<RealmCharacter>();
        public List<CharacterSecret> CharacterSecrets { get; set; } = new List<CharacterSecret>();
        public List<PoliticalHook> PoliticalHooks { get; set; } = new List<PoliticalHook>();
        public List<CharacterClaim> CharacterClaims { get; set; } = new List<CharacterClaim>();
        public List<FeudalContract> FeudalContracts { get; set; } = new List<FeudalContract>();
        public List<ActiveScheme> ActiveSchemes { get; set; } = new List<ActiveScheme>();
        public List<EventChain> EventChains { get; set; } = new List<EventChain>();
        public List<ReignObjective> ReignObjectives { get; set; } = new List<ReignObjective>();
        public List<CharacterObjective> CharacterObjectives { get; set; } = new List<CharacterObjective>();
        public List<DynastyChronicleEntry> DynastyChronicle { get; set; } = new List<DynastyChronicleEntry>();
        public List<AiAgentProfile> AiAgentProfiles { get; set; } = new List<AiAgentProfile>();
        public DelegatedAuthoritySettings DelegatedAuthoritySettings { get; set; } = new DelegatedAuthoritySettings();
        public List<AiActionRequest> AiProposalQueue { get; set; } = new List<AiActionRequest>();
        public List<AiActionLogEntry> AiActionLog { get; set; } = new List<AiActionLogEntry>();
        public List<AiConversationSession> AiConversationSessions { get; set; } = new List<AiConversationSession>();
        public List<AiMeetingRecord> AiMeetingHistory { get; set; } = new List<AiMeetingRecord>();
        public int DynastyGlory { get; set; } = 0;
        public int RoyalDirectorCooldownDays { get; set; } = 0;
        public string LastRoyalDirectorEventKey { get; set; } = "";
        public SuccessionLawType SuccessionLaw { get; set; } = SuccessionLawType.DesignatedHeir;
        public CrownAuthorityLevel CrownAuthority { get; set; } = CrownAuthorityLevel.Limited;
        public WarGoal CurrentWarGoal { get; set; } = null;

        // NEW GRAND STRATEGY ENTITIES
        public int Army
        {
            get => Armies != null ? System.Linq.Enumerable.Sum(Armies, a => a.TotalSoldiers) : 0;
            set { if (Armies != null && Armies.Count > 0) Armies[0].TotalSoldiers = value; }
        }
        public List<Army> Armies { get; set; } = new List<Army>();
        public List<Army> EnemyArmies { get; set; } = new List<Army>();
        
        // INTERNAL POLITICS
        public List<Governor> Governors { get; set; } = new List<Governor>();
        public List<Faction> Factions { get; set; } = new List<Faction>();
        public List<ReinforcementOrder> Reinforcements { get; set; } = new List<ReinforcementOrder>();

        // ROYAL INTELLIGENCE
        public List<SpyNetwork> SpyNetworks { get; set; } = new List<SpyNetwork>();
        public List<IntelligenceOperation> IntelligenceOperations { get; set; } = new List<IntelligenceOperation>();
        public int CounterIntelligenceLevel { get; set; } = 10;
        public List<string> SecretReports { get; set; } = new List<string>();

        // ECONOMY & TRADE
        public List<Loan> Loans { get; set; } = new List<Loan>();
        public List<string> ProtectedTradeRoutes { get; set; } = new List<string>();
        public int MerchantsTrust { get; set; } = 50;
        public int ActiveSupplyContracts { get; set; } = 0;
        public int SeasonalMarketDaysLeft { get; set; } = 0;

        // TTS Settings
        public string SpeechProvider { get; set; } = "sapi5";
        public bool UseSuperTonic { get; set; } = false;
        public double SuperTonicSpeed { get; set; } = 1.0;
        public bool SapiReadsEvents { get; set; } = true;
        public bool SapiReadsNPCs { get; set; } = false;
        public string SapiVoiceName { get; set; } = "";

        public GameState()
        {
            // Initialization
            Provinces.Add(new Province { Name = "دمشق", Vassal = "الأمير محمود", VassalReligion = "سُني أشعري", Income = 60, Garrison = 40, LocalGarrison = 800, RecruitableLevy=1000, FortLevel=3, Satisfaction = 80, Opinion = 40, Religion = "سُني أشعري", Minorities = "مسيحي أرثوذكسي", HolySite = "الجامع الأموي الكبير", ConnectedProvinces=new List<string>{"حمص", "القدس"} });
            Provinces.Add(new Province { Name = "القدس", Vassal = "الأمير كمال", VassalReligion = "سُني أشعري", Income = 40, Garrison = 30, LocalGarrison = 500, FortLevel=2, Satisfaction = 75, Opinion = 20, Religion = "مسيحي أرثوذكسي", Minorities = "سُني أشعري، شيعي إسماعيلي، يهودي", HolySite = "المسجد الأقصى وقبة الصخرة / كنيسة القيامة", ConnectedProvinces=new List<string>{"دمشق"} });
            Provinces.Add(new Province { Name = "حمص", Vassal = "الأمير سعد", VassalReligion = "سُني أشعري", Income = 30, Garrison = 20, LocalGarrison= 300, FortLevel=1, Satisfaction = 65, Opinion = -15, Religion = "سُني أشعري", Minorities = "شيعي علوي", ConnectedProvinces=new List<string>{"دمشق", "حلب"} });
            Provinces.Add(new Province { Name = "حلب", Vassal = "الأمير محمود بن صالح", VassalReligion = "شيعي إمامي", Income = 50, Garrison = 35, LocalGarrison= 600, FortLevel=2, Satisfaction = 75, Opinion = 50, Religion = "شيعي إمامي", Minorities = "سُني أشعري، مسيحي سرياني يعقوبي", ConnectedProvinces=new List<string>{"حمص", "بغداد"} });
            Provinces.Add(new Province { Name = "بغداد", Vassal = "الأمير منصور", VassalReligion = "سُني أشعري", Income = 80, Garrison = 50, LocalGarrison= 1000, FortLevel=3, Satisfaction = 85, Opinion = 60, Religion = "سُني أشعري", Minorities = "مسيحي نسطوري، شيعي إمامي", HolySite = "دار الخلافة وبيت الحكمة", ConnectedProvinces=new List<string>{"حلب"} });

            Neighbors.Add(new Neighbor { Name = "الدولة السلجوقية", Capital = "أصفهان", Ruler = "السلطان ألب أرسلان", Army = 450, Religion = "سُني أشعري", Opinion = -20, Relation = "هدنة", BorderTarget = "بغداد", ClaimableProvinces = new List<NeighborProvince> { new NeighborProvince { Name = "الموصل", Religion = "سُني أشعري", Garrison = 60, Income = 45, ConnectedProvinces=new List<string>{"بغداد"} }, new NeighborProvince { Name = "الجزيرة", Religion = "سُني أشعري", Garrison = 40, Income = 30 } } });
            Neighbors.Add(new Neighbor { Name = "الدولة الفاطمية", Capital = "القاهرة", Ruler = "الخليفة المستنصر بالله", Army = 380, Religion = "شيعي إسماعيلي", Opinion = -60, Relation = "عدائية", BorderTarget = "القدس", ClaimableProvinces = new List<NeighborProvince> { new NeighborProvince { Name = "عسقلان", Religion = "شيعي إسماعيلي", Garrison = 50, Income = 35, ConnectedProvinces=new List<string>{"القدس"} }, new NeighborProvince { Name = "دمياط", Religion = "شيعي إسماعيلي", Garrison = 45, Income = 40 } } });
            Neighbors.Add(new Neighbor { Name = "الدولة البيزنطية", Capital = "القسطنطينية", Ruler = "الإمبراطور ميخائيل السابع", Army = 500, Religion = "مسيحي أرثوذكسي", Opinion = -40, Relation = "حياد", BorderTarget = "حمص", ClaimableProvinces = new List<NeighborProvince> { new NeighborProvince { Name = "أنطاكية", Religion = "مسيحي أرثوذكسي", Garrison = 70, Income = 50, ConnectedProvinces=new List<string>{"حلب","حمص"} }, new NeighborProvince { Name = "اللاذقية", Religion = "مسيحي أرثوذكسي", Garrison = 45, Income = 35 } } });
            Neighbors.Add(new Neighbor { Name = "إمارات الشام", Capital = "حلب", Ruler = "الأمير محمود بن مرداس", Army = 120, Religion = "شيعي إمامي", Opinion = 50, Relation = "موالية", BorderTarget = "حلب", ClaimableProvinces = new List<NeighborProvince> { new NeighborProvince { Name = "الرقة", Religion = "شيعي إمامي", Garrison = 30, Income = 25 } } });

            Council.Add("first_minister", new CouncilMember { Name = "نظام الملك", Title = "الوزير الأول", Task = "إدارة شؤون الدولة", Target = "أصفهان", TurnsLeft = 0 });
            Council.Add("steward", new CouncilMember { Name = "وزير المالية", Title = "وزير المالية", Task = "إدارة الخزينة", Target = "الأسواق", TurnsLeft = 0 });
            Council.Add("marshal", new CouncilMember { Name = "قائد الجند", Title = "قائد الجند", Task = "حشد العسكر", Target = null, TurnsLeft = 0 });
            Council.Add("spymaster", new CouncilMember { Name = "مدير الاستخبارات", Title = "مدير الاستخبارات", Task = "كشف المؤامرات", Target = null, TurnsLeft = 0 });
            Council.Add("chaplain", new CouncilMember { Name = "كبير القضاة", Title = "كبير القضاة", Task = "توطيد العلاقات الدينية", Target = "المساجد", TurnsLeft = 0 });
        
            // Initial Army
            Armies.Add(new Army { Name="جيش العاصمة الميداني", CommanderName="قائد الجيش", CurrentProvince="دمشق", TotalSoldiers=1500 });

            ReconcileOldSaves();
        }

        public void ReconcileOldSaves()
        {
            if (Time == null) Time = new TimeState();
            if (Provinces == null) Provinces = new List<Province>();
            if (Neighbors == null) Neighbors = new List<Neighbor>();
            if (Council == null) Council = new Dictionary<string, CouncilMember>();
            if (FirstMinister == null) FirstMinister = new FirstMinister();
            if (HeadCleric == null) HeadCleric = new Cleric();
            if (RulerTraits == null) RulerTraits = new List<string>();
            if (ActiveReligiousEvents == null) ActiveReligiousEvents = new List<string>();
            if (ReligiousLaws == null) ReligiousLaws = new List<string>();
            if (Wives == null) Wives = new List<Spouse>();
            if (Children == null) Children = new List<Child>();
            if (Treaties == null) Treaties = new List<DiplomaticTreaty>();
            if (BuildingQueue == null) BuildingQueue = new List<BuildingTask>();
            if (ActivePlots == null) ActivePlots = new List<ActivePlot>();
            if (HeirSkills == null) HeirSkills = new Dictionary<string, int>();
            if (TurnWarnings == null) TurnWarnings = new List<string>();
            if (PoliticalMemories == null) PoliticalMemories = new List<PoliticalMemory>();
            if (RoyalPromises == null) RoyalPromises = new List<RoyalPromise>();
            if (LivingRealmLog == null) LivingRealmLog = new List<LivingRealmEvent>();
            if (RoyalReputationScores == null) RoyalReputationScores = new Dictionary<string, int>();
            if (Armies == null) Armies = new List<Army>();
            if (EnemyArmies == null) EnemyArmies = new List<Army>();
            if (Governors == null) Governors = new List<Governor>();
            if (Factions == null) Factions = new List<Faction>();
            if (Reinforcements == null) Reinforcements = new List<ReinforcementOrder>();
            if (SpyNetworks == null) SpyNetworks = new List<SpyNetwork>();
            if (IntelligenceOperations == null) IntelligenceOperations = new List<IntelligenceOperation>();
            if (SecretReports == null) SecretReports = new List<string>();
            if (Loans == null) Loans = new List<Loan>();
            if (ProtectedTradeRoutes == null) ProtectedTradeRoutes = new List<string>();
            if (AiConversationSessions == null) AiConversationSessions = new List<AiConversationSession>();
            if (AiMeetingHistory == null) AiMeetingHistory = new List<AiMeetingRecord>();

            if (MinisterBudgets == null) MinisterBudgets = new Dictionary<string, int> { { "first_minister", 0 }, { "steward", 0 }, { "marshal", 0 }, { "spymaster", 0 } };

            EnsureCouncilMember("first_minister", "نظام الملك", "الوزير الأول");
            EnsureCouncilMember("steward", "وزير المالية", "وزير المالية");
            EnsureCouncilMember("marshal", "قائد الجند", "قائد الجند");
            EnsureCouncilMember("spymaster", "مدير الاستخبارات", "مدير الاستخبارات");
            EnsureCouncilMember("chaplain", "كبير القضاة", "كبير القضاة");
            EnsureCouncilMember("chancellor", "الأمير خالد", "المستشار الدبلوماسي");
            
            // Fix old saves that might have the chancellor targeting Aleppo by default
            if (Council.ContainsKey("chancellor"))
            {
                var chan = Council["chancellor"];
                if (chan.Target == "حلب" && chan.Task == "تحسين العلاقات")
                {
                    chan.Target = null;
                    chan.Task = "انتظار الأوامر";
                }
            }
            EnsureReputationKey("Just");
            EnsureReputationKey("Deceptive");
            EnsureReputationKey("Warrior");
            EnsureReputationKey("Cruel");
            EnsureReputationKey("Generous");
            EnsureReputationKey("Pious");
            EnsureReputationKey("PromiseKeeper");
            EnsureReputationKey("OathBreaker");
            EnsureReputationKey("TradeProtector");

            var random = new Random();
            string[] fakeNames = { "الأمير القائد", "الوالي زيد", "الحاكم مروان", "القائد سيف", "الأمير عمر" };

            foreach (var p in Provinces)
            {
                if (string.IsNullOrEmpty(p.Id)) p.Id = Guid.NewGuid().ToString();
                if (p.Buildings == null) p.Buildings = new List<LocalBuilding>();
                if (p.ConnectedProvinces == null) p.ConnectedProvinces = new List<string>();
                if (p.LocalGarrison == 0) p.LocalGarrison = Math.Max(200, p.Garrison * 10);
                if (p.RecruitableLevy == 0) p.RecruitableLevy = 500;
                if (p.SupplyLimit == 0) p.SupplyLimit = 5000;
                if (p.FortLevel == 0) p.FortLevel = 1;
                if (string.IsNullOrWhiteSpace(p.GovernorId)) p.GovernorId = "gov_" + p.Id;
            }

            for (int neighborIndex = 0; neighborIndex < Neighbors.Count; neighborIndex++)
            {
                var n = Neighbors[neighborIndex];
                if (string.IsNullOrWhiteSpace(n.Id)) n.Id = Guid.NewGuid().ToString();
                if (string.IsNullOrWhiteSpace(n.RulerName)) n.RulerName = n.Ruler;
                if (n.ClaimableProvinces == null) n.ClaimableProvinces = new List<NeighborProvince>();
                if (n.ActiveTreaties == null) n.ActiveTreaties = new List<string>();
                if (n.ActiveClaims == null) n.ActiveClaims = new List<string>();
                if (n.InternalDecisionLog == null) n.InternalDecisionLog = new List<string>();
                if (n.CourtStability <= 0) n.CourtStability = Math.Clamp(45 + n.Trust / 4 - Math.Max(0, -n.Opinion / 4), 10, 100);
                if (n.CouncilCompetence <= 0) n.CouncilCompetence = Math.Clamp(40 + n.EconomicStrength / 5 + n.MilitaryStrength / 8, 10, 100);
                if (string.IsNullOrWhiteSpace(n.DevelopmentFocus)) n.DevelopmentFocus = n.EconomicTrouble > 60 ? "إصلاح الأزمة الداخلية" : (n.MilitaryAmbition > 70 ? "تقوية الجيش" : "تنمية الأسواق");
                if (string.IsNullOrWhiteSpace(n.PoliticalGoal)) n.PoliticalGoal = n.Opinion < -30 ? "إضعاف نفوذ اللاعب" : (n.Opinion > 35 ? "تعميق العلاقات" : "حفظ التوازن");
                if (n.MilitaryAmbition <= 0) n.MilitaryAmbition = Math.Clamp(40 + (n.Army / 25) - Math.Max(0, n.Opinion / 3), 10, 100);
                if (n.FearOfPlayer <= 0) n.FearOfPlayer = Math.Clamp((Army - n.Army) / 20 + 35, 0, 100);
                if (n.AllianceDesire <= 0) n.AllianceDesire = Math.Clamp(n.Opinion + n.Trust / 2, 0, 100);
                if (string.IsNullOrWhiteSpace(n.InternalCrisis)) n.InternalCrisis = n.EconomicTrouble > 65 ? "ضائقة اقتصادية" : "لا توجد أزمة معلنة";
                if (n.EconomicTrouble <= 0) n.EconomicTrouble = Math.Clamp(50 - n.EconomicStrength + Math.Max(0, -n.Opinion / 3), 0, 100);
                if (string.IsNullOrWhiteSpace(n.SecretPlan)) n.SecretPlan = n.Opinion < -40 ? "تمويل فصيل داخلي" : (n.MilitaryAmbition > 70 ? "تجهيز حملة حدودية" : "مراقبة الحدود");
                if (n.DaysUntilNextMove <= 0) n.DaysUntilNextMove = 12 + (neighborIndex * 5);
                foreach (var claimable in n.ClaimableProvinces)
                    if (claimable.ConnectedProvinces == null) claimable.ConnectedProvinces = new List<string>();
            }

            foreach (var spouse in Wives)
            {
                if (string.IsNullOrWhiteSpace(spouse.Id)) spouse.Id = Guid.NewGuid().ToString();
                if (string.IsNullOrWhiteSpace(spouse.OriginType)) spouse.OriginType = "Unknown";
                if (spouse.OpinionOfKing <= 0) spouse.OpinionOfKing = SpouseOpinion > 0 ? SpouseOpinion : 50;
                if (spouse.Trust <= 0) spouse.Trust = 50;
                if (spouse.Influence <= 0) spouse.Influence = 10;
                if (spouse.CurrentTask == string.Empty) spouse.CurrentTask = null;
                if (spouse.DutyDaysRemaining < 0) spouse.DutyDaysRemaining = 0;
                if (!spouse.IsPregnant) spouse.PregnancyDaysLeft = 0;
                if (string.IsNullOrWhiteSpace(spouse.CourtGoal)) spouse.CourtGoal = spouse.IsMotherOfHeir ? "حماية موقع ابنها" : "زيادة نفوذها في البلاط";
                if (spouse.DaysUntilNextCourtMove <= 0) spouse.DaysUntilNextCourtMove = 30;
            }

            foreach (var child in Children)
            {
                if (string.IsNullOrWhiteSpace(child.Id)) child.Id = Guid.NewGuid().ToString();
            }

            foreach (var treaty in Treaties)
            {
                if (string.IsNullOrWhiteSpace(treaty.Id)) treaty.Id = Guid.NewGuid().ToString();
                if (treaty.DurationDays <= 0 && treaty.EndDateDays > treaty.StartDateDays)
                    treaty.DurationDays = treaty.EndDateDays - treaty.StartDateDays;
            }

            foreach (var gov in Governors)
            {
                if (string.IsNullOrWhiteSpace(gov.Id)) gov.Id = Guid.NewGuid().ToString();
                if (gov.Traits == null) gov.Traits = new List<string>();
                if (string.IsNullOrWhiteSpace(gov.CurrentGoal)) gov.CurrentGoal = gov.Ambition > 70 ? "زيادة نفوذ المقاطعة" : (gov.Loyalty > 75 ? "حفظ الاستقرار" : "انتظار فرصة سياسية");
                if (string.IsNullOrWhiteSpace(gov.SecretPlan)) gov.SecretPlan = gov.OpinionOfKing < -40 ? "البحث عن حلفاء ساخطين" : "لا توجد خطة مكشوفة";
                if (gov.DaysUntilNextMove <= 0) gov.DaysUntilNextMove = 25;
                if (string.IsNullOrWhiteSpace(gov.ProvinceId) && !string.IsNullOrWhiteSpace(gov.ProvinceName))
                {
                    var province = Provinces.Find(p => p.Name == gov.ProvinceName);
                    if (province != null) gov.ProvinceId = province.Id;
                }

                if (!string.IsNullOrWhiteSpace(gov.ProvinceId))
                {
                    var province = Provinces.Find(p => p.Id == gov.ProvinceId);
                    if (province != null)
                    {
                        gov.ProvinceName = province.Name;
                        province.GovernorId = gov.Id;
                        province.GovernorName = gov.Name;
                    }
                }
            }

            foreach (var faction in Factions)
            {
                if (string.IsNullOrWhiteSpace(faction.Id)) faction.Id = Guid.NewGuid().ToString();
                if (faction.MemberGovernorIds == null) faction.MemberGovernorIds = new List<string>();
            }

            foreach (var network in SpyNetworks)
            {
                if (string.IsNullOrWhiteSpace(network.Id)) network.Id = Guid.NewGuid().ToString();
                if (network.Strength <= 0) network.Strength = 10;
                if (network.Secrecy <= 0) network.Secrecy = 10;
                if (network.Infiltration <= 0) network.Infiltration = 5;
                if (network.Analysis <= 0) network.Analysis = 5;
                if (network.DaysUntilNextReport <= 0) network.DaysUntilNextReport = 7;
            }

            foreach (var operation in IntelligenceOperations)
            {
                if (string.IsNullOrWhiteSpace(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            }

            foreach (var loan in Loans)
            {
                if (string.IsNullOrWhiteSpace(loan.Id)) loan.Id = Guid.NewGuid().ToString();
            }

            foreach (var memory in PoliticalMemories)
            {
                if (string.IsNullOrWhiteSpace(memory.Id)) memory.Id = Guid.NewGuid().ToString();
            }

            foreach (var promise in RoyalPromises)
            {
                if (string.IsNullOrWhiteSpace(promise.Id)) promise.Id = Guid.NewGuid().ToString();
            }

            foreach (var realmEvent in LivingRealmLog)
            {
                if (string.IsNullOrWhiteSpace(realmEvent.Id)) realmEvent.Id = Guid.NewGuid().ToString();
            }

            if (Armies.Count == 0)
            {
                Armies.Add(new Army
                {
                    Name = "جيش العاصمة الميداني",
                    CommanderName = "قائد الجيش",
                    CurrentProvince = "دمشق",
                    TotalSoldiers = 500
                });
            }

            foreach (var army in Armies)
            {
                if (string.IsNullOrWhiteSpace(army.Id)) army.Id = Guid.NewGuid().ToString();
                if (string.IsNullOrWhiteSpace(army.Name)) army.Name = "جيش ملكي";
                if (string.IsNullOrWhiteSpace(army.CurrentOrder)) army.CurrentOrder = "Idle";
                if (army.TotalSoldiers <= 0) army.TotalSoldiers = 500;
            }

            foreach (var enemyArmy in EnemyArmies)
            {
                if (string.IsNullOrWhiteSpace(enemyArmy.Id)) enemyArmy.Id = Guid.NewGuid().ToString();
                if (string.IsNullOrWhiteSpace(enemyArmy.Name)) enemyArmy.Name = "جيش معاد";
                if (enemyArmy.TotalSoldiers < 0) enemyArmy.TotalSoldiers = 0;
            }

            foreach (var p in Provinces)
            {
                if (!Governors.Exists(g => g.ProvinceId == p.Id))
                {
                    var governor = new Governor
                    {
                        Id = p.GovernorId,
                        Name = string.IsNullOrWhiteSpace(p.GovernorName) ? fakeNames[random.Next(fakeNames.Length)] : p.GovernorName,
                        ProvinceId = p.Id,
                        ProvinceName = p.Name,
                        Age = random.Next(30, 60)
                    };
                    Governors.Add(governor);
                    p.GovernorId = governor.Id;
                    p.GovernorName = governor.Name;
                }
            }

            SaveMigrationService.Migrate(this);
        }

        private void EnsureCouncilMember(string key, string name, string title)
        {
            if (!Council.ContainsKey(key) || Council[key] == null)
                Council[key] = new CouncilMember();

            if (string.IsNullOrWhiteSpace(Council[key].Name)) Council[key].Name = name;
            if (string.IsNullOrWhiteSpace(Council[key].Title)) Council[key].Title = title;
            
            // Force replace hardcoded names from old saves with generic titles
            var badNames = new System.Collections.Generic.List<string> { "الشيخ عبد الرحمن", "الوزير جعفر", "قائد الجند طارق", "القائد طارق", "القائد القائد", "الأمير خالد", "نظام الملك", "عائشة" };
            foreach (var bad in badNames)
            {
                if (Council[key].Name.Contains(bad))
                {
                    Council[key].Name = title;
                }
            }
        }

        private void EnsureReputationKey(string key)
        {
            if (!RoyalReputationScores.ContainsKey(key))
                RoyalReputationScores[key] = 0;
        }
    }
}
