using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>
    /// Lobby: creazione stanza, join con codice e coda. Su mobile il codice si
    /// digita con la tastiera nativa del dispositivo; in editor/desktop (dove
    /// <see cref="TouchScreenKeyboard.isSupported"/> è false) resta il tastierino a schermo.
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
        private TouchScreenKeyboard nativeKeyboard;

        public PvpLobbyScreen(
            Transform parent,
            string username,
            UnityAction onCreateRoom,
            UnityAction onJoinRoom,
            UnityAction onQueue,
            UnityAction onLoadout,
            UnityAction onProfile,
            UnityAction onClose)
        {
            root = PvpUiFactory.CreatePanel(parent, "Lobby", PvpUiFactory.Ink);
            PvpUiFactory.Stretch(root);

            RectTransform titleBand = PvpUiFactory.CreateTitleBand(
                root, "ARENA MULTIPLAYER", "Stanze private, matchmaking e progressione profilo");
            PvpUiFactory.SetAnchors(titleBand, new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.985f));

            Button close = PvpUiFactory.CreateButton(
                root, "Close", "X", new Color(0.5f, 0.12f, 0.12f, 0.98f), onClose, 26);
            PvpUiFactory.SetAnchors((RectTransform)close.transform, new Vector2(0.93f, 0.895f), new Vector2(0.975f, 0.965f));

            RectTransform accountPanel = PvpUiFactory.CreateSoftPanel(root, "Account Panel", new Color(0.03f, 0.05f, 0.075f, 0.92f));
            PvpUiFactory.SetAnchors(accountPanel, new Vector2(0.05f, 0.77f), new Vector2(0.95f, 0.865f));
            Text player = PvpUiFactory.CreateText(
                accountPanel, "Player", username, 24, TextAnchor.MiddleLeft);
            player.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)player.transform, new Vector2(0.03f, 0.34f), new Vector2(0.42f, 0.94f));
            Text playerCaption = PvpUiFactory.CreateLabel(
                accountPanel, "Player Caption", "ACCOUNT CONNESSO", 14, TextAnchor.MiddleLeft);
            PvpUiFactory.SetAnchors((RectTransform)playerCaption.transform, new Vector2(0.03f, 0.08f), new Vector2(0.42f, 0.38f));

            Button loadout = PvpUiFactory.CreateButton(
                accountPanel, "Loadout", "LOADOUT", new Color(0.08f, 0.32f, 0.48f, 0.98f), onLoadout, 20);
            PvpUiFactory.SetAnchors((RectTransform)loadout.transform, new Vector2(0.53f, 0.18f), new Vector2(0.73f, 0.82f));

            Button profile = PvpUiFactory.CreateButton(
                accountPanel, "Profile", "PROFILO", new Color(0.26f, 0.16f, 0.48f, 0.98f), onProfile, 20);
            PvpUiFactory.SetAnchors((RectTransform)profile.transform, new Vector2(0.76f, 0.18f), new Vector2(0.97f, 0.82f));

            Button create = PvpUiFactory.CreateButton(
                root, "CreateRoom", "CREA STANZA", new Color(0.05f, 0.45f, 0.5f, 0.98f), onCreateRoom, 28);
            PvpUiFactory.SetAnchors((RectTransform)create.transform, new Vector2(0.06f, 0.64f), new Vector2(0.47f, 0.715f));

            Button queue = PvpUiFactory.CreateButton(
                root, "Queue", "CERCA AVVERSARIO", new Color(0.32f, 0.13f, 0.54f, 0.98f), onQueue, 28);
            PvpUiFactory.SetAnchors((RectTransform)queue.transform, new Vector2(0.53f, 0.64f), new Vector2(0.94f, 0.715f));

            RectTransform codePanel = PvpUiFactory.CreateSoftPanel(root, "Room Code Panel", new Color(0.04f, 0.065f, 0.09f, 0.94f));
            PvpUiFactory.SetAnchors(codePanel, new Vector2(0.06f, 0.475f), new Vector2(0.94f, 0.59f));
            roomCodeText = PvpUiFactory.CreateText(codePanel, "RoomCode", "CODICE STANZA: -", 34);
            roomCodeText.color = PvpUiFactory.Gold;
            PvpUiFactory.Stretch((RectTransform)roomCodeText.transform, 10f, 2f);

            // --- Join con codice: display + tastiera nativa/fisica ---
            Text joinCaption = PvpUiFactory.CreateLabel(
                root, "JoinCaption", "ENTRA CON CODICE", 18, TextAnchor.MiddleLeft);
            joinCaption.color = PvpUiFactory.Arcane;
            PvpUiFactory.SetAnchors((RectTransform)joinCaption.transform, new Vector2(0.06f, 0.385f), new Vector2(0.34f, 0.455f));

            // Il campo del codice è un pulsante: su mobile apre la tastiera nativa.
            Button codeField = PvpUiFactory.CreateButton(
                root, "TypedCode", "______", new Color(0.1f, 0.16f, 0.22f, 0.98f), OpenNativeKeyboard, 34);
            PvpUiFactory.SetAnchors((RectTransform)codeField.transform, new Vector2(0.31f, 0.392f), new Vector2(0.69f, 0.452f));
            typedCodeText = codeField.GetComponentInChildren<Text>();
            typedCodeText.color = new Color(0.6f, 0.95f, 1f);

            Button join = PvpUiFactory.CreateButton(
                root, "JoinRoom", "ENTRA", new Color(0.5f, 0.32f, 0.05f, 0.98f), onJoinRoom, 22);
            PvpUiFactory.SetAnchors((RectTransform)join.transform, new Vector2(0.71f, 0.387f), new Vector2(0.86f, 0.457f));

            Text hint = PvpUiFactory.CreateText(
                root, "KeyboardHint",
                TouchScreenKeyboard.isSupported ? "Tocca il codice per aprire la tastiera" : "Digita il codice con la tastiera",
                18, TextAnchor.MiddleCenter, FontStyle.Normal);
            hint.color = new Color(0.7f, 0.8f, 0.9f);
            PvpUiFactory.SetAnchors((RectTransform)hint.transform, new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.39f));

            statusText = PvpUiFactory.CreateText(
                root, "Status", "Connessione...", 20, TextAnchor.MiddleCenter, FontStyle.Normal);
            statusText.color = PvpUiFactory.TextMuted;
            PvpUiFactory.SetAnchors((RectTransform)statusText.transform, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.1f));
        }

        public string TypedRoomCode => typedCode;

        /// <summary>Da chiamare ogni frame: rispecchia il testo della tastiera nativa nel codice.</summary>
        public void Tick()
        {
            if (nativeKeyboard != null)
            {
                typedCode = Sanitize(nativeKeyboard.text);
                RefreshTypedCode();
                if (nativeKeyboard.status != TouchScreenKeyboard.Status.Visible)
                    nativeKeyboard = null;
                return;
            }

            if (!TouchScreenKeyboard.isSupported)
                ApplyPhysicalKeyboardInput();
        }

        public void SetStatus(string message) => statusText.text = message;

        public void ShowRoomCode(string code) => roomCodeText.text = $"CODICE STANZA: {code}";

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void Destroy() => Object.Destroy(root.gameObject);

        private void OpenNativeKeyboard()
        {
            if (!TouchScreenKeyboard.isSupported)
                return;
            nativeKeyboard = TouchScreenKeyboard.Open(
                typedCode,
                TouchScreenKeyboardType.ASCIICapable,
                autocorrection: false,
                multiline: false,
                secure: false,
                alert: false,
                textPlaceholder: "CODICE");
            nativeKeyboard.characterLimit = CodeLength;
        }

        /// <summary>Tiene solo i caratteri validi (maiuscoli, alfabeto codice), max 6.</summary>
        private static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;
            var builder = new System.Text.StringBuilder(CodeLength);
            foreach (char character in raw.ToUpperInvariant())
            {
                if (builder.Length >= CodeLength)
                    break;
                if (CodeAlphabet.IndexOf(character) >= 0)
                    builder.Append(character);
            }
            return builder.ToString();
        }

        private void ApplyPhysicalKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
                EraseChar();

            TryAppendKey(keyboard.aKey, 'A');
            TryAppendKey(keyboard.bKey, 'B');
            TryAppendKey(keyboard.cKey, 'C');
            TryAppendKey(keyboard.dKey, 'D');
            TryAppendKey(keyboard.eKey, 'E');
            TryAppendKey(keyboard.fKey, 'F');
            TryAppendKey(keyboard.gKey, 'G');
            TryAppendKey(keyboard.hKey, 'H');
            TryAppendKey(keyboard.jKey, 'J');
            TryAppendKey(keyboard.kKey, 'K');
            TryAppendKey(keyboard.mKey, 'M');
            TryAppendKey(keyboard.nKey, 'N');
            TryAppendKey(keyboard.pKey, 'P');
            TryAppendKey(keyboard.qKey, 'Q');
            TryAppendKey(keyboard.rKey, 'R');
            TryAppendKey(keyboard.sKey, 'S');
            TryAppendKey(keyboard.tKey, 'T');
            TryAppendKey(keyboard.uKey, 'U');
            TryAppendKey(keyboard.vKey, 'V');
            TryAppendKey(keyboard.wKey, 'W');
            TryAppendKey(keyboard.xKey, 'X');
            TryAppendKey(keyboard.yKey, 'Y');
            TryAppendKey(keyboard.zKey, 'Z');
            TryAppendKey(keyboard.digit2Key, '2');
            TryAppendKey(keyboard.digit3Key, '3');
            TryAppendKey(keyboard.digit4Key, '4');
            TryAppendKey(keyboard.digit5Key, '5');
            TryAppendKey(keyboard.digit6Key, '6');
            TryAppendKey(keyboard.digit7Key, '7');
            TryAppendKey(keyboard.digit8Key, '8');
            TryAppendKey(keyboard.digit9Key, '9');
            TryAppendKey(keyboard.numpad2Key, '2');
            TryAppendKey(keyboard.numpad3Key, '3');
            TryAppendKey(keyboard.numpad4Key, '4');
            TryAppendKey(keyboard.numpad5Key, '5');
            TryAppendKey(keyboard.numpad6Key, '6');
            TryAppendKey(keyboard.numpad7Key, '7');
            TryAppendKey(keyboard.numpad8Key, '8');
            TryAppendKey(keyboard.numpad9Key, '9');
        }

        private void TryAppendKey(KeyControl key, char character)
        {
            if (key == null || !key.wasPressedThisFrame || typedCode.Length >= CodeLength)
                return;
            if (CodeAlphabet.IndexOf(character) < 0)
                return;
            typedCode += character;
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
