using System.Collections;
using System.Collections.Generic;
using AccardND.Battlefield;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private sealed class MageConstellationStar
	{
		public RectTransform Rect;
		public Image Image;
		public Vector3 From;
		public Vector3 Scatter;
		public Vector3 To;
		public bool HasStart;
		public bool HasEnd;
	}

	private sealed class MageConstellationLine
	{
		public RectTransform Rect;
		public Image Image;
	}

	private sealed class MageConstellationGeometry
	{
		public readonly Vector3[] Vertices;
		public readonly int[] Edges;

		public MageConstellationGeometry(Vector3[] vertices, int[] edges)
		{
			Vertices = vertices;
			Edges = edges;
		}
	}

	private IEnumerator PlayMageVigorConstellation(BattleCardState target, int startDieSides, int endDieSides)
	{
		if (target == null || (Object)(object)target.View == (Object)null)
			yield break;

		RectTransform parent = (Object)(object)safeAreaRoot != (Object)null ? safeAreaRoot : canvasRect;
		if ((Object)(object)parent == (Object)null)
			yield break;

		int startSides = Mathf.Max(3, startDieSides);
		int endSides = Mathf.Max(3, endDieSides);
		MageConstellationGeometry startGeometry = CreateDieConstellationGeometry(startSides);
		MageConstellationGeometry endGeometry = CreateDieConstellationGeometry(endSides);
		Vector2 center = WorldToLocalPoint(parent, target.View.RectTransform.position);
		float radius = Mathf.Clamp(Mathf.Min(target.View.RectTransform.rect.width, target.View.RectTransform.rect.height) * 0.42f, 78f, 155f);
		Color starColor = new Color(0.78f, 0.28f, 1f, 1f);
		Color lineColor = new Color(0.58f, 0.12f, 1f, 0.72f);

		GameObject rootObject = new GameObject("Mage Vigor Constellation", typeof(RectTransform), typeof(CanvasGroup));
		rootObject.transform.SetParent((Transform)(object)parent, false);
		RectTransform root = (RectTransform)rootObject.transform;
		root.anchorMin = new Vector2(0.5f, 0.5f);
		root.anchorMax = new Vector2(0.5f, 0.5f);
		root.pivot = new Vector2(0.5f, 0.5f);
		root.anchoredPosition = center;
		root.sizeDelta = new Vector2(radius * 3.2f, radius * 3.2f);
		root.SetAsLastSibling();
		CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
		group.alpha = 0f;

		Text label = CreateText("Mage Vigor Label", rootObject.transform, MmoUiTheme.BodyFont, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
		label.color = new Color(0.95f, 0.78f, 1f, 1f);
		SetRect(label.rectTransform, new Vector2(0.26f, 0.08f), new Vector2(0.74f, 0.26f));
		label.text = $"D{startSides}";

		int starCount = Mathf.Max(startGeometry.Vertices.Length, endGeometry.Vertices.Length);
		var stars = new List<MageConstellationStar>(starCount);
		for (int index = 0; index < starCount; index++)
		{
			MageConstellationStar star = new MageConstellationStar();
			star.HasStart = index < startGeometry.Vertices.Length;
			star.HasEnd = index < endGeometry.Vertices.Length;
			star.From = star.HasStart ? startGeometry.Vertices[index] : Vector3.zero;
			star.To = star.HasEnd ? endGeometry.Vertices[index] : Vector3.zero;
			Vector3 scatterOrigin = star.HasStart ? star.From : star.To;
			Vector3 scatterDirection = scatterOrigin.sqrMagnitude > 0.0001f ? scatterOrigin.normalized : Vector3.up;
			star.Scatter = scatterOrigin + scatterDirection * 1.05f;
			Image image = CreateImage("Mage Vigor Star", rootObject.transform, starColor);
			star.Image = image;
			star.Rect = image.rectTransform;
			star.Rect.anchorMin = new Vector2(0.5f, 0.5f);
			star.Rect.anchorMax = new Vector2(0.5f, 0.5f);
			star.Rect.pivot = new Vector2(0.5f, 0.5f);
			star.Rect.sizeDelta = Vector2.one * 12f;
			stars.Add(star);
		}

		int lineCount = Mathf.Max(startGeometry.Edges.Length, endGeometry.Edges.Length) / 2;
		var lines = new List<MageConstellationLine>(lineCount);
		for (int index = 0; index < lineCount; index++)
		{
			Image image = CreateImage("Mage Vigor Line", rootObject.transform, lineColor);
			image.transform.SetAsFirstSibling();
			RectTransform rect = image.rectTransform;
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			lines.Add(new MageConstellationLine { Rect = rect, Image = image });
		}

		float elapsed = 0f;
		while (elapsed < 0.22f)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(elapsed / 0.22f);
			Quaternion rotation = MageDieRotation(progress * 0.24f);
			group.alpha = Mathf.SmoothStep(0f, 1f, progress);
			root.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, progress);
			UpdateConstellationPose(stars, lines, startGeometry.Edges, rotation, radius, lineColor, starColor, showStart: true);
			yield return null;
		}

		elapsed = 0f;
		while (elapsed < 0.42f)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(elapsed / 0.42f);
			Quaternion rotation = MageDieRotation(0.24f + progress * 0.42f);
			UpdateConstellationPose(stars, lines, startGeometry.Edges, rotation, radius, lineColor, starColor, showStart: true);
			yield return null;
		}

		elapsed = 0f;
		while (elapsed < 0.42f)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.42f));
			Quaternion rotation = MageDieRotation(0.66f + progress * 0.18f);
			foreach (MageConstellationStar star in stars)
			{
				Vector3 point = Vector3.LerpUnclamped(star.HasStart ? star.From : Vector3.zero, star.Scatter, progress);
				PlaceConstellationStar(star, point, rotation, radius, starColor, star.HasStart ? Mathf.Lerp(1f, 0.38f, progress) : 0f);
				star.Image.color = Color.Lerp(starColor, new Color(starColor.r, starColor.g, starColor.b, 0.38f), progress);
			}
			HideConstellationLines(lines);
			yield return null;
		}

		label.text = $"D{endSides}";
		elapsed = 0f;
		while (elapsed < 0.5f)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.5f));
			Quaternion rotation = MageDieRotation(0.84f + progress * 0.24f);
			for (int index = 0; index < stars.Count; index++)
			{
				MageConstellationStar star = stars[index];
				Vector3 point = Vector3.LerpUnclamped(star.Scatter, star.HasEnd ? star.To : Vector3.zero, progress);
				float alpha = star.HasEnd ? Mathf.Lerp(0.38f, 1f, progress) : Mathf.Lerp(0.38f, 0f, progress);
				PlaceConstellationStar(star, point, rotation, radius, starColor, alpha);
			}
			UpdateConstellationLines(lines, stars, endGeometry.Edges, lineColor, progress);
			yield return null;
		}

		elapsed = 0f;
		while (elapsed < 0.72f)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(elapsed / 0.72f);
			Quaternion rotation = MageDieRotation(1.08f + progress * 0.46f);
			UpdateConstellationPose(stars, lines, endGeometry.Edges, rotation, radius, lineColor, starColor, showStart: false);
			yield return null;
		}

		elapsed = 0f;
		while (elapsed < 0.2f)
		{
			elapsed += Time.unscaledDeltaTime;
			group.alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / 0.2f));
			root.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, Mathf.Clamp01(elapsed / 0.2f));
			yield return null;
		}

		Object.Destroy(rootObject);
	}

	private static MageConstellationGeometry CreateDieConstellationGeometry(int sides)
	{
		return sides switch
		{
			4 => CreateTetrahedronGeometry(),
			6 => CreateCubeGeometry(),
			8 => CreateOctahedronGeometry(),
			10 => CreateD10TrapezohedronGeometry(),
			12 => CreateDodecahedronGeometry(),
			20 => CreateIcosahedronGeometry(),
			_ => sides <= 4 ? CreateTetrahedronGeometry()
				: sides <= 6 ? CreateCubeGeometry()
				: sides <= 8 ? CreateOctahedronGeometry()
				: sides <= 10 ? CreateD10TrapezohedronGeometry()
				: sides <= 12 ? CreateDodecahedronGeometry()
				: CreateIcosahedronGeometry()
		};
	}

	private static MageConstellationGeometry CreateTetrahedronGeometry()
	{
		float baseRadius = 1.05f;
		float baseY = -0.46f;
		return NormalizeGeometry(
			new[]
			{
				new Vector3(0f, 1.22f, 0f),
				new Vector3(Mathf.Cos(90f * Mathf.Deg2Rad) * baseRadius, baseY, Mathf.Sin(90f * Mathf.Deg2Rad) * baseRadius),
				new Vector3(Mathf.Cos(210f * Mathf.Deg2Rad) * baseRadius, baseY, Mathf.Sin(210f * Mathf.Deg2Rad) * baseRadius),
				new Vector3(Mathf.Cos(330f * Mathf.Deg2Rad) * baseRadius, baseY, Mathf.Sin(330f * Mathf.Deg2Rad) * baseRadius)
			},
			new[] { 0, 1, 0, 2, 0, 3, 1, 2, 1, 3, 2, 3 });
	}

	private static MageConstellationGeometry CreateCubeGeometry()
	{
		return NormalizeGeometry(
			new[]
			{
				new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f),
				new Vector3(1f, 1f, -1f), new Vector3(-1f, 1f, -1f),
				new Vector3(-1f, -1f, 1f), new Vector3(1f, -1f, 1f),
				new Vector3(1f, 1f, 1f), new Vector3(-1f, 1f, 1f)
			},
			new[] { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 });
	}

	private static MageConstellationGeometry CreateOctahedronGeometry()
	{
		return NormalizeGeometry(
			new[]
			{
				new Vector3(0f, 1.35f, 0f), new Vector3(1f, 0f, 0f),
				new Vector3(0f, 0f, 1f), new Vector3(-1f, 0f, 0f),
				new Vector3(0f, 0f, -1f), new Vector3(0f, -1.35f, 0f)
			},
			new[] { 0, 1, 0, 2, 0, 3, 0, 4, 5, 1, 5, 2, 5, 3, 5, 4, 1, 2, 2, 3, 3, 4, 4, 1 });
	}

	private static MageConstellationGeometry CreateBiPyramidGeometry(int ringCount)
	{
		var vertices = new List<Vector3> { new Vector3(0f, 1.35f, 0f), new Vector3(0f, -1.35f, 0f) };
		for (int index = 0; index < ringCount; index++)
		{
			float angle = (360f / ringCount * index + 18f) * Mathf.Deg2Rad;
			vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
		}

		var edges = new List<int>();
		for (int index = 0; index < ringCount; index++)
		{
			int current = index + 2;
			int next = ((index + 1) % ringCount) + 2;
			edges.Add(0); edges.Add(current);
			edges.Add(1); edges.Add(current);
			edges.Add(current); edges.Add(next);
		}
		return NormalizeGeometry(vertices.ToArray(), edges.ToArray());
	}

	private static MageConstellationGeometry CreateD10TrapezohedronGeometry()
	{
		var vertices = new List<Vector3>
		{
			new Vector3(0f, 1.24f, 0f),
			new Vector3(0f, -1.24f, 0f)
		};
		for (int index = 0; index < 5; index++)
		{
			float upperAngle = (72f * index) * Mathf.Deg2Rad;
			float lowerAngle = (72f * index + 36f) * Mathf.Deg2Rad;
			vertices.Add(new Vector3(Mathf.Cos(upperAngle), 0.32f, Mathf.Sin(upperAngle)));
			vertices.Add(new Vector3(Mathf.Cos(lowerAngle), -0.32f, Mathf.Sin(lowerAngle)));
		}

		var edges = new List<int>();
		for (int index = 0; index < 5; index++)
		{
			int upper = 2 + index * 2;
			int lower = upper + 1;
			int previousLower = 2 + ((index + 4) % 5) * 2 + 1;
			edges.Add(0); edges.Add(upper);
			edges.Add(1); edges.Add(lower);
			edges.Add(upper); edges.Add(lower);
			edges.Add(upper); edges.Add(previousLower);
		}
		return NormalizeGeometry(vertices.ToArray(), edges.ToArray());
	}

	private static MageConstellationGeometry CreateDodecahedronGeometry()
	{
		float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
		Vector3[] icosahedronVertices =
		{
			new Vector3(-1f, phi, 0f), new Vector3(1f, phi, 0f), new Vector3(-1f, -phi, 0f), new Vector3(1f, -phi, 0f),
			new Vector3(0f, -1f, phi), new Vector3(0f, 1f, phi), new Vector3(0f, -1f, -phi), new Vector3(0f, 1f, -phi),
			new Vector3(phi, 0f, -1f), new Vector3(phi, 0f, 1f), new Vector3(-phi, 0f, -1f), new Vector3(-phi, 0f, 1f)
		};
		int[,] faces =
		{
			{ 0, 11, 5 }, { 0, 5, 1 }, { 0, 1, 7 }, { 0, 7, 10 }, { 0, 10, 11 },
			{ 1, 5, 9 }, { 5, 11, 4 }, { 11, 10, 2 }, { 10, 7, 6 }, { 7, 1, 8 },
			{ 3, 9, 4 }, { 3, 4, 2 }, { 3, 2, 6 }, { 3, 6, 8 }, { 3, 8, 9 },
			{ 4, 9, 5 }, { 2, 4, 11 }, { 6, 2, 10 }, { 8, 6, 7 }, { 9, 8, 1 }
		};

		Vector3[] vertices = BuildFaceCenters(icosahedronVertices, faces);
		return NormalizeGeometry(vertices, BuildFaceAdjacencyEdges(faces));
	}

	private static MageConstellationGeometry CreateIcosahedronGeometry()
	{
		float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
		Vector3[] vertices =
		{
			new Vector3(-1f, phi, 0f), new Vector3(1f, phi, 0f), new Vector3(-1f, -phi, 0f), new Vector3(1f, -phi, 0f),
			new Vector3(0f, -1f, phi), new Vector3(0f, 1f, phi), new Vector3(0f, -1f, -phi), new Vector3(0f, 1f, -phi),
			new Vector3(phi, 0f, -1f), new Vector3(phi, 0f, 1f), new Vector3(-phi, 0f, -1f), new Vector3(-phi, 0f, 1f)
		};
		return NormalizeGeometry(vertices, BuildNearestEdges(vertices, 30));
	}

	private static MageConstellationGeometry NormalizeGeometry(Vector3[] vertices, int[] edges)
	{
		float maxMagnitude = 1f;
		foreach (Vector3 vertex in vertices)
			maxMagnitude = Mathf.Max(maxMagnitude, vertex.magnitude);

		for (int index = 0; index < vertices.Length; index++)
			vertices[index] /= maxMagnitude;
		return new MageConstellationGeometry(vertices, edges);
	}

	private static int[] BuildNearestEdges(Vector3[] vertices, int edgeLimit)
	{
		var candidates = new List<(int a, int b, float distance)>();
		for (int a = 0; a < vertices.Length; a++)
		{
			for (int b = a + 1; b < vertices.Length; b++)
				candidates.Add((a, b, (vertices[a] - vertices[b]).sqrMagnitude));
		}
		candidates.Sort((left, right) => left.distance.CompareTo(right.distance));
		var edges = new List<int>(edgeLimit * 2);
		for (int index = 0; index < candidates.Count && index < edgeLimit; index++)
		{
			edges.Add(candidates[index].a);
			edges.Add(candidates[index].b);
		}
		return edges.ToArray();
	}

	private static Vector3[] BuildFaceCenters(Vector3[] sourceVertices, int[,] faces)
	{
		int faceCount = faces.GetLength(0);
		Vector3[] centers = new Vector3[faceCount];
		for (int index = 0; index < faceCount; index++)
		{
			centers[index] = (
				sourceVertices[faces[index, 0]]
				+ sourceVertices[faces[index, 1]]
				+ sourceVertices[faces[index, 2]]) / 3f;
		}
		return centers;
	}

	private static int[] BuildFaceAdjacencyEdges(int[,] faces)
	{
		int faceCount = faces.GetLength(0);
		var edges = new List<int>(60);
		for (int a = 0; a < faceCount; a++)
		{
			for (int b = a + 1; b < faceCount; b++)
			{
				int shared = 0;
				for (int ai = 0; ai < 3; ai++)
				{
					for (int bi = 0; bi < 3; bi++)
					{
						if (faces[a, ai] == faces[b, bi])
							shared++;
					}
				}
				if (shared == 2)
				{
					edges.Add(a);
					edges.Add(b);
				}
			}
		}
		return edges.ToArray();
	}

	private static void UpdateConstellationPose(
		List<MageConstellationStar> stars,
		List<MageConstellationLine> lines,
		int[] edges,
		Quaternion rotation,
		float radius,
		Color lineColor,
		Color starColor,
		bool showStart)
	{
		foreach (MageConstellationStar star in stars)
		{
			bool visible = showStart ? star.HasStart : star.HasEnd;
			Vector3 point = showStart ? star.From : star.To;
			PlaceConstellationStar(star, point, rotation, radius, starColor, visible ? 1f : 0f);
		}
		UpdateConstellationLines(lines, stars, edges, lineColor, 1f);
	}

	private static void PlaceConstellationStar(
		MageConstellationStar star,
		Vector3 point,
		Quaternion rotation,
		float radius,
		Color color,
		float alpha)
	{
		Vector3 rotated = rotation * point;
		float perspective = 1.22f / Mathf.Max(0.42f, 1.22f - rotated.z * 0.36f);
		star.Rect.anchoredPosition = new Vector2(rotated.x, rotated.y) * radius * perspective;
		float size = Mathf.Lerp(9f, 17f, Mathf.InverseLerp(-1f, 1f, rotated.z));
		star.Rect.sizeDelta = Vector2.one * size * Mathf.Lerp(0.8f, 1.08f, perspective - 0.8f);
		star.Image.color = new Color(color.r, color.g, color.b, alpha);
	}

	private static Quaternion MageDieRotation(float phase)
	{
		return Quaternion.Euler(18f, -28f + phase * 72f, 0f);
	}

	private static void UpdateConstellationLines(
		List<MageConstellationLine> lines,
		List<MageConstellationStar> stars,
		int[] edges,
		Color color,
		float alpha)
	{
		for (int index = 0; index < lines.Count; index++)
		{
			int edgeIndex = index * 2;
			bool active = edges != null && edgeIndex + 1 < edges.Length;
			lines[index].Image.enabled = active;
			if (!active)
				continue;

			Vector2 from = stars[edges[edgeIndex]].Rect.anchoredPosition;
			Vector2 to = stars[edges[edgeIndex + 1]].Rect.anchoredPosition;
			Vector2 delta = to - from;
			lines[index].Rect.anchoredPosition = from + delta * 0.5f;
			lines[index].Rect.sizeDelta = new Vector2(delta.magnitude, 3f);
			lines[index].Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
			lines[index].Image.color = new Color(color.r, color.g, color.b, color.a * alpha);
		}
	}

	private static void HideConstellationLines(List<MageConstellationLine> lines)
	{
		foreach (MageConstellationLine line in lines)
			line.Image.enabled = false;
	}

	private static int LowerVigorDieBySteps(int dieSides, int steps)
	{
		int result = dieSides;
		for (int index = 0; index < steps; index++)
			result = LowerVigorDieOnce(result);
		return result;
	}

	private static int LowerVigorDieOnce(int dieSides)
	{
		if (dieSides <= 3)
			return 3;
		return dieSides switch
		{
			4 => 3,
			6 => 4,
			8 => 6,
			10 => 8,
			12 => 10,
			20 => 12,
			_ => dieSides <= 6 ? 4 : dieSides <= 8 ? 6 : dieSides <= 10 ? 8 : dieSides <= 12 ? 10 : 12
		};
	}

	private static Vector2 WorldToLocalPoint(RectTransform parent, Vector3 worldPosition)
	{
		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			parent,
			RectTransformUtility.WorldToScreenPoint(null, worldPosition),
			null,
			out Vector2 localPoint);
		return localPoint;
	}
}
}
