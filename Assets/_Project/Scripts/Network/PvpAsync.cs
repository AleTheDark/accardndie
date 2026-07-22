using System.Threading.Tasks;

namespace AccardND.Network
{
    public static class PvpAsync
    {
        public static async Task NextFrameAsync()
        {
            await Task.Yield();
        }
    }
}
