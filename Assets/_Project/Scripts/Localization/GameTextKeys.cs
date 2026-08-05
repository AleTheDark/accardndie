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
            public const string CardPower = "pvp.loadout.card_power";
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
        }

        public static class Options
        {
            public const string ReturnToMenuTitle = "options.return_to_menu.title";
            public const string ReturnToMenuBody = "options.return_to_menu.body";
            public const string MainMenu = "options.main_menu";
            public const string Surrender = "options.surrender";
            public const string SurrenderTitle = "options.surrender.title";
            public const string SurrenderBody = "options.surrender.body";
        }

        public static class GameLog
        {
            public const string EntryContext = "game_log.entry_context";
        }

        public static class Campaign
        {
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
            public const string DeckExhaustedBanner = "campaign.banner.deck_exhausted";
            public const string NotEnoughCards = "campaign.message.not_enough_cards";
            public const string ChapterCompleted = "campaign.chapter.completed";
            public const string ChapterUnlockFirst = "campaign.chapter.unlock_first";
            public const string ChapterUnlocked = "campaign.chapter.unlocked";
            public const string ChapterHoneyCost = "campaign.chapter.honey_cost";
            public const string DefeatFormationBanner = "campaign.banner.defeat_formation";
            public const string DefeatRetreatBanner = "campaign.banner.defeat_retreat";
        }

        public static class Merchant
        {
            public const string NoMoreCards = "merchant.message.no_more_cards";
            public const string InsufficientExperience = "merchant.message.insufficient_experience";
            public const string UnknownCard = "merchant.card.unknown";
            public const string BranchCards = "merchant.branch.cards";
            public const string BranchItems = "merchant.branch.items";
            public const string BranchConfirmTitle = "merchant.branch_confirm.title";
            public const string BranchConfirmBody = "merchant.branch_confirm.body";
            public const string RecoverInsufficientExperience = "merchant.message.recover_insufficient_experience";
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
            public const string DefrostUsed = "consumable.message.defrost_used";
            public const string DefrostUsedLog = "consumable.log.defrost_used";
            public const string EmpowerUsed = "consumable.message.empower_used";
            public const string EmpowerUsedLog = "consumable.log.empower_used";
            public const string DoubleExperienceReady = "consumable.message.double_experience_ready";
            public const string DoubleExperienceReadyLog = "consumable.log.double_experience_ready";
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
