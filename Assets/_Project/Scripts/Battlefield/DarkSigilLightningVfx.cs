using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    /// <summary>
    /// Scariche procedurali intermittenti che avvolgono una pedina con Sigillo Oscuro.
    /// La geometria viene rigenerata a ogni impulso: nessuna animazione si ripete in loop.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class DarkSigilLightningVfx : MaskableGraphic
    {
        private readonly List<Bolt> bolts = new();
        private float nextBurstAt;
        private float burstEndsAt;
        private float nextFlickerAt;
        private int flickerIndex;
        private bool bursting;
        private bool warriorSupremeLoop;
		private bool necromanticTransformation;
        private Color haloColor = new(0.45f, 0f, 0.025f, 0.18f);
        private Color bodyColor = new(1f, 0.015f, 0.025f, 0.9f);
        private Color darkCoreColor = new(0.008f, 0f, 0.012f, 1f);
        private Color brightCoreColor = new(1f, 0.3f, 0.24f, 1f);
        private Color sparkColor = new(1f, 0.04f, 0.025f, 0.62f);

        private struct Bolt
        {
            public List<Vector2> Points;
            public bool BlackCore;
            public float Width;
            public float Phase;
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            color = Color.white;
            nextBurstAt = Time.unscaledTime + Random.Range(0.12f, 0.65f);
        }

        /// <summary>
        /// Riusa la scarica del Sigillo Oscuro sulla pedina potenziata. Dopo l'attivazione
        /// continua a produrre impulsi frequenti, ma con cadenza casuale e non in loop continuo.
        /// </summary>
        internal void ConfigureWarriorSupreme()
        {
            warriorSupremeLoop = true;
            haloColor = new Color(0f, 0.1f, 0.42f, 0.32f);
            bodyColor = new Color(0.015f, 0.42f, 1f, 0.94f);
            darkCoreColor = new Color(0f, 0.005f, 0.025f, 1f);
            brightCoreColor = new Color(0.42f, 0.94f, 1f, 1f);
            sparkColor = new Color(0.2f, 0.82f, 1f, 0.78f);
            nextBurstAt = Time.unscaledTime + Random.Range(0.08f, 0.28f);
        }

		public void ConfigureNecromanticTransformation()
		{
			warriorSupremeLoop = true;
			necromanticTransformation = true;
			haloColor = new Color(0.08f, 0.55f, 0.12f, 0.34f);
			bodyColor = new Color(0.12f, 1f, 0.28f, 1f);
			darkCoreColor = new Color(0f, 0.055f, 0.015f, 1f);
			brightCoreColor = new Color(0.62f, 1f, 0.68f, 1f);
			sparkColor = new Color(0.56f, 1f, 0.62f, 0.96f);
			nextBurstAt = Time.unscaledTime;
		}

        private void Update()
        {
            float now = Time.unscaledTime;
            if (!bursting)
            {
                if (now < nextBurstAt)
                    return;

                BeginBurst(now);
                return;
            }

            if (now >= burstEndsAt)
            {
                bursting = false;
                bolts.Clear();
                canvasRenderer.Clear();
                if (warriorSupremeLoop)
                {
                    // La Suprema mantiene il VFX sempre vivo: ogni impulso viene subito
                    // sostituito da una nuova scarica casuale finche' il componente resta attivo.
                    BeginBurst(now);
                    return;
                }

                nextBurstAt = now + Random.Range(0.3f, 1.45f);
                return;
            }

            if (now >= nextFlickerAt)
            {
                flickerIndex++;
                BuildBolts();
                nextFlickerAt = now + Random.Range(0.028f, 0.065f);
                SetVerticesDirty();
            }
        }

        private void BeginBurst(float now)
        {
            bursting = true;
            flickerIndex = 0;
            burstEndsAt = now + (warriorSupremeLoop
                ? Random.Range(0.2f, 0.46f)
                : Random.Range(0.16f, 0.42f));
            nextFlickerAt = now;
            BuildBolts();
            SetVerticesDirty();
        }

        private void BuildBolts()
        {
            bolts.Clear();
            Rect r = rectTransform.rect;
            int count = necromanticTransformation
				? Random.Range(8, 13)
				: warriorSupremeLoop ? Random.Range(3, 7) : Random.Range(2, 5);
            for (int i = 0; i < count; i++)
            {
                Vector2 start = RandomPerimeterPoint(r, Random.value);
				float travel = necromanticTransformation
					? Random.Range(0.24f, 0.52f)
					: Random.Range(0.12f, 0.34f);
                Vector2 end = RandomPerimeterPoint(r, Mathf.Repeat(Random.value + travel, 1f));
				AddBolt(start, end,
					necromanticTransformation ? Random.Range(9, 14) : Random.Range(6, 10),
					necromanticTransformation ? Random.Range(2.8f, 4.6f) : Random.Range(1.7f, 3.2f),
					Random.value > 0.46f);

                if (Random.value < 0.72f && bolts.Count > 0)
                {
                    List<Vector2> trunk = bolts[bolts.Count - 1].Points;
                    Vector2 origin = trunk[Random.Range(2, Mathf.Max(3, trunk.Count - 2))];
                    Vector2 outward = (origin - r.center).normalized;
                    Vector2 branchEnd = origin + outward * Random.Range(18f, 48f)
                        + Random.insideUnitCircle * 18f;
                    AddBolt(origin, branchEnd, Random.Range(3, 6), Random.Range(0.75f, 1.35f), Random.value > 0.32f);
                }
            }
        }

        private void AddBolt(Vector2 start, Vector2 end, int segments, float width, bool blackCore)
        {
            var points = new List<Vector2>(segments + 1) { start };
            Vector2 direction = end - start;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized;
            float amplitude = Mathf.Clamp(direction.magnitude * 0.105f, 7f, 22f);
            for (int i = 1; i < segments; i++)
            {
                float t = i / (float)segments;
                float taper = Mathf.Sin(t * Mathf.PI);
                Vector2 jitter = normal * Random.Range(-amplitude, amplitude) * taper;
                jitter += Random.insideUnitCircle * amplitude * 0.18f;
                points.Add(Vector2.Lerp(start, end, t) + jitter);
            }
            points.Add(end);
            bolts.Add(new Bolt { Points = points, Width = width, BlackCore = blackCore, Phase = Random.value * 6.28f });
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!bursting)
                return;

            float life = Mathf.InverseLerp(burstEndsAt, burstEndsAt - 0.16f, Time.unscaledTime);
            float pulse = Mathf.Clamp01(0.5f + Mathf.Sin((flickerIndex * 2.37f) + Time.unscaledTime * 43f) * 0.5f);
            float alpha = Mathf.Clamp01(Mathf.Max(0.38f, pulse) * Mathf.Lerp(0.35f, 1f, life));

            foreach (Bolt bolt in bolts)
            {
                // Alone rosso trasparente, corpo cremisi e nucleo nero/rosso: tre passate
                // danno spessore alle scariche anche sopra illustrazioni molto luminose.
                Color halo = haloColor; halo.a *= alpha;
                Color body = bodyColor; body.a *= alpha;
                DrawPolyline(vh, bolt.Points, bolt.Width * 4.8f, halo);
                DrawPolyline(vh, bolt.Points, bolt.Width * 2.15f, body);
                Color core = bolt.BlackCore
                    ? darkCoreColor
                    : brightCoreColor;
                core.a *= alpha;
                DrawPolyline(vh, bolt.Points, bolt.Width * 0.72f, core);

                for (int i = 1; i < bolt.Points.Count - 1; i += 2)
                {
                    float spark = bolt.Width * Random.Range(1.3f, 2.3f);
                    Color sparkTint = sparkColor; sparkTint.a *= alpha;
                    AddQuad(vh, bolt.Points[i] - Vector2.one * spark, bolt.Points[i] + Vector2.one * spark, sparkTint);
                }
            }
        }

        private static void DrawPolyline(VertexHelper vh, List<Vector2> points, float width, Color color)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[i + 1];
                Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * (width * 0.5f);
                AddQuad(vh, a - n, a + n, b + n, b - n, color);
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2 min, Vector2 max, Color color)
        {
            AddQuad(vh, new Vector2(min.x, min.y), new Vector2(min.x, max.y),
                new Vector2(max.x, max.y), new Vector2(max.x, min.y), color);
        }

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int index = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = color;
            v.position = a; vh.AddVert(v);
            v.position = b; vh.AddVert(v);
            v.position = c; vh.AddVert(v);
            v.position = d; vh.AddVert(v);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private static Vector2 RandomPerimeterPoint(Rect r, float t)
        {
            t = Mathf.Repeat(t, 1f) * 4f;
            const float inset = 4f;
            return t < 1f ? new Vector2(Mathf.Lerp(r.xMin + inset, r.xMax - inset, t), r.yMax - inset)
                : t < 2f ? new Vector2(r.xMax - inset, Mathf.Lerp(r.yMax - inset, r.yMin + inset, t - 1f))
                : t < 3f ? new Vector2(Mathf.Lerp(r.xMax - inset, r.xMin + inset, t - 2f), r.yMin + inset)
                : new Vector2(r.xMin + inset, Mathf.Lerp(r.yMin + inset, r.yMax - inset, t - 3f));
        }
    }
}
