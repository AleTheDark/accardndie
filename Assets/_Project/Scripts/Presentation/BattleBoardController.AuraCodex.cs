using System.Text;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private GameObject auraCodexPanel;

	private void CreateAuraCodexView(Transform canvasTransform, Font font)
	{
		Image overlay = CreateImage("Aura Codex", canvasTransform, new Color(0.01f, 0.018f, 0.028f, 0.98f));
		overlay.raycastTarget = true;
		Stretch(overlay.rectTransform);
		auraCodexPanel = ((Component)overlay).gameObject;
		Canvas canvas = auraCodexPanel.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 950;
		auraCodexPanel.AddComponent<GraphicRaycaster>();

		Text title = CreateText("Aura Codex Title", ((Component)overlay).transform, font, 40, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(title);
		title.text = "CODICE DELLE AURE";
		title.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(title.rectTransform, new Vector2(0.08f, 0.9f), new Vector2(0.82f, 0.97f));

		Button closeButton = CreateImageButton("Close Aura Codex", ((Component)overlay).transform, font, cancelActionSprite, string.Empty);
		((UnityEvent)closeButton.onClick).AddListener(new UnityAction(CloseAuraCodex));
		SetRect((RectTransform)((Component)closeButton).transform, new Vector2(0.85f, 0.9f), new Vector2(0.95f, 0.965f));

		// Area scrollabile: ScrollRect -> Viewport (mask) -> Content (layout) -> Text.
		GameObject scrollObject = new GameObject("Aura Codex Scroll", new System.Type[2]
		{
			typeof(RectTransform),
			typeof(ScrollRect)
		});
		((Transform)scrollObject.GetComponent<RectTransform>()).SetParent(((Component)overlay).transform, false);
		ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
		SetRect(scrollObject.GetComponent<RectTransform>(), new Vector2(0.07f, 0.06f), new Vector2(0.93f, 0.88f));
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.scrollSensitivity = 34f;

		Image viewportImage = CreateImage("Viewport", scrollObject.transform, new Color(0f, 0f, 0f, 0.001f));
		viewportImage.raycastTarget = true;
		Stretch(viewportImage.rectTransform);
		((Component)viewportImage).gameObject.AddComponent<RectMask2D>();
		scrollRect.viewport = viewportImage.rectTransform;

		GameObject contentObject = new GameObject("Content", new System.Type[3]
		{
			typeof(RectTransform),
			typeof(VerticalLayoutGroup),
			typeof(ContentSizeFitter)
		});
		RectTransform contentRect = contentObject.GetComponent<RectTransform>();
		((Transform)contentRect).SetParent(((Component)viewportImage).transform, false);
		contentRect.anchorMin = new Vector2(0f, 1f);
		contentRect.anchorMax = new Vector2(1f, 1f);
		contentRect.pivot = new Vector2(0.5f, 1f);
		contentRect.offsetMin = Vector2.zero;
		contentRect.offsetMax = Vector2.zero;
		VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(24, 24, 20, 20);
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		layout.childAlignment = (TextAnchor)0;
		ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		Text body = CreateText("Aura Codex Body", contentObject.transform, font, 24, (FontStyle)0, (TextAnchor)0);
		body.color = new Color(0.88f, 0.92f, 0.95f);
		body.horizontalOverflow = HorizontalWrapMode.Wrap;
		body.verticalOverflow = VerticalWrapMode.Overflow;
		body.resizeTextForBestFit = false;
		body.raycastTarget = false;
		body.supportRichText = true;
		body.text = BuildAuraCodexText();
		scrollRect.content = contentRect;

		auraCodexPanel.SetActive(false);
	}

	private void OpenAuraCodexFromOptions()
	{
		if ((Object)(object)optionsPanel != (Object)null)
		{
			CloseOptionsPanel();
		}
		OpenAuraCodex();
	}

	private void OpenAuraCodex()
	{
		if ((Object)(object)auraCodexPanel != (Object)null)
		{
			auraCodexPanel.SetActive(true);
			auraCodexPanel.transform.SetAsLastSibling();
		}
	}

	private void CloseAuraCodex()
	{
		if ((Object)(object)auraCodexPanel != (Object)null && auraCodexPanel.activeSelf)
		{
			auraCodexPanel.SetActive(false);
		}
	}

	private string BuildAuraCodexText()
	{
		StringBuilder builder = new StringBuilder();
		builder.AppendLine("<b>COME SI ATTIVANO</b>");
		builder.AppendLine("Schieri sempre 3 carte. In base alla loro composizione si attiva UNA sola aura, con questa priorita':");
		builder.AppendLine("• 3 carte della stessa CLASSE  ->  Aura di Classe");
		builder.AppendLine("• 3 carte della stessa FAMIGLIA (classi diverse)  ->  Aura di Famiglia");
		builder.AppendLine("• 1 Fortuza + 1 Astuta + 1 Magica  ->  Aura di Formazione");
		builder.AppendLine("L'Aura di Classe sostituisce quella di Famiglia: non si sommano.");
		builder.AppendLine();
		builder.AppendLine("Famiglie:  Fortuza (Warrior, Barbarian, Paladin)  ·  Astuta (Rogue, Assassin, Hunter)  ·  Magica (Mage, Necromancer, Priest)");
		builder.AppendLine();
		builder.AppendLine("<b>AURA DI FORMAZIONE</b>");
		AppendAuraEntry(builder, "Formazione bilanciata", BattleAuraType.Formation);
		builder.AppendLine();
		builder.AppendLine("<b>AURE DI FAMIGLIA</b>");
		AppendAuraEntry(builder, "Fortuza", BattleAuraType.Might);
		AppendAuraEntry(builder, "Astuta", BattleAuraType.Cunning);
		AppendAuraEntry(builder, "Magica", BattleAuraType.Magic);
		builder.AppendLine();
		builder.AppendLine("<b>AURE DI CLASSE</b>");
		AppendAuraEntry(builder, "Warrior", BattleAuraType.Warrior);
		AppendAuraEntry(builder, "Barbarian", BattleAuraType.Barbarian);
		AppendAuraEntry(builder, "Paladin", BattleAuraType.Paladin);
		AppendAuraEntry(builder, "Ladri", BattleAuraType.Rogue);
		AppendAuraEntry(builder, "Assassin", BattleAuraType.Assassin);
		AppendAuraEntry(builder, "Hunter", BattleAuraType.Hunter);
		AppendAuraEntry(builder, "Mage", BattleAuraType.Mage);
		AppendAuraEntry(builder, "Necromancer", BattleAuraType.Necromancer);
		AppendAuraEntry(builder, "Priest", BattleAuraType.Priest);
		return builder.ToString();
	}

	private void AppendAuraEntry(StringBuilder builder, string label, BattleAuraType aura)
	{
		builder.AppendLine("<b>" + label + ":</b> " + AuraEffectText(aura, configuration?.ClassBalance));
	}

	// Descrizione concisa dell'effetto di ogni aura (fonte: Docs/card-rules-and-auras.md).
	private static string AuraEffectText(BattleAuraType aura, ClassBalanceConfiguration balance = null)
	{
		return aura switch
		{
			BattleAuraType.Formation => CardRulesGlossary.FormationAuraDescription(),
			BattleAuraType.Might => CardRulesGlossary.FamilyAuraDescription(ClassFamily.Might),
			BattleAuraType.Cunning => CardRulesGlossary.FamilyAuraDescription(ClassFamily.Cunning),
			BattleAuraType.Magic => CardRulesGlossary.FamilyAuraDescription(ClassFamily.Magic),
			BattleAuraType.Warrior => CardRulesGlossary.ClassAuraDescription(HeroClass.Warrior, balance),
			BattleAuraType.Barbarian => CardRulesGlossary.ClassAuraDescription(HeroClass.Barbarian, balance),
			BattleAuraType.Paladin => CardRulesGlossary.ClassAuraDescription(HeroClass.Paladin, balance),
			BattleAuraType.Rogue => CardRulesGlossary.ClassAuraDescription(HeroClass.Rogue, balance),
			BattleAuraType.Assassin => CardRulesGlossary.ClassAuraDescription(HeroClass.Assassin, balance),
			BattleAuraType.Hunter => CardRulesGlossary.ClassAuraDescription(HeroClass.Hunter, balance),
			BattleAuraType.Mage => CardRulesGlossary.ClassAuraDescription(HeroClass.Mage, balance),
			BattleAuraType.Necromancer => CardRulesGlossary.ClassAuraDescription(HeroClass.Necromancer, balance),
			BattleAuraType.Priest => CardRulesGlossary.ClassAuraDescription(HeroClass.Priest, balance),
			_ => "nessun effetto.",
		};
	}
}
}
