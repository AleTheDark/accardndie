using System.Collections.Generic;
using AccardND.Battlefield;
using AccardND.NetProtocol;

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
    }
}
