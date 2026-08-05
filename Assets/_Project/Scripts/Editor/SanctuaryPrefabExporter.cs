#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AccardND.EditorTools
{
[InitializeOnLoad]
internal static class SanctuaryPrefabExporter
{
	private const string PrefabFolder = "Assets/_Project/Resources/UI/Prefabs";
	private const string PrefabPath = PrefabFolder + "/SanctuaryRoom.prefab";
	private static int pendingExportFrames;

	static SanctuaryPrefabExporter()
	{
		EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		EditorApplication.delayCall += RepairPrefabMissingScripts;
		// La prima esportazione avviene appena la schermata runtime esiste. In seguito il
		// prefab resta completamente sotto il controllo dell'artista.
		if (!File.Exists(PrefabPath))
		{
			if (EditorApplication.isPlaying)
			{
				BeginPendingExport();
			}
			else
			{
				EditorApplication.delayCall += TryCreateMissingPrefab;
			}
		}
	}

	private static void OnPlayModeStateChanged(PlayModeStateChange state)
	{
		if (state == PlayModeStateChange.EnteredPlayMode && !File.Exists(PrefabPath))
		{
			BeginPendingExport();
		}
	}

	private static void BeginPendingExport()
	{
		pendingExportFrames = 600;
		EditorApplication.update -= TryPendingExport;
		EditorApplication.update += TryPendingExport;
	}

	private static void TryPendingExport()
	{
		if (File.Exists(PrefabPath) || !EditorApplication.isPlaying || --pendingExportFrames <= 0)
		{
			EditorApplication.update -= TryPendingExport;
			return;
		}
		if (ExportFromRuntime(showMissingWarning: false))
		{
			EditorApplication.update -= TryPendingExport;
		}
	}

	[MenuItem("AccardND/UI/Santuario/Aggiorna prefab dalla schermata runtime")]
	private static void ExportFromRuntimeMenu()
	{
		ExportFromRuntime(showMissingWarning: true);
	}

	private static void TryCreateMissingPrefab()
	{
		if (!File.Exists(PrefabPath))
		{
			ExportFromRuntime(showMissingWarning: false);
		}
	}

	private static bool ExportFromRuntime(bool showMissingWarning)
	{
		GameObject source = FindRuntimeSanctuary();
		if (source == null)
		{
			if (showMissingWarning)
			{
				Debug.LogWarning(
					"Prefab Santuario non creato: avvia il gioco, attendi la schermata iniziale e ripeti " +
					"AccardND/UI/Santuario/Aggiorna prefab dalla schermata runtime.");
			}
			return false;
		}

		Directory.CreateDirectory(PrefabFolder);
		GameObject clone = Object.Instantiate(source);
		clone.name = "SanctuaryRoom";
		clone.hideFlags = HideFlags.None;
		try
		{
			Transform list = FindChild(clone.transform, "Sanctuary List");
			if (list != null)
			{
				for (int index = list.childCount - 1; index >= 0; index--)
				{
					Object.DestroyImmediate(list.GetChild(index).gameObject);
				}
			}
			RemoveRuntimeAuthoringComponents(clone);

			PrefabUtility.SaveAsPrefabAsset(clone, PrefabPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			Debug.Log($"Prefab Santuario salvato in {PrefabPath}. Ora e' modificabile nel Prefab Mode.");
			return true;
		}
		finally
		{
			Object.DestroyImmediate(clone);
		}
	}

	private static void RemoveRuntimeAuthoringComponents(GameObject root)
	{
		MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
		foreach (MonoBehaviour behaviour in behaviours)
		{
			if (behaviour != null && behaviour.GetType().Name == "EditableRuntimeText")
			{
				Object.DestroyImmediate(behaviour);
			}
		}
	}

	private static void RepairPrefabMissingScripts()
	{
		if (!File.Exists(PrefabPath))
		{
			return;
		}

		GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
		bool changed = false;
		try
		{
			Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
			foreach (Transform target in transforms)
			{
				if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target.gameObject) <= 0)
				{
					continue;
				}
				GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target.gameObject);
				changed = true;
			}
			if (changed)
			{
				PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
				AssetDatabase.SaveAssets();
				Debug.Log("Prefab Santuario ripulito dai componenti runtime non serializzabili.");
			}
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	private static GameObject FindRuntimeSanctuary()
	{
		GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
		foreach (GameObject candidate in objects)
		{
			if (candidate == null || candidate.name != "Sanctuary" || !candidate.scene.IsValid())
			{
				continue;
			}
			if (candidate.GetComponent<Canvas>() != null)
			{
				return candidate;
			}
		}
		return null;
	}

	private static Transform FindChild(Transform root, string objectName)
	{
		if (root == null)
		{
			return null;
		}
		if (root.name == objectName)
		{
			return root;
		}
		for (int index = 0; index < root.childCount; index++)
		{
			Transform found = FindChild(root.GetChild(index), objectName);
			if (found != null)
			{
				return found;
			}
		}
		return null;
	}
}
}
#endif
