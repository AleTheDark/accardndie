using System.Collections;
using System.Collections.Generic;
using AccardND.GameCore;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    /// <summary>
    /// Mostra un dado 3D di classe dentro un'area UI e lo fa rotolare in modo
    /// scriptato: il risultato è deciso prima, il dado gira e decelera fino a
    /// fermarsi con la faccia del risultato rivolta verso la camera.
    /// </summary>
    public sealed class Dice3DRollView : MonoBehaviour
    {
        private const int TextureSize = 512;
        private const float CameraDistance = 3f;

        private static int rigCounter;

        private RawImage image;
        private RectTransform viewRect;
        private Image[] frictionSparkImages;
        private RectTransform[] frictionSparkRects;
        private RenderTexture renderTexture;
        private GameObject renderRoot;
        private Transform diePivot;
        private Camera renderCamera;
        private GameObject die;
        private int dieSides = -1;
        private HeroClass dieClass;
        private List<DieFace> dieFaces = new List<DieFace>();
        private Coroutine rollCoroutine;
        private bool homeCaptured;
        private Vector2 homeAnchoredPosition;
        private RectTransform bounceArea;
        private Dice3DRollView bouncePartner;
        private Vector2 bounceOffset;
        private Vector2 bounceVelocity;
        private Vector2 bounceMin;
        private Vector2 bounceMax;
        private float bounceCurveSeed;
        private float bounceCurveSign;
        private bool bouncing;

        /// <summary>Esiste il modello 3D per questo numero di facce?</summary>
        public static bool IsSupported(int sides)
        {
            return Resources.Load<GameObject>($"DnD_Dice/Mesh/00_D{ResolveSides(sides)}") != null;
        }

        // Il D3 logico non ha modello: si tira un D6 (il valore 1-3 esiste comunque).
        private static int ResolveSides(int sides)
        {
            return sides == 3 ? 6 : sides;
        }

        public static Dice3DRollView Create(Transform parent)
        {
            var go = new GameObject(
                "Die 3D",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(Dice3DRollView));
            go.transform.SetParent(parent, false);

            Dice3DRollView view = go.GetComponent<Dice3DRollView>();
            view.image = go.GetComponent<RawImage>();
            view.viewRect = (RectTransform)go.transform;
            view.image.raycastTarget = false;
            view.image.color = Color.white;
            view.CreateFrictionSparks();

            // Riempi l'area del genitore restando quadrato: il render è 1:1
            // e uno stiramento deformerebbe il dado.
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            AspectRatioFitter fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            view.BuildRig();
            go.SetActive(false);
            return view;
        }

        public RectTransform RectTransform => (RectTransform)transform;

        private void BuildRig()
        {
            renderTexture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "Die 3D RT",
                antiAliasing = 4
            };
            renderTexture.Create();
            ClearRenderTexture(renderTexture);
            image.texture = renderTexture;

            // Ogni vista ha il suo set isolato, lontano dalla scena e dagli altri.
            rigCounter++;
            renderRoot = new GameObject($"Die 3D Rig {rigCounter}");
            Object.DontDestroyOnLoad(renderRoot);
            renderRoot.transform.position = new Vector3(10000f, 10000f, 10000f + rigCounter * 50f);

            diePivot = new GameObject("Die Pivot").transform;
            diePivot.SetParent(renderRoot.transform, false);

            renderCamera = new GameObject("Die Camera", typeof(Camera)).GetComponent<Camera>();
            renderCamera.transform.SetParent(renderRoot.transform, false);
            renderCamera.transform.localPosition = new Vector3(0f, 1.35f, -CameraDistance);
            renderCamera.transform.localRotation = Quaternion.LookRotation(-renderCamera.transform.localPosition.normalized, Vector3.up);
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            renderCamera.orthographic = true;
            renderCamera.orthographicSize = 0.82f;
            renderCamera.nearClipPlane = 0.01f;
            renderCamera.farClipPlane = 10f;
            renderCamera.targetTexture = renderTexture;
            renderCamera.enabled = false;

            // Luce propria a corto raggio: la scena di battaglia è solo UI e
            // potrebbe non avere luci 3D che raggiungono il rig.
            Light light = new GameObject("Die Light", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(renderRoot.transform, false);
            light.transform.localPosition = new Vector3(0.8f, 1.4f, -1.8f);
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = 2.2f;
            light.color = Color.white;
        }

        /// <summary>
        /// Avvia il tiro scriptato: il dado rotola e si ferma con la faccia
        /// che mostra <paramref name="result"/> rivolta verso la camera.
        /// </summary>
        public void StartScriptedRoll(int sides, HeroClass heroClass, int result, float duration)
        {
            gameObject.SetActive(true);
            EnsureDie(ResolveSides(sides), heroClass);
            if (die != null)
                die.SetActive(true);
            HideFrictionSparks();
            if (rollCoroutine != null)
                StopCoroutine(rollCoroutine);
            rollCoroutine = StartCoroutine(SpiralRollRoutine(result, duration));
        }

        /// <summary>
        /// Sostituisce la tinta glow del dado corrente con un colore arbitrario
        /// (es. blu/rosso per l'iniziativa). Da chiamare dopo StartScriptedRoll.
        /// </summary>
        public void OverrideGlow(Color glow, string cacheKey)
        {
            if (die == null)
                return;
            Material material = ClassDice3D.GetCustomGlowMaterial(dieSides, cacheKey, glow);
            if (material == null)
                return;
            foreach (Renderer renderer in die.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
        }

        /// <summary>
        /// Area (in genere l'intera metà campo) sulle cui pareti il dado
        /// rimbalza durante il tiro, e l'eventuale dado gemello con cui urtare.
        /// </summary>
        public void SetBounceArea(RectTransform area, Dice3DRollView partner)
        {
            bounceArea = area;
            bouncePartner = partner;
        }

        public void Hide()
        {
            if (rollCoroutine != null)
            {
                StopCoroutine(rollCoroutine);
                rollCoroutine = null;
            }
            bouncing = false;
            if (homeCaptured)
                viewRect.anchoredPosition = homeAnchoredPosition;
            if (die != null)
                die.SetActive(true);
            HideFrictionSparks();
            renderCamera.enabled = false;
            if (renderTexture != null)
                ClearRenderTexture(renderTexture);
            gameObject.SetActive(false);
        }

        private static void ClearRenderTexture(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;
        }

        private void CreateFrictionSparks()
        {
            const int sparkCount = 24;
            frictionSparkImages = new Image[sparkCount];
            frictionSparkRects = new RectTransform[sparkCount];

            Sprite streakSprite = CreateFrictionStreakSprite();
            Sprite glowSprite = CreateSoftGlowSprite();
            Sprite moteSprite = CreateSparkMoteSprite();
            for (int index = 0; index < sparkCount; index++)
            {
                var sparkObject = new GameObject(
                    $"Arcane Friction Spark {index + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                sparkObject.transform.SetParent(transform, false);

                Image spark = sparkObject.GetComponent<Image>();
                spark.sprite = index < 10 ? streakSprite : index < 16 ? glowSprite : moteSprite;
                spark.color = Color.clear;
                spark.raycastTarget = false;

                RectTransform rect = spark.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = index < 10
                    ? new Vector2(78f, 12f)
                    : index < 16
                        ? new Vector2(38f, 38f)
                        : new Vector2(10f, 10f);

                frictionSparkImages[index] = spark;
                frictionSparkRects[index] = rect;
            }
        }

        private void UpdateFrictionSparks(Vector2 movementDelta, float progress, float spinAmount)
        {
            if (frictionSparkImages == null || movementDelta.sqrMagnitude < 0.0001f)
            {
                HideFrictionSparks();
                return;
            }

            Vector2 direction = movementDelta.normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float intensity = Mathf.Clamp01(movementDelta.magnitude / 16f + spinAmount / 220f);
            float fadeOut = 1f - SmoothStep3(Mathf.InverseLerp(0.72f, 1f, progress));
            float baseAlpha = Mathf.Clamp01(intensity * fadeOut * 1.18f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            for (int index = 0; index < frictionSparkImages.Length; index++)
            {
                float normalizedIndex = frictionSparkImages.Length <= 1
                    ? 0f
                    : (float)index / (frictionSparkImages.Length - 1);
                float layerOffset = index < 10 ? 0f : index < 16 ? 0.18f : 0.34f;
                float seed = normalizedIndex * 2.17f + layerOffset;
                float age = Mathf.Repeat(progress * 7.5f + seed, 1f);
                float side = index % 2 == 0 ? -1f : 1f;
                float flutter = SmoothNoise(seed, progress);
                float edgeNoise = SmoothNoise(seed + 2.4f, progress * 0.7f);
                float edgeDistance = Mathf.Lerp(index < 10 ? 16f : 22f, index < 16 ? 46f : 62f, age);
                edgeDistance += edgeNoise * (index < 10 ? 5f : 9f);
                float trailDistance = Mathf.Lerp(index < 10 ? -14f : -4f, index < 10 ? 118f : 76f, age);
                Vector2 position = normal * (side * edgeDistance) - direction * trailDistance;
                position += direction * Mathf.Sin((progress * 4f + seed) * Mathf.PI) * 5f;
                position += normal * flutter * (index < 10 ? 5f : 10f);

                RectTransform rect = frictionSparkRects[index];
                rect.anchoredPosition = position;
                float pulse = Mathf.Sin(age * Mathf.PI);
                float naturalFade = Mathf.Pow(pulse, 0.72f) * Mathf.Lerp(1f, 0.68f, age);
                Color color;

                if (index < 10)
                {
                    rect.localRotation = Quaternion.Euler(0f, 0f, angle + flutter * 7f);
                    rect.sizeDelta = new Vector2(Mathf.Lerp(78f, 154f, age), Mathf.Lerp(18f, 5f, age));
                    float alpha = naturalFade * baseAlpha * 0.62f;
                    Color core = new Color(1f, 0.9f, 0.48f, alpha);
                    Color arcane = new Color(0.5f, 0.86f, 1f, alpha * 0.56f);
                    Color ember = new Color(1f, 0.38f, 0.12f, alpha * 0.72f);
                    color = age < 0.45f
                        ? Color.Lerp(core, arcane, age / 0.45f)
                        : Color.Lerp(arcane, ember, (age - 0.45f) / 0.55f);
                }
                else if (index < 16)
                {
                    rect.localRotation = Quaternion.identity;
                    position = normal * (side * Mathf.Lerp(18f, 34f, age)) - direction * Mathf.Lerp(-8f, 42f, age);
                    position += normal * flutter * 6f;
                    rect.anchoredPosition = position;

                    float size = Mathf.Lerp(28f, 58f, pulse) * Mathf.Lerp(0.8f, 1.12f, intensity);
                    rect.sizeDelta = new Vector2(size, size);
                    float alpha = naturalFade * baseAlpha * 0.22f;
                    color = new Color(0.35f, 0.78f, 1f, alpha);
                }
                else
                {
                    rect.localRotation = Quaternion.Euler(0f, 0f, angle + 90f + flutter * 34f);
                    float size = Mathf.Lerp(4f, 12f, pulse) * Mathf.Lerp(0.75f, 1.18f, intensity);
                    rect.sizeDelta = new Vector2(size, size);
                    float alpha = naturalFade * baseAlpha * 0.72f;
                    Color hot = new Color(1f, 0.92f, 0.45f, alpha);
                    Color magic = new Color(0.62f, 0.92f, 1f, alpha * 0.62f);
                    color = Color.Lerp(hot, magic, Mathf.SmoothStep(0f, 1f, normalizedIndex));
                }

                frictionSparkImages[index].color = color;
            }
        }

        private void HideFrictionSparks()
        {
            if (frictionSparkImages == null)
                return;

            for (int index = 0; index < frictionSparkImages.Length; index++)
                frictionSparkImages[index].color = Color.clear;
        }

        private static float SmoothNoise(float seed, float time)
        {
            return Mathf.PerlinNoise(seed * 1.31f + time * 0.9f, seed * 0.73f + time * 0.42f) * 2f - 1f;
        }

        private static Sprite CreateFrictionStreakSprite()
        {
            const int width = 128;
            const int height = 24;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Generated Dice Arcane Streak",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color transparent = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float horizontal = Mathf.Clamp01(1f - (float)x / (width - 1));
                    float vertical = 1f - Mathf.Abs((y + 0.5f) / height * 2f - 1f);
                    float core = Mathf.Pow(Mathf.SmoothStep(0f, 1f, vertical), 2.2f);
                    float tail = Mathf.Pow(Mathf.SmoothStep(0f, 1f, horizontal), 1.6f);
                    float alpha = core * tail;
                    texture.SetPixel(x, y, alpha > 0.02f ? new Color(1f, 1f, 1f, alpha) : transparent);
                }
            }

            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.08f, 0.5f), height);
        }

        private static Sprite CreateSoftGlowSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Generated Dice Arcane Glow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(u * u + v * v);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.4f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateSparkMoteSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Generated Dice Spark Mote",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(u * u + v * v);
                    float sparkle = Mathf.Pow(Mathf.Clamp01(1f - distance), 5f);
                    float cross = Mathf.Max(
                        Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u) * 7f), 2f),
                        Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v) * 7f), 2f)) * 0.45f;
                    float alpha = Mathf.Clamp01(sparkle + cross) * Mathf.Clamp01(1f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void EnsureDie(int sides, HeroClass heroClass)
        {
            if (die != null && dieSides == sides && dieClass == heroClass)
            {
                die.SetActive(true);
                return;
            }

            if (die != null)
                Destroy(die);
            die = ClassDice3D.Create(sides, heroClass, diePivot);
            dieSides = sides;
            dieClass = heroClass;
            dieFaces.Clear();
            if (die == null)
                return;

            NormalizeDie(die.transform, sides);
            // Le normali vanno riportate nello spazio del pivot (che è ciò che
            // ruotiamo): se l'FBX ha una rotazione di import sulla root, usarle
            // nello spazio del modello inclinerebbe ogni atterraggio.
            dieFaces.Clear();
            foreach (DieFace face in ClassDice3D.GetFaces(die, sides))
            {
                Vector3 pivotNormal = diePivot.InverseTransformDirection(
                    die.transform.TransformDirection(face.Normal)).normalized;
                Vector3 pivotDigitUp = face.DigitUp == Vector3.zero
                    ? Vector3.zero
                    : diePivot.InverseTransformDirection(
                        die.transform.TransformDirection(face.DigitUp)).normalized;
                dieFaces.Add(new DieFace(pivotNormal, face.Value, pivotDigitUp));
            }
            if (dieFaces.Count < sides)
                Debug.LogWarning($"[Accard N' Die] D{sides}: rilevate {dieFaces.Count}/{sides} facce (mesh leggibile? atterraggi possibili storti).");
        }

        private IEnumerator SpiralRollRoutine(int result, float duration)
        {
            renderCamera.enabled = true;
            if (die == null)
                yield break;

            homeAnchoredPosition = viewRect.anchoredPosition;
            homeCaptured = true;

            Quaternion targetRotation = TargetRotationFor(result);
            Transform dieTransform = diePivot;
            dieTransform.localRotation = Random.rotationUniform;

            ComputeBounceLimits();
            Vector2 bounceSpan = bounceMax - bounceMin;
            float horizontalRadius = Mathf.Clamp(bounceSpan.x * 0.46f, 128f, 280f);
            float verticalRadius = Mathf.Clamp(bounceSpan.y * 0.42f, 84f, 190f);
            float spiralTurns = Random.Range(1.55f, 2f);
            float spiralDirection = Random.value < 0.5f ? -1f : 1f;
            float startAngle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 previousOffset = PolarSpiralOffset(
                horizontalRadius,
                verticalRadius,
                startAngle,
                spiralTurns,
                spiralDirection,
                0f);
            bounceOffset = previousOffset;
            viewRect.anchoredPosition = homeAnchoredPosition + bounceOffset;
            bouncing = true;

            float spiralDuration = Mathf.Max(0.2f, duration);
            float elapsed = 0f;
            while (elapsed < spiralDuration)
            {
                float progress = Mathf.Clamp01(elapsed / spiralDuration);
                float eased = SmoothStep5(progress);
                bounceOffset = PolarSpiralOffset(
                    horizontalRadius,
                    verticalRadius,
                    startAngle,
                    spiralTurns,
                    spiralDirection,
                    eased);
                Vector2 delta = bounceOffset - previousOffset;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    Vector3 tangent = new Vector3(delta.x, delta.y, 0f).normalized;
                    Vector3 rollAxis = Vector3.Cross(Vector3.forward, tangent).normalized;
                    float spinDegrees = delta.magnitude * 6.8f + Mathf.Lerp(540f, 95f, eased) * Time.unscaledDeltaTime;
                    dieTransform.localRotation =
                        Quaternion.AngleAxis(spinDegrees, rollAxis) * dieTransform.localRotation;
                    UpdateFrictionSparks(delta, progress, spinDegrees);
                }
                else
                {
                    HideFrictionSparks();
                }

                if (progress > 0.72f)
                {
                    float faceBlend = SmoothStep3(Mathf.InverseLerp(0.72f, 1f, progress));
                    dieTransform.localRotation = Quaternion.Slerp(dieTransform.localRotation, targetRotation, faceBlend * 0.2f);
                }

                viewRect.anchoredPosition = homeAnchoredPosition + bounceOffset;
                previousOffset = bounceOffset;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            HideFrictionSparks();
            Quaternion settleStart = dieTransform.localRotation;
            float settleDuration = Mathf.Max(0.16f, duration * 0.16f);
            elapsed = 0f;
            while (elapsed < settleDuration)
            {
                float progress = Mathf.Clamp01(elapsed / settleDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                dieTransform.localRotation = Quaternion.Slerp(settleStart, targetRotation, eased);
                viewRect.anchoredPosition = homeAnchoredPosition;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            dieTransform.localRotation = targetRotation;
            viewRect.anchoredPosition = homeAnchoredPosition;
            bouncing = false;
            HideFrictionSparks();
            rollCoroutine = null;
        }

        private static Vector2 PolarSpiralOffset(
            float horizontalRadius,
            float verticalRadius,
            float startAngle,
            float turns,
            float direction,
            float progress)
        {
            float t = Mathf.Clamp01(progress);
            float radius = 1f - t;
            float angle = startAngle + direction * turns * Mathf.PI * 2f * t;
            return new Vector2(
                Mathf.Cos(angle) * horizontalRadius * radius,
                Mathf.Sin(angle) * verticalRadius * radius);
        }

        private static float SmoothStep3(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float SmoothStep5(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value * (value * (6f * value - 15f) + 10f);
        }

        private IEnumerator RollRoutine(int result, float duration)
        {
            renderCamera.enabled = true;
            if (die == null)
                yield break;

            // Posizione di riposo del riquadro: catturata una volta e ripristinata
            // sempre, così un tiro interrotto non lascia derive.
            homeAnchoredPosition = viewRect.anchoredPosition;
            homeCaptured = true;
            viewRect.anchoredPosition = homeAnchoredPosition;

            // Si ruota il pivot (centrato sulla mesh), non la root del modello:
            // se la mesh è fuori asse rispetto alla root, ruotarla la farebbe orbitare.
            Quaternion targetRotation = TargetRotationFor(result);
            Transform dieTransform = diePivot;
            dieTransform.localRotation = Random.rotationUniform;

            float tumbleDuration = duration * 0.66f;
            float settleDuration = Mathf.Max(duration - tumbleDuration, 0.01f);

            // Lancio con rimbalzi: il dado parte con una velocità casuale e
            // rimbalza sulle pareti della propria metà campo (bounceArea)
            // perdendo energia per attrito; nell'assestamento una molla lo
            // riporta dolcemente alla posizione di riposo.
            ComputeBounceLimits();
            bounceOffset = Vector2.zero;
            Vector2 bounceSpan = bounceMax - bounceMin;
            float horizontalSpeed = Mathf.Max(250f, Mathf.Max(1f, bounceSpan.x) * 2.45f / Mathf.Max(duration, 0.2f));
            float verticalSpeed = Mathf.Max(120f, Mathf.Max(1f, bounceSpan.y) * 1.28f / Mathf.Max(duration, 0.2f));
            float horizontalSign = Random.value < 0.5f ? -1f : 1f;
            bounceVelocity = new Vector2(
                horizontalSpeed * horizontalSign,
                Random.Range(-verticalSpeed, verticalSpeed));
            bounceCurveSeed = Random.Range(0f, 100f);
            bounceCurveSign = Random.value < 0.5f ? -1f : 1f;
            float friction = 1.05f / Mathf.Max(tumbleDuration, 0.1f);
            bouncing = true;

            // Fase 1: rotolamento libero che decelera.
            Vector3 axis = Random.onUnitSphere;
            float elapsed = 0f;
            while (elapsed < tumbleDuration)
            {
                float progress = elapsed / tumbleDuration;
                float easedProgress = progress * progress * (3f - 2f * progress);
                float speed = Mathf.Lerp(2050f, 520f, easedProgress);
                axis = Vector3.Slerp(axis, Random.onUnitSphere, Time.unscaledDeltaTime * 1.35f).normalized;
                dieTransform.localRotation =
                    Quaternion.AngleAxis(speed * Time.unscaledDeltaTime, axis) * dieTransform.localRotation;
                Vector2 beforeBounce = bounceOffset;
                UpdateBounce(Time.unscaledDeltaTime, friction, 0f);
                UpdateFrictionSparks(bounceOffset - beforeBounce, progress, speed * Time.unscaledDeltaTime);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            HideFrictionSparks();
            // Fase 2: assestamento morbido sulla faccia del risultato.
            Quaternion settleStart = dieTransform.localRotation;
            Vector2 settleStartOffset = bounceOffset;
            Vector2 settleWobble = Random.insideUnitCircle.normalized * Mathf.Min(28f, settleStartOffset.magnitude * 0.32f);
            elapsed = 0f;
            while (elapsed < settleDuration)
            {
                float progress = Mathf.Clamp01(elapsed / settleDuration);
                float eased = progress * progress * progress * (progress * (6f * progress - 15f) + 10f);
                dieTransform.localRotation = Quaternion.Slerp(settleStart, targetRotation, eased);
                float wobble = Mathf.Sin(progress * Mathf.PI * 2.2f) * (1f - eased);
                bounceOffset = Vector2.LerpUnclamped(settleStartOffset, Vector2.zero, eased) + settleWobble * wobble;
                viewRect.anchoredPosition = homeAnchoredPosition + bounceOffset;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            dieTransform.localRotation = targetRotation;
            viewRect.anchoredPosition = homeAnchoredPosition;
            bouncing = false;
            HideFrictionSparks();
            rollCoroutine = null;
        }

        // Limiti dell'offset (rispetto alla posizione di riposo) perché il
        // dado resti dentro bounceArea. Senza area impostata, o se l'area è
        // più piccola del dado, si ripiega su una piccola orbita locale.
        private void ComputeBounceLimits()
        {
            bounceMin = new Vector2(-48f, -48f);
            bounceMax = new Vector2(48f, 48f);
            if (bounceArea == null || !(viewRect.parent is RectTransform parent))
                return;

            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            bounceArea.GetWorldCorners(corners);
            Vector2 areaMin = parent.InverseTransformPoint(corners[0]);
            Vector2 areaMax = parent.InverseTransformPoint(corners[2]);
            viewRect.GetWorldCorners(corners);
            Vector2 dieMin = parent.InverseTransformPoint(corners[0]);
            Vector2 dieMax = parent.InverseTransformPoint(corners[2]);
            if (dieMax.x - dieMin.x < 1f)
                return;

            const float margin = 4f;
            Vector2 minimum = areaMin - dieMin + Vector2.one * margin;
            Vector2 maximum = areaMax - dieMax - Vector2.one * margin;
            if (maximum.x < minimum.x || maximum.y < minimum.y)
                return;
            bounceMin = minimum;
            bounceMax = maximum;
        }

        // Un passo di simulazione: attrito (o molla verso casa in fase di
        // assestamento), rimbalzo sulle pareti e urto col dado gemello.
        private void UpdateBounce(float deltaTime, float friction, float settleDuration)
        {
            if (deltaTime <= 0f)
                return;

            if (settleDuration > 0f)
            {
                // Molla criticamente smorzata: converge alla posizione di
                // riposo senza oscillare, come un dado che scivola fermandosi.
                float omega = 8f / settleDuration;
                bounceVelocity += (-(omega * omega) * bounceOffset - 2f * omega * bounceVelocity) * deltaTime;
            }
            else
            {
                Vector2 radial = bounceOffset.sqrMagnitude > 25f ? bounceOffset.normalized : bounceVelocity.normalized;
                Vector2 tangent = new Vector2(-radial.y, radial.x);
                if (tangent.sqrMagnitude > 0.001f)
                {
                    float curve = Mathf.Sin(Time.unscaledTime * 6.1f + bounceCurveSeed)
                        + 0.45f * Mathf.Sin(Time.unscaledTime * 11.7f + bounceCurveSeed * 1.37f);
                    bounceVelocity += tangent.normalized * (bounceCurveSign * curve * 520f * deltaTime);
                }
                bounceVelocity *= Mathf.Exp(-friction * deltaTime);
            }
            bounceOffset += bounceVelocity * deltaTime;

            const float restitution = 0.68f;
            if (bounceOffset.x < bounceMin.x)
            {
                bounceOffset.x = bounceMin.x;
                bounceVelocity.x = Mathf.Abs(bounceVelocity.x) * restitution;
            }
            else if (bounceOffset.x > bounceMax.x)
            {
                bounceOffset.x = bounceMax.x;
                bounceVelocity.x = -Mathf.Abs(bounceVelocity.x) * restitution;
            }
            if (bounceOffset.y < bounceMin.y)
            {
                bounceOffset.y = bounceMin.y;
                bounceVelocity.y = Mathf.Abs(bounceVelocity.y) * restitution;
            }
            else if (bounceOffset.y > bounceMax.y)
            {
                bounceOffset.y = bounceMax.y;
                bounceVelocity.y = -Mathf.Abs(bounceVelocity.y) * restitution;
            }

            ResolvePartnerCollision();
            viewRect.anchoredPosition = homeAnchoredPosition + bounceOffset;
        }

        // Urto elastico col dado gemello (masse uguali): separa i riquadri e
        // scambia le componenti di velocità lungo la congiungente dei centri.
        private void ResolvePartnerCollision()
        {
            Dice3DRollView other = bouncePartner;
            if (other == null || !bouncing || !other.bouncing || !(viewRect.parent is RectTransform parent))
                return;

            Vector2 delta = parent.InverseTransformVector(viewRect.position - other.viewRect.position);
            float minDistance = (viewRect.rect.width + other.viewRect.rect.width) * 0.5f * 0.92f;
            float distance = delta.magnitude;
            if (distance <= 0.001f || distance >= minDistance)
                return;

            Vector2 normal = delta / distance;
            float push = (minDistance - distance) * 0.5f;
            NudgeBy(normal * push);
            other.NudgeBy(-normal * push);

            float approachMine = Vector2.Dot(bounceVelocity, normal);
            float approachTheirs = Vector2.Dot(other.bounceVelocity, normal);
            if (approachMine - approachTheirs < 0f)
            {
                bounceVelocity += (approachTheirs - approachMine) * normal;
                other.bounceVelocity += (approachMine - approachTheirs) * normal;
            }
        }

        private void NudgeBy(Vector2 delta)
        {
            bounceOffset = new Vector2(
                Mathf.Clamp(bounceOffset.x + delta.x, bounceMin.x, bounceMax.x),
                Mathf.Clamp(bounceOffset.y + delta.y, bounceMin.y, bounceMax.y));
            viewRect.anchoredPosition = homeAnchoredPosition + bounceOffset;
        }

        // Rotazione che porta la faccia con il valore richiesto a guardare
        // dritta verso la camera del rig, così il risultato si legge frontale.
        // La torsione attorno all'asse di vista resta libera: si sceglie quella
        // che porta una delle altre facce il più possibile verso il basso, come
        // un dado appoggiato sul tavolo.
        private Quaternion TargetRotationFor(int result)
        {
            Vector3 faceNormal = Vector3.up;
            Vector3 digitUp = Vector3.zero;
            bool found = false;
            foreach (DieFace face in dieFaces)
            {
                if (face.Value == result)
                {
                    faceNormal = face.Normal;
                    digitUp = face.DigitUp;
                    found = true;
                    break;
                }
            }
            if (!found)
                Debug.LogWarning($"[Accard N' Die] D{dieSides}: nessuna faccia con valore {result}, atterraggio non scriptato.");

            Vector3 toCamera = renderCamera.transform.localPosition.normalized;

            // D4 a lettura di vertice: il valore di una faccia è il numero del
            // vertice opposto, quindi il dado atterra appoggiato su quella
            // faccia (normale in giù) e l'apice mostra il risultato. La
            // torsione gira una delle altre facce verso la camera.
            if (dieSides == 4)
            {
                Quaternion restingRotation = Quaternion.FromToRotation(faceNormal, Vector3.down);
                float bestRestAngle = 0f;
                float bestFacing = float.MinValue;
                for (int angle = 0; angle < 360; angle += 3)
                {
                    Quaternion candidate = Quaternion.AngleAxis(angle, Vector3.up) * restingRotation;
                    float facing = float.MinValue;
                    foreach (DieFace face in dieFaces)
                    {
                        Vector3 rotated = candidate * face.Normal;
                        if (Vector3.Dot(rotated, Vector3.down) > 0.9f)
                            continue;
                        if (Vector3.Dot(rotated, toCamera) > facing)
                            facing = Vector3.Dot(rotated, toCamera);
                    }
                    if (facing > bestFacing)
                    {
                        bestFacing = facing;
                        bestRestAngle = angle;
                    }
                }
                return Quaternion.AngleAxis(bestRestAngle, Vector3.up) * restingRotation;
            }

            Quaternion baseRotation = Quaternion.FromToRotation(faceNormal, toCamera);

            // Con l'orientamento del glifo calibrato la torsione è esatta:
            // il numero appare dritto rispetto alla camera.
            if (digitUp != Vector3.zero)
            {
                Vector3 current = Vector3.ProjectOnPlane(baseRotation * digitUp, toCamera);
                Vector3 desired = Vector3.ProjectOnPlane(renderCamera.transform.up, toCamera);
                if (current.sqrMagnitude > 1e-6f && desired.sqrMagnitude > 1e-6f)
                {
                    float twist = Vector3.SignedAngle(current, desired, toCamera);
                    return Quaternion.AngleAxis(twist, toCamera) * baseRotation;
                }
            }

            float bestAngle = 0f;
            float bestDownAlignment = float.MinValue;
            for (int angle = 0; angle < 360; angle += 3)
            {
                Quaternion candidate = Quaternion.AngleAxis(angle, toCamera) * baseRotation;
                float downAlignment = float.MinValue;
                foreach (DieFace face in dieFaces)
                {
                    Vector3 rotated = candidate * face.Normal;
                    if (Vector3.Dot(rotated, toCamera) > 0.9f)
                        continue; // la faccia del risultato
                    if (-rotated.y > downAlignment)
                        downAlignment = -rotated.y;
                }
                if (downAlignment > bestDownAlignment)
                {
                    bestDownAlignment = downAlignment;
                    bestAngle = angle;
                }
            }
            return Quaternion.AngleAxis(bestAngle, toCamera) * baseRotation;
        }

        // Centra il dado sul pivot e lo scala perché la sua sfera di ingombro
        // abbia raggio 0.5: a differenza della scatola, la sfera non cambia con
        // la rotazione, quindi il dado non può mai sporgere dall'inquadratura.
        private static void NormalizeDie(Transform target, int sides)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float radius = 0f;
            foreach (MeshFilter meshFilter in target.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null || !meshFilter.sharedMesh.isReadable)
                {
                    radius = Mathf.Max(radius, bounds.extents.magnitude);
                    continue;
                }
                foreach (Vector3 vertex in meshFilter.sharedMesh.vertices)
                {
                    Vector3 world = meshFilter.transform.TransformPoint(vertex);
                    radius = Mathf.Max(radius, (world - bounds.center).magnitude);
                }
            }
            if (radius > 0.0001f)
            {
                float targetRadius = sides == 4 ? 0.56f : 0.5f;
                target.localScale *= targetRadius / radius;
            }

            Renderer[] scaled = target.GetComponentsInChildren<Renderer>(true);
            Bounds scaledBounds = scaled[0].bounds;
            for (int i = 1; i < scaled.Length; i++)
                scaledBounds.Encapsulate(scaled[i].bounds);
            target.position += target.parent.position - scaledBounds.center;
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
