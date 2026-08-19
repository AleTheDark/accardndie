using System.Collections.Generic;
using AccardND.GameData;
using NUnit.Framework;
using UnityEngine;

namespace AccardND.GameCore.Tests
{
    public sealed class CampaignRunSaveTests
    {
        [Test]
        public void RestoreProgress_RoundTripsCounters()
        {
            RunProgressState original = CreateProgress();
            original.CompleteMonsterRoom(new[] { 3, 4 });
            original.CompleteMonsterRoom(new[] { 5 });
			original.AddGold(7);
            original.TrySpendExperience(2);

            var save = new CampaignRunSave();
            CampaignRunMapper.WriteProgress(save, original);

            RunProgressState restored = CreateProgress();
            CampaignRunMapper.ReadProgress(save, restored);

            Assert.That(restored.PlayerLevel, Is.EqualTo(original.PlayerLevel));
            Assert.That(restored.CurrentExperience, Is.EqualTo(original.CurrentExperience));
            Assert.That(restored.TotalExperience, Is.EqualTo(original.TotalExperience));
            Assert.That(restored.AvailableExperience, Is.EqualTo(original.AvailableExperience));
			Assert.That(restored.Gold, Is.EqualTo(original.Gold));
            Assert.That(restored.RoomsCleared, Is.EqualTo(original.RoomsCleared));
        }

        [Test]
        public void RestoreProgress_RoundTripsKillCounters()
        {
            RunProgressState original = CreateProgress();
            original.RecordEnemiesDefeated(7);
            original.CompleteMinibossRoom(50);

            var save = new CampaignRunSave();
            CampaignRunMapper.WriteProgress(save, original);

            RunProgressState restored = CreateProgress();
            CampaignRunMapper.ReadProgress(save, restored);

            Assert.That(restored.EnemiesDefeated, Is.EqualTo(7));
            Assert.That(restored.MinibossesDefeated, Is.EqualTo(1));
        }

        /// <summary>
        /// I contatori delle quest della taverna devono sopravvivere a una run ripresa:
        /// il sommario di fine run e' quello che li porta al server, e se il save li perde
        /// il giocatore vede la quest tornare indietro.
        /// </summary>
        [Test]
        public void RestoreProgress_RoundTripsTavernQuestCounters()
        {
            RunProgressState original = CreateProgress();
            original.RecordSupremeUsed();
            original.RecordSupremeUsed();
            original.RecordQuickChallengeCompleted();
            original.RecordMerchantPurchase();
            original.RecordMerchantPurchase();
            original.RecordMerchantPurchase();
            original.CompleteMonsterRoom(new[] { 9, 9 });

            var save = new CampaignRunSave();
            CampaignRunMapper.WriteProgress(save, original);

            RunProgressState restored = CreateProgress();
            CampaignRunMapper.ReadProgress(save, restored);

            Assert.That(restored.SupremesUsed, Is.EqualTo(2));
            Assert.That(restored.QuickChallengesCompleted, Is.EqualTo(1));
            Assert.That(restored.MerchantPurchases, Is.EqualTo(3));
            Assert.That(restored.GoldEarned, Is.EqualTo(original.GoldEarned));
            Assert.That(restored.GoldEarned, Is.GreaterThan(0));
            Assert.That(restored.LevelsGained, Is.EqualTo(original.LevelsGained));
        }

        /// <summary>
        /// L'oro earned segue solo l'avventura: vendere una carta al mercante sposta oro
        /// gia' guadagnato, e contarlo renderebbe la quest completabile compra-e-rivendi.
        /// </summary>
        [Test]
        public void GoldEarned_ignores_gold_handed_back_by_the_merchant()
        {
            RunProgressState progress = CreateProgress();
            progress.CompleteMonsterRoom(new[] { 4 });
            int earnedFromTheRoom = progress.GoldEarned;

            progress.AddGold(500);

            Assert.That(progress.GoldEarned, Is.EqualTo(earnedFromTheRoom));
            Assert.That(progress.Gold, Is.EqualTo(earnedFromTheRoom + 500));
        }

