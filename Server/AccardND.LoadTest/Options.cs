namespace AccardND.LoadTest;

/// <summary>Profili di traffico simulabili.</summary>
public enum Profile
{
    /// <summary>Solo connessioni aperte e inattive: misura quanta RAM costa un giocatore fermo.</summary>
    Connect,

    /// <summary>Il traffico di chi gioca in singolo: login, pannelli, run di campagna, reward.</summary>
    SinglePlayer,

    /// <summary>Partite PvP vere, a coppie.</summary>
    Pvp,

    /// <summary>Misto: una quota di bot gioca PvP, il resto in singolo.</summary>
    Mixed,

    /// <summary>Solo pagine web (hall of fame, statistiche, health): niente WebSocket.</summary>
    Web
}

/// <summary>Come i bot si autenticano.</summary>
public enum LoginMode
{
    /// <summary>Registra un account nuovo per bot (default): e' il caso peggiore, crea righe.</summary>
    Register,

    /// <summary>Riusa gli account di una corsa precedente con lo stesso --prefix.</summary>
    Login
}

public sealed class Options
{
    public string Url { get; set; } = "ws://127.0.0.1:5017/ws";
    public string WebUrl { get; set; }
    public Profile Profile { get; set; } = Profile.Mixed;
    public LoginMode LoginMode { get; set; } = LoginMode.Register;

    public int Clients { get; set; } = 100;
    public int WebClients { get; set; }

    /// <summary>Secondi su cui distribuire l'ingresso dei bot. 0 = tutti insieme (thundering herd).</summary>
    public int RampSeconds { get; set; } = 60;
    public int DurationSeconds { get; set; } = 300;

    /// <summary>Pausa fra un'azione e la successiva, in secondi.</summary>
    public double ThinkMinSeconds { get; set; } = 3;
    public double ThinkMaxSeconds { get; set; } = 10;

    /// <summary>Quota di bot che gioca PvP nel profilo misto.</summary>
    public double PvpShare { get; set; } = 0.25;

    /// <summary>PvP dentro stanze private (nessun requisito) o in coda ranked.</summary>
    public bool PvpRanked { get; set; }

    public string ClientVersion { get; set; } = "0.9.2";
    public string Prefix { get; set; } = "lt";
    public string Password { get; set; } = "loadtest123";

    public int RequestTimeoutSeconds { get; set; } = 30;
    public int ReportSeconds { get; set; } = 5;
    public string JsonOut { get; set; }

    /// <summary>Necessario per puntare al server di produzione: i bot scrivono sul database.</summary>
    public bool AllowProduction { get; set; }

    public bool Verbose { get; set; }

    public string Nickname(int index) => $"{Prefix}{index:D5}";

    public static Options Parse(string[] args, out string error)
    {
        var options = new Options();
        error = null;
        for (int index = 0; index < args.Length; index++)
        {
            string key = args[index];
            string Next()
            {
                if (index + 1 >= args.Length)
                    throw new ArgumentException($"Manca il valore di {key}.");
                return args[++index];
            }

            try
            {
                switch (key)
                {
                    case "--url": options.Url = Next(); break;
                    case "--web-url": options.WebUrl = Next(); break;
                    case "--profile": options.Profile = ParseEnum<Profile>(Next()); break;
                    case "--login": options.LoginMode = ParseEnum<LoginMode>(Next()); break;
                    case "--clients": options.Clients = int.Parse(Next()); break;
                    case "--web-clients": options.WebClients = int.Parse(Next()); break;
                    case "--ramp": options.RampSeconds = int.Parse(Next()); break;
                    case "--duration": options.DurationSeconds = int.Parse(Next()); break;
                    case "--think-min": options.ThinkMinSeconds = double.Parse(Next()); break;
                    case "--think-max": options.ThinkMaxSeconds = double.Parse(Next()); break;
                    case "--pvp-share": options.PvpShare = double.Parse(Next()); break;
                    case "--pvp-ranked": options.PvpRanked = true; break;
                    case "--client-version": options.ClientVersion = Next(); break;
                    case "--prefix": options.Prefix = Next(); break;
                    case "--password": options.Password = Next(); break;
                    case "--timeout": options.RequestTimeoutSeconds = int.Parse(Next()); break;
                    case "--report": options.ReportSeconds = int.Parse(Next()); break;
                    case "--json": options.JsonOut = Next(); break;
                    case "--allow-production": options.AllowProduction = true; break;
                    case "--verbose": options.Verbose = true; break;
                    case "--help" or "-h": error = "help"; return options;
                    default:
                        error = $"Opzione sconosciuta: {key}";
                        return options;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return options;
            }
        }

        if (options.Clients < 0 || options.WebClients < 0)
            error = "Il numero di client non puo' essere negativo.";
        if (options.ThinkMaxSeconds < options.ThinkMinSeconds)
            error = "--think-max deve essere >= --think-min.";
        if (options.Profile == Profile.Web && string.IsNullOrWhiteSpace(options.WebUrl))
            error = "Il profilo web richiede --web-url.";
        return options;
    }

    private static T ParseEnum<T>(string value) where T : struct =>
        Enum.TryParse(value, ignoreCase: true, out T parsed)
            ? parsed
            : throw new ArgumentException($"Valore non valido '{value}'. Ammessi: {string.Join(", ", Enum.GetNames(typeof(T)))}");

    public const string Usage = """
        Prova di carico di AccardND.

          dotnet run -c Release --project Server/AccardND.LoadTest -- [opzioni]

        Bersaglio
          --url <ws://...|wss://...>   endpoint WebSocket (default ws://127.0.0.1:5017/ws)
          --web-url <https://...>      radice del sito per il carico HTTP (hall of fame, statistiche)
          --allow-production           conferma esplicita se l'url punta al server vero

        Carico
          --profile <connect|singleplayer|pvp|mixed|web>   default mixed
          --clients <n>                bot WebSocket (default 100)
          --web-clients <n>            worker HTTP paralleli (default 0)
          --ramp <secondi>             finestra di ingresso dei bot (default 60, 0 = tutti insieme)
          --duration <secondi>         durata della fase a regime (default 300)
          --think-min/--think-max <s>  pausa fra le azioni di un bot (default 3 / 10)
          --pvp-share <0..1>           quota di bot in PvP nel profilo misto (default 0.25)
          --pvp-ranked                 PvP dalla coda ranked invece che da stanze private

        Account
          --login <register|login>     default register (crea un account per bot)
          --prefix <testo>             prefisso degli account bot (default lt)
          --password <testo>           password degli account bot (default loadtest123)
          --client-version <x.y.z>     versione dichiarata al server (default 0.9.2)

        Uscita
          --report <secondi>           riga di stato ogni N secondi (default 5)
          --json <file>                salva il riepilogo finale in JSON
          --verbose                    stampa il dettaglio dei primi errori
        """;
}
