using System.Text;

namespace AccardND.Localization
{
    /// <summary>Chiavi stabili usate dal codice. I testi italiani sono nel catalogo editor.</summary>
    public static class GameTextKeys
    {
        public static class Common
        {
            public const string Activate = "common.activate";
            public const string Back = "common.back";
            public const string Cancel = "common.cancel";
            public const string Close = "common.close";
            public const string Confirm = "common.confirm";
            public const string Continue = "common.continue";
            public const string Card = "common.card";
            public const string Cpu = "common.cpu";
            public const string Exit = "common.exit";
            public const string Mute = "common.mute";
            public const string Muted = "common.muted";
            public const string Opponent = "common.opponent";
            public const string Proceed = "common.proceed";
            public const string Use = "common.use";
            public const string You = "common.you";
            public const string Guest = "common.guest";
            public const string Level = "common.level";
        }

        public static class Login
        {
            public const string Title = "login.title";
            public const string Subtitle = "login.subtitle";
            public const string InitialStatus = "login.status.initial";
            public const string GoogleRequired = "login.status.google_required";
            public const string RestoringSession = "login.status.restoring_session";
            public const string WelcomeBack = "login.status.welcome_back";
            public const string GoogleButton = "login.button.google";
            public const string NicknamePlaceholder = "login.nickname.placeholder";
            public const string UpdateTitle = "login.update.title";
            public const string UpdateNow = "login.update.button";
            public const string UpdateRequiredGeneric = "login.update.required_generic";
            public const string UpdateRequiredVersion = "login.update.required_version";
            public const string UpdateMessage = "login.update.message";
            public const string VersionOutdated = "login.status.version_outdated";
            public const string MaintenanceTitle = "login.maintenance.title";
            public const string MaintenanceMessage = "login.maintenance.message";
            public const string MaintenanceRetry = "login.maintenance.button";
            public const string MaintenanceStatus = "login.status.maintenance";
            public const string AccessFailed = "login.status.access_failed";
            public const string CheckingUpdates = "login.status.checking_updates";
            public const string Updated = "login.status.updated";
            public const string AccessingProvider = "login.status.accessing_provider";
            public const string AccessComplete = "login.status.access_complete";
            public const string CheckingAccount = "login.status.checking_account";
            public const string GoogleAccountRequired = "login.status.google_account_required";
            public const string ChooseNickname = "login.status.choose_nickname";
            public const string NicknameTooShort = "login.status.nickname_too_short";
            public const string CheckingNickname = "login.status.checking_nickname";
            public const string Welcome = "login.status.welcome";
            public const string NicknameUnavailable = "login.status.nickname_unavailable";
            public const string OpeningMenu = "login.status.opening_menu";
            public const string AuthenticationUnavailable = "login.error.authentication_unavailable";
            public const string MissingToken = "login.error.missing_token";
            public const string MissingAccountToken = "login.error.missing_account_token";
            public const string AccountServerUnavailable = "login.error.account_server_unavailable";
            public const string ServerError = "login.error.server";
            public const string ServerTimeout = "login.error.server_timeout";
        }

        public static class Account
        {
            public const string SessionUsedElsewhere = "account.session.used_elsewhere";
            public const string SessionClosedBody = "account.session.closed_body";
            public const string CloseGame = "account.session.close_game";
            public const string ReturnToLogin = "account.session.return_to_login";

            /// <summary>Badge di stato: il socket è giù e si sta riaprendo da solo.</summary>
            public const string BadgeReconnecting = "account.session.badge_reconnecting";

            /// <summary>Badge di stato: da qui non si torna da soli, si passa dal login.</summary>
            public const string BadgeSessionExpired = "account.session.badge_expired";
        }

        public static class PvpLoadout
        {
            public const string Title = "pvp.loadout.title";
            public const string Subtitle = "pvp.loadout.subtitle";
            public const string Save = "pvp.loadout.save";
            public const string Catalog = "pvp.loadout.catalog";
            public const string ChooseRemaining = "pvp.loadout.choose_remaining";
            public const string Valid = "pvp.loadout.valid";
            public const string Summary = "pvp.loadout.summary";
            public const string MissingDatabase = "pvp.loadout.missing_database";
            public const string SelectedSuffix = "pvp.loadout.selected_suffix";
            public const string InspectionBody = "pvp.loadout.inspection_body";
            public const string Add = "pvp.loadout.add";
            public const string AlreadyAdded = "pvp.loadout.already_added";
            public const string Full = "pvp.loadout.full";
            public const string YourLoadout = "pvp.loadout.your_loadout";
            public const string RemoveHint = "pvp.loadout.remove_hint";
            public const string CardStrength = "pvp.loadout.card_strength";
            public const string ChooseClass = "pvp.loadout.choose_class";
            public const string ClassCardCount = "pvp.loadout.class_card_count";
            public const string SlotTitle = "pvp.loadout.slot_title";
            public const string CardStats = "pvp.loadout.card_stats";
            public const string CardPower = "pvp.loadout.card_power";
            public const string Info = "pvp.loadout.info";
            public const string Choose = "pvp.loadout.choose";
            public const string Remove = "pvp.loadout.remove";
        }

        public static class PvpNickname
        {
            public const string DialogTitle = "pvp.nickname.dialog.title";
            public const string DialogHint = "pvp.nickname.dialog.hint";
            public const string Placeholder = "pvp.nickname.dialog.placeholder";
            public const string Saved = "pvp.nickname.status.saved";
        }

        public static class Ads
        {
            public const string FakeCountdownReward = "ads.fake.countdown.reward";
            public const string FakeCountdownClose = "ads.fake.countdown.close";
            public const string FakeRewardUnlocked = "ads.fake.reward_unlocked";
            public const string FakeBanner = "ads.fake.banner";
            public const string FakeRewardedTitle = "ads.fake.title.rewarded";
            public const string FakeInterstitialTitle = "ads.fake.title.interstitial";
        }

        public static class ReviewPrompt
        {
            public const string RatingTitle = "review_prompt.title.rating";
            public const string StoreTitle = "review_prompt.title.store";
            public const string ThanksTitle = "review_prompt.title.thanks";
        }

