namespace AccardND.Presentation
{
    public enum BossDebugScenario
    {
        Bragus,
        Medusa,
        Palatir,
        Seraphel,
        Trentor,
        Jurinashor
    }

    public static class BossDebugSelection
    {
        private const string EditorPreferenceKey = "AccardND.BossDebugScenario";

        public static BossDebugScenario Current
        {
            get
            {
#if UNITY_EDITOR
                return (BossDebugScenario)UnityEditor.EditorPrefs.GetInt(
                    EditorPreferenceKey,
                    (int)BossDebugScenario.Bragus);
#else
                return BossDebugScenario.Bragus;
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetInt(EditorPreferenceKey, (int)value);
#endif
            }
        }
    }
}
