using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
    public sealed class MageVigorConstellationDebugScene : MonoBehaviour
    {
        private readonly (int from, int to)[] transitions =
        {
            (20, 12), (12, 10), (10, 8), (8, 6), (6, 4), (4, 3)
        };

        private RectTransform effectRoot;
        private Text transitionText;
        private Text titleText;
        private Coroutine activeEffect;
        private int selectedTransitionIndex;

        private sealed class Star
        {
            public RectTransform Rect;
            public Image Image;
            public Vector3 From;
            public Vector3 Scatter;
            public Vector3 To;
            public bool HasStart;
            public bool HasEnd;
        }

        private sealed class Line
        {
            public RectTransform Rect;
            public Image Image;
        }

        private sealed class Geometry
        {
            public readonly Vector3[] Vertices;
            public readonly int[] Edges;

            public Geometry(Vector3[] vertices, int[] edges)
            {
                Vertices = vertices;
                Edges = edges;
            }
        }

        private void Awake()
        {
            RemoveBattleBoardIfPresent();
            EnsureCamera();
            EnsureEventSystem();
            BuildUi();
        }

        private void Start()
        {
            PlaySelected();
        }

        private void BuildUi()
        {
            Canvas canvas = new GameObject("Mage Vigor Debug Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage("Background", canvas.transform, new Color(0.012f, 0.01f, 0.022f, 1f));
            Stretch(background.rectTransform);

            effectRoot = new GameObject("Effect Root", typeof(RectTransform)).GetComponent<RectTransform>();
            effectRoot.SetParent(canvas.transform, false);
            Stretch(effectRoot);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText = CreateText("Title", canvas.transform, font, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            titleText.text = "MAGE VIGOR CONSTELLATION TEST";
            titleText.color = new Color(0.95f, 0.82f, 1f, 1f);
            SetRect(titleText.rectTransform, new Vector2(0.18f, 0.84f), new Vector2(0.82f, 0.93f));

            Button previousButton = CreateButton("Previous Transition", canvas.transform, font, "<");
            SetRect(previousButton.GetComponent<RectTransform>(), new Vector2(0.29f, 0.08f), new Vector2(0.35f, 0.16f));
            previousButton.onClick.AddListener(() => ChangeTransition(-1));

            Image selectorPanel = CreateImage("Transition Selector", canvas.transform, new Color(0.08f, 0.045f, 0.12f, 0.96f));
            SetRect(selectorPanel.rectTransform, new Vector2(0.36f, 0.08f), new Vector2(0.51f, 0.16f));
            transitionText = CreateText("Transition Label", selectorPanel.transform, font, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(transitionText.rectTransform, 8f);

            Button nextButton = CreateButton("Next Transition", canvas.transform, font, ">");
            SetRect(nextButton.GetComponent<RectTransform>(), new Vector2(0.52f, 0.08f), new Vector2(0.58f, 0.16f));
            nextButton.onClick.AddListener(() => ChangeTransition(1));

            Button playButton = CreateButton("Play Button", canvas.transform, font, "PLAY");
            SetRect(playButton.GetComponent<RectTransform>(), new Vector2(0.61f, 0.08f), new Vector2(0.72f, 0.16f));
            playButton.onClick.AddListener(PlaySelected);
            RefreshTransitionText();
        }

        private void PlaySelected()
        {
            if (activeEffect != null)
                StopCoroutine(activeEffect);

            for (int index = effectRoot.childCount - 1; index >= 0; index--)
                Destroy(effectRoot.GetChild(index).gameObject);

            int selected = Mathf.Clamp(selectedTransitionIndex, 0, transitions.Length - 1);
            (int from, int to) transition = transitions[selected];
            titleText.text = $"D{transition.from}  ->  D{transition.to}";
            activeEffect = StartCoroutine(PlayConstellation(transition.from, transition.to));
        }

        private void ChangeTransition(int direction)
        {
            selectedTransitionIndex = (selectedTransitionIndex + direction + transitions.Length) % transitions.Length;
            RefreshTransitionText();
        }

        private void RefreshTransitionText()
        {
            if (transitionText == null)
                return;

            (int from, int to) transition = transitions[Mathf.Clamp(selectedTransitionIndex, 0, transitions.Length - 1)];
            transitionText.text = $"D{transition.from}  ->  D{transition.to}";
        }

        private IEnumerator PlayConstellation(int startSides, int endSides)
        {
            Geometry startGeometry = CreateGeometry(startSides);
            Geometry endGeometry = CreateGeometry(endSides);
            float radius = Mathf.Min(Screen.width, Screen.height) * 0.18f;
            Color starColor = new Color(0.78f, 0.28f, 1f, 1f);
            Color lineColor = new Color(0.58f, 0.12f, 1f, 0.74f);

            GameObject rootObject = new GameObject("Debug Mage Vigor Constellation", typeof(RectTransform), typeof(CanvasGroup));
            rootObject.transform.SetParent(effectRoot, false);
            RectTransform root = (RectTransform)rootObject.transform;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, 35f);
            root.sizeDelta = new Vector2(radius * 4f, radius * 4f);
            CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text label = CreateText("Die Label", rootObject.transform, font, 52, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.text = $"D{startSides}";
            label.color = new Color(0.95f, 0.78f, 1f, 1f);
            SetRect(label.rectTransform, new Vector2(0.34f, 0.03f), new Vector2(0.66f, 0.18f));

            int starCount = Mathf.Max(startGeometry.Vertices.Length, endGeometry.Vertices.Length);
            var stars = new List<Star>(starCount);
            for (int index = 0; index < starCount; index++)
            {
                Star star = new Star();
                star.HasStart = index < startGeometry.Vertices.Length;
                star.HasEnd = index < endGeometry.Vertices.Length;
                star.From = star.HasStart ? startGeometry.Vertices[index] : Vector3.zero;
                star.To = star.HasEnd ? endGeometry.Vertices[index] : Vector3.zero;
                Vector3 scatterOrigin = star.HasStart ? star.From : star.To;
                Vector3 scatterDirection = scatterOrigin.sqrMagnitude > 0.0001f ? scatterOrigin.normalized : Vector3.up;
                star.Scatter = scatterOrigin + scatterDirection * 1.05f;
                Image image = CreateImage("Star", rootObject.transform, starColor);
                star.Image = image;
                star.Rect = image.rectTransform;
                star.Rect.anchorMin = new Vector2(0.5f, 0.5f);
                star.Rect.anchorMax = new Vector2(0.5f, 0.5f);
                star.Rect.pivot = new Vector2(0.5f, 0.5f);
                stars.Add(star);
            }

            int lineCount = Mathf.Max(startGeometry.Edges.Length, endGeometry.Edges.Length) / 2;
            var lines = new List<Line>(lineCount);
            for (int index = 0; index < lineCount; index++)
            {
                Image image = CreateImage("Line", rootObject.transform, lineColor);
                image.transform.SetAsFirstSibling();
                RectTransform rect = image.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                lines.Add(new Line { Rect = rect, Image = image });
            }

            float elapsed = 0f;
            while (elapsed < 0.22f)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / 0.22f);
                group.alpha = Mathf.SmoothStep(0f, 1f, progress);
                root.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, progress);
                UpdatePose(stars, lines, startGeometry.Edges, Rotation(progress * 0.24f), radius, lineColor, starColor, showStart: true);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.55f)
            {
                elapsed += Time.unscaledDeltaTime;
                UpdatePose(stars, lines, startGeometry.Edges, Rotation(0.24f + Mathf.Clamp01(elapsed / 0.55f) * 0.5f), radius, lineColor, starColor, showStart: true);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.42f)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.42f));
                Quaternion rotation = Rotation(0.74f + progress * 0.18f);
                foreach (Star star in stars)
                {
                    Vector3 point = Vector3.LerpUnclamped(star.HasStart ? star.From : Vector3.zero, star.Scatter, progress);
                    PlaceStar(star, point, rotation, radius, starColor, star.HasStart ? Mathf.Lerp(1f, 0.38f, progress) : 0f);
                }
                HideLines(lines);
                yield return null;
            }

            label.text = $"D{endSides}";
            elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.5f));
                Quaternion rotation = Rotation(0.92f + progress * 0.24f);
                foreach (Star star in stars)
                {
                    Vector3 point = Vector3.LerpUnclamped(star.Scatter, star.HasEnd ? star.To : Vector3.zero, progress);
                    float alpha = star.HasEnd ? Mathf.Lerp(0.38f, 1f, progress) : Mathf.Lerp(0.38f, 0f, progress);
                    PlaceStar(star, point, rotation, radius, starColor, alpha);
                }
                UpdateLines(lines, stars, endGeometry.Edges, lineColor, progress);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.85f)
            {
                elapsed += Time.unscaledDeltaTime;
                UpdatePose(stars, lines, endGeometry.Edges, Rotation(1.16f + Mathf.Clamp01(elapsed / 0.85f) * 0.55f), radius, lineColor, starColor, showStart: false);
                yield return null;
            }

            activeEffect = null;
        }

        private static Geometry CreateGeometry(int sides)
        {
            return sides switch
            {
                4 => Tetrahedron(),
                6 => Cube(),
                8 => Octahedron(),
                10 => D10Trapezohedron(),
                12 => Dodecahedron(),
                20 => Icosahedron(),
                _ => sides <= 4 ? Tetrahedron()
                    : sides <= 6 ? Cube()
                    : sides <= 8 ? Octahedron()
                    : sides <= 10 ? D10Trapezohedron()
                    : sides <= 12 ? Dodecahedron()
                    : Icosahedron()
            };
        }

        private static Geometry Tetrahedron()
        {
            float baseRadius = 1.05f;
            float baseY = -0.46f;
            return Normalize(new[]
            {
                new Vector3(0f, 1.22f, 0f),
                new Vector3(Mathf.Cos(90f * Mathf.Deg2Rad) * baseRadius, baseY, Mathf.Sin(90f * Mathf.Deg2Rad) * baseRadius),
                new Vector3(Mathf.Cos(210f * Mathf.Deg2Rad) * baseRadius, baseY, Mathf.Sin(210f * Mathf.Deg2Rad) * baseRadius),
                new Vector3(Mathf.Cos(330f * Mathf.Deg2Rad) * baseRadius, baseY, Mathf.Sin(330f * Mathf.Deg2Rad) * baseRadius)
            }, new[] { 0, 1, 0, 2, 0, 3, 1, 2, 1, 3, 2, 3 });
        }

        private static Geometry Cube()
        {
            return Normalize(new[]
            {
                new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f),
                new Vector3(1f, 1f, -1f), new Vector3(-1f, 1f, -1f),
                new Vector3(-1f, -1f, 1f), new Vector3(1f, -1f, 1f),
                new Vector3(1f, 1f, 1f), new Vector3(-1f, 1f, 1f)
            }, new[] { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 });
        }

        private static Geometry Octahedron()
        {
            return Normalize(new[]
            {
                new Vector3(0f, 1.35f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 1f), new Vector3(-1f, 0f, 0f),
                new Vector3(0f, 0f, -1f), new Vector3(0f, -1.35f, 0f)
            }, new[] { 0, 1, 0, 2, 0, 3, 0, 4, 5, 1, 5, 2, 5, 3, 5, 4, 1, 2, 2, 3, 3, 4, 4, 1 });
        }

        private static Geometry BiPyramid(int ringCount)
        {
            var vertices = new List<Vector3> { new Vector3(0f, 1.35f, 0f), new Vector3(0f, -1.35f, 0f) };
            for (int index = 0; index < ringCount; index++)
            {
                float angle = (360f / ringCount * index + 18f) * Mathf.Deg2Rad;
                vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }

            var edges = new List<int>();
            for (int index = 0; index < ringCount; index++)
            {
                int current = index + 2;
                int next = ((index + 1) % ringCount) + 2;
                edges.Add(0); edges.Add(current);
                edges.Add(1); edges.Add(current);
                edges.Add(current); edges.Add(next);
            }
            return Normalize(vertices.ToArray(), edges.ToArray());
        }

        private static Geometry D10Trapezohedron()
        {
            var vertices = new List<Vector3>
            {
                new Vector3(0f, 1.24f, 0f),
                new Vector3(0f, -1.24f, 0f)
            };
            for (int index = 0; index < 5; index++)
            {
                float upperAngle = (72f * index) * Mathf.Deg2Rad;
                float lowerAngle = (72f * index + 36f) * Mathf.Deg2Rad;
                vertices.Add(new Vector3(Mathf.Cos(upperAngle), 0.32f, Mathf.Sin(upperAngle)));
                vertices.Add(new Vector3(Mathf.Cos(lowerAngle), -0.32f, Mathf.Sin(lowerAngle)));
            }

            var edges = new List<int>();
            for (int index = 0; index < 5; index++)
            {
                int upper = 2 + index * 2;
                int lower = upper + 1;
                int previousLower = 2 + ((index + 4) % 5) * 2 + 1;
                edges.Add(0); edges.Add(upper);
                edges.Add(1); edges.Add(lower);
                edges.Add(upper); edges.Add(lower);
                edges.Add(upper); edges.Add(previousLower);
            }
            return Normalize(vertices.ToArray(), edges.ToArray());
        }

        private static Geometry Dodecahedron()
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] icosahedronVertices =
            {
                new Vector3(-1f, phi, 0f), new Vector3(1f, phi, 0f), new Vector3(-1f, -phi, 0f), new Vector3(1f, -phi, 0f),
                new Vector3(0f, -1f, phi), new Vector3(0f, 1f, phi), new Vector3(0f, -1f, -phi), new Vector3(0f, 1f, -phi),
                new Vector3(phi, 0f, -1f), new Vector3(phi, 0f, 1f), new Vector3(-phi, 0f, -1f), new Vector3(-phi, 0f, 1f)
            };
            int[,] faces =
            {
                { 0, 11, 5 }, { 0, 5, 1 }, { 0, 1, 7 }, { 0, 7, 10 }, { 0, 10, 11 },
                { 1, 5, 9 }, { 5, 11, 4 }, { 11, 10, 2 }, { 10, 7, 6 }, { 7, 1, 8 },
                { 3, 9, 4 }, { 3, 4, 2 }, { 3, 2, 6 }, { 3, 6, 8 }, { 3, 8, 9 },
                { 4, 9, 5 }, { 2, 4, 11 }, { 6, 2, 10 }, { 8, 6, 7 }, { 9, 8, 1 }
            };

            Vector3[] vertices = BuildFaceCenters(icosahedronVertices, faces);
            return Normalize(vertices, BuildFaceAdjacencyEdges(faces));
        }

        private static Geometry Icosahedron()
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] vertices =
            {
                new Vector3(-1f, phi, 0f), new Vector3(1f, phi, 0f), new Vector3(-1f, -phi, 0f), new Vector3(1f, -phi, 0f),
                new Vector3(0f, -1f, phi), new Vector3(0f, 1f, phi), new Vector3(0f, -1f, -phi), new Vector3(0f, 1f, -phi),
                new Vector3(phi, 0f, -1f), new Vector3(phi, 0f, 1f), new Vector3(-phi, 0f, -1f), new Vector3(-phi, 0f, 1f)
            };
            return Normalize(vertices, BuildNearestEdges(vertices, 30));
        }

        private static Geometry Normalize(Vector3[] vertices, int[] edges)
        {
            float maxMagnitude = 1f;
            foreach (Vector3 vertex in vertices)
                maxMagnitude = Mathf.Max(maxMagnitude, vertex.magnitude);
            for (int index = 0; index < vertices.Length; index++)
                vertices[index] /= maxMagnitude;
            return new Geometry(vertices, edges);
        }

        private static int[] BuildNearestEdges(Vector3[] vertices, int edgeLimit)
        {
            var candidates = new List<(int a, int b, float distance)>();
            for (int a = 0; a < vertices.Length; a++)
            {
                for (int b = a + 1; b < vertices.Length; b++)
                    candidates.Add((a, b, (vertices[a] - vertices[b]).sqrMagnitude));
            }
            candidates.Sort((left, right) => left.distance.CompareTo(right.distance));
            var edges = new List<int>(edgeLimit * 2);
            for (int index = 0; index < candidates.Count && index < edgeLimit; index++)
            {
                edges.Add(candidates[index].a);
                edges.Add(candidates[index].b);
            }
            return edges.ToArray();
        }

        private static Vector3[] BuildFaceCenters(Vector3[] sourceVertices, int[,] faces)
        {
            int faceCount = faces.GetLength(0);
            Vector3[] centers = new Vector3[faceCount];
            for (int index = 0; index < faceCount; index++)
            {
                centers[index] = (
                    sourceVertices[faces[index, 0]]
                    + sourceVertices[faces[index, 1]]
                    + sourceVertices[faces[index, 2]]) / 3f;
            }
            return centers;
        }

        private static int[] BuildFaceAdjacencyEdges(int[,] faces)
        {
            int faceCount = faces.GetLength(0);
            var edges = new List<int>(60);
            for (int a = 0; a < faceCount; a++)
            {
                for (int b = a + 1; b < faceCount; b++)
                {
                    int shared = 0;
                    for (int ai = 0; ai < 3; ai++)
                    {
                        for (int bi = 0; bi < 3; bi++)
                        {
                            if (faces[a, ai] == faces[b, bi])
                                shared++;
                        }
                    }
                    if (shared == 2)
                    {
                        edges.Add(a);
                        edges.Add(b);
                    }
                }
            }
            return edges.ToArray();
        }

        private static void UpdatePose(List<Star> stars, List<Line> lines, int[] edges, Quaternion rotation, float radius, Color lineColor, Color starColor, bool showStart)
        {
            foreach (Star star in stars)
            {
                bool visible = showStart ? star.HasStart : star.HasEnd;
                Vector3 point = showStart ? star.From : star.To;
                PlaceStar(star, point, rotation, radius, starColor, visible ? 1f : 0f);
            }
            UpdateLines(lines, stars, edges, lineColor, 1f);
        }

        private static void PlaceStar(Star star, Vector3 point, Quaternion rotation, float radius, Color color, float alpha)
        {
            Vector3 rotated = rotation * point;
            float perspective = 1.22f / Mathf.Max(0.42f, 1.22f - rotated.z * 0.36f);
            star.Rect.anchoredPosition = new Vector2(rotated.x, rotated.y) * radius * perspective;
            float size = Mathf.Lerp(10f, 20f, Mathf.InverseLerp(-1f, 1f, rotated.z));
            star.Rect.sizeDelta = Vector2.one * size * Mathf.Lerp(0.8f, 1.08f, perspective - 0.8f);
            star.Image.color = new Color(color.r, color.g, color.b, alpha);
        }

        private static Quaternion Rotation(float phase)
        {
            return Quaternion.Euler(18f, -28f + phase * 72f, 0f);
        }

        private static void UpdateLines(List<Line> lines, List<Star> stars, int[] edges, Color color, float alpha)
        {
            for (int index = 0; index < lines.Count; index++)
            {
                int edgeIndex = index * 2;
                bool active = edges != null && edgeIndex + 1 < edges.Length;
                lines[index].Image.enabled = active;
                if (!active)
                    continue;

                Vector2 from = stars[edges[edgeIndex]].Rect.anchoredPosition;
                Vector2 to = stars[edges[edgeIndex + 1]].Rect.anchoredPosition;
                Vector2 delta = to - from;
                lines[index].Rect.anchoredPosition = from + delta * 0.5f;
                lines[index].Rect.sizeDelta = new Vector2(delta.magnitude, 4f);
                lines[index].Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                lines[index].Image.color = new Color(color.r, color.g, color.b, color.a * alpha);
            }
        }

        private static void HideLines(List<Line> lines)
        {
            foreach (Line line in lines)
                line.Image.enabled = false;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, FontStyle style, TextAnchor alignment)
        {
            Text text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, size / 2);
            text.resizeTextMaxSize = size;
            global::AccardND.Battlefield.EditableRuntimeText.Bind(text);
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label)
        {
            Image image = CreateImage(name, parent, new Color(0.33f, 0.08f, 0.48f, 0.95f));
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text text = CreateText("Label", image.transform, font, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.text = label;
            global::AccardND.Battlefield.EditableRuntimeText.Bind(text, fallbackDefaultText: label);
            Stretch(text.rectTransform, 4f);
            return button;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            SetRect(rect, Vector2.zero, Vector2.one);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureCamera()
        {
            if ((Object)Camera.main != null)
                return;

            Camera camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.01f, 0.022f, 1f);
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void RemoveBattleBoardIfPresent()
        {
            BattleBoardController controller = FindAnyObjectByType<BattleBoardController>();
            if ((Object)controller == null)
                return;

            Destroy(controller.gameObject);
        }

        private static void EnsureEventSystem()
        {
            if ((Object)FindAnyObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
