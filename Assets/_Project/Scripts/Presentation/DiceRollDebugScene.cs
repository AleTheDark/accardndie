using System.Collections;
using System.Collections.Generic;
using AccardND.Battlefield;
using AccardND.GameCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
    public sealed class DiceRollDebugScene : MonoBehaviour
    {
        private readonly int[] diceSides = { 4, 6, 8, 10, 12, 20 };
        private readonly HeroClass[] classes =
        {
            HeroClass.Assassin,
            HeroClass.Mage,
            HeroClass.Warrior,
            HeroClass.Rogue,
            HeroClass.Hunter,
            HeroClass.Priest,
            HeroClass.Paladin,
            HeroClass.Necromancer,
            HeroClass.Barbarian
        };

        private RectTransform board;
        private RectTransform dieSlot;
        private RectTransform allDiceRoot;
        private Dice3DRollView dieView;
        private readonly List<RectTransform> allDieSlots = new List<RectTransform>();
        private readonly List<Dice3DRollView> allDieViews = new List<Dice3DRollView>();
        private readonly List<Text> allDieLabels = new List<Text>();
        private Text dieText;
        private Text classText;
        private Text resultText;
        private Text supportText;
        private InputField resultInput;
        private int selectedDieIndex = 1;
        private int selectedClassIndex;

        private void Awake()
        {
            RemoveBattleBoardIfPresent();
            EnsureCamera();
            EnsureEventSystem();
            BuildUi();
        }

        private void Start()
        {
            StartCoroutine(RemoveBattleBoardForStartupFrames());
            RefreshLabels();
            RollSelected();
        }

        private void BuildUi()
        {
            Canvas canvas = new GameObject("Dice Roll Debug Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Image background = CreateImage("Background", canvas.transform, new Color(0.014f, 0.017f, 0.024f, 1f));
            Stretch(background.rectTransform);

            Text title = CreateText("Title", canvas.transform, font, 44, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.text = "DICE ROLL TEST";
            title.color = new Color(0.93f, 0.88f, 0.72f);
            SetRect(title.rectTransform, new Vector2(0.18f, 0.87f), new Vector2(0.82f, 0.96f));

            board = new GameObject("Invisible Dice Board", typeof(RectTransform)).GetComponent<RectTransform>();
            board.SetParent(canvas.transform, false);
            SetRect(board, new Vector2(0.14f, 0.24f), new Vector2(0.86f, 0.82f));

            Image boardHint = CreateImage("Board Hint", canvas.transform, new Color(0.06f, 0.065f, 0.075f, 0.42f));
            SetRect(boardHint.rectTransform, new Vector2(0.14f, 0.24f), new Vector2(0.86f, 0.82f));

            dieSlot = new GameObject("Die Slot", typeof(RectTransform)).GetComponent<RectTransform>();
            dieSlot.SetParent(canvas.transform, false);
            dieSlot.anchorMin = new Vector2(0.5f, 0.5f);
            dieSlot.anchorMax = new Vector2(0.5f, 0.5f);
            dieSlot.pivot = new Vector2(0.5f, 0.5f);
            dieSlot.anchoredPosition = Vector2.zero;
            dieSlot.sizeDelta = new Vector2(290f, 290f);

            dieView = Dice3DRollView.Create(dieSlot);
            dieView.SetBounceArea(board, null);

            dieText = CreateText("Die Label", canvas.transform, font, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            dieText.color = Color.white;
            SetRect(dieText.rectTransform, new Vector2(0.18f, 0.12f), new Vector2(0.32f, 0.18f));

            classText = CreateText("Class Label", canvas.transform, font, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            classText.color = Color.white;
            SetRect(classText.rectTransform, new Vector2(0.36f, 0.12f), new Vector2(0.64f, 0.18f));

            resultText = CreateText("Result Label", canvas.transform, font, 36, FontStyle.Bold, TextAnchor.MiddleCenter);
            resultText.color = new Color(1f, 0.82f, 0.28f);
            SetRect(resultText.rectTransform, new Vector2(0.68f, 0.12f), new Vector2(0.82f, 0.18f));

            supportText = CreateText("Support Label", canvas.transform, font, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            supportText.color = new Color(0.78f, 0.82f, 0.88f);
            SetRect(supportText.rectTransform, new Vector2(0.2f, 0.79f), new Vector2(0.8f, 0.84f));
            supportText.text = "Choose die, class and forced result, then roll.";

            Button prevDie = CreateButton("Previous Die", canvas.transform, font, "< D");
            SetRect(prevDie.GetComponent<RectTransform>(), new Vector2(0.18f, 0.045f), new Vector2(0.27f, 0.105f));
            prevDie.onClick.AddListener(() => ChangeDie(-1));

            Button nextDie = CreateButton("Next Die", canvas.transform, font, "D >");
            SetRect(nextDie.GetComponent<RectTransform>(), new Vector2(0.28f, 0.045f), new Vector2(0.37f, 0.105f));
            nextDie.onClick.AddListener(() => ChangeDie(1));

            Button prevClass = CreateButton("Previous Class", canvas.transform, font, "< CLASS");
            SetRect(prevClass.GetComponent<RectTransform>(), new Vector2(0.4f, 0.045f), new Vector2(0.5f, 0.105f));
            prevClass.onClick.AddListener(() => ChangeClass(-1));

            Button nextClass = CreateButton("Next Class", canvas.transform, font, "CLASS >");
            SetRect(nextClass.GetComponent<RectTransform>(), new Vector2(0.51f, 0.045f), new Vector2(0.61f, 0.105f));
            nextClass.onClick.AddListener(() => ChangeClass(1));

            Button rollButton = CreateButton("Roll", canvas.transform, font, "ROLL");
            SetRect(rollButton.GetComponent<RectTransform>(), new Vector2(0.64f, 0.04f), new Vector2(0.74f, 0.11f));
            rollButton.onClick.AddListener(RollSelected);

            Button rollAllButton = CreateButton("Roll All Results", canvas.transform, font, "ROLL ALL");
            SetRect(rollAllButton.GetComponent<RectTransform>(), new Vector2(0.75f, 0.04f), new Vector2(0.85f, 0.11f));
            rollAllButton.onClick.AddListener(RollAllResults);

            resultInput = CreateInputField("Forced Result", canvas.transform, font);
            SetRect(resultInput.GetComponent<RectTransform>(), new Vector2(0.87f, 0.04f), new Vector2(0.94f, 0.11f));
        }

        private void ChangeDie(int direction)
        {
            selectedDieIndex = (selectedDieIndex + direction + diceSides.Length) % diceSides.Length;
            RefreshLabels();
            ClampResultInputToSelectedDie();
        }

        private void ChangeClass(int direction)
        {
            selectedClassIndex = (selectedClassIndex + direction + classes.Length) % classes.Length;
            RefreshLabels();
        }

        private void RollSelected()
        {
            int sides = diceSides[Mathf.Clamp(selectedDieIndex, 0, diceSides.Length - 1)];
            HeroClass heroClass = classes[Mathf.Clamp(selectedClassIndex, 0, classes.Length - 1)];
            int result = ResolveRequestedResult(sides);
            resultText.text = result.ToString();
            HideAllDiceViews();
            dieSlot.gameObject.SetActive(true);
            dieSlot.anchoredPosition = Vector2.zero;
            dieView.SetBounceArea(board, null);
            dieView.StartScriptedRoll(sides, heroClass, result, 1.35f);
        }

        private void RollAllResults()
        {
            int sides = diceSides[Mathf.Clamp(selectedDieIndex, 0, diceSides.Length - 1)];
            HeroClass heroClass = classes[Mathf.Clamp(selectedClassIndex, 0, classes.Length - 1)];
            resultText.text = $"1-{sides}";
            dieView.Hide();
            dieSlot.gameObject.SetActive(false);

            EnsureAllDiceViews(sides);
            LayoutAllDiceSlots(sides);
            for (int result = 1; result <= sides; result++)
            {
                int index = result - 1;
                allDieSlots[index].gameObject.SetActive(true);
                allDieLabels[index].text = result.ToString();
                allDieViews[index].SetBounceArea(allDieSlots[index], null);
                allDieViews[index].StartScriptedRoll(sides, heroClass, result, 1.35f);
            }

            for (int index = sides; index < allDieSlots.Count; index++)
            {
                allDieViews[index].Hide();
                allDieSlots[index].gameObject.SetActive(false);
            }
        }

        private void EnsureAllDiceViews(int count)
        {
            if (allDiceRoot == null)
            {
                allDiceRoot = new GameObject("All Dice Results Root", typeof(RectTransform)).GetComponent<RectTransform>();
                allDiceRoot.SetParent(board.parent, false);
                SetRect(allDiceRoot, new Vector2(0.14f, 0.24f), new Vector2(0.86f, 0.82f));
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            while (allDieViews.Count < count)
            {
                int result = allDieViews.Count + 1;
                RectTransform slot = new GameObject($"Result {result} Slot", typeof(RectTransform)).GetComponent<RectTransform>();
                slot.SetParent(allDiceRoot, false);
                Dice3DRollView view = Dice3DRollView.Create(slot);
                Text label = CreateText("Result Label", slot, font, 18, FontStyle.Bold, TextAnchor.LowerCenter);
                label.color = new Color(1f, 0.86f, 0.36f);
                Stretch(label.rectTransform, 2f);

                allDieSlots.Add(slot);
                allDieViews.Add(view);
                allDieLabels.Add(label);
            }
        }

        private void LayoutAllDiceSlots(int count)
        {
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            float gap = 0.014f;
            float cellWidth = (1f - gap * (columns + 1)) / columns;
            float cellHeight = (1f - gap * (rows + 1)) / rows;

            for (int index = 0; index < count; index++)
            {
                int row = index / columns;
                int column = index % columns;
                float minX = gap + column * (cellWidth + gap);
                float maxX = minX + cellWidth;
                float maxY = 1f - gap - row * (cellHeight + gap);
                float minY = maxY - cellHeight;
                SetRect(allDieSlots[index], new Vector2(minX, minY), new Vector2(maxX, maxY));
            }
        }

        private void HideAllDiceViews()
        {
            for (int index = 0; index < allDieViews.Count; index++)
            {
                allDieViews[index].Hide();
                allDieSlots[index].gameObject.SetActive(false);
            }
        }

        private void RefreshLabels()
        {
            int sides = diceSides[Mathf.Clamp(selectedDieIndex, 0, diceSides.Length - 1)];
            HeroClass heroClass = classes[Mathf.Clamp(selectedClassIndex, 0, classes.Length - 1)];
            dieText.text = $"D{sides}";
            classText.text = heroClass.ToString().ToUpperInvariant();
            if (resultInput != null && string.IsNullOrWhiteSpace(resultInput.text))
                resultInput.text = "1";
        }

        private int ResolveRequestedResult(int sides)
        {
            if (resultInput == null || !int.TryParse(resultInput.text, out int requested))
            {
                requested = Random.Range(1, sides + 1);
            }

            int result = Mathf.Clamp(requested, 1, sides);
            if (resultInput != null)
                resultInput.text = result.ToString();
            return result;
        }

        private void ClampResultInputToSelectedDie()
        {
            if (resultInput == null)
                return;

            int sides = diceSides[Mathf.Clamp(selectedDieIndex, 0, diceSides.Length - 1)];
            if (!int.TryParse(resultInput.text, out int requested))
                requested = 1;
            resultInput.text = Mathf.Clamp(requested, 1, sides).ToString();
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label)
        {
            Image image = CreateImage(name, parent, new Color(0.12f, 0.12f, 0.14f, 0.96f));
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.9f, 0.55f);
            colors.pressedColor = new Color(0.78f, 0.68f, 0.4f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            Text text = CreateText("Label", image.transform, font, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label;
            text.color = Color.white;
            Stretch(text.rectTransform, 4f);
            return button;
        }

        private static InputField CreateInputField(string name, Transform parent, Font font)
        {
            Image image = CreateImage(name, parent, new Color(0.08f, 0.08f, 0.095f, 0.98f));
            image.raycastTarget = true;
            InputField input = image.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.text = "1";

            Text text = CreateText("Text", image.transform, font, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.color = Color.white;
            Stretch(text.rectTransform, 4f);
            input.textComponent = text;

            Text placeholder = CreateText("Placeholder", image.transform, font, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
            placeholder.text = "RESULT";
            placeholder.color = new Color(1f, 1f, 1f, 0.36f);
            Stretch(placeholder.rectTransform, 4f);
            input.placeholder = placeholder;

            return input;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, FontStyle style, TextAnchor alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            global::AccardND.Battlefield.EditableRuntimeText.Bind(text);
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            SetRect(rect, Vector2.zero, Vector2.one);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
                return;

            Camera camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private static void RemoveBattleBoardIfPresent()
        {
            foreach (BattleBoardController controller in Object.FindObjectsByType<BattleBoardController>(FindObjectsSortMode.None))
            {
                if ((Object)controller != null)
                    Destroy(controller.gameObject);
            }

            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if ((Object)canvas != null && canvas.name == "Battle Canvas")
                    Destroy(canvas.gameObject);
            }
        }

        private IEnumerator RemoveBattleBoardForStartupFrames()
        {
            for (int frame = 0; frame < 30; frame++)
            {
                RemoveBattleBoardIfPresent();
                yield return null;
            }
        }

    }
}
