using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using AccardND.NetProtocol;

namespace AccardND.PvpUi
{
    /// <summary>
    /// Lobby PvP: room private, matchmaking e accesso con codice. Il layout e'
    /// pensato come una schermata console compatta, leggibile anche in portrait.
    /// </summary>
    internal sealed class PvpLobbyScreen
    {
        private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        private const int CodeLength = 6;

        private readonly RectTransform root;
        private readonly Text playerText;
        private readonly Text rankText;
        private readonly Text statusText;
        private readonly Text roomCodeText;
        private readonly Text typedCodeText;
        private readonly RectTransform waitingPanel;
        private readonly Image[] waitingDiceImages = new Image[3];
        private readonly int[] waitingDiceSides = { 4, 6, 8, 10, 12, 20 };
        private float waitingDiceTimer;
        private readonly Button leaveRoomButton;
        private string typedCode = string.Empty;
        private bool waitingForOpponent;
        private TouchScreenKeyboard nativeKeyboard;

        public PvpLobbyScreen(
            Transform parent,
            string username,
            UnityAction onCreateRoom,
            UnityAction onJoinRoom,
            UnityAction onQueue,
            UnityAction onCancelQueue,
            UnityAction onLeaveRoom,
            UnityAction onLoadout,
            UnityAction onProfile,
            UnityAction onClose)
        {
            root = PvpUiFactory.CreatePanel(parent, "Lobby", PvpUiFactory.Ink);
            PvpUiFactory.Stretch(root);

            RectTransform content = PvpUiFactory.CreateSoftPanel(root, "Lobby Content", new Color(0.008f, 0.012f, 0.018f, 0.74f));
            PvpUiFactory.SetAnchors(content, new Vector2(0.055f, 0.07f), new Vector2(0.945f, 0.955f));

            RectTransform titleBand = PvpUiFactory.CreateTitleBand(
                content, "ARENA MULTIPLAYER");
            PvpUiFactory.SetAnchors(titleBand, new Vector2(0.035f, 0.875f), new Vector2(0.965f, 0.975f));

            Button close = PvpUiFactory.CreateButton(
                content, "Close", "X", new Color(0.5f, 0.12f, 0.12f, 0.98f), onClose, 22);
            PvpUiFactory.SetAnchors((RectTransform)close.transform, new Vector2(0.895f, 0.905f), new Vector2(0.965f, 0.958f));

            RectTransform accountPanel = PvpUiFactory.CreateSoftPanel(content, "Account Panel", new Color(0.025f, 0.04f, 0.055f, 0.95f));
            PvpUiFactory.SetAnchors(accountPanel, new Vector2(0.055f, 0.735f), new Vector2(0.945f, 0.845f));
            playerText = PvpUiFactory.CreateText(accountPanel, "Player", username, 36, TextAnchor.MiddleLeft);
            playerText.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)playerText.transform, new Vector2(0.04f, 0.34f), new Vector2(0.56f, 0.9f));

            rankText = PvpUiFactory.CreateLabel(accountPanel, "Rank", "Rank in aggiornamento...", 20, TextAnchor.MiddleLeft);
            rankText.color = new Color(0.62f, 0.82f, 0.92f);
            PvpUiFactory.SetAnchors((RectTransform)rankText.transform, new Vector2(0.04f, 0.08f), new Vector2(0.56f, 0.34f));

            Button loadout = PvpUiFactory.CreateButton(
                accountPanel, "Loadout", "LOADOUT", new Color(0.08f, 0.32f, 0.48f, 0.98f), onLoadout, 25);
            PvpUiFactory.SetAnchors((RectTransform)loadout.transform, new Vector2(0.61f, 0.2f), new Vector2(0.79f, 0.8f));

            Button profile = PvpUiFactory.CreateButton(
                accountPanel, "Profile", "PROFILO", new Color(0.26f, 0.16f, 0.48f, 0.98f), onProfile, 25);
            PvpUiFactory.SetAnchors((RectTransform)profile.transform, new Vector2(0.81f, 0.2f), new Vector2(0.96f, 0.8f));

            RectTransform actionPanel = PvpUiFactory.CreateSoftPanel(content, "Action Panel", new Color(0.015f, 0.022f, 0.032f, 0.9f));
            PvpUiFactory.SetAnchors(actionPanel, new Vector2(0.055f, 0.5f), new Vector2(0.945f, 0.69f));

            Button create = PvpUiFactory.CreateButton(
                actionPanel, "CreateRoom", "CREA STANZA", new Color(0.05f, 0.45f, 0.5f, 0.98f), onCreateRoom, 36);
            PvpUiFactory.SetAnchors((RectTransform)create.transform, new Vector2(0.035f, 0.2f), new Vector2(0.49f, 0.84f));

            Button queue = PvpUiFactory.CreateButton(
                actionPanel, "Queue", "CERCA AVVERSARIO", new Color(0.32f, 0.13f, 0.54f, 0.98f), onQueue, 34);
            PvpUiFactory.SetAnchors((RectTransform)queue.transform, new Vector2(0.51f, 0.2f), new Vector2(0.965f, 0.84f));

