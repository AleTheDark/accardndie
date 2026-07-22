using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    [InitializeOnLoad]
    public static class ComposableGolemAssetBuilder
    {
        private const string RootFolder = "Assets/Resources/Minibosses/ComposableGolem";
        private const string MeshFolder = RootFolder + "/Meshes";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string OrbitRingPrefabPath = PrefabFolder + "/GolemOrbitingMaterials_Ring.prefab";

        static ComposableGolemAssetBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        [MenuItem("Accard N' Die/Rebuild Composable Golem Assets", priority = 42)]
        public static void RebuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Directory.CreateDirectory(MeshFolder);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);

            Mesh crystalMesh = SaveMesh("Golem_Crystal_Octahedron", CreateOctahedronMesh());
            Mesh shardMesh = SaveMesh("Golem_Crystal_Shard", CreateShardMesh());
            Mesh glassPaneMesh = SaveMesh("Golem_Glass_Pane", CreateGlassPaneMesh());
            Mesh ironChunkMesh = SaveMesh("Golem_Iron_Chunk", CreateIronChunkMesh());
            Mesh ringMesh = SaveMesh("Golem_Thin_Orbit_Ring", CreateRingMesh(48, 0.58f, 0.018f));

            Material crystal = SaveMaterial("MAT_Golem_Crystal", new Color(0.05f, 0.82f, 1f, 0.76f), 0.02f, 0.92f, true, new Color(0.0f, 0.78f, 1f, 1f), 1.8f);
            Material crystalCore = SaveMaterial("MAT_Golem_Crystal_Core", new Color(0.62f, 0.96f, 1f, 0.92f), 0f, 0.95f, true, new Color(0.0f, 0.95f, 1f, 1f), 2.8f);
            Material glass = SaveMaterial("MAT_Golem_Glass", new Color(0.72f, 1f, 0.9f, 0.34f), 0f, 1f, true, new Color(0.32f, 1f, 0.82f, 1f), 0.72f);
            Material glassEdge = SaveMaterial("MAT_Golem_Glass_Edge", new Color(0.88f, 1f, 0.96f, 0.58f), 0f, 1f, true, new Color(0.5f, 1f, 0.9f, 1f), 1.35f);
            Material iron = SaveMaterial("MAT_Golem_Iron", new Color(0.52f, 0.5f, 0.46f, 1f), 0.92f, 0.42f, false, Color.black, 0f);
            Material ironHot = SaveMaterial("MAT_Golem_Iron_HotEdge", new Color(1f, 0.52f, 0.18f, 1f), 0.18f, 0.68f, false, new Color(1f, 0.34f, 0.05f, 1f), 1.9f);
            Material spark = SaveMaterial("MAT_Golem_Hit_Sparks", new Color(1f, 0.76f, 0.24f, 1f), 0f, 0.72f, false, new Color(1f, 0.55f, 0.1f, 1f), 2.4f);

            GameObject crystalOrbit = BuildCrystalOrbit(crystalMesh, shardMesh, ringMesh, crystal, crystalCore);
            SavePrefab(crystalOrbit, "GolemOrbit_Crystal");
            GameObject glassOrbit = BuildGlassOrbit(glassPaneMesh, ringMesh, glass, glassEdge);
            SavePrefab(glassOrbit, "GolemOrbit_Glass");
            GameObject ironOrbit = BuildIronOrbit(ironChunkMesh, ringMesh, iron, ironHot);
            SavePrefab(ironOrbit, "GolemOrbit_Iron");

            SavePrefab(BuildCrystalHit(shardMesh, crystal, crystalCore), "GolemHit_Crystal_Shatter");
            SavePrefab(BuildGlassHit(glassPaneMesh, glass, glassEdge), "GolemHit_Glass_Splinters");
            SavePrefab(BuildIronHit(ironChunkMesh, iron, ironHot, spark), "GolemHit_Iron_Sparks");
            SavePrefab(BuildOrbitRing(), "GolemOrbitingMaterials_Ring");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Accard N' Die] Asset golem componibile rigenerati in {RootFolder}");
        }

        private static void BuildIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OrbitRingPrefabPath) == null)
                RebuildAll();
        }

        private static GameObject BuildCrystalOrbit(Mesh crystalMesh, Mesh shardMesh, Mesh ringMesh, Material crystal, Material core)
        {
            GameObject root = NewRoot("GolemOrbit_Crystal");
            AddMesh(root.transform, "Crystal Core", crystalMesh, core, Vector3.zero, Quaternion.Euler(0f, 18f, 0f), new Vector3(0.42f, 0.72f, 0.42f));
            AddMesh(root.transform, "Inner Orbit Halo", ringMesh, crystal, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.03f, 0.48f);
                AddMesh(root.transform, "Floating Crystal Shard " + (i + 1), shardMesh, crystal, position, Quaternion.Euler(18f, angle + 20f, 35f), new Vector3(0.16f, 0.28f, 0.16f));
            }

            return root;
        }

        private static GameObject BuildGlassOrbit(Mesh paneMesh, Mesh ringMesh, Material glass, Material edge)
        {
            GameObject root = NewRoot("GolemOrbit_Glass");
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.02f, 0.34f);
                AddMesh(root.transform, "Glass Pane " + (i + 1), paneMesh, glass, position, Quaternion.Euler(8f, angle + 28f, i % 2 == 0 ? 8f : -11f), new Vector3(0.3f, 0.48f, 0.3f));
            }

            AddMesh(root.transform, "Thin Glass Orbit", ringMesh, edge, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.92f, 0.92f, 0.92f));
            AddPrimitive(root.transform, "Glass Refraction Pearl", PrimitiveType.Sphere, edge, new Vector3(0f, 0.18f, 0f), Quaternion.identity, new Vector3(0.18f, 0.18f, 0.18f));
            return root;
        }

        private static GameObject BuildIronOrbit(Mesh chunkMesh, Mesh ringMesh, Material iron, Material hot)
        {
            GameObject root = NewRoot("GolemOrbit_Iron");
            AddMesh(root.transform, "Iron Core", chunkMesh, iron, Vector3.zero, Quaternion.Euler(10f, 28f, 4f), new Vector3(0.62f, 0.46f, 0.52f));
            AddMesh(root.transform, "Forged Orbit Band", ringMesh, iron, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(1.04f, 1.04f, 1.04f));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.02f, 0.48f);
                AddPrimitive(root.transform, "Iron Rivet " + (i + 1), PrimitiveType.Sphere, i % 3 == 0 ? hot : iron, position, Quaternion.identity, new Vector3(0.08f, 0.08f, 0.08f));
            }

            return root;
        }

        private static GameObject BuildCrystalHit(Mesh shardMesh, Material crystal, Material core)
        {
            GameObject root = NewRoot("GolemHit_Crystal_Shatter");
            AddPrimitive(root.transform, "Cyan Impact Flash", PrimitiveType.Sphere, core, Vector3.zero, Quaternion.identity, new Vector3(0.34f, 0.34f, 0.34f));
            for (int i = 0; i < 12; i++)
            {
                float yaw = i * 30f;
                float height = (i % 4 - 1.5f) * 0.09f;
                Vector3 position = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, height, 0.34f + (i % 3) * 0.07f);
                AddMesh(root.transform, "Crystal Impact Shard " + (i + 1), shardMesh, crystal, position, Quaternion.Euler(28f + i * 7f, yaw, 46f), new Vector3(0.1f, 0.25f, 0.1f));
            }

            return root;
        }

        private static GameObject BuildGlassHit(Mesh paneMesh, Material glass, Material edge)
        {
            GameObject root = NewRoot("GolemHit_Glass_Splinters");
            for (int i = 0; i < 14; i++)
            {
                float yaw = i * 25.714f;
                Vector3 position = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, (i % 5 - 2) * 0.05f, 0.28f + (i % 4) * 0.06f);
                AddMesh(root.transform, "Glass Splinter " + (i + 1), paneMesh, i % 2 == 0 ? glass : edge, position, Quaternion.Euler(i * 13f, yaw + 90f, i * 9f), new Vector3(0.12f, 0.27f, 0.12f));
            }

            AddPrimitive(root.transform, "Glass White Flash", PrimitiveType.Sphere, edge, Vector3.zero, Quaternion.identity, new Vector3(0.22f, 0.22f, 0.22f));
            return root;
        }

        private static GameObject BuildIronHit(Mesh chunkMesh, Material iron, Material hot, Material spark)
        {
            GameObject root = NewRoot("GolemHit_Iron_Sparks");
            AddMesh(root.transform, "Dented Iron Plate", chunkMesh, iron, Vector3.zero, Quaternion.Euler(-8f, 18f, 0f), new Vector3(0.5f, 0.28f, 0.58f));
            for (int i = 0; i < 10; i++)
            {
                float yaw = -70f + i * 14f;
                Vector3 position = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.1f + (i % 3) * 0.04f, 0.34f);
                AddPrimitive(root.transform, "Iron Spark " + (i + 1), PrimitiveType.Capsule, i % 4 == 0 ? hot : spark, position, Quaternion.Euler(68f, yaw, 0f), new Vector3(0.025f, 0.13f, 0.025f));
            }

            return root;
        }

        private static GameObject BuildOrbitRing()
        {
            GameObject root = NewRoot("GolemOrbitingMaterials_Ring");
            GameObject crystal = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/GolemOrbit_Crystal.prefab"));
            GameObject glass = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/GolemOrbit_Glass.prefab"));
            GameObject iron = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/GolemOrbit_Iron.prefab"));
            ParentOrbitItem(crystal, root.transform, "Orbit Slot - Cristallo", new Vector3(-0.95f, 0f, 0.55f), 24f);
            ParentOrbitItem(glass, root.transform, "Orbit Slot - Vetro", new Vector3(0.95f, 0f, 0.55f), -24f);
            ParentOrbitItem(iron, root.transform, "Orbit Slot - Ferro", new Vector3(0f, 0f, -1.08f), 0f);
            return root;
        }

        private static void ParentOrbitItem(GameObject item, Transform parent, string name, Vector3 position, float yaw)
        {
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            item.transform.localScale = Vector3.one * 0.58f;
        }

        private static GameObject NewRoot(string name)
        {
            return new GameObject(name);
        }

        private static void SavePrefab(GameObject root, string fileName)
        {
            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + fileName + ".prefab");
            Object.DestroyImmediate(root);
        }

        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = MeshFolder + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static Material SaveMaterial(string name, Color baseColor, float metallic, float smoothness, bool transparent, Color emission, float emissionPower)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.name = name;
            material.color = baseColor;
            SetColor(material, "_BaseColor", baseColor);
            SetColor(material, "_Color", baseColor);
            SetFloat(material, "_Metallic", metallic);
            SetFloat(material, "_Smoothness", smoothness);
            SetColor(material, "_EmissionColor", emission * emissionPower);
            if (emissionPower > 0f)
                material.EnableKeyword("_EMISSION");

            if (transparent)
            {
                SetFloat(material, "_Surface", 1f);
                SetFloat(material, "_AlphaClip", 0f);
                SetFloat(material, "_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                SetFloat(material, "_Surface", 0f);
                SetFloat(material, "_ZWrite", 1f);
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = -1;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
                material.SetColor(property, value);
        }

        private static GameObject AddMesh(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            GameObject child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localRotation = rotation;
            child.transform.localScale = scale;
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            child.GetComponent<MeshRenderer>().sharedMaterial = material;
            return child;
        }

        private static GameObject AddPrimitive(Transform parent, string name, PrimitiveType primitive, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            GameObject child = GameObject.CreatePrimitive(primitive);
            child.name = name;
            Object.DestroyImmediate(child.GetComponent<Collider>());
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localRotation = rotation;
            child.transform.localScale = scale;
            child.GetComponent<Renderer>().sharedMaterial = material;
            return child;
        }

        private static Mesh CreateOctahedronMesh()
        {
            Vector3[] vertices =
            {
                new(0f, 1f, 0f),
                new(1f, 0f, 0f),
                new(0f, 0f, 1f),
                new(-1f, 0f, 0f),
                new(0f, 0f, -1f),
                new(0f, -1f, 0f)
            };
            int[] triangles =
            {
                0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4,
                5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1
            };
            return MeshFrom(vertices, triangles);
        }

        private static Mesh CreateShardMesh()
        {
            Vector3[] vertices =
            {
                new(0f, 1.12f, 0f),
                new(0.28f, 0f, 0.18f),
                new(-0.24f, 0f, 0.22f),
                new(-0.18f, 0f, -0.25f),
                new(0.24f, 0f, -0.2f),
                new(0f, -0.42f, 0f)
            };
            int[] triangles =
            {
                0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1,
                5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4
            };
            return MeshFrom(vertices, triangles);
        }

        private static Mesh CreateGlassPaneMesh()
        {
            Vector3[] vertices =
            {
                new(-0.42f, -0.55f, 0f),
                new(0.36f, -0.42f, 0.02f),
                new(0.28f, 0.48f, -0.015f),
                new(-0.24f, 0.62f, 0.01f),
                new(-0.48f, 0.05f, -0.02f)
            };
            int[] triangles = { 0, 1, 2, 0, 2, 4, 4, 2, 3, 2, 1, 0, 4, 2, 0, 3, 2, 4 };
            return MeshFrom(vertices, triangles);
        }

        private static Mesh CreateIronChunkMesh()
        {
            Vector3[] vertices =
            {
                new(-0.55f, -0.35f, -0.35f), new(0.48f, -0.32f, -0.4f), new(0.58f, 0.28f, -0.3f), new(-0.42f, 0.36f, -0.38f),
                new(-0.62f, -0.28f, 0.34f), new(0.42f, -0.36f, 0.42f), new(0.5f, 0.34f, 0.36f), new(-0.48f, 0.3f, 0.4f)
            };
            int[] triangles =
            {
                0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7
            };
            return MeshFrom(vertices, triangles);
        }

        private static Mesh CreateRingMesh(int segments, float radius, float thickness)
        {
            List<Vector3> vertices = new();
            List<int> triangles = new();
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments;
                float a1 = Mathf.PI * 2f * (i + 1) / segments;
                int start = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a0) * (radius - thickness), 0f, Mathf.Sin(a0) * (radius - thickness)));
                vertices.Add(new Vector3(Mathf.Cos(a0) * (radius + thickness), 0f, Mathf.Sin(a0) * (radius + thickness)));
                vertices.Add(new Vector3(Mathf.Cos(a1) * (radius - thickness), 0f, Mathf.Sin(a1) * (radius - thickness)));
                vertices.Add(new Vector3(Mathf.Cos(a1) * (radius + thickness), 0f, Mathf.Sin(a1) * (radius + thickness)));
                triangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
            }

            return MeshFrom(vertices.ToArray(), triangles.ToArray());
        }

        private static Mesh MeshFrom(Vector3[] vertices, int[] triangles)
        {
            Mesh mesh = new()
            {
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
