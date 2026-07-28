using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
	[RequireComponent(typeof(CanvasRenderer))]
	public sealed class D20WireframeGraphic : Graphic
	{
		[SerializeField] private float lineThickness = 7f;
		[SerializeField] private float rotationDegrees;
		[SerializeField] private float explosion;

		private static readonly Vector3[] Vertices = BuildVertices();

		public float RotationDegrees
		{
			get => rotationDegrees;
			set
			{
				rotationDegrees = value;
				SetVerticesDirty();
			}
		}

		public float Explosion
		{
			get => explosion;
			set
			{
				explosion = Mathf.Clamp01(value);
				SetVerticesDirty();
			}
		}

		public float LineThickness
		{
			get => lineThickness;
			set
			{
				lineThickness = Mathf.Max(1f, value);
				SetVerticesDirty();
			}
		}

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();
			Rect rect = rectTransform.rect;
			float scale = Mathf.Min(rect.width, rect.height) * 0.39f;
			Vector2 center = rect.center;
			Quaternion rotation = Quaternion.Euler(18f, rotationDegrees, -10f);
			Vector2[] points = new Vector2[Vertices.Length];
			float closestDistance = float.MaxValue;

			for (int i = 0; i < Vertices.Length; i++)
			{
				Vector3 point = rotation * Vertices[i];
				float perspective = 1f / (1.55f - point.z * 0.24f);
				points[i] = center + new Vector2(point.x, point.y) * scale * perspective;
			}

			for (int i = 0; i < Vertices.Length - 1; i++)
			{
				for (int j = i + 1; j < Vertices.Length; j++)
				{
					closestDistance = Mathf.Min(closestDistance, Vector3.Distance(Vertices[i], Vertices[j]));
				}
			}

			Color32 edgeColor = color;
			edgeColor.a = (byte)Mathf.RoundToInt(edgeColor.a * (1f - explosion));
			for (int i = 0; i < Vertices.Length - 1; i++)
			{
				for (int j = i + 1; j < Vertices.Length; j++)
				{
					if (Vector3.Distance(Vertices[i], Vertices[j]) <= closestDistance * 1.04f)
					{
						Vector2 midpoint = (points[i] + points[j]) * 0.5f;
						Vector2 blast = (midpoint - center).normalized * explosion * scale * 0.72f;
						AddLine(vh, points[i] + blast, points[j] + blast, lineThickness * (1f + explosion * 0.55f), edgeColor);
					}
				}
			}
		}

		private static Vector3[] BuildVertices()
		{
			float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
			return new[]
			{
				new Vector3(-1f, phi, 0f).normalized,
				new Vector3(1f, phi, 0f).normalized,
				new Vector3(-1f, -phi, 0f).normalized,
				new Vector3(1f, -phi, 0f).normalized,
				new Vector3(0f, -1f, phi).normalized,
				new Vector3(0f, 1f, phi).normalized,
				new Vector3(0f, -1f, -phi).normalized,
				new Vector3(0f, 1f, -phi).normalized,
				new Vector3(phi, 0f, -1f).normalized,
				new Vector3(phi, 0f, 1f).normalized,
				new Vector3(-phi, 0f, -1f).normalized,
				new Vector3(-phi, 0f, 1f).normalized
			};
		}

		private static void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float thickness, Color32 color)
		{
			Vector2 direction = end - start;
			if (direction.sqrMagnitude < 0.01f)
				return;

			Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (thickness * 0.5f);
			UIVertex vertex = UIVertex.simpleVert;
			vertex.color = color;
			UIVertex[] quad = new UIVertex[4];
			vertex.position = start - normal;
			quad[0] = vertex;
			vertex.position = start + normal;
			quad[1] = vertex;
			vertex.position = end + normal;
			quad[2] = vertex;
			vertex.position = end - normal;
			quad[3] = vertex;
			vh.AddUIVertexQuad(quad);
		}
	}
}
