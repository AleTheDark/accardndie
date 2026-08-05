using System.Runtime.CompilerServices;

// I test del motore devono poter comporre stati di partita che non sono
// raggiungibili dalle sole azioni pubbliche (una pedina gia' eliminata, un buff
// gia' applicato). Senza questo servirebbero setup lunghissimi via Attack/Pass,
// che renderebbero i test fragili proprio dove devono essere precisi.
[assembly: InternalsVisibleTo("AccardND.GameCore.Tests")]
