using System.Collections.Generic;
using AccardND.GameCore;
using UnityEngine;

namespace AccardND.Battlefield
{
    /// <summary>
    /// Faccia di un dado: normale in spazio locale del modello, valore stampato
    /// e direzione "alto" del glifo sulla faccia (zero se non calibrata).
    /// </summary>
    public readonly struct DieFace
    {
        public DieFace(Vector3 normal, int value)
            : this(normal, value, Vector3.zero)
        {
        }

        public DieFace(Vector3 normal, int value, Vector3 digitUp)
        {
            Normal = normal;
            Value = value;
            DigitUp = digitUp;
        }

        public Vector3 Normal { get; }
        public int Value { get; }
        public Vector3 DigitUp { get; }
    }

    /// <summary>
    /// Costruisce i dadi 3D da Resources/DnD_Dice colorandoli in base alla classe
    /// che li tira e generando i numeri sulle facce a partire dalla mesh.
    /// </summary>
    public static class ClassDice3D
    {
        private static readonly Dictionary<HeroClass, Material> materialCache = new Dictionary<HeroClass, Material>();

        // Mappa per dado: valore stampato sul modello per ogni faccia,
        // nell'ordine deterministico prodotto da ComputeFaces. Calibrata
        // renderizzando ogni faccia dei modelli DnD_Dice e leggendo il numero
        // (2026-07); se una voce manca si usa l'euristica "facce opposte
        // sommano a N+1".
        private static readonly Dictionary<int, int[]> faceValueOverrides = new Dictionary<int, int[]>
        {
            // D4 a lettura di vertice: il "valore" di una faccia è il numero
            // stampato al vertice opposto (il dado atterra su quella faccia).
            { 4, new[] { 3, 2, 4, 1 } },
            { 6, new[] { 4, 6, 2, 5, 1, 3 } },
            { 8, new[] { 2, 3, 8, 5, 1, 4, 6, 7 } },
            { 10, new[] { 5, 4, 10, 9, 3, 8, 2, 1, 7, 6 } },
            { 12, new[] { 8, 3, 5, 6, 10, 12, 1, 11, 7, 4, 2, 9 } },
            { 20, new[] { 17, 10, 3, 8, 12, 7, 16, 15, 19, 20, 1, 2, 6, 5, 9, 14, 13, 18, 11, 4 } }
        };

        // Orientamento del glifo su ogni faccia: gradi (in senso orario,
        // guardando la faccia frontalmente) di cui il numero risulta ruotato
        // rispetto al riferimento ProjectOnPlane(Vector3.up, normale).
        // Calibrato insieme ai valori; se manca, il glifo può apparire ruotato.
        // Il D4 non serve: atterra appoggiato e i numeri all'apice sono dritti.
        private static readonly Dictionary<int, float[]> faceDigitUpAngles = new Dictionary<int, float[]>
        {
            { 6, new[] { 0f, 0f, 90f, 270f, 0f, 0f } },
            { 8, new[] { 0f, 0f, 0f, 0f, 60f, 300f, 300f, 60f } },
            { 10, new[] { 180f, 240f, 100f, 90f, 270f, 95f, 60f, 60f, 300f, 0f } },
            { 12, new[] { 15f, 300f, 60f, 90f, 90f, 0f, 180f, 270f, 90f, 270f, 120f, 0f } },
            { 20, new[] { 180f, 180f, 270f, 225f, 30f, 70f, 120f, 270f, 330f, 150f, 0f, 230f, 300f, 30f, 120f, 90f, 210f, 270f, 0f, 0f } }
        };

        /// <summary>
        /// Direzione "alto" del glifo nello spazio locale della root del dado,
        /// dato l'angolo calibrato attorno alla normale della faccia.
        /// </summary>
        private static Vector3 DigitUpFor(Vector3 localNormal, float clockwiseDegrees)
        {
            Vector3 reference = Vector3.ProjectOnPlane(Vector3.up, localNormal);
            if (reference.sqrMagnitude < 1e-4f)
                reference = Vector3.ProjectOnPlane(Vector3.forward, localNormal);
            return Quaternion.AngleAxis(clockwiseDegrees, localNormal) * reference.normalized;
        }

