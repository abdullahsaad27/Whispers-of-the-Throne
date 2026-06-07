using KingdomBlind_CSharp.Models;
using KingdomBlind_CSharp.Systems;
using Xunit;

namespace KingdomBlind_CSharp.Tests;

public class StabilizationTests
{
    [Fact]
    public void AdvanceWeek_AdvancesSevenActualDays_WhenNoImportantStopOccurs()
    {
        var state = new GameState();
        state.Time.IsPaused = true;
        state.SuppressRandomMajorEvents = true;
        foreach (var neighbor in state.Neighbors) neighbor.DaysUntilNextMove = 90;
        foreach (var governor in state.Governors) governor.DaysUntilNextMove = 90;
        foreach (var wife in state.Wives) wife.DaysUntilNextCourtMove = 90;

        var result = CalendarTimeSystem.AdvanceWeek(state);

        Assert.True(result.Success);
        Assert.Equal(8, state.Time.Day);
        Assert.Contains("تم تقديم 7 من أصل 7 يوم", result.MainMessage);
        Assert.False(result.ShouldPauseTime);
    }

    [Fact]
    public void AdvanceMonth_StopsOnBirth_AndCreatesChild()
    {
        var state = new GameState();
        state.SuppressRandomMajorEvents = true;
        var wife = new Spouse
        {
            Name = "الملكة ليلى",
            OriginType = "Unknown",
            IsPregnant = true,
            PregnancyDaysLeft = 1
        };
        state.Wives.Add(wife);

        var result = CalendarTimeSystem.AdvanceMonth(state);

        Assert.True(result.ShouldPauseTime);
        Assert.Single(state.Children);
        Assert.False(wife.IsPregnant);
        Assert.Contains("ولدت", result.MainMessage);
    }

    [Fact]
    public void ReconcileOldSaves_RecreatesCriticalListsAndIds()
    {
        var state = new GameState
        {
            Wives = null!,
            Children = null!,
            Treaties = null!,
            Armies = null!,
            EnemyArmies = null!,
            Governors = null!,
            Factions = null!,
            SpyNetworks = null!,
            Loans = null!
        };

        state.ReconcileOldSaves();

        Assert.NotNull(state.Wives);
        Assert.NotNull(state.Children);
        Assert.NotNull(state.Treaties);
        Assert.NotEmpty(state.Armies);
        Assert.NotEmpty(state.Governors);
        Assert.All(state.Provinces, p => Assert.False(string.IsNullOrWhiteSpace(p.GovernorId)));
    }

    [Fact]
    public void DeclareWar_Fails_WhenAnotherWarIsActive()
    {
        var state = new GameState();
        state.ActiveWar = new ActiveWar { Type = "conquest", NeighborIdx = 0, TargetProvince = "الموصل" };

        var result = WarfareSystem.DeclareWar(state, 1, true);

        Assert.False(result.Success);
        Assert.Contains("حرب قائمة", result.MainMessage);
    }

    [Fact]
    public void DeclareWar_Fails_AgainstNonAggressionPact()
    {
        var state = new GameState();
        var neighbor = state.Neighbors[3];
        neighbor.Opinion = 60;
        neighbor.Trust = 60;

        var pact = DiplomacySystem.SignNonAggressionPact(state, neighbor.Id);
        var result = WarfareSystem.DeclareWar(state, 3, true);

        Assert.True(pact.Success);
        Assert.False(result.Success);
        Assert.Contains("عدم اعتداء", result.MainMessage);
    }

    [Fact]
    public void DiplomaticMarriage_AddsWifeAndTreaty_AndSynchronizesAllyFlags()
    {
        var state = new GameState();
        var neighbor = state.Neighbors[3];
        neighbor.Opinion = 60;
        state.Prestige = 100;

        var result = DynastySystem.ArrangeMarriage(state, 3);

        Assert.True(result.Success);
        Assert.Contains(state.Wives, w => w.OriginType == "ForeignKingdom" && w.OriginId == neighbor.Id);
        Assert.True(neighbor.IsAlly);
        Assert.True(neighbor.Alliance);
        Assert.True(DiplomacySystem.HasActiveTreaty(state, neighbor.Id, "MarriageAlliance"));
    }