        [Test]
        public void RestoreDeck_PreservesZonesAndCount()
        {
            List<CardDefinition> definitions = CreateCards(6);
            var deck = new CampaignDeckState(definitions);
            List<CampaignCardInstance> hand = deck.DrawCombatHand(new FixedRandom(), 3);
            foreach (CampaignCardInstance card in hand)
                deck.Deploy(card);
            deck.TryApplyMerchantUpgrade(deck.Cards[3]);
            deck.CompleteCombat(new[] { hand[0] }); // 1 cimitero, 2 cooldown, 3 mazzo

            var save = new CampaignRunSave();
            CampaignRunMapper.WriteDeck(save, deck);

            var restored = new CampaignDeckState(new List<CardDefinition>());
            CampaignRunMapper.ReadDeck(save, restored, id => Resolve(definitions, id));

            Assert.That(restored.Cards, Has.Count.EqualTo(deck.Cards.Count));
            Assert.That(restored.GraveyardCount, Is.EqualTo(deck.GraveyardCount));
            Assert.That(restored.CooldownCount, Is.EqualTo(deck.CooldownCount));
            Assert.That(restored.AvailableCount, Is.EqualTo(deck.AvailableCount));
            Assert.That(restored.NextInstanceId, Is.EqualTo(deck.NextInstanceId));
            Assert.That(restored.Cards[3].MerchantUpgradeCount, Is.EqualTo(1));
            Assert.That(restored.Cards[3].PermanentItemBonus, Is.EqualTo(1));
            DestroyCards(definitions);
        }

        [Test]
        public void ReadDeck_SkipsCardsMissingFromDatabase()
        {
            List<CardDefinition> definitions = CreateCards(3);
            var deck = new CampaignDeckState(definitions);
            var save = new CampaignRunSave();
            CampaignRunMapper.WriteDeck(save, deck);

            // Il database "aggiornato" non contiene più la prima carta.
            var reduced = new List<CardDefinition> { definitions[1], definitions[2] };
            var restored = new CampaignDeckState(new List<CardDefinition>());
            CampaignRunMapper.ReadDeck(save, restored, id => Resolve(reduced, id));

            Assert.That(restored.Cards, Has.Count.EqualTo(2));
            Assert.That(restored.ContainsDefinition(definitions[0].Id), Is.False);
            DestroyCards(definitions);
        }

        [Test]
        public void Service_SaveLoadClear_RoundTripsThroughStore()
        {
            var store = new InMemoryStore();
            var service = new CampaignRunSaveService(store);
            Assert.That(service.HasSave, Is.False);

            var save = new CampaignRunSave
            {
                playerLevel = 3,
                roomsCleared = 7,
				playerMana = 8,
                campaignScenarioId = "mirror",
                merchantRoomsBlockedUntilMonster = true,
                nextMonsterDifficultyIncrease = 2
            };
            save.deck.Add(new CampaignCardSave
            {
                definitionId = "card-1",
                zone = (int)CampaignCardZone.Cooldown,
                instanceId = 4
            });
            save.nextInstanceId = 5;

            service.Save(save);
            Assert.That(service.HasSave, Is.True);
            Assert.That(service.TryLoad(out CampaignRunSave loaded), Is.True);

            Assert.That(loaded.playerLevel, Is.EqualTo(3));
            Assert.That(loaded.roomsCleared, Is.EqualTo(7));
			Assert.That(loaded.playerMana, Is.EqualTo(8));
            Assert.That(loaded.campaignScenarioId, Is.EqualTo("mirror"));
            Assert.That(loaded.merchantRoomsBlockedUntilMonster, Is.True);
            Assert.That(loaded.nextMonsterDifficultyIncrease, Is.EqualTo(2));
            Assert.That(loaded.deck, Has.Count.EqualTo(1));
            Assert.That(loaded.deck[0].definitionId, Is.EqualTo("card-1"));
            Assert.That(loaded.deck[0].zone, Is.EqualTo((int)CampaignCardZone.Cooldown));
            Assert.That(loaded.nextInstanceId, Is.EqualTo(5));

            service.Clear();
            Assert.That(service.HasSave, Is.False);
        }