        public static class PvpResult
        {
            public const string Victory = "pvp.result.victory";
            public const string Defeat = "pvp.result.defeat";
            public const string Title = "pvp.result.title";
            public const string VictorySubtitle = "pvp.result.victory_subtitle";
            public const string DefeatSubtitle = "pvp.result.defeat_subtitle";
            public const string Continue = "pvp.result.continue";
            public const string Timeout = "pvp.result.timeout";
            public const string OpponentForfeit = "pvp.result.opponent_forfeit";
            public const string Surrendered = "pvp.result.surrendered";
            public const string OpponentSurrendered = "pvp.result.opponent_surrendered";
            public const string Placement = "pvp.result.placement";
            public const string League = "pvp.result.league";
            public const string Promoted = "pvp.result.promoted";
            public const string Demoted = "pvp.result.demoted";
            public const string Friendly = "pvp.result.friendly";
            public const string Achievements = "pvp.result.achievements";
            public const string AchievementItem = "pvp.result.achievement_item";
            public const string TripleExperienceOffer = "pvp.result.triple_experience_offer";
            public const string TripleExperienceApplied = "pvp.result.triple_experience_applied";
            public const string TripleExperienceUnavailable = "pvp.result.triple_experience_unavailable";
            public const string SurrenderLobbyStatus = "pvp.result.surrender_lobby_status";
        }

        public static class PvpStatus
        {
            public const string ReconnectRemaining = "pvp.status.reconnect_remaining";
            public const string ReconnectExpired = "pvp.status.reconnect_expired";
            public const string SurrenderNotSent = "pvp.status.surrender_not_sent";
            public const string MoveRejected = "pvp.status.move_rejected";
            public const string ActionNotConfirmed = "pvp.status.action_not_confirmed";
            public const string ActionNotSent = "pvp.status.action_not_sent";
        }

        public static class PvpLog
        {
            public const string MatchAgainst = "pvp.log.match_against";
            public const string RoundStarted = "pvp.log.round_started";
            public const string DecisiveSelection = "pvp.log.decisive_selection";
            public const string DeploymentStarted = "pvp.log.deployment_started";
            public const string CardDeployed = "pvp.log.card_deployed";
            public const string Auras = "pvp.log.auras";
            public const string TurnSkipped = "pvp.log.turn_skipped";
            public const string CardRevived = "pvp.log.card_revived";
            public const string ProtectionRedirect = "pvp.log.protection_redirect";
            public const string ProtectionAdvantage = "pvp.log.protection_advantage";
            public const string Attachment = "pvp.log.attachment";
            public const string Fury = "pvp.log.fury";
            public const string MightAura = "pvp.log.might_aura";
            public const string MageAura = "pvp.log.mage_aura";
            public const string SpiritExpired = "pvp.log.spirit_expired";
            public const string Timeout = "pvp.log.timeout";
            public const string Forfeit = "pvp.log.forfeit";
            public const string RoundEnded = "pvp.log.round_ended";
            public const string ReplayMismatch = "pvp.log.replay_mismatch";
            public const string WonByForfeit = "pvp.log.won_by_forfeit";
            public const string LostByForfeit = "pvp.log.lost_by_forfeit";
            public const string WonMatch = "pvp.log.won_match";
            public const string LostMatch = "pvp.log.lost_match";
            public const string Assassin = "pvp.log.ability.assassin";
            public const string Mage = "pvp.log.ability.mage";
            public const string Hunter = "pvp.log.ability.hunter";
            public const string PaladinSelf = "pvp.log.ability.paladin_self";
            public const string PaladinOther = "pvp.log.ability.paladin_other";
            public const string Priest = "pvp.log.ability.priest";
            public const string Warrior = "pvp.log.ability.warrior";
            public const string Necromancer = "pvp.log.ability.necromancer";
            public const string CounterattackPrefix = "pvp.log.attack.counterattack_prefix";
            public const string OverkillTag = "pvp.log.attack.overkill_tag";
            public const string Impossible = "pvp.log.attack.impossible";
            public const string RollOutcome = "pvp.log.attack.roll_outcome";
            public const string Resists = "pvp.log.attack.resists";
            public const string BecomesSpirit = "pvp.log.attack.becomes_spirit";
            public const string Eliminated = "pvp.log.attack.eliminated";
            public const string LosesLife = "pvp.log.attack.loses_life";
            public const string Attack = "pvp.log.attack.summary";
            public const string RollSingle = "pvp.log.roll.single";
            public const string RollDouble = "pvp.log.roll.double";
            public const string RollHighest = "pvp.log.roll.highest";
            public const string RollLowest = "pvp.log.roll.lowest";
            public const string RollSum = "pvp.log.roll.sum";
            public const string RollResult = "pvp.log.roll.result";
            public const string UnknownPlayer = "pvp.log.unknown_player";
            public const string SurrenderSent = "pvp.log.surrender_sent";
        }

        public static class PvpError
        {
            public const string AbilityRequiresAction = "pvp.error.ability_requires_action";
            public const string ManaInsufficient = "pvp.error.mana_insufficient";
            public const string ManaInsufficientGeneric = "pvp.error.mana_insufficient_generic";
            public const string SupremeNotAvailable = "pvp.error.supreme_not_available";
            public const string TurnEndedWaiting = "pvp.message.turn_ended_waiting";
            public const string Surrendered = "pvp.message.surrendered";
        }

        public static class Data
        {
            public static string CardName(string id) => $"card.{id}.name";
            public static string CardRules(string id) => $"card.{id}.rules";
            public static string ScenarioName(string id) => $"scenario.{id}.name";
        }

        public static class Audio
        {
            public const string VolumePercent = "audio.volume.percent";
        }