    [Fact]
    public void ExclusiveWifeDuty_PreventsDuplicateTargetAssignments()
    {
        var state = new GameState();
        var wife1 = new Spouse { Name = "الملكة الأولى", OriginType = "Unknown" };
        var wife2 = new Spouse { Name = "الملكة الثانية", OriginType = "Unknown" };
        state.Wives.Add(wife1);
        state.Wives.Add(wife2);

        bool first = DynastySystem.TryAssignExclusiveDuty(state, wife1.Id, "CalmProvince", "province_1", "دمشق", 14, out _);
        bool second = DynastySystem.TryAssignExclusiveDuty(state, wife2.Id, "PoliticalMediation", "province_1", "دمشق", 14, out var message);

        Assert.True(first);
        Assert.False(second);
        Assert.Contains("نفس الهدف السياسي", message);
    }

    [Fact]
    public void MonthlyEconomy_AddsTradeIncomeOnceAtMonthEnd()
    {
        var state = new GameState();
        state.SuppressRandomMajorEvents = true;
        state.Time.Day = 29;
        var neighbor = state.Neighbors[3];
        neighbor.Opinion = 60;

        DiplomacySystem.OfferTrade(state, 3);
        int beforeGold = state.Gold;
        int provinceIncome = state.Provinces.Where(p => !p.Occupied).Sum(p => p.Income);

        CalendarTimeSystem.AdvanceDay(state);

        Assert.Equal(beforeGold + provinceIncome + 30, state.Gold);
    }

