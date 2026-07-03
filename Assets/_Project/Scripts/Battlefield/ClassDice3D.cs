using System.Collections.Generic;
using AccardND.GameCore;
using UnityEngine;

namespace AccardND.Battlefield
{
    /// <summary>Faccia di un dado: normale in spazio locale del modello e valore stampato.</summary>
    public readonly struct DieFace
    {
        public DieFace(Vector3 normal, int value)
        {
            Normal = normal;
            Value = value;
        }

        public Vector3 Normal { get; }
        public int Value { get; }
    }

    /// <summary>
    /// Costruisce i dadi 3D da Resources/Dice colorandoli in base alla classe
    /// che li tira e generando i numeri sulle facce a partire dalla mesh.
    /// </summary>
    public static class ClassDice3D
    {
        private static readonly Dictionary<HeroClass, Material> materialCache = new Dictionary<HeroClass, Material>();

        // Mappa per dado: valore stampato sulla texture per ogni faccia,
        // nell'ordine deterministico prodotto da ComputeFaces. Da calibrare
        // guardando i modelli; finché una voce manca si usa l'euristica
        // "facce opposte sommano a N+1".
        private static readonly Dictionary<int, int[]> faceValueOverrides = new Dictionary<int, int[]>();

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
                HeroClass.Assassin => new Color(0.9f, 0.15f, 0.2f),
                HeroClass.Warrior => new Color(1f, 0.4f, 0.3f),
                HeroClass.Mage => new Color(0.4f, 0.6f, 1f),
                HeroClass.Paladin => new Color(1f, 0.85f, 0.35f),
                HeroClass.Rogue => new Color(0.75f, 0.45f, 1f),
                HeroClass.Hunter => new Color(1f, 0.55f, 0.2f),
                HeroClass.Barbarian => new Color(1f, 0.45f, 0.25f),
                HeroClass.Necromancer => new Color(0.35f, 1f, 0.45f),
                HeroClass.Priest => new Color(1f, 0.92f, 0.65f),
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

        public static Material GetMaterial(HeroClass heroClass)
        {
            if (materialCache.TryGetValue(heroClass, out Material cached) && cached != null)
                return cached;

            Color body = BodyColor(heroClass);
            Material baseMaterial = Resources.Load<Material>("Dice/Space");
            Material material;
            if (baseMaterial != null)
            {
                // Material del pack con i numeri già disegnati nelle texture:
                // basta tingerlo con il colore della classe. La texture emissiva
                // ha i numeri neri, quindi restano leggibili su ogni tinta.
                material = new Material(baseMaterial) { name = $"Die {heroClass}" };
                material.SetColor("_BaseColor", body);
                material.SetColor("_Color", body);
                // La galassia emissiva usa il colore di bagliore della classe,
                // così resta visibile anche sui dadi con corpo scuro.
                material.SetColor("_EmissionColor", GlowColor(heroClass) * 0.85f);
                material.EnableKeyword("_EMISSION");
                // Superficie opaca: con smoothness alta il riflesso della luce e
                // del cielo schiarisce il dado a prescindere dal colore base.
                material.SetFloat("_Smoothness", 0.15f);
                material.SetFloat("_EnvironmentReflections", 0f);
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                material = new Material(shader) { name = $"Die {heroClass}" };
                material.SetColor("_BaseColor", body);
                material.SetColor("_Color", body);
                material.SetFloat("_Smoothness", 0.55f);
                material.SetFloat("_Glossiness", 0.55f);
            }
            materialCache[heroClass] = material;
            return material;
        }

        /// <summary>
        /// Istanzia il dado D{sides} da Resources/Dice con materiale della classe
        /// e numeri sulle facce. Ritorna null se la risorsa non esiste.
        /// </summary>
        // Modelli dedicati (pacchetto DnD_Dice) che usano il proprio material
        // originale invece del material tinto per classe.
        private static readonly Dictionary<int, string> dedicatedModels = new Dictionary<int, string>
        {
            { 12, "DnD_Dice/Mesh/00_D12" }
        };

        public static GameObject Create(int sides, HeroClass heroClass, Transform parent = null)
        {
            GameObject prefab = null;
            bool dedicated = dedicatedModels.TryGetValue(sides, out string dedicatedPath);
            if (dedicated)
            {
                prefab = Resources.Load<GameObject>(dedicatedPath);
                dedicated = prefab != null;
            }
            if (prefab == null)
                prefab = Resources.Load<GameObject>($"Dice/D{sides}");
            if (prefab == null)
            {
                Debug.LogError($"[Accard N' Die] Dado 'Dice/D{sides}' non trovato in Resources.");
                return null;
            }

            GameObject die = Object.Instantiate(prefab, parent, false);
            die.name = $"D{sides} {heroClass}";

            if (dedicated)
            {
                foreach (Renderer renderer in die.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.sharedMaterial = GetDedicatedMaterial(sides, heroClass, renderer.sharedMaterial);
                }
                return die;
            }

            Material material = GetMaterial(heroClass);
            foreach (Renderer renderer in die.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Con le texture del pack i numeri sono già sulle facce; i TextMesh
            // generati dalla mesh servono solo come fallback senza texture.
            if (material.GetTexture("_BaseMap") == null && material.GetTexture("_MainTex") == null)
            {
                MeshFilter meshFilter = die.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.isReadable)
                    AddFaceNumbers(meshFilter, sides, NumberColor(heroClass));
                else
                    Debug.LogWarning($"[Accard N' Die] Mesh del D{sides} non leggibile: numeri sulle facce saltati.");
            }

            return die;
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

            foreach (Face face in ComputeFaces(meshFilter.sharedMesh, sides))
            {
                Vector3 worldNormal = meshFilter.transform.TransformDirection(face.Normal);
                Vector3 localNormal = dieInstance.transform.InverseTransformDirection(worldNormal).normalized;
                result.Add(new DieFace(localNormal, face.Number));
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
            // qualunque dado (il D20 è il più fitto, ~42°).
            const float minSeparationDot = 0.866f;
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

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