        public static Color BodyColor(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => new Color(0.08f, 0.05f, 0.06f),      // nero
                HeroClass.Warrior => new Color(0.55f, 0.12f, 0.1f),        // rosso acciaio
                HeroClass.Mage => new Color(0.12f, 0.2f, 0.62f),           // blu profondo
                HeroClass.Paladin => new Color(0.92f, 0.75f, 0.15f),       // giallo oro
                HeroClass.Rogue => new Color(0.28f, 0.16f, 0.38f),         // viola scuro
                HeroClass.Hunter => new Color(0.92f, 0.45f, 0.08f),        // arancione
                HeroClass.Barbarian => new Color(0.45f, 0.22f, 0.1f),      // terra bruciata
                HeroClass.Necromancer => new Color(0.07f, 0.16f, 0.1f),    // verde nerastro
                HeroClass.Priest => new Color(0.94f, 0.94f, 0.9f),         // bianco
                _ => new Color(0.4f, 0.4f, 0.45f)
            };
        }

        public static Color NumberColor(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => new Color(0.85f, 0.1f, 0.12f),       // rosso su nero
                HeroClass.Warrior => new Color(0.9f, 0.9f, 0.95f),
                HeroClass.Mage => new Color(0.55f, 0.85f, 1f),
                HeroClass.Paladin => new Color(0.25f, 0.15f, 0.04f),
                HeroClass.Rogue => new Color(0.85f, 0.82f, 0.9f),
                HeroClass.Hunter => new Color(0.12f, 0.07f, 0.03f),
                HeroClass.Barbarian => new Color(0.93f, 0.88f, 0.78f),
                HeroClass.Necromancer => new Color(0.45f, 0.95f, 0.4f),
                HeroClass.Priest => new Color(0.85f, 0.68f, 0.2f),         // oro su bianco
                _ => Color.white
            };
        }

        /// <summary>Tinta della galassia emissiva: brillante, indipendente dal corpo.</summary>
        public static Color GlowColor(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => new Color(1f, 0.05f, 0.04f),
                HeroClass.Warrior => new Color(0.45f, 0.48f, 0.52f),
                HeroClass.Mage => new Color(0.45f, 0.18f, 0.8f),
                HeroClass.Paladin => new Color(0.78f, 0.56f, 0.08f),
                HeroClass.Rogue => new Color(0.03f, 0.025f, 0.035f),
                HeroClass.Hunter => new Color(0.72f, 0.28f, 0.04f),
                HeroClass.Barbarian => new Color(0.45f, 0.22f, 0.08f),
                HeroClass.Necromancer => new Color(0.04f, 0.34f, 0.12f),
                HeroClass.Priest => new Color(0.86f, 0.78f, 0.58f),
                _ => new Color(0.8f, 0.8f, 0.9f)
            };
        }

        private static readonly Dictionary<string, Material> dedicatedMaterialCache = new Dictionary<string, Material>();

        // Variante per classe del material originale dei modelli dedicati: lo
        // shader del pacchetto espone la tinta in _Color (applicata via mask).
        private static Material GetDedicatedMaterial(int sides, HeroClass heroClass, Material baseMaterial)
        {
            if (baseMaterial == null)
                return null;

            string key = $"{sides}:{heroClass}";
            if (dedicatedMaterialCache.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Material material = new Material(baseMaterial) { name = $"D{sides} {heroClass}" };
            material.SetColor("_Color", GlowColor(heroClass));
            dedicatedMaterialCache[key] = material;
            return material;
        }

        /// <summary>
        /// Variante del material dedicato con un glow arbitrario, per dadi che
        /// non appartengono a una classe (es. iniziativa: blu/rosso).
        /// </summary>
        public static Material GetCustomGlowMaterial(int sides, string key, Color glow)
        {
            string cacheKey = $"{sides}:custom:{key}";
            if (dedicatedMaterialCache.TryGetValue(cacheKey, out Material cached) && cached != null)
                return cached;

            Material baseMaterial = Resources.Load<Material>($"DnD_Dice/Material/D{sides}");
            if (baseMaterial == null)
                return null;
            Material material = new Material(baseMaterial) { name = $"D{sides} {key}" };
            material.SetColor("_Color", glow);
            dedicatedMaterialCache[cacheKey] = material;
            return material;
        }

        public static Material GetMaterial(HeroClass heroClass)
        {
            if (materialCache.TryGetValue(heroClass, out Material cached) && cached != null)
                return cached;

            Color body = BodyColor(heroClass);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = $"Die {heroClass}" };
            material.SetColor("_BaseColor", body);
            material.SetColor("_Color", body);
            material.SetFloat("_Smoothness", 0.55f);
            material.SetFloat("_Glossiness", 0.55f);
            materialCache[heroClass] = material;
            return material;
        }

        /// <summary>
        /// Istanzia il dado D{sides} da Resources/DnD_Dice con materiale della classe
        /// e numeri sulle facce. Ritorna null se la risorsa non esiste.
        /// </summary>
        private static readonly Dictionary<int, string> modelPaths = new Dictionary<int, string>
        {
            { 4, "DnD_Dice/Mesh/00_D4" },
            { 6, "DnD_Dice/Mesh/00_D6" },
            { 8, "DnD_Dice/Mesh/00_D8" },
            { 10, "DnD_Dice/Mesh/00_D10" },
            { 12, "DnD_Dice/Mesh/00_D12" },
            { 20, "DnD_Dice/Mesh/00_D20" }
        };

        public static GameObject Create(int sides, HeroClass heroClass, Transform parent = null)
        {
            if (!modelPaths.TryGetValue(sides, out string modelPath))
            {
                Debug.LogError($"[Accard N' Die] Dado D{sides} non configurato.");
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(modelPath);
            if (prefab == null)
            {
                Debug.LogError($"[Accard N' Die] Dado '{modelPath}' non trovato in Resources.");
                return null;
            }

            GameObject die = Object.Instantiate(prefab, parent, false);
            die.name = $"D{sides} {heroClass}";

            ApplyMaterials(die, sides, heroClass);
            return die;
        }

        public static void ApplyMaterials(GameObject die, int sides, HeroClass heroClass)
        {
            if (die == null)
                return;

            foreach (Renderer renderer in die.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Material dedicatedMaterial = Resources.Load<Material>($"DnD_Dice/Material/D{sides}");
                Material baseMaterial = dedicatedMaterial != null ? dedicatedMaterial : renderer.sharedMaterial;
                renderer.sharedMaterial = GetDedicatedMaterial(sides, heroClass, baseMaterial);
            }
        }

        /// <summary>
        /// Facce del dado istanziato: normali nello spazio locale della root del
        /// dado e valore stampato su ciascuna faccia. Lista vuota se la mesh non
        /// è leggibile.
        /// </summary>
        public static List<DieFace> GetFaces(GameObject dieInstance, int sides)
        {
            var result = new List<DieFace>();
            MeshFilter meshFilter = dieInstance.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null || !meshFilter.sharedMesh.isReadable)
                return result;

            faceDigitUpAngles.TryGetValue(sides, out float[] digitAngles);
            int faceIndex = 0;
            foreach (Face face in ComputeFaces(meshFilter.sharedMesh, sides))
            {
                Vector3 worldNormal = meshFilter.transform.TransformDirection(face.Normal);
                Vector3 localNormal = dieInstance.transform.InverseTransformDirection(worldNormal).normalized;
                Vector3 digitUp = Vector3.zero;
                if (digitAngles != null && faceIndex < digitAngles.Length)
                {
                    // L'angolo è calibrato sulla normale nello spazio della
                    // root: il riferimento va costruito lì e poi riportato.
                    Vector3 rootDigitUp = DigitUpFor(localNormal, digitAngles[faceIndex]);
                    digitUp = rootDigitUp;
                }
                result.Add(new DieFace(localNormal, face.Number, digitUp));
                faceIndex++;
            }
            return result;
        }

        private sealed class Face
        {
            public Vector3 Normal;
            public Vector3 Centroid;
            public float Area;
            public int Number;
        }

        // Estrae le facce reali del dado dalla mesh, con normali verso l'esterno,
        // in ordine deterministico, e assegna a ognuna il proprio valore.
        //
        // Le facce sono i minimi locali della funzione di supporto: su un solido
        // convesso la distanza dal centro al piano d'appoggio è minima esattamente
        // in direzione delle facce e massima su spigoli e vertici. Funziona anche
        // con facce bombate e spigoli molto arrotondati, dove il clustering dei
        // triangoli per normale confonde le facce con gli spigoli.
        private static List<Face> ComputeFaces(Mesh mesh, int expectedFaces)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3 center = mesh.bounds.center;
            var offsets = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                offsets[i] = vertices[i] - center;

            // Campionamento uniforme della sfera (spirale di Fibonacci).
            const int sampleCount = 768;
            var samples = new List<(Vector3 direction, float support)>(sampleCount);
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < sampleCount; i++)
            {
                float y = 1f - (i + 0.5f) * 2f / sampleCount;
                float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float theta = golden * i;
                var direction = new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius);
                samples.Add((direction, Support(offsets, direction)));
            }
            samples.Sort((a, b) => a.support.CompareTo(b.support));

            // Dalle direzioni più "piatte" in su, tenendo solo direzioni ben
            // separate: 30° è sotto la distanza minima tra facce adiacenti di
            // qualunque dado (il D20 è il più fitto, ~42°). Il D4 ha facce a
            // 109°: una soglia più larga scarta i falsi minimi creati dai
            // numeri in rilievo e dagli smussi.
            float minSeparationDot = expectedFaces == 4 ? 0.5f : 0.866f;
            var faces = new List<Face>();
            foreach ((Vector3 direction, float _) in samples)
            {
                if (HasCloseNormal(faces, direction, minSeparationDot))
                    continue;
                Vector3 refined = RefineFaceDirection(offsets, direction);
                if (HasCloseNormal(faces, refined, minSeparationDot))
                    continue;
                float support = Support(offsets, refined);
                faces.Add(new Face { Normal = refined, Centroid = center + refined * support });
                if (faces.Count == expectedFaces)
                    break;
            }
            if (faces.Count < expectedFaces)
                Debug.LogWarning($"[Accard N' Die] D{expectedFaces}: trovate solo {faces.Count} facce nella mesh.");

            // Raggio stimato della faccia (per dimensionare le etichette): metà
            // dell'angolo verso la faccia più vicina, proiettato sulla superficie.
            foreach (Face face in faces)
            {
                float minAngle = 180f;
                foreach (Face other in faces)
                {
                    if (other == face)
                        continue;
                    minAngle = Mathf.Min(minAngle, Vector3.Angle(face.Normal, other.Normal));
                }
                float supportDistance = Vector3.Distance(face.Centroid, center);
                float faceRadius = supportDistance * Mathf.Tan(minAngle * Mathf.Deg2Rad * 0.5f);
                face.Area = faceRadius * faceRadius;
            }

            // Ordine deterministico, indipendente dall'ordine dei triangoli:
            // serve perché le mappe di calibrazione facce→valori siano stabili.
            faces.Sort((x, y) =>
            {
                int byHeight = y.Normal.y.CompareTo(x.Normal.y);
                if (byHeight != 0)
                    return byHeight;
                return Mathf.Atan2(x.Normal.x, x.Normal.z).CompareTo(Mathf.Atan2(y.Normal.x, y.Normal.z));
            });

            if (faceValueOverrides.TryGetValue(expectedFaces, out int[] values) && values.Length == faces.Count)
            {
                for (int i = 0; i < faces.Count; i++)
                    faces[i].Number = values[i];
            }
            else
            {
                AssignNumbers(faces);
            }
            return faces;
        }

        private static void AddFaceNumbers(MeshFilter meshFilter, int expectedFaces, Color numberColor)
        {
            List<Face> faces = ComputeFaces(meshFilter.sharedMesh, expectedFaces);

            Font font = AccardND.Battlefield.MmoUiTheme.BodyFont;
            Transform meshTransform = meshFilter.transform;
            foreach (Face face in faces)
            {
                float faceRadius = Mathf.Sqrt(face.Area);
                var labelGo = new GameObject($"Face {face.Number}");
                labelGo.transform.SetParent(meshTransform, false);
                labelGo.transform.localPosition = face.Centroid + face.Normal * (faceRadius * 0.16f);
                labelGo.transform.localRotation = FaceRotation(face.Normal);

                TextMesh text = labelGo.AddComponent<TextMesh>();
                text.text = face.Number.ToString();
                text.font = font;
                text.fontSize = 64;
                text.fontStyle = FontStyle.Bold;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = numberColor;
                // Altezza del glifo ~ characterSize * fontSize * 0.1
                text.characterSize = faceRadius * 0.6f * 10f / text.fontSize;
                labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        // Distanza dal centro al piano d'appoggio nella direzione data.
        private static float Support(Vector3[] offsets, Vector3 direction)
        {
            float max = float.MinValue;
            for (int i = 0; i < offsets.Length; i++)
            {
                float dot = Vector3.Dot(offsets[i], direction);
                if (dot > max)
                    max = dot;
            }
            return max;
        }

        private static bool HasCloseNormal(List<Face> faces, Vector3 direction, float minDot)
        {
            foreach (Face face in faces)
            {
                if (Vector3.Dot(face.Normal, direction) > minDot)
                    return true;
            }
            return false;
        }

        // Discesa locale sulla sfera: piccole rotazioni della direzione finché la
        // funzione di supporto non smette di diminuire. Converge al centro faccia.
        private static Vector3 RefineFaceDirection(Vector3[] offsets, Vector3 direction)
        {
            Vector3 best = direction.normalized;
            float bestSupport = Support(offsets, best);
            float step = 4f;
            for (int iteration = 0; iteration < 40 && step > 0.05f; iteration++)
            {
                Vector3 tangentA = Vector3.Cross(best, Mathf.Abs(best.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 tangentB = Vector3.Cross(best, tangentA);
                bool improved = false;
                for (int candidateIndex = 0; candidateIndex < 4; candidateIndex++)
                {
                    Vector3 axis = candidateIndex switch
                    {
                        0 => tangentA,
                        1 => -tangentA,
                        2 => tangentB,
                        _ => -tangentB
                    };
                    Vector3 candidate = Quaternion.AngleAxis(step, axis) * best;
                    float support = Support(offsets, candidate);
                    if (support < bestSupport)
                    {
                        bestSupport = support;
                        best = candidate;
                        improved = true;
                        break;
                    }
                }
                if (!improved)
                    step *= 0.6f;
            }
            return best.normalized;
        }

        // Assegna i numeri in modo che facce opposte sommino a N+1, come su un dado vero.
        private static void AssignNumbers(List<Face> faces)
        {
            int total = faces.Count;
            var ordered = new List<Face>(faces);
            ordered.Sort((x, y) => y.Normal.y.CompareTo(x.Normal.y));

            var assigned = new HashSet<Face>();
            int low = 1;
            foreach (Face face in ordered)
            {
                if (assigned.Contains(face))
                    continue;
                face.Number = low;
                assigned.Add(face);

                Face opposite = null;
                float bestDot = -0.5f;
                foreach (Face candidate in ordered)
                {
                    if (assigned.Contains(candidate))
                        continue;
                    float dot = Vector3.Dot(face.Normal, candidate.Normal);
                    if (dot < bestDot)
                    {
                        bestDot = dot;
                        opposite = candidate;
                    }
                }
                if (opposite != null)
                {
                    opposite.Number = total + 1 - low;
                    assigned.Add(opposite);
                }
                low++;
            }
        }

        private static Quaternion FaceRotation(Vector3 normal)
        {
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal);
            if (up.sqrMagnitude < 1e-4f)
                up = Vector3.ProjectOnPlane(Vector3.forward, normal);
            return Quaternion.LookRotation(-normal, up.normalized);
        }
    }
}