        public static class Combat
        {
            public const string CardEliminated = "combat.card.eliminated";
            public const string CardKnockedOut = "combat.card.knocked_out";
            public const string HitPoints = "combat.card.hit_points";
            public const string CpuMaster = "combat.cpu.master";
            public const string CpuMasterScenario = "combat.cpu.master_scenario";
            public const string Preparation = "combat.banner.preparation";
            public const string ExperienceProgress = "combat.hud.experience_progress";
            public const string ChangeForm = "combat.banner.change_form";
            public const string OutcomeEliminated = "combat.outcome.eliminated";
            public const string OutcomeResists = "combat.outcome.resists";
            public const string ResultDetailed = "combat.result.detailed";
            public const string ResultEliminates = "combat.result.eliminates";
            public const string ResultResists = "combat.result.resists";
            public const string ImpossibleAttackDetailed = "combat.attack.impossible_detailed";
            public const string BonusAuraWarrior = "combat.bonus.aura_warrior";
            public const string BonusHunterPrey = "combat.bonus.hunter_prey";
            public const string BonusFury = "combat.bonus.fury";
            public const string BonusEquipmentMalus = "combat.bonus.equipment_malus";
            public const string BonusMightAura = "combat.bonus.might_aura";
            public const string BonusOther = "combat.bonus.other";
            public const string BonusSourceFury = "combat.bonus_source.fury";
            public const string BonusSourceBlessing = "combat.bonus_source.blessing";
            public const string BonusSourceGeneric = "combat.bonus_source.generic";
            public const string DeploymentComplete = "combat.message.deployment_complete";
            public const string InitiativeBanner = "combat.banner.initiative";
            public const string InitiativeStarted = "combat.message.initiative_started";
            public const string InitiativeLog = "combat.log.initiative";
            public const string InitiativeCallout = "combat.callout.initiative";
			public const string PetrifiedCallout = "combat.callout.petrified";
			public const string FreedCallout = "combat.callout.freed";
			public const string CrumbledCallout = "combat.callout.crumbled";
			public const string UnpetrifyRoll = "combat.roll.unpetrify";
            public const string ActionAttack = "combat.action.attack";
            public const string ActionAbility = "combat.action.ability";
            public const string ActionEquip = "combat.action.equip";
            public const string ActionSkip = "combat.action.skip";
            public const string ActionChangeForm = "combat.action.change_form";
            public const string ActionSupreme = "combat.action.supreme";
            public const string SkipTurnMessage = "combat.message.skip_turn";
            public const string SkipTurnLog = "combat.log.skip_turn";
            public const string InhibitedSkipsTurn = "combat.message.inhibited_skips_turn";
            public const string PlayerTurnBanner = "combat.banner.player_turn";
            public const string ChooseAction = "combat.message.choose_action";
            public const string CpuTurnBanner = "combat.banner.cpu_turn";
            public const string CpuChoosingTarget = "combat.message.cpu_choosing_target";
            public const string CpuPaladinRedirect = "combat.message.cpu_paladin_redirect";
            public const string CpuPaladinSelfDefense = "combat.message.cpu_paladin_self_defense";
            public const string PlayerTurnSkippedSuffix = "combat.log.player_turn_skipped_suffix";
            public const string ImpossiblePlayerAttack = "combat.message.impossible_player_attack";
            public const string GuaranteedKill = "combat.message.guaranteed_kill";
            public const string CpuTargetChoiceLog = "combat.log.cpu_target_choice";
            public const string PaladinRedirect = "combat.message.paladin_redirect";
            public const string PaladinSelfDefense = "combat.message.paladin_self_defense";
            public const string CpuTurnSkippedSuffix = "combat.log.cpu_turn_skipped_suffix";
            public const string ImpossibleCpuAttack = "combat.message.impossible_cpu_attack";
            public const string AttackTargetPrompt = "combat.message.attack_target_prompt";
            public const string AssassinTargetPrompt = "combat.message.assassin_target_prompt";
            public const string WarriorAbilityReady = "combat.message.warrior_ability_ready";
            public const string MageTargetPrompt = "combat.message.mage_target_prompt";
            public const string PaladinTargetPrompt = "combat.message.paladin_target_prompt";
            public const string HunterTargetPrompt = "combat.message.hunter_target_prompt";
            public const string NecromancerTargetPrompt = "combat.message.necromancer_target_prompt";
            public const string PriestTargetPrompt = "combat.message.priest_target_prompt";
            public const string AttachmentTargetPrompt = "combat.message.attachment_target_prompt";
            public const string AutoVictoryLog = "combat.log.auto_victory";
            public const string AutoVictoryMessage = "combat.message.auto_victory";
            public const string AttachmentApplied = "combat.message.attachment_applied";
            public const string AttachmentAppliedLog = "combat.log.attachment_applied";
            public const string CpuAttachmentApplied = "combat.message.cpu_attachment_applied";
            public const string CpuAttachmentAppliedLog = "combat.log.cpu_attachment_applied";
            public const string SpiritLastTurnEndedLog = "combat.log.spirit_last_turn_ended";
            public const string GolemNewFormLog = "combat.log.golem_new_form";
            public const string AuraActiveLog = "combat.log.aura_active";
            public const string CpuAuraActiveLog = "combat.log.cpu_aura_active";
            public const string ManaInsufficientAbility = "combat.message.mana_insufficient_ability";
            public const string RollDefenseNamed = "combat.roll.defense_named";
            public const string RollAttackNamed = "combat.roll.attack_named";
            public const string RogueRerollStatus = "combat.status.rogue_reroll";
            public const string RollAttack = "combat.roll.attack";
            public const string RollDefense = "combat.roll.defense";
            public const string RollResistance = "combat.roll.resistance";
            public const string RollCpuAttack = "combat.roll.cpu_attack";
            public const string RollYourDefense = "combat.roll.your_defense";
            public const string DefenseWarrior = "combat.defense.warrior";
            public const string DefensePaladin = "combat.defense.paladin";
            public const string DefenseBarbarian = "combat.defense.barbarian";
            public const string DefenseHunter = "combat.defense.hunter";
            public const string DefenseAssassin = "combat.defense.assassin";
            public const string DefenseRogue = "combat.defense.rogue";
            public const string DefenseMage = "combat.defense.mage";
            public const string DefensePriest = "combat.defense.priest";
            public const string DefenseNecromancer = "combat.defense.necromancer";
            public const string DisadvantageAgainst = "combat.modifier.disadvantage_against";
			public const string CpuHudRoom = "combat.cpu_hud.room";
			public const string CpuHudThreat = "combat.cpu_hud.threat";
			public const string CpuHudCave = "combat.cpu_hud.cave";
			public const string CpuHudAreaCave = "combat.cpu_hud.area_cave";
			public const string CpuHudArea = "combat.cpu_hud.area";
			public const string CpuHudEncounterBoss = "combat.cpu_hud.encounter.boss";
			public const string CpuHudEncounterMerchant = "combat.cpu_hud.encounter.merchant";
			public const string CpuHudEncounterLoot = "combat.cpu_hud.encounter.loot";
			public const string CpuHudEncounterUnexpected = "combat.cpu_hud.encounter.unexpected";
			public const string CpuHudDifficultyEasy = "combat.cpu_hud.difficulty.easy";
			public const string CpuHudDifficultyHard = "combat.cpu_hud.difficulty.hard";
			public const string CpuHudDifficultyNormal = "combat.cpu_hud.difficulty.normal";
			public const string CpuHudTooltipGolem = "combat.cpu_hud.tooltip.golem";
			public const string CpuHudTooltipCampaignBoss = "combat.cpu_hud.tooltip.campaign_boss";
			public const string CpuHudTooltipCampaignScenario = "combat.cpu_hud.tooltip.campaign_scenario";
			public const string CpuHudTooltipArea = "combat.cpu_hud.tooltip.area";
			public const string CpuHudTooltipBaseArea = "combat.cpu_hud.tooltip.base_area";
			public const string CosmicCardDescription = "combat.card.cosmic_description";
        }

