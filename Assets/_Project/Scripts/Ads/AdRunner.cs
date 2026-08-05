using System.Collections;
using UnityEngine;

namespace AccardND.Ads
{
    /// <summary>
    /// Un MonoBehaviour nascosto che sopravvive ai cambi di scena, per le corotine e gli
    /// oggetti di scena della pubblicita' finta. Serve anche ai provider veri: un SDK che
    /// deve mostrare un pannello ha bisogno di qualcuno che stia in scena, e il gameplay
    /// non deve prestargli il proprio.
    ///
    /// Non usa Task.Delay: su WebGL non c'e' un thread che lo faccia scattare, mentre una
    /// corotina gira sul ciclo del gioco su ogni piattaforma.
    /// </summary>
    internal sealed class AdRunner : MonoBehaviour
    {
        private static AdRunner instance;

        public static AdRunner Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                var host = new GameObject("AccardND Ads");
                Object.DontDestroyOnLoad(host);
                instance = host.AddComponent<AdRunner>();
                return instance;
            }
        }

        public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);
    }
}
