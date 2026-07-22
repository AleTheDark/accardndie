using System.IO;
using AccardND.Battlefield;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    public static class DiceResultPrefabBuilder
    {
        private const string OutputFolder = "Assets/Resources/DnD_Dice/Prefab";
        private const string AutoOrientMarker = OutputFolder + "/.auto_oriented_non_manual_results";

        private static readonly int[] DiceSides = { 4, 6, 8, 10, 12, 20 };
        // Il D4 resta manuale (lettura al vertice, già sistemato a mano);
        // gli altri usano la calibrazione DigitUp di ClassDice3D.
        private static readonly int[] AutoOrientedDiceSides = { 6, 8, 10, 12, 20 };

        [InitializeOnLoadMethod]
        private static void GenerateOnceWhenFolderIsEmpty()
        {
            EditorApplication.delayCall += () =>
            {
                EnsureOutputFolder();
                if (Directory.GetFiles(OutputFolder, "*.prefab", SearchOption.TopDirectoryOnly).Length == 0)
                    GenerateMissingResultPrefabs();
                if (!File.Exists(AutoOrientMarker))
                {
                    OrientGeneratedResultPrefabsExceptD4D6();
                    File.WriteAllText(AutoOrientMarker, "D8/D10/D12/D20 auto-oriented. D4/D6 are manual.");
                    AssetDatabase.ImportAsset(AutoOrientMarker);
                }
            };
        }

        [MenuItem("Accard N' Die/Dice/Generate Missing Result Prefabs")]
        public static void GenerateMissingResultPrefabs()
        {
            EnsureOutputFolder();

            int created = 0;
            foreach (int sides in DiceSides)
            {
                GameObject source = Resources.Load<GameObject>($"DnD_Dice/Mesh/00_D{sides}");
                Material material = Resources.Load<Material>($"DnD_Dice/Material/D{sides}");
                if (source == null)
                {
                    Debug.LogWarning($"[Accard N' Die] D{sides}: mesh sorgente non trovata.");
                    continue;
                }

                for (int result = 1; result <= sides; result++)
                {
                    string path = ResultPrefabPath(sides, result);
                    if (File.Exists(path))
                        continue;

                    GameObject root = CreateResultPrefabRoot(source, material, sides, result);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Object.DestroyImmediate(root);
                    created++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Accard N' Die] Creati {created} prefab risultato in {OutputFolder}.");
        }

        [MenuItem("Accard N' Die/Dice/Orient Result Prefabs Except D4-D6")]
        public static void OrientGeneratedResultPrefabsExceptD4D6()
        {
            EnsureOutputFolder();

            int saved = 0;
            foreach (int sides in AutoOrientedDiceSides)
            {
                GameObject source = Resources.Load<GameObject>($"DnD_Dice/Mesh/00_D{sides}");
                Material material = Resources.Load<Material>($"DnD_Dice/Material/D{sides}");
                if (source == null)
                {
                    Debug.LogWarning($"[Accard N' Die] D{sides}: mesh sorgente non trovata.");
                    continue;
                }

                for (int result = 1; result <= sides; result++)
                {
                    string path = ResultPrefabPath(sides, result);
                    GameObject root = CreateResultPrefabRoot(source, material, sides, result);
                    root.transform.localRotation = TargetRotationFor(root, sides, result);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Object.DestroyImmediate(root);
                    saved++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Accard N' Die] Orientati {saved} prefab risultato. D4/D6 non toccati.");
        }

        private static GameObject CreateResultPrefabRoot(GameObject source, Material material, int sides, int result)
        {
            GameObject root = new GameObject($"D{sides}_Result_{result}");
            GameObject model = Object.Instantiate(source, root.transform, false);
            model.name = "Model";

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (material != null)
                    renderer.sharedMaterial = material;
            }

            Normalize(model.transform);
            return root;
        }

        private static void Normalize(Transform target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);

            float radius = Mathf.Max(bounds.extents.magnitude, 0.0001f);
            target.localScale *= 0.5f / radius;

            Renderer[] scaledRenderers = target.GetComponentsInChildren<Renderer>(true);
            Bounds scaledBounds = scaledRenderers[0].bounds;
            for (int index = 1; index < scaledRenderers.Length; index++)
                scaledBounds.Encapsulate(scaledRenderers[index].bounds);

            target.position += target.parent.position - scaledBounds.center;
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/DnD_Dice"))
                AssetDatabase.CreateFolder("Assets/Resources", "DnD_Dice");
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/Resources/DnD_Dice", "Prefab");
        }

        private static string ResultPrefabPath(int sides, int result)
        {
            return $"{OutputFolder}/D{sides}_Result_{result}.prefab";
        }

        // Orienta la ROOT del prefab così che la faccia del risultato guardi la
        // camera del rig di gioco (0, 1.35, -3) con il numero dritto.
        //
        // Attenzione agli spazi: normali e DigitUp calibrati di ClassDice3D
        // valgono nello spazio della root del MODELLO FBX (il figlio "Model",
        // che porta la rotazione di import, es. 270° su X). Vanno riportati
        // nello spazio della root del prefab (dove applichiamo la rotazione)
        // passando dalla rotazione locale del modello — senza questa
        // conversione ogni faccia risulta storta di una quantità diversa.
        private static Quaternion TargetRotationFor(GameObject root, int sides, int result)
        {
            Transform model = root.transform.childCount > 0 ? root.transform.GetChild(0) : null;
            if (model == null)
                return Quaternion.identity;

            Vector3 faceNormal = Vector3.up;
            Vector3 digitUp = Vector3.zero;
            bool found = false;
            // GetFaces sul MODELLO (come fa il runtime): normali e DigitUp
            // calibrati nello spazio della root FBX.
            foreach (DieFace face in ClassDice3D.GetFaces(model.gameObject, sides))
            {
                if (face.Value == result)
                {
                    faceNormal = model.localRotation * face.Normal;
                    digitUp = face.DigitUp == Vector3.zero
                        ? Vector3.zero
                        : model.localRotation * face.DigitUp;
                    found = true;
                    break;
                }
            }

            if (!found)
                Debug.LogWarning($"[Accard N' Die] D{sides}: nessuna faccia con valore {result} durante orientamento prefab.");

            Vector3 cameraPosition = new Vector3(0f, 1.35f, -3f);
            Vector3 toCamera = cameraPosition.normalized;
            Vector3 cameraUp = Quaternion.LookRotation(-toCamera, Vector3.up) * Vector3.up;
            Quaternion baseRotation = Quaternion.FromToRotation(faceNormal, toCamera);

            if (digitUp != Vector3.zero)
            {
                Vector3 current = Vector3.ProjectOnPlane(baseRotation * digitUp, toCamera);
                Vector3 desired = Vector3.ProjectOnPlane(cameraUp, toCamera);
                if (current.sqrMagnitude > 1e-6f && desired.sqrMagnitude > 1e-6f)
                {
                    float twist = Vector3.SignedAngle(current, desired, toCamera);
                    return Quaternion.AngleAxis(twist, toCamera) * baseRotation;
                }
            }
            return baseRotation;
        }
    }
}
