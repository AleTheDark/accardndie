using AccardND.Battlefield;
using UnityEngine;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
    private void ConfigureCampaignDeploymentHandSwipe(PrototypeCardView view, int index)
    {
        DeploymentHandSwipeSelector selector = view.gameObject.AddComponent<DeploymentHandSwipeSelector>();
        selector.Configure(
            this,
            view,
            () => CanSelectCampaignDeploymentCard(index),
            () => SelectCampaignDeploymentCardFromSwipe(index),
            ConfirmPendingDeployment,
            () => PreviewCampaignDeploymentCardActions(index),
            () => ClearCampaignDeploymentCardActions(index));
    }

    private bool CanSelectCampaignDeploymentCard(int index)
    {
        return draftActive
            && deploymentDraftActive
            && !inputLocked
            && index >= 0
            && index < draftViews.Count
            && !selectedDraftCards.Contains(index)
            && currentDeploymentIndex >= 0
            && currentDeploymentIndex < deploymentOrder.Count
            && deploymentOrder[currentDeploymentIndex].BelongsToPlayer
            && IsAdventureTutorialDraftCardAllowed(index);
    }

    private void SelectCampaignDeploymentCardFromSwipe(int index)
    {
        if (CanSelectCampaignDeploymentCard(index) && pendingDeploymentIndex != index)
            ToggleDraftCard(index);
    }

    private void PreviewCampaignDeploymentCardActions(int index)
    {
        if (!CanSelectCampaignDeploymentCard(index)
            || index < 0
            || index >= draftCandidates.Count)
        {
            return;
        }

        // Every new gesture has priority over the selection left open by the
        // previous click/release, even when it starts on that same card.
        if (pendingDeploymentIndex >= 0
            && pendingDeploymentIndex < draftViews.Count)
        {
            PrototypeCardView previous = draftViews[pendingDeploymentIndex];
            if (previous != null)
            {
                previous.SetDraftSelected(false);
                previous.ClearActionOverlay();
            }
            pendingDeploymentIndex = -1;
            ClearTargetHints();
        }

        PrototypeCardView cardView = draftViews[index];
        ShowDeploymentMatchupHints(draftCandidates[index]);
        if (adventureScriptedTutorialActive)
        {
            cardView.ShowConfirmAction(
                confirmActionSprite,
                () => ConfirmCampaignDeploymentCard(index));
            return;
        }

        cardView.ShowConfirmInfoActions(
            confirmActionSprite,
            infoActionSprite,
            () => ConfirmCampaignDeploymentCard(index),
            () => ShowCardInspection(draftCandidates[index]));
    }

    private void ClearCampaignDeploymentCardActions(int index)
    {
        if (index >= 0 && index < draftViews.Count && draftViews[index] != null)
            draftViews[index].ClearActionOverlay();

        // Al rilascio ToggleDraftCard riapplica subito la preview sulla carta
        // confermabile; durante un annullamento, invece, non deve restare sospesa.
        if (pendingDeploymentIndex < 0)
            ClearTargetHints();
    }

    private void ConfirmCampaignDeploymentCard(int index)
    {
        if (!CanSelectCampaignDeploymentCard(index))
            return;

        SelectCampaignDeploymentCardFromSwipe(index);
        if (pendingDeploymentIndex == index)
            ConfirmPendingDeployment();
    }

    private void ConfigurePvpDeploymentHandSwipe(
        PrototypeCardView view,
        int handIndex,
        string definitionId)
    {
        DeploymentHandSwipeSelector selector = view.gameObject.AddComponent<DeploymentHandSwipeSelector>();
        selector.Configure(
            this,
            view,
            () => CanSelectPvpDeploymentCard(handIndex),
            () => SelectPvpDeploymentCard(handIndex, definitionId, view),
            () => TryConfirmPvpPendingDeployment());
    }

    private bool CanSelectPvpDeploymentCard(int handIndex)
    {
        return pvpPresentationActive
            && pvpState != null
            && pvpState.IsMyDeployTurn
            && handIndex >= 0
            && handIndex < pvpHandViews.Count;
    }
}
}
