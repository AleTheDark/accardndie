#if UNITY_EDITOR
using AccardND.GameData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
    /// <summary>Entry point della scena debug; non viene incluso nelle build.</summary>
    public bool DebugStartFirstLevelTalentTour()
    {
        if ((Object)(object)modeSelectionProfileButton == (Object)null
            || (Object)(object)modeSelectionPanel == (Object)null)
            return false;

        PlayerPrefs.DeleteKey(TutorialTourSeenPrefsKey(TutorialSurface.HubProfile));
        PlayerPrefs.Save();

        if ((Object)(object)levelUpRewardPopup != (Object)null)
            levelUpRewardPopup.SetActive(false);
        if ((Object)(object)profilePanel != (Object)null)
            profilePanel.SetActive(false);

        ShowHubFromSinglePlayer();
        TryStartFirstLevelTalentTour();
        Debug.Log("[LEVEL-UP TOUR DEBUG] Tour avviato. Segui gli highlight: PROFILO > TALENTI > FAVO.");
        return true;
    }
}
}
#endif
