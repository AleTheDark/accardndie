using System.Collections;
using System.Collections.Generic;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    [DisallowMultipleComponent]
    public sealed class Card3DView : MonoBehaviour
    {
        [Header("Dimensioni mondo")]
        [SerializeField, Min(0.1f)] private float height = 1f;
        [SerializeField, Min(0.001f)] private float thickness = 0.035f;
        [SerializeField, Min(0f)] private float cornerRadius = 0.045f;
        [SerializeField, Range(2, 10)] private int cornerSegments = 5;
        [SerializeField] private Color edgeColor = new(0.12f, 0.09f, 0.06f, 1f);
        [SerializeField] private Color backFallbackColor = new(0.08f, 0.12f, 0.16f, 1f);

        private const float CardAspect = 848f / 1264f;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Transform frontSurface;
        private Transform backSurface;
        private GameObject frontContent;
        private Material edgeMaterial;
        private Coroutine animationCoroutine;

        public CardDefinition Definition { get; private set; }
        public float Width => height * CardAspect;
        public float Height => height;
        public float Thickness => thickness;

        private void Awake()
        {
            RebuildGeometry();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    Destroy(meshFilter.sharedMesh);
                if (edgeMaterial != null)
                    Destroy(edgeMaterial);
            }
        }

        public void Bind(CardDefinition definition, GameConfiguration configuration)
        {
            Definition = definition;
            RebuildGeometry();
            if (frontContent != null)
                DestroyRuntimeObject(frontContent);

            if (definition == null || configuration == null || frontSurface == null)
                return;

            PrototypeCardView card = PrototypeCardView.Create(frontSurface, definition, configuration);
            frontContent = card.gameObject;
            card.name = $"Front - {definition.DisplayName}";
            card.SetInteractable(false);
            RectTransform rect = (RectTransform)card.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        public void RebuildGeometry()
        {
            EnsureComponents();
            Mesh oldMesh = meshFilter.sharedMesh;
            meshFilter.sharedMesh = BuildRoundedPrism(Width, height, thickness, cornerRadius, cornerSegments);
            meshFilter.sharedMesh.name = "Card 3D Rounded Body";
            if (oldMesh != null && oldMesh != meshFilter.sharedMesh)
                DestroyRuntimeObject(oldMesh);

            if (edgeMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                edgeMaterial = new Material(shader) { name = "Card 3D Edge (Runtime)" };
            }
            edgeMaterial.color = edgeColor;
            if (edgeMaterial.HasProperty("_Smoothness"))
                edgeMaterial.SetFloat("_Smoothness", 0.32f);
            meshRenderer.sharedMaterial = edgeMaterial;

            EnsureSurface(ref frontSurface, "Front Surface", false);
            EnsureSurface(ref backSurface, "Back Surface", true);
            BuildBackSurface();
        }

        public void SetRaised(bool raised)
        {
            Vector3 position = transform.localPosition;
            position.z = raised ? -0.12f : 0f;
            transform.localPosition = position;
            transform.localRotation = raised
                ? Quaternion.Euler(-4f, 0f, 0f)
                : Quaternion.identity;
        }

        public void AnimateDeal(Vector3 fromLocalPosition, Vector3 toLocalPosition, float duration = 0.3f)
        {
            StartAnimation(AnimateTransform(fromLocalPosition, toLocalPosition,
                Quaternion.Euler(0f, 180f, -8f), Quaternion.identity, duration));
        }

        public void AnimateFlip(bool showFront, float duration = 0.35f)
        {
            Quaternion target = showFront ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            StartAnimation(AnimateRotation(transform.localRotation, target, duration));
        }

        private void StartAnimation(IEnumerator routine)
        {
            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);
            animationCoroutine = StartCoroutine(routine);
        }

        private IEnumerator AnimateTransform(
            Vector3 from,
            Vector3 to,
            Quaternion fromRotation,
            Quaternion toRotation,
            float duration)
        {
            transform.localPosition = from;
            transform.localRotation = fromRotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration)));
                transform.localPosition = Vector3.LerpUnclamped(from, to, progress);
                transform.localRotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, progress);
                yield return null;
            }
            transform.localPosition = to;
            transform.localRotation = toRotation;
            animationCoroutine = null;
        }

        private IEnumerator AnimateRotation(Quaternion from, Quaternion to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration)));
                transform.localRotation = Quaternion.SlerpUnclamped(from, to, progress);
                yield return null;
            }
            transform.localRotation = to;
            animationCoroutine = null;
        }

        private void EnsureComponents()
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
        }

        private void EnsureSurface(ref Transform surface, string surfaceName, bool back)
        {
            Transform existing = transform.Find(surfaceName);
            if (existing == null)
            {
                var surfaceObject = new GameObject(surfaceName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                existing = surfaceObject.transform;
                existing.SetParent(transform, false);
                Canvas canvas = surfaceObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.overrideSorting = true;
                canvas.sortingOrder = back ? 0 : 1;
                CanvasScaler scaler = surfaceObject.GetComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 2f;
            }
            surface = existing;
            RectTransform rect = (RectTransform)surface;
            rect.sizeDelta = new Vector2(848f, 1264f);
            rect.localScale = new Vector3(Width / 848f, height / 1264f, 1f);
            rect.localPosition = new Vector3(0f, 0f, back ? thickness * 0.501f : -thickness * 0.501f);
            rect.localRotation = back ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        }

        private void BuildBackSurface()
        {
            if (backSurface == null || backSurface.childCount > 0)
                return;
            var imageObject = new GameObject("Card Back", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(backSurface, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = backFallbackColor;
            image.raycastTarget = false;
            CardDatabase database = Resources.Load<CardDatabase>("CardDatabase");
            if (database != null)
            {
                foreach (CardDefinition card in database.Cards)
                {
                    if (card != null && card.Category == CardCategory.CardBack && card.Artwork != null)
                    {
                        image.sprite = card.Artwork;
                        image.color = Color.white;
                        image.preserveAspect = true;
                        break;
                    }
                }
            }
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Mesh BuildRoundedPrism(
            float width,
            float height,
            float depth,
            float radius,
            int segments)
        {
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(width, height) * 0.25f);
            segments = Mathf.Max(2, segments);
            var outline = new List<Vector2>(segments * 4);
            Vector2[] centers =
            {
                new(width * 0.5f - radius, height * 0.5f - radius),
                new(-width * 0.5f + radius, height * 0.5f - radius),
                new(-width * 0.5f + radius, -height * 0.5f + radius),
                new(width * 0.5f - radius, -height * 0.5f + radius)
            };
            float[] starts = { 0f, 90f, 180f, 270f };
            for (int corner = 0; corner < 4; corner++)
            {
                for (int step = 0; step < segments; step++)
                {
                    float angle = (starts[corner] + 90f * step / (segments - 1)) * Mathf.Deg2Rad;
                    outline.Add(centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }

            int count = outline.Count;
            var vertices = new Vector3[count * 2 + 2];
            vertices[0] = new Vector3(0f, 0f, -depth * 0.5f);
            vertices[count + 1] = new Vector3(0f, 0f, depth * 0.5f);
            for (int index = 0; index < count; index++)
            {
                vertices[index + 1] = new Vector3(outline[index].x, outline[index].y, -depth * 0.5f);
                vertices[count + 2 + index] = new Vector3(outline[index].x, outline[index].y, depth * 0.5f);
            }

            var triangles = new List<int>(count * 12);
            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                triangles.Add(0); triangles.Add(next + 1); triangles.Add(index + 1);
                triangles.Add(count + 1); triangles.Add(count + 2 + index); triangles.Add(count + 2 + next);
                int front = index + 1;
                int frontNext = next + 1;
                int back = count + 2 + index;
                int backNext = count + 2 + next;
                triangles.Add(front); triangles.Add(backNext); triangles.Add(frontNext);
                triangles.Add(front); triangles.Add(back); triangles.Add(backNext);
            }

            var mesh = new Mesh { vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
