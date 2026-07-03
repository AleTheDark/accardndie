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
        private Vector3 homeLocalPosition;

        /// <summary>Esiste il modello 3D per questo numero di facce?</summary>
        public static bool IsSupported(int sides)
        {
            return Resources.Load<GameObject>($"Dice/D{ResolveSides(sides)}") != null;
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
            view.image.raycastTarget = false;
            view.image.color = Color.white;

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
            renderCamera.transform.localPosition = new Vector3(0f, 0f, -CameraDistance);
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            renderCamera.orthographic = true;
            renderCamera.orthographicSize = 0.52f;
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
            if (rollCoroutine != null)
                StopCoroutine(rollCoroutine);
            rollCoroutine = StartCoroutine(RollRoutine(result, duration));
        }

        public void Hide()
        {
            if (rollCoroutine != null)
            {
                StopCoroutine(rollCoroutine);
                rollCoroutine = null;
            }
            if (homeCaptured)
                transform.localPosition = homeLocalPosition;
            renderCamera.enabled = false;
            gameObject.SetActive(false);
        }

        private void EnsureDie(int sides, HeroClass heroClass)
        {
            if (die != null && dieSides == sides && dieClass == heroClass)
                return;

            if (die != null)
                Destroy(die);
            die = ClassDice3D.Create(sides, heroClass, diePivot);
            dieSides = sides;
            dieClass = heroClass;
            dieFaces.Clear();
            if (die == null)
                return;

            NormalizeDie(die.transform);
            // Le normali vanno riportate nello spazio del pivot (che è ciò che
            // ruotiamo): se l'FBX ha una rotazione di import sulla root, usarle
            // nello spazio del modello inclinerebbe ogni atterraggio.
            dieFaces.Clear();
            foreach (DieFace face in ClassDice3D.GetFaces(die, sides))
            {
                Vector3 pivotNormal = diePivot.InverseTransformDirection(
                    die.transform.TransformDirection(face.Normal)).normalized;
                dieFaces.Add(new DieFace(pivotNormal, face.Value));
            }
            if (dieFaces.Count < sides)
                Debug.LogWarning($"[Accard N' Die] D{sides}: rilevate {dieFaces.Count}/{sides} facce (mesh leggibile? atterraggi possibili storti).");
        }

        private IEnumerator RollRoutine(int result, float duration)
        {
            renderCamera.enabled = true;
            if (die == null)
                yield break;

            // Posizione di riposo del riquadro: catturata una volta e ripristinata
            // sempre, così un tiro interrotto non lascia derive.
            if (!homeCaptured)
            {
                homeLocalPosition = transform.localPosition;
                homeCaptured = true;
            }
            transform.localPosition = homeLocalPosition;

            // Si ruota il pivot (centrato sulla mesh), non la root del modello:
            // se la mesh è fuori asse rispetto alla root, ruotarla la farebbe orbitare.
            Quaternion targetRotation = TargetRotationFor(result);
            Transform dieTransform = diePivot;
            dieTransform.localRotation = Random.rotationUniform;

            float tumbleDuration = duration * 0.7f;
            float settleDuration = Mathf.Max(duration - tumbleDuration, 0.01f);

            // Orbita pseudo-casuale del riquadro: parte ampia e si restringe
            // fino a fermarsi al centro insieme al dado.
            float orbitRadius = ((RectTransform)transform).rect.width * 0.24f;
            float orbitPhase = Random.Range(0f, Mathf.PI * 2f);
            float orbitSpeed = Random.Range(5.5f, 8.5f) * (Random.value < 0.5f ? -1f : 1f);
            float orbitNoise = Random.Range(0f, 100f);

            // Fase 1: rotolamento libero che decelera.
            Vector3 axis = Random.onUnitSphere;
            float elapsed = 0f;
            while (elapsed < tumbleDuration)
            {
                float progress = elapsed / tumbleDuration;
                float speed = Mathf.Lerp(2200f, 420f, progress * progress);
                axis = Vector3.Slerp(axis, Random.onUnitSphere, Time.unscaledDeltaTime * 2.2f).normalized;
                dieTransform.localRotation =
                    Quaternion.AngleAxis(speed * Time.unscaledDeltaTime, axis) * dieTransform.localRotation;
                ApplyOrbit(elapsed / duration, orbitRadius, orbitPhase, orbitSpeed, orbitNoise);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Fase 2: assestamento morbido sulla faccia del risultato.
            Quaternion settleStart = dieTransform.localRotation;
            elapsed = 0f;
            while (elapsed < settleDuration)
            {
                float progress = Mathf.Clamp01(elapsed / settleDuration);
                float eased = 1f - (1f - progress) * (1f - progress) * (1f - progress);
                dieTransform.localRotation = Quaternion.Slerp(settleStart, targetRotation, eased);
                ApplyOrbit((tumbleDuration + elapsed) / duration, orbitRadius, orbitPhase, orbitSpeed, orbitNoise);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            dieTransform.localRotation = targetRotation;
            transform.localPosition = homeLocalPosition;
            rollCoroutine = null;
        }

        // Sposta il riquadro lungo un cerchio irregolare il cui raggio si annulla
        // dolcemente a fine tiro, così il dado torna esattamente al centro.
        private void ApplyOrbit(float normalizedTime, float radius, float phase, float speed, float noise)
        {
            float t = Mathf.Clamp01(normalizedTime);
            float shrink = (1f - t) * (1f - t);
            float angle = phase + t * speed;
            float wobble = 0.75f + 0.25f * Mathf.PerlinNoise(noise, t * 3f);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * shrink * wobble);
            transform.localPosition = homeLocalPosition + (Vector3)offset;
        }

        // Rotazione che porta la faccia con il valore richiesto a guardare la
        // camera (che osserva il dado lungo +Z, quindi la normale va su -Z),
        // scegliendo la torsione che lascia il dado "dritto" sullo schermo.
        private Quaternion TargetRotationFor(int result)
        {
            Vector3 faceNormal = Vector3.back;
            bool found = false;
            foreach (DieFace face in dieFaces)
            {
                if (face.Value == result)
                {
                    faceNormal = face.Normal;
                    found = true;
                    break;
                }
            }
            if (!found)
                Debug.LogWarning($"[Accard N' Die] D{dieSides}: nessuna faccia con valore {result}, atterraggio non scriptato.");
            Quaternion baseRotation = Quaternion.FromToRotation(faceNormal, Vector3.back);

            // FromToRotation lascia una torsione arbitraria attorno all'asse di
            // vista: proviamo le torsioni possibili e teniamo quella che porta
            // una delle facce vicine il più possibile verso il basso. La faccia
            // frontale resta così con uno spigolo orizzontale alla base, come un
            // dado appoggiato: il D4 punta in su, il D6 resta dritto.
            float bestAngle = 0f;
            float bestDownAlignment = float.MinValue;
            for (int angle = 0; angle < 360; angle += 3)
            {
                Quaternion candidate = Quaternion.AngleAxis(angle, Vector3.back) * baseRotation;
                float downAlignment = float.MinValue;
                foreach (DieFace face in dieFaces)
                {
                    Vector3 rotated = candidate * face.Normal;
                    if (rotated.z < -0.9f)
                        continue;
                    if (-rotated.y > downAlignment)
                        downAlignment = -rotated.y;
                }
                if (downAlignment > bestDownAlignment)
                {
                    bestDownAlignment = downAlignment;
                    bestAngle = angle;
                }
            }
            return Quaternion.AngleAxis(bestAngle, Vector3.back) * baseRotation;
        }

        // Centra il dado sul pivot e lo scala perché la sua sfera di ingombro
        // abbia raggio 0.5: a differenza della scatola, la sfera non cambia con
        // la rotazione, quindi il dado non può mai sporgere dall'inquadratura.
        private static void NormalizeDie(Transform target)
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
                target.localScale *= 0.5f / radius;

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
