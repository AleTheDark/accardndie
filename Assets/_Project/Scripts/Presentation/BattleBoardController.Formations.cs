using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private List<CardDefinition> DrawMonsterFormationForCurrentDifficulty()
	{
		int formationSize = configuration.Gameplay.FormationSize;
		List<CardDefinition> monsterPool = GetMonsterPoolForDifficulty(pendingRoomDifficulty);
		// Ogni stanza mostro puo' attivare un'aura: cambia solo quale viene premiata dal
		// punteggio, non il fatto che ci sia.
		List<CardDefinition> list = TryDrawSynergyMonsterFormation(
			monsterPool,
			formationSize,
			preferClassAura: pendingRoomDifficulty == RoomDifficulty.Hard);
		List<CardDefinition> list2 = list;
		if (list2 != null)
		{
			return ApplyChapterOnePreMinibossPowerCap(list2, monsterPool, formationSize);
		}
		if (monsterPool.Count >= formationSize)
		{
			list2 = DrawWeightedMonsterCandidates(monsterPool, formationSize, pendingRoomDifficulty);
			return ApplyChapterOnePreMinibossPowerCap(list2, monsterPool, formationSize);
		}
		AppendLog($"MOSTRI {RoomDifficultyRules.For(pendingRoomDifficulty).DisplayName.ToUpperInvariant()} FALLBACK - pool insufficiente ({monsterPool.Count}/{formationSize}).");
		int maximumCardStrength = RoomDifficultyRules.For(pendingRoomDifficulty).MaximumMonsterCardStrength;
		List<CardDefinition> list3 = cardDatabase.Cards.Where((CardDefinition card) => (Object)(object)card != (Object)null && card.Category == CardCategory.Monster && card.CanEnterCombat && card.Strength <= maximumCardStrength).ToList();
		list2 = DrawWeightedMonsterCandidates(list3, Mathf.Min(formationSize, list3.Count), pendingRoomDifficulty);
		return ApplyChapterOnePreMinibossPowerCap(list2, list3, formationSize);
	}

	private List<CardDefinition> ApplyChapterOnePreMinibossPowerCap(
		List<CardDefinition> formation,
		List<CardDefinition> preferredPool,
		int formationSize)
	{
		const int maximumPower = 24;
		bool beforeFirstMiniboss = runProgress != null
			&& runProgress.RoomsCleared < configuration.Progression.MinibossEveryRooms;
		bool isChapterOne = string.Equals(campaignScenarioId, "fog", StringComparison.OrdinalIgnoreCase);
		if (!isChapterOne || !beforeFirstMiniboss || formation.Sum(card => card.Strength) <= maximumPower)
		{
			return formation;
		}

		List<List<CardDefinition>> candidates = BuildFormationCandidates(preferredPool, formationSize)
			.Where(candidate => candidate.Sum(card => card.Strength) <= maximumPower)
			.ToList();
		if (candidates.Count == 0)
		{
			List<CardDefinition> allMonsters = cardDatabase.Cards
				.Where(card => (Object)(object)card != (Object)null
					&& card.Category == CardCategory.Monster
					&& card.CanEnterCombat
					&& card.Strength <= RoomDifficultyRules.For(pendingRoomDifficulty).MaximumMonsterCardStrength)
				.ToList();
			candidates = BuildFormationCandidates(allMonsters, formationSize)
				.Where(candidate => candidate.Sum(card => card.Strength) <= maximumPower)
				.ToList();
		}
		if (candidates.Count == 0)
		{
			AppendLog("CAPITOLO 1 - impossibile creare una formazione entro potenza 24.");
			return formation;
		}

		List<CardDefinition> cappedFormation = PickScoredFormation(
			candidates,
			candidate => candidate.Sum(card => card.Strength));
		AppendLog($"CAPITOLO 1 - formazione pre-miniboss limitata a potenza {cappedFormation.Sum(card => card.Strength)}/24.");
		return cappedFormation;
	}

	private List<CardDefinition> DrawMonsterFormationForCurrentCombat()
	{
		if (survivingCpuFormation.Count == 0)
		{
			return DrawMonsterFormationForCurrentDifficulty();
		}
		return new List<CardDefinition>(survivingCpuFormation);
	}

	private List<CardDefinition> GetMonsterPoolForDifficulty(RoomDifficulty difficulty)
	{
		int minimumStrength;
		int maximumStrength;
		switch (difficulty)
		{
		case RoomDifficulty.Easy:
			minimumStrength = 2;
			maximumStrength = 6;
			break;
		case RoomDifficulty.Normal:
			minimumStrength = 4;
			maximumStrength = 8;
			break;
		default:
			minimumStrength = 6;
			maximumStrength = 10;
			break;
		}
		maximumStrength = Mathf.Min(maximumStrength, RoomDifficultyRules.For(pendingRoomDifficulty).MaximumMonsterCardStrength);
		return cardDatabase.Cards.Where((CardDefinition card) => (Object)(object)card != (Object)null && card.Category == CardCategory.Monster && card.CanEnterCombat && card.Strength >= minimumStrength && card.Strength <= maximumStrength).ToList();
	}

	private List<CardDefinition> TryDrawSynergyMonsterFormation(List<CardDefinition> pool, int count, bool preferClassAura)
	{
		if (count != 3 || pool.Count < count)
		{
			return null;
		}
		List<List<CardDefinition>> candidates = (from formation in BuildFormationCandidates(pool, count)
			where DetermineAura(formation) != BattleAuraType.None
			select formation).ToList();
		return PickScoredFormation(candidates, delegate(List<CardDefinition> formation)
		{
			BattleAuraType aura = DetermineAura(formation);
			int num = ((preferClassAura && IsClassAura(aura)) ?30 : 12);
			int num2 = ((!preferClassAura && IsFamilyAura(aura)) ?18 : 0);
			return formation.Sum((CardDefinition card) => card.Strength) + num + num2;
		});
	}

	private List<CardDefinition> DrawWeightedMonsterCandidates(List<CardDefinition> pool, int count, RoomDifficulty difficulty)
	{
		if (pool.Count <= count)
		{
			return new List<CardDefinition>(pool);
		}
		List<CardDefinition> list = new List<CardDefinition>(pool);
		List<CardDefinition> list2 = new List<CardDefinition>(count);
		while (list2.Count < count && list.Count > 0)
		{
			int val = list.Sum((CardDefinition card) => MonsterDifficultyWeight(card, difficulty));
			int num = random.NextInclusive(1, Math.Max(1, val));
			for (int num2 = 0; num2 < list.Count; num2++)
			{
				num -= MonsterDifficultyWeight(list[num2], difficulty);
				if (num <= 0)
				{
					list2.Add(list[num2]);
					list.RemoveAt(num2);
					break;
				}
			}
		}
		return list2;
	}

	private static int MonsterDifficultyWeight(CardDefinition card, RoomDifficulty difficulty)
	{
		int num = Mathf.Clamp(card.Strength, 1, 10);
		return difficulty switch
		{
			RoomDifficulty.Easy => 8 + Math.Abs(4 - num) * -1,
			RoomDifficulty.Normal => num,
			_ => num * num, 
		};
	}

	private static List<List<CardDefinition>> BuildFormationCandidates(IReadOnlyList<CardDefinition> pool, int count)
	{
		List<List<CardDefinition>> list = new List<List<CardDefinition>>();
		if (count != 3)
		{
			return list;
		}
		for (int i = 0; i < pool.Count - 2; i++)
		{
			for (int j = i + 1; j < pool.Count - 1; j++)
			{
				for (int k = j + 1; k < pool.Count; k++)
				{
					list.Add(new List<CardDefinition>
					{
						pool[i],
						pool[j],
						pool[k]
					});
				}
			}
		}
		return list;
	}

	private List<CardDefinition> PickScoredFormation(List<List<CardDefinition>> candidates, Func<List<CardDefinition>, int> score)
	{
		if (candidates.Count == 0)
		{
			return null;
		}
		List<List<CardDefinition>> list = candidates.OrderByDescending(score).Take(Mathf.Max(1, candidates.Count / 4)).ToList();
		return list[random.NextInclusive(0, list.Count - 1)];
	}

	private static BattleAuraType DetermineAura(IReadOnlyList<CardDefinition> formation)
	{
		if (formation == null || formation.Count != 3)
		{
			return BattleAuraType.None;
		}
		if (formation.All((CardDefinition card) => card.HeroClass == formation[0].HeroClass))
		{
			return formation[0].HeroClass switch
			{
				HeroClass.Warrior => BattleAuraType.Warrior, 
				HeroClass.Barbarian => BattleAuraType.Barbarian, 
				HeroClass.Paladin => BattleAuraType.Paladin, 
				HeroClass.Rogue => BattleAuraType.Rogue, 
				HeroClass.Assassin => BattleAuraType.Assassin, 
				HeroClass.Hunter => BattleAuraType.Hunter, 
				HeroClass.Mage => BattleAuraType.Mage, 
				HeroClass.Necromancer => BattleAuraType.Necromancer, 
				HeroClass.Priest => BattleAuraType.Priest, 
				_ => BattleAuraType.None, 
			};
		}
		List<ClassFamily> list = formation.Select((CardDefinition card) => HeroClassFamily.Of(card.HeroClass)).ToList();
		if (list.Contains(ClassFamily.Might) && list.Contains(ClassFamily.Cunning) && list.Contains(ClassFamily.Magic))
		{
			return BattleAuraType.Formation;
		}
		if (list.All((ClassFamily family) => family == ClassFamily.Might))
		{
			return BattleAuraType.Might;
		}
		if (list.All((ClassFamily family) => family == ClassFamily.Cunning))
		{
			return BattleAuraType.Cunning;
		}
		if (list.All((ClassFamily family) => family == ClassFamily.Magic))
		{
			return BattleAuraType.Magic;
		}
		return BattleAuraType.None;
	}

	private static bool IsClassAura(BattleAuraType aura)
	{
		if (aura != BattleAuraType.Warrior && aura != BattleAuraType.Barbarian && aura != BattleAuraType.Paladin && aura != BattleAuraType.Rogue && aura != BattleAuraType.Assassin && aura != BattleAuraType.Hunter && aura != BattleAuraType.Mage && aura != BattleAuraType.Necromancer)
		{
			return aura == BattleAuraType.Priest;
		}
		return true;
	}

	private static bool IsFamilyAura(BattleAuraType aura)
	{
		if (aura != BattleAuraType.Might && aura != BattleAuraType.Cunning && aura != BattleAuraType.Magic)
		{
			return aura == BattleAuraType.Formation;
		}
		return true;
	}
}
}