        public static class Options
        {
            public const string ReturnToMenuTitle = "options.return_to_menu.title";
            public const string ReturnToMenuBody = "options.return_to_menu.body";
            public const string MainMenu = "options.main_menu";
            public const string Surrender = "options.surrender";
            public const string SurrenderTitle = "options.surrender.title";
            public const string SurrenderBody = "options.surrender.body";
            public const string Title = "options.title";
            public const string SectionAudio = "options.section.audio";
            public const string SectionLanguage = "options.section.language";
            public const string SectionGame = "options.section.game";
            public const string SfxVolume = "options.audio.sfx";
            public const string MusicVolume = "options.audio.music";
            public const string Mute = "options.audio.mute";
            public const string Unmute = "options.audio.unmute";
            public const string LanguageLabel = "options.language.label";
            public const string LanguageUnavailable = "options.language.unavailable";
            public const string Log = "options.game.log";
            public const string AuraCodex = "options.game.aura_codex";
            public const string Privacy = "options.game.privacy";
            public const string Logout = "options.logout";
            public const string LogoutTitle = "options.logout.title";
            public const string LogoutBody = "options.logout.body";
        }

        public static class GameLog
        {
            public const string EntryContext = "game_log.entry_context";
			public const string BattleLogTitle = "game_log.battle_log_title";
        }

        public static class Library
        {
            public const string Title = "library.title";
        }

        public static class Inspection
        {
            public const string ActiveStatuses = "inspection.active_statuses";
            public const string Strength = "inspection.summary.strength";
            public const string StrengthChanged = "inspection.summary.strength_changed";
            public const string Family = "inspection.summary.family";
            public const string Class = "inspection.summary.class";
            public const string Advantage = "inspection.summary.advantage";
            public const string NoFamily = "inspection.summary.no_family";
            public const string NoClass = "inspection.summary.no_class";
            public const string FamilyAura = "inspection.aura.family";
            public const string ClassAura = "inspection.aura.class";
            public const string Ability = "inspection.ability";
            public const string PassiveAbility = "inspection.passive_ability";
            public const string Supreme = "inspection.supreme";
            public const string ManaSuffix = "inspection.mana_suffix";
            public const string HunterMarkStatus = "inspection.status.hunter_mark";
            public const string HunterMarkDescription = "inspection.status.hunter_mark_description";
            public const string SupremeCostMalusStatus = "inspection.status.supreme_cost_malus";
            public const string SupremeCostMalusDescription = "inspection.status.supreme_cost_malus_description";
            public const string SeraphelSealsStatus = "inspection.status.seraphel_seals";
            public const string SeraphelSealsDescription = "inspection.status.seraphel_seals_description";
        }

        public static class Hints
        {
            public const string Next = "hints.action.next";
            public const string Complete = "hints.action.complete";
            public const string StepProgress = "hints.step.progress";
        }

        public static class Campaign
        {
            public const string Title = "campaign.title";

            /// <summary>Popup della run lasciata a metà: titolo, i due corpi e i tre bottoni.</summary>
            public const string RecoveryTitle = "campaign.recovery.title";
            public const string RecoverySessionBody = "campaign.recovery.session_body";
            public const string RecoverySavedBody = "campaign.recovery.saved_body";
            public const string RecoveryResume = "campaign.recovery.resume";
            public const string RecoveryAbandon = "campaign.recovery.abandon";
            public const string RecoveryCancel = "campaign.recovery.cancel";
            public const string RecoveryUnusableSave = "campaign.recovery.unusable_save";

