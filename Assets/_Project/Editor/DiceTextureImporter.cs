using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    public sealed class DiceTextureImporter : AssetPostprocessor
    {
        private const string DicePath = "Assets/_Project/Art/Dice/";
        private const int Columns = 4;
        private const int Rows = 2;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(DicePath, StringComparison.Ordinal))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            AndroidTextureCompression.Apply(importer, 1024, TextureImporterFormat.ASTC_4x4);

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            float frameSize = Mathf.Floor(width / (float)Columns);
            string sheetName = Path.GetFileNameWithoutExtension(assetPath);
            int animationFrameCount = sheetName == "D8" || sheetName == "D12" ? 7 : Columns * Rows;
            var sprites = new System.Collections.Generic.List<SpriteMetaData>();
            Color32[] sourcePixels = LoadSourcePixels(width, height);

            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    int index = row * Columns + column;
                    if (index >= animationFrameCount)
                        continue;

                    sprites.Add(new SpriteMetaData
                    {
                        name = $"{sheetName}_roll_{index}",
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        rect = new Rect(
                            column * frameSize,
                            height - (row + 1) * frameSize,
                            frameSize,
                            frameSize)
                    });
                }
            }

            AddResultSprites(sprites, sheetName, width, height, frameSize, sourcePixels);

#pragma warning disable CS0618
            importer.spritesheet = sprites.ToArray();
