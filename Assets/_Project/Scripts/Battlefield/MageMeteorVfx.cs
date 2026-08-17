using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    /// <summary>A real 3D, URP-rendered meteor presented inside the battle UI.</summary>
    internal sealed class MageMeteorVfx : MonoBehaviour
    {
        private const int VfxLayer = 30;
        private RenderTexture target;
        private Material meteorMaterial;
        private Transform meteor;
        private Camera vfxCamera;

        public RectTransform Rect => (RectTransform)transform;

        public static MageMeteorVfx Create(RectTransform parent, Texture texture)
        {
            GameObject root = new("Mage Supreme 3D Meteor", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(MageMeteorVfx));
            root.transform.SetParent(parent, false);
            MageMeteorVfx effect = root.GetComponent<MageMeteorVfx>();
            effect.Build(texture);
            return effect;
        }

        private void Build(Texture texture)
        {
            Rect.sizeDelta = new Vector2(720f, 720f);
            target = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGBHalf) { name = "Mage Meteor VFX", antiAliasing = 1 };
            target.Create();
            RawImage image = GetComponent<RawImage>();
            image.texture = target;
            image.raycastTarget = false;

            GameObject cameraObject = new("Mage Meteor VFX Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            vfxCamera = cameraObject.GetComponent<Camera>();
            vfxCamera.clearFlags = CameraClearFlags.SolidColor;
            vfxCamera.backgroundColor = Color.clear;
            vfxCamera.orthographic = true;
            vfxCamera.orthographicSize = 1.25f;
            vfxCamera.nearClipPlane = 0.1f;
            vfxCamera.farClipPlane = 10f;
            vfxCamera.cullingMask = 1 << VfxLayer;
            vfxCamera.targetTexture = target;
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -4f);

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Cracking Meteor Sphere";
            sphere.layer = VfxLayer;
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = Vector3.zero;
            Destroy(sphere.GetComponent<Collider>());
            meteor = sphere.transform;
            Shader shader = Shader.Find("AccardND/VFX/Mage Meteor");
            meteorMaterial = new Material(shader) { name = "Mage Meteor Runtime Material" };
            meteorMaterial.SetTexture("_BaseMap", texture);
            meteorMaterial.SetFloat("_Dissolve", 0f);
            sphere.GetComponent<Renderer>().sharedMaterial = meteorMaterial;
        }

        public void SetFlight(float time, float progress)
        {
            meteor.localRotation = Quaternion.Euler(time * 93f, time * 151f, time * 47f);
            meteor.localScale = Vector3.one * (0.88f + Mathf.Sin(time * 9f) * 0.025f);
            meteorMaterial.SetFloat("_EmissionStrength", 2.5f + Mathf.Sin(time * 11f) * 0.65f + progress);
        }

        public void SetDissolve(float value)
        {
            if (meteorMaterial != null) meteorMaterial.SetFloat("_Dissolve", value);
            if (meteor != null) meteor.localRotation *= Quaternion.Euler(2.8f, 4.5f, 1.7f);
        }

        private void OnDestroy()
        {
            if (meteorMaterial != null) Destroy(meteorMaterial);
            if (target != null) { target.Release(); Destroy(target); }
        }
    }
}
