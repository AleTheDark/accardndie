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
		if ((Object)(object)definition == (Object)null
			|| (!definition.CanEnterCombat && !IsMedusaBossDefinition(definition) && !IsTrentorBossDefinition(definition) && !IsBragusBossDefinition(definition) && !IsPalatirBossDefinition(definition))
			|| (Object)(object)row == (Object)null)
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
			RefreshComposableGolemPawn(state);
		}
		if (!belongsToPlayer && IsMedusaBossDefinition(definition))
		{
			activeMedusaBoss ??= new MedusaBoss(random);
			RefreshMedusaBossPawn(state);
		}
		if (!belongsToPlayer && IsTrentorBossDefinition(definition))
		{
			activeTrentorBoss ??= new TrentorBoss(random);
			PlayTrentorJoinBattlefieldSfx();
			RefreshTrentorBossPawn(state);
		}
		if (!belongsToPlayer && IsBragusBossDefinition(definition))
		{
			activeBragusBoss ??= new BragusBoss(random);
			RefreshBragusBossPawn(state);
		}
		if (!belongsToPlayer && IsPalatirBossDefinition(definition))
		{
			activePalatirBoss ??= new PalatirBoss(random);
			RefreshPalatirBossPawn(state);
		}
		return state;
	}

	private static bool IsComposableGolemDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, ComposableGolemCardId, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsMedusaBossDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, MedusaBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsTrentorBossDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, TrentorBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsBragusBossDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, BragusBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsPalatirBossDefinition(CardDefinition definition)
	{
		return (Object)(object)definition != (Object)null
			&& string.Equals(definition.Id, PalatirBossCardId, StringComparison.OrdinalIgnoreCase);
	}

	private void RefreshBragusBossPawn(BattleCardState bragus)
	{
		if (bragus == null || (Object)(object)bragus.View == (Object)null || activeBragusBoss == null)
		{
			return;
		}
		bragus.View.SetStrengthValue(BragusBoss.CardStrength);
		UpdateBragusBossHealthBar(bragus);
		RefreshPersistentStatus(bragus);
	}

	private void RefreshPalatirBossPawn(BattleCardState palatir)
	{
		if (palatir == null || (Object)(object)palatir.View == (Object)null || activePalatirBoss == null)
		{
			return;
		}
		palatir.View.SetStrengthValue(PalatirBoss.CardStrength);
		palatir.View.SetPalatirShields(activePalatirBoss.ActiveShields);
		UpdatePalatirBossHealthBar(palatir);
		RefreshPersistentStatus(palatir);
	}

	private void UpdatePalatirBossHealthBar(BattleCardState palatir)
	{
		if (palatir == null || (Object)(object)palatir.View == (Object)null || activePalatirBoss == null)
		{
			return;
		}

		palatir.View.SetHealthBar(
			activePalatirBoss.HitPoints,
			activePalatirBoss.MaxHitPoints,
			activePalatirBoss.HasActiveShields
				? new Color(0.35f, 0.18f, 0.95f, 0.98f)
				: new Color(0.96f, 0.34f, 0.78f, 0.98f));
	}

	private void UpdateBragusBossHealthBar(BattleCardState bragus)
	{
		if (bragus == null || (Object)(object)bragus.View == (Object)null || activeBragusBoss == null)
		{
			return;
		}

		bragus.View.SetHealthBar(
			activeBragusBoss.HitPoints,
			activeBragusBoss.MaxHitPoints,
			new Color(0.9f, 0.32f, 0.18f, 0.98f));
	}

	private void RefreshTrentorBossPawn(BattleCardState trentor)
	{
		if (trentor == null || (Object)(object)trentor.View == (Object)null || activeTrentorBoss == null)
		{
			return;
		}
		trentor.View.SetStrengthValue(TrentorBoss.CardStrength);
		UpdateTrentorBossHealthBar(trentor);
		RefreshPersistentStatus(trentor);
	}

	private void UpdateTrentorBossHealthBar(BattleCardState trentor)
	{
		if (trentor == null || (Object)(object)trentor.View == (Object)null || activeTrentorBoss == null)
		{
			return;
		}

		trentor.View.SetHealthBar(
			activeTrentorBoss.HitPoints,
			activeTrentorBoss.MaxHitPoints,
			new Color(0.28f, 0.82f, 0.34f, 0.98f));
	}

	private void RefreshMedusaBossPawn(BattleCardState medusa)
	{
		if (medusa == null || (Object)(object)medusa.View == (Object)null || activeMedusaBoss == null)
		{
			return;
		}
		medusa.View.SetStrengthValue(MedusaBoss.CardStrength);
		UpdateMedusaBossHealthBar(medusa);
		RefreshPersistentStatus(medusa);
	}

	private void UpdateMedusaBossHealthBar(BattleCardState medusa)
	{
		if (medusa == null || (Object)(object)medusa.View == (Object)null || activeMedusaBoss == null)
		{
			return;
		}

		medusa.View.SetHealthBar(
			activeMedusaBoss.HitPoints,
			activeMedusaBoss.MaxHitPoints,
			new Color(0.56f, 0.74f, 0.66f, 0.98f));
	}

	// La pedina del Golem usa la grafica standard delle carte: barra HP colorata
	// in base alla forma attiva e badge di stato con il nome della forma.
	private void RefreshComposableGolemPawn(BattleCardState golem)
	{
		if (golem == null || (Object)(object)golem.View == (Object)null || activeComposableGolem == null)
		{
			return;
		}
		golem.View.SetComposableGolemForm(activeComposableGolem.ActiveForm.Form);
		golem.View.SetStrengthValue(activeComposableGolem.ActiveForm.Power);
		UpdateComposableGolemHealthBar(golem);
		RefreshPersistentStatus(golem);
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

	private void ShowPendingDeploymentInspection()
	{
		if (!draftActive
			|| !deploymentDraftActive
			|| pendingDeploymentIndex < 0
			|| pendingDeploymentIndex >= draftCandidates.Count)
		{
			return;
		}

		ShowCardInspection(draftCandidates[pendingDeploymentIndex]);
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
		if (state != null && !draftActive && !deploymentDraftActive && !attackTargetingActive && pendingAbilityUser == null && pendingDeploymentIndex < 0 && abilityTargetMode == AbilityTargetMode.None && (Object)(object)cardInspectionPanel != (Object)null)
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
			if ((Object)(object)battleAnimationPlayer != (Object)null
				&& (Object)(object)activeAbilityUser.View != (Object)null
				&& (Object)(object)battleCardState.View != (Object)null)
			{
				((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, battleCardState.View, AbilityTargetLineColor));
				((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayAssassinInhibitSmoke(battleCardState.View));
			}
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
			int baseDieSides = runProgress != null ? runProgress.MasterVigorDieSides : configuration.Gameplay.VigorDieSides;
			int startDieSides = EffectiveVigorDieSides(battleCardState2, baseDieSides);
			battleCardState2.PendingVigorStepPenalty = Math.Max(battleCardState2.PendingVigorStepPenalty, num);
			int endDieSides = EffectiveVigorDieSides(battleCardState2, baseDieSides);
			activeAbilityUser.AbilityArmed = false;
			activeAbilityUser.AbilityUsed = true;
			RefreshPersistentStatus(battleCardState2);
			PlayClassAbilitySfx(HeroClass.Mage);
			if ((Object)(object)battleAnimationPlayer != (Object)null
				&& (Object)(object)activeAbilityUser.View != (Object)null
				&& (Object)(object)battleCardState2.View != (Object)null)
			{
				((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, battleCardState2.View, AbilityTargetLineColor));
			}
			((MonoBehaviour)this).StartCoroutine(PlayMageVigorConstellation(
				battleCardState2,
				startDieSides,
				endDieSides));
			SetMessage($"Grazie all'abilita del mago, il prossimo dado Vigore di {battleCardState2.Card.Name} scende di {num} step: usera un D{endDieSides}.");
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
			if ((Object)(object)battleAnimationPlayer != (Object)null
				&& (Object)(object)activeAbilityUser.View != (Object)null
				&& (Object)(object)battleCardState3.View != (Object)null)
			{
				((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, battleCardState3.View, AbilityTargetLineColor));
				((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayHunterMarkReticle(battleCardState3.View));
			}
			SetMessage($"CACCIATORE: {activeAbilityUser.Card.Name} marca {battleCardState3.Card.Name}. Bersaglio marcato: chi lo attacca prende +{HunterMarkValueFor(activeAbilityUser)}.");
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
			MatchupResult matchupResult = IsPalatirBossProxy(cpuCards[index])
				? MatchupResult.Neutral
				: ClassMatchup.Compare(battleCardState4.Card.HeroClass, cpuCards[index].Card.HeroClass);
			AppendLog("BERSAGLIO SCELTO - " + battleCardState4.Card.Name + " attacca " + cpuCards[index].Card.Name + " " + $"({matchupResult})");
			NotifyAdventureTutorial(AdventureTutorialAction.EnemyTargeted);
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
				if ((Object)(object)battleAnimationPlayer != (Object)null)
				{
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, target.View, AbilityTargetLineColor));
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayNecromancerReviveSkullConvergence(target.View));
				}
				SetMessage("NECROMANTE: " + activeAbilityUser.Card.Name + " rialza " + target.Card.Name + ". Agira subito dopo il Necromante.");
				TriggerMagicAuraAfterAbility();
			}
			else if (abilityTargetMode == AbilityTargetMode.PaladinAlly && !target.Eliminated)
			{
				activeAbilityUser.AbilityArmed = true;
				activeAbilityUser.ProtectedAlly = target;
				RefreshPersistentStatus(activeAbilityUser);
				PlayClassAbilitySfx(HeroClass.Paladin);
				if ((Object)(object)battleAnimationPlayer != (Object)null
					&& (Object)(object)activeAbilityUser.View != (Object)null
					&& (Object)(object)target.View != (Object)null)
				{
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, target.View, AbilityTargetLineColor));
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayPaladinProtectionConstellation(target.View));
				}
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
				if ((Object)(object)battleAnimationPlayer != (Object)null
					&& (Object)(object)activeAbilityUser.View != (Object)null
					&& (Object)(object)target.View != (Object)null)
				{
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(activeAbilityUser.View, target.View, AbilityTargetLineColor));
					((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayPriestBlessing(activeAbilityUser.View, target.View, num));
				}
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
