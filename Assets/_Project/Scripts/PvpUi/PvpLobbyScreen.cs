using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>
    /// Lobby: creazione stanza, join con codice (tastierino a schermo) e coda.
    /// </summary>
    internal sealed class PvpLobbyScreen
    {
        private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        private const int CodeLength = 6;

        private readonly RectTransform root;
        private readonly Text statusText;
        private readonly Text roomCodeText;
        private readonly Text typedCodeText;
        private string typedCode = string.Empty;

        public PvpLobbyScreen(
            Transform parent,
            string username,
            UnityAction onCreateRoom,
            UnityAction onJoinRoom,
            UnityAction onQueue,
            UnityAction onClose)
        {
            root = PvpUiFactory.CreatePanel(parent, "Lobby", new Color(0.07f, 0.1f, 0.14f, 0.98f));
            PvpUiFactory.Stretch(root);

            Text title = PvpUiFactory.CreateText(root, "Title", "MULTIPLAYER PVP", 40);
            PvpUiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0.1f, 0.9f), new Vector2(0.9f, 0.99f));

            Button close = PvpUiFactory.CreateButton(
                root, "Close", "X", new Color(0.5f, 0.12f, 0.12f, 0.98f), onClose, 26);
            PvpUiFactory.SetAnchors((RectTransform)close.transform, new Vector2(0.93f, 0.92f), new Vector2(0.985f, 0.99f));

            Text player = PvpUiFactory.CreateText(
                root, "Player", $"Account: {username}", 18, TextAnchor.MiddleCenter, FontStyle.Normal);
            player.color = new Color(0.75f, 0.85f, 0.95f);
            PvpUiFactory.SetAnchors((RectTransform)player.transform, new Vector2(0.1f, 0.845f), new Vector2(0.9f, 0.9f));

            Button create = PvpUiFactory.CreateButton(
                root, "CreateRoom", "CREA STANZA", new Color(0.05f, 0.45f, 0.5f, 0.98f), onCreateRoom, 24);
            PvpUiFactory.SetAnchors((RectTransform)create.transform, new Vector2(0.06f, 0.72f), new Vector2(0.47f, 0.82f));

            Button queue = PvpUiFactory.CreateButton(
                root, "Queue", "CERCA AVVERSARIO", new Color(0.28f, 0.12f, 0.45f, 0.98f), onQueue, 24);
            PvpUiFactory.SetAnchors((RectTransform)queue.transform, new Vector2(0.53f, 0.72f), new Vector2(0.94f, 0.82f));

            roomCodeText = PvpUiFactory.CreateText(root, "RoomCode", string.Empty, 42);
            roomCodeText.color = new Color(1f, 0.85f, 0.3f);
            PvpUiFactory.SetAnchors((RectTransform)roomCodeText.transform, new Vector2(0.06f, 0.6f), new Vector2(0.94f, 0.71f));

            // --- Join con codice: display + tastierino ---
            Text joinCaption = PvpUiFactory.CreateText(
                root, "JoinCaption", "ENTRA CON CODICE:", 20, TextAnchor.MiddleLeft);
            PvpUiFactory.SetAnchors((RectTransform)joinCaption.transform, new Vector2(0.06f, 0.5f), new Vector2(0.4f, 0.58f));

            typedCodeText = PvpUiFactory.CreateText(root, "TypedCode", "______", 34);
            typedCodeText.color = new Color(0.6f, 0.95f, 1f);
            PvpUiFactory.SetAnchors((RectTransform)typedCodeText.transform, new Vector2(0.38f, 0.5f), new Vector2(0.68f, 0.58f));

            Button join = PvpUiFactory.CreateButton(
                root, "JoinRoom", "ENTRA", new Color(0.5f, 0.32f, 0.05f, 0.98f), onJoinRoom, 22);
            PvpUiFactory.SetAnchors((RectTransform)join.transform, new Vector2(0.7f, 0.495f), new Vector2(0.86f, 0.585f));

            Button erase = PvpUiFactory.CreateButton(
                root, "Erase", "<", new Color(0.35f, 0.35f, 0.4f, 0.98f), EraseChar, 24);
            PvpUiFactory.SetAnchors((RectTransform)erase.transform, new Vector2(0.875f, 0.495f), new Vector2(0.94f, 0.585f));

            BuildKeypad();

            statusText = PvpUiFactory.CreateText(
                root, "Status", "Connessione...", 20, TextAnchor.MiddleCenter, FontStyle.Normal);
            PvpUiFactory.SetAnchors((RectTransform)statusText.transform, new Vector2(0.03f, 0.01f), new Vector2(0.97f, 0.11f));
        }

        public string TypedRoomCode => typedCode;

        public void SetStatus(string message) => statusText.text = message;

        public void ShowRoomCode(string code) => roomCodeText.text = $"CODICE STANZA: {code}";

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void Destroy() => Object.Destroy(root.gameObject);

        private void BuildKeypad()
        {
            const int columns = 11;
            const float xStart = 0.06f;
            const float xEnd = 0.94f;
            const float yStart = 0.44f;
            const float rowHeight = 0.095f;
            float cellWidth = (xEnd - xStart) / columns;

            for (int index = 0; index < CodeAlphabet.Length; index++)
            {
                char letter = CodeAlphabet[index];
                int row = index / columns;
                int column = index % columns;
                float xMin = xStart + column * cellWidth;
                float yMax = yStart - row * rowHeight;
                Button key = PvpUiFactory.CreateButton(
                    root, $"Key{letter}", letter.ToString(),
                    new Color(0.13f, 0.2f, 0.28f, 0.98f), () => AppendChar(letter), 22);
                PvpUiFactory.SetAnchors(
                    (RectTransform)key.transform,
                    new Vector2(xMin + 0.003f, yMax - rowHeight + 0.012f),
                    new Vector2(xMin + cellWidth - 0.003f, yMax));
            }
        }

        private void AppendChar(char letter)
        {
            if (typedCode.Length >= CodeLength)
                return;
            typedCode += letter;
            RefreshTypedCode();
        }

        private void EraseChar()
        {
            if (typedCode.Length == 0)
                return;
            typedCode = typedCode.Substring(0, typedCode.Length - 1);
            RefreshTypedCode();
        }

        private void RefreshTypedCode() =>
            typedCodeText.text = typedCode.PadRight(CodeLength, '_');
    }
}