#pragma warning restore CS0618
        }

        private static void AddResultSprites(
            System.Collections.Generic.ICollection<SpriteMetaData> sprites,
            string sheetName,
            int width,
            int height,
            float rollFrameSize,
            Color32[] sourcePixels)
        {
            if (!int.TryParse(sheetName.Substring(1), out int sides))
                return;

            int[] rowCounts;
            int duplicateGroups = 1;
            int firstResult = 1;

            switch (sides)
            {
                case 4:
                    rowCounts = new[] { 4 };
                    duplicateGroups = 2;
                    break;
                case 6:
                    rowCounts = new[] { 6 };
                    duplicateGroups = 2;
                    break;
                case 8:
                    rowCounts = new[] { 4, 4 };
                    break;
                case 10:
                    rowCounts = new[] { 4, 4, 3 };
                    firstResult = 0;
                    break;
                case 12:
                    rowCounts = new[] { 4, 4, 4 };
                    break;
                case 20:
                    rowCounts = new[] { 5, 5, 5, 5 };
                    break;
                default:
                    return;
            }

            float rollAreaHeight = Rows * rollFrameSize;
            float resultAreaHeight = Mathf.Floor((height - rollAreaHeight) / duplicateGroups);
            float resultAreaTop = height - rollAreaHeight;
            float rowHeight = Mathf.Floor(resultAreaHeight / rowCounts.Length);
            int maximumColumns = 0;
            foreach (int count in rowCounts)
                maximumColumns = Mathf.Max(maximumColumns, count);
            float columnWidth = Mathf.Floor(width / (float)maximumColumns);

            int result = firstResult;
            for (int row = 0; row < rowCounts.Length; row++)
            {
                int columnsInRow = rowCounts[row];
                float offsetX = Mathf.Floor((width - columnsInRow * columnWidth) * 0.5f);
                int regionY = Mathf.RoundToInt(Mathf.Clamp(
                    resultAreaTop - (row + 1) * rowHeight,
                    0f,
                    height - rowHeight));
                var rowRegion = new RectInt(0, regionY, width, Mathf.RoundToInt(rowHeight));
                List<Rect> detectedRects = DetectOpaqueObjects(
                    sourcePixels,
                    width,
                    height,
                    rowRegion,
                    columnsInRow,
                    columnWidth,
                    rowHeight);

                for (int column = 0; column < columnsInRow; column++)
                {
                    float x = Mathf.Clamp(offsetX + column * columnWidth, 0f, width - columnWidth);
                    float y = Mathf.Clamp(
                        resultAreaTop - (row + 1) * rowHeight,
                        0f,
                        height - rowHeight);
                    Rect rect = detectedRects.Count == columnsInRow
                        ? detectedRects[column]
                        : new Rect(x, y, columnWidth, rowHeight);
                    sprites.Add(new SpriteMetaData
                    {
                        name = $"{sheetName}_result_{result:00}",
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        rect = rect
                    });
                    result++;
                }
            }
        }

        private Color32[] LoadSourcePixels(int expectedWidth, int expectedHeight)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(Path.GetFullPath(assetPath));
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, false)
                    || texture.width != expectedWidth
                    || texture.height != expectedHeight)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    return Array.Empty<Color32>();
                }

                Color32[] pixels = texture.GetPixels32();
                UnityEngine.Object.DestroyImmediate(texture);
                return pixels;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Impossibile analizzare alpha di {assetPath}: {exception.Message}");
                return Array.Empty<Color32>();
            }
        }

        private static List<Rect> DetectOpaqueObjects(
            Color32[] pixels,
            int textureWidth,
            int textureHeight,
            RectInt region,
            int expectedCount,
            float expectedWidth,
            float expectedHeight)
        {
            if (pixels.Length != textureWidth * textureHeight)
                return new List<Rect>();

            int minimumX = Mathf.Clamp(region.xMin, 0, textureWidth);
            int maximumX = Mathf.Clamp(region.xMax, 0, textureWidth);
            int minimumY = Mathf.Clamp(region.yMin, 0, textureHeight);
            int maximumY = Mathf.Clamp(region.yMax, 0, textureHeight);
            var visited = new bool[region.width * region.height];
            var components = new List<OpaqueComponent>();
            var pending = new Stack<Vector2Int>();

            for (int y = minimumY; y < maximumY; y++)
            {
                for (int x = minimumX; x < maximumX; x++)
                {
                    int localIndex = (y - minimumY) * region.width + (x - minimumX);
                    if (visited[localIndex] || pixels[y * textureWidth + x].a < 20)
                        continue;

                    var component = new OpaqueComponent(x, y);
                    pending.Push(new Vector2Int(x, y));
                    visited[localIndex] = true;

                    while (pending.Count > 0)
                    {
                        Vector2Int point = pending.Pop();
                        component.Include(point.x, point.y);

                        TryVisit(point.x - 1, point.y);
                        TryVisit(point.x + 1, point.y);
                        TryVisit(point.x, point.y - 1);
                        TryVisit(point.x, point.y + 1);
                    }

                    if (component.Width >= expectedWidth * 0.28f
                        && component.Height >= expectedHeight * 0.28f)
                    {
                        components.Add(component);
                    }

                    void TryVisit(int nextX, int nextY)
                    {
                        if (nextX < minimumX || nextX >= maximumX || nextY < minimumY || nextY >= maximumY)
                            return;

                        int nextLocalIndex = (nextY - minimumY) * region.width + (nextX - minimumX);
                        if (visited[nextLocalIndex])
                            return;

                        visited[nextLocalIndex] = true;
                        if (pixels[nextY * textureWidth + nextX].a >= 20)
                            pending.Push(new Vector2Int(nextX, nextY));
                    }
                }
            }

            List<OpaqueComponent> selected = components
                .OrderByDescending(component => component.Area)
                .Take(expectedCount)
                .OrderBy(component => component.MinimumX)
                .ToList();

            if (selected.Count != expectedCount)
                return new List<Rect>();

            const int padding = 3;
            return selected.Select(component =>
            {
                int x = Mathf.Max(0, component.MinimumX - padding);
                int y = Mathf.Max(0, component.MinimumY - padding);
                int maxX = Mathf.Min(textureWidth, component.MaximumX + padding + 1);
                int maxY = Mathf.Min(textureHeight, component.MaximumY + padding + 1);
                return new Rect(x, y, maxX - x, maxY - y);
            }).ToList();
        }

        private sealed class OpaqueComponent
        {
            public OpaqueComponent(int x, int y)
            {
                MinimumX = MaximumX = x;
                MinimumY = MaximumY = y;
            }

            public int MinimumX { get; private set; }
            public int MaximumX { get; private set; }
            public int MinimumY { get; private set; }
            public int MaximumY { get; private set; }
            public int Area { get; private set; }
            public int Width => MaximumX - MinimumX + 1;
            public int Height => MaximumY - MinimumY + 1;

            public void Include(int x, int y)
            {
                MinimumX = Mathf.Min(MinimumX, x);
                MaximumX = Mathf.Max(MaximumX, x);
                MinimumY = Mathf.Min(MinimumY, y);
                MaximumY = Mathf.Max(MaximumY, y);
                Area++;
            }
        }
    }
}
