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
	private List<CardDefinition> DrawMonsterFormationForCurrentTier()
	{
		int formationSize = configuration.Gameplay.FormationSize;
		List<CardDefinition> monsterPoolForTier = GetMonsterPoolForTier(currentMonsterTier);
		int num = currentMonsterTier;
		List<CardDefinition> list = ((num <= 1) ?TryDrawNoAuraMonsterFormation(monsterPoolForTier, formationSize) : ((num >= 4) ?TryDrawSynergyMonsterFormation(monsterPoolForTier, formationSize, preferClassAura: true) : ((num != 3) ?null : TryDrawSynergyMonsterFormation(monsterPoolForTier, formationSize, preferClassAura: false))));
		List<CardDefinition> list2 = list;
		if (list2 != null)
		{
			return list2;
		}
		if (monsterPoolForTier.Count >= formationSize)
		{
			return DrawWeightedMonsterCandidates(monsterPoolForTier, formationSize, currentMonsterTier);
		}
		AppendLog($"MOSTRI TIER {currentMonsterTier} FALLBACK - pool insufficiente ({monsterPoolForTier.Count}/{formationSize}).");
		List<CardDefinition> list3 = cardDatabase.Cards.Where((CardDefinition card) => (Object)(object)card != (Object)null && card.Category == CardCategory.Monster && card.CanEnterCombat).ToList();
		return DrawWeightedMonsterCandidates(list3, Mathf.Min(formationSize, list3.Count), currentMonsterTier);
	}

	private List<CardDefinition> DrawMonsterFormationForCurrentCombat()
	{
		if (survivingCpuFormation.Count == 0)
		{
			return DrawMonsterFormationForCurrentTier();
		}
		return new List<CardDefinition>(survivingCpuFormation);
	}

	private List<CardDefinition> GetMonsterPoolForTier(int tier)
	{
		int minimumStrength;
		int maximumStrength;
		switch (Mathf.Clamp(tier, 1, 4))
		{
		case 1:
			minimumStrength = 2;
			maximumStrength = 4;
			break;
		case 2:
			minimumStrength = 2;
			maximumStrength = 6;
			break;
		case 3:
			minimumStrength = 4;
			maximumStrength = 8;
			break;
		default:
			minimumStrength = 6;
			maximumStrength = 10;
			break;
		}
		return cardDatabase.Cards.Where((CardDefinition card) => (Object)(object)card != (Object)null && card.Category == CardCategory.Monster && card.CanEnterCombat && card.Strength >= minimumStrength && card.Strength <= maximumStrength).ToList();
	}

	private List<CardDefinition> TryDrawNoAuraMonsterFormation(List<CardDefinition> pool, int count)
	{
		if (count != 3 || pool.Count < count)
		{
			return null;
		}
		List<List<CardDefinition>> candidates = (from formation in BuildFormationCandidates(pool, count)
			where DetermineAura(formation) == BattleAuraType.None
			select formation).ToList();
		return PickScoredFormation(candidates, (List<CardDefinition> formation) => -formation.Sum((CardDefinition card) => card.Strength));
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

	private List<CardDefinition> DrawWeightedMonsterCandidates(List<CardDefinition> pool, int count, int tier)
	{
		if (pool.Count <= count)
		{
			return new List<CardDefinition>(pool);
		}
		List<CardDefinition> list = new List<CardDefinition>(pool);
		List<CardDefinition> list2 = new List<CardDefinition>(count);
		while (list2.Count < count && list.Count > 0)
		{
			int val = list.Sum((CardDefinition card) => MonsterTierWeight(card, tier));
			int num = random.NextInclusive(1, Math.Max(1, val));
			for (int num2 = 0; num2 < list.Count; num2++)
			{
				num -= MonsterTierWeight(list[num2], tier);
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

	private static int MonsterTierWeight(CardDefinition card, int tier)
	{
		int num = Mathf.Clamp(card.Strength, 1, 10);
		return Mathf.Clamp(tier, 1, 4) switch
		{
			1 => 12 - num, 
			2 => 8 + Math.Abs(4 - num) * -1, 
			3 => num, 
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
