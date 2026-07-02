using UnityEngine;

namespace AccardND.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MinibossGolemFacing : MonoBehaviour
    {
        [SerializeField] private RectTransform source;
        [SerializeField] private RectTransform targetRoot;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Vector3 rotationOffset;
        [SerializeField, Min(0.05f)] private float targetRectFill = 0.78f;

        private Vector2 lastSourceSize;
        private Bounds visualBounds;
        private bool hasVisualBounds;

        public void Configure(RectTransform sourceRect, RectTransform targetRectRoot, Transform visual, Vector3 modelRotationOffset)
        {
            source = sourceRect;
            targetRoot = targetRectRoot;
            visualRoot = visual;
            rotationOffset = modelRotationOffset;
            CacheVisualBounds();
            FitVisualToSource();
            FaceNow();
        }

        public void FaceNow()
        {
            if (visualRoot == null || source == null || targetRoot == null)
                return;

            Vector3 sourcePosition = source.position;
            Vector3 targetPosition = TargetCenter();
            Vector2 boardDirection = new(targetPosition.x - sourcePosition.x, targetPosition.y - sourcePosition.y);
            if (boardDirection.sqrMagnitude < 0.001f)
                return;

            Vector3 lookDirection = new(boardDirection.x, 0f, boardDirection.y);
            visualRoot.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                * Quaternion.Euler(rotationOffset);
        }

        private void LateUpdate()
        {
            FitVisualToSource();
            FaceNow();
        }

        private Vector3 TargetCenter()
        {
            int count = 0;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < targetRoot.childCount; i++)
            {
                Transform child = targetRoot.GetChild(i);
                if (!child.gameObject.activeInHierarchy)
                    continue;

                sum += child.position;
                count++;
            }

            return count > 0 ? sum / count : targetRoot.position;
        }

        private void FitVisualToSource()
        {
            if (visualRoot == null || source == null || !hasVisualBounds)
                return;

            Vector2 sourceSize = source.rect.size;
            if ((sourceSize - lastSourceSize).sqrMagnitude < 0.01f)
                return;

            float targetHeight = Mathf.Max(24f, sourceSize.y * targetRectFill);
            float modelHeight = Mathf.Max(0.001f, visualBounds.size.y);
            float scale = targetHeight / modelHeight;
            visualRoot.localScale = Vector3.one * scale;
            visualRoot.localPosition = new Vector3(0f, 0f, -8f);
            lastSourceSize = sourceSize;
        }

        private void CacheVisualBounds()
        {
            hasVisualBounds = false;
            if (visualRoot == null)
                return;

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds bounds = renderers[i].bounds;
                if (!hasVisualBounds)
                {
                    visualBounds = bounds;
                    hasVisualBounds = true;
                }
                else
                {
                    visualBounds.Encapsulate(bounds);
                }
            }
        }
    }
}
