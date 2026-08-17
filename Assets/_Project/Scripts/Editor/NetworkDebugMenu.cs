using AccardND.Network;
using UnityEditor;
using UnityEngine;

namespace AccardND.EditorTools
{
    /// <summary>
    /// Comandi per provare in editor riconnessione, rete assente e sessione scaduta.
    ///
    /// Servono perché in editor il server sta sulla stessa macchina: staccare il Wi-Fi
    /// non interrompe niente, e spegnere il server prova il riavvio, che è un'altra
    /// cosa. Da qui invece si stronca il socket lasciando in piedi tutto il resto, che
    /// è esattamente quello che fa una rete che sparisce.
    ///
    /// Vanno usati in play mode, con il login già fatto.
    /// </summary>
    internal static class NetworkDebugMenu
    {
        private const string Root = "Accard N' Die/Debug/Rete/";
        private const string DropPath = Root + "Simula caduta di rete";
        private const string OutagePath = Root + "Rete assente (resta giù)";
        private const string ExpirePath = Root + "Simula sessione scaduta";
        private const string StatePath = Root + "Stato della sessione nel log";

        [MenuItem(DropPath, false, 0)]
        private static void DropConnection() => AccountServerSession.DebugDropConnection();

        [MenuItem(DropPath, true, 0)]
        private static bool ValidateDrop() => Application.isPlaying;

        [MenuItem(OutagePath, false, 1)]
        private static void ToggleOutage()
        {
            bool down = !AccountServerSession.DebugNetworkOutage;
            AccountServerSession.DebugNetworkOutage = down;
            Menu.SetChecked(OutagePath, down);
        }

        [MenuItem(OutagePath, true, 1)]
        private static bool ValidateOutage()
        {
            // La spunta va riallineata qui: è l'unico punto che Unity richiama prima di
            // disegnare il menu, e lo stato vero vive nel client, non nel menu.
            Menu.SetChecked(OutagePath, Application.isPlaying && AccountServerSession.DebugNetworkOutage);
            return Application.isPlaying;
        }

        [MenuItem(ExpirePath, false, 2)]
        private static void ExpireSession() => AccountServerSession.DebugExpireSession();

        [MenuItem(ExpirePath, true, 2)]
        private static bool ValidateExpire() => Application.isPlaying;

        [MenuItem(StatePath, false, 20)]
        private static void LogState() => AccountServerSession.DebugLogState();

        [MenuItem(StatePath, true, 20)]
        private static bool ValidateLogState() => Application.isPlaying;
    }
}
