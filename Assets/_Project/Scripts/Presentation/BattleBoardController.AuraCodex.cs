using System.Text;
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

		Text title = CreateText("Aura Codex Title", ((Component)overlay).transform, font, 34, (FontStyle)1, (TextAnchor)4);
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

		Text body = CreateText("Aura Codex Body", contentObject.transform, font, 19, (FontStyle)0, (TextAnchor)0);
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
			optionsPanel.SetActive(false);
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

	private static string BuildAuraCodexText()
	{
		StringBuilder builder = new StringBuilder();
		builder.AppendLine("<b>COME SI ATTIVANO</b>");
		builder.AppendLine("Schieri sempre 3 carte. In base alla loro composizione si attiva UNA sola aura, con questa priorita':");
		builder.AppendLine("• 3 carte della stessa CLASSE  ->  Aura di Classe");
		builder.AppendLine("• 3 carte della stessa FAMIGLIA (classi diverse)  ->  Aura di Famiglia");
		builder.AppendLine("• 1 Might + 1 Cunning + 1 Magic  ->  Aura di Formazione");
		builder.AppendLine("L'Aura di Classe sostituisce quella di Famiglia: non si sommano.");
		builder.AppendLine();
		builder.AppendLine("Famiglie:  Might (Warrior, Barbarian, Paladin)  ·  Cunning (Rogue, Assassin, Hunter)  ·  Magic (Mage, Necromancer, Priest)");
		builder.AppendLine();
		builder.AppendLine("<b>AURA DI FORMAZIONE</b>");
		AppendAuraEntry(builder, "Formazione bilanciata", BattleAuraType.Formation);
		builder.AppendLine();
		builder.AppendLine("<b>AURE DI FAMIGLIA</b>");
		AppendAuraEntry(builder, "Might", BattleAuraType.Might);
		AppendAuraEntry(builder, "Cunning", BattleAuraType.Cunning);
		AppendAuraEntry(builder, "Magic", BattleAuraType.Magic);
		builder.AppendLine();
		builder.AppendLine("<b>AURE DI CLASSE</b>");
		AppendAuraEntry(builder, "Warrior", BattleAuraType.Warrior);
		AppendAuraEntry(builder, "Barbarian", BattleAuraType.Barbarian);
		AppendAuraEntry(builder, "Paladin", BattleAuraType.Paladin);
		AppendAuraEntry(builder, "Rogue", BattleAuraType.Rogue);
		AppendAuraEntry(builder, "Assassin", BattleAuraType.Assassin);
		AppendAuraEntry(builder, "Hunter", BattleAuraType.Hunter);
		AppendAuraEntry(builder, "Mage", BattleAuraType.Mage);
		AppendAuraEntry(builder, "Necromancer", BattleAuraType.Necromancer);
		AppendAuraEntry(builder, "Priest", BattleAuraType.Priest);
		return builder.ToString();
	}

	private static void AppendAuraEntry(StringBuilder builder, string label, BattleAuraType aura)
	{
		builder.AppendLine("<b>" + label + ":</b> " + AuraEffectText(aura));
	}

	// Descrizione concisa dell'effetto di ogni aura (fonte: Docs/card-rules-and-auras.md).
	private static string AuraEffectText(BattleAuraType aura)
	{
		return aura switch
		{
			BattleAuraType.Formation =>
				"una volta per combattimento, quando una tua carta avrebbe svantaggio di famiglia, lo svantaggio diventa neutro.",
			BattleAuraType.Might =>
				"la prima volta per round che una tua carta Might non elimina il bersaglio, ottiene +1 permanente (attacco e difesa) per il resto del combattimento.",
			BattleAuraType.Cunning =>
				"una volta per round, quando una tua carta Cunning attacca un nemico con malus, marca o inibizione, tira con vantaggio anche se il matchup non lo darebbe.",
			BattleAuraType.Magic =>
				"le tue carte Magic manipolano il campo indebolendo progressivamente il nemico vivo piu' forte (-1 permanente).",
			BattleAuraType.Warrior =>
				"quando un Warrior usa la somma dei dadi, aggiunge +1 al totale.",
			BattleAuraType.Barbarian =>
				"la Furia diventa +3 invece di +2, in attacco e difesa.",
			BattleAuraType.Paladin =>
				"quando un Paladin protegge un alleato e sopravvive, contrattacca subito l'attaccante con +1 in attacco.",
			BattleAuraType.Rogue =>
				"i Rogue rilanciano gli 1 in attacco anche quando tirano due dadi (vantaggio o svantaggio).",
			BattleAuraType.Assassin =>
				"quando un Assassin inibisce un nemico, quel nemico subisce anche -1 permanente.",
			BattleAuraType.Hunter =>
				"la marca da' +3; se ad attaccare il bersaglio marcato e' un Hunter, il bonus diventa +5.",
			BattleAuraType.Mage =>
				"la prima abilita' Mage abbassa il dado avversario di 2 step invece di 1.",
			BattleAuraType.Necromancer =>
				"la prima volta per combattimento che un tuo alleato viene eliminato, resta come Spirito con un ultimo turno.",
			BattleAuraType.Priest =>
				"la Benedizione da' +3 invece di +2.",
			_ => "nessun effetto.",
		};
	}
}
}