            public const string Adventure = "campaign.mode.adventure";
            public const string Hardcore = "campaign.mode.hardcore";
            public const string Advance = "campaign.action.advance";
            public const string RetryRoom = "campaign.action.retry_room";
            public const string GolemRetryStatePreservedLog = "campaign.log.golem_retry_state_preserved";
            public const string RewardQueuedLog = "campaign.log.reward_queued";
            public const string RewardNotRecordedLog = "campaign.log.reward_not_recorded";
            public const string OfflineSummarySaved = "campaign.message.offline_summary_saved";
            public const string RewardConnectionRequired = "campaign.message.reward_connection_required";
            public const string AdMultiplierConnectionRequired = "campaign.message.ad_multiplier_connection_required";
            public const string AdUnavailable = "campaign.message.ad_unavailable";
            public const string AdWatchIncomplete = "campaign.message.ad_watch_incomplete";
            public const string AdMultiplierNotAppliedLog = "campaign.log.ad_multiplier_not_applied";
            public const string TripleSavedToProfile = "campaign.message.triple_saved_to_profile";
            public const string DeckExhaustedBanner = "campaign.banner.deck_exhausted";
            public const string NotEnoughCards = "campaign.message.not_enough_cards";
            public const string ChapterCompleted = "campaign.chapter.completed";
            public const string ChapterUnlockFirst = "campaign.chapter.unlock_first";
            public const string ChapterUnlocked = "campaign.chapter.unlocked";
            public const string ChapterBeatPreviousBoss = "campaign.chapter.beat_previous_boss";
            public const string ChapterComingSoon = "campaign.chapter.coming_soon";
            public const string DefeatFormationBanner = "campaign.banner.defeat_formation";
            public const string DefeatRetreatBanner = "campaign.banner.defeat_retreat";
			public const string DefeatFormationMessage = "campaign.message.defeat_formation";
			public const string DefeatRetreatMessage = "campaign.message.defeat_retreat";
			public const string GameOverFormationMessage = "campaign.message.game_over_formation";
            public const string RoomReward = "campaign.message.room_reward";
            public const string AccountRewardSummary = "campaign.message.account_reward_summary";
            public const string WatchAdToTriple = "campaign.message.watch_ad_to_triple";
            public const string TripleQuestion = "campaign.reward.triple_question";
            public const string TripleQuestionWithAd = "campaign.reward.triple_question_with_ad";
            public const string RewardReady = "campaign.reward.ready";
            public const string RewardPopupBody = "campaign.reward.popup_body";
            public const string Triple = "campaign.action.triple";
            public const string WatchAdExperience = "campaign.action.watch_ad_experience";
            public const string LevelUpTitle = "campaign.level_up.title";
            public const string NextRoom = "campaign.action.next_room";
            public const string MaxLevel = "campaign.level_up.max_level";
            public const string ExperienceProgress = "campaign.level_up.experience_progress";
            public const string LevelUpBody = "campaign.level_up.body";
            public const string ManaInsufficient = "campaign.message.mana_insufficient";
            public const string SeraphelManaRegeneration = "campaign.message.seraphel_mana_regeneration";
            public const string SeraphelManaRegenerationLog = "campaign.log.seraphel_mana_regeneration";
            public const string SupremeManaInsufficient = "campaign.supreme.mana_insufficient";
            public const string RogueSupremePrompt = "campaign.supreme.rogue_prompt";
            public const string MassSupremeStarted = "campaign.supreme.mass_started";
            public const string MassSupremeDefeated = "campaign.supreme.mass_defeated";
            public const string MassSupremeResisted = "campaign.supreme.mass_resisted";
            public const string SeraphelDamaged = "campaign.seraphel.damaged";
            public const string SeraphelPhaseTwo = "campaign.seraphel.phase_two";
            public const string SeraphelJudgement = "campaign.seraphel.judgement";
            public const string SeraphelThreeSeals = "campaign.seraphel.three_seals";
            public const string SeraphelSealsApplied = "campaign.seraphel.seals_applied";
            public const string SeraphelJudgementResisted = "campaign.seraphel.judgement_resisted";
            public const string DeckBuilderTitle = "campaign.deck_builder.title";
            public const string DeckBuilderChooseChampion = "campaign.deck_builder.choose_champion";
            public const string DeckBuilderChooseViceChampion = "campaign.deck_builder.choose_vice_champion";
            public const string DeckBuilderComplete = "campaign.deck_builder.complete";
			public const string MerchantRoomCompleteBanner = "campaign.banner.merchant_room_complete";
			public const string CombatRoomCompleteBanner = "campaign.banner.combat_room_complete";
			public const string LootRoomCompleteBanner = "campaign.banner.loot_room_complete";
			public const string QuickChallengeCompleteBanner = "campaign.banner.quick_challenge_complete";
			public const string JurinashorSwordSummoned = "campaign.jurinashor.sword_summoned";
            public const string DeckBuilderEmptyDeckHint = "campaign.deck_builder.empty_deck_hint";
            public const string InitialDraftEmptyDeckHint = "campaign.initial_draft.empty_deck_hint";
			public const string RoomChoiceHeading = "campaign.room_choice.heading";
			public const string RoomChoiceHint = "campaign.room_choice.hint";
            public const string InitialDraftRemove = "campaign.initial_draft.remove";
            public const string InitialDraftSelect = "campaign.initial_draft.select";
            public const string InitialDraftChooseCaptain = "campaign.initial_draft.choose_captain";
            public const string InitialDraftTitle = "campaign.initial_draft.title";
            public const string InitialDraftStatus = "campaign.initial_draft.status";
            public const string InitialDraftCaptainStatus = "campaign.initial_draft.captain_status";
            public const string InitialDraftCaptainPrompt = "campaign.initial_draft.captain_prompt";
            public const string InitialDraftCardsPrompt = "campaign.initial_draft.cards_prompt";
            public const string InitialDraftChooseCaptainFirst = "campaign.initial_draft.choose_captain_first";
            public const string InitialDraftChooseCardsFirst = "campaign.initial_draft.choose_cards_first";
            public const string InitialDraftConfirm = "campaign.initial_draft.confirm";
            public const string InitialDraftChoose = "campaign.initial_draft.choose";
            public const string ChooseFormationCards = "campaign.deployment.choose_cards";
            public const string YourFormation = "campaign.formation.player";
            public const string RewardTitle = "campaign.reward.title";
            public const string ChapterCompletedTitle = "campaign.reward.chapter_completed_title";
            public const string TripleApplied = "campaign.reward.triple_applied";
            public const string PrepareBag = "campaign.bag.prepare";
            public const string BagUnavailable = "campaign.bag.unavailable";
            public const string BagEmpty = "campaign.bag.empty";
            public const string ChooseModeToStart = "campaign.message.choose_mode_to_start";
        }

        public static class ImplementationArchive
        {
            public const string Title = "implementation_archive.title";
			public const string ConsumablesTitle = "implementation_archive.consumables.title";
			public const string ConsumablesEmpty = "implementation_archive.consumables.empty";
            public const string DeckTitle = "implementation_archive.deck.title";
            public const string DeckEmpty = "implementation_archive.deck.empty";
            public const string CooldownTitle = "implementation_archive.cooldown.title";
            public const string CooldownEmpty = "implementation_archive.cooldown.empty";
            public const string GraveyardTitle = "implementation_archive.graveyard.title";
            public const string GraveyardEmpty = "implementation_archive.graveyard.empty";
        }

        public static class Adventure
        {
            public const string Title = "adventure.title";
            public const string TutorialTitle = "adventure.tutorial.title";
            public const string TutorialBody = "adventure.tutorial.body";
            public const string TutorialStart = "adventure.tutorial.start";
            public const string TutorialChapterTitle = "adventure.tutorial.chapter_title";
            public const string TutorialChapterSubtitle = "adventure.tutorial.chapter_subtitle";

            /// <summary>Titolo della schermata che elenca i moduli del tutorial.</summary>
            public const string TutorialIndexTitle = "adventure.tutorial.index_title";
            public const string TutorialModuleVisitShopStatus = "adventure.tutorial.module.visit_shop_status";
            public const string TutorialModuleVisitShopFirst = "adventure.tutorial.module.visit_shop_first";
            public const string TutorialModuleOpensLater = "adventure.tutorial.module.opens_later";
            public const string TutorialModuleCompleteFirst = "adventure.tutorial.module.complete_first";


            /// <summary>
            /// Le chiavi per modulo si compongono dall'id: il catalogo dei moduli e' la sola
            /// lista, e un elenco parallelo di costanti finirebbe per divergere.
            /// </summary>
            public static string TutorialModuleTitle(string moduleId) =>
                $"adventure.tutorial.module.{moduleId}.title";

            public static string TutorialModuleSubtitle(string moduleId) =>
                $"adventure.tutorial.module.{moduleId}.subtitle";

            public static string TutorialModuleIntro(string moduleId) =>
                $"adventure.tutorial.module.{moduleId}.intro";

