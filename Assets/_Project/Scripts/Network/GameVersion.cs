using UnityEngine;

namespace AccardND.Network
{
    /// <summary>
    /// Versione della build, dichiarata al server a ogni accesso. È il
    /// bundleVersion dei Project Settings: l'unico posto dove si cambia.
    /// Il server la confronta con la propria versione target e respinge i client
    /// vecchi, quindi dopo ogni deploy le due devono coincidere.
    /// </summary>
    public static class GameVersion
    {
        public static string Current => Application.version;
    }
}
