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
	private BattleCardState AddCard(ICollection<BattleCardState> destination, RectTransform row, CardDefinition definition, bool belongsToPlayer, int index, CampaignCardInstance campaignCard = null)
	{
		if ((Object)(object)definition == (Object)null || !definition.CanEnterCombat || (Object)(object)row == (Object)null)
		{
			return null;
		}
		PrototypeCardView prototypeCardView = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)row, definition, configuration);
		BattleCardState state = new BattleCardState(definition, prototypeCardView, belongsToPlayer, campaignCard);
		if (belongsToPlayer)
		{
			((UnityEvent)prototypeCardView.Button.onClick).AddListener((UnityAction)delegate
			{
				HandlePlayerCardClick(state);
			});
		}
		else
		{
			((UnityEvent)prototypeCardView.Button.onClick).AddListener((UnityAction)delegate
			{
				HandleCpuCardClick(index);
			});
		}
		destination.Add(state);
		if (!belongsToPlayer && IsComposableGolemDefinition(definition))
		{
			AttachComposableGolemModel(state);
		}
		return state;
	}

	private static bool IsComposableGolemDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, ComposableGolemCardId, StringComparison.OrdinalIgnoreCase);
	}

	private void AttachComposableGolemModel(BattleCardState golem)
	{
		if (golem == null || (Object)(object)golem.View == (Object)null || (Object)(object)playerRow == (Object)null)
		{
			return;
		}
		GameObject prefab = Resources.Load<GameObject>(ComposableGolemModelResourcePath);
		if ((Object)(object)prefab == (Object)null)
		{
			AppendLog("MINIBOSS - modello 3D Golem Componibile non trovato in Resources/Minibosses.");
			return;
		}
		RectTransform cardRect = golem.View.RectTransform;
		RectTransform anchor = new GameObject("Composable Golem Model Preview", new Type[3]
		{
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(RawImage)
		}).GetComponent<RectTransform>();
		((Transform)anchor).SetParent((Transform)(object)cardRect, false);
		SetRect(anchor, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.97f));
		((Transform)anchor).SetAsLastSibling();
		MinibossGolemRenderView preview = ((Component)anchor).gameObject.AddComponent<MinibossGolemRenderView>();
		preview.Configure(cardRect, playerRow, prefab, Vector3.zero, PickGolemFormPlaceholderSprites());
		activeComposableGolemPreview = preview;
		if (activeComposableGolem != null)
		{
			activeComposableGolemPreview.SetActiveForm(activeComposableGolem.ActiveForm.Form, false);
			UpdateComposableGolemHealthBar(golem);
		}
		AppendLog("MINIBOSS - preview 3D Golem Componibile agganciata alla pedina.");
	}

	private void UpdateComposableGolemHealthBar(BattleCardState golem)
	{
		if (golem == null || (Object)(object)golem.View == (Object)null || activeComposableGolem == null)
		{
			return;
		}

		golem.View.SetHealthBar(
			activeComposableGolem.HitPoints,
			activeComposableGolem.MaxHitPoints,
			GolemHealthColor(activeComposableGolem.ActiveForm.Form));
	}

	private Sprite[] PickGolemFormPlaceholderSprites()
	{
		Sprite[] sprites = new Sprite[3];
		if (cardDatabase == null)
		{
			return sprites;
		}

		List<CardDefinition> candidates = cardDatabase.Cards
			.Where((CardDefinition card) => (Object)(object)card != (Object)null && (Object)(object)card.Artwork != (Object)null)
			.ToList();
		for (int i = 0; i < sprites.Length && candidates.Count > 0; i++)
		{
			int index = random.NextInclusive(0, candidates.Count - 1);
			sprites[i] = candidates[index].Artwork;
			candidates.RemoveAt(index);
		}
		return sprites;
	}

	private void MakeDeploymentPreviewInspectable(PrototypeCardView preview, CardDefinition definition)
	{
		if ((Object)(object)preview == (Object)null || (Object)(object)definition == (Object)null)
		{
			return;
		}
		preview.SetInteractable(interactable: true);
		((UnityEvent)preview.Button.onClick).AddListener((UnityAction)delegate
		{
			if (CanInspectDeploymentPreview())
			{
				ShowCardInspection(definition);
			}
		});
	}

	private bool CanInspectDeploymentPreview()
	{
		if (draftActive && deploymentDraftActive && (Object)(object)cardInspectionPanel != (Object)null)
		{
			return !cardInspectionPanel.activeSelf;
		}
		return false;
	}

	private void HandlePlayerCardClick(BattleCardState state)
	{
		if (CanUsePlayerCardAction(state))
		{
			SelectPlayerAbilityTarget(state);
		}
		else if (CanInspectBattleCard(state))
		{
			ShowCardInspection(state);
		}
	}

	private void HandleCpuCardClick(int index)
	{
		BattleCardState state = ((index >= 0 && index < cpuCards.Count) ?cpuCards[index] : null);
		if (CanUseCpuCardAction(index))
		{
			SelectCpuTarget(index);
		}
		else if (CanInspectBattleCard(state))
		{
			ShowCardInspection(state);
		}
	}

	private bool CanInspectBattleCard(BattleCardState state)
	{
		if (state != null && !draftActive && !deploymentDraftActive && !inputLocked && !gameFinished && !attackTargetingActive && pendingAbilityUser == null && pendingDeploymentIndex < 0 && abilityTargetMode == AbilityTargetMode.None && (Object)(object)cardInspectionPanel != (Object)null)
		{
			return !cardInspectionPanel.activeSelf;
		}
		return false;
	}

	private bool CanUsePlayerCardAction(BattleCardState card)
	{
		if (card != null && !inputLocked && !gameFinished)
		{
			if ((abilityTargetMode != AbilityTargetMode.NecromancerAlly || !CanReviveWithNecromancer(card)) && (abilityTargetMode != AbilityTargetMode.PaladinAlly || card.Eliminated || activeAbilityUser == null) && (abilityTargetMode != AbilityTargetMode.PriestAlly || card.Eliminated))
			{
				if (abilityTargetMode == AbilityTargetMode.AttachmentAlly)
				{
					return CanTargetAttachment(activeAttachmentSource, card);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private bool CanUseCpuCardAction(int index)
	{
		if (index >= 0 && index < cpuCards.Count && !inputLocked && !gameFinished && pendingAbilityUser == null && pendingDeploymentIndex < 0 && selectedPlayerIndex >= 0 && !cpuCards[index].Eliminated && turnOrder.Count > 0 && turnOrder[currentTurnIndex].BelongsToPlayer)
		{
			if (!attackTargetingActive && abilityTargetMode != AbilityTargetMode.AssassinEnemy && abilityTargetMode != AbilityTargetMode.MageEnemy)
			{
				return abilityTargetMode == AbilityTargetMode.HunterEnemy && !IsHunterMarked(cpuCards[index]);
			}
			return true;
		}
		return false;
	}

	private void SelectCpuTarget(int index)
	{
		if (inputLocked || gameFinished || selectedPlayerIndex < 0 || index >= cpuCards.Count || cpuCards[index].Eliminated)
		{
			return;
		}
		if (abilityTargetMode == AbilityTargetMode.AssassinEnemy && activeAbilityUser != null)
		{
			BattleCardState battleCardState = cpuCards[index];
			battleCardState.InhibitedTurns = Math.Max(battleCardState.InhibitedTurns, 1);
			battleCardState.WasInhibited = true;
			if (playerAura == BattleAuraType.Assassin)
			{
				battleCardState.PermanentCombatBonus--;
			}
			activeAbilityUser.AbilityArmed = false;
			activeAbilityUser.AbilityUsed = true;
			RefreshPersistentStatus(battleCardState);
			PlayClassAbilitySfx(HeroClass.Assassin);
			string text = ((playerAura == BattleAuraType.Assassin) ?" e gli infligge -1 permanente" : string.Empty);
			SetMessage("ASSASSINO: " + activeAbilityUser.Card.Name + " inibisce " + battleCardState.Card.Name + text + ". Saltera il prossimo turno.");
			TriggerMagicAuraAfterAbility();
			abilityTargetMode = AbilityTargetMode.None;
			ClearTargetHints();
			RefreshAbilityButton(activeAbilityUser);
			activeAbilityUser = null;
			((Component)cancelActionButton).gameObject.SetActive(false);
			UpdateInteractions();
		}
		else if (abilityTargetMode == AbilityTargetMode.MageEnemy && activeAbilityUser != null)
		{
			BattleCardState battleCardState2 = cpuCards[index];
			int num = ((playerAura != BattleAuraType.Mage) ?1 : 2);
			battleCardState2.PendingVigorStepPenalty = Math.Max(battleCardState2.PendingVigorStepPenalty, num);
			activeAbilityUser.AbilityArmed = false;
			activeAbilityUser.AbilityUsed = true;
			RefreshPersistentStatus(battleCardState2);
			PlayClassAbilitySfx(HeroClass.Mage);
			SetMessage($"MAGO: {activeAbilityUser.Card.Name} indebolisce {battleCardState2.Card.Name}. Il suo prossimo Vigore scende di {num} step.");
			TriggerMagicAuraAfterAbility();
			abilityTargetMode = AbilityTargetMode.None;
			ClearTargetHints();
			RefreshAbilityButton(activeAbilityUser);
			activeAbilityUser = null;
			((Component)cancelActionButton).gameObject.SetActive(false);
			UpdateInteractions();
		}
		else if (abilityTargetMode == AbilityTargetMode.HunterEnemy && activeAbilityUser != null)
		{
			BattleCardState battleCardState3 = cpuCards[index];
			if (IsHunterMarked(battleCardState3))
			{
				SetMessage($"CACCIATORE: {battleCardState3.Card.Name} e gia marcato. Scegli un altro bersaglio.");
				UpdateInteractions();
				return;
			}
			if (activeAbilityUser.MarkedTarget != null && !activeAbilityUser.MarkedTarget.Eliminated)
			{
				RefreshPersistentStatus(activeAbilityUser.MarkedTarget);
			}
			activeAbilityUser.MarkedTarget = battleCardState3;
			activeAbilityUser.AbilityArmed = false;
			activeAbilityUser.AbilityUsed = true;
			RefreshPersistentStatus(battleCardState3);
			PlayClassAbilitySfx(HeroClass.Hunter);
			SetMessage($"CACCIATORE: {activeAbilityUser.Card.Name} marca {battleCardState3.Card.Name}. Preda persistente: chi lo attacca prende +{HunterMarkValueFor(activeAbilityUser)}.");
			TriggerMagicAuraAfterAbility();
			abilityTargetMode = AbilityTargetMode.None;
			ClearTargetHints();
			RefreshAbilityButton(activeAbilityUser);
			activeAbilityUser = null;
			((Component)cancelActionButton).gameObject.SetActive(false);
			UpdateInteractions();
		}
		else if (attackTargetingActive)
		{
			attackTargetingActive = false;
			BattleCardState battleCardState4 = turnOrder[currentTurnIndex];
			MatchupResult matchupResult = ClassMatchup.Compare(battleCardState4.Card.HeroClass, cpuCards[index].Card.HeroClass);
			AppendLog("BERSAGLIO SCELTO - " + battleCardState4.Card.Name + " attacca " + cpuCards[index].Card.Name + " " + $"({matchupResult})");
			((MonoBehaviour)this).StartCoroutine(ExecutePlayerTurn(index));
		}
	}

	private void SelectPlayerAbilityTarget(BattleCardState target)
	{
		if (inputLocked || target == null)
		{
			return;
		}
		if (abilityTargetMode == AbilityTargetMode.AttachmentAlly && activeAttachmentSource != null)
		{
			if (CanTargetAttachment(activeAttachmentSource, target))
			{
				((MonoBehaviour)this).StartCoroutine(ExecuteAttachment(activeAttachmentSource, target));
			}
		}
		else
		{
			if (activeAbilityUser == null)
			{
				return;
			}
			if (abilityTargetMode == AbilityTargetMode.NecromancerAlly && CanReviveWithNecromancer(target))
			{
				target.Eliminated = false;
				target.RevivedRound = 0;
				MoveTurnAfter(activeAbilityUser, target);
				target.View.ResetState();
				target.View.SetInitiative(target.Initiative);
				RefreshPersistentStatus(target);
				ApplyPlayerAuraVisuals(appendLog: false);
				activeAbilityUser.AbilityUsed = true;
				PlayClassAbilitySfx(HeroClass.Necromancer);
				SetMessage("NEGROMANTE: " + activeAbilityUser.Card.Name + " rialza " + target.Card.Name + ". Agira subito dopo il Necromancer.");
				TriggerMagicAuraAfterAbility();
			}
			else if (abilityTargetMode == AbilityTargetMode.PaladinAlly && !target.Eliminated)
			{
				activeAbilityUser.AbilityArmed = true;
				activeAbilityUser.ProtectedAlly = target;
				RefreshPersistentStatus(activeAbilityUser);
				PlayClassAbilitySfx(HeroClass.Paladin);
				string text = ((target == activeAbilityUser) ?"si proteggera: vantaggio al prossimo tiro difesa." : ("proteggera " + target.Card.Name + ": se viene attaccato deviera il colpo su di se e difendera con vantaggio."));
				SetMessage("PALADINO: " + activeAbilityUser.Card.Name + " " + text);
			}
			else
			{
				if (abilityTargetMode != AbilityTargetMode.PriestAlly || target.Eliminated)
				{
					return;
				}
				int num = ((playerAura == BattleAuraType.Priest) ?(configuration.ClassBalance.PriestBlessingBonus + 1) : configuration.ClassBalance.PriestBlessingBonus);
				target.PendingAttackBonus += num;
				if (target.PendingAttackBonusKind != PendingAttackBonusKind.Fury)
				{
					target.PendingAttackBonusKind = PendingAttackBonusKind.Blessing;
				}
				RefreshPersistentStatus(target);
				activeAbilityUser.AbilityUsed = true;
				PlayClassAbilitySfx(HeroClass.Priest);
				SetMessage($"SACERDOTE: {activeAbilityUser.Card.Name} benedice {target.Card.Name} con +{num}.");
				TriggerMagicAuraAfterAbility();
			}
			BattleCardState card = activeAbilityUser;
			abilityTargetMode = AbilityTargetMode.None;
			activeAbilityUser = null;
			((Component)cancelActionButton).gameObject.SetActive(false);
			RefreshAbilityButton(card);
			RefreshInitiativeDisplay();
			UpdateInteractions();
		}
	}

	private void MoveTurnAfter(BattleCardState actor, BattleCardState target)
	{
		if (actor == null || target == null || turnOrder.Count == 0)
		{
			return;
		}
		int num = turnOrder.IndexOf(actor);
		if (num < 0)
		{
			num = Mathf.Clamp(currentTurnIndex, 0, turnOrder.Count - 1);
		}
		int num2 = turnOrder.IndexOf(target);
		if (num2 >= 0)
		{
			turnOrder.RemoveAt(num2);
			if (num2 < num)
			{
				num--;
			}
		}
		int index = Mathf.Clamp(num + 1, 0, turnOrder.Count);
		turnOrder.Insert(index, target);
		currentTurnIndex = Mathf.Clamp(num, 0, turnOrder.Count - 1);
	}
}
}