		// --- Battaglia in corso ---

		[Test]
		public void PawnReferences_SurviveTheRoundTripOnBothSides()
		{
			// I riferimenti fra pedine (bersaglio marcato, alleato protetto, attaccamento)
			// viaggiano come un intero solo. Lo zero e' la prima pedina del giocatore, e
			// non deve poter essere scambiato per la prima del nemico.
			foreach (int index in new[] { 0, 1, 7 })
			{
				int ally = CampaignBattleSave.EncodePawn(belongsToPlayer: true, index);
				int enemy = CampaignBattleSave.EncodePawn(belongsToPlayer: false, index);

				Assert.That(ally, Is.Not.EqualTo(enemy));
				Assert.That(CampaignBattleSave.DecodeBelongsToPlayer(ally), Is.True);
				Assert.That(CampaignBattleSave.DecodeIndex(ally), Is.EqualTo(index));
				Assert.That(CampaignBattleSave.DecodeBelongsToPlayer(enemy), Is.False);
				Assert.That(CampaignBattleSave.DecodeIndex(enemy), Is.EqualTo(index));
			}
		}

		[Test]
		public void ASaveWithoutABattle_DoesNotClaimToHaveOne()
		{
			// JsonUtility non conosce il null per gli oggetti annidati: al ritorno dal
			// disco "battle" e' comunque un oggetto, solo vuoto. Se lo scambiassimo per
			// una battaglia vera, riprendere una run ferma alla scelta della via
			// aprirebbe un campo senza pedine.
			var save = new CampaignRunSave { roomsCleared = 3 };
			string json = JsonUtility.ToJson(save);

			var reloaded = JsonUtility.FromJson<CampaignRunSave>(json);

			Assert.That(reloaded.HasBattle, Is.False);
		}

		[Test]
		public void ABattleSnapshot_ComesBackWithItsPawnsAndItsDice()
		{
			var save = new CampaignRunSave
			{
				battle = new CampaignBattleSave
				{
					roundNumber = 4,
					currentTurnIndex = 2,
					randomSeed = 99,
					randomDraws = 128
				}
			};
			save.battle.playerPawns.Add(new CampaignBattlePawnSave
			{
				definitionId = "hero-1",
				eliminated = true,
				inhibitedTurns = 2,
				markedTarget = CampaignBattleSave.EncodePawn(belongsToPlayer: false, 1)
			});
			save.battle.cpuPawns.Add(new CampaignBattlePawnSave { definitionId = "monster-1" });
			save.battle.cpuPawns.Add(new CampaignBattlePawnSave { definitionId = "monster-2" });

			var reloaded = JsonUtility.FromJson<CampaignRunSave>(JsonUtility.ToJson(save));

			Assert.That(reloaded.HasBattle, Is.True);
			Assert.That(reloaded.battle.roundNumber, Is.EqualTo(4));
			Assert.That(reloaded.battle.currentTurnIndex, Is.EqualTo(2));
			Assert.That(reloaded.battle.randomDraws, Is.EqualTo(128), "Senza il conto dei dadi la ripresa li ritira.");
			Assert.That(reloaded.battle.playerPawns[0].eliminated, Is.True, "I morti restano morti.");
			Assert.That(reloaded.battle.playerPawns[0].inhibitedTurns, Is.EqualTo(2));
			Assert.That(
				CampaignBattleSave.DecodeIndex(reloaded.battle.playerPawns[0].markedTarget),
				Is.EqualTo(1));
			Assert.That(reloaded.battle.cpuPawns, Has.Count.EqualTo(2));
		}

		// --- Proprietario del salvataggio ---
		//
		// Questi passano davvero da PlayerPrefs: la chiave per account e l'adozione del
		// salvataggio senza proprietario vivono lì, e un finto store non proverebbe
		// niente. Ogni test si porta via le proprie chiavi.

