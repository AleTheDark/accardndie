#if UNITY_EDITOR
using AccardND.PvpUi;
using UnityEditor;
using UnityEngine;

namespace AccardND.EditorTools
{
    public sealed class GoogleOAuthEditorSettingsWindow : EditorWindow
    {
        private string clientId;
        private string clientSecret;

        [MenuItem("Tools/AccardND/Google OAuth Editor")]
        private static void Open()
        {
            GetWindow<GoogleOAuthEditorSettingsWindow>("Google OAuth Editor");
        }

        private void OnEnable()
        {
            clientId = EditorPrefs.GetString(
                PvpUgsAuth.EditorGoogleClientIdPrefsKey,
                PvpUgsAuth.DefaultEditorGoogleClientId);
            clientSecret = EditorPrefs.GetString(PvpUgsAuth.EditorGoogleClientSecretPrefsKey, string.Empty);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("OAuth Google per Play Mode", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Usa un client OAuth di tipo Applicazione web con questa URI di reindirizzamento autorizzata:\n"
                + PvpUgsAuth.EditorGoogleRedirectUri
                + "\n\nIl client ID deve coincidere con quello configurato nel provider Google di Unity Authentication.",
                MessageType.Info);

            clientId = EditorGUILayout.TextField("Client ID", clientId);
            clientSecret = EditorGUILayout.PasswordField("Client secret", clientSecret);

            EditorGUILayout.Space();
            if (GUILayout.Button("Salva nelle preferenze locali"))
            {
                EditorPrefs.SetString(
                    PvpUgsAuth.EditorGoogleClientIdPrefsKey,
                    clientId != null ? clientId.Trim() : string.Empty);
                EditorPrefs.SetString(
                    PvpUgsAuth.EditorGoogleClientSecretPrefsKey,
                    clientSecret != null ? clientSecret.Trim() : string.Empty);
                ShowNotification(new GUIContent("Configurazione salvata"));
            }

            EditorGUILayout.HelpBox(
                "Il client secret resta nelle EditorPrefs di questa macchina e non viene incluso nelle build o nel repository.",
                MessageType.None);
        }
    }
}
#endif
