using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    /// <summary>Presentazione persistente dei due sgherri. I modelli restano fuori dal
    /// Canvas e seguono la carta proiettandone la posizione sul piano di battaglia.</summary>
    public sealed class NecromancerMinionVfx : MonoBehaviour
    {
        private const string AlivePath = "Models/Necromancer/fleck_alive";
        private const string UndeadPath = "Models/Necromancer/fleck_undead";
        private const string SoulWispTexturePath = "UI/necromancer_soul_wisp_aaa";
        private const string TerrainTexturePath = "UI/necromancer_terrain_aaa";
        private readonly List<GameObject> summons = new();
        private readonly List<Text> powerBadges = new();
        private readonly Dictionary<Transform, Text> badgeBySocket = new();
        private readonly HashSet<GameObject> dyingMinions = new();
        private BattleSfxPlayer minionSfxPlayer;
        private AudioClip minionDeathSfx;
        private RectTransform caster;
        private Transform worldRoot;
        private GameObject presentationRoot;
        private RectTransform presentationRect;
        private RenderTexture renderTexture;
        private Material presentationMaterial;
        private GameObject vfxCameraObject;
        private const float FleckModelScale = 500f;
        // I centri devono stare oltre il bordo esterno della carta, non sopra
        // il ritratto o la cornice del Necromante.
        // Inquadratura: la camera ortografica e gli offset dei socket sono legati.
        // Cambiando orthographicSize vanno riscalati gli offset con lo stesso
        // fattore, altrimenti gli sgherri escono dai lati o finiscono sulla carta.
        private const float CameraOrthoSize = 3.5f;
        private const float SocketOffsetWide = 3.31f;
        private const float SocketOffsetLateral = 1.3f;
        // Meta' larghezza occupata da una voragine (quad 3.35 per la scala del gruppo).
        private const float ContentHalfWidth = 1.25f;
        // Inclinazione della vista: a 69 gradi un modello verticale si proiettava
        // al 36% della sua altezza e la rana occupava un settimo dell'inquadratura.
        // A 42 gradi recupera volume e silhouette, e la voragine resta un'ellisse
        // (64% di schiacciamento) invece di diventare una striscia.
        private const float CameraPitchDegrees = 42f;
        private const float CameraDistance = 11.63f;
        // Alzato per centrare verticalmente il gruppo nell'inquadratura piu' stretta.
        private const float CameraFocusHeight = 1.36f;
        private float socketOffsetWorld = SocketOffsetWide;
        private const float MinionGroupScale = 0.70f;
        private const int VfxLayer = 30;
        private const float DefaultSurfaceAspect = 1.6f;
        private const float SupersampleFactor = 2f;
        // Quanto il numero si stringe alla sagoma nel layout laterale: 1 = bordo dei
        // bounds (braccia comprese), valori piu' bassi lo portano verso il torso.
        private const float BesideHugFactor = 0.72f;
        private static readonly Dictionary<Light, int> sceneLightMasks = new();
        private static int isolatingPresentations;
        private bool isolatesSceneLights;
        // Ogni Necromante ha bisogno di una porzione di mondo tutta sua: le camere
        // VFX condividono il layer, quindi due presentazioni sulla stessa origine si
        // vedevano a vicenda e gli sgherri comparivano moltiplicati su ogni carta.
        private static readonly HashSet<int> usedSceneSlots = new();
        private const float SceneSlotSpacing = 120f;
        private static GameObject sharedLightRig;
        private static int lightRigUsers;
        private bool usesLightRig;
        private int sceneSlot = -1;
        private Vector3 sceneOrigin = new Vector3(12000f, 12000f, 12000f);
        private float facingYaw;
        private bool lateralLayout;
        private bool layoutInitialized;
        private Vector2 presentationVelocity;
        private readonly Vector3[] socketVelocities = new Vector3[2];
        private const float LayoutSmoothTime = 0.24f;

        public static IEnumerator Summon(RectTransform casterRect, bool belongsToPlayer)
        {
            if (casterRect == null)
                yield break;
            NecromancerMinionVfx vfx = casterRect.GetComponent<NecromancerMinionVfx>();
            if (vfx == null)
                vfx = casterRect.gameObject.AddComponent<NecromancerMinionVfx>();
            yield return vfx.SummonRoutine(casterRect, belongsToPlayer);
        }

        public static void RemoveOne(RectTransform casterRect)
        {
            NecromancerMinionVfx vfx = casterRect != null
                ? casterRect.GetComponent<NecromancerMinionVfx>()
                : null;
            vfx?.RemoveOneVisual();
        }

        public static IEnumerator PlayCombatStrength(
            RectTransform casterRect, int total, bool minionSurvived)
        {
            NecromancerMinionVfx vfx = casterRect != null
                ? casterRect.GetComponent<NecromancerMinionVfx>()
                : null;
            if (vfx == null)
                yield break;
            yield return vfx.PlayCombatStrengthRoutine(total, minionSurvived);
        }

        private IEnumerator PlayCombatStrengthRoutine(int total, bool minionSurvived)
        {
            Text badge = null;
            for (int i = summons.Count - 1; i >= 0 && badge == null; i--)
            {
                GameObject summon = summons[i];
                if (summon != null && summon.transform.parent != null)
                    badgeBySocket.TryGetValue(summon.transform.parent, out badge);
            }
            if (badge == null)
                yield break;
            RectTransform rect = badge.rectTransform;
            Color verdict = minionSurvived
                ? new Color(0.25f, 1f, 0.38f)
                : new Color(1f, 0.2f, 0.16f);
            const float duration = 0.72f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (badge == null)
                    yield break;
                float p = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, p);
                badge.text = Mathf.RoundToInt(Mathf.Lerp(2f, total, eased)).ToString();
                badge.color = Color.Lerp(Color.white, verdict, eased);
                float pulse = 1f + Mathf.Sin(p * Mathf.PI) * 0.48f;
                rect.localScale = Vector3.one * pulse;
                yield return null;
            }
            badge.text = total.ToString();
            badge.color = verdict;
            rect.localScale = Vector3.one * 1.12f;
            yield return new WaitForSecondsRealtime(0.3f);
            if (badge == null)
                yield break;
            if (minionSurvived)
            {
                badge.text = "2";
                badge.color = Color.white;
                rect.localScale = Vector3.one;
            }
        }

        private void RemoveOneVisual()
        {
            for (int i = summons.Count - 1; i >= 0; i--)
            {
                GameObject minion = summons[i];
                summons.RemoveAt(i);
                if (minion == null) continue;
                Transform socket = minion.transform.parent;
                Text badge = null;
                if (socket != null && badgeBySocket.TryGetValue(socket, out badge))
                {
                    badgeBySocket.Remove(socket);
                    powerBadges.Remove(badge);
                }
                // Il badge se ne va insieme al modello, non prima e non dopo.
                StartCoroutine(PlayMinionDeathAndCloseChasm(minion, socket, badge));
                return;
            }
        }

        private IEnumerator PlayMinionDeathAndCloseChasm(
            GameObject minion, Transform socket, Text badge)
        {
            dyingMinions.Add(minion);
            minionSfxPlayer?.RefreshSettings();
            minionSfxPlayer?.PlayClip(minionDeathSfx);
            SpawnTransformationBurst(socket);
            Renderer[] renderers = minion.GetComponentsInChildren<Renderer>(true);
            Vector3 startScale = minion.transform.localScale;
            Transform chasm = socket.childCount > 0 ? socket.GetChild(0) : null;
            Vector3 chasmScale = chasm != null ? chasm.localScale : Vector3.one;
            Color badgeColor = badge != null ? badge.color : Color.white;
            const float duration = 0.92f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(elapsed / duration);
                float collapse = 1f - Mathf.SmoothStep(0f, 1f, p);
                if (badge != null)
                {
                    badgeColor.a = collapse;
                    badge.color = badgeColor;
                }
                minion.transform.localScale = startScale * collapse;
                minion.transform.localPosition += Vector3.down * Time.unscaledDeltaTime * 1.4f;
                minion.transform.Rotate(0f, Time.unscaledDeltaTime * 260f, 0f, Space.Self);
                foreach (Renderer renderer in renderers)
                    renderer.enabled = p < 0.78f;
                if (chasm != null)
                {
                    chasm.localScale = chasmScale * collapse;
                    chasm.Rotate(0f, -Time.unscaledDeltaTime * 190f, 0f, Space.Self);
                }
                yield return null;
            }
            if (badge != null)
                Destroy(badge.gameObject);
            if (socket != null)
                Destroy(socket.gameObject);
            dyingMinions.Remove(minion);
        }

        private IEnumerator SummonRoutine(RectTransform casterRect, bool belongsToPlayer)
        {
            caster = casterRect;
            if (minionSfxPlayer == null)
            {
                minionSfxPlayer = new BattleSfxPlayer();
                minionSfxPlayer.Initialize(caster, "Necromancer Minion SFX Audio Source");
                minionDeathSfx = Resources.Load<AudioClip>("SFX/necromancer_supreme_death");
            }
            layoutInitialized = false;
            presentationVelocity = Vector2.zero;
            for (int i = 0; i < socketVelocities.Length; i++) socketVelocities[i] = Vector3.zero;
            // Il modello Fleck e' esportato di profilo lungo l'asse X. +/-90 gradi
            // lo allineano verticalmente sul tavolo: alleati verso l'alto, nemici verso il basso.
            facingYaw = belongsToPlayer ? -90f : 90f;
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            DestroyPresentation();
            // Le liste vanno azzerate PRIMA di ricreare la superficie: e'
            // CreatePresentationSurface a ripopolare powerBadges, e svuotarla dopo
            // lasciava i badge scollegati dai socket. Da li' nascevano il numero
            // fermo ai bordi e la potenza che sopravviveva allo sgherro morto.
            summons.Clear();
            powerBadges.Clear();
            badgeBySocket.Clear();
            AllocateSceneOrigin();
            CreatePresentationSurface();
            worldRoot = new GameObject("Necromancer Minions Persistent VFX").transform;
            worldRoot.position = sceneOrigin;
            SetLayerRecursively(worldRoot.gameObject, VfxLayer);

            GameObject alivePrefab = Resources.Load<GameObject>(AlivePath);
            GameObject undeadPrefab = Resources.Load<GameObject>(UndeadPath);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Transform socket = new GameObject($"Minion Socket {i + 1}").transform;
                socket.SetParent(worldRoot, false);
                socket.localPosition = new Vector3(side * socketOffsetWorld, 0f, 0.18f);
                // Riduce l'intero gruppo senza alterare la scala 500/500/500
                // richiesta sui prefab Fleck.
                socket.localScale = Vector3.one * MinionGroupScale;
                SetLayerRecursively(socket.gameObject, VfxLayer);
                if (i < powerBadges.Count)
                    badgeBySocket[socket] = powerBadges[i];
                CreateChasm(socket, i);
                StartCoroutine(OpenChasm(socket));
                if (alivePrefab != null)
                {
                    GameObject alive = Instantiate(alivePrefab, socket);
                    alive.name = $"Fleck Alive {i + 1}";
                    NormalizeModel(alive);
                    ConfigureFleckMaterials(alive, undead: false, socket.position.y);
                    SetLayerRecursively(alive, VfxLayer);
                    summons.Add(alive);
                    StartCoroutine(EmergeAndTransform(alive, undeadPrefab, socket, i));
                }
            }
            yield return new WaitForSecondsRealtime(2.8f);
        }

        private void CreatePresentationSurface()
        {
            presentationRoot = new GameObject(
                "Necromancer Minions Card-Anchored Surface",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            presentationRect = presentationRoot.GetComponent<RectTransform>();
            presentationRect.SetParent(caster, false);
            presentationRect.anchorMin = presentationRect.anchorMax = new Vector2(0.5f, 0.5f);
            presentationRect.pivot = new Vector2(0.5f, 0.5f);
            // I due sgherri stanno ai lati del Necromante, all'altezza indicata
            // dalle X, e non nella fascia inferiore occupata da dadi e mana.
            presentationRect.anchoredPosition = new Vector2(0f, caster.rect.height * 0.08f);
            presentationRect.sizeDelta = new Vector2(
                Mathf.Max(300f, caster.rect.width * 2.35f),
                Mathf.Max(190f, caster.rect.height * 1.08f));
            presentationRect.SetAsLastSibling();

            // La RenderTexture deve avere lo stesso rapporto del rect: con una
            // risoluzione fissa 16:10 la camera ortografica inquadrava un'area di
            // proporzioni diverse e la RawImage la schiacciava in orizzontale.
            // La risoluzione segue i pixel realmente occupati sullo schermo: una RT
            // enorme ridotta a un centinaio di pixel perdeva tutto il dettaglio
            // delle ossa nel downscale bilineare.
            float surfaceAspect = SurfaceAspect();
            Canvas casterCanvas = caster.GetComponentInParent<Canvas>();
            float canvasScale = casterCanvas != null ? casterCanvas.scaleFactor : 1f;
            float displayHeight = presentationRect.rect.height * Mathf.Max(0.1f, canvasScale);
            int renderHeight = Mathf.Clamp(
                Mathf.RoundToInt(displayHeight * SupersampleFactor), 256, 1440);
            int renderWidth = Mathf.Clamp(
                Mathf.RoundToInt(renderHeight * surfaceAspect), 256, 2560);
            renderTexture = new RenderTexture(renderWidth, renderHeight, 24, RenderTextureFormat.ARGBHalf)
            {
                name = "Necromancer Minions Transparent HDR Render",
                // URP ignora l'MSAA impostato sulla RenderTexture: l'antialiasing
                // dipende solo dal pipeline asset, qui basta il supersampling.
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture.Create();
            // Una RT appena creata contiene memoria GPU non inizializzata: senza
            // questa pulizia la RawImage disegna un fotogramma di spazzatura (il
            // lampo colorato all'evocazione) prima che la camera VFX renderizzi.
            RenderTexture previousTarget = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previousTarget;
            RawImage image = presentationRoot.GetComponent<RawImage>();
            image.texture = renderTexture;
            image.raycastTarget = false;
            // Resta nascosta finche' la camera non ha prodotto il primo fotogramma.
            image.enabled = false;
            StartCoroutine(RevealWhenRendered(image));
            // I materiali della scena isolata scrivono colori premoltiplicati per
            // l'alpha. Con il blending UI standard la trasparenza verrebbe
            // applicata due volte e le particelle additive coprirebbero il tavolo
            // invece di illuminarlo.
            Shader premultipliedUi = Resources.Load<Shader>("Shaders/PremultipliedRawImage");
            if (premultipliedUi != null && premultipliedUi.isSupported)
            {
                presentationMaterial = new Material(premultipliedUi)
                {
                    name = "Necromancer Minions Premultiplied UI Material"
                };
                image.material = presentationMaterial;
            }
            else
            {
                Debug.LogError(
                    "Shader UI premoltiplicato non trovato: gli sgherri verranno composti due volte.");
            }
            CreatePowerBadges();

            // Non deve essere figlia della UI: quando il Necromante si sposta deve
            // muoversi solo la RawImage, non la camera che osserva la scena isolata.
            vfxCameraObject = new GameObject("Necromancer Minions VFX Camera", typeof(Camera));
            Camera camera = vfxCameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << VfxLayer;
            camera.orthographic = true;
            camera.orthographicSize = CameraOrthoSize;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30f;
            camera.targetTexture = renderTexture;
            camera.allowHDR = true;
            // Assegnare la targetTexture reimposta l'aspect sulla risoluzione della
            // RenderTexture: va forzato dopo, ed e' cio' che tiene le proporzioni
            // corrette quando il rect della carta cambia forma.
            camera.aspect = surfaceAspect;
            // Vista in tre quarti: abbastanza inclinata da mostrare le voragini come
            // ellissi, abbastanza bassa da non schiacciare i Fleck sul terreno.
            Vector3 focus = sceneOrigin + Vector3.up * CameraFocusHeight;
            float pitch = CameraPitchDegrees * Mathf.Deg2Rad;
            camera.transform.position = focus + new Vector3(
                0f,
                CameraDistance * Mathf.Sin(pitch),
                -CameraDistance * Mathf.Cos(pitch));
            camera.transform.LookAt(focus);
            AcquireVfxLightRig();
            IsolateVfxLayerFromSceneLights();
        }

        /// <summary>Rig condiviso da tutte le presentazioni. Le direzionali illuminano
        /// l'intero layer a prescindere da dove si trovano, quindi un rig per
        /// Necromante avrebbe sommato le intensita' su ogni sgherro in campo.</summary>
        private void AcquireVfxLightRig()
        {
            usesLightRig = true;
            lightRigUsers++;
            if (sharedLightRig != null)
                return;
            sharedLightRig = new GameObject("Necromancer Minions Shared VFX Lights");
            // Somma delle intensita' tenuta sotto controllo: con quattro direzionali
            // a 5.95 complessivi l'albedo chiaro delle ossa saturava a bianco e la
            // normal map spariva. Il pipeline asset rende le luci aggiuntive per
            // vertice, quindi solo la key (la piu' intensa, che URP promuove a main
            // light) disegna davvero il rilievo: le altre due sono solo tinta.
            CreateVfxLight("Fleck Bone Key Light", new Color(1f, 0.91f, 0.74f), 1.3f,
                Quaternion.Euler(52f, -34f, 0f));
            CreateVfxLight("Fleck Necrotic Fill", new Color(0.22f, 1f, 0.48f), 0.45f,
                Quaternion.Euler(68f, 138f, 0f));
            CreateVfxLight("Fleck Moon Rim", new Color(0.38f, 0.58f, 1f), 0.3f,
                Quaternion.Euler(38f, 205f, 0f));
            // Tolta la "Flat Beauty Fill": era bianca, a 1.65, allineata alla camera,
            // quindi annullava ogni ombreggiatura e appiattiva la silhouette.
        }

        private void ReleaseVfxLightRig()
        {
            if (!usesLightRig)
                return;
            usesLightRig = false;
            lightRigUsers = Mathf.Max(0, lightRigUsers - 1);
            if (lightRigUsers > 0 || sharedLightRig == null)
                return;
            Destroy(sharedLightRig);
            sharedLightRig = null;
        }

        /// <summary>Le luci della scena hanno culling mask "tutto" e illuminano anche
        /// il layer isolato: la direzionale della battaglia aggiungeva 2.0 di bianco
        /// pieno sopra al rig, fuori da ogni bilanciamento. Qui il layer viene tolto
        /// dalla loro mask e restituito quando la presentazione finisce.</summary>
        private void AllocateSceneOrigin()
        {
            if (sceneSlot >= 0)
                return;
            int slot = 0;
            while (usedSceneSlots.Contains(slot))
                slot++;
            usedSceneSlots.Add(slot);
            sceneSlot = slot;
            sceneOrigin = new Vector3(12000f + slot * SceneSlotSpacing, 12000f, 12000f);
        }

        private void ReleaseSceneOrigin()
        {
            if (sceneSlot < 0)
                return;
            usedSceneSlots.Remove(sceneSlot);
            sceneSlot = -1;
        }

        private void IsolateVfxLayerFromSceneLights()
        {
            isolatingPresentations++;
            isolatesSceneLights = true;
            if (isolatingPresentations > 1)
                return;
            int vfxBit = 1 << VfxLayer;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light == null || (light.cullingMask & vfxBit) == 0)
                    continue;
                // Le luci dedicate al rig hanno esattamente questo layer: non vanno
                // toccate, nemmeno quelle di un secondo Necromante in campo.
                if (light.cullingMask == vfxBit)
                    continue;
                sceneLightMasks[light] = light.cullingMask;
                light.cullingMask &= ~vfxBit;
            }
        }

        private void RestoreSceneLights()
        {
            if (!isolatesSceneLights)
                return;
            isolatesSceneLights = false;
            isolatingPresentations = Mathf.Max(0, isolatingPresentations - 1);
            if (isolatingPresentations > 0)
                return;
            foreach (KeyValuePair<Light, int> entry in sceneLightMasks)
            {
                if (entry.Key != null)
                    entry.Key.cullingMask = entry.Value;
            }
            sceneLightMasks.Clear();
        }

        private void CreatePowerBadges()
        {
            Font font = Resources.Load<Font>("Fonts/Alegreya")
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < 2; i++)
            {
                GameObject badgeObject = new GameObject(
                    $"Minion {i + 1} Strength", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                RectTransform rect = badgeObject.GetComponent<RectTransform>();
                rect.SetParent(presentationRect, false);
                // Allineato al centro proiettato della rispettiva voragine e poco
                // sotto il terreno infernale, invece che ai bordi dello schermo.
                float x = i == 0 ? 0.20f : 0.80f;
                rect.anchorMin = rect.anchorMax = new Vector2(x, 0.36f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(48f, 48f);
                Text text = badgeObject.GetComponent<Text>();
                text.font = font;
                text.fontSize = 30;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.text = "2";
                text.color = Color.white;
                text.raycastTarget = false;
                Outline outline = badgeObject.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = true;
                Outline tightOutline = badgeObject.AddComponent<Outline>();
                tightOutline.effectColor = new Color(0f, 0f, 0f, 1f);
                tightOutline.effectDistance = new Vector2(1f, -1f);
                tightOutline.useGraphicAlpha = true;
                badgeObject.transform.SetAsLastSibling();
                powerBadges.Add(text);
            }
        }

        private void CreateVfxLight(
            string name, Color color, float intensity, Quaternion rotation, bool castShadows = true)
        {
            GameObject lightObject = new GameObject(name, typeof(Light));
            lightObject.transform.SetParent(sharedLightRig.transform, false);
            lightObject.transform.rotation = rotation;
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.cullingMask = 1 << VfxLayer;
            light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        }

        private void DestroyPresentation()
        {
            if (presentationRoot != null) Destroy(presentationRoot);
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            if (vfxCameraObject != null) Destroy(vfxCameraObject);
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            if (presentationMaterial != null) Destroy(presentationMaterial);
            RestoreSceneLights();
            ReleaseVfxLightRig();
            ReleaseSceneOrigin();
            presentationRoot = null;
            worldRoot = null;
            renderTexture = null;
            presentationMaterial = null;
            presentationRect = null;
            vfxCameraObject = null;
        }

        private void OnDestroy() => DestroyPresentation();

        private void LateUpdate()
        {
            if (caster == null || presentationRect == null)
                return;

            // Il layout della battaglia puo' riparentare o ridimensionare le carte.
            // Mantieni gli sgherri legati al Necromante che li ha evocati, non a una
            // posizione assoluta del tavolo.
            if (presentationRect.parent != caster)
                presentationRect.SetParent(caster, false);
            presentationRect.anchorMin = presentationRect.anchorMax = new Vector2(0.5f, 0.5f);
            // Prima del layout: i badge vengono proiettati con questa camera e
            // devono usare l'aspect aggiornato.
            SyncCameraAspect();
            ApplyResponsiveMinionLayout();
            presentationRect.localRotation = Quaternion.identity;
            presentationRect.localScale = Vector3.one;
            presentationRoot.SetActive(caster.gameObject.activeInHierarchy);
        }

        private static IEnumerator RevealWhenRendered(RawImage image)
        {
            yield return new WaitForEndOfFrame();
            if (image != null)
                image.enabled = true;
        }

        /// <summary>Rapporto larghezza/altezza della superficie su cui la scena
        /// isolata viene proiettata. La camera ortografica deve inquadrare la
        /// stessa forma, altrimenti la RawImage stira il render.</summary>
        private float SurfaceAspect()
        {
            if (presentationRect == null)
                return DefaultSurfaceAspect;
            Rect rect = presentationRect.rect;
            return rect.width > 1f && rect.height > 1f
                ? rect.width / rect.height
                : DefaultSurfaceAspect;
        }

        private void SyncCameraAspect()
        {
            if (vfxCameraObject == null)
                return;
            Camera camera = vfxCameraObject.GetComponent<Camera>();
            if (camera == null)
                return;
            float aspect = SurfaceAspect();
            if (!Mathf.Approximately(camera.aspect, aspect))
                camera.aspect = aspect;
        }

        private void ApplyResponsiveMinionLayout()
        {
            Canvas canvas = caster.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, caster.position);
            float normalizedX = Screen.width > 0 ? screenPoint.x / Screen.width : 0.5f;
            if (!layoutInitialized)
            {
                lateralLayout = normalizedX < 0.30f || normalizedX > 0.70f;
                layoutInitialized = true;
            }
            else if (lateralLayout)
            {
                // Soglia di rientro piu' interna: evita continui cambi layout
                // quando la carta oscilla vicino al confine.
                if (normalizedX >= 0.36f && normalizedX <= 0.64f)
                    lateralLayout = false;
            }
            else if (normalizedX < 0.29f || normalizedX > 0.71f)
            {
                lateralLayout = true;
            }

            // Con carte molto strette la superficie diventa meno larga della coppia
            // di voragini: stringi l'offset invece di lasciarle uscire dall'inquadratura.
            float halfWidth = CameraOrthoSize * SurfaceAspect();
            float desiredOffset = Mathf.Min(
                lateralLayout ? SocketOffsetLateral : SocketOffsetWide,
                Mathf.Max(0f, halfWidth - ContentHalfWidth));
            Vector2 targetPresentationPosition = new Vector2(
                0f,
                caster.rect.height * (lateralLayout ? 0.52f : 0.08f));
            presentationRect.anchoredPosition = Vector2.SmoothDamp(
                presentationRect.anchoredPosition,
                targetPresentationPosition,
                ref presentationVelocity,
                LayoutSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            if (worldRoot != null)
            {
                for (int i = 0; i < Mathf.Min(2, worldRoot.childCount); i++)
                {
                    Transform socket = worldRoot.GetChild(i);
                    float side = i == 0 ? -1f : 1f;
                    Vector3 target = new Vector3(side * desiredOffset, 0f, 0.18f);
                    socket.localPosition = Vector3.SmoothDamp(
                        socket.localPosition,
                        target,
                        ref socketVelocities[i],
                        LayoutSmoothTime,
                        Mathf.Infinity,
                        Time.unscaledDeltaTime);
                }
            }

            if (worldRoot == null)
                return;
            for (int i = 0; i < worldRoot.childCount; i++)
            {
                Transform socket = worldRoot.GetChild(i);
                if (!badgeBySocket.TryGetValue(socket, out Text badge) || badge == null)
                    continue;
                Vector2 anchor;
                Camera vfxCamera = vfxCameraObject != null
                    ? vfxCameraObject.GetComponent<Camera>()
                    : null;
                float badgeSide = i == 0 ? -1f : 1f;
                if (vfxCamera != null)
                {
                    // Margine espresso in frazione di superficie: mezza larghezza del
                    // badge piu' un filo di stacco, cosi' il numero sfiora la sagoma
                    // invece di finire a distanza fissa da essa.
                    float surfaceWidth = presentationRect.rect.width;
                    // 0.45 perche' la cifra occupa meno di meta' del suo riquadro:
                    // contare l'intero box lasciava un buco visibile.
                    float badgeHalf = surfaceWidth > 1f
                        ? badge.rectTransform.sizeDelta.x * 0.45f * 0.5f / surfaceWidth
                        : 0.03f;
                    // Usa il bordo reale della sagoma, non un offset stimato dal
                    // centro: la distanza resta identica in ogni layout.
                    anchor = TerrainBadgeAnchor(
                        vfxCamera, socket, lateralLayout, badgeSide, badgeHalf + 0.012f);
                }
                else
                {
                    anchor = new Vector2(i == 0 ? 0.37f : 0.63f, 0.36f);
                }
                // Il socket e' gia' interpolato: il badge deve seguirlo senza un
                // secondo SmoothDamp, altrimenti rimane indietro durante lo spostamento.
                badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = anchor;
                badge.rectTransform.anchoredPosition = Vector2.zero;
                KeepBadgeOnScreen(badge, eventCamera);
            }
        }

        private static Vector2 TerrainBadgeAnchor(
            Camera camera, Transform socket, bool beside, float side, float besideMargin)
        {
            if (beside)
            {
                // Di fianco il riferimento e' la sagoma dello sgherro, non la voragine:
                // il quad del terreno e' largo il doppio e la sua texture ha molto
                // margine trasparente, quindi il numero finiva lontanissimo dal corpo
                // e sul lato esterno usciva perfino dallo schermo.
                if (TryGetMinionViewportSpan(camera, socket,
                        out float bodyMinX, out float bodyMaxX, out float bodyCenterY))
                {
                    // BesideHugFactor < 1 perche' i bounds comprendono le braccia
                    // spalancate, che sono quasi tutto vuoto: agganciarsi al loro
                    // bordo faceva sembrare il numero staccato dal corpo.
                    float bodyCenterX = (bodyMinX + bodyMaxX) * 0.5f;
                    float bodyHalf = (bodyMaxX - bodyMinX) * 0.5f;
                    return new Vector2(
                        bodyCenterX + side * (bodyHalf * BesideHugFactor + besideMargin),
                        bodyCenterY);
                }
                Vector3 socketFallback = camera.WorldToViewportPoint(socket.position);
                return new Vector2(
                    socketFallback.x + side * (besideMargin + 0.09f),
                    socketFallback.y);
            }

            Renderer terrainRenderer = null;
            foreach (Renderer candidate in socket.GetComponentsInChildren<Renderer>(true))
            {
                if (candidate.name == "Necromancer Terrain AAA Sprite")
                {
                    terrainRenderer = candidate;
                    break;
                }
            }
            Vector3 socketViewport = camera.WorldToViewportPoint(socket.position);
            if (terrainRenderer == null)
                return new Vector2(socketViewport.x, socketViewport.y - 0.115f);

            Bounds bounds = terrainRenderer.bounds;
            float minY = float.PositiveInfinity;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = bounds.center + Vector3.Scale(
                    bounds.extents, new Vector3(x, y, z));
                minY = Mathf.Min(minY, camera.WorldToViewportPoint(corner).y);
            }
            // La X deve appartenere al socket, non ai bounds del terreno: questi
            // ultimi cambiano proiezione con rotazione, particelle e vista inclinata
            // e potevano trascinare il numero verso il bordo della carta.
            return new Vector2(socketViewport.x, minY - 0.018f);
        }

        /// <summary>Il numero segue lo sgherro, ma lo sgherro puo' trovarsi su una carta
        /// appoggiata al bordo dello schermo: qui il badge viene rientrato quel tanto
        /// che basta a restare visibile, senza staccarsi dal modello.</summary>
        private static void KeepBadgeOnScreen(Text badge, Camera eventCamera)
        {
            if (badge == null || Screen.width <= 0)
                return;
            RectTransform rect = badge.rectTransform;
            // lossyScale porta gia' dentro lo scaleFactor del canvas: e' il fattore
            // che converte le unita' del rect in pixel di schermo.
            float pixelsPerUnit = Mathf.Abs(rect.lossyScale.x);
            if (pixelsPerUnit <= 0.0001f)
                return;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.position);
            float halfWidthScreen = rect.sizeDelta.x * 0.5f * pixelsPerUnit;
            const float ScreenPadding = 6f;
            float shift = 0f;
            float leftOverflow = halfWidthScreen + ScreenPadding - screenPoint.x;
            float rightOverflow = screenPoint.x - (Screen.width - halfWidthScreen - ScreenPadding);
            if (leftOverflow > 0f)
                shift = leftOverflow / pixelsPerUnit;
            else if (rightOverflow > 0f)
                shift = -rightOverflow / pixelsPerUnit;
            if (!Mathf.Approximately(shift, 0f))
                rect.anchoredPosition = new Vector2(shift, rect.anchoredPosition.y);
        }

        /// <summary>Estensione orizzontale proiettata del solo modello: esclude il quad
        /// del terreno e le particelle, che sono molto piu' larghi della sagoma.</summary>
        private static bool TryGetMinionViewportSpan(
            Camera camera, Transform socket, out float minX, out float maxX, out float centerY)
        {
            minX = float.PositiveInfinity;
            maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            bool found = false;
            foreach (Renderer candidate in socket.GetComponentsInChildren<Renderer>(true))
            {
                if (candidate is ParticleSystemRenderer)
                    continue;
                if (candidate.name == "Necromancer Terrain AAA Sprite")
                    continue;
                if (!candidate.enabled)
                    continue;
                Bounds bounds = candidate.bounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(
                        bounds.extents, new Vector3(x, y, z));
                    Vector3 viewport = camera.WorldToViewportPoint(corner);
                    minX = Mathf.Min(minX, viewport.x);
                    maxX = Mathf.Max(maxX, viewport.x);
                    minY = Mathf.Min(minY, viewport.y);
                    maxY = Mathf.Max(maxY, viewport.y);
                }
                found = true;
            }
            centerY = found ? (minY + maxY) * 0.5f : 0f;
            return found;
        }

        private static void CreateChasm(Transform socket, int index)
        {
            Transform chasm = new GameObject($"AAA Necromantic Chasm {index + 1}").transform;
            chasm.SetParent(socket, false);
            chasm.localScale = Vector3.zero;
            CreateTerrainSurface(chasm);
            CreateChasmParticles(chasm);
            SetLayerRecursively(chasm.gameObject, VfxLayer);
        }

        private static void CreateTerrainSurface(Transform parent)
        {
            Texture2D terrainTexture = Resources.Load<Texture2D>(TerrainTexturePath);
            GameObject terrain = GameObject.CreatePrimitive(PrimitiveType.Quad);
            terrain.name = "Necromancer Terrain AAA Sprite";
            terrain.transform.SetParent(parent, false);
            terrain.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            terrain.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            terrain.transform.localScale = new Vector3(3.35f, 3.35f, 1f);
            Object.Destroy(terrain.GetComponent<Collider>());

            Shader shader = Resources.Load<Shader>("Shaders/NecromancerTerrainTransparent");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("Necromancer terrain shader non trovato.");
                terrain.SetActive(false);
                return;
            }
            Material material = new Material(shader) { name = "Necromancer Terrain AAA Runtime Material" };
            if (terrainTexture != null)
            {
                material.SetTexture("_MainTex", terrainTexture);
            }
            material.SetColor("_Color", Color.white);
            terrain.GetComponent<Renderer>().material = material;
        }

        private static void CreateChasmParticles(Transform parent)
        {
            GameObject mist = new GameObject("Necrotic Mist and Soul Embers");
            mist.transform.SetParent(parent, false);
            mist.transform.localPosition = Vector3.up * 0.08f;
            ParticleSystem particles = mist.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.15f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.38f, 0.85f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.18f, 1f, 0.05f, 0.9f), new Color(0.18f, 0.02f, 0.25f, 0.45f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = 8f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = 0.9f;
            shape.donutRadius = 0.28f;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            // Unity richiede la stessa MinMaxCurveMode sui tre assi.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.18f, 0.65f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.15f), new Keyframe(0.22f, 1f),
                new Keyframe(0.72f, 0.82f), new Keyframe(1f, 0f)));
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.55f, 1f, 0.72f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.18f),
                    new GradientAlphaKey(0.75f, 0.72f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ConfigureParticleRenderer(mist.GetComponent<ParticleSystemRenderer>());
            particles.Play();
        }

        private static IEnumerator OpenChasm(Transform socket)
        {
            Transform chasm = socket.GetChild(0);
            for (float elapsed = 0f; elapsed < 1.15f; elapsed += Time.unscaledDeltaTime)
            {
                float p = Mathf.SmoothStep(0f, 1f, elapsed / 1.15f);
                float pulse = 1f + Mathf.Sin(p * Mathf.PI * 7f) * (1f - p) * 0.12f;
                chasm.localScale = new Vector3(p * pulse, p, p * pulse);
                chasm.localRotation = Quaternion.Euler(0f, p * 24f, 0f);
                yield return null;
            }
            chasm.localScale = Vector3.one;
        }

        private IEnumerator EmergeAndTransform(GameObject alive, GameObject undeadPrefab, Transform socket, int index)
        {
            // NormalizeModel corregge anche il pivot dell'FBX. Conserva quell'offset:
            // sovrascrivere localPosition lo annullava e poteva spingere la mesh fuori camera.
            Vector3 top = alive.transform.localPosition + new Vector3(0f, 0.08f, 0f);
            Vector3 buried = top + Vector3.down * 5.4f;
            for (float elapsed = 0f; elapsed < 1.65f; elapsed += Time.unscaledDeltaTime)
            {
                float p = Mathf.SmoothStep(0f, 1f, elapsed / 1.65f);
                alive.transform.localPosition = Vector3.LerpUnclamped(buried, top, p);
                alive.transform.localRotation = Quaternion.Euler(0f, facingYaw + Mathf.Sin(p * 12f) * 8f, 0f);
                yield return null;
            }

            // Il vivo si solleva sopra il portale e accelera fino a diventare un
            // vortice. Negli ultimi fotogrammi si contrae prima della frantumazione.
            Vector3 spinStart = alive.transform.localPosition;
            Vector3 spinApex = spinStart + Vector3.up * 1.65f;
            float accumulatedYaw = facingYaw;
            const float spinDuration = 1.18f;
            for (float elapsed = 0f; elapsed < spinDuration; elapsed += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(elapsed / spinDuration);
                float eased = Mathf.SmoothStep(0f, 1f, p);
                alive.transform.localPosition = Vector3.Lerp(spinStart, spinApex, eased);
                float angularSpeed = Mathf.Lerp(180f, 1800f, p * p);
                accumulatedYaw += angularSpeed * Time.unscaledDeltaTime;
                alive.transform.localRotation = Quaternion.Euler(
                    Mathf.Sin(p * Mathf.PI * 5f) * 5f, accumulatedYaw, p * 18f);
                yield return null;
            }

            SpawnTransformationBurst(socket, alive.transform.localPosition + Vector3.up * 0.7f);
            Renderer[] aliveRenderers = alive.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in aliveRenderers)
                renderer.enabled = false;

            if (undeadPrefab != null)
            {
                GameObject undead = Instantiate(undeadPrefab, socket);
                undead.name = $"Fleck Undead Minion {index + 1} - Power 2";
                NormalizeModel(undead);
                ConfigureFleckMaterials(undead, undead: true, socket.position.y);
                SetLayerRecursively(undead, VfxLayer);
                Vector3 undeadTop = undead.transform.localPosition + new Vector3(0f, 0.08f, 0f);
                Vector3 undeadScale = undead.transform.localScale;
                undead.transform.localPosition = alive.transform.localPosition;
                undead.transform.localRotation = alive.transform.localRotation;
                summons.Add(undead);

                // La trasformazione e' un solo vortice: il non-morto eredita punto,
                // angolo e velocita' del vivo, poi completa un numero intero di giri
                // rallentando fino a fermarsi nuovamente rivolto verso il campo.
                const float inheritedAngularSpeed = 1800f;
                float targetYaw = facingYaw + Mathf.Ceil(
                    (accumulatedYaw + 720f - facingYaw) / 360f) * 360f;
                float remainingRotation = targetYaw - accumulatedYaw;
                float undeadSpinDuration = 2f * remainingRotation / inheritedAngularSpeed;
                Vector3 undeadSpinStart = undead.transform.localPosition;
                for (float elapsed = 0f; elapsed < undeadSpinDuration; elapsed += Time.unscaledDeltaTime)
                {
                    float p = Mathf.Clamp01(elapsed / undeadSpinDuration);
                    float eased = Mathf.SmoothStep(0f, 1f, p);
                    float angularSpeed = Mathf.Lerp(inheritedAngularSpeed, 0f, p);
                    accumulatedYaw += angularSpeed * Time.unscaledDeltaTime;
                    undead.transform.localPosition = Vector3.Lerp(undeadSpinStart, undeadTop, eased);
                    undead.transform.localRotation = Quaternion.Euler(
                        Mathf.Sin(p * Mathf.PI * 3f) * (1f - p) * 5f,
                        accumulatedYaw,
                        Mathf.Lerp(18f, 0f, eased));
                    yield return null;
                }
                undead.transform.localPosition = undeadTop;
                undead.transform.localRotation = Quaternion.Euler(0f, facingYaw, 0f);
                undead.transform.localScale = undeadScale;
                StartCoroutine(PlayFloatingIdle(undead, index));
            }
            summons.Remove(alive);
            Destroy(alive);
        }

        private IEnumerator PlayFloatingIdle(GameObject minion, int index)
        {
            if (minion == null)
                yield break;
            Transform model = minion.transform;
            Vector3 basePosition = model.localPosition;
            Vector3 baseScale = model.localScale;
            float phase = index * Mathf.PI;
            while (model != null
                && model.gameObject.activeInHierarchy
                && !dyingMinions.Contains(model.gameObject))
            {
                float time = Time.unscaledTime;
                float wave = Mathf.Sin(time * 1.15f + phase);
                float secondary = Mathf.Sin(time * 0.63f + phase * 0.7f);
                model.localPosition = basePosition + new Vector3(
                    secondary * 0.045f,
                    wave * 0.16f,
                    Mathf.Cos(time * 0.82f + phase) * 0.035f);
                model.localRotation = Quaternion.Euler(
                    secondary * 2.2f,
                    facingYaw + wave * 3.2f,
                    wave * 2.7f);
                model.localScale = baseScale * (1f + secondary * 0.012f);
                yield return null;
            }
        }

        private static void SpawnTransformationBurst(Transform socket, Vector3? localPosition = null)
        {
            GameObject burst = new GameObject("Fleck Transformation Debris");
            burst.transform.SetParent(socket, false);
            burst.transform.localPosition = localPosition ?? Vector3.up * 0.8f;
            ParticleSystem particles = burst.AddComponent<ParticleSystem>();
            SetLayerRecursively(burst, VfxLayer);
            // AddComponent avvia il ParticleSystem immediatamente perche' playOnAwake
            // e' true di default. Unity non permette di cambiare duration mentre gira.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.duration = 0.7f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.78f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 1f, 0.72f), Color.white);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.3f, 1f, 0.54f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.82f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ConfigureParticleRenderer(burst.GetComponent<ParticleSystemRenderer>());
            particles.Play();
            Object.Destroy(burst, 2f);
        }

        private static void NormalizeModel(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;
            // Gli FBX Fleck sono esportati in unita' molto piccole: la scala di
            // presentazione richiesta e' esattamente 500 su tutti e tre gli assi.
            model.transform.localScale = Vector3.one * FleckModelScale;
            model.transform.localRotation = Quaternion.identity;

            // Gli FBX non condividono lo stesso pivot. Dopo la scala appoggia il
            // punto piu' basso del renderer sul socket, evitando offset laterali o
            // verticali importati dal file.
            renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 localBottom = model.transform.InverseTransformPoint(
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            model.transform.localPosition -= localBottom;
        }

        private static void ConfigureFleckMaterials(GameObject model, bool undead, float clipWorldY)
        {
            string suffix = undead ? " 1" : string.Empty;
            Texture2D albedo = Resources.Load<Texture2D>(
                $"Models/Necromancer/texture_pbr_20250901{suffix}");
            Texture2D normal = Resources.Load<Texture2D>(
                $"Models/Necromancer/texture_pbr_20250901_normal{suffix}");
            Shader litShader = Resources.Load<Shader>("Shaders/NecromancerFleckSurfaceClip");
            if (litShader == null || !litShader.isSupported)
            {
                Debug.LogError("Shader di ritaglio Fleck non trovato o non supportato.");
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = false;
                return;
            }

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                int materialCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                var materials = new Material[materialCount];
                for (int i = 0; i < materialCount; i++)
                {
                    Material material = new Material(litShader)
                    {
                        name = $"Fleck {(undead ? "Undead" : "Alive")} Explicit URP Material"
                    };
                    // Stesso valore del materiale importato con l'FBX: il bianco pieno
                    // spingeva l'albedo gia' chiarissimo delle ossa contro il clipping.
                    material.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f, 1f));
                    material.SetFloat("_ClipWorldY", clipWorldY);
                    material.SetFloat("_Surface", 0f);
                    material.SetFloat("_ZWrite", 1f);
                    material.SetFloat("_Metallic", 0f);
                    // Corpo completamente opaco: niente lucentezza. Azzerare la
                    // smoothness da sola non basta, URP/Lit continua a sommare sia
                    // il riflesso speculare della key sia quello dell'ambiente preso
                    // dallo skybox: servono le due keyword.
                    material.SetFloat("_Smoothness", 0f);
                    material.SetFloat("_SpecularHighlights", 0f);
                    material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    material.SetFloat("_EnvironmentReflections", 0f);
                    material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    if (albedo != null)
                    {
                        material.SetTexture("_BaseMap", albedo);
                    }
                    // Nessuna emissione: era una luminosita' propria del corpo,
                    // indipendente dalle luci e quindi impossibile da spegnere
                    // ribilanciando il rig.
                    material.SetColor("_EmissionColor", Color.black);
                    material.DisableKeyword("_EMISSION");
                    if (normal != null)
                    {
                        material.SetTexture("_BumpMap", normal);
                        material.SetFloat("_BumpScale", 1f);
                        material.EnableKeyword("_NORMALMAP");
                    }
                    material.renderQueue = -1;
                    material.SetOverrideTag("RenderType", "Opaque");
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    materials[i] = material;
                }
                renderer.materials = materials;
            }
        }

        private static void ConfigureParticleRenderer(ParticleSystemRenderer renderer)
        {
            if (renderer == null)
                return;
            Shader shader = Resources.Load<Shader>("Shaders/NecromancerSoulWispAdditive");
            if (shader == null || !shader.isSupported)
            {
                // Meglio nessuna particella che i quad magenta dello shader errore.
                renderer.enabled = false;
                return;
            }
            Material material = new Material(shader) { name = "Necromancer Particle Runtime Material" };
            Texture2D soulTexture = Resources.Load<Texture2D>(SoulWispTexturePath);
            if (soulTexture != null)
            {
                material.SetTexture("_MainTex", soulTexture);
            }
            material.SetColor("_Color", new Color(0.72f, 1.35f, 0.9f, 1f));
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}
