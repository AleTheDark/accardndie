using System.Collections.Generic;
using AccardND.GameCore;
using UnityEngine;

namespace AccardND.Battlefield
{
    /// <summary>AAA-style procedural fire package used by Empowered 3D dice.</summary>
    internal sealed class EmpoweredDiceFireVfx : MonoBehaviour
    {
        private static Material shellMaterial;
        private static Material resultMaskMaterial;
        // Uno shader mancante non si risolve riprovando: la ricerca si fa una
        // volta sola, altrimenti ogni dado ripaga un Shader.Find a vuoto.
        private static bool shellMaterialResolved;
        private static bool resultMaskMaterialResolved;
        private bool particleMaterialResolved;
        private readonly List<Renderer> shellRenderers = new List<Renderer>();
        private readonly List<Renderer> resultMaskRenderers = new List<Renderer>();
        private MaterialPropertyBlock shellProperties;
        private Material particleMaterial;
        private Color rimColor;
        private Color coreColor;
        private ParticleSystem flames;
        private ParticleSystem embers;
        private Light coreLight;
        private bool active;

        public static EmpoweredDiceFireVfx Create(Transform parent)
        {
            GameObject root = new GameObject("Empower Fire Ascendant VFX");
            root.transform.SetParent(parent, false);
            EmpoweredDiceFireVfx vfx = root.AddComponent<EmpoweredDiceFireVfx>();
            vfx.Build();
            root.SetActive(false);
            return vfx;
        }

        public void SetActive(bool value)
        {
            active = value;
            gameObject.SetActive(value);
            foreach (Renderer shell in shellRenderers)
                if (shell != null) shell.enabled = value;
            foreach (Renderer mask in resultMaskRenderers)
                if (mask != null) mask.enabled = false;
            if (value)
            {
                if (coreLight != null) coreLight.enabled = true;
                flames?.Play(true);
                embers?.Play(true);
            }
            if (!value)
            {
                flames?.Clear(true);
                embers?.Clear(true);
            }
        }

        public void SetSettled(bool value)
        {
            if (!active)
                value = false;
            foreach (Renderer mask in resultMaskRenderers)
                if (mask != null) mask.enabled = value;
        }

        public void BindDie(GameObject die)
        {
            foreach (Renderer shell in shellRenderers)
                if (shell != null) Destroy(shell.gameObject);
            shellRenderers.Clear();
            foreach (Renderer mask in resultMaskRenderers)
                if (mask != null) Destroy(mask.gameObject);
            resultMaskRenderers.Clear();
            if (die == null)
                return;

            // Senza il proprio shader il pezzo non si monta affatto: un
            // renderer con material nullo dipingerebbe il dado di magenta.
            Material shellMat = GetShellMaterial();
            Material maskMat = GetResultMaskMaterial();
            foreach (MeshFilter source in die.GetComponentsInChildren<MeshFilter>(true))
            {
                if (source.sharedMesh == null)
                    continue;
                if (shellMat != null)
                {
                    GameObject shellObject = new GameObject(
                        "Empower Infernal Fresnel Shell",
                        typeof(MeshFilter),
                        typeof(MeshRenderer),
                        typeof(EmpoweredDiceVfxPart));
                    shellObject.transform.SetParent(source.transform, false);
                    shellObject.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                    MeshRenderer shell = shellObject.GetComponent<MeshRenderer>();
                    shell.sharedMaterial = shellMat;
                    shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    shell.receiveShadows = false;
                    shell.enabled = active;
                    shellRenderers.Add(shell);
                }

                if (maskMat != null)
                {
                    GameObject maskObject = new GameObject(
                        "Empower Landed Result Stencil Mask",
                        typeof(MeshFilter),
                        typeof(MeshRenderer),
                        typeof(EmpoweredDiceVfxPart));
                    maskObject.transform.SetParent(source.transform, false);
                    maskObject.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                    MeshRenderer mask = maskObject.GetComponent<MeshRenderer>();
                    mask.sharedMaterial = maskMat;
                    mask.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mask.receiveShadows = false;
                    mask.enabled = false;
                    resultMaskRenderers.Add(mask);
                }
            }
            ApplyPalette();
        }

        public void ConfigureForClass(HeroClass heroClass)
        {
            // Tutti i poliedri condividono il trattamento celestiale del D20;
            // soltanto la tinta identifica la classe che sta lanciando il dado.
            rimColor = ClassDice3D.GlowColor(heroClass);
            coreColor = rimColor;
            ApplyPalette();
        }

