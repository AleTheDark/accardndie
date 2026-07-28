using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Editor
{
    /// <summary>
    /// I RectTransform del gioco sono costruiti via codice a runtime: spostarli in play mode
    /// non lascia niente da salvare in scena. Questa finestra cattura i valori runtime
    /// dell'oggetto selezionato e li restituisce come snippet C# da incollare nel builder
    /// di layout (es. BattleBoardController.ApplyResponsiveLayout).
    /// </summary>
    public sealed class PlayModeLayoutCaptureWindow : EditorWindow
    {
        [Serializable]
        private sealed class Capture
        {
            public string Path;
            public string ObjectName;
            public string FieldExpression;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public Vector3 LocalScale;
            public Vector3 LocalEulerAngles;
            public bool Frozen;

            [NonSerialized] public RectTransform Rect;
        }

        private const float OffsetEpsilon = 0.01f;

        [SerializeField] private List<Capture> captures = new List<Capture>();
        [SerializeField] private Vector2 scroll;
        [SerializeField] private bool includeTransformExtras;

        [MenuItem("Accard N' Die/Layout/Play Mode Layout Capture", priority = 81)]
        public static void Open()
        {
            PlayModeLayoutCaptureWindow window = GetWindow<PlayModeLayoutCaptureWindow>();
            window.titleContent = new GUIContent("Layout Capture");
            window.minSize = new Vector2(420f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        // Il layout viene riapplicato dal gioco a ogni cambio schermo/stato: per gli elementi
        // "frozen" riscriviamo i valori catturati a ogni update, tranne quando l'oggetto e'
        // selezionato (in quel caso stai editando tu nell'Inspector e assorbiamo le modifiche).
        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying || captures.Count == 0)
                return;

            bool dirty = false;
            foreach (Capture capture in captures)
            {
                if (!capture.Frozen)
                    continue;

                if (capture.Rect == null && !TryRebind(capture))
                    continue;

                if (IsSelected(capture.Rect))
                {
                    if (ReadInto(capture, capture.Rect))
                        dirty = true;
                    continue;
                }

                ApplyTo(capture, capture.Rect);
            }

            if (dirty)
                Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Entra in play mode, sposta l'elemento dall'Inspector, poi premi \"Cattura selezione\". " +
                    "Gli snippet gia' catturati restano leggibili anche dopo l'uscita dal play mode.",
                    MessageType.Info);
            }

            if (captures.Count == 0)
            {
                EditorGUILayout.HelpBox("Nessuna cattura. Seleziona un oggetto UI in Hierarchy e premi \"Cattura selezione\".", MessageType.None);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = captures.Count - 1; i >= 0; i--)
                DrawCapture(captures[i], i);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUI.enabled = EditorApplication.isPlaying && Selection.gameObjects.Length > 0;
            if (GUILayout.Button("Cattura selezione", EditorStyles.toolbarButton))
                CaptureSelection();
            GUI.enabled = true;

            GUI.enabled = captures.Count > 0;
            if (GUILayout.Button("Copia tutto", EditorStyles.toolbarButton))
            {
                EditorGUIUtility.systemCopyBuffer = BuildAllSnippets();
                ShowNotification(new GUIContent($"{captures.Count} snippet copiati"));
            }

            if (GUILayout.Button("Svuota", EditorStyles.toolbarButton) &&
                EditorUtility.DisplayDialog("Layout Capture", "Elimino tutte le catture?", "Elimina", "Annulla"))
            {
                captures.Clear();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();
            includeTransformExtras = GUILayout.Toggle(
                includeTransformExtras,
                new GUIContent("Scala/rotazione", "Includi negli snippet localScale e localEulerAngles anche quando sono ai valori di default."),
                EditorStyles.toolbarButton);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCapture(Capture capture, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(capture.ObjectName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            GUI.enabled = EditorApplication.isPlaying;
            bool frozen = GUILayout.Toggle(capture.Frozen, "Freeze", EditorStyles.miniButton, GUILayout.Width(56f));
            if (frozen != capture.Frozen)
            {
                capture.Frozen = frozen;
                if (frozen && capture.Rect == null)
                    TryRebind(capture);
            }

            if (GUILayout.Button("Aggiorna", EditorStyles.miniButton, GUILayout.Width(62f)))
            {
                if (capture.Rect != null || TryRebind(capture))
                {
                    ReadInto(capture, capture.Rect);
                    ResolveFieldExpression(capture, capture.Rect);
                }
            }
            GUI.enabled = true;

            if (GUILayout.Button("Copia", EditorStyles.miniButton, GUILayout.Width(46f)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildSnippet(capture);
                ShowNotification(new GUIContent("Snippet copiato"));
            }

            if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f)))
            {
                captures.RemoveAt(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(capture.Path, EditorStyles.miniLabel);
            EditorGUILayout.SelectableLabel(
                BuildSnippet(capture),
                EditorStyles.textArea,
                GUILayout.Height(EditorGUIUtility.singleLineHeight * CountLines(BuildSnippet(capture))));

            EditorGUILayout.EndVertical();
        }

        private void CaptureSelection()
        {
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (selected == null || selected.transform is not RectTransform rect)
                    continue;

                string path = BuildPath(rect);
                Capture capture = captures.Find(existing => existing.Path == path);
                if (capture == null)
                {
                    capture = new Capture { Path = path, ObjectName = selected.name };
                    captures.Add(capture);
                }

                capture.Rect = rect;
                ReadInto(capture, rect);
                ResolveFieldExpression(capture, rect);
            }

            Repaint();
        }

        private static bool IsSelected(RectTransform rect)
        {
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (selected != null && selected.transform == rect)
                    return true;
            }

            return false;
        }

        private static bool TryRebind(Capture capture)
        {
            GameObject found = GameObject.Find(capture.Path);
            if (found == null || found.transform is not RectTransform rect)
                return false;

            capture.Rect = rect;
            return true;
        }

        private static bool ReadInto(Capture capture, RectTransform rect)
        {
            if (rect == null)
                return false;

            bool changed =
                capture.AnchorMin != rect.anchorMin ||
                capture.AnchorMax != rect.anchorMax ||
                capture.Pivot != rect.pivot ||
                capture.AnchoredPosition != rect.anchoredPosition ||
                capture.SizeDelta != rect.sizeDelta ||
                capture.LocalScale != rect.localScale ||
                capture.LocalEulerAngles != rect.localEulerAngles;

            capture.AnchorMin = rect.anchorMin;
            capture.AnchorMax = rect.anchorMax;
            capture.Pivot = rect.pivot;
            capture.AnchoredPosition = rect.anchoredPosition;
            capture.SizeDelta = rect.sizeDelta;
            capture.LocalScale = rect.localScale;
            capture.LocalEulerAngles = rect.localEulerAngles;
            return changed;
        }

        private static void ApplyTo(Capture capture, RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = capture.AnchorMin;
            rect.anchorMax = capture.AnchorMax;
            rect.pivot = capture.Pivot;
            rect.anchoredPosition = capture.AnchoredPosition;
            rect.sizeDelta = capture.SizeDelta;
            rect.localScale = capture.LocalScale;
            rect.localEulerAngles = capture.LocalEulerAngles;
        }

        private string BuildAllSnippets()
        {
            StringBuilder builder = new StringBuilder();
            foreach (Capture capture in captures)
            {
                builder.AppendLine(BuildSnippet(capture));
                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private string BuildSnippet(Capture capture)
        {
            string target = string.IsNullOrEmpty(capture.FieldExpression) ? "/* rect */" : capture.FieldExpression;
            StringBuilder builder = new StringBuilder();
            builder.Append("// ").AppendLine(capture.Path);

            bool stretched = capture.AnchorMin != capture.AnchorMax;
            Vector2 offsetMin = capture.AnchoredPosition - Vector2.Scale(capture.SizeDelta, capture.Pivot);
            Vector2 offsetMax = offsetMin + capture.SizeDelta;

            if (stretched && offsetMin.sqrMagnitude < OffsetEpsilon && offsetMax.sqrMagnitude < OffsetEpsilon)
            {
                // Forma canonica del progetto: SetRect() azzera gli offset.
                builder.Append("SetRect(").Append(target).Append(", ")
                    .Append(Format(capture.AnchorMin)).Append(", ")
                    .Append(Format(capture.AnchorMax)).Append(");");
            }
            else if (stretched)
            {
                builder.Append(target).Append(".anchorMin = ").Append(Format(capture.AnchorMin)).AppendLine(";");
                builder.Append(target).Append(".anchorMax = ").Append(Format(capture.AnchorMax)).AppendLine(";");
                builder.Append(target).Append(".offsetMin = ").Append(Format(offsetMin)).AppendLine(";");
                builder.Append(target).Append(".offsetMax = ").Append(Format(offsetMax)).Append(";");
                builder.AppendLine();
                builder.Append("// offset non nulli: SetRect() li azzererebbe, usa le righe qui sopra.");
            }
            else
            {
                builder.Append(target).Append(".anchorMin = ").Append(target).Append(".anchorMax = ")
                    .Append(Format(capture.AnchorMin)).AppendLine(";");
                builder.Append(target).Append(".pivot = ").Append(Format(capture.Pivot)).AppendLine(";");
                builder.Append(target).Append(".anchoredPosition = ").Append(Format(capture.AnchoredPosition)).AppendLine(";");
                builder.Append(target).Append(".sizeDelta = ").Append(Format(capture.SizeDelta)).Append(";");
            }

            if (includeTransformExtras || capture.LocalScale != Vector3.one)
            {
                builder.AppendLine();
                builder.Append(target).Append(".localScale = ").Append(Format(capture.LocalScale)).Append(";");
            }

            if (includeTransformExtras || capture.LocalEulerAngles.sqrMagnitude > 0.0001f)
            {
                builder.AppendLine();
                builder.Append(target).Append(".localEulerAngles = ").Append(Format(capture.LocalEulerAngles)).Append(";");
            }

            return builder.ToString();
        }

        private static int CountLines(string text)
        {
            int lines = 1;
            foreach (char character in text)
            {
                if (character == '\n')
                    lines++;
            }

            return lines + 1;
        }

        private static string Format(Vector2 value)
        {
            return $"new Vector2({Format(value.x)}, {Format(value.y)})";
        }

        private static string Format(Vector3 value)
        {
            return $"new Vector3({Format(value.x)}, {Format(value.y)}, {Format(value.z)})";
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture) + "f";
        }

        private static string BuildPath(Transform target)
        {
            StringBuilder builder = new StringBuilder();
            for (Transform current = target; current != null; current = current.parent)
                builder.Insert(0, current.name).Insert(0, '/');

            return builder.ToString();
        }

        // Cerca quale campo di quale MonoBehaviour in scena punta a questo RectTransform,
        // cosi' lo snippet nomina la variabile reale (es. playerHud.Rect) invece di un placeholder.
        private static void ResolveFieldExpression(Capture capture, RectTransform rect)
        {
            capture.FieldExpression = null;
            if (rect == null)
                return;

            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                string expression;
                try
                {
                    expression = FindExpression(behaviour, behaviour.GetType(), rect, string.Empty, 0);
                }
                catch (Exception)
                {
                    // Un componente che esplode in reflection non deve far fallire l'intera scansione.
                    continue;
                }

                if (string.IsNullOrEmpty(expression))
                    continue;

                capture.FieldExpression = expression;
                return;
            }
        }

        private static string FindExpression(object owner, Type type, RectTransform target, string prefix, int depth)
        {
            if (owner == null || type == null || depth > 2)
                return null;

            for (Type current = type; current != null && current != typeof(MonoBehaviour) && current != typeof(object); current = current.BaseType)
            {
                // Salta i campi interni di Unity (es. Graphic.m_RectTransform): non sono nomi
                // utilizzabili in uno snippet.
                if (current.Namespace != null && current.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal))
                    continue;

                FieldInfo[] fields = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                foreach (FieldInfo field in fields)
                {
                    object value;
                    try
                    {
                        value = field.GetValue(owner);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (value == null)
                        continue;

                    // Un campo Unity puo' contenere un riferimento distrutto o mai assegnato
                    // (es. EventSystem.m_FirstSelected): leggerne .transform lancerebbe
                    // UnassignedReferenceException. L'== di UnityEngine.Object li intercetta entrambi.
                    if (value is UnityEngine.Object unityValue && unityValue == null)
                        continue;

                    string name = prefix + field.Name;
                    switch (value)
                    {
                        case RectTransform rectValue when rectValue == target:
                            return name;
                        case Graphic graphic when graphic.transform == target:
                            return name + ".rectTransform";
                        // Le parentesi esterne servono: senza, il cast si lega all'accesso
                        // al membro che segue nello snippet e il codice non compila.
                        case GameObject gameObject when gameObject.transform == target:
                            return $"((RectTransform){name}.transform)";
                        case Component component when component.transform == target:
                            return $"((RectTransform)((Component){name}).transform)";
                        case UnityEngine.Object:
                            continue;
                    }

                    Type valueType = value.GetType();
                    if (!valueType.IsClass || valueType == typeof(string) || valueType.IsArray || valueType.Namespace?.StartsWith("System") == true)
                        continue;

                    string nested = FindExpression(value, valueType, target, name + ".", depth + 1);
                    if (!string.IsNullOrEmpty(nested))
                        return nested;
                }
            }

            return null;
        }
    }
}
