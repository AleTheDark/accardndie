# Build Android — il subtarget texture che si rimette da solo su PVRTC

Sintomo: carichi l'App Bundle su Google Play e la scheda dice **"Disponibile su
3.098 dispositivi"** invece che su decine di migliaia. Sul telefono, il Play Store
risponde **"il tuo dispositivo non è compatibile con questa versione"** anche da un
Android recente e perfettamente in regola con `minSdk` e architettura.

## La causa

Nel manifest del bundle finisce questa riga:

```xml
<supports-gl-texture android:name="GL_IMG_texture_compression_pvrtc" />
```

`supports-gl-texture` è un **filtro rigido** del Play Store: l'app viene offerta solo
ai dispositivi che supportano almeno uno dei formati dichiarati. PVRTC è il formato
delle GPU PowerVR, che su Android non esistono quasi più — il parco è Adreno
(Qualcomm) e Mali (ARM). Da qui i 3.098 dispositivi.

La riga **non** viene dalle Player Settings, che sono a posto:

```
PlayerSettings.Android.textureCompressionFormats   = [ASTC]
m_BuildTargetDefaultTextureCompressionFormat       = 03000000   # 3 = ASTC
```

Viene da `EditorUserBuildSettings.androidBuildSubtarget`, la voce *Texture
Compression* della vecchia finestra Build Settings. Ed è il **bridge MCP** a
riscriverla a ogni build:

```csharp
// Library/PackageCache/com.coplaydev.unity-mcp@.../Editor/Tools/Build/BuildTargetMapping.cs
public static int ResolveSubtarget(string subtarget)
{
    if (string.IsNullOrEmpty(subtarget))
        return (int)StandaloneBuildSubtarget.Player;   // = 2
    ...
}
```

Quel valore finisce in `BuildPlayerOptions.subtarget`. Su Standalone il campo indica
Player/Server, dove 2 significa "Player". Su **Android** lo stesso campo indica il
formato di compressione texture, e 2 significa **PVRTC**:

| Enum | 2 |
|---|---|
| `StandaloneBuildSubtarget` | `Player` |
| `MobileTextureSubtarget` | `PVRTC` |

Risultato: ogni build Android lanciata con lo strumento `manage_build` senza passare
`subtarget` si impone PVRTC, **sovrascrivendo qualunque cosa tu abbia impostato prima**.
Impostare ASTC a mano e poi buildare con quel tool non serve a niente: il valore viene
ripristinato durante la build, non prima.

Nota sul rapporto fra le due impostazioni: le Player Settings dichiaravano ASTC mentre
il subtarget diceva PVRTC, e le due cose convivevano senza che Unity segnalasse nulla.
Non abbiamo accertato quale delle due comandi davvero i dati impacchettati — vedi
"Perché il peso cambia" in fondo. Quel che è certo è che a comandare la riga
`supports-gl-texture`, e quindi il filtro del Play Store, è il **subtarget**.

## Come si builda senza incapparci

Chiamare `BuildPipeline.BuildPlayer` direttamente, passando il subtarget giusto:

```csharp
var scene = new System.Collections.Generic.List<string>();
foreach (var s in EditorBuildSettings.scenes)
    if (s.enabled) scene.Add(s.path);

EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
EditorUserBuildSettings.buildAppBundle = true;

var opts = new BuildPlayerOptions();
opts.scenes           = scene.ToArray();
opts.locationPathName = "Builds/Android/Accardndie.aab";
opts.target           = BuildTarget.Android;
opts.targetGroup      = BuildTargetGroup.Android;
opts.subtarget        = (int)MobileTextureSubtarget.ASTC;   // 6, non lasciarlo a 0
BuildPipeline.BuildPlayer(opts);
```

Dalla UI di Unity il problema non si pone: Build Settings usa il valore che vedi nel
menù a tendina *Texture Compression*. Basta che sia **ASTC**, non PVRTC.

## Come si verifica, prima di caricare su Play

Il manifest unito resta in chiaro nell'output Gradle:

```bash
grep supports-gl-texture \
  Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/intermediates/bundle_manifest/release/processApplicationManifestReleaseForBundle/AndroidManifest.xml
```

Deve rispondere `GL_KHR_texture_compression_astc_ldr`. Se dice `GL_IMG_texture_compression_pvrtc`,
il bundle è da rifare: non caricarlo, perché il numero di dispositivi compatibili è la
prima cosa che noterai e l'ultima che collegherai a questa impostazione.

Controprova sul lato Play: dopo il caricamento, "Disponibile su N dispositivi" deve
essere nell'ordine delle decine di migliaia. Se leggi qualche migliaio, è di nuovo PVRTC.

## Il peso cambia, e dice qualcosa

Correggendo il subtarget il bundle è calato da 142,4 a 140,1 MB. Vale la pena
notarlo perché una riga di manifest non pesa 2,3 MB: se il solo effetto del
subtarget fosse la dichiarazione, il bundle sarebbe rimasto identico. Fra le due
build precedenti, che differivano solo per `versionCode` e restavano entrambe su
PVRTC, lo scarto era stato di **7 byte**.

Il sospetto è quindi che il subtarget influenzi anche i dati texture impacchettati,
e non solo l'etichetta — nonostante le Player Settings dicessero ASTC in entrambi i
casi. Non è stato verificato aprendo le texture nei due bundle, quindi resta un
sospetto motivato e non un fatto accertato. Ai fini pratici non cambia la
conclusione: il subtarget va tenuto su ASTC.