            public const string TutorialModuleReplayBody = "adventure.tutorial.module.replay_body";
            public const string ChapterScenarioLabel = "adventure.chapter.scenario_label";
            public const string ClassChoiceTitle = "adventure.class_choice.title";
            public const string ClassChoiceBody = "adventure.class_choice.body";
            public const string ClassChoiceFinal = "adventure.class_choice.final";
            public const string ClassChoiceTestComplete = "adventure.class_choice.test_complete";
            public const string ClassChoiceUnlocking = "adventure.class_choice.unlocking";
            public const string ClassChoiceFailed = "adventure.class_choice.failed";
            public const string ClassChoiceConnectionRequired = "adventure.class_choice.connection_required";
            public const string ClassUnlockedLog = "adventure.class_choice.unlocked_log";
            public const string ChapterClearQueuedLog = "adventure.log.chapter_clear_queued";
            public const string ChapterClearNotRecordedLog = "adventure.log.chapter_clear_not_recorded";
            public const string TutorialConnectionRequired = "adventure.message.tutorial_connection_required";
            public const string ChapterNeedsPreviousBossMessage = "adventure.message.chapter_needs_previous_boss";
            public const string ChapterComingSoonMessage = "adventure.message.chapter_coming_soon";
            public const string HardcoreConnectionRequired = "adventure.message.hardcore_connection_required";
            public const string ChapterConfirmTitle = "adventure.chapter.confirm_title";
			public const string ChapterRewardsHeading = "adventure.chapter.rewards_heading";
			public const string ChapterRewardClass = "adventure.chapter.reward.class";
			public const string ChapterRewardNumber = "adventure.chapter.reward.chapter";
			public const string ChapterRewardPropolis = "adventure.chapter.reward.propolis";
            public static string ChapterTitle(string chapterId) => $"adventure.chapter.{chapterId}.title";
        }

		/// <summary>Testi del favo talenti. Gli id vengono dal server, i testi dalla locale attiva.</summary>
		public static class Talents
		{
			public const string PropolisPoints = "talents.propolis_points";
			public const string Loading = "talents.loading";
			public const string Unavailable = "talents.unavailable";
			public const string Maxed = "talents.maxed";
			public const string Unlock = "talents.action.unlock";
			public const string Upgrade = "talents.action.upgrade";
			public const string FirstRank = "talents.effect.first_rank";
			public const string Now = "talents.effect.now";
			public const string LockedTier = "talents.locked.tier";
			public const string LockedPoints = "talents.locked.points";
			public static string BranchName(string id) => $"talents.branch.{id}.name";
			public static string TalentName(string id) => $"talents.node.{id}.name";
			public static string TalentDescription(string id) => $"talents.node.{id}.description";
			public static string TalentValue(string id) => $"talents.node.{id}.value";
		}

        public static class Merchant
        {
            public const string UpgradeTitle = "merchant.upgrade.title";
            public const string UpgradeRules = "merchant.upgrade.rules";
            public const string RelicUnlocked = "merchant.relic.unlocked";
            public const string RelicLocked = "merchant.relic.locked";
            public const string OfferTaken = "merchant.offer.taken";
            public const string OfferLocked = "merchant.offer.locked";
            public const string DeckFull = "merchant.deck.full";
            public const string EmptyCards = "merchant.cards.empty";
            public const string SelectUpgradePawn = "merchant.upgrade.select_pawn";
            public const string UpgradeAction = "merchant.upgrade.action";
            public const string UpgradeMaximum = "merchant.upgrade.maximum";
            public const string UpgradeRelicRequired = "merchant.upgrade.relic_required";
            public const string UpgradeSelectionHint = "merchant.upgrade.selection_hint";
            public const string UpgradeCardMaximumDescription = "merchant.upgrade.card_maximum_description";
            public const string UpgradeCardDescription = "merchant.upgrade.card_description";
            public const string UpgradeSelectDeckPawn = "merchant.upgrade.message.select_deck_pawn";
            public const string UpgradeAlreadyMaximum = "merchant.upgrade.message.already_maximum";
            public const string UpgradeBranchLocked = "merchant.upgrade.message.branch_locked";
            public const string UpgradeUnlockRelic = "merchant.upgrade.message.unlock_relic";
            public const string UpgradeInsufficientGold = "merchant.upgrade.message.insufficient_gold";
            public const string UpgradeUnavailable = "merchant.upgrade.message.unavailable";
            public const string UpgradeFreeLog = "merchant.upgrade.log.free";
            public const string UpgradeFreeSuccess = "merchant.upgrade.message.free_success";
            public const string UpgradePaidLog = "merchant.upgrade.log.paid";
            public const string UpgradePaidSuccess = "merchant.upgrade.message.paid_success";
            public const string RecoverDescription = "merchant.card.recover_description";
            public const string SellDescription = "merchant.card.sell_description";
            public const string DeckCount = "merchant.deck.count";
            public const string GraveyardCount = "merchant.graveyard.count";
            public const string CampaignTitle = "merchant.title";
            public const string BranchUpgrades = "merchant.branch.upgrades";
            public const string ShopTitle = "shop.title";
            public const string ShopOffers = "shop.section.offers";
            public const string ShopCatalog = "shop.section.catalog";
            public const string ShopPremium = "shop.section.premium";
            public const string ShopPremiumNoAds = "shop.premium.no_ads";
            public const string ShopPremiumClasses = "shop.premium.classes";
            public const string ShopPremiumClassesSupreme = "shop.premium.classes_supreme";
            public const string ShopPremiumSupremeUpgrade = "shop.premium.supreme_upgrade";
            public const string ShopPremiumOwned = "shop.premium.owned";
            public const string ShopPremiumAndroidOnly = "shop.premium.android_only";
            public const string ShopPremiumOpening = "shop.premium.opening";
            public const string ShopPremiumDeferred = "shop.premium.deferred";
            public const string ShopPremiumFailed = "shop.premium.failed";
            public const string ShopPremiumGranted = "shop.premium.granted";
            public const string ShopPremiumOffline = "shop.premium.offline";
            public const string ShopPremiumAlreadyRedeemed = "shop.premium.already_redeemed";
            public const string ShopPremiumBadReceipt = "shop.premium.bad_receipt";
            public const string ShopPremiumStoreOff = "shop.premium.store_off";
            public const string ShopPremiumWrongStore = "shop.premium.wrong_store";
            public const string ShopPremiumUnknownProduct = "shop.premium.unknown_product";
            public const string ShopPremiumNotPaid = "shop.premium.not_paid";
            public const string ShopEmptyBody = "shop.empty.body";
            public const string ShopGoSanctuary = "shop.action.go_sanctuary";
            public const string ShopPrepareBag = "shop.action.prepare_bag";
            public const string ShopConfirmPurchase = "shop.confirm.title";
            public const string ShopLoading = "shop.message.loading";
            public const string ShopReconnecting = "shop.message.reconnecting";
            public const string ShopConnectionRequired = "shop.message.connection_required";
            public const string ShopLoadFailed = "shop.message.load_failed";
            public const string ShopLoadFailedLog = "shop.log.load_failed";
            public const string ShopOfferAvailable = "shop.offer.available";
            public const string ShopOfferSoldOut = "shop.offer.sold_out";
            public const string ShopStockPrice = "shop.catalog.stock_price";
            public const string ShopPreparingPurchase = "shop.message.preparing_purchase";
            public const string ShopPurchaseFailedLog = "shop.log.purchase_failed";
            public const string NoMoreCards = "merchant.message.no_more_cards";
            public const string InsufficientExperience = "merchant.message.insufficient_experience";
            public const string UnknownCard = "merchant.card.unknown";
            public const string BranchCards = "merchant.branch.cards";
            public const string BranchItems = "merchant.branch.items";
            public const string BranchConfirmTitle = "merchant.branch_confirm.title";
            public const string BranchConfirmBody = "merchant.branch_confirm.body";
            public const string RecoverInsufficientExperience = "merchant.message.recover_insufficient_experience";
            public const string GoldAvailable = "merchant.status.gold_available";
            public const string InsufficientGold = "merchant.message.insufficient_gold";
            public const string Sell = "merchant.action.sell";
            public const string Recover = "merchant.action.recover";
            public const string Upgrade = "merchant.action.upgrade";
            public const string SelectCard = "merchant.action.select_card";
            public const string SellForGold = "merchant.action.sell_for_gold";
            public const string RecoverForGold = "merchant.action.recover_for_gold";
            public const string GoldPrice = "merchant.price.gold";
            public const string GoldCounter = "merchant.counter.gold";
            public const string MysteryPurchasePrefix = "merchant.purchase.mystery_prefix";
            public const string PurchasePrefix = "merchant.purchase.prefix";
            public const string CardPurchaseLog = "merchant.log.card_purchase";
            public const string CardPurchased = "merchant.message.card_purchased";
            public const string ItemPurchaseLog = "merchant.log.item_purchase";
            public const string ItemPurchased = "merchant.message.item_purchased";
            public const string SoldLog = "merchant.log.sold";
            public const string Sold = "merchant.message.sold";
            public const string RecoverInsufficientGold = "merchant.message.recover_insufficient_gold";
            public const string RecoveredLog = "merchant.log.recovered";
            public const string Recovered = "merchant.message.recovered";
        }

