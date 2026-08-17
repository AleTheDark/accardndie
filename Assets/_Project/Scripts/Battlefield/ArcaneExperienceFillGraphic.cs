using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArcaneExperienceFillGraphic : MaskableGraphic
    {
        private const int RibbonSegments = 28;
        private const int SparkCount = 14;
        private const int RuneCount = 12;

        /// <summary>
        /// Quante volte al secondo rigenerare la mesh. Ogni rebuild ricostruisce
        /// a mano qualche centinaio di quad in C#, e questa barra e' anche la
        /// vita di ogni carta sul tavolo: moltiplicata per le carte in campo,
        /// a ogni frame era il costo CPU piu' alto della battaglia. A quaranta
        /// si salta circa un frame su tre dei sessanta, con un margine che
        /// l'occhio non dovrebbe cogliere su nastri e scintille.
        /// </summary>
        private const float RebuildsPerSecond = 40f;

        private float nextRebuildAt;

        [SerializeField] private Color deepColor = new Color(0.008f, 0.055f, 0.34f, 1f);
        [SerializeField] private Color coreColor = new Color(0.015f, 0.28f, 0.92f, 1f);
        [SerializeField] private Color crestColor = new Color(0.12f, 0.58f, 1f, 1f);

        public void SetPalette(Color deep, Color core, Color crest)
        {
            deepColor = deep;
            coreColor = core;
            crestColor = crest;
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            useLegacyMeshGeneration = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // Riaccesa, la barra si ridisegna subito invece di aspettare il turno.
            nextRebuildAt = 0f;
        }

        private void Update()
        {
            if (!isActiveAndEnabled)
                return;

            float now = Time.unscaledTime;
            if (now < nextRebuildAt)
                return;

            nextRebuildAt = now + 1f / RebuildsPerSecond;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            float time = Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;
            AddVerticalGradient(vh, rect, deepColor, coreColor, crestColor);

            AddRibbon(vh, rect, time * 0.45f, 0.22f, 0.035f, 0.075f,
                new Color(crestColor.r, crestColor.g, crestColor.b, 0.25f));
            AddRibbon(vh, rect, -time * 0.32f, 0.52f, 0.045f, 0.055f,
                new Color(crestColor.r, crestColor.g, crestColor.b, 0.22f));
            AddRibbon(vh, rect, time * 0.38f, 0.79f, 0.028f, 0.045f,
                new Color(coreColor.r, coreColor.g, coreColor.b, 0.20f));

            AddRunes(vh, rect, time, crestColor);
            AddSparks(vh, rect, time, coreColor, crestColor);
            AddLeadingEdge(vh, rect, time, coreColor, crestColor);
        }

        private static void AddVerticalGradient(VertexHelper vh, Rect rect, Color bottom, Color middle, Color top)
        {
            float mid = Mathf.Lerp(rect.yMin, rect.yMax, 0.52f);
            AddQuad(vh, new Rect(rect.xMin, rect.yMin, rect.width, mid - rect.yMin), bottom, middle);
            AddQuad(vh, new Rect(rect.xMin, mid, rect.width, rect.yMax - mid), middle, top);
        }

        private static void AddRibbon(VertexHelper vh, Rect rect, float time, float lane,
            float amplitude, float thickness, Color color)
        {
            Vector2 previousTop = default;
            Vector2 previousBottom = default;
            for (int i = 0; i <= RibbonSegments; i++)
            {
                float u = i / (float)RibbonSegments;
                float wave = Mathf.Sin(u * 8f - time * 2.1f + lane * 7f) * amplitude;
                wave += Mathf.Sin(u * 15f + time * 1.15f) * amplitude * 0.16f;
                float y = rect.yMin + rect.height * Mathf.Clamp01(lane + wave);
                float half = rect.height * thickness * (0.65f + 0.35f * Mathf.Sin(u * 12f + time * 3f));
                Vector2 top = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, u), y + half);
                Vector2 bottom = new Vector2(top.x, y - half);
                if (i > 0)
                    AddQuad(vh, previousBottom, bottom, top, previousTop, color);
                previousTop = top;
                previousBottom = bottom;
            }
        }

        private static void AddSparks(VertexHelper vh, Rect rect, float time, Color core, Color crest)
        {
            for (int i = 0; i < SparkCount; i++)
            {
                float seed = Hash(i * 17.13f + 4.7f);
                float speed = Mathf.Lerp(0.10f, 0.31f, Hash(i * 3.91f));
                float travel = Mathf.Repeat(seed + time * speed, 1f);
                float vertical = Hash(i * 11.73f + Mathf.Floor(time * speed + seed) * 2.31f);
                float flicker = 0.35f + 0.65f * Mathf.Pow(Mathf.Abs(Mathf.Sin(time * (4f + seed * 5f) + i)), 3f);
                float size = rect.height * Mathf.Lerp(0.025f, 0.095f, Hash(i * 6.17f)) * flicker;
                Vector2 center = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, travel),
                    Mathf.Lerp(rect.yMin + size, rect.yMax - size, vertical));
                Color spark = Color.Lerp(new Color(core.r, core.g, core.b, 0.42f), crest, flicker);
                spark.a *= Mathf.SmoothStep(0f, 1f, Mathf.Sin(travel * Mathf.PI));
                AddDiamond(vh, center, size, size * Mathf.Lerp(0.45f, 1.7f, seed), spark);
            }
        }

        private static void AddRunes(VertexHelper vh, Rect rect, float time, Color runeColor)
        {
            for (int i = 0; i < RuneCount; i++)
            {
                float seed = Hash(i * 9.41f + 1.7f);
                float travel = Mathf.Repeat(seed + time * Mathf.Lerp(0.025f, 0.065f, Hash(i * 4.13f)), 1f);
                float lane = Mathf.Lerp(0.25f, 0.75f, Hash(i * 7.17f));
                float pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(time * 1.7f + i * 1.91f)), 2f);
                float size = rect.height * Mathf.Lerp(0.18f, 0.30f, Hash(i * 2.77f));
                Vector2 center = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, travel),
                    Mathf.Lerp(rect.yMin, rect.yMax, lane));
                Color rune = new Color(runeColor.r, runeColor.g, runeColor.b,
                    Mathf.Sin(travel * Mathf.PI) * Mathf.Lerp(0.16f, 0.52f, pulse));
                float stroke = Mathf.Max(0.65f, size * 0.085f);
                AddRune(vh, center, size, stroke, i % 4, rune);
            }
        }

        private static void AddRune(VertexHelper vh, Vector2 center, float size, float stroke, int shape, Color color)
        {
            Vector2 up = Vector2.up * size;
            Vector2 right = Vector2.right * size;
            if (shape == 0)
            {
                AddLine(vh, center - up, center + up, stroke, color);
                AddLine(vh, center - up, center + right * 0.7f, stroke, color);
                AddLine(vh, center + up, center + right * 0.7f, stroke, color);
            }
            else if (shape == 1)
            {
                AddLine(vh, center - right, center + up, stroke, color);
                AddLine(vh, center + up, center + right, stroke, color);
                AddLine(vh, center + right, center - up, stroke, color);
                AddLine(vh, center - up, center - right, stroke, color);
            }
            else if (shape == 2)
            {
                AddLine(vh, center - up, center + up, stroke, color);
                AddLine(vh, center - right, center + right, stroke, color);
                AddLine(vh, center - up * 0.65f, center + right * 0.7f, stroke, color);
            }
            else
            {
                AddLine(vh, center - right, center + right, stroke, color);
                AddLine(vh, center - right, center + up * 0.8f, stroke, color);
                AddLine(vh, center + right, center - up * 0.8f, stroke, color);
            }
        }

        private static void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float thickness, Color color)
        {
            Vector2 normal = new Vector2(-(end - start).y, (end - start).x).normalized * thickness;
            AddQuad(vh, start - normal, end - normal, end + normal, start + normal, color);
        }

        private static void AddLeadingEdge(VertexHelper vh, Rect rect, float time, Color core, Color crest)
        {
            float pulse = 0.7f + Mathf.Sin(time * 5.6f) * 0.22f;
            float width = Mathf.Min(rect.width, rect.height * 1.9f);
            Rect glow = new Rect(rect.xMax - width, rect.yMin, width, rect.height);
            AddHorizontalQuad(vh, glow, new Color(core.r, core.g, core.b, 0f),
                new Color(crest.r, crest.g, crest.b, pulse));
        }

        private static float Hash(float value)
        {
            return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
        }

        private static void AddQuad(VertexHelper vh, Rect rect, Color bottom, Color top)
        {
            int index = vh.currentVertCount;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), bottom, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), bottom, Vector2.right);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), top, Vector2.one);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), top, Vector2.up);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }

        private static void AddHorizontalQuad(VertexHelper vh, Rect rect, Color left, Color right)
        {
            int index = vh.currentVertCount;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), left, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), right, Vector2.right);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), right, Vector2.one);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), left, Vector2.up);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int index = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.right);
            vh.AddVert(c, color, Vector2.one);
            vh.AddVert(d, color, Vector2.up);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 2, index + 3, index);
        }

        private static void AddDiamond(VertexHelper vh, Vector2 center, float radiusX, float radiusY, Color color)
        {
            AddQuad(vh,
                center + Vector2.left * radiusX,
                center + Vector2.down * radiusY,
                center + Vector2.right * radiusX,
                center + Vector2.up * radiusY,
                color);
        }
    }
}