    [Fact]
    public void UpgradeNetwork_FailsClearly_WhenGoldIsInsufficient()
    {
        var state = new GameState();
        var network = new SpyNetwork { Name = "شبكة اختبار", TargetType = "RoyalCourt", TargetId = "court" };
        state.SpyNetworks.Add(network);
        state.Gold = 0;

        var result = IntelligenceSystem.UpgradeNetwork(state, network.Id, "تجنيد مخبرين محليين");

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.MainMessage));
        Assert.Equal(10, network.Strength);
    }

    [Fact]
    public void ReconcileOldSaves_InitializesLivingRealmState()
    {
        var state = new GameState
        {
            PoliticalMemories = null!,
            RoyalPromises = null!,
            LivingRealmLog = null!,
            RoyalReputationScores = null!
        };

        state.ReconcileOldSaves();

        Assert.NotNull(state.PoliticalMemories);
        Assert.NotNull(state.RoyalPromises);
        Assert.NotNull(state.LivingRealmLog);
        Assert.Contains("PromiseKeeper", state.RoyalReputationScores.Keys);
        Assert.All(state.Neighbors, n => Assert.False(string.IsNullOrWhiteSpace(n.PoliticalGoal)));
        Assert.All(state.Governors, g => Assert.False(string.IsNullOrWhiteSpace(g.CurrentGoal)));
    }

    [Fact]
    public void AddMemory_ChangesActorAndAppearsInLivingRealmReport()
    {
        var state = new GameState();
        var governor = state.Governors.First();
        int beforeOpinion = governor.OpinionOfKing;

        LivingRealmSystem.AddMemory(state, "Governor", governor.Id, governor.Name, "Test", "أنقذه الملك من أزمة.", 12, 6, 0, 2, 360, true);

        Assert.True(governor.OpinionOfKing > beforeOpinion);
        Assert.Contains("أنقذه الملك", LivingRealmSystem.GetLivingRealmReport(state));
    }

    [Fact]
    public void Promise_IsFulfilledByMatchingStateAndImprovesReputation()
    {
        var state = new GameState();
        foreach (var neighbor in state.Neighbors) neighbor.DaysUntilNextMove = 90;
        foreach (var governor in state.Governors) governor.DaysUntilNextMove = 90;
        var targetGovernor = state.Governors.First();

        var promise = LivingRealmSystem.AddPromise(
            state,
            "LowerTaxes",
            "Governor",
            targetGovernor.Id,
            targetGovernor.Name,
            "خفض الضرائب لهذا الوالي",
            30,
            "اجعل الضرائب منخفضة.");
        state.TaxLevel = "منخفض";

        LivingRealmSystem.ProcessDailyLivingRealm(state);

        Assert.True(promise.IsFulfilled);
        Assert.True(state.RoyalReputationScores["PromiseKeeper"] > 0);
    }

    [Fact]
    public void NeighborEconomicTrouble_GeneratesAidDecisionEvent()
    {
        var state = new GameState();
        foreach (var neighbor in state.Neighbors) neighbor.DaysUntilNextMove = 90;
        foreach (var governor in state.Governors) governor.DaysUntilNextMove = 90;
        var troubledNeighbor = state.Neighbors.First();
        troubledNeighbor.EconomicTrouble = 90;
        troubledNeighbor.DaysUntilNextMove = 1;

        var result = LivingRealmSystem.ProcessDailyLivingRealm(state);

        Assert.True(result.ShouldNarrate);
        Assert.True(result.ShouldPauseTime);
        Assert.Contains(state.LivingRealmLog, e => e.EventType == "EconomicAidRequest" && e.ActorId == troubledNeighbor.Id && !e.IsResolved);
    }

    [Fact]
    public void ResolveAidEvent_RecordsMemoryAndSpendsResources()
    {
        var state = new GameState();
        foreach (var neighbor in state.Neighbors) neighbor.DaysUntilNextMove = 90;
        foreach (var governor in state.Governors) governor.DaysUntilNextMove = 90;
        var troubledNeighbor = state.Neighbors.First();
        troubledNeighbor.EconomicTrouble = 90;
        troubledNeighbor.DaysUntilNextMove = 1;
        int goldBefore = state.Gold;
        int foodBefore = state.Food;

        LivingRealmSystem.ProcessDailyLivingRealm(state);
        var realmEvent = state.LivingRealmLog.First(e => e.EventType == "EconomicAidRequest");
        var result = LivingRealmSystem.ResolveLivingEvent(state, realmEvent.Id, "Aid");

        Assert.True(result.Success);
        Assert.True(realmEvent.IsResolved);
        Assert.Equal(goldBefore - 100, state.Gold);
        Assert.Equal(foodBefore - 150, state.Food);
        Assert.Contains(state.PoliticalMemories, m => m.ActorId == troubledNeighbor.Id && m.Category == "Aid");
    }

    [Fact]
    public void ReconcileOldSaves_InitializesGrandStrategyLayer()
    {
        var state = new GameState
        {
            RealmCharacters = null!,
            CharacterSecrets = null!,
            PoliticalHooks = null!,
            CharacterClaims = null!,
            FeudalContracts = null!,
            ActiveSchemes = null!,
            EventChains = null!,
            ReignObjectives = null!
        };

        state.ReconcileOldSaves();

        Assert.NotNull(state.RealmCharacters);
        Assert.Contains(state.RealmCharacters, c => c.SourceType == "Ruler" && c.SourceId == "player");
        Assert.Contains(state.RealmCharacters, c => c.SourceType == "Governor");
        Assert.Contains(state.RealmCharacters, c => c.SourceType == "Neighbor");
        Assert.Equal(state.Provinces.Count, state.FeudalContracts.Count);
        Assert.NotEmpty(state.ReignObjectives);
    }

    [Fact]
    public void AiProvider_FallsBackWithoutSavingApiKey()
    {
        var state = new GameState();
        var provider = new FallbackAiProvider(new AiProviderSettings
        {
            ProviderType = AiProviderType.Gemini,
            Model = "gemini-test",
            ApiKeyEnvironmentVariable = "GEMINI_API_KEY",
            AllowOnlineRequests = false
        });

        var response = provider.GenerateDialogue(state, new AiDialogueRequest
        {
            CharacterName = "وزير",
            CharacterRole = "وزير",
            Context = "مال",
            RulerName = state.RulerName
        });
        string serialized = System.Text.Json.JsonSerializer.Serialize(new AppConfig
        {
            AiProvider = new AiProviderSettings
            {
                ProviderType = AiProviderType.OpenRouter,
                ApiKeyEnvironmentVariable = "OPENROUTER_API_KEY"
            }
        });

        Assert.True(response.UsedFallback);
        Assert.Contains("fallback", response.Text);
        Assert.DoesNotContain("sk-", serialized);
        Assert.Contains("OPENROUTER_API_KEY", serialized);
    }

    [Fact]
    public void DiscoverSecret_CreatesKnownSecretAndHook()
    {
        var state = new GameState();
        var governor = state.Governors.First();

        var result = GrandStrategySystem.DiscoverSecret(state, "Governor", governor.Id);

        Assert.True(result.Success);
        Assert.Contains(state.CharacterSecrets, s => s.IsKnownToPlayer && s.OwnerName == governor.Name);
        Assert.Contains(state.PoliticalHooks, h => h.TargetName == governor.Name && !h.IsUsed);
    }

    [Fact]
    public void ActiveScheme_ResolvesThroughDailyProcessing()
    {
        var state = new GameState();
        var target = state.RealmCharacters.First(c => c.SourceType == "Governor");
        var start = GrandStrategySystem.StartScheme(state, SchemeType.FabricateHook, target.Id);
        var scheme = state.ActiveSchemes.Single();
        scheme.DaysRemaining = 1;
        scheme.SuccessChance = 100;
        scheme.Secrecy = 100;

        var result = GrandStrategySystem.ProcessDailySchemes(state);

        Assert.True(start.Success);
        Assert.True(result.ShouldNarrate);
        Assert.True(scheme.IsResolved);
    }

    [Fact]
    public void Succession_GrantsClaimsToChildren()
    {
        var state = new GameState();
        var heir = new Child { Name = "الأمير الأول", Age = 18, IsHeir = true };
        var claimant = new Child { Name = "الأمير الثاني", Age = 16, IsHeir = false };
        state.Children.Add(heir);
        state.Children.Add(claimant);
        state.HeirName = heir.Name;
        state.HeirAge = heir.Age;
        state.Governors.First().OpinionOfKing = -50;
        state.Governors.First().Ambition = 90;

        var report = GrandStrategySystem.HandleRulerDeathAndSuccession(state, state.RulerName);

        Assert.False(state.RulerIsDead);
        Assert.Contains(state.CharacterClaims, c => c.TargetType == "Throne" && c.HolderName == heir.Name);
        Assert.Contains(state.CharacterClaims, c => c.TargetType == "Throne" && c.HolderName == claimant.Name);
        Assert.Contains("أزمة خلافة", report);
    }

    [Fact]
    public void WarGoal_AllowsEnforcedPeaceAtHighWarScore()
    {
        var state = new GameState();
        var neighbor = state.Neighbors[3];
        neighbor.HasClaim = true;
        neighbor.ClaimedProvince = neighbor.ClaimableProvinces.First().Name;
        state.Armies[0].TotalSoldiers = 3000;

        var declare = WarfareSystem.DeclareWar(state, 3, false);
        state.Armies[0].CurrentProvince = state.ActiveWar!.TargetProvince;
        state.SiegeData!.TargetGarrison = 0;

        var peace = WarfareSystem.NegotiatePeace(state, "EnforceDemands");

        Assert.True(declare.Success);
        Assert.True(peace.Success);
        Assert.Null(state.ActiveWar);
        Assert.Null(state.CurrentWarGoal);
    }

    [Fact]
    public void EventChain_PausesForCurrentNarrativeStep_AndResolves()
    {
        var state = new GameState();
        var chain = EventChainSystem.StartChain(
            state,
            "TestChain",
            "actor",
            "ممثل",
            new EventChainStep
            {
                TitleKey = "event.scheme.title",
                BodyKey = "event.scheme.body",
                RequiresDecision = true
            });

        var daily = EventChainSystem.ProcessDailyChains(state);
        var resolved = EventChainSystem.ResolveCurrentStep(state, chain.Id, "Accept");

        Assert.True(daily.ShouldPauseTime);
        Assert.Contains("همس", daily.MainMessage);
        Assert.True(resolved.Success);
        Assert.True(chain.IsComplete);
    }

    [Fact]
    public void UpgradeBuilding_FromEmptySlot_UsesBuildLanguageAndQueuesFirstLevel()
    {
        var state = new GameState();
        var province = state.Provinces.First();
        province.Buildings.RemoveAll(b => b.BuildingType == "سوق");
        state.BuildingQueue.Clear();

        var result = EconomySystem.UpgradeBuilding(state, 0, "سوق");

        Assert.True(result.Success);
        Assert.Equal("بناء المبنى", result.Title);
        Assert.Contains("بناء سوق", result.MainMessage);
        Assert.DoesNotContain("ترقية سوق", result.MainMessage);
        Assert.Contains(state.BuildingQueue, q => q.ProvinceName == province.Name && q.BuildingType == "سوق");
    }

    [Fact]
    public void FirstMinisterBudgetAndRoadTaxTask_CompletesThroughDailyProcessing()
    {
        var state = new GameState();
        state.MinisterBudgets = null!;
        var appoint = FirstMinisterSystem.AppointMinister(state, "ابن جهير", 7, 6, 80);
        var budget = FirstMinisterSystem.SetMonthlyBudget(state, 20);
        state.Gold = 1000;
        state.MerchantsTrust = 40;

        var assigned = FirstMinisterSystem.AssignTask(state, "RoadTaxReform");
        int goldAfterAssignment = state.Gold;
        state.FirstMinister.TaskDaysRemaining = 1;
        var completed = FirstMinisterSystem.ProcessDailyFirstMinister(state);

        Assert.True(appoint.Success);
        Assert.True(budget.Success);
        Assert.Equal(20, state.FirstMinister.MonthlyBudgetPercent);
        Assert.Equal(20, state.MinisterBudgets["first_minister"]);
        Assert.True(assigned.Success);
        Assert.True(completed.ShouldNarrate);
        Assert.Equal("انتظار الأوامر", state.FirstMinister.CurrentTask);
        Assert.Equal(0, state.FirstMinister.TaskDaysRemaining);
        Assert.Equal(48, state.MerchantsTrust);
        Assert.Equal(goldAfterAssignment + 80, state.Gold);
    }

    [Fact]
    public void TradeDevelopmentActions_AddMonthlyIncomeAtMonthEnd()
    {
        var state = new GameState();
        state.Time.Day = 30;
        state.Gold = 2000;
        state.Food = 2000;
        state.MerchantsTrust = 50;
        state.ActiveSupplyContracts = 0;
        state.SeasonalMarketDaysLeft = 0;
        state.ProtectedTradeRoutes.Clear();
        foreach (var member in state.Council.Values)
            member.IsCorrupt = false;
        foreach (var key in state.MinisterBudgets.Keys.ToList())
            state.MinisterBudgets[key] = 0;

        var market = EconomySystem.StartSeasonalMarket(state);
        var merchants = EconomySystem.GrantMerchantPrivileges(state);
        var route = EconomySystem.ProtectTradeRoute(state, "طريق بغداد-دمشق");
        int goldBeforeMonthEnd = state.Gold;
        int provinceIncome = state.Provinces.Where(p => !p.Occupied).Sum(p => p.Income);
        int expectedTradeDevelopmentIncome = 150 + (state.MerchantsTrust * 2) + 25 + 20;

        var monthly = EconomySystem.ProcessDailyEconomy(state);

        Assert.True(market.Success);
        Assert.True(merchants.Success);
        Assert.True(route.Success);
        Assert.Equal(29, state.SeasonalMarketDaysLeft);
        Assert.Equal(goldBeforeMonthEnd + provinceIncome + expectedTradeDevelopmentIncome, state.Gold);
        Assert.Contains("دخل السوق الموسمي", monthly.MainMessage);
        Assert.Contains("دخل عقود التجار", monthly.MainMessage);
        Assert.Contains("دخل الطرق المحمية", monthly.MainMessage);
    }

    [Fact]
    public void RoyalDirector_CreatesContextualTradeOpportunity_WhenTradeIsProsperous()
    {
        var state = new GameState();
        state.Gold = 900;
        state.MerchantsTrust = 75;
        state.HeirName = "ولي العهد";
        state.HeirAge = 18;
        state.ProtectedTradeRoutes.Add("طريق بغداد-دمشق");
        foreach (var neighbor in state.Neighbors)
        {
            neighbor.Opinion = 20;
            neighbor.MilitaryAmbition = 10;
        }
        foreach (var councilor in state.Council.Values)
            councilor.Influence = 30;

        var result = RoyalDirectorSystem.ProcessDailyDirector(state);

        Assert.True(result.ShouldNarrate);
        Assert.Contains(state.LivingRealmLog, e => e.EventType == "DirectorTradeOpportunity" && !e.IsResolved);
        Assert.Contains(state.DynastyChronicle, e => e.Category == "Director");
    }

    [Fact]
    public void PersonalObjectives_AreCreatedForImportantCharacters()
    {
        var state = new GameState();

        PersonalObjectiveSystem.EnsurePersonalObjectives(state);

        Assert.NotEmpty(state.CharacterObjectives);
        Assert.Contains(state.CharacterObjectives, o => o.SourceType == "Governor");
        Assert.Contains("أهداف وطموحات الشخصيات", PersonalObjectiveSystem.GetObjectivesReport(state));
    }

    [Fact]
    public void DynastyChronicle_RecordsGloryAndRank()
    {
        var state = new GameState();

        DynastyChronicleSystem.RecordEvent(state, "Trade", "اختبار السوق", "ازدهرت القوافل في بغداد.", 120, 2);

        Assert.Equal(120, state.DynastyGlory);
        Assert.Equal("سلالة محترمة", DynastyChronicleSystem.GetGloryRank(state.DynastyGlory));
        Assert.Contains("اختبار السوق", DynastyChronicleSystem.GetChronicleReport(state));
    }

    [Fact]
    public void AiCommandRouter_MapsSmallModelNumbersToGameScreens()
    {
        bool routed = AiCommandRouterSystem.TryRoute("واحد", out var command);

        Assert.True(routed);
        Assert.Equal(AiRoutedCommand.Governance, command);
        Assert.Contains("ملخص الملك", AiCommandRouterSystem.GetProtocolPrompt());
    }

    [Fact]
    public void AiModelCatalog_ProvidesProviderDefaultsWithoutApiKeys()
    {
        var ollamaDefaults = AiModelCatalogService.GetDefaultModels(AiProviderType.Ollama);
        var geminiEndpoint = AiModelCatalogService.GetDefaultEndpoint(AiProviderType.Gemini);

        Assert.NotEmpty(ollamaDefaults);
        Assert.Contains("generativelanguage.googleapis.com", geminiEndpoint);
        Assert.DoesNotContain(ollamaDefaults, m => m.Id.Contains("sk-"));
    }

    [Fact]
    public void ReconcileOldSaves_InitializesAiCourtAgentsAndDelegation()
    {
        var state = new GameState
        {
            AiAgentProfiles = null!,
            AiProposalQueue = null!,
            AiActionLog = null!,
            DelegatedAuthoritySettings = null!
        };

        state.ReconcileOldSaves();

        Assert.NotNull(state.AiAgentProfiles);
        Assert.NotNull(state.AiProposalQueue);
        Assert.NotNull(state.AiActionLog);
        Assert.NotNull(state.DelegatedAuthoritySettings);
        Assert.Contains(state.AiAgentProfiles, p => p.Role == AiAgentRole.Spymaster);
        Assert.Equal(AiAuthorityLevel.Advisor, AiAgentSystem.GetAuthorityForRole(state, AiAgentRole.FirstMinister));
    }

    [Fact]
    public void AiProposalQueue_GeneratesStructuredProposalWithoutChangingTreasury()
    {
        var state = new GameState();
        state.SuppressRandomMajorEvents = false;
        int goldBefore = state.Gold;

        var result = AiProposalQueue.GenerateMonthlyProposals(state, 2);

        Assert.True(result.Success);
        Assert.Equal(goldBefore, state.Gold);
        Assert.NotEmpty(state.AiProposalQueue);
        Assert.All(state.AiProposalQueue, p => Assert.Equal(AiProposalStatus.Pending, p.Status));
        Assert.Contains("مقترحات", result.MainMessage);
    }

    [Fact]
    public void AiActionValidator_BlocksAutonomousActionWithoutDelegation()
    {
        var state = new GameState();
        AiAgentSystem.EnsureAgents(state);
        var spy = state.AiAgentProfiles.First(p => p.Role == AiAgentRole.Spymaster);
        var request = new AiActionRequest
        {
            AgentCharacterId = spy.CharacterId,
            AgentName = spy.CharacterName,
            Role = spy.Role,
            ActionType = AiActionType.BuildSpyNetwork,
            TargetType = AiActionTargetType.Council,
            TargetId = "court",
            TargetName = "البلاط",
            GoldCost = 150,
            EstimatedRisk = 10,
            RequiresKingApproval = false
        };

        var result = AiActionValidator.ValidateAndExecute(state, request, approvedByKing: false);

        Assert.False(result.Success);
        Assert.Contains("الأفعال التلقائية", result.MainMessage);
        Assert.Empty(state.SpyNetworks);
    }

    [Fact]
    public void AiActionValidator_ExecutesApprovedProposalThroughSystemAndLogsIt()
    {
        var state = new GameState();
        AiAgentSystem.EnsureAgents(state);
        var spy = state.AiAgentProfiles.First(p => p.Role == AiAgentRole.Spymaster);
        var request = new AiActionRequest
        {
            AgentCharacterId = spy.CharacterId,
            AgentName = spy.CharacterName,
            Role = spy.Role,
            ActionType = AiActionType.BuildSpyNetwork,
            TargetType = AiActionTargetType.Council,
            TargetId = "court",
            TargetName = "البلاط",
            GoldCost = 150,
            EstimatedRisk = 10,
            RequiresKingApproval = true,
            CreatedDate = state.Time.GetDateString(),
            SimilarityKey = "test"
        };

        var result = AiActionValidator.ValidateAndExecute(state, request, approvedByKing: true);

        Assert.True(result.Success);
        Assert.Contains(state.SpyNetworks, n => n.TargetType == "RoyalCourt");
        Assert.Contains(state.AiActionLog, l => l.AgentName == spy.CharacterName && l.WasSuccessful);
        Assert.Equal(AiProposalStatus.Executed, request.Status);
    }

    [Fact]
    public void AiContextBuilder_PreventsGovernorFromTargetingAnotherGovernor()
    {
        var state = new GameState();
        AiAgentSystem.EnsureAgents(state);
        var governorProfile = state.AiAgentProfiles.First(p => p.Role == AiAgentRole.Governor);
        var otherGovernor = state.Governors.First(g => g.Id != governorProfile.SourceId);
        var request = new AiActionRequest
        {
            AgentCharacterId = governorProfile.CharacterId,
            AgentName = governorProfile.CharacterName,
            Role = governorProfile.Role,
            ActionType = AiActionType.CalmAngryGovernor,
            TargetType = AiActionTargetType.Governor,
            TargetId = otherGovernor.Id,
            TargetName = otherGovernor.Name
        };

        bool allowed = AiContextBuilder.HasSufficientKnowledge(state, governorProfile, request, out var reason);

        Assert.False(allowed);
        Assert.Contains("مقاطعته", reason);
    }

    [Fact]
    public void AiProviderFallback_ReturnsLocalReportWhenOnlineUnavailable()
    {
        var state = new GameState();
        var provider = new FallbackAiProvider(new AiProviderSettings
        {
            ProviderType = AiProviderType.OpenAICompatible,
            AllowOnlineRequests = false
        });

        var response = provider.GenerateDialogue(state, new AiDialogueRequest
        {
            CharacterName = "مسؤول الجواسيس",
            CharacterRole = "مسؤول الجواسيس",
            Context = "حماية القصر",
            RulerName = state.RulerName
        });

        Assert.True(response.UsedFallback);
        Assert.Contains("الحوار الذكي غير متاح", response.Text);
    }

    [Fact]
    public void NeighborRealmAi_DevelopsOwnRealmAndLogsDecision()
    {
        var state = new GameState();
        var neighbor = state.Neighbors[0];
        neighbor.EconomicTrouble = 80;
        neighbor.EconomicStrength = 40;

        var result = AiWorldActorSystem.ProcessMonthlyWorldActors(state, new AiActorSettings
        {
            AllowAiNeighborRealmManagement = true
        });

        Assert.True(result.ShouldNarrate);
        Assert.True(neighbor.EconomicTrouble < 80);
        Assert.Equal("إصلاح الأسواق والجباية", neighbor.DevelopmentFocus);
        Assert.NotEmpty(neighbor.InternalDecisionLog);
        Assert.Contains(state.AiActionLog, l => l.Role == AiAgentRole.NeighborRuler);
    }

    [Fact]
    public void GovernorAi_ManagesProvinceWhenEnabled()
    {
        var state = new GameState();
        var governor = state.Governors[0];
        var province = state.Provinces.First(p => p.Id == governor.ProvinceId);
        governor.Loyalty = 90;
        governor.OpinionOfKing = 70;
        governor.Wealth = 120;
        province.Satisfaction = 85;
        province.LocalGarrison = 600;
        int incomeBefore = province.Income;

        var result = AiWorldActorSystem.ProcessMonthlyWorldActors(state, new AiActorSettings
        {
            AllowAiGovernorDecisions = true
        });

        Assert.True(result.ShouldNarrate);
        Assert.Contains(province.Buildings, b => b.BuildingType == "سوق" || b.BuildingType == "مزرعة");
        Assert.True(province.Income >= incomeBefore);
        Assert.Contains(state.AiActionLog, l => l.Role == AiAgentRole.Governor);
    }

    [Fact]
    public void FactionAi_CanEscalateToUltimatumWhenEnabled()
    {
        var state = new GameState();
        var leader = state.Governors[0];
        var faction = new Faction
        {
            Name = "فصيل اختبار الضرائب",
            Type = "LowerTaxes",
            LeaderGovernorId = leader.Id,
            DemandText = "خفض الضرائب",
            PowerPercent = 45,
            Discontent = 80,
            DaysUntilUltimatum = -1,
            IsActive = true
        };
        faction.MemberGovernorIds.Add(leader.Id);
        state.Factions.Add(faction);

        var result = AiWorldActorSystem.ProcessMonthlyWorldActors(state, new AiActorSettings
        {
            AllowAiFactionDecisions = true
        });

        Assert.True(result.ShouldNarrate);
        Assert.True(result.ShouldPauseTime);
        Assert.Equal(14, faction.DaysUntilUltimatum);
        Assert.Contains(state.AiActionLog, l => l.Role == AiAgentRole.FactionLeader);
    }

    [Fact]
    public void AiAgentSystem_CreatesFactionLeaderProfiles()
    {
        var state = new GameState();
        var leader = state.Governors[0];
        state.Factions.Add(new Faction
        {
            Name = "فصيل الولاية الغاضبة",
            Type = "LowerTaxes",
            LeaderGovernorId = leader.Id,
            DemandText = "خفض الضرائب",
            PowerPercent = 40,
            Discontent = 60,
            IsActive = true
        });

        AiAgentSystem.EnsureAgents(state);

        Assert.Contains(state.AiAgentProfiles, p => p.Role == AiAgentRole.FactionLeader && p.SourceType == "Faction");
    }

    [Fact]
    public void ReconcileOldSaves_InitializesAiConversationSessionsAndMeetingHistory()
    {
        var state = new GameState
        {
            AiConversationSessions = null!,
            AiMeetingHistory = null!
        };

        state.ReconcileOldSaves();

        Assert.NotNull(state.AiConversationSessions);
        Assert.NotNull(state.AiMeetingHistory);
    }

    [Fact]
    public void AiSessionSystem_CreatesSeparateSessionsPerCharacterForSameLocalModel()
    {
        var state = new GameState();
        AiAgentSystem.EnsureAgents(state);
        var firstMinister = state.AiAgentProfiles.First(p => p.Role == AiAgentRole.FirstMinister);
        var spymaster = state.AiAgentProfiles.First(p => p.Role == AiAgentRole.Spymaster);
        var provider = new AiProviderSettings
        {
            ProviderType = AiProviderType.Local,
            Model = "local-council-model"
        };
        var actors = new AiActorSettings
        {
            SmartDialoguesEnabled = true,
            ApplyToMinisters = true
        };

        AiSessionSystem.GenerateReply(state, firstMinister, "الحرب", "هل نعلن الحرب؟", "اختبار", provider, actors);
        AiSessionSystem.GenerateReply(state, spymaster, "الحرب", "هل نعلن الحرب؟", "اختبار", provider, actors);

        var sessions = state.AiConversationSessions
            .Where(s => s.ProviderType == AiProviderType.Local && s.Model == "local-council-model")
            .ToList();

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, s => s.CharacterId == firstMinister.CharacterId);
        Assert.Contains(sessions, s => s.CharacterId == spymaster.CharacterId);
        Assert.All(sessions, s => Assert.NotEmpty(s.Messages));
    }

    [Fact]
    public void AiMeetingSystem_RunCouncilMeetingStoresTranscriptWithoutChangingRealmState()
    {
        var state = new GameState();
        int goldBefore = state.Gold;
        int armyBefore = state.Army;
        var provider = new AiProviderSettings { ProviderType = AiProviderType.Disabled };
        var actors = new AiActorSettings { SmartDialoguesEnabled = true, ApplyToMinisters = true };

        var result = AiMeetingSystem.RunCouncilMeeting(
            state,
            "الحرب والدفاع",
            "أفكر في شن حرب، ما رأيكم؟",
            provider,
            actors);

        Assert.True(result.Success);
        Assert.Contains(state.RulerName, result.MainMessage);
        Assert.Contains("محضر رأي فقط", result.MainMessage);
        Assert.NotEmpty(state.AiMeetingHistory);
        Assert.True(state.AiConversationSessions.Count >= 5);
        Assert.Equal(goldBefore, state.Gold);
        Assert.Equal(armyBefore, state.Army);
        Assert.Null(state.ActiveWar);
    }

    [Fact]
    public void AiMeetingSystem_NeighborAudienceUsesNeighborRulerSession()
    {
        var state = new GameState();
        var neighbor = state.Neighbors[0];
        var provider = new AiProviderSettings
        {
            ProviderType = AiProviderType.Local,
            Model = "single-local-model"
        };
        var actors = new AiActorSettings
        {
            SmartDialoguesEnabled = true,
            ApplyToNeighborRulers = true
        };

        var result = AiMeetingSystem.RunNeighborAudience(
            state,
            neighbor.Id,
            "ما شروطك لبقاء الحدود آمنة؟",
            provider,
            actors);

        Assert.True(result.Success);
        Assert.Contains(neighbor.Name, result.MainMessage);
        Assert.Contains(state.AiConversationSessions, s =>
            s.ProviderType == AiProviderType.Local &&
            s.Model == "single-local-model" &&
            s.CharacterRole == AiAgentRole.NeighborRuler);
    }
}