		private static string UniqueOwner() => "player-" + System.Guid.NewGuid().ToString("N");

		private static void ForgetRun(string owner)
		{
			PlayerPrefs.DeleteKey(PlayerPrefsCampaignRunStore.Key);
			if (owner != null)
				PlayerPrefs.DeleteKey("AccardND.CampaignRun." + owner);
			PlayerPrefs.Save();
		}

		[Test]
		public void TwoAccountsOnTheSameDevice_DoNotSeeEachOthersRun()
		{
			string first = UniqueOwner();
			string second = UniqueOwner();
			ForgetRun(first);
			ForgetRun(second);
			try
			{
				var firstService = new CampaignRunSaveService(
					new PlayerPrefsCampaignRunStore(() => first));
				var secondService = new CampaignRunSaveService(
					new PlayerPrefsCampaignRunStore(() => second));

				firstService.Save(new CampaignRunSave { roomsCleared = 4 });

				Assert.That(firstService.HasSave, Is.True);
				Assert.That(secondService.HasSave, Is.False, "La campagna di un account non va offerta all'altro.");
			}
			finally
			{
				ForgetRun(first);
				ForgetRun(second);
			}
		}

		[Test]
		public void ARunPlayedWithoutAnAccount_IsAdoptedByTheFirstOneThatLogsIn()
		{
			string owner = UniqueOwner();
			ForgetRun(owner);
			try
			{
				// Nessun account: e' la situazione di chi gioca offline, ed e' anche quella
				// di tutti i salvataggi scritti prima che la chiave avesse un proprietario.
				var offline = new CampaignRunSaveService(new PlayerPrefsCampaignRunStore(() => null));
				offline.Save(new CampaignRunSave { roomsCleared = 9 });

				var loggedIn = new CampaignRunSaveService(new PlayerPrefsCampaignRunStore(() => owner));

				Assert.That(loggedIn.TryLoad(out CampaignRunSave adopted), Is.True);
				Assert.That(adopted.roomsCleared, Is.EqualTo(9));
				// Adottato una volta sola: la chiave senza proprietario non resta lì a
				// farsi raccogliere anche dal prossimo account.
				Assert.That(PlayerPrefs.HasKey(PlayerPrefsCampaignRunStore.Key), Is.False);
			}
			finally
			{
				ForgetRun(owner);
			}
		}

		[Test]
		public void AnAdoptionNeverOverwritesACampaignTheAccountAlreadyHas()
		{
			string owner = UniqueOwner();
			ForgetRun(owner);
			try
			{
				var mine = new CampaignRunSaveService(new PlayerPrefsCampaignRunStore(() => owner));
				mine.Save(new CampaignRunSave { roomsCleared = 12 });

				var orphan = new CampaignRunSaveService(new PlayerPrefsCampaignRunStore(() => null));
				orphan.Save(new CampaignRunSave { roomsCleared = 1 });

				Assert.That(mine.TryLoad(out CampaignRunSave loaded), Is.True);
				Assert.That(loaded.roomsCleared, Is.EqualTo(12), "La campagna dell'account vince su quella orfana.");
			}
			finally
			{
				ForgetRun(owner);
			}
		}

		[Test]
		public void Service_LegacySaveWithoutMana_UsesCampaignDefault()
		{
			var store = new InMemoryStore();
			store.Save("{\"version\":1,\"gameVersion\":\"1.0.0\"}");
			var service = new CampaignRunSaveService(store, () => "1.0.0");

			Assert.That(service.TryLoad(out CampaignRunSave loaded), Is.True);
			Assert.That(loaded.playerMana, Is.EqualTo(CampaignRunSave.DefaultPlayerMana));
		}

		// --- La patch ---