            waitingPanel = PvpUiFactory.CreateSoftPanel(content, "Waiting Opponent Panel", new Color(0.04f, 0.025f, 0.075f, 0.96f));
            PvpUiFactory.SetAnchors(waitingPanel, new Vector2(0.055f, 0.5f), new Vector2(0.945f, 0.69f));
            waitingPanel.gameObject.SetActive(false);

            CreateWaitingDiceRoll(waitingPanel);

            Text waitingTitle = PvpUiFactory.CreateTitleText(waitingPanel, "Waiting Title", "CERCO AVVERSARIO", 34, TextAnchor.MiddleLeft);
            waitingTitle.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)waitingTitle.transform, new Vector2(0.25f, 0.42f), new Vector2(0.95f, 0.88f));

            Text waitingHint = PvpUiFactory.CreateText(waitingPanel, "Waiting Hint", "Preparati: il duello parte appena troviamo un match.", 20, TextAnchor.MiddleLeft, FontStyle.Normal);
            waitingHint.color = new Color(0.72f, 0.86f, 0.95f);
            PvpUiFactory.SetAnchors((RectTransform)waitingHint.transform, new Vector2(0.25f, 0.22f), new Vector2(0.95f, 0.45f));

            Button cancelQueue = PvpUiFactory.CreateButton(
                waitingPanel, "Cancel Queue", "ANNULLA", new Color(0.5f, 0.15f, 0.15f, 0.98f), onCancelQueue, 16);
            PvpUiFactory.SetAnchors((RectTransform)cancelQueue.transform, new Vector2(0.78f, 0.04f), new Vector2(0.96f, 0.2f));

            RectTransform codePanel = PvpUiFactory.CreateSoftPanel(content, "Room Code Panel", new Color(0.035f, 0.04f, 0.048f, 0.94f));
            PvpUiFactory.SetAnchors(codePanel, new Vector2(0.055f, 0.365f), new Vector2(0.945f, 0.455f));

            Text roomLabel = PvpUiFactory.CreateLabel(codePanel, "Room Label", "STANZA ATTIVA", 18, TextAnchor.MiddleLeft);
            roomLabel.color = new Color(0.62f, 0.82f, 0.92f);
            PvpUiFactory.SetAnchors((RectTransform)roomLabel.transform, new Vector2(0.035f, 0.52f), new Vector2(0.32f, 0.9f));

            roomCodeText = PvpUiFactory.CreateText(codePanel, "RoomCode", "CODICE: -", 36, TextAnchor.MiddleLeft);
            roomCodeText.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)roomCodeText.transform, new Vector2(0.035f, 0.08f), new Vector2(0.68f, 0.62f));

            leaveRoomButton = PvpUiFactory.CreateButton(
                codePanel, "LeaveRoom", "CHIUDI", new Color(0.5f, 0.15f, 0.15f, 0.98f), onLeaveRoom, 22);
            PvpUiFactory.SetAnchors((RectTransform)leaveRoomButton.transform, new Vector2(0.73f, 0.22f), new Vector2(0.965f, 0.78f));
            leaveRoomButton.gameObject.SetActive(false);

            RectTransform joinPanel = PvpUiFactory.CreateSoftPanel(content, "Join Code Panel", new Color(0.015f, 0.024f, 0.034f, 0.92f));
            PvpUiFactory.SetAnchors(joinPanel, new Vector2(0.055f, 0.18f), new Vector2(0.945f, 0.34f));

            Text joinCaption = PvpUiFactory.CreateLabel(joinPanel, "JoinCaption", "ENTRA CON CODICE", 22, TextAnchor.MiddleLeft);
            joinCaption.color = PvpUiFactory.Arcane;
            PvpUiFactory.SetAnchors((RectTransform)joinCaption.transform, new Vector2(0.035f, 0.68f), new Vector2(0.62f, 0.95f));

            Button codeField = PvpUiFactory.CreateButton(
                joinPanel, "TypedCode", "______", new Color(0.1f, 0.16f, 0.22f, 0.98f), OpenNativeKeyboard, 42);
            PvpUiFactory.SetAnchors((RectTransform)codeField.transform, new Vector2(0.035f, 0.16f), new Vector2(0.72f, 0.62f));
            typedCodeText = codeField.GetComponentInChildren<Text>();
            typedCodeText.color = new Color(0.6f, 0.95f, 1f);

            Button join = PvpUiFactory.CreateButton(
                joinPanel, "JoinRoom", "ENTRA", new Color(0.5f, 0.32f, 0.05f, 0.98f), onJoinRoom, 30);
            PvpUiFactory.SetAnchors((RectTransform)join.transform, new Vector2(0.745f, 0.16f), new Vector2(0.965f, 0.62f));

            Text hint = PvpUiFactory.CreateText(
                joinPanel, "KeyboardHint",
                TouchScreenKeyboard.isSupported ? "Tocca il codice per aprire la tastiera" : "Digita il codice con la tastiera",
                18, TextAnchor.MiddleLeft, FontStyle.Normal);
            hint.color = new Color(0.7f, 0.8f, 0.9f);
            PvpUiFactory.SetAnchors((RectTransform)hint.transform, new Vector2(0.035f, 0.01f), new Vector2(0.965f, 0.15f));

            statusText = PvpUiFactory.CreateText(content, "Status", "Connessione...", 24, TextAnchor.MiddleCenter, FontStyle.Normal);
            statusText.color = PvpUiFactory.TextMuted;
            RectTransform statusPanel = PvpUiFactory.CreateSoftPanel(content, "Status Panel", new Color(0.025f, 0.04f, 0.06f, 0.88f));
            PvpUiFactory.SetAnchors(statusPanel, new Vector2(0.055f, 0.045f), new Vector2(0.945f, 0.115f));
            statusText.transform.SetParent(statusPanel, false);
            PvpUiFactory.Stretch((RectTransform)statusText.transform, 14f, 4f);
        }

        public string TypedRoomCode => typedCode;

        public void Tick()
        {
            if (waitingForOpponent)
                TickWaitingDice();

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

        public void SetPlayerName(string username) => playerText.text = username;

        public void SetProfile(ProfileData profile)
        {
            if (profile == null)
                return;

            if (!string.IsNullOrWhiteSpace(profile.username))
                SetPlayerName(profile.username);

            rankText.text = FormatRank(profile);
            rankText.color = profile.ranked && !profile.placement
                ? PvpUiFactory.Gold
                : new Color(0.62f, 0.82f, 0.92f);
        }

        public void SetWaitingForOpponent(bool waiting)
        {
            waitingForOpponent = waiting;
            if (waitingPanel != null)
                waitingPanel.gameObject.SetActive(waiting);
            if (waiting)
                RollWaitingDice();
        }

        private void CreateWaitingDiceRoll(Transform parent)
        {
            for (int index = 0; index < waitingDiceImages.Length; index++)
            {
                RectTransform slot = PvpUiFactory.CreateSoftPanel(parent, $"Waiting Dice Slot {index + 1}", new Color(0.02f, 0.06f, 0.075f, 0.84f));
                float left = 0.055f + index * 0.065f;
                PvpUiFactory.SetAnchors(slot, new Vector2(left, 0.22f), new Vector2(left + 0.055f, 0.76f));

                var diceObject = new GameObject("Dice", typeof(RectTransform), typeof(Image));
                diceObject.transform.SetParent(slot, false);
                Image image = diceObject.GetComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                PvpUiFactory.SetAnchors((RectTransform)diceObject.transform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
                waitingDiceImages[index] = image;
            }

            RollWaitingDice();
        }

        private void TickWaitingDice()
        {
            waitingDiceTimer -= Time.unscaledDeltaTime;
            if (waitingDiceTimer <= 0f)
                RollWaitingDice();

            for (int index = 0; index < waitingDiceImages.Length; index++)
            {
                Image image = waitingDiceImages[index];
                if (image == null)
                    continue;

                float direction = index % 2 == 0 ? 1f : -1f;
                image.rectTransform.Rotate(0f, 0f, direction * (38f + index * 9f) * Time.unscaledDeltaTime);
            }
        }

        private void RollWaitingDice()
        {
            waitingDiceTimer = 3f;
            Color[] colors =
            {
                new Color(0.72f, 0.95f, 1f, 1f),
                new Color(1f, 0.82f, 0.25f, 1f),
                new Color(0.76f, 0.55f, 1f, 1f),
                new Color(0.42f, 1f, 0.62f, 1f),
                new Color(1f, 0.42f, 0.36f, 1f),
                new Color(1f, 1f, 1f, 1f)
            };

            for (int index = 0; index < waitingDiceImages.Length; index++)
            {
                int sides = waitingDiceSides[Random.Range(0, waitingDiceSides.Length)];
                Image image = waitingDiceImages[index];
                if (image != null)
                {
                    image.sprite = RandomWaitingDiceSprite(sides);
                    image.color = colors[Random.Range(0, colors.Length)];
                    image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-24f, 24f));
                    image.enabled = image.sprite != null;
                }
            }
        }

        private static Sprite RandomWaitingDiceSprite(int sides)
        {
            Sprite player = Resources.Load<Sprite>($"UI/D{sides}_Player");
            Sprite cpu = Resources.Load<Sprite>($"UI/D{sides}_Cpu");
            if (player == null)
                return cpu;
            if (cpu == null)
                return player;
            return Random.value < 0.5f ? player : cpu;
        }

        public void ShowRoomCode(string code)
        {
            roomCodeText.text = $"CODICE: {code}";
            leaveRoomButton.gameObject.SetActive(true);
        }

        public void ClearRoomCode()
        {
            roomCodeText.text = "CODICE: -";
            leaveRoomButton.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void Destroy() => Object.Destroy(root.gameObject);

        private static string FormatRank(ProfileData profile)
        {
            if (!profile.ranked)
                return "Rank: non classificato";
            if (profile.placement)
                return $"Rank: piazzamento - {profile.placementRemaining} partite rimaste";
            return $"Rank: {profile.tier} {profile.division} - {profile.leaguePoints} LP";
        }

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