        private void ApplyPalette()
        {
            if (shellProperties == null)
                return;
            shellProperties.SetColor("_RimColor", rimColor * 4.2f);
            shellProperties.SetColor("_CoreColor", coreColor * 6.2f);
            foreach (Renderer shell in shellRenderers)
                if (shell != null) shell.SetPropertyBlock(shellProperties);
            if (particleMaterial != null)
            {
                particleMaterial.SetColor("_EdgeColor", rimColor * 2.4f);
                particleMaterial.SetColor("_CoreColor", coreColor * 4.8f);
            }
            if (coreLight != null)
                coreLight.color = Color.Lerp(rimColor, coreColor, 0.35f);
        }

        private void Build()
        {
            // MaterialPropertyBlock alloca una risorsa Unity nativa: va creato
            // dopo AddComponent, mai nel costruttore implicito del MonoBehaviour.
            shellProperties = new MaterialPropertyBlock();
            flames = CreateSystem("White-Gold Flame Mantle", 58, 0.42f, 0.04f, 0.18f);
            ParticleSystem.MainModule flameMain = flames.main;
            flameMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.72f, 0.72f, 0.12f),
                new Color(1f, 1f, 1f, 0.9f));
            flameMain.startSize = new ParticleSystem.MinMaxCurve(0.075f, 0.18f);
            flameMain.startSpeed = new ParticleSystem.MinMaxCurve(0.34f, 0.9f);
            ParticleSystem.ShapeModule flameShape = flames.shape;
            flameShape.shapeType = ParticleSystemShapeType.Sphere;
            // Emissione sulla pelle esterna, non dentro la silhouette del dado.
            flameShape.radius = 0.57f;
            flameShape.radiusThickness = 0.02f;
            ParticleSystem.VelocityOverLifetimeModule flameVelocity = flames.velocityOverLifetime;
            flameVelocity.enabled = true;
            flameVelocity.space = ParticleSystemSimulationSpace.World;
            flameVelocity.y = new ParticleSystem.MinMaxCurve(0.7f, 1.45f);
            flameVelocity.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
            flameVelocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            ParticleSystem.ColorOverLifetimeModule flameColor = flames.colorOverLifetime;
            flameColor.enabled = true;
            flameColor.color = FireGradient(0.78f);
            ParticleSystem.SizeOverLifetimeModule flameSize = flames.sizeOverLifetime;
            flameSize.enabled = true;
            flameSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 0f));
            ParticleSystem.NoiseModule noise = flames.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 1.4f;
            noise.scrollSpeed = 1.8f;

            embers = CreateSystem("Empower Ember Sparks", 38, 1.05f, 0.01f, 0.055f);
            ParticleSystem.MainModule emberMain = embers.main;
            emberMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.72f, 0.72f, 0.72f), Color.white);
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 2.2f);
            ParticleSystem.ShapeModule emberShape = embers.shape;
            emberShape.shapeType = ParticleSystemShapeType.Sphere;
            emberShape.radius = 0.58f;
            ParticleSystem.VelocityOverLifetimeModule emberVelocity = embers.velocityOverLifetime;
            emberVelocity.enabled = true;
            emberVelocity.space = ParticleSystemSimulationSpace.World;
            emberVelocity.x = new ParticleSystem.MinMaxCurve(-0.32f, 0.32f);
            emberVelocity.y = new ParticleSystem.MinMaxCurve(0.55f, 1.5f);
            emberVelocity.z = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
            ParticleSystem.ColorOverLifetimeModule emberColor = embers.colorOverLifetime;
            emberColor.enabled = true;
            emberColor.color = FireGradient(1f);
            ParticleSystem.TrailModule trails = embers.trails;
            trails.enabled = true;
            trails.lifetime = 0.12f;
            trails.ratio = 0.72f;
            ParticleSystemRenderer emberRenderer = embers.GetComponent<ParticleSystemRenderer>();
            Material emberTrailMaterial = GetParticleMaterial();
            if (emberTrailMaterial != null)
                emberRenderer.trailMaterial = emberTrailMaterial;

            coreLight = new GameObject("Empower Pulsing Core", typeof(Light)).GetComponent<Light>();
            coreLight.transform.SetParent(transform, false);
            coreLight.type = LightType.Point;
            coreLight.color = new Color(1f, 0.43f, 0.035f);
            coreLight.range = 3.2f;
            coreLight.intensity = 3.4f;
            ConfigureForClass(HeroClass.Warrior);
        }

        private ParticleSystem CreateSystem(string systemName, float rate, float lifetime, float minSize, float maxSize)
        {
            GameObject go = new GameObject(systemName, typeof(ParticleSystem));
            go.transform.SetParent(transform, false);
            ParticleSystem system = go.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.maxParticles = 180;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // Se lo shader non c'e' si tiene il material di default del sistema
            // particellare: scialbo, ma visibile e del colore giusto.
            Material material = GetParticleMaterial();
            if (material != null)
                renderer.material = material;
            renderer.sortingFudge = -8f;
            return system;
        }

        private static ParticleSystem.MinMaxGradient FireGradient(float alpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.94f, 0.94f, 0.94f), 0.22f),
                    new GradientColorKey(new Color(0.68f, 0.68f, 0.68f), 0.7f),
                    new GradientColorKey(new Color(0.18f, 0.18f, 0.18f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(alpha, 0.1f),
                    new GradientAlphaKey(alpha * 0.82f, 0.62f),
                    new GradientAlphaKey(0f, 1f)
                });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>
        /// Primo shader disponibile tra quelli indicati, o null. Gli shader del
        /// pacchetto vivono in Resources/Shaders proprio perche' Shader.Find li
        /// trovi anche nella build: se un giorno tornassero fuori da li' il
        /// player non avrebbe piu' nulla da trovare, e un material con shader
        /// nullo dipinge di magenta tutto quello che tocca. Meglio niente
        /// Empower che un dado magenta.
        /// </summary>
        private static Shader FindShader(params string[] shaderNames)
        {
            foreach (string shaderName in shaderNames)
            {
                Shader shader = shaderName switch
                {
                    "AccardND/VFX/Empower Fire Particle" => Resources.Load<Shader>("Shaders/EmpowerFireParticle"),
                    "AccardND/VFX/Empower Dice Shell" => Resources.Load<Shader>("Shaders/EmpowerDiceShell"),
                    "AccardND/VFX/Empower Dice Result Mask" => Resources.Load<Shader>("Shaders/EmpowerDiceResultMask"),
                    _ => Shader.Find(shaderName)
                };
                if (shader != null && shader.isSupported)
                    return shader;
            }
            return null;
        }

        private Material GetParticleMaterial()
        {
            if (particleMaterialResolved)
                return particleMaterial;
            particleMaterialResolved = true;
            Shader shader = FindShader(
                "AccardND/VFX/Empower Fire Particle",
                "Universal Render Pipeline/Particles/Unlit",
                "Particles/Standard Unlit",
                "Legacy Shaders/Particles/Additive");
            if (shader == null)
                return null;

            particleMaterial = new Material(shader) { name = "Empower Fire Additive (Runtime Instance)" };
            if (particleMaterial.HasProperty("_Surface")) particleMaterial.SetFloat("_Surface", 1f);
            if (particleMaterial.HasProperty("_Blend")) particleMaterial.SetFloat("_Blend", 1f);
            ApplyPalette();
            return particleMaterial;
        }

        private static Material GetShellMaterial()
        {
            if (shellMaterialResolved)
                return shellMaterial;
            shellMaterialResolved = true;
            Shader shader = FindShader("AccardND/VFX/Empower Dice Shell");
            if (shader == null)
                return null;

            shellMaterial = new Material(shader) { name = "Empower Infernal Shell (Runtime)" };
            return shellMaterial;
        }

        private static Material GetResultMaskMaterial()
        {
            if (resultMaskMaterialResolved)
                return resultMaskMaterial;
            resultMaskMaterialResolved = true;
            Shader shader = FindShader("AccardND/VFX/Empower Dice Result Mask");
            if (shader == null)
                return null;

            resultMaskMaterial = new Material(shader) { name = "Empower Landed Result Stencil Mask (Runtime)" };
            return resultMaskMaterial;
        }

        private void LateUpdate()
        {
            if (!active || coreLight == null)
                return;
            float pulse = Mathf.Sin(Time.unscaledTime * 11f + GetInstanceID() * 0.01f);
            coreLight.intensity = 3.6f + pulse * 0.85f;
            coreLight.range = 3.15f + pulse * 0.18f;
        }
    }

    /// <summary>
    /// Marca guscio e maschera dell'Empower. Vivono appesi sotto la mesh del
    /// dado (devono seguirne ogni rotazione), quindi finiscono in qualsiasi
    /// GetComponentsInChildren fatto sul dado: chi enumera quella gerarchia per
    /// lavorare sul dado vero — lo sgretolamento, l'override del glow — li
    /// riconosce da qui e li salta.
    /// </summary>
    internal sealed class EmpoweredDiceVfxPart : MonoBehaviour
    {
    }
}