		/// <summary>
		/// Una run comincia con le carte, i costi e le stanze della sua versione. Ripresa con
		/// un'altra rimetterebbe in piedi uno stato che quella versione non sa piu' leggere:
		/// il salvataggio non si usa, e si dice al giocatore con che versione l'aveva
		/// cominciata invece di farlo sparire in silenzio.
		/// </summary>
		[Test]
		public void ARunPlayedWithAnotherPatch_IsNotResumed()
		{
			var store = new InMemoryStore();
			new CampaignRunSaveService(store, () => "1.4.0").Save(new CampaignRunSave { roomsCleared = 5 });

			var afterTheUpdate = new CampaignRunSaveService(store, () => "1.5.0");

			Assert.That(afterTheUpdate.Load(out CampaignRunSave loaded),
				Is.EqualTo(CampaignRunLoadResult.OtherGameVersion));
			Assert.That(loaded, Is.Not.Null, "Il salvataggio esce comunque: serve a dire quale versione era.");
			Assert.That(loaded.gameVersion, Is.EqualTo("1.4.0"));
			Assert.That(afterTheUpdate.TryLoad(out CampaignRunSave usable), Is.False);
			Assert.That(usable, Is.Null);
		}

		[Test]
		public void ARunPlayedWithThisPatch_IsResumed()
		{
			var store = new InMemoryStore();
			var service = new CampaignRunSaveService(store, () => "1.4.0");
			service.Save(new CampaignRunSave { roomsCleared = 5 });

			Assert.That(service.Load(out CampaignRunSave loaded), Is.EqualTo(CampaignRunLoadResult.Loaded));
			Assert.That(loaded.roomsCleared, Is.EqualTo(5));
			Assert.That(loaded.gameVersion, Is.EqualTo("1.4.0"), "La patch la timbra il servizio, non il chiamante.");
		}

		[Test]
		public void ASaveWrittenBeforeTheVersionStamp_IsNotResumed()
		{
			var store = new InMemoryStore();
			store.Save("{\"version\":2,\"roomsCleared\":3}");

			Assert.That(new CampaignRunSaveService(store, () => "1.4.0").Load(out _),
				Is.EqualTo(CampaignRunLoadResult.OtherGameVersion));
		}

		// --- La stanza in corso ---

		/// <summary>
		/// Il punto di ripresa non deve mai stare prima di una scelta gia' fatta: se il
		/// giocatore e' entrato in una stanza, il salvataggio deve dire quella stanza e non
		/// la scelta della via, o riaprire il gioco diventa un modo di cambiare porta.
		/// </summary>
		[Test]
		public void AnOpenedRoom_ComesBackAsThatRoomAndNotAsTheChoice()
		{
			var store = new InMemoryStore();
			var service = new CampaignRunSaveService(store, () => "1.4.0");
			var save = new CampaignRunSave
			{
				roomState = new CampaignRoomStateSave
				{
					roomEntered = true,
					roomType = (int)RoomType.Monster,
					scenarioId = "fog",
					roomDifficulty = (int)RoomDifficulty.Hard,
					backgroundIndex = 4,
					entryRandomSeed = 99,
					entryRandomDraws = 128,
					entryCpuRandomSeed = 42,
					entryCpuRandomDraws = 17
				}
			};

			service.Save(save);
			Assert.That(service.TryLoad(out CampaignRunSave loaded), Is.True);

			Assert.That(loaded.HasRoomState, Is.True);
			Assert.That(loaded.roomState.roomEntered, Is.True);
			Assert.That(loaded.roomState.roomType, Is.EqualTo((int)RoomType.Monster));
			Assert.That(loaded.roomState.scenarioId, Is.EqualTo("fog"));
			Assert.That(loaded.roomState.roomDifficulty, Is.EqualTo((int)RoomDifficulty.Hard));
			Assert.That(loaded.roomState.backgroundIndex, Is.EqualTo(4));
			// I dadi della soglia: e' da li' che la stanza si rimonta identica.
			Assert.That(loaded.roomState.entryRandomSeed, Is.EqualTo(99));
			Assert.That(loaded.roomState.entryRandomDraws, Is.EqualTo(128));
			Assert.That(loaded.roomState.entryCpuRandomSeed, Is.EqualTo(42));
			Assert.That(loaded.roomState.entryCpuRandomDraws, Is.EqualTo(17));
		}

