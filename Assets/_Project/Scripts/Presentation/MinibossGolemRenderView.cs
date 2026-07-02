using System.Collections;
using AccardND.GameCore;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MinibossGolemRenderView : MonoBehaviour
    {
        private const int TextureSize = 512;

        private RectTransform source;
        private RectTransform targetRoot;
        private Transform modelPivot;
        private Transform formRing;
        private GameObject renderRoot;
        private RenderTexture renderTexture;
        private Vector3 rotationOffset;
        private Sprite[] placeholderSprites = new Sprite[3];
        private Coroutine formRotationRoutine;
        private ComposableGolemForm activeForm = ComposableGolemForm.Iron;

        public void Configure(RectTransform sourceRect, RectTransform targetRectRoot, GameObject prefab, Vector3 modelRotationOffset, Sprite[] formPlaceholderSprites = null)
        {
            source = sourceRect;
            targetRoot = targetRectRoot;
            rotationOffset = modelRotationOffset;
            if (formPlaceholderSprites != null)
            {
                for (int i = 0; i < Mathf.Min(placeholderSprites.Length, formPlaceholderSprites.Length); i++)
                    placeholderSprites[i] = formPlaceholderSprites[i];
            }

            RawImage image = GetComponent<RawImage>();
            image.raycastTarget = false;
            image.color = new Color(0.015f, 0.02f, 0.026f, 1f);

            renderTexture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "Composable Golem Preview RT",
                antiAliasing = 4
            };
            renderTexture.Create();
            image.texture = renderTexture;
            image.color = Color.white;

            renderRoot = new GameObject("Composable Golem Render Scene");
            renderRoot.transform.position = new Vector3(10000f, 10000f, 10000f);

            modelPivot = new GameObject("Composable Golem Render Pivot").transform;
            modelPivot.SetParent(renderRoot.transform, false);

            GameObject model = Object.Instantiate(prefab, modelPivot, false);
            model.name = "Composable Golem Model";
            int rendererCount = model.GetComponentsInChildren<Renderer>(true).Length;
            if (!NormalizeModel(model.transform))
            {
                Destroy(model);
                CreateFallbackModel(modelPivot);
            }
            else
            {
                ApplyRuntimeGolemMaterials(model.transform);
            }
            Debug.Log((object)$"[Accard N' Die] Preview 3D Golem: prefab '{prefab.name}', renderer trovati {rendererCount}.");

            CreateFormPedestals();

            Camera camera = new GameObject("Composable Golem Preview Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(renderRoot.transform, false);
            camera.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            camera.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 1.28f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            camera.targetTexture = renderTexture;

            Light light = new GameObject("Composable Golem Preview Light", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(renderRoot.transform, false);
            light.transform.localPosition = new Vector3(0.2f, 2.4f, -0.4f);
            light.type = LightType.Directional;
            light.intensity = 1.55f;
            light.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            SetActiveForm(activeForm, false);
        }

        public void SetActiveForm(ComposableGolemForm form, bool animate = true)
        {
            activeForm = form;
            if (formRing == null)
                return;

            float targetAngle = form switch
            {
                ComposableGolemForm.Iron => 0f,
                ComposableGolemForm.Crystal => 120f,
                ComposableGolemForm.Glass => 240f,
                _ => 0f
            };

            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            if (!animate)
            {
                formRing.localRotation = targetRotation;
                return;
            }

            if (formRotationRoutine != null)
                StopCoroutine(formRotationRoutine);
            formRotationRoutine = StartCoroutine(AnimateFormRing(targetRotation));
        }

        private void LateUpdate()
        {
            FaceNow();
        }

        private void OnDestroy()
        {
            if (renderRoot != null)
                Destroy(renderRoot);
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        private void FaceNow()
        {
            if (modelPivot == null || source == null || targetRoot == null)
                return;

            Vector3 sourcePosition = source.position;
            Vector3 targetPosition = TargetCenter();
            Vector2 boardDirection = new(targetPosition.x - sourcePosition.x, targetPosition.y - sourcePosition.y);
            if (boardDirection.sqrMagnitude < 0.001f)
                return;

            Vector3 lookDirection = new(boardDirection.x, 0f, boardDirection.y);
            modelPivot.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                * Quaternion.Euler(rotationOffset);
        }

        private IEnumerator AnimateFormRing(Quaternion targetRotation)
        {
            Quaternion start = formRing.localRotation;
            float elapsed = 0f;
            const float duration = 0.55f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                formRing.localRotation = Quaternion.Slerp(start, targetRotation, progress);
                yield return null;
            }

            formRing.localRotation = targetRotation;
            formRotationRoutine = null;
        }

        private void CreateFormPedestals()
        {
            formRing = new GameObject("Composable Golem Form Ring").transform;
            formRing.SetParent(renderRoot.transform, false);
            CreateFormPedestal(ComposableGolemForm.Iron, "FERRO", new Vector3(0f, -0.02f, -0.98f), new Color(0.88f, 0.84f, 0.72f));
            CreateFormPedestal(ComposableGolemForm.Crystal, "CRISTALLO", new Vector3(-0.88f, -0.02f, 0.52f), new Color(0.06f, 0.78f, 1f));
            CreateFormPedestal(ComposableGolemForm.Glass, "VETRO", new Vector3(0.88f, -0.02f, 0.52f), new Color(0.5f, 1f, 0.9f));
        }

        private void CreateFormPedestal(ComposableGolemForm form, string label, Vector3 localPosition, Color color)
        {
            Transform root = new GameObject(label + " Token").transform;
            root.SetParent(formRing, false);
            root.localPosition = localPosition;

            GameObject token = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            token.name = label + " Token Body";
            token.transform.SetParent(root, false);
            token.transform.localScale = new Vector3(0.32f, 0.045f, 0.32f);
            token.transform.localPosition = Vector3.zero;
            AssignMaterial(token.GetComponent<Renderer>(), color, 0.16f, 0.64f);

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = label + " Marker";
            marker.transform.SetParent(root, false);
            marker.transform.localScale = new Vector3(0.18f, 0.045f, 0.18f);
            marker.transform.localPosition = new Vector3(0f, 0.105f, 0f);
            AssignMaterial(marker.GetComponent<Renderer>(), Color.Lerp(color, Color.white, 0.18f), 0f, 0.42f);
        }

        private Vector3 TargetCenter()
        {
            int count = 0;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < targetRoot.childCount; i++)
            {
                Transform child = targetRoot.GetChild(i);
                if (!child.gameObject.activeInHierarchy)
                    continue;

                sum += child.position;
                count++;
            }

            return count > 0 ? sum / count : targetRoot.position;
        }

        private static bool NormalizeModel(Transform model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return false;

            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds rendererBounds = renderers[i].bounds;
                Vector3 min = rendererBounds.min;
                Vector3 max = rendererBounds.max;
                Vector3[] corners =
                {
                    new(min.x, min.y, min.z),
                    new(min.x, min.y, max.z),
                    new(min.x, max.y, min.z),
                    new(min.x, max.y, max.z),
                    new(max.x, min.y, min.z),
                    new(max.x, min.y, max.z),
                    new(max.x, max.y, min.z),
                    new(max.x, max.y, max.z)
                };

                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 localCorner = model.InverseTransformPoint(corners[cornerIndex]);
                    if (!hasBounds)
                    {
                        bounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localCorner);
                    }
                }
            }

            if (!hasBounds)
                return false;

            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 0.001f);
            float scale = 1.72f / maxSize;
            model.localScale = Vector3.one * scale;
            model.localPosition = new Vector3(0f, 0.06f, 0f) - bounds.center * scale;
            return true;
        }

        private static void ApplyRuntimeGolemMaterials(Transform model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Texture2D albedo = Resources.Load<Texture2D>("Minibosses/texture_pbr_20250901");
            Texture2D normal = Resources.Load<Texture2D>("Minibosses/texture_pbr_20250901_normal");
            Texture2D metallic = Resources.Load<Texture2D>("Minibosses/texture_pbr_20250901_metallic");
            Texture2D roughness = Resources.Load<Texture2D>("Minibosses/texture_pbr_20250901_roughness");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                    materials = new Material[1];

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = new(shader)
                    {
                        name = "Runtime Textured Golem Material",
                        color = Color.white
                    };
                    if (material.HasProperty("_Color"))
                        material.SetColor("_Color", Color.white);
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", Color.white);
                    if (albedo != null)
                    {
                        material.mainTexture = albedo;
                        if (material.HasProperty("_BaseMap"))
                            material.SetTexture("_BaseMap", albedo);
                    }
                    if (normal != null)
                    {
                        if (material.HasProperty("_BumpMap"))
                            material.SetTexture("_BumpMap", normal);
                        if (material.HasProperty("_NORMALMAP"))
                            material.EnableKeyword("_NORMALMAP");
                    }
                    if (metallic != null && material.HasProperty("_MetallicGlossMap"))
                    {
                        material.SetTexture("_MetallicGlossMap", metallic);
                        material.EnableKeyword("_METALLICSPECGLOSSMAP");
                    }
                    if (material.HasProperty("_Metallic"))
                        material.SetFloat("_Metallic", metallic != null ?0.55f : 0.18f);
                    if (material.HasProperty("_Smoothness"))
                        material.SetFloat("_Smoothness", roughness != null ?0.62f : 0.38f);
                    if (roughness != null && material.HasProperty("_SpecGlossMap"))
                        material.SetTexture("_SpecGlossMap", roughness);
                    materials[materialIndex] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static void AssignMaterial(Renderer renderer, Color color, float metallic, float smoothness, Sprite sprite = null)
        {
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");
            Material material = new(shader)
            {
                name = "Runtime Form Token Material",
                color = color
            };
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (sprite != null && sprite.texture != null)
            {
                material.mainTexture = sprite.texture;
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", sprite.texture);
            }
            renderer.sharedMaterial = material;
        }

        private static void CreateFallbackModel(Transform parent)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Composable Golem Fallback Body";
            body.transform.SetParent(parent, false);
            body.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);
            body.transform.localPosition = Vector3.zero;

            Renderer renderer = body.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material material = new(shader) { color = new Color(0.48f, 0.54f, 0.58f, 1f) };
                renderer.sharedMaterial = material;
            }
        }
    }
}
