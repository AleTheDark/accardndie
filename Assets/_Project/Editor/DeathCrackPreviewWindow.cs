using UnityEditor;
using UnityEngine;

internal sealed class DeathCrackPreviewWindow : EditorWindow
{
    private static readonly string[] ResourcePaths =
    {
        "UI/assassin_crack",
        "UI/warrior_crack",
        "UI/mage_crack",
        "UI/paladin_crack",
        "UI/rogue_crack",
        "UI/hunter_crack",
        "UI/barbarian_crack",
        "UI/necromancer_crack",
        "UI/priest_crack"
    };

    [InitializeOnLoadMethod]
    private static void OpenOnceAfterCompile()
    {
        if (SessionState.GetBool("AccardND.DeathCrackPreviewOpened", false))
            return;

        SessionState.SetBool("AccardND.DeathCrackPreviewOpened", true);
        EditorApplication.delayCall += Open;
    }

    [MenuItem("AccardND/Tests/Death Crack Preview")]
    private static void Open()
    {
        DeathCrackPreviewWindow window = GetWindow<DeathCrackPreviewWindow>();
        window.titleContent = new GUIContent("Death Crack Preview");
        window.minSize = new Vector2(520f, 520f);
        window.Show();
    }

    private void OnGUI()
    {
        float padding = 16f;
        float gap = 12f;
        float cell = Mathf.Floor((position.width - padding * 2f - gap * 2f) / 3f);
        cell = Mathf.Clamp(cell, 120f, 220f);

        Rect root = new Rect(
            padding,
            padding,
            cell * 3f + gap * 2f,
            cell * 3f + gap * 2f);

        for (int index = 0; index < ResourcePaths.Length; index++)
        {
            int column = index % 3;
            int row = index / 3;
            Rect rect = new Rect(
                root.x + column * (cell + gap),
                root.y + row * (cell + gap),
                cell,
                cell);

            DrawSpriteCell(rect, Resources.Load<Sprite>(ResourcePaths[index]));
        }
    }

    private static void DrawSpriteCell(Rect rect, Sprite sprite)
    {
        EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.09f, 1f));
        if (sprite == null)
        {
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.02f, 0.02f, 1f));
            return;
        }

        Texture2D texture = sprite.texture;
        Rect texCoords = new Rect(
            sprite.textureRect.x / texture.width,
            sprite.textureRect.y / texture.height,
            sprite.textureRect.width / texture.width,
            sprite.textureRect.height / texture.height);
        GUI.DrawTextureWithTexCoords(rect, texture, texCoords, true);
    }
}
