using UnityEngine;

namespace AccardND.Presentation.ReviewPrompt
{
    /// <summary>
    /// Genera la stella del popup recensione. Il progetto non ha uno sprite stella e
    /// <see cref="AccardND.Battlefield.MmoUiTheme"/> gia' disegna pannelli e bottoni a
    /// codice: una stella procedurale segue la stessa strada e non aggiunge un asset da
    /// mantenere.
    /// </summary>
    internal static class ReviewStarSprite
    {
        private const int Size = 128;

        // 4x4 campioni per pixel: senza supersampling le punte della stella diventano
        // una scaletta ben visibile a questa dimensione.
        private const int Samples = 4;

        private static Sprite filled;
        private static Sprite outline;

        public static Sprite Filled => filled != null ? filled : filled = Build(fill: true);

        public static Sprite Outline => outline != null ? outline : outline = Build(fill: false);

        private static Sprite Build(bool fill)
        {
            Vector2[] polygon = BuildStarPolygon();
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = fill ? "AccardReviewStarFilled" : "AccardReviewStarOutline"
            };

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float coverage = Coverage(polygon, x, y, fill);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, Size, Size),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100f);
        }

        /// <summary>
        /// Quanta parte del pixel cade dentro la stella. Per la versione vuota si tiene
        /// solo l'anello di bordo, cosi' la stella non selezionata resta leggibile senza
        /// sembrare piena a meta'.
        /// </summary>
        private static float Coverage(Vector2[] polygon, int x, int y, bool fill)
        {
            int inside = 0;
            int total = Samples * Samples;

            for (int sy = 0; sy < Samples; sy++)
            {
                for (int sx = 0; sx < Samples; sx++)
                {
                    var point = new Vector2(
                        x + (sx + 0.5f) / Samples,
                        y + (sy + 0.5f) / Samples);

                    bool inShape = Contains(polygon, point);
                    if (!fill && inShape)
                    {
                        // Bordo: dentro la stella grande ma fuori da quella rimpicciolita.
                        inShape = !Contains(Shrink(polygon, 0.82f), point);
                    }

                    if (inShape)
                        inside++;
                }
            }

            return (float)inside / total;
        }

        private static Vector2[] BuildStarPolygon()
        {
            const int points = 5;
            var vertices = new Vector2[points * 2];
            var center = new Vector2(Size / 2f, Size / 2f);
            float outerRadius = Size * 0.47f;
            float innerRadius = outerRadius * 0.42f;

            for (int i = 0; i < vertices.Length; i++)
            {
                // Si parte dalla punta in alto e si alternano raggio esterno e interno.
                float angle = Mathf.PI / 2f + i * Mathf.PI / points;
                float radius = (i % 2 == 0) ? outerRadius : innerRadius;
                vertices[i] = center + new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius);
            }

            return vertices;
        }

        private static Vector2[] Shrink(Vector2[] polygon, float factor)
        {
            var center = new Vector2(Size / 2f, Size / 2f);
            var shrunk = new Vector2[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
                shrunk[i] = center + (polygon[i] - center) * factor;
            return shrunk;
        }

        /// <summary>Ray casting orizzontale: dispari = dentro.</summary>
        private static bool Contains(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if (polygon[i].y > point.y == polygon[j].y > point.y)
                    continue;

                float crossX = (polygon[j].x - polygon[i].x)
                    * (point.y - polygon[i].y)
                    / (polygon[j].y - polygon[i].y)
                    + polygon[i].x;

                if (point.x < crossX)
                    inside = !inside;
            }

            return inside;
        }
    }
}