        public static class Profile
        {
            public const string Title = "profile.title";
            public const string Adventurer = "profile.section.adventurer";
            public const string TabOverview = "profile.tab.overview";
			public const string TabTalents = "profile.tab.talents";
            public const string TabAchievements = "profile.tab.achievements";
            public const string TabMessages = "profile.tab.messages";
            public const string Loading = "profile.message.loading";
            public const string NoMessages = "profile.message.none";
            public const string ExpiresHours = "profile.reward.expires_hours";
            public const string PendingRewardBody = "profile.reward.pending_body";
            public const string Triple = "profile.reward.triple";
            public const string PendingReward = "profile.reward.pending";
            public const string Campaign = "profile.reward.campaign";
            public const string CampaignEndRooms = "profile.reward.campaign_end_rooms";
            public const string CampaignEnd = "profile.reward.campaign_end";
            public const string Claiming = "profile.reward.claiming";
            public const string LoadingAd = "profile.reward.loading_ad";
            public const string ConnectionRequired = "profile.reward.connection_required";
            public const string AdUnavailable = "profile.reward.ad_unavailable";
            public const string AdIncomplete = "profile.reward.ad_incomplete";
            public const string TripleApplied = "profile.reward.triple_applied";
            public const string PendingLoadFailedLog = "profile.log.pending_load_failed";
            public const string TripleNotAppliedLog = "profile.log.triple_not_applied";
            public const string TripleRecoveredLog = "profile.log.triple_recovered";
            public const string TripleRejectedLog = "profile.log.triple_rejected";
        }

        public static class Tavern
        {
			public const string Title = "tavern.title";
            public const string DailyQuests = "tavern.daily_quests";
            public const string RefreshCountdown = "tavern.refresh_countdown";
            public const string BonusClaimed = "tavern.bonus.claimed";
            public const string BonusAvailable = "tavern.bonus.available";
            public const string HoneyReward = "tavern.quest.honey_reward";
            public const string QuestClaimed = "tavern.quest.claimed";
            public const string QuestClaim = "tavern.quest.claim";
            public const string QuestInProgress = "tavern.quest.in_progress";
            public const string QuestClaiming = "tavern.quest.claiming";
            public const string BadgeRefreshFailedLog = "tavern.log.badge_refresh_failed";
            public const string Loading = "tavern.message.loading";
            public const string Reconnecting = "tavern.message.reconnecting";
            public const string Offline = "tavern.message.offline";
            public const string Unavailable = "tavern.message.unavailable";
            public const string NoQuests = "tavern.message.no_quests";
            public const string Claiming = "tavern.reward.claiming";
            public const string LoadingAd = "tavern.reward.loading_ad";
            public const string AdUnavailable = "tavern.reward.ad_unavailable";
            public const string AdIncomplete = "tavern.reward.ad_incomplete";
            public const string ClaimNotUnlockedLog = "tavern.log.claim_not_unlocked";
			public static string QuestTitle(string id) => $"tavern.quest.{id}.title";
			public static string QuestDescription(string id) => $"tavern.quest.{id}.description";
        }

        public static class Server
        {
            public const string GenericError = "server.error.generic";
            public const string ErrorPrefix = "server.error.prefix";
            public const string SessionUsedElsewhere = "server.session.used_elsewhere";
            public const string MatchOpponentLeft = "server.match.opponent_left";
            public const string MatchOpponentTimeout = "server.match.opponent_timeout";
            public const string FriendInvalid = "server.friend.invalid";
            public const string FriendNotAdded = "server.friend.not_added";
            public const string FriendOffline = "server.friend.offline";
            public const string FriendBusy = "server.friend.busy";
            public static string Error(string code) => $"server.error.{code}";
        }

        public static class Hub
        {
            public const string Campaign = "hub.action.campaign";
            public const string Arena = "hub.action.multiplayer";
            public const string Sanctuary = "hub.action.sanctuary";
            public const string Library = "hub.action.library";
            public const string Shop = "hub.action.shop";
            public const string Profile = "hub.action.profile";
            public const string Leaderboard = "hub.action.leaderboard";
            public const string Tavern = "hub.action.tavern";
        }

