using System;
using AccardND.GameCore;
using UnityEngine;

namespace AccardND.Presentation
{
    /// <summary>
    /// Small, cached procedural sprites used by the composable golem UI VFX.
    /// They replace the default white UI rectangle with material-specific silhouettes
    /// while keeping the effects independent from imported textures and shaders.
    /// </summary>
    internal static class ComposableGolemVfxSprites
    {
        private const float TwoPi = Mathf.PI * 2f;

        private static Sprite glow;
        private static readonly Sprite[] telegraphs = new Sprite[3];
        private static readonly Sprite[,] trails = new Sprite[3, 3];
        private static readonly Sprite[,] particles = new Sprite[3, 3];
        private static readonly Sprite[,] facets = new Sprite[3, 2];

        public static Sprite Glow
        {
            get
            {
                if (glow == null)
                {
                    glow = CreateSprite("Golem VFX Soft Core", 96, 96, (x, y) =>
                    {
                        float radius = Mathf.Sqrt(x * x + y * y);
                        float core = Mathf.Pow(Mathf.Clamp01(1f - radius), 2.4f);
                        float corona = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.58f) / 0.42f) * 0.18f;
                        return Mathf.Max(core, corona);
                    });
                }
                return glow;
            }
        }

        public static Sprite Telegraph(ComposableGolemForm form)
        {
            int formIndex = FormIndex(form);
            return telegraphs[formIndex] != null
                ? telegraphs[formIndex]
                : telegraphs[formIndex] = CreateTelegraph(form);
        }

        public static Sprite Trail(ComposableGolemForm form, int variant)
        {
            int formIndex = FormIndex(form);
            int variantIndex = Mathf.Abs(variant) % trails.GetLength(1);
            return trails[formIndex, variantIndex] != null
                ? trails[formIndex, variantIndex]
                : trails[formIndex, variantIndex] = CreateTrail(form, variantIndex);
        }

        public static Sprite Particle(ComposableGolemForm form, int variant)
        {
            int formIndex = FormIndex(form);
            int variantIndex = Mathf.Abs(variant) % particles.GetLength(1);
            return particles[formIndex, variantIndex] != null
                ? particles[formIndex, variantIndex]
                : particles[formIndex, variantIndex] = CreateShard(form, variantIndex, false);
        }

        public static Sprite Facet(ComposableGolemForm form, int variant)
        {
            int formIndex = FormIndex(form);
            int variantIndex = Mathf.Abs(variant) % facets.GetLength(1);
            return facets[formIndex, variantIndex] != null
                ? facets[formIndex, variantIndex]
                : facets[formIndex, variantIndex] = CreateShard(form, variantIndex, true);
        }

        private static int FormIndex(ComposableGolemForm form)
        {
            return form switch
            {
                ComposableGolemForm.Iron => 0,
                ComposableGolemForm.Crystal => 1,
                ComposableGolemForm.Glass => 2,
                _ => 0
            };
        }

        private static Sprite CreateTelegraph(ComposableGolemForm form)
        {
            string name = $"Golem {form} Combat Sigil";
            return CreateSprite(name, 128, 128, (x, y) =>
            {
                float radius = Mathf.Sqrt(x * x + y * y);
                float angle = Mathf.Atan2(y, x);

                switch (form)
                {
                    case ComposableGolemForm.Iron:
                    {
                        float tooth = Mathf.Cos(angle * 12f) > 0.12f ? 0.075f : 0f;
                        float gearRadius = 0.73f + tooth;
                        float gear = SoftBand(radius - gearRadius, 0.045f, 0.025f);
                        float brace = Mathf.Max(
                            SoftBand(Mathf.Abs(x) - 0.055f, 0.022f, 0.018f),
                            SoftBand(Mathf.Abs(y) - 0.055f, 0.022f, 0.018f));
                        brace *= SoftInside(radius, 0.56f, 0.05f) * SoftOutside(radius, 0.25f, 0.04f);

                        float localAngle = Mathf.Repeat(angle + Mathf.PI / 8f, Mathf.PI / 4f) - Mathf.PI / 8f;
                        float boltDistance = Mathf.Sqrt(
                            (radius - 0.53f) * (radius - 0.53f)
                            + (localAngle * 0.53f) * (localAngle * 0.53f));
                        float bolts = SoftInside(boltDistance, 0.055f, 0.025f);
                        return Mathf.Max(gear, Mathf.Max(brace * 0.72f, bolts));
                    }

                    case ComposableGolemForm.Crystal:
                    {
                        float boundary = PolygonBoundary(angle, 6, 0.8f);
                        float hexagon = SoftBand(radius - boundary, 0.038f, 0.022f);
                        float spokeDistance = Mathf.Abs(Mathf.Sin(angle * 3f)) * radius;
                        float facetsAlpha = SoftInside(spokeDistance, 0.022f, 0.018f)
                            * SoftInside(radius, 0.69f, 0.04f)
                            * SoftOutside(radius, 0.2f, 0.05f);
                        float coreDiamond = SoftBand(Mathf.Abs(x) + Mathf.Abs(y) * 0.62f - 0.22f, 0.025f, 0.02f);
                        return Mathf.Max(hexagon, Mathf.Max(facetsAlpha * 0.72f, coreDiamond));
                    }

                    case ComposableGolemForm.Glass:
                    default:
                    {
                        float boundary = PolygonBoundary(angle + 0.16f, 8, 0.81f);
                        float breaks = Smooth01(Mathf.Clamp01((Mathf.Abs(Mathf.Sin(angle * 3.5f + 0.4f)) - 0.09f) * 7f));
                        float outerArc = SoftBand(radius - boundary, 0.034f, 0.024f) * breaks;
                        float innerArc = SoftBand(radius - 0.57f - Mathf.Sin(angle * 4f) * 0.025f, 0.022f, 0.02f)
                            * Mathf.Clamp01((Mathf.Abs(Mathf.Cos(angle * 2.5f - 0.2f)) - 0.13f) * 6f);
                        float refraction = SoftInside(Mathf.Abs(y - x * 0.28f), 0.018f, 0.018f)
                            * SoftInside(radius, 0.7f, 0.04f)
                            * SoftOutside(radius, 0.18f, 0.04f);
                        return Mathf.Max(outerArc, Mathf.Max(innerArc * 0.78f, refraction * 0.54f));
                    }
                }
            });
        }

        private static Sprite CreateTrail(ComposableGolemForm form, int variant)
        {
            string name = $"Golem {form} Attack Ribbon {variant + 1}";
            return CreateSprite(name, 192, 64, (x, y) =>
            {
                float progress = Mathf.Clamp01((x + 1f) * 0.5f);
                float envelope = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(progress * Mathf.PI)), 0.42f);
                float softness = variant == 0 ? 0.34f : variant == 1 ? 0.2f : 0.1f;
                float alpha;

                switch (form)
                {
                    case ComposableGolemForm.Iron:
                    {
                        float hammerHead = Mathf.SmoothStep(0f, 1f, progress) * 0.24f;
                        float jaggedWidth = (0.16f + envelope * (0.5f + hammerHead))
                            * (0.9f + Mathf.Sin(progress * 52f) * 0.08f);
                        alpha = SoftInside(Mathf.Abs(y), jaggedWidth, softness);
                        float moltenSeam = SoftInside(Mathf.Abs(y + Mathf.Sin(progress * 18f) * 0.055f), 0.055f, 0.04f);
                        alpha = Mathf.Max(alpha * (variant == 0 ? 0.62f : 0.9f), moltenSeam);
                        break;
                    }

                    case ComposableGolemForm.Crystal:
                    {
                        float spearWidth = 0.08f + envelope * (variant == 0 ? 0.58f : variant == 1 ? 0.34f : 0.16f);
                        float crystalline = spearWidth * (0.82f + Mathf.Abs(Mathf.Sin(progress * 16f)) * 0.22f);
                        alpha = SoftInside(Mathf.Abs(y), crystalline, softness);
                        float upperRay = SoftInside(Mathf.Abs(y - 0.34f * Mathf.Sin(progress * Mathf.PI)), 0.035f, 0.03f);
                        float lowerRay = SoftInside(Mathf.Abs(y + 0.27f * Mathf.Sin(progress * Mathf.PI)), 0.028f, 0.03f);
                        alpha = Mathf.Max(alpha, Mathf.Max(upperRay, lowerRay) * envelope * (variant == 0 ? 0.5f : 0.78f));
                        break;
                    }

                    case ComposableGolemForm.Glass:
                    default:
                    {
                        float waveA = Mathf.Sin(progress * 12f + variant * 0.7f) * 0.2f;
                        float waveB = -Mathf.Sin(progress * 9f + 1.4f) * 0.28f;
                        float ribbonA = SoftInside(Mathf.Abs(y - waveA), variant == 0 ? 0.3f : 0.13f, softness);
                        float ribbonB = SoftInside(Mathf.Abs(y - waveB), variant == 0 ? 0.2f : 0.075f, softness * 0.8f);
                        float caustic = SoftInside(Mathf.Abs(y + Mathf.Sin(progress * 22f) * 0.42f), 0.025f, 0.025f);
                        alpha = Mathf.Max(ribbonA * 0.72f, Mathf.Max(ribbonB * 0.84f, caustic));
                        break;
                    }
                }

                float endFade = Smooth01(Mathf.Clamp01(progress * 8f))
                    * Smooth01(Mathf.Clamp01((1f - progress) * 10f));
                return alpha * envelope * endFade;
            });
        }

        private static Sprite CreateShard(ComposableGolemForm form, int variant, bool facet)
        {
            Vector2[] vertices;
            switch (form)
            {
                case ComposableGolemForm.Iron:
                    vertices = facet
                        ? new[]
                        {
                            new Vector2(-0.96f, -0.36f), new Vector2(0.48f, -0.5f),
                            new Vector2(0.98f, -0.08f), new Vector2(0.68f, 0.42f),
                            new Vector2(-0.72f, 0.5f), new Vector2(-1f, 0.08f)
                        }
                        : new[]
                        {
                            new Vector2(-0.98f, -0.14f), new Vector2(0.42f, -0.34f),
                            new Vector2(1f, 0f), new Vector2(0.42f, 0.34f),
                            new Vector2(-0.98f, 0.14f)
                        };
                    break;

                case ComposableGolemForm.Crystal:
                    vertices = facet
                        ? new[]
                        {
                            new Vector2(-0.3f, -1f), new Vector2(0.56f, -0.34f),
                            new Vector2(0.32f, 0.88f), new Vector2(-0.12f, 1f),
                            new Vector2(-0.58f, 0.14f)
                        }
                        : new[]
                        {
                            new Vector2(-0.18f, -1f), new Vector2(0.42f, -0.26f),
                            new Vector2(0.12f, 1f), new Vector2(-0.38f, 0.22f)
                        };
                    break;

                case ComposableGolemForm.Glass:
                default:
                    vertices = facet
                        ? new[]
                        {
                            new Vector2(-0.72f, -0.82f), new Vector2(0.48f, -1f),
                            new Vector2(0.92f, 0.18f), new Vector2(0.2f, 1f),
                            new Vector2(-0.86f, 0.48f)
                        }
                        : new[]
                        {
                            new Vector2(-0.48f, -1f), new Vector2(0.68f, -0.42f),
                            new Vector2(0.3f, 1f), new Vector2(-0.72f, 0.4f)
                        };
                    break;
            }

            float rotation = variant * (form == ComposableGolemForm.Iron ? 0.12f : 0.19f);
            float cosine = Mathf.Cos(rotation);
            float sine = Mathf.Sin(rotation);
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector2 vertex = vertices[index];
                vertices[index] = new Vector2(
                    vertex.x * cosine - vertex.y * sine,
                    vertex.x * sine + vertex.y * cosine);
            }

            string name = $"Golem {form} {(facet ? "Armor Facet" : "Shard")} {variant + 1}";
            return CreatePolygonSprite(name, vertices, facet ? 80 : 64);
        }

        private static Sprite CreatePolygonSprite(string name, Vector2[] vertices, int size)
        {
            return CreateSprite(name, size, size, (x, y) =>
            {
                Vector2 point = new(x, y);
                bool inside = ContainsPoint(vertices, point);
                float edgeDistance = float.MaxValue;
                for (int index = 0; index < vertices.Length; index++)
                {
                    Vector2 a = vertices[index];
                    Vector2 b = vertices[(index + 1) % vertices.Length];
                    edgeDistance = Mathf.Min(edgeDistance, DistanceToSegment(point, a, b));
                }

                if (inside)
                    return Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(edgeDistance * 12f));
                return Mathf.Clamp01(1f - edgeDistance / 0.055f) * 0.7f;
            });
        }

        private static Sprite CreateSprite(string name, int width, int height, Func<float, float, float> alphaField)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name + " Texture",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float normalizedY = ((y + 0.5f) / height) * 2f - 1f;
                for (int x = 0; x < width; x++)
                {
                    float normalizedX = ((x + 0.5f) / width) * 2f - 1f;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaField(normalizedX, normalizedY)) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static float PolygonBoundary(float angle, int sides, float radius)
        {
            float sector = TwoPi / sides;
            float localAngle = Mathf.Repeat(angle + sector * 0.5f, sector) - sector * 0.5f;
            return radius * Mathf.Cos(Mathf.PI / sides) / Mathf.Max(0.001f, Mathf.Cos(localAngle));
        }

        private static float SoftBand(float signedDistance, float halfWidth, float feather)
        {
            return 1f - Smooth01(Mathf.Clamp01((Mathf.Abs(signedDistance) - halfWidth) / feather));
        }

        private static float SoftInside(float value, float edge, float feather)
        {
            return 1f - Smooth01(Mathf.Clamp01((value - edge) / feather));
        }

        private static float SoftOutside(float value, float edge, float feather)
        {
            return Smooth01(Mathf.Clamp01((value - edge) / feather));
        }

        private static float Smooth01(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static bool ContainsPoint(Vector2[] vertices, Vector2 point)
        {
            bool inside = false;
            for (int current = 0, previous = vertices.Length - 1; current < vertices.Length; previous = current++)
            {
                Vector2 a = vertices[current];
                Vector2 b = vertices[previous];
                bool crosses = (a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (crosses)
                    inside = !inside;
            }
            return inside;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
                return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared);
            return Vector2.Distance(point, a + segment * t);
        }
    }
}
