using System.Collections.Generic;
using AccardND.Battlefield;
using AccardND.GameData;
using AccardND.NetProtocol;
using UnityEngine.Events;

namespace AccardND.PvpUi
{
    public interface IPvpMatchView
    {
        void ShowPvpMatch(
            PvpClientMatchState state,
            IReadOnlyList<LoadoutCardDto> myLoadout,
            IBattlePresentationActions actions);

        void UpdatePvpMatch(
            PvpClientMatchState state,
            IReadOnlyList<LoadoutCardDto> myLoadout,
            IReadOnlyList<BattlePresentationEvent> events);

        void HidePvpMatch();

        void PlayPvpVictorySfx();

        void ShowPvpLoadoutCardInspection(
            CardDefinition definition,
            UnityAction onAdd,
            bool canAdd,
            string buttonText);
    }
}
