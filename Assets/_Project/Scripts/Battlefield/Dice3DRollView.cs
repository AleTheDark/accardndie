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
        private Outline arcaneShadowOutline;
        private Outline arcaneClassOutline;
        private ArcaneDiceTrailGraphic arcaneTrail;
        private RectTransform arcaneTrailArea;
        private Color arcaneGlowColor = new Color(0.2f, 0.5f, 1f);
        private RenderTexture renderTexture;
        private GameObject renderRoot;
        private Transform diePivot;
        private Camera renderCamera;
        private GameObject die;
        private EmpoweredDiceFireVfx empoweredFire;
        private bool empowered;
        private int dieSides = -1;
        private HeroClass dieClass;
        private List<DieFace> dieFaces = new List<DieFace>();
        private readonly List<MeshFilter> crumbleSourceFilters = new List<MeshFilter>();
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
        private readonly List<MeshCrumbleFragment> activeCrumbleFragments = new List<MeshCrumbleFragment>();

        /// <summary>Esiste il modello 3D per questo numero di facce?</summary>
        public static bool IsSupported(int sides)
        {
            return Resources.Load<GameObject>($"DnD_Dice/Mesh/00_D{ResolveSides(sides)}") != null;
        }

        // Il D2 logico usa il modello D4: il risultato deciso dalla logica resta 1 o 2.
        private static int ResolveSides(int sides)
        {
            return sides == 2 ? 4 : sides;
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
            view.CreateArcaneDieAura();

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
            // 24 bit garantiscono depth + stencil: la stencil protegge il valore
            // finale dalle particelle senza spegnere guscio, luce o pulsazione.
            renderTexture = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32)
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
            empoweredFire = EmpoweredDiceFireVfx.Create(diePivot);
        }

        public void SetEmpowered(bool value)
        {
            // L'aura non viene toccata qui: LateUpdate la ridipinge ogni frame
            // e leggera' 'empowered' da se'. Scriverla adesso significherebbe
            // vederla sovrascritta prima ancora del frame successivo.
            empowered = value;
            empoweredFire?.SetActive(value);
        }

        /// <summary>
        /// Avvia il tiro scriptato: il dado rotola e si ferma con la faccia
        /// che mostra <paramref name="result"/> rivolta verso la camera.
        /// </summary>
        public void StartScriptedRoll(int sides, HeroClass heroClass, int result, float duration)
        {
            gameObject.SetActive(true);
            SetArcaneOutlinesVisible(true);
            EnsureDie(ResolveSides(sides), heroClass);
            empoweredFire?.ConfigureForClass(heroClass);
            if (die != null)
                die.SetActive(true);
            ConfigureArcaneMagic(heroClass);
            SetEmpowered(empowered);
            empoweredFire?.SetSettled(false);
            if (rollCoroutine != null)
                StopCoroutine(rollCoroutine);
            // Un dado che rotola e rimbalza e' esattamente il momento in cui i
            // sessanta frame si vedono: il tiro se li prende, Hide() li ridà.
            FrameRateGovernor.Acquire(this);
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
            {
                // Il glow contestuale ridipinge il dado, non il fuoco: guscio e
                // maschera tengono i propri material.
                if (renderer.GetComponent<EmpoweredDiceVfxPart>() != null)
                    continue;
                renderer.sharedMaterial = material;
            }

            // Anche aura e trail devono rispettare gli override contestuali,
            // ad esempio blu giocatore e rosso nemico durante l'iniziativa.
            arcaneGlowColor = glow;
            if (arcaneClassOutline != null)
                arcaneClassOutline.effectColor = new Color(glow.r, glow.g, glow.b, 0.9f);
            if (arcaneTrail != null)
                arcaneTrail.Configure(glow, viewRect);
        }

        /// <summary>
        /// Area (in genere l'intera metà campo) sulle cui pareti il dado
        /// rimbalza durante il tiro, e l'eventuale dado gemello con cui urtare.
        /// </summary>
        public void SetBounceArea(RectTransform area, Dice3DRollView partner)
        {
            bounceArea = area;
            bouncePartner = partner;
            EnsureArcaneTrail();
        }

        public void Hide()
        {
            FrameRateGovernor.Release(this);
            if (rollCoroutine != null)
            {
                StopCoroutine(rollCoroutine);
                rollCoroutine = null;
            }
            bouncing = false;
            CleanupMeshCrumbleFragments();
            if (homeCaptured)
                viewRect.anchoredPosition = homeAnchoredPosition;
            if (die != null)
                die.SetActive(true);
            if (arcaneTrail != null)
                arcaneTrail.ClearTrail();
            else
                arcaneTrail = null;
            renderCamera.enabled = false;
            if (renderTexture != null)
                ClearRenderTexture(renderTexture);
            gameObject.SetActive(false);

            // La vista viene riutilizzata tra tiri diversi. Spegnere soltanto
            // il GameObject del fuoco lasciava il flag `empowered` latente:
            // un successivo StartScriptedRoll che non configura esplicitamente
            // l'Empower (per esempio alcuni tiri contestuali) poteva riaccenderlo.
            SetEmpowered(false);
        }

        /// <summary>
        /// Frantuma la mesh realmente renderizzata dal dado. I triangoli del modello
        /// vengono divisi in blocchi spaziali che conservano materiali e prospettiva 3D.
        /// </summary>
        public IEnumerator PlayMeshCrumble()
        {
            if (die == null)
                yield break;

            // Gli Outline UI duplicano l'intera RenderTexture. Quando il dado e'
            // diviso in frammenti, ogni copia offset sembra quindi uno strato
            // aggiuntivo della mesh invece di una semplice aura sul dado intero.
            SetArcaneOutlinesVisible(false);

            // Il dado scartato da vantaggio/svantaggio deve tornare al suo aspetto
            // normale prima di sgretolarsi: l'Empower riguarda il tiro, non i
            // frammenti del dado eliminato.
            SetEmpowered(false);

            if (rollCoroutine != null)
            {
                StopCoroutine(rollCoroutine);
                rollCoroutine = null;
            }
            bouncing = false;
            renderCamera.enabled = true;

            CleanupMeshCrumbleFragments();
            List<MeshCrumbleFragment> fragments = BuildMeshCrumbleFragments();
            activeCrumbleFragments.AddRange(fragments);
            if (fragments.Count == 0)
            {
                die.SetActive(false);
                yield return new WaitForSecondsRealtime(0.68f);
                yield break;
            }

            const float fractureDelay = 0.16f;
            float elapsed = 0f;
            Quaternion settledRotation = diePivot.localRotation;
            while (elapsed < fractureDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fractureDelay);
                float shake = Mathf.Sin(progress * Mathf.PI * 10f) * (1f - progress) * 2.8f;
                diePivot.localRotation = settledRotation * Quaternion.Euler(shake, -shake * 0.6f, shake * 0.45f);
                yield return null;
            }
            diePivot.localRotation = settledRotation;
            die.SetActive(false);
            foreach (MeshCrumbleFragment fragment in fragments)
                fragment.Root.SetActive(true);

            const float crumbleDuration = 0.52f;
            elapsed = 0f;
            while (elapsed < crumbleDuration)
            {
                float delta = Time.unscaledDeltaTime;
                elapsed += delta;
                float progress = Mathf.Clamp01(elapsed / crumbleDuration);
                foreach (MeshCrumbleFragment fragment in fragments)
                {
                    // Hide() puo' arrivare a meta' sgretolamento (uscita dalla
                    // battaglia, tiro annullato) e distruggere i frammenti: la
                    // coroutine vive sulla carta, non su questa vista, e non si
                    // ferma da sola.
                    if (fragment.Root == null)
                        continue;
                    fragment.Velocity += Vector3.down * (2.8f * delta);
                    fragment.Root.transform.position += fragment.Velocity * delta;
                    fragment.Root.transform.rotation = Quaternion.AngleAxis(
                        fragment.SpinSpeed * delta,
                        fragment.SpinAxis) * fragment.Root.transform.rotation;
                    float scale = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 1f, progress));
                    fragment.Root.transform.localScale = fragment.InitialScale * scale;
                }
                yield return null;
            }

            foreach (MeshCrumbleFragment fragment in fragments)
            {
                if (fragment.Mesh != null)
                    Destroy(fragment.Mesh);
                if (fragment.Root != null)
                    Destroy(fragment.Root);
            }
            activeCrumbleFragments.Clear();
        }

        private void CleanupMeshCrumbleFragments()
        {
            foreach (MeshCrumbleFragment fragment in activeCrumbleFragments)
            {
                if (fragment.Mesh != null)
                    Destroy(fragment.Mesh);
                if (fragment.Root != null)
                    Destroy(fragment.Root);
            }
            activeCrumbleFragments.Clear();
        }

        private List<MeshCrumbleFragment> BuildMeshCrumbleFragments()
        {
            var output = new List<MeshCrumbleFragment>();
            foreach (MeshFilter sourceFilter in crumbleSourceFilters)
            {
                // Le sorgenti vengono catturate prima che l'Empower aggiunga i
                // propri duplicati alla gerarchia. In questo modo lo
                // sgretolamento non dipende affatto dallo stato o dalla
                // struttura interna del VFX.
                if (sourceFilter == null)
                    continue;

                Mesh sourceMesh = sourceFilter.sharedMesh;
                MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
                if (sourceMesh == null || sourceRenderer == null || !sourceMesh.isReadable)
                    continue;

                Vector3[] sourceVertices = sourceMesh.vertices;
                Bounds bounds = sourceMesh.bounds;
                Vector3 size = bounds.size;
                var cells = new Dictionary<int, List<int>[]>();
                for (int subMesh = 0; subMesh < sourceMesh.subMeshCount; subMesh++)
                {
                    int[] triangles = sourceMesh.GetTriangles(subMesh);
                    for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
                    {
                        Vector3 center = (sourceVertices[triangles[triangle]]
                            + sourceVertices[triangles[triangle + 1]]
                            + sourceVertices[triangles[triangle + 2]]) / 3f;
                        int x = SpatialCell(center.x, bounds.min.x, size.x);
                        int y = SpatialCell(center.y, bounds.min.y, size.y);
                        int z = SpatialCell(center.z, bounds.min.z, size.z);
                        int key = x + y * 3 + z * 9;
                        if (!cells.TryGetValue(key, out List<int>[] cellSubMeshes))
                        {
                            cellSubMeshes = new List<int>[sourceMesh.subMeshCount];
                            for (int index = 0; index < cellSubMeshes.Length; index++)
                                cellSubMeshes[index] = new List<int>();
                            cells.Add(key, cellSubMeshes);
                        }
                        cellSubMeshes[subMesh].Add(triangles[triangle]);
                        cellSubMeshes[subMesh].Add(triangles[triangle + 1]);
                        cellSubMeshes[subMesh].Add(triangles[triangle + 2]);
                    }
                }

                foreach (KeyValuePair<int, List<int>[]> cell in cells)
                {
                    Mesh fragmentMesh = CopyMeshTriangles(sourceMesh, cell.Value);
                    if (fragmentMesh == null)
                        continue;

                    // Ogni pezzo deve ruotare attorno al proprio baricentro. Se
                    // conservasse le coordinate della mesh sorgente orbiterebbe
                    // invece attorno al centro del dado, producendo l'effetto a
                    // "petali" che si vede durante lo sgretolamento.
                    Vector3 sourceLocalCenter = fragmentMesh.bounds.center;
                    RecenterMesh(fragmentMesh, sourceLocalCenter);

                    var fragmentObject = new GameObject("Die Mesh Fragment", typeof(MeshFilter), typeof(MeshRenderer));
                    fragmentObject.transform.SetParent(diePivot, false);
                    fragmentObject.transform.position = sourceFilter.transform.TransformPoint(sourceLocalCenter);
                    fragmentObject.transform.rotation = sourceFilter.transform.rotation;
                    fragmentObject.transform.localScale = WorldScaleRelativeTo(sourceFilter.transform, diePivot);
                    fragmentObject.GetComponent<MeshFilter>().sharedMesh = fragmentMesh;
                    fragmentObject.GetComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;

                    Vector3 directionFromDie = fragmentObject.transform.position - diePivot.position;
                    Vector3 direction = directionFromDie.sqrMagnitude > 0.0001f
                        ? directionFromDie.normalized
                        : Random.onUnitSphere;
                    output.Add(new MeshCrumbleFragment
                    {
                        Root = fragmentObject,
                        Mesh = fragmentMesh,
                        InitialScale = fragmentObject.transform.localScale,
                        Velocity = direction * Random.Range(0.65f, 1.25f) + Vector3.up * Random.Range(0.05f, 0.45f),
                        SpinAxis = Random.onUnitSphere,
                        SpinSpeed = Random.Range(150f, 420f)
                    });
                    fragmentObject.SetActive(false);
                }
            }
            return output;
        }

        private static void RecenterMesh(Mesh mesh, Vector3 center)
        {
            Vector3[] vertices = mesh.vertices;
            for (int index = 0; index < vertices.Length; index++)
                vertices[index] -= center;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private static Vector3 WorldScaleRelativeTo(Transform source, Transform parent)
        {
            Vector3 sourceScale = source.lossyScale;
            Vector3 parentScale = parent.lossyScale;
            return new Vector3(
                SafeScaleRatio(sourceScale.x, parentScale.x),
                SafeScaleRatio(sourceScale.y, parentScale.y),
                SafeScaleRatio(sourceScale.z, parentScale.z));
        }

        private static float SafeScaleRatio(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
        }

        private static int SpatialCell(float value, float minimum, float size)
        {
            if (size <= 0.0001f)
                return 1;
            return Mathf.Clamp(Mathf.FloorToInt((value - minimum) / size * 3f), 0, 2);
        }

        private static Mesh CopyMeshTriangles(Mesh source, List<int>[] sourceTriangles)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uv = source.uv;
            var copiedVertices = new List<Vector3>();
            var copiedNormals = new List<Vector3>();
            var copiedUv = new List<Vector2>();
            var copiedTriangles = new List<int>[sourceTriangles.Length];
            for (int subMesh = 0; subMesh < sourceTriangles.Length; subMesh++)
            {
                copiedTriangles[subMesh] = new List<int>();
                foreach (int sourceIndex in sourceTriangles[subMesh])
                {
                    copiedTriangles[subMesh].Add(copiedVertices.Count);
                    copiedVertices.Add(vertices[sourceIndex]);
                    copiedNormals.Add(normals.Length == vertices.Length ? normals[sourceIndex] : Vector3.up);
                    copiedUv.Add(uv.Length == vertices.Length ? uv[sourceIndex] : Vector2.zero);
                }
            }
            if (copiedVertices.Count == 0)
                return null;

            var mesh = new Mesh { name = source.name + " Crumble Fragment" };
            mesh.SetVertices(copiedVertices);
            mesh.SetNormals(copiedNormals);
            mesh.SetUVs(0, copiedUv);
            mesh.subMeshCount = copiedTriangles.Length;
            for (int subMesh = 0; subMesh < copiedTriangles.Length; subMesh++)
                mesh.SetTriangles(copiedTriangles[subMesh], subMesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        private sealed class MeshCrumbleFragment
        {
            public GameObject Root;
            public Mesh Mesh;
            public Vector3 InitialScale;
            public Vector3 Velocity;
            public Vector3 SpinAxis;
            public float SpinSpeed;
        }

        private void ConfigureArcaneMagic(HeroClass heroClass)
        {
            Color glow = ArcaneClassColor(heroClass);
            arcaneGlowColor = glow;
            arcaneClassOutline.effectColor = new Color(glow.r, glow.g, glow.b, 0.9f);
            arcaneShadowOutline.effectColor = new Color(0f, 0f, 0f, 0.94f);
            if (arcaneTrail != null)
                arcaneTrail.Configure(glow, viewRect);
        }

        private void CreateArcaneDieAura()
        {
            arcaneShadowOutline = gameObject.AddComponent<Outline>();
            arcaneShadowOutline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            arcaneShadowOutline.effectDistance = new Vector2(10f, -10f);
            arcaneShadowOutline.useGraphicAlpha = true;

            arcaneClassOutline = gameObject.AddComponent<Outline>();
            arcaneClassOutline.effectColor = new Color(0.2f, 0.5f, 1f, 0.86f);
            arcaneClassOutline.effectDistance = new Vector2(-5f, 5f);
            arcaneClassOutline.useGraphicAlpha = true;
        }

        private void SetArcaneOutlinesVisible(bool visible)
        {
            if (arcaneShadowOutline != null)
                arcaneShadowOutline.enabled = visible;
            if (arcaneClassOutline != null)
                arcaneClassOutline.enabled = visible;
        }

        private void LateUpdate()
        {
            if (arcaneShadowOutline == null || !gameObject.activeInHierarchy)
                return;

            float time = Time.unscaledTime;
            float distortionA = Mathf.Sin(time * 8.7f + GetInstanceID() * 0.013f);
            float distortionB = Mathf.Sin(time * 13.1f + 1.9f);
            arcaneShadowOutline.effectDistance = new Vector2(9.5f + distortionA * 2.8f, -9.5f + distortionB * 2.4f);
            arcaneClassOutline.effectDistance = new Vector2(-4.8f + distortionB * 1.9f, 4.8f + distortionA * 1.7f);
            arcaneShadowOutline.effectColor = new Color(0.005f, 0.008f, 0.018f, 0.86f + distortionB * 0.07f);
            float shimmer = 0.78f + (distortionA + 1f) * 0.08f;
            if (empowered)
                shimmer = Mathf.Min(1f, shimmer + 0.16f);
            arcaneClassOutline.effectColor = new Color(
                Mathf.Lerp(arcaneGlowColor.r, 1f, 0.08f + Mathf.Max(0f, distortionB) * 0.1f),
                Mathf.Lerp(arcaneGlowColor.g, 1f, 0.08f + Mathf.Max(0f, distortionB) * 0.1f),
                Mathf.Lerp(arcaneGlowColor.b, 1f, 0.08f + Mathf.Max(0f, distortionB) * 0.1f),
                shimmer);
        }

        private void EnsureArcaneTrail()
        {
            if (bounceArea == null)
                return;
            if (arcaneTrail != null && arcaneTrailArea == bounceArea)
                return;
            if (arcaneTrail != null)
                Destroy(arcaneTrail.gameObject);
            arcaneTrail = null;
            arcaneTrailArea = null;

            GameObject trailObject = new GameObject(
                "3D Die Arcane Motion Trail",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(ArcaneDiceTrailGraphic));
            RectTransform trailRect = (RectTransform)trailObject.transform;
            trailRect.SetParent(bounceArea, false);
            trailRect.anchorMin = Vector2.zero;
            trailRect.anchorMax = Vector2.one;
            trailRect.offsetMin = Vector2.zero;
            trailRect.offsetMax = Vector2.zero;
            trailRect.SetAsFirstSibling();
            arcaneTrail = trailObject.GetComponent<ArcaneDiceTrailGraphic>();
            arcaneTrailArea = bounceArea;
            arcaneTrail.raycastTarget = false;
            arcaneTrail.Configure(ArcaneClassColor(dieClass), viewRect);
        }

        private static Color ArcaneClassColor(HeroClass heroClass)
        {
            return ClassDice3D.GlowColor(heroClass);
        }

        private static void ClearRenderTexture(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;
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
            crumbleSourceFilters.Clear();
            if (die == null)
                return;

            NormalizeDie(die.transform, sides);
            crumbleSourceFilters.AddRange(die.GetComponentsInChildren<MeshFilter>(true));
            empoweredFire?.BindDie(die);
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
            // Inizia a campionare soltanto dopo il salto al punto iniziale:
            // evita il segmento dritto dalla posizione di riposo alla spirale.
            if (arcaneTrail != null)
                arcaneTrail.Begin();
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
            if (arcaneTrail != null)
                arcaneTrail.StopEmission();
            if (empowered)
                empoweredFire?.SetSettled(true);
            rollCoroutine = null;
            FrameRateGovernor.Release(this);
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
            if (arcaneTrail != null)
                arcaneTrail.Begin();

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
                UpdateBounce(Time.unscaledDeltaTime, friction, 0f);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

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
            rollCoroutine = null;
            FrameRateGovernor.Release(this);
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
            FrameRateGovernor.Release(this);
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            if (renderRoot != null)
                Destroy(renderRoot);
            if (arcaneTrail != null)
                Destroy(arcaneTrail.gameObject);
        }
    }

    /// <summary>
    /// Ribbon UI che segue esclusivamente il percorso del dado 3D. La doppia
    /// fascia nera/colorata produce la scia infusa mostrata nel mockup.
    /// </summary>
    internal sealed class ArcaneDiceTrailGraphic : MaskableGraphic
    {
        private const int MaxSamples = 96;
        private const float Lifetime = 0.48f;
        private readonly List<TrailSample> samples = new List<TrailSample>(MaxSamples);
        private readonly Vector3[] targetCorners = new Vector3[4];
        private RectTransform target;
        private Color glowColor = new Color(0.2f, 0.5f, 1f);
        private bool emitting;
        private Vector2 lastPosition;

        public void Configure(Color glow, RectTransform trackedTarget)
        {
            glowColor = glow;
            target = trackedTarget;
            SetVerticesDirty();
        }

        public void Begin()
        {
            samples.Clear();
            emitting = true;
            if (TryGetTargetPosition(out Vector2 position))
            {
                lastPosition = position;
                samples.Add(new TrailSample(position, Time.unscaledTime));
            }
            gameObject.SetActive(true);
            SetVerticesDirty();
        }

        public void StopEmission()
        {
            emitting = false;
        }

        public void ClearTrail()
        {
            emitting = false;
            samples.Clear();
            SetVerticesDirty();
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (emitting && TryGetTargetPosition(out Vector2 position))
            {
                if ((position - lastPosition).sqrMagnitude >= 1f)
                {
                    samples.Add(new TrailSample(position, now));
                    lastPosition = position;
                    if (samples.Count > MaxSamples)
                        samples.RemoveAt(0);
                }
            }

            while (samples.Count > 0 && now - samples[0].Time > Lifetime)
                samples.RemoveAt(0);

            if (!emitting && samples.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (samples.Count < 2)
                return;

            float now = Time.unscaledTime;
            // Il nero vive soltanto sui margini: il centro rimane trasparente,
            // mentre il colore è massimo vicino al bordo e svanisce al centro.
            AddOutlineEdges(vh, now, 58f, 11f);
            AddEdgeGlowGradient(vh, now, 58f, 11f);
        }

        private void AddOutlineEdges(VertexHelper vh, float now, float maximumWidth, float baseThickness)
        {
            for (int index = 1; index < samples.Count; index++)
            {
                TrailSample previous = samples[index - 1];
                TrailSample current = samples[index];
                Vector2 direction = current.Position - previous.Position;
                if (direction.sqrMagnitude < 0.01f)
                    continue;

                float previousLife = Mathf.Clamp01(1f - (now - previous.Time) / Lifetime);
                float currentLife = Mathf.Clamp01(1f - (now - current.Time) / Lifetime);
                Vector2 previousNormal = JoinNormal(index - 1);
                Vector2 currentNormal = JoinNormal(index);
                ComputeEdgeShape(index - 1, previous.Time, previousLife, maximumWidth, baseThickness, 0.17f,
                    out float previousOuterLeft, out float previousInnerLeft);
                ComputeEdgeShape(index, current.Time, currentLife, maximumWidth, baseThickness, 0.17f,
                    out float currentOuterLeft, out float currentInnerLeft);
                ComputeEdgeShape(index - 1, previous.Time, previousLife, maximumWidth, baseThickness, 2.83f,
                    out float previousOuterRight, out float previousInnerRight);
                ComputeEdgeShape(index, current.Time, currentLife, maximumWidth, baseThickness, 2.83f,
                    out float currentOuterRight, out float currentInnerRight);
                Color previousColor = new Color(0.002f, 0.004f, 0.012f, 0.94f * previousLife);
                Color currentColor = new Color(0.002f, 0.004f, 0.012f, 0.94f * currentLife);

                AddEdgeQuad(
                    vh,
                    previous.Position + previousNormal * previousOuterLeft,
                    previous.Position + previousNormal * previousInnerLeft,
                    current.Position + currentNormal * currentInnerLeft,
                    current.Position + currentNormal * currentOuterLeft,
                    previousColor,
                    currentColor);
                AddEdgeQuad(
                    vh,
                    previous.Position - previousNormal * previousOuterRight,
                    previous.Position - previousNormal * previousInnerRight,
                    current.Position - currentNormal * currentInnerRight,
                    current.Position - currentNormal * currentOuterRight,
                    previousColor,
                    currentColor);
            }
        }

        private static void ComputeEdgeShape(
            int index,
            float time,
            float life,
            float maximumWidth,
            float baseThickness,
            float phase,
            out float outer,
            out float inner)
        {
            // Solo frequenze morbide: una curva continua, mai dentellata.
            float broad = Mathf.Sin(index * 0.48f + time * 4.1f + phase) * 0.68f;
            float medium = Mathf.Sin(index * 0.93f - time * 5.3f + phase * 1.7f) * 0.32f;
            float wave = Mathf.Clamp(broad + medium, -1f, 1f);
            outer = maximumWidth * life * life * (1f + wave * 0.24f);
            float thickness = baseThickness * life * (0.72f + (wave + 1f) * 0.24f);
            inner = Mathf.Max(0f, outer - thickness);
        }

        private void AddEdgeGlowGradient(VertexHelper vh, float now, float maximumWidth, float outlineThickness)
        {
            for (int index = 1; index < samples.Count; index++)
            {
                TrailSample previous = samples[index - 1];
                TrailSample current = samples[index];
                Vector2 direction = current.Position - previous.Position;
                if (direction.sqrMagnitude < 0.01f)
                    continue;

                float previousLife = Mathf.Clamp01(1f - (now - previous.Time) / Lifetime);
                float currentLife = Mathf.Clamp01(1f - (now - current.Time) / Lifetime);
                Vector2 previousNormal = JoinNormal(index - 1);
                Vector2 currentNormal = JoinNormal(index);
                ComputeEdgeShape(index - 1, previous.Time, previousLife, maximumWidth, outlineThickness, 0.17f,
                    out _, out float previousWidthLeft);
                ComputeEdgeShape(index, current.Time, currentLife, maximumWidth, outlineThickness, 0.17f,
                    out _, out float currentWidthLeft);
                ComputeEdgeShape(index - 1, previous.Time, previousLife, maximumWidth, outlineThickness, 2.83f,
                    out _, out float previousWidthRight);
                ComputeEdgeShape(index, current.Time, currentLife, maximumWidth, outlineThickness, 2.83f,
                    out _, out float currentWidthRight);
                float shimmerPrevious = 0.78f + Mathf.Max(0f, Mathf.Sin(previous.Time * 24f + index * 1.37f)) * 0.22f;
                float shimmerCurrent = 0.78f + Mathf.Max(0f, Mathf.Sin(current.Time * 24f + index * 1.37f)) * 0.22f;

                // Quattro stop per lato: brillante all'esterno e molto tenue,
                // ma ancora visibile, arrivando al centro.
                AddGradientSide(vh, previous.Position, current.Position, previousNormal, currentNormal, previousWidthLeft, currentWidthLeft,
                    1f, 0.7f, previousLife, currentLife, 0.78f, 0.52f, shimmerPrevious, shimmerCurrent);
                AddGradientSide(vh, previous.Position, current.Position, previousNormal, currentNormal, previousWidthLeft, currentWidthLeft,
                    0.7f, 0.4f, previousLife, currentLife, 0.52f, 0.25f, shimmerPrevious, shimmerCurrent);
                AddGradientSide(vh, previous.Position, current.Position, previousNormal, currentNormal, previousWidthLeft, currentWidthLeft,
                    0.4f, 0f, previousLife, currentLife, 0.25f, 0.065f, shimmerPrevious, shimmerCurrent);

                AddGradientSide(vh, previous.Position, current.Position, -previousNormal, -currentNormal, previousWidthRight, currentWidthRight,
                    1f, 0.7f, previousLife, currentLife, 0.78f, 0.52f, shimmerPrevious, shimmerCurrent);
                AddGradientSide(vh, previous.Position, current.Position, -previousNormal, -currentNormal, previousWidthRight, currentWidthRight,
                    0.7f, 0.4f, previousLife, currentLife, 0.52f, 0.25f, shimmerPrevious, shimmerCurrent);
                AddGradientSide(vh, previous.Position, current.Position, -previousNormal, -currentNormal, previousWidthRight, currentWidthRight,
                    0.4f, 0f, previousLife, currentLife, 0.25f, 0.065f, shimmerPrevious, shimmerCurrent);
            }
        }

        private void AddGradientSide(
            VertexHelper vh,
            Vector2 previousCenter,
            Vector2 currentCenter,
            Vector2 previousNormal,
            Vector2 currentNormal,
            float previousWidth,
            float currentWidth,
            float outerStop,
            float innerStop,
            float previousLife,
            float currentLife,
            float outerAlpha,
            float innerAlpha,
            float previousShimmer,
            float currentShimmer)
        {
            Color previousOuter = GradientColor(outerAlpha * previousLife, previousShimmer);
            Color previousInner = GradientColor(innerAlpha * previousLife, previousShimmer * 0.72f);
            Color currentOuter = GradientColor(outerAlpha * currentLife, currentShimmer);
            Color currentInner = GradientColor(innerAlpha * currentLife, currentShimmer * 0.72f);
            int vertex = vh.currentVertCount;
            vh.AddVert(previousCenter + previousNormal * previousWidth * outerStop, previousOuter, Vector2.zero);
            vh.AddVert(previousCenter + previousNormal * previousWidth * innerStop, previousInner, Vector2.one);
            vh.AddVert(currentCenter + currentNormal * currentWidth * innerStop, currentInner, Vector2.one);
            vh.AddVert(currentCenter + currentNormal * currentWidth * outerStop, currentOuter, Vector2.zero);
            vh.AddTriangle(vertex, vertex + 1, vertex + 2);
            vh.AddTriangle(vertex, vertex + 2, vertex + 3);
        }

        private Vector2 JoinNormal(int index)
        {
            if (samples.Count < 2)
                return Vector2.up;

            Vector2 tangent;
            if (index <= 0)
                tangent = samples[1].Position - samples[0].Position;
            else if (index >= samples.Count - 1)
                tangent = samples[samples.Count - 1].Position - samples[samples.Count - 2].Position;
            else
                tangent = samples[index + 1].Position - samples[index - 1].Position;

            if (tangent.sqrMagnitude < 0.0001f)
                return Vector2.up;
            tangent.Normalize();
            return new Vector2(-tangent.y, tangent.x);
        }

        private Color GradientColor(float alpha, float shimmer)
        {
            return new Color(
                Mathf.Lerp(glowColor.r, 1f, shimmer * 0.22f),
                Mathf.Lerp(glowColor.g, 1f, shimmer * 0.22f),
                Mathf.Lerp(glowColor.b, 1f, shimmer * 0.22f),
                alpha);
        }

        private static void AddEdgeQuad(
            VertexHelper vh,
            Vector2 outerPrevious,
            Vector2 innerPrevious,
            Vector2 innerCurrent,
            Vector2 outerCurrent,
            Color previousColor,
            Color currentColor)
        {
            int vertex = vh.currentVertCount;
            vh.AddVert(outerPrevious, previousColor, Vector2.zero);
            vh.AddVert(innerPrevious, previousColor, Vector2.one);
            vh.AddVert(innerCurrent, currentColor, Vector2.one);
            vh.AddVert(outerCurrent, currentColor, Vector2.zero);
            vh.AddTriangle(vertex, vertex + 1, vertex + 2);
            vh.AddTriangle(vertex, vertex + 2, vertex + 3);
        }

        private void AddRibbon(
            VertexHelper vh,
            float now,
            float maximumWidth,
            Color baseColor,
            float distortion,
            float shimmer)
        {
            for (int index = 1; index < samples.Count; index++)
            {
                TrailSample previous = samples[index - 1];
                TrailSample current = samples[index];
                Vector2 direction = current.Position - previous.Position;
                if (direction.sqrMagnitude < 0.01f)
                    continue;

                float previousLife = Mathf.Clamp01(1f - (now - previous.Time) / Lifetime);
                float currentLife = Mathf.Clamp01(1f - (now - current.Time) / Lifetime);
                Vector2 normal = new Vector2(-direction.y, direction.x).normalized;
                float previousNoise = 1f + Mathf.Sin((index - 1) * 2.41f + previous.Time * 15.7f) * distortion;
                float currentNoise = 1f + Mathf.Sin(index * 2.41f + current.Time * 15.7f) * distortion;
                float previousWidth = maximumWidth * previousLife * previousLife * previousNoise;
                float currentWidth = maximumWidth * currentLife * currentLife * currentNoise;
                float previousShimmer = shimmer * Mathf.Max(0f, Mathf.Sin(previous.Time * 22f + index * 1.7f));
                float currentShimmer = shimmer * Mathf.Max(0f, Mathf.Sin(current.Time * 22f + index * 1.7f));
                Color previousColor = new Color(
                    Mathf.Lerp(baseColor.r, 1f, previousShimmer),
                    Mathf.Lerp(baseColor.g, 1f, previousShimmer),
                    Mathf.Lerp(baseColor.b, 1f, previousShimmer),
                    baseColor.a * previousLife);
                Color currentColor = new Color(
                    Mathf.Lerp(baseColor.r, 1f, currentShimmer),
                    Mathf.Lerp(baseColor.g, 1f, currentShimmer),
                    Mathf.Lerp(baseColor.b, 1f, currentShimmer),
                    baseColor.a * currentLife);

                int vertex = vh.currentVertCount;
                vh.AddVert(previous.Position - normal * previousWidth, previousColor, Vector2.zero);
                vh.AddVert(previous.Position + normal * previousWidth, previousColor, Vector2.one);
                vh.AddVert(current.Position + normal * currentWidth, currentColor, Vector2.one);
                vh.AddVert(current.Position - normal * currentWidth, currentColor, Vector2.zero);
                vh.AddTriangle(vertex, vertex + 1, vertex + 2);
                vh.AddTriangle(vertex, vertex + 2, vertex + 3);
            }
        }

        private void AddFragments(VertexHelper vh, float now)
        {
            for (int index = 2; index < samples.Count; index += 3)
            {
                TrailSample previous = samples[index - 1];
                TrailSample current = samples[index];
                Vector2 direction = current.Position - previous.Position;
                if (direction.sqrMagnitude < 0.01f)
                    continue;

                float life = Mathf.Clamp01(1f - (now - current.Time) / Lifetime);
                if (life <= 0.04f)
                    continue;

                Vector2 tangent = direction.normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float side = (index & 1) == 0 ? -1f : 1f;
                float seed = Mathf.Abs(Mathf.Sin(index * 12.9898f + current.Time * 4.7f));
                Vector2 center = current.Position
                    + normal * side * (44f + seed * 34f) * life
                    - tangent * (8f + seed * 22f);
                float size = (4f + seed * 8f) * life;
                Color dark = new Color(0.002f, 0.006f, 0.018f, 0.78f * life);
                Color glow = new Color(glowColor.r, glowColor.g, glowColor.b, 0.9f * life);
                AddFragmentQuad(vh, center, tangent, normal, size * 1.65f, dark);
                AddFragmentQuad(vh, center, tangent, normal, size, glow);
            }
        }

        private static void AddFragmentQuad(
            VertexHelper vh,
            Vector2 center,
            Vector2 tangent,
            Vector2 normal,
            float size,
            Color color)
        {
            Vector2 longAxis = tangent * size;
            Vector2 shortAxis = normal * size * 0.42f;
            int vertex = vh.currentVertCount;
            vh.AddVert(center - longAxis, new Color(color.r, color.g, color.b, 0f), Vector2.zero);
            vh.AddVert(center + shortAxis, color, Vector2.one);
            vh.AddVert(center + longAxis, new Color(color.r, color.g, color.b, 0f), Vector2.one);
            vh.AddVert(center - shortAxis, color, Vector2.zero);
            vh.AddTriangle(vertex, vertex + 1, vertex + 2);
            vh.AddTriangle(vertex, vertex + 2, vertex + 3);
        }

        private bool TryGetTargetPosition(out Vector2 position)
        {
            position = default;
            if (target == null || !target.gameObject.activeInHierarchy)
                return false;
            target.GetWorldCorners(targetCorners);
            Vector3 worldCenter = (targetCorners[0] + targetCorners[2]) * 0.5f;
            position = rectTransform.InverseTransformPoint(worldCenter);
            return true;
        }

        private readonly struct TrailSample
        {
            public TrailSample(Vector2 position, float time)
            {
                Position = position;
                Time = time;
            }

            public Vector2 Position { get; }
            public float Time { get; }
        }
    }
}
