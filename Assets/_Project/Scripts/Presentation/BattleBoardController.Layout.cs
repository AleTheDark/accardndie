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
	private const float LandscapePlayerFormationLift = 0.045f;
	private const float OpponentFormationLift = 0.05f;
	private const float LandscapeHandDrop = -0.035f;
	private const float LandscapeMessageLift = 0.055f;

	private void ApplyResponsiveLayout()
	{
		if (!((Object)(object)safeAreaRoot == (Object)null) && !((Object)(object)canvasRect == (Object)null))
		{
			previousScreenWidth = Screen.width;
			previousScreenHeight = Screen.height;
			previousSafeArea = Screen.safeArea;
			previousScreenOrientation = Screen.orientation;
			Rect safeArea = Screen.safeArea;
			float num = Mathf.Max(1f, (float)Screen.width);
			float num2 = Mathf.Max(1f, (float)Screen.height);
			RefreshScenarioBackground();
			safeAreaRoot.anchorMin = new Vector2(safeArea.xMin / num, safeArea.yMin / num2);
			safeAreaRoot.anchorMax = new Vector2(safeArea.xMax / num, safeArea.yMax / num2);
			safeAreaRoot.offsetMin = Vector2.zero;
			safeAreaRoot.offsetMax = Vector2.zero;
			float num3 = Mathf.Max(1f, safeArea.width);
			float num4 = Mathf.Max(1f, safeArea.height);
			float num5 = num3 / num4;
			RefreshModeSelectionLayout();
			RefreshCampaignModeSelectionLayout();
			RefreshAdventureChapterLayout();
			RefreshRoomChoiceLayout();
			RefreshCardInspectionLayout();
			RefreshDeckBuilderLayout();
			RefreshInitialDraftLayout();
			ResponsiveLayoutConfiguration responsiveLayout = configuration.ResponsiveLayout;
			bool flag = IsCompactLayout(num5, responsiveLayout);
			bool flag2 = !flag && num5 >= 1.65f;
			canvasScaler.referenceResolution = (flag ?responsiveLayout.PortraitReferenceResolution : responsiveLayout.LandscapeReferenceResolution);
			canvasScaler.matchWidthOrHeight = (flag ?1f : (flag2 ?0.25f : 0f));
			Canvas.ForceUpdateCanvases();
			Rect rect = safeAreaRoot.rect;
			float width = rect.width;
			rect = safeAreaRoot.rect;
			float height = rect.height;
			if (width <= 0f || height <= 0f)
			{
				rect = canvasRect.rect;
				width = rect.width;
				rect = canvasRect.rect;
				height = rect.height;
			}
			float num6 = (flag ?(width * responsiveLayout.CompactRowWidth) : Mathf.Min(width * (flag2 ?0.82f : responsiveLayout.LandscapeRowWidth), responsiveLayout.LandscapeMaximumRowWidth));
			float num7 = Mathf.Clamp(num6 * responsiveLayout.GapFraction, responsiveLayout.MinimumGap, responsiveLayout.MaximumGap);
			int num8 = Mathf.Max(1, configuration.Gameplay.FormationSize);
			float num9 = (num6 - num7 * (float)(num8 - 1)) / (float)num8;
			float num10 = height * (flag ?responsiveLayout.CompactCardHeight : (flag2 ?0.305f : responsiveLayout.LandscapeCardHeight * 0.92f));
			float num11 = Mathf.Min(num9, num10 * 1f);
			float width2 = num11 * (float)num8 + num7 * (float)(num8 - 1);
			bool flag3 = (currentRoomType == RoomType.Monster || currentRoomType == RoomType.Boss) && (draftActive || deploymentDraftActive || playerCards.Count > 0 || cpuCards.Count > 0);
			bool flag4 = IsMerchantActionHudVisible() || IsSingleActionNonCombatHudVisible();
			float anchor = ((!flag3) ?(flag ?0.775f : (flag2 ?0.845f : 0.79f)) : (flag ?0.76f : (flag2 ?0.845f : 0.79f)));
			anchor += OpponentFormationLift;
			anchor = ClampBattlefieldAnchor(anchor, num11, height, flag ?0.055f : 0.08f, (!flag3) ?(flag ?0.87f : (flag2 ?0.995f : 0.94f)) : (flag ?0.835f : (flag2 ?0.995f : 0.94f)));
			ConfigureBattlefieldRow(cpuRow, cpuCards, width2, num11, num11, num7, anchor);
			float anchor2 = ((!deploymentDraftActive) ?(flag ?0.205f : (flag2 ?0.145f : 0.15f)) : (flag ?0.35f : (flag2 ?0.24f : 0.25f)));
			if (!flag)
			{
				anchor2 += LandscapePlayerFormationLift;
			}
			anchor2 = ClampBattlefieldAnchor(anchor2, num11, height, flag ?0.06f : 0.025f, Mathf.Max(0.12f, anchor - num11 / Mathf.Max(1f, height) - 0.035f));
			ConfigureBattlefieldRow(playerRow, playerCards, width2, num11, num11, num7, anchor2);
			int num12 = (((Object)(object)playerHandRow != (Object)null) ?((Transform)playerHandRow).childCount : 0);
			if (num12 > 0)
			{
				float handOverlap = responsiveLayout.HandOverlap;
				float num13 = (float)num12 - (float)(num12 - 1) * handOverlap;
				int handSizingCount = draftActive
					? Mathf.Max(num12, draftCandidates.Count)
					: Mathf.Max(num12, configuration.DeckBuilding.CombatHandSize);
				handSizingCount = Mathf.Max(handSizingCount, configuration.DeckBuilding.CombatHandSize);
				float handSizingSpan = (float)handSizingCount - (float)(handSizingCount - 1) * handOverlap;
				float num14 = num6 * (flag2 ?0.96f : 0.9f) / Mathf.Max(1f, handSizingSpan);
				float num15 = height * (flag ?responsiveLayout.CompactHandHeight : (flag2 ?0.4f : responsiveLayout.LandscapeHandHeight));
				float num16 = Mathf.Min(num14, num15 * 0.6708861f);
				float cardHeight = num16 / 0.6708861f;
				float width3 = num16 * num13;
				float handVerticalAnchor = flag ?0.13f : LandscapeHandDrop;
				if (!flag
					&& pvpPresentationActive
					&& pvpState != null
					&& pvpState.Phase == AccardND.NetProtocol.PvpClientPhase.DecisiveSelection)
				{
					handVerticalAnchor = LandscapeHandDrop;
				}
				ConfigureRow(playerHandRow, width3, num16, cardHeight, (0f - num16) * handOverlap, handVerticalAnchor);
			}
			if (flag)
			{
				SetRect(tableGlowRect, new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f));
				SetRect(topInfoBarRect, new Vector2(0.035f, 0.952f), new Vector2(0.7f, 0.992f));
				SetRect(playerHud.Rect, new Vector2(0.2275f, 0.002f), new Vector2(0.7725f, 0.09f));
				SetRect(cpuHud.Rect, new Vector2(0.2275f, 0.907f), new Vector2(0.7725f, 0.992f));
				SetRect((RectTransform)((Component)logButton).transform, new Vector2(0.81f, 0.917f), new Vector2(0.982f, 0.995f));
				if ((Object)(object)settingsButtonLabel != (Object)null)
				{
					SetRect(settingsButtonLabel.rectTransform, new Vector2(0.785f, 0.895f), new Vector2(1f, 0.937f));
				}
				ConfigureLogPanelRect(compact: true, wideLandscape: false);
				if ((Object)(object)optionsPanel != (Object)null)
				{
					SetRect((RectTransform)optionsPanel.transform, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.9f));
				}
				SetRect(implementationArchiveButtonRect, new Vector2(0.81f, 0.02f), new Vector2(0.982f, 0.102f));
				if ((Object)(object)implementationArchiveButtonLabel != (Object)null)
				{
					SetRect(implementationArchiveButtonLabel.rectTransform, new Vector2(0.785f, 0f), new Vector2(1f, 0.042f));
				}
				SetRect(implementationArchivePanelRect, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.94f));
				SetRect(messagePanelRect, flag4 ?new Vector2(0.08f, 0.055f) : (flag3 ?new Vector2(0.02f, 0.43f) : new Vector2(0.08f, 0.065f)), flag4 ?new Vector2(0.92f, 0.215f) : (flag3 ?new Vector2(0.82f, 0.535f) : new Vector2(0.92f, 0.17f)));
				SetTimelineBaseRect(new Vector2(0.78f, 0.30f), new Vector2(0.998f, 0.70f));
				ConfigureTimelineLayout(vertical: true);
				SetRect(cpuTitleRect, flag3 ?new Vector2(0.05f, 0.78f) : new Vector2(0.05f, 0.848f), flag3 ?new Vector2(0.95f, 0.822f) : new Vector2(0.95f, 0.89f));
				SetRect(roundText.rectTransform, new Vector2(0.05f, 0.545f), new Vector2(0.62f, 0.59f));
				SetRect(campaignZoneRect, new Vector2(0.64f, 0.545f), new Vector2(0.95f, 0.59f));
				SetRect(playerTitleRect, new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.395f));
			}
			else
			{
				SetRect(tableGlowRect, flag2 ?new Vector2(0.04f, 0.105f) : new Vector2(0.08f, 0.13f), flag2 ?new Vector2(0.96f, 0.895f) : new Vector2(0.92f, 0.87f));
				SetRect(topInfoBarRect, flag2 ?new Vector2(0.05f, 0.925f) : new Vector2(0.08f, 0.93f), new Vector2(0.73f, 0.985f));
				SetRect(playerHud.Rect, flag2 ?new Vector2(0.385f, 0.035f) : new Vector2(0.385f, 0.035f), flag2 ?new Vector2(0.615f, 0.197f) : new Vector2(0.615f, 0.198f));
				SetRect(cpuHud.Rect, flag2 ?new Vector2(0.385f, 0.823f) : new Vector2(0.385f, 0.822f), flag2 ?new Vector2(0.615f, 0.985f) : new Vector2(0.615f, 0.985f));
				SetRect((RectTransform)((Component)logButton).transform, new Vector2(0.84f, 0.852f), new Vector2(0.995f, 0.992f));
				if ((Object)(object)settingsButtonLabel != (Object)null)
				{
					SetRect(settingsButtonLabel.rectTransform, new Vector2(0.81f, 0.865f), new Vector2(1f, 0.919f));
				}
				ConfigureLogPanelRect(false, flag2);
				if ((Object)(object)optionsPanel != (Object)null)
				{
					SetRect((RectTransform)optionsPanel.transform, new Vector2(0.64f, 0.52f), new Vector2(0.98f, 0.92f));
				}
				SetRect(implementationArchiveButtonRect, new Vector2(0.84f, 0.068f), new Vector2(0.995f, 0.218f));
				if ((Object)(object)implementationArchiveButtonLabel != (Object)null)
				{
					SetRect(implementationArchiveButtonLabel.rectTransform, new Vector2(0.81f, 0.012f), new Vector2(1f, 0.068f));
				}
				SetRect(implementationArchivePanelRect, new Vector2(0.62f, 0.05f), new Vector2(0.98f, 0.94f));
				SetRect(messagePanelRect,
					(flag4 ?new Vector2(0.28f, 0.39f) : (flag3 ?new Vector2(0.26f, 0.43f) : new Vector2(0.28f, 0.43f))) + new Vector2(0f, LandscapeMessageLift),
					(flag4 ?new Vector2(0.72f, 0.61f) : (flag3 ?new Vector2(0.74f, 0.57f) : new Vector2(0.72f, 0.57f))) + new Vector2(0f, LandscapeMessageLift));
				SetTimelineBaseRect(flag2 ?new Vector2(0.84f, 0.31f) : new Vector2(0.82f, 0.31f), flag2 ?new Vector2(0.985f, 0.69f) : new Vector2(0.965f, 0.69f));
				ConfigureTimelineLayout(vertical: true);
				SetRect(cpuTitleRect, flag3 ?new Vector2(0.12f, 0.725f) : new Vector2(0.12f, 0.805f), flag3 ?new Vector2(0.88f, 0.765f) : new Vector2(0.88f, 0.85f));
				SetRect(roundText.rectTransform, new Vector2(0.17f, 0.575f), new Vector2(0.55f, 0.625f));
				SetRect(campaignZoneRect, new Vector2(0.57f, 0.575f), new Vector2(0.83f, 0.625f));
				SetRect(playerTitleRect, new Vector2(0.12f, 0.32f), new Vector2(0.88f, 0.38f));
			}
			if (IsCampaignEndedBannerVisible())
			{
				SetCenteredCampaignEndedMessagePanel();
			}
			ApplyCompactChromeVisibility(flag);
			ApplyResponsiveTextSizing(flag);
			Canvas.ForceUpdateCanvases();
			ResizeTimelineTiles();
		}
	}

	private void ApplyResponsiveTextSizing(bool compact)
	{
		SetResponsiveText(topInfoText, compact ?24 : 19, compact ?18 : 13);
		SetResponsiveText(roundText, compact ?24 : 20, compact ?18 : 14);
		SetResponsiveText(campaignZoneText, compact ?22 : 18, compact ?16 : 13);
		SetResponsiveText(cpuTitleText, compact ?28 : 25, compact ?20 : 17);
		SetResponsiveText(playerTitleText, compact ?28 : 25, compact ?20 : 17);
		SetResponsiveText(messageText, compact ?29 : 22, compact ?21 : 16);
		SetResponsiveText(turnBannerText, compact ?30 : 24, compact ?22 : 17);
		SetResponsiveHudText(playerHud, compact);
		SetResponsiveHudText(cpuHud, compact);
		if ((Object)(object)hudTooltipText != (Object)null)
		{
			SetResponsiveText(hudTooltipText, compact ?24 : 18, compact ?18 : 13);
		}
	}

	private static void SetResponsiveHudText(CombatantHud hud, bool compact)
	{
		if (hud == null)
		{
			return;
		}
		SetResponsiveText(hud.NameText, compact ?24 : 21, compact ?17 : 14);
		SetResponsiveText(hud.LevelText, compact ?20 : 16, compact ?15 : 12);
		SetResponsiveText(hud.ExperienceText, compact ?18 : 14, compact ?13 : 10);
		SetResponsiveText(hud.DiceText, compact ?19 : 15, compact ?14 : 11);
		SetResponsiveText(hud.DeckText, compact ?19 : 15, compact ?14 : 11);
		SetResponsiveText(hud.CooldownText, compact ?19 : 15, compact ?14 : 11);
		SetResponsiveText(hud.GraveyardText, compact ?19 : 15, compact ?14 : 11);
	}

	private static void SetResponsiveText(Text text, int maxSize, int minSize)
	{
		if ((Object)(object)text == (Object)null)
		{
			return;
		}
		text.fontSize = maxSize;
		text.resizeTextMaxSize = maxSize;
		text.resizeTextMinSize = Mathf.Min(maxSize, minSize);
	}

	private bool IsCurrentCompactLayout()
	{
		float width = Mathf.Max(1f, Screen.safeArea.width);
		float height = Mathf.Max(1f, Screen.safeArea.height);
		return IsCompactLayout(width / height, configuration.ResponsiveLayout);
	}

	private void ConfigureLogPanelRect(bool compact, bool wideLandscape)
	{
		if ((Object)(object)logPanel == (Object)null)
		{
			return;
		}

		RectTransform rectTransform = (RectTransform)logPanel.transform;
		SetRect(
			rectTransform,
			compact ?new Vector2(0.015f, 0.035f) : (wideLandscape ?new Vector2(0.08f, 0.08f) : new Vector2(0.04f, 0.07f)),
			compact ?new Vector2(0.985f, 0.925f) : (wideLandscape ?new Vector2(0.92f, 0.895f) : new Vector2(0.96f, 0.895f)));
		ConfigureLogTextRect();
	}

	private void ApplyCompactChromeVisibility(bool compact)
	{
		if (cpuHud != null && (Object)(object)cpuHud.Rect != (Object)null)
		{
			((Component)cpuHud.Rect).gameObject.SetActive(combatChromeVisible);
		}
		if ((Object)(object)cpuTitleText != (Object)null)
		{
			((Component)cpuTitleText).gameObject.SetActive(false);
		}
		if ((Object)(object)topInfoBarRect != (Object)null)
		{
			((Component)topInfoBarRect).gameObject.SetActive(false);
		}
		if ((Object)(object)roundText != (Object)null)
		{
			((Component)roundText).gameObject.SetActive(false);
		}
	}

	private static bool IsCompactLayout(float aspect, ResponsiveLayoutConfiguration layoutConfiguration)
	{
		if ((int)Screen.orientation == 3 || (int)Screen.orientation == 4)
		{
			return false;
		}
		if ((int)Screen.orientation == 1 || (int)Screen.orientation == 2)
		{
			return true;
		}
		return aspect < layoutConfiguration.CompactAspectBreakpoint;
	}

	private void ConfigureTimelineLayout(bool vertical)
	{
		timelineLayoutVertical = vertical;
		ResizeTimelineTiles();
	}

	private void SetTimelineBaseRect(Vector2 minimum, Vector2 maximum)
	{
		timelineBackgroundBaseMin = minimum;
		timelineBackgroundBaseMax = maximum;
		hasTimelineBackgroundBaseRect = true;
		SetRect(timelineBackgroundRect, minimum, maximum);
	}

	private void RestoreTimelineBaseRect()
	{
		if ((Object)(object)timelineBackgroundRect == (Object)null || !hasTimelineBackgroundBaseRect)
		{
			return;
		}
		SetRect(timelineBackgroundRect, timelineBackgroundBaseMin, timelineBackgroundBaseMax);
	}

	private bool IsTimelineVerticalLayout()
	{
		return timelineLayoutVertical;
	}

	private static float ClampBattlefieldAnchor(float anchor, float cardHeight, float layoutHeight, float minimum, float maximum)
	{
		float num = cardHeight / Mathf.Max(1f, layoutHeight) * 0.5f;
		float num2 = minimum + num;
		float num3 = Mathf.Max(num2, maximum - num);
		return Mathf.Clamp(anchor, num2, num3);
	}

	private void ApplyHandFan()
	{
		if ((Object)(object)playerHandRow == (Object)null || ((Transform)playerHandRow).childCount < 1)
		{
			return;
		}
		ResponsiveLayoutConfiguration responsiveLayout = configuration.ResponsiveLayout;
		int childCount = ((Transform)playerHandRow).childCount;
		for (int i = 0; i < childCount; i++)
		{
			Transform child = ((Transform)playerHandRow).GetChild(i);
			RectTransform val = (RectTransform)(object)((child is RectTransform) ?child : null);
			if (!((Object)(object)val == (Object)null))
			{
				PrototypeCardView component = ((Component)val).GetComponent<PrototypeCardView>();
				if (!((Object)(object)component != (Object)null) || (!component.IsDragging && !draftEntranceAnimatingViews.Contains(component) && !handRelayoutAnimatingViews.Contains(component)))
				{
					float num = ((childCount <= 1) ?0f : ((float)i / (float)(childCount - 1) * 2f - 1f));
					Vector2 anchoredPosition = val.anchoredPosition;
					anchoredPosition.y = (0f - Mathf.Abs(num)) * responsiveLayout.HandEdgeDrop;
					val.anchoredPosition = anchoredPosition;
					((Transform)val).localRotation = Quaternion.Euler(0f, 0f, (0f - num) * responsiveLayout.HandMaximumAngle);
				}
			}
		}
	}

	private static void ConfigureRow(RectTransform row, float width, float cardWidth, float cardHeight, float spacing, float verticalAnchor)
	{
		row.anchorMin = new Vector2(0.5f, verticalAnchor);
		row.anchorMax = new Vector2(0.5f, verticalAnchor);
		row.anchoredPosition = Vector2.zero;
		row.sizeDelta = new Vector2(width, cardHeight);
		HorizontalLayoutGroup component = ((Component)row).GetComponent<HorizontalLayoutGroup>();
		component.spacing = spacing;
		component.childForceExpandWidth = false;
		component.childForceExpandHeight = false;
		for (int i = 0; i < ((Transform)row).childCount; i++)
		{
			LayoutElement component2 = ((Component)((Transform)row).GetChild(i)).GetComponent<LayoutElement>();
			if (!((Object)(object)component2 == (Object)null))
			{
				component2.minWidth = cardWidth;
				component2.preferredWidth = cardWidth;
				component2.flexibleWidth = 0f;
				component2.minHeight = cardHeight;
				component2.preferredHeight = cardHeight;
				component2.flexibleHeight = 0f;
			}
		}
	}

	private static void ConfigureBattlefieldRow(RectTransform row, IReadOnlyList<BattleCardState> cards, float width, float cardWidth, float cardHeight, float spacing, float verticalAnchor)
	{
		row.anchorMin = new Vector2(0.5f, verticalAnchor);
		row.anchorMax = new Vector2(0.5f, verticalAnchor);
		row.anchoredPosition = Vector2.zero;
		row.sizeDelta = new Vector2(width, cardHeight);
		HorizontalLayoutGroup component = ((Component)row).GetComponent<HorizontalLayoutGroup>();
		if ((Object)(object)component != (Object)null)
		{
			((Behaviour)component).enabled = false;
		}
		int num;
		int num2;
		if (cards != null)
		{
			num = ((cards.Count > 0) ?1 : 0);
			if (num != 0)
			{
				num2 = cards.Count((BattleCardState card) => card != null && (Object)(object)card.View != (Object)null);
				goto IL_0093;
			}
		}
		else
		{
			num = 0;
		}
		num2 = ((Transform)row).childCount;
		goto IL_0093;
		IL_0093:
		int num3 = num2;
		if (num3 == 0)
		{
			num3 = 1;
		}
		float num4 = ((num3 == 2) ?((cardWidth + spacing) * 0.82f) : (cardWidth + spacing));
		float num5 = (0f - num4) * (float)(num3 - 1) * 0.5f;
		int num6 = 0;
		if (num != 0)
		{
			for (int num7 = 0; num7 < cards.Count; num7++)
			{
				BattleCardState battleCardState = cards[num7];
				if (battleCardState != null && !((Object)(object)battleCardState.View == (Object)null))
				{
					RectTransform rectTransform = battleCardState.View.RectTransform;
					ConfigureBattlefieldChild(rectTransform, cardWidth, cardHeight);
					rectTransform.anchoredPosition = new Vector2(num5 + num4 * (float)num6, 0f);
					num6++;
				}
			}
			return;
		}
		for (int num8 = 0; num8 < ((Transform)row).childCount; num8++)
		{
			Transform child = ((Transform)row).GetChild(num8);
			RectTransform val = (RectTransform)(object)((child is RectTransform) ?child : null);
			if (!((Object)(object)val == (Object)null))
			{
				ConfigureBattlefieldChild(val, cardWidth, cardHeight);
				val.anchoredPosition = new Vector2(num5 + num4 * (float)num6, 0f);
				num6++;
			}
		}
	}

	private static void ConfigureBattlefieldChild(RectTransform child, float cardWidth, float cardHeight)
	{
		LayoutElement component = ((Component)child).GetComponent<LayoutElement>();
		if ((Object)(object)component != (Object)null)
		{
			component.ignoreLayout = true;
			component.minWidth = cardWidth;
			component.preferredWidth = cardWidth;
			component.flexibleWidth = 0f;
			component.minHeight = cardHeight;
			component.preferredHeight = cardHeight;
			component.flexibleHeight = 0f;
		}
		((Component)child).gameObject.SetActive(true);
		child.anchorMin = new Vector2(0.5f, 0.5f);
		child.anchorMax = new Vector2(0.5f, 0.5f);
		child.pivot = new Vector2(0.5f, 0.5f);
		child.sizeDelta = new Vector2(cardWidth, cardHeight);
	}

	private void LoadBattle()
	{
		cardDatabase = Resources.Load<CardDatabase>("CardDatabase");
		if ((Object)(object)cardDatabase == (Object)null)
		{
			SetMessage("Database carte non trovato. Usa Accard N' Die > Rebuild Card Database.");
			return;
		}
		formationDraftService = new FormationDraftService(random);
		BeginInitialDeckBuilding();
	}
}
}
