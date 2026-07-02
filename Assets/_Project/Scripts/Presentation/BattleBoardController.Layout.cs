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
			safeAreaRoot.anchorMin = new Vector2(safeArea.xMin / num, safeArea.yMin / num2);
			safeAreaRoot.anchorMax = new Vector2(safeArea.xMax / num, safeArea.yMax / num2);
			safeAreaRoot.offsetMin = Vector2.zero;
			safeAreaRoot.offsetMax = Vector2.zero;
			float num3 = Mathf.Max(1f, safeArea.width);
			float num4 = Mathf.Max(1f, safeArea.height);
			float num5 = num3 / num4;
			RefreshModeSelectionLayout();
			RefreshRoomChoiceLayout();
			RefreshCardInspectionLayout();
			RefreshDeckBuilderLayout();
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
			float num10 = height * (flag ?responsiveLayout.CompactCardHeight : (flag2 ?0.34f : responsiveLayout.LandscapeCardHeight));
			float num11 = Mathf.Min(num9, num10 * 1f);
			float width2 = num11 * (float)num8 + num7 * (float)(num8 - 1);
			bool flag3 = (currentRoomType == RoomType.Monster || currentRoomType == RoomType.Boss) && (draftActive || deploymentDraftActive || playerCards.Count > 0 || cpuCards.Count > 0);
			bool flag4 = IsMerchantActionHudVisible() || IsSingleActionNonCombatHudVisible();
			float anchor = ((!flag3) ?(flag ?0.715f : 0.67f) : (flag ?0.675f : 0.63f));
			anchor = ClampBattlefieldAnchor(anchor, num11, height, flag ?0.055f : 0.11f, (!flag3) ?(flag ?0.835f : 0.795f) : (flag ?0.775f : 0.72f));
			ConfigureBattlefieldRow(cpuRow, cpuCards, width2, num11, num11, num7, anchor);
			float anchor2 = ((!deploymentDraftActive) ?(flag ?0.205f : 0.17f) : (flag ?0.35f : 0.29f));
			anchor2 = ClampBattlefieldAnchor(anchor2, num11, height, flag ?0.06f : 0.105f, Mathf.Max(0.12f, anchor - num11 / Mathf.Max(1f, height) - 0.035f));
			ConfigureBattlefieldRow(playerRow, playerCards, width2, num11, num11, num7, anchor2);
			int num12 = (((Object)(object)playerHandRow != (Object)null) ?((Transform)playerHandRow).childCount : 0);
			if (num12 > 0)
			{
				float handOverlap = responsiveLayout.HandOverlap;
				float num13 = (float)num12 - (float)(num12 - 1) * handOverlap;
				float num14 = num6 * (flag2 ?0.96f : 0.9f) / Mathf.Max(1f, num13);
				float num15 = height * (flag ?responsiveLayout.CompactHandHeight : (flag2 ?0.4f : responsiveLayout.LandscapeHandHeight));
				float num16 = Mathf.Min(num14, num15 * 0.6708861f);
				float cardHeight = num16 / 0.6708861f;
				float width3 = num16 * num13;
				ConfigureRow(playerHandRow, width3, num16, cardHeight, (0f - num16) * handOverlap, flag ?0.105f : (flag2 ?0.095f : 0.08f));
			}
			if (flag)
			{
				SetRect(tableGlowRect, new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f));
				SetRect(topInfoBarRect, new Vector2(0.035f, 0.952f), new Vector2(0.82f, 0.992f));
				SetRect(playerHudRect, new Vector2(0.04f, 0.055f), new Vector2(0.54f, 0.19f));
				SetRect((RectTransform)((Component)logButton).transform, new Vector2(0.84f, 0.952f), new Vector2(0.96f, 0.992f));
				if ((Object)(object)optionsPanel != (Object)null)
				{
					SetRect((RectTransform)optionsPanel.transform, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.9f));
				}
				SetRect(implementationArchiveButtonRect, new Vector2(0.8f, 0.005f), new Vector2(0.96f, 0.115f));
				SetRect(implementationArchivePanelRect, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.94f));
				SetRect(messagePanelRect, flag4 ?new Vector2(0.06f, 0.045f) : (flag3 ?new Vector2(0.08f, 0.43f) : (deploymentDraftActive ?new Vector2(0.04f, 0.49f) : new Vector2(0.04f, 0.065f))), flag4 ?new Vector2(0.94f, 0.235f) : (flag3 ?new Vector2(0.92f, 0.535f) : (deploymentDraftActive ?new Vector2(0.96f, 0.575f) : new Vector2(0.96f, 0.17f))));
				SetRect(timelineBackgroundRect, flag3 ?new Vector2(0.04f, 0.827f) : new Vector2(0.04f, 0.895f), flag3 ?new Vector2(0.96f, 0.875f) : new Vector2(0.96f, 0.942f));
				SetRect(cpuTitleRect, flag3 ?new Vector2(0.05f, 0.78f) : new Vector2(0.05f, 0.848f), flag3 ?new Vector2(0.95f, 0.822f) : new Vector2(0.95f, 0.89f));
				SetRect(roundText.rectTransform, new Vector2(0.05f, 0.545f), new Vector2(0.62f, 0.59f));
				SetRect(campaignZoneRect, new Vector2(0.64f, 0.545f), new Vector2(0.95f, 0.59f));
				SetRect(playerTitleRect, new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.395f));
			}
			else
			{
				SetRect(tableGlowRect, flag2 ?new Vector2(0.04f, 0.105f) : new Vector2(0.08f, 0.13f), flag2 ?new Vector2(0.96f, 0.895f) : new Vector2(0.92f, 0.87f));
				SetRect(topInfoBarRect, flag2 ?new Vector2(0.05f, 0.925f) : new Vector2(0.08f, 0.93f), new Vector2(0.84f, 0.985f));
				SetRect(playerHudRect, flag2 ?new Vector2(0.025f, 0.035f) : new Vector2(0.045f, 0.045f), flag2 ?new Vector2(0.265f, 0.21f) : new Vector2(0.32f, 0.22f));
				SetRect((RectTransform)((Component)logButton).transform, new Vector2(0.87f, 0.93f), new Vector2(0.98f, 0.985f));
				if ((Object)(object)optionsPanel != (Object)null)
				{
					SetRect((RectTransform)optionsPanel.transform, new Vector2(0.64f, 0.52f), new Vector2(0.98f, 0.92f));
				}
				SetRect(implementationArchiveButtonRect, new Vector2(0.9f, 0.015f), new Vector2(0.985f, 0.17f));
				SetRect(implementationArchivePanelRect, new Vector2(0.62f, 0.05f), new Vector2(0.98f, 0.94f));
				SetRect(messagePanelRect, flag4 ?new Vector2(0.28f, 0.36f) : (flag3 ?new Vector2(0.22f, 0.41f) : new Vector2(0.25f, 0.41f)), flag4 ?new Vector2(0.72f, 0.575f) : (flag3 ?new Vector2(0.78f, 0.555f) : new Vector2(0.75f, 0.555f)));
				SetRect(timelineBackgroundRect, flag3 ?new Vector2(0.18f, 0.775f) : new Vector2(0.18f, 0.865f), flag3 ?new Vector2(0.82f, 0.825f) : new Vector2(0.82f, 0.91f));
				SetRect(cpuTitleRect, flag3 ?new Vector2(0.12f, 0.725f) : new Vector2(0.12f, 0.805f), flag3 ?new Vector2(0.88f, 0.765f) : new Vector2(0.88f, 0.85f));
				SetRect(roundText.rectTransform, new Vector2(0.17f, 0.575f), new Vector2(0.55f, 0.625f));
				SetRect(campaignZoneRect, new Vector2(0.57f, 0.575f), new Vector2(0.83f, 0.625f));
				SetRect(playerTitleRect, new Vector2(0.12f, 0.32f), new Vector2(0.88f, 0.38f));
			}
			Canvas.ForceUpdateCanvases();
			ResizeTimelineTiles();
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
				if (!((Object)(object)component != (Object)null) || (!component.IsDragging && !draftEntranceAnimatingViews.Contains(component)))
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