		/// <summary>
		/// Le porte estratte sopravvivono con la loro anteprima: il Detector speso su quelle
		/// tre porte deve valere ancora dopo un riavvio, altrimenti bastava riaprire il gioco
		/// per riavere l'oggetto tenendosi quello che si era visto.
		/// </summary>
		[Test]
		public void TheDoorsAlreadyDrawn_AreStillTheSameOnesAfterAReload()
		{
			var store = new InMemoryStore();
			var service = new CampaignRunSaveService(store, () => "1.4.0");
			var room = new CampaignRoomStateSave { roomEntered = false };
			room.doors.Add(new CampaignDoorSave());
			room.doors.Add(new CampaignDoorSave
			{
				revealed = true,
				roomType = (int)RoomType.Merchant,
				scenarioId = "god_merchant",
				difficulty = (int)RoomDifficulty.Hard
			});
			room.doors.Add(new CampaignDoorSave());

			service.Save(new CampaignRunSave { roomState = room });
			Assert.That(service.TryLoad(out CampaignRunSave loaded), Is.True);

			Assert.That(loaded.HasRoomState, Is.True);
			Assert.That(loaded.roomState.roomEntered, Is.False);
			Assert.That(loaded.roomState.doors, Has.Count.EqualTo(3));
			Assert.That(loaded.roomState.doors[0].revealed, Is.False);
			Assert.That(loaded.roomState.doors[1].revealed, Is.True);
			Assert.That(loaded.roomState.doors[1].roomType, Is.EqualTo((int)RoomType.Merchant));
			Assert.That(loaded.roomState.doors[1].scenarioId, Is.EqualTo("god_merchant"));
			Assert.That(loaded.roomState.doors[2].revealed, Is.False);
		}

		[Test]
		public void ASaveWithoutARoomState_DoesNotClaimToHaveOne()
		{
			var store = new InMemoryStore();
			var service = new CampaignRunSaveService(store, () => "1.4.0");
			service.Save(new CampaignRunSave { roomsCleared = 2 });

			Assert.That(service.TryLoad(out CampaignRunSave loaded), Is.True);
			Assert.That(loaded.HasRoomState, Is.False,
				"Senza stanza in corso si riparte dalla scelta della via, come si e' sempre fatto.");
		}

        private static RunProgressState CreateProgress()
        {
            return new RunProgressState(
                experiencePerLevel: 5,
                roomClearExperience: 2,
                maximumLevel: 5,
                roomsPerMasterLevel: 3,
                vigorDiceByLevel: new[] { 6, 8, 10, 12, 20 });
        }

        private static CardDefinition Resolve(List<CardDefinition> definitions, string id)
        {
            foreach (CardDefinition definition in definitions)
                if (definition.Id == id)
                    return definition;
            return null;
        }

        private static List<CardDefinition> CreateCards(int count)
        {
            var result = new List<CardDefinition>();
            for (int index = 0; index < count; index++)
                result.Add(CreateCard($"card-{index}", $"Card {index}", index + 2, HeroClass.Warrior));
            return result;
        }

        private static CardDefinition CreateCard(string id, string displayName, int strength, HeroClass heroClass)
        {
            CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
            card.ApplyImportedData(id, displayName, CardCategory.Monster, null, strength, true, heroClass, true);
            return card;
        }

        private static void DestroyCards(IEnumerable<CardDefinition> cards)
        {
            foreach (CardDefinition card in cards)
                Object.DestroyImmediate(card);
        }

        private sealed class FixedRandom : IRandomSource
        {
            public int NextInclusive(int minimum, int maximum) => minimum;
        }

        private sealed class InMemoryStore : ICampaignRunStore
        {
            private string json;

            public void Save(string value) => json = value;
            public bool TryLoad(out string value)
            {
                value = json;
                return !string.IsNullOrEmpty(value);
            }
            public bool Exists() => !string.IsNullOrEmpty(json);
            public void Delete() => json = null;
        }
    }
}