        public static class Sanctuary
        {
            public const string Title = "sanctuary.title";
            public const string AltarClasses = "sanctuary.altar.classes";
            public const string AltarTechniques = "sanctuary.altar.techniques";
            public const string AltarRelics = "sanctuary.altar.relics";
            public const string Loading = "sanctuary.message.loading";
            public const string Reconnecting = "sanctuary.message.reconnecting";
            public const string Offline = "sanctuary.message.offline";
            public const string Unavailable = "sanctuary.message.unavailable";
            public const string EmptyAltar = "sanctuary.message.empty_altar";
            public const string ClassesStatus = "sanctuary.status.classes";
            public const string TechniquesStatus = "sanctuary.status.techniques";
            public const string RelicsStatus = "sanctuary.status.relics";
            public const string OfferItem = "sanctuary.offer.item";
            public const string OfferSlot = "sanctuary.offer.slot";
            public const string OfferTechnique = "sanctuary.offer.technique";
            public const string OfferClass = "sanctuary.offer.class";
            public const string HoneyAvailable = "sanctuary.offer.honey_available";
            public const string HoneyInsufficientBody = "sanctuary.offer.honey_insufficient_body";
            public const string OfferHoney = "sanctuary.action.offer_honey";
            public const string HoneyInsufficient = "sanctuary.action.honey_insufficient";
            public const string ConnectionRequired = "sanctuary.message.connection_required";
            public const string ItemUnlocked = "sanctuary.message.item_unlocked";
            public const string EntryOwned = "sanctuary.message.entry_owned";
            public const string CardOwned = "sanctuary.card.owned";
            public const string CardComingSoon = "sanctuary.card.coming_soon";
            public const string CardFromTutorial = "sanctuary.card.from_tutorial";
            public const string CardHoneyCost = "sanctuary.card.honey_cost";
            public const string DiscoveryClasses = "sanctuary.discovery.classes";
            public const string DiscoveryTechniques = "sanctuary.discovery.techniques";
            public const string DiscoveryRelics = "sanctuary.discovery.relics";
            public const string CatalogReceivedLog = "sanctuary.log.catalog_received";
            public const string NoConnectionLog = "sanctuary.log.no_connection";
            public const string CatalogFailedLog = "sanctuary.log.catalog_failed";
            public const string PurchasedLog = "sanctuary.log.purchased";
            public const string PurchaseRejectedLog = "sanctuary.log.purchase_rejected";
        }

        public static class Consumables
        {
            public const string CannotUseInBattle = "consumable.message.cannot_use_in_battle";
            public const string BlockedByBattleLog = "consumable.log.blocked_by_battle";
            public const string EmpowerBossBlocked = "consumable.message.empower_boss_blocked";
            public const string EmpowerBossBlockedLog = "consumable.log.empower_boss_blocked";
            public const string DetectorRevealed = "consumable.message.detector_revealed";
            public const string DetectorNextChoice = "consumable.message.detector_next_choice";
            public const string DetectorActivatedLog = "consumable.log.detector_activated";
            public const string SecondChanceUsed = "consumable.message.second_chance_used";
            public const string SecondChanceUsedLog = "consumable.log.second_chance_used";
            public const string EmpowerUsed = "consumable.message.empower_used";
            public const string EmpowerUsedLog = "consumable.log.empower_used";
            public const string DoubleExperienceReady = "consumable.message.double_experience_ready";
            public const string DoubleExperienceReadyLog = "consumable.log.double_experience_ready";
            public const string ManaRecovered = "consumable.message.mana_recovered";
            public const string ManaRecoveredLog = "consumable.log.mana_recovered";
            public const string JollyUsed = "consumable.message.jolly_used";
            public const string JollyUsedLog = "consumable.log.jolly_used";
            public const string RubySealNoTarget = "consumable.message.ruby_seal_no_target";
            public const string RubySealNoTargetLog = "consumable.log.ruby_seal_no_target";
            public const string RubySealSelectTarget = "consumable.message.ruby_seal_select_target";
            public const string RubySealSelectTargetLog = "consumable.log.ruby_seal_select_target";
            public const string RubySealTargetTitle = "consumable.ruby_seal.target_title";
            public const string RubySealInvalidTarget = "consumable.message.ruby_seal_invalid_target";
            public const string RubySealAlreadyApplied = "consumable.message.ruby_seal_already_applied";
            public const string RubySealApplied = "consumable.message.ruby_seal_applied";
            public const string RubySealAppliedLog = "consumable.log.ruby_seal_applied";
            public const string GrantedLog = "consumable.log.granted";
            public const string GrantedDescription = "consumable.reward.granted";
            public const string BagEmptyLog = "consumable.log.bag_empty";
            public const string BagLoadedLog = "consumable.log.bag_loaded";
            public const string DoubleExperienceConsumedLog = "consumable.log.double_experience_consumed";
            public const string InspectionSummary = "consumable.inspection.summary";

            public static string Name(string id) => $"consumable.{id}.name";
            public static string Description(string id) => $"consumable.{id}.description";
        }

        public static string RuntimeUi(string bindingKey)
        {
            var builder = new StringBuilder("ui.runtime.");
            bool separatorPending = false;
            foreach (char character in bindingKey ?? string.Empty)
            {
                if (char.IsLetterOrDigit(character))
                {
                    if (separatorPending && builder[^1] != '.')
                        builder.Append('.');
                    builder.Append(char.ToLowerInvariant(character));
                    separatorPending = false;
                }
                else
                {
                    separatorPending = true;
                }
            }

            return builder.ToString().TrimEnd('.');
        }

        public static class Rules
        {
            public const string AuraCodexTitle = "rules.aura_codex.title";
            public const string AbilityTitle = "rules.ability_title";
            public const string NoCombatAbility = "rules.ability.none";
            public const string NoAura = "rules.aura.none";
            public const string FormationAura = "rules.aura.formation";

            public const string SupremeTitle = "rules.supreme_title";
            public const string SupremeLocked = "rules.supreme.locked";
            public const string ManaCost = "rules.mana_cost";
            public const string ManaCostFree = "rules.mana_cost.free";

            public static string HeroClassName(string classId) => $"rules.class.{classId}.name";
            public static string ShortAbility(string classId) => $"rules.class.{classId}.ability_short";
            public static string AbilityDescription(string classId) => $"rules.class.{classId}.ability";
            public static string SupremeName(string classId) => $"rules.class.{classId}.supreme.name";
            public static string SupremeDescription(string classId) => $"rules.class.{classId}.supreme";
            public static string ClassAura(string classId) => $"rules.class.{classId}.aura";
            public static string FamilyName(string familyId) => $"rules.family.{familyId}.name";
            public static string FamilyAura(string familyId) => $"rules.family.{familyId}.aura";
        }
    }
}
