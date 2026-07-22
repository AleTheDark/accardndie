using UnityEngine;

namespace AccardND.Network
{
    /// <summary>
    /// Credenziali ospite deterministiche legate al dispositivo, allineate allo schema usato
    /// dal fallback di PvpBootstrap: cosi single player e PvP condividono la stessa identita
    /// ospite quando non si usa Unity Authentication.
    /// </summary>
    public static class GuestCredentials
    {
        private const string NicknamePrefsKey = "AccardND.PvpNickname";
        private const int NicknameMaxLength = 18;

        public static (string Username, string Password) Derive()
        {
            string device = SystemInfo.deviceUniqueIdentifier;
            string editorSuffix = Application.isEditor ? "-editor" : string.Empty;
            string savedNickname = Sanitize(PlayerPrefs.GetString(NicknamePrefsKey, string.Empty));

            string username = !string.IsNullOrWhiteSpace(savedNickname)
                ? savedNickname + editorSuffix
                : $"ospite-{(uint)device.GetHashCode() % 100000:D5}{editorSuffix}";

            string password = $"dev-{device.Substring(0, Mathf.Min(12, device.Length))}";
            return (username, password);
        }

        private static string Sanitize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;
            var builder = new System.Text.StringBuilder(NicknameMaxLength);
            foreach (char character in raw.Trim())
            {
                if (builder.Length >= NicknameMaxLength)
                    break;
                if (char.IsLetterOrDigit(character) || character == '_' || character == '-')
                    builder.Append(character);
            }
            return builder.ToString();
        }
    }
}
