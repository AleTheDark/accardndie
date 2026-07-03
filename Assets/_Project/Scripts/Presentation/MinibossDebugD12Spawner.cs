using AccardND.Battlefield;
using AccardND.GameCore;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MinibossDebugD12Spawner : MonoBehaviour
    {
        private const int TextureSize = 1024;

        private const string DndD12ResourcePath = "DnD_Dice/Mesh/00_D12";

        // Se attivo mostra l'asset DnD_Dice D12 (con il suo material originale)
        // al posto del dado di classe, per valutarlo visivamente.
        [SerializeField] private bool useDndD12 = true;
        [SerializeField] private int sides = 12;
        [SerializeField] private HeroClass heroClass = HeroClass.Assassin;
        [SerializeField] private float rotationSpeed = 30f;
        [SerializeField] private float screenSize = 620f;
        // Modalità calibrazione: mostra le facce una alla volta (nell'ordine
        // deterministico di ClassDice3D) per leggere i numeri stampati sulla
        // texture e compilare la mappa faccia→valore.
        [SerializeField] private bool calibrateFaces;
        [SerializeField] private float secondsPerFace = 2.5f;

        private Transform model;
        private GameObject renderRoot;
        private RenderTexture renderTexture;
        private System.Collections.Generic.List<DieFace> faces;
        private Text calibrationText;

        private void Awake()
        {
            // Rig di render isolato, lontano dal resto della scena.
            renderRoot = new GameObject("D12 Debug Render Scene");
            renderRoot.transform.position = new Vector3(10000f, 10000f, 10000f);

            GameObject instance;
            if (useDndD12)
            {
                GameObject prefab = Resources.Load<GameObject>(DndD12ResourcePath);
                if (prefab == null)
                {
                    Debug.LogError($"[Accard N' Die] Asset '{DndD12ResourcePath}' non trovato in Resources.");
                    Destroy(renderRoot);
                    return;
                }
                instance = Instantiate(prefab, renderRoot.transform, false);
                instance.name = "DnD D12";
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else
            {
                instance = ClassDice3D.Create(sides, heroClass, renderRoot.transform);
            }
            if (instance == null)
            {
                Destroy(renderRoot);
                return;
            }
            model = instance.transform;
            NormalizeModel(model);
            faces = useDndD12 ? null : ClassDice3D.GetFaces(instance, sides);

            renderTexture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "D12 Debug RT",
                antiAliasing = 4
            };
            renderTexture.Create();

            Camera camera = new GameObject("D12 Debug Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(renderRoot.transform, false);
            camera.transform.localPosition = new Vector3(0f, 0f, -3f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 0.75f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            camera.targetTexture = renderTexture;

            CreateScreenOutput();
            Debug.Log($"[Accard N' Die] D{sides} di classe {heroClass} mostrato al centro dello schermo.");
        }

        private void CreateScreenOutput()
        {
            GameObject canvasGo = new GameObject("D12 Debug Canvas", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            GameObject imageGo = new GameObject("D12 Debug Image", typeof(RawImage));
            imageGo.transform.SetParent(canvasGo.transform, false);
            RawImage image = imageGo.GetComponent<RawImage>();
            image.texture = renderTexture;
            image.raycastTarget = false;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(screenSize, screenSize);

            GameObject textGo = new GameObject("Calibration Text", typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);
            calibrationText = textGo.GetComponent<Text>();
            calibrationText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            calibrationText.fontSize = 34;
            calibrationText.fontStyle = FontStyle.Bold;
            calibrationText.alignment = TextAnchor.MiddleCenter;
            calibrationText.color = new Color(1f, 0.85f, 0.3f);
            calibrationText.raycastTarget = false;
            RectTransform textRect = calibrationText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0f, -screenSize * 0.5f - 40f);
            textRect.sizeDelta = new Vector2(900f, 60f);
        }

        // Riporta il modello a dimensione unitaria e centrato sul pivot,
        // qualunque sia la scala di import dell'FBX.
        private static void NormalizeModel(Transform target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize > 0.0001f)
                target.localScale *= 1f / maxSize;

            Renderer[] scaled = target.GetComponentsInChildren<Renderer>(true);
            Bounds scaledBounds = scaled[0].bounds;
            for (int i = 1; i < scaled.Length; i++)
                scaledBounds.Encapsulate(scaled[i].bounds);
            target.position += target.parent.position - scaledBounds.center;
        }

        private void Update()
        {
            if (model == null)
                return;

            if (calibrateFaces && faces != null && faces.Count > 0)
            {
                // La camera guarda il dado lungo +Z: la faccia in esame va su -Z.
                int index = Mathf.FloorToInt(Time.time / Mathf.Max(secondsPerFace, 0.5f)) % faces.Count;
                DieFace face = faces[index];
                model.localRotation = Quaternion.FromToRotation(face.Normal, Vector3.back);
                if (calibrationText != null)
                    calibrationText.text = $"Faccia {index + 1}/{faces.Count} — valore assegnato {face.Value}";
                return;
            }

            if (calibrationText != null && calibrationText.text.Length > 0)
                calibrationText.text = string.Empty;
            model.Rotate(20f * Time.deltaTime, rotationSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            if (renderRoot != null)
                Destroy(renderRoot);
        }
    }
}
