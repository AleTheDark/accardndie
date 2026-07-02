using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>Lobby minimale: connessione, creazione stanza, join con codice, coda.</summary>
    internal sealed class PvpLobbyScreen
    {
        private readonly RectTransform root;
        private readonly Text statusText;
        private readonly Text roomCodeText;

        public PvpLobbyScreen(
            Transform parent,
            string username,
            string roomCodeToJoin,
            UnityAction onCreateRoom,
            UnityAction onJoinRoom,
            UnityAction onQueue)
        {
            root = PvpUiFactory.CreatePanel(parent, "Lobby", new Color(0.07f, 0.1f, 0.14f, 0.98f));
            PvpUiFactory.Stretch(root);

            Text title = PvpUiFactory.CreateText(root, "Title", "ACCARD N' DIE  -  PVP", 44);
            PvpUiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.97f));

            Text player = PvpUiFactory.CreateText(
                root, "Player", $"Giocatore: {username}   (modificabile dall'Inspector di PvpBootstrap)",
                20, TextAnchor.MiddleCenter, FontStyle.Normal);
            player.color = new Color(0.75f, 0.85f, 0.95f);
            PvpUiFactory.SetAnchors((RectTransform)player.transform, new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.85f));

            Button create = PvpUiFactory.CreateButton(
                root, "CreateRoom", "CREA STANZA", new Color(0.05f, 0.45f, 0.5f, 0.98f), onCreateRoom, 26);
            PvpUiFactory.SetAnchors((RectTransform)create.transform, new Vector2(0.3f, 0.6f), new Vector2(0.7f, 0.7f));

            string joinLabel = string.IsNullOrWhiteSpace(roomCodeToJoin)
                ? "ENTRA CON CODICE (impostalo nell'Inspector)"
                : $"ENTRA NELLA STANZA {roomCodeToJoin.ToUpperInvariant()}";
            Button join = PvpUiFactory.CreateButton(
                root, "JoinRoom", joinLabel, new Color(0.5f, 0.32f, 0.05f, 0.98f), onJoinRoom, 22);
            PvpUiFactory.SetAnchors((RectTransform)join.transform, new Vector2(0.3f, 0.47f), new Vector2(0.7f, 0.57f));

            Button queue = PvpUiFactory.CreateButton(
                root, "Queue", "CERCA AVVERSARIO (CODA)", new Color(0.28f, 0.12f, 0.45f, 0.98f), onQueue, 24);
            PvpUiFactory.SetAnchors((RectTransform)queue.transform, new Vector2(0.3f, 0.34f), new Vector2(0.7f, 0.44f));

            roomCodeText = PvpUiFactory.CreateText(root, "RoomCode", string.Empty, 52);
            roomCodeText.color = new Color(1f, 0.85f, 0.3f);
            PvpUiFactory.SetAnchors((RectTransform)roomCodeText.transform, new Vector2(0.1f, 0.18f), new Vector2(0.9f, 0.32f));

            statusText = PvpUiFactory.CreateText(root, "Status", "Connessione...", 22, TextAnchor.MiddleCenter, FontStyle.Normal);
            PvpUiFactory.SetAnchors((RectTransform)statusText.transform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.16f));
        }

        public void SetStatus(string message) => statusText.text = message;

        public void ShowRoomCode(string code) =>
            roomCodeText.text = $"CODICE STANZA: {code}";

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        public void Destroy() => Object.Destroy(root.gameObject);
    }
}
