namespace AccardND.Server.Admin;

/// <summary>
/// Pagina HTML del pannello admin, self-contained (CSS e JS inline, nessuna CDN).
/// Serve login, dashboard, tabelle e grafici. Parla solo con /admin/api/*.
/// </summary>
public static class AdminPage
{
    public const string Html = """
<!doctype html>
<html lang="it">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>AccardND · Admin</title>
<style>
:root{
  --bg:#0f1216; --panel:#171b21; --panel2:#1e242c; --line:#2a323c;
  --text:#e8e6e1; --muted:#8a94a2; --gold:#d4af37; --gold2:#f0d878;
  --blue:#5aa9e6; --green:#5ad19a; --red:#e06a6a; --purple:#b18cf0;
}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--text);
  font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;font-size:14px}
a{color:var(--blue)}
h1,h2,h3{margin:0 0 .4em;font-weight:600}
button{font:inherit;cursor:pointer;border:1px solid var(--line);background:var(--panel2);
  color:var(--text);padding:.5em .9em;border-radius:8px}
button:hover{border-color:var(--gold)}
button.primary{background:var(--gold);color:#1a1a1a;border-color:var(--gold);font-weight:600}
button.danger{border-color:var(--red);color:var(--red)}
button.danger:hover{background:var(--red);color:#fff}
input,select{font:inherit;background:var(--panel);color:var(--text);
  border:1px solid var(--line);border-radius:8px;padding:.5em .7em}
input:focus,select:focus{outline:none;border-color:var(--gold)}
.hidden{display:none!important}
.muted{color:var(--muted)}
.mono{font-family:ui-monospace,SFMono-Regular,Menlo,monospace;font-size:12px}

/* Login */
#login{min-height:100vh;display:grid;place-items:center}
#login .card{background:var(--panel);border:1px solid var(--line);border-radius:14px;
  padding:2em;width:min(360px,92vw)}
#login h1{color:var(--gold);text-align:center}
#login .row{display:flex;flex-direction:column;gap:.4em;margin-bottom:1em}
#login label{color:var(--muted);font-size:12px}
#loginErr{color:var(--red);min-height:1.2em;font-size:13px;text-align:center}

/* App */
header{position:sticky;top:0;z-index:5;display:flex;align-items:center;gap:1em;
  padding:.8em 1.2em;background:var(--panel);border-bottom:1px solid var(--line)}
header .brand{color:var(--gold);font-weight:700;font-size:16px;letter-spacing:.5px}
header .spacer{flex:1}
.pill{display:inline-flex;align-items:center;gap:.4em;background:var(--panel2);
  border:1px solid var(--line);border-radius:999px;padding:.35em .8em;font-size:12px}
.dot{width:8px;height:8px;border-radius:50%;background:var(--green)}
nav{display:flex;gap:.3em;padding:.6em 1.2em;background:var(--panel);border-bottom:1px solid var(--line);
  flex-wrap:wrap}
nav button{background:transparent;border:1px solid transparent}
nav button.active{border-color:var(--gold);color:var(--gold)}
main{padding:1.2em;max-width:1200px;margin:0 auto}
section{margin-bottom:1.5em}

/* KPI grid */
.kpis{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:.8em}
.kpi{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:1em}
.kpi .v{font-size:26px;font-weight:700;color:var(--gold2)}
.kpi .l{color:var(--muted);font-size:12px;margin-top:.2em}
.kpi .sub{color:var(--muted);font-size:11px;margin-top:.3em}

.panel{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:1.1em}
.panel h2{font-size:15px}
.toolbar{display:flex;gap:.6em;align-items:center;flex-wrap:wrap;margin-bottom:.9em}
.toolbar .spacer{flex:1}
.legend{display:flex;gap:1em;flex-wrap:wrap;font-size:12px;margin-top:.5em}
.legend label{display:inline-flex;align-items:center;gap:.4em;cursor:pointer;color:var(--muted)}
.legend .sw{width:12px;height:12px;border-radius:3px;display:inline-block}

table{width:100%;border-collapse:collapse;font-size:13px}
th,td{text-align:left;padding:.55em .6em;border-bottom:1px solid var(--line)}
th{color:var(--muted);font-weight:600;font-size:12px;text-transform:uppercase;letter-spacing:.5px}
tbody tr{cursor:pointer}
tbody tr:hover{background:var(--panel2)}
.tag{display:inline-block;padding:.15em .5em;border-radius:6px;font-size:11px;border:1px solid var(--line)}
.tag.win{color:var(--green);border-color:var(--green)}
.tag.loss{color:var(--red);border-color:var(--red)}
.tag.draw{color:var(--muted)}
.tag.ranked{color:var(--gold);border-color:var(--gold)}

/* Modal */
.overlay{position:fixed;inset:0;background:rgba(0,0,0,.6);display:grid;place-items:center;z-index:20;padding:1em}
.modal{background:var(--panel);border:1px solid var(--line);border-radius:14px;
  width:min(760px,96vw);max-height:90vh;overflow:auto;padding:1.4em}
.modal .close{position:absolute}
.modal h2{color:var(--gold)}
.grid2{display:grid;grid-template-columns:1fr 1fr;gap:.8em}
.field{background:var(--panel2);border:1px solid var(--line);border-radius:8px;padding:.6em .8em}
.field .k{color:var(--muted);font-size:11px}
.field .v{font-size:15px;margin-top:.2em}
.actions{display:flex;gap:.5em;flex-wrap:wrap;margin:1em 0;padding:1em 0;border-top:1px solid var(--line);border-bottom:1px solid var(--line)}
.subh{color:var(--muted);font-size:12px;text-transform:uppercase;letter-spacing:.5px;margin:1.2em 0 .5em}

/* Barre di avanzamento (quest, achievement) */
.bar{position:relative;height:8px;min-width:90px;border-radius:4px;background:var(--panel2);
  border:1px solid var(--line);overflow:hidden}
.bar i{display:block;height:100%;background:var(--blue)}
.bar.done i{background:var(--green)}
.chips{display:flex;gap:.4em;flex-wrap:wrap}
.qtitle{font-weight:600}
.qdesc{color:var(--muted);font-size:12px}
@media(max-width:640px){.grid2{grid-template-columns:1fr}}
</style>
</head>
<body>

<div id="login">
  <div class="card">
    <h1>⚔ AccardND</h1>
    <p class="muted" style="text-align:center;margin-top:-.4em">Pannello amministrazione</p>
    <form id="loginForm">
      <div class="row"><label>Username</label><input id="u" autocomplete="username" required></div>
      <div class="row"><label>Password</label><input id="p" type="password" autocomplete="current-password" required></div>
      <button class="primary" style="width:100%" type="submit">Entra</button>
      <div id="loginErr"></div>
    </form>
  </div>
</div>

<div id="app" class="hidden">
  <header>
    <span class="brand">⚔ AccardND · Admin</span>
    <span class="spacer"></span>
    <span class="pill"><span class="dot"></span><span id="online">0</span> online</span>
    <button id="refresh">↻ Aggiorna</button>
    <button id="logout">Esci</button>
  </header>
  <nav>
    <button data-tab="overview" class="active">Panoramica</button>
    <button data-tab="players">Giocatori</button>
    <button data-tab="quests">Quest taverna</button>
    <button data-tab="matches">Partite PvP</button>
    <button data-tab="seasons">Stagioni</button>
  </nav>
  <main>
    <div id="tab-overview" class="tab">
      <section class="kpis" id="kpis"></section>
      <section class="panel">
        <div class="toolbar">
          <h2 style="margin:0">Attività nel tempo</h2>
          <span class="spacer"></span>
          <select id="range">
            <option value="7">7 giorni</option>
            <option value="30" selected>30 giorni</option>
            <option value="90">90 giorni</option>
          </select>
        </div>
        <div id="chart"></div>
        <div class="legend" id="legend"></div>
      </section>
    </div>

    <div id="tab-players" class="tab hidden">
      <section class="panel">
        <div class="toolbar">
          <input id="playerSearch" placeholder="Cerca nome, mail o player_id…" style="min-width:260px">
          <span class="spacer"></span>
          <span class="muted" id="playersCount"></span>
        </div>
        <table><thead><tr id="playersHead">
          <th data-sort="name">Nome</th><th data-sort="source">Fonte</th>
          <th data-sort="level">Liv.</th><th data-sort="exp">Exp tot.</th>
          <th data-sort="honey">Miele</th><th data-sort="matches">Match</th>
          <th data-sort="wins">Win</th><th data-sort="losses">Sconfitte</th>
          <th data-sort="created">Registrato</th><th data-sort="lastLogin">Ultimo login</th>
        </tr></thead><tbody id="playersBody"></tbody></table>
      </section>
    </div>

    <div id="tab-quests" class="tab hidden">
      <section class="kpis" id="questKpis"></section>
      <section class="panel">
        <div class="toolbar">
          <h2 style="margin:0">Quest di oggi</h2>
          <span class="muted mono" id="questDay"></span>
          <span class="spacer"></span>
          <span class="muted" id="questRefresh"></span>
        </div>
        <table><thead><tr>
          <th>Quest</th><th>Obiettivo</th><th>Assegnata</th><th>Completata</th><th>Riscossa</th><th>Completamento</th>
        </tr></thead><tbody id="questTodayBody"></tbody></table>
        <p class="muted" style="font-size:12px">
          "Assegnata" conta i giocatori che oggi hanno aperto la taverna: le quest partono al
          primo contatto della giornata, non a mezzanotte.
        </p>
      </section>
      <section class="panel">
        <div class="toolbar">
          <h2 style="margin:0">Storico giornate</h2>
          <span class="spacer"></span>
          <select id="questRange">
            <option value="7">7 giorni</option>
            <option value="14" selected>14 giorni</option>
            <option value="30">30 giorni</option>
            <option value="90">90 giorni</option>
          </select>
        </div>
        <div id="questChart"></div>
        <div class="legend" id="questLegend"></div>
        <table style="margin-top:1em"><thead><tr>
          <th>Giorno</th><th>Giocatori</th><th>Quest riscosse</th><th>Giornate complete</th><th>Miele erogato</th>
        </tr></thead><tbody id="questHistoryBody"></tbody></table>
        <p class="muted" style="font-size:12px">
          Lo storico conta le riscossioni: i contatori sono cumulativi, quindi rivalutare oggi
          il completamento di ieri direbbe quanti hanno superato la soglia da allora.
        </p>
      </section>
      <section class="panel">
        <h2>Catalogo completo</h2>
        <table><thead><tr>
          <th>Quest</th><th>Tipo</th><th>Obiettivo</th><th>Giorni in cui è uscita</th><th>Riscossioni totali</th>
        </tr></thead><tbody id="questCatalogBody"></tbody></table>
      </section>
    </div>

    <div id="tab-matches" class="tab hidden">
      <section class="panel">
        <h2>Ultime partite PvP</h2>
        <table><thead><tr>
          <th>Quando</th><th>Giocatore A</th><th>Punti</th><th>Giocatore B</th><th>Tipo</th><th>Fine</th>
        </tr></thead><tbody id="matchesBody"></tbody></table>
      </section>
    </div>

    <div id="tab-seasons" class="tab hidden">
      <section class="panel">
        <h2>Stagioni</h2>
        <table><thead><tr>
          <th>ID</th><th>Nome</th><th>Inizio</th><th>Fine</th><th>Attiva</th><th>Match</th><th>Giocatori</th>
        </tr></thead><tbody id="seasonsBody"></tbody></table>
      </section>
    </div>
  </main>
</div>

<div id="modal" class="overlay hidden"><div class="modal" id="modalBox"></div></div>

<script>
const API = '/admin/api';
let token = localStorage.getItem('adm_token') || '';

function fmtDate(s){ if(!s) return '—'; const d=new Date(s); return isNaN(d)?s:d.toLocaleString('it-IT'); }
function fmtDay(s){ if(!s) return '—'; const d=new Date(s); return isNaN(d)?s:d.toLocaleDateString('it-IT'); }
function esc(s){ return (s??'').toString().replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])); }
function el(id){ return document.getElementById(id); }

// 'external' dice solo che l'account nasce da un token Unity Authentication:
// il metodo vero (Google o ospite anonimo) sta in auth_method.
// 'google.com' e' quello che arriva dal claim firmato nel token UGS; 'google' e'
// il valore che dichiara il client quando il token non porta il provider.
const AUTH_LABELS={'google':'Google','google.com':'Google',
  'google-play-games':'Google Play Games','play-games':'Google Play Games',
  'anonymous':'ospite anonimo','unknown':'metodo sconosciuto'};
function authLabel(m){ return m ? (AUTH_LABELS[m]||m) : 'metodo sconosciuto'; }
function sourceLabel(source,method){
  return source==='external' ? authLabel(method) : source;
}

async function api(path, opts={}){
  const res = await fetch(API+path, {
    ...opts,
    headers:{ 'Authorization':'Bearer '+token, 'Content-Type':'application/json', ...(opts.headers||{}) }
  });
  if(res.status===401){ logout(); throw new Error('unauth'); }
  const data = await res.json().catch(()=>null);
  if(!res.ok) throw new Error(data&&data.error || ('HTTP '+res.status));
  return data;
}

/* ---- Auth ---- */
el('loginForm').addEventListener('submit', async e=>{
  e.preventDefault(); el('loginErr').textContent='';
  try{
    const res = await fetch(API+'/login',{method:'POST',headers:{'Content-Type':'application/json'},
      body:JSON.stringify({username:el('u').value,password:el('p').value})});
    const data = await res.json();
    if(!res.ok){ el('loginErr').textContent = data.error||'Errore'; return; }
    token = data.token; localStorage.setItem('adm_token',token); showApp();
  }catch(err){ el('loginErr').textContent='Connessione fallita'; }
});
function logout(){ token=''; localStorage.removeItem('adm_token'); el('app').classList.add('hidden'); el('login').classList.remove('hidden'); }
el('logout').addEventListener('click', async ()=>{ try{ await api('/logout',{method:'POST'}); }catch{} logout(); });

async function showApp(){
  el('login').classList.add('hidden'); el('app').classList.remove('hidden');
  await loadOverview();
}

/* ---- Tabs ---- */
const LOADERS={overview:loadOverview,players:loadPlayers,quests:loadQuests,matches:loadMatches,seasons:loadSeasons};
document.querySelectorAll('nav button').forEach(b=>b.addEventListener('click',()=>{
  document.querySelectorAll('nav button').forEach(x=>x.classList.remove('active'));
  b.classList.add('active');
  document.querySelectorAll('.tab').forEach(t=>t.classList.add('hidden'));
  el('tab-'+b.dataset.tab).classList.remove('hidden');
  const load=LOADERS[b.dataset.tab];
  if(load) load();
}));
el('refresh').addEventListener('click',()=>{
  LOADERS[document.querySelector('nav button.active').dataset.tab]();
});

/* ---- Overview ---- */
const METRICS=[
  {key:'logins',label:'Login',color:'var(--blue)'},
  {key:'signups',label:'Registrazioni',color:'var(--gold)'},
  {key:'matches',label:'Partite PvP',color:'var(--green)'},
  {key:'campaign',label:'Run campagna',color:'var(--purple)'},
];
let visible={logins:true,signups:true,matches:true,campaign:true};
let seriesCache=null;

async function loadOverview(){
  const o = await api('/overview');
  el('online').textContent=o.onlineNow;
  const bySource=(o.accountsBySource||[]).map(s=>esc(sourceLabel(s.source,s.authMethod))+': '+s.count).join(' · ')||'—';
  const kpis=[
    ['Account totali',o.totalAccounts,`+${o.newAccounts7d} in 7g · +${o.newAccounts30d} in 30g`],
    ['Attivi (24h)',o.activePlayers24h,`${o.activePlayers7d} in 7 giorni`],
    ['Login (24h)',o.logins24h,`${o.logins7d} in 7 giorni`],
    ['Partite PvP',o.totalMatches,`${o.matches24h} in 24h · ${o.rankedMatches} ranked`],
    ['Run campagna',o.totalCampaignRuns,`${o.campaignRuns24h} in 24h · ${o.campaignRuns7d} in 7g`],
    ['Online ora',o.onlineNow,bySource],
    ['Stagione',o.activeSeason||'—',''],
  ];
  el('kpis').innerHTML=kpis.map(k=>`<div class="kpi"><div class="v">${esc(k[1])}</div><div class="l">${esc(k[0])}</div><div class="sub">${esc(k[2])}</div></div>`).join('');
  await loadChart();
  renderLegend();
}
el('range').addEventListener('change',loadChart);

async function loadChart(){
  const days=el('range').value;
  const data=await api('/timeseries?days='+days);
  seriesCache=data.points;
  drawChart();
}
function renderLegend(){
  el('legend').innerHTML=METRICS.map(m=>
    `<label><input type="checkbox" data-m="${m.key}" ${visible[m.key]?'checked':''}>
      <span class="sw" style="background:${m.color}"></span>${m.label}</label>`).join('');
  el('legend').querySelectorAll('input').forEach(i=>i.addEventListener('change',()=>{
    visible[i.dataset.m]=i.checked; drawChart();
  }));
}
function drawChart(){
  if(!seriesCache) return;
  el('chart').innerHTML=lineChartSvg(seriesCache, METRICS.filter(m=>visible[m.key]),
    Math.min(1100, el('chart').clientWidth||900), 280);
}
/* Grafico a linee condiviso da panoramica e quest: nessuna libreria, solo SVG. */
function lineChartSvg(pts, active, W, H){
  const padL=38,padR=12,padT=14,padB=26;
  let max=1;
  pts.forEach(p=>active.forEach(m=>{ if(p[m.key]>max) max=p[m.key]; }));
  const n=pts.length, iw=W-padL-padR, ih=H-padT-padB;
  const x=i=> padL + (n<=1?0:iw*i/(n-1));
  const y=v=> padT + ih - ih*v/max;
  let svg=`<svg viewBox="0 0 ${W} ${H}" width="100%" style="max-width:100%">`;
  // griglia orizzontale
  for(let g=0;g<=4;g++){ const gv=Math.round(max*g/4), gy=y(gv);
    svg+=`<line x1="${padL}" y1="${gy}" x2="${W-padR}" y2="${gy}" stroke="var(--line)"/>`;
    svg+=`<text x="4" y="${gy+4}" fill="var(--muted)" font-size="10">${gv}</text>`; }
  // etichette x (max 8)
  const step=Math.max(1,Math.ceil(n/8));
  for(let i=0;i<n;i+=step){ svg+=`<text x="${x(i)}" y="${H-8}" fill="var(--muted)" font-size="10" text-anchor="middle">${pts[i].date.slice(5)}</text>`; }
  // linee
  active.forEach(m=>{
    const d=pts.map((p,i)=>`${i===0?'M':'L'}${x(i).toFixed(1)},${y(p[m.key]).toFixed(1)}`).join(' ');
    svg+=`<path d="${d}" fill="none" stroke="${m.color}" stroke-width="2"/>`;
    pts.forEach((p,i)=>{ if(n<=60) svg+=`<circle cx="${x(i)}" cy="${y(p[m.key])}" r="2" fill="${m.color}"/>`; });
  });
  svg+='</svg>';
  return svg;
}
function staticLegend(metrics){
  return metrics.map(m=>`<label><span class="sw" style="background:${m.color}"></span>${m.label}</label>`).join('');
}

/* ---- Players ---- */
let searchTimer=null;
el('playerSearch').addEventListener('input',()=>{ clearTimeout(searchTimer); searchTimer=setTimeout(loadPlayers,250); });

// L'ordinamento lo fa il server: ordinare qui riguarderebbe solo la pagina
// caricata, e con piu' account della pagina il risultato sarebbe sbagliato.
let playerSort={key:'lastLogin',desc:true};
// I testuali partono crescenti (A->Z), i numerici e le date decrescenti.
const SORT_STARTS_ASC=['name','source'];

el('playersHead').addEventListener('click',event=>{
  const th=event.target.closest('th[data-sort]');
  if(!th) return;
  const key=th.dataset.sort;
  if(playerSort.key===key) playerSort.desc=!playerSort.desc;
  else playerSort={key,desc:!SORT_STARTS_ASC.includes(key)};
  loadPlayers();
});

function paintSortIndicators(){
  el('playersHead').querySelectorAll('th[data-sort]').forEach(th=>{
    if(th.dataset.label===undefined) th.dataset.label=th.textContent;
    th.style.cursor='pointer';
    th.style.userSelect='none';
    const active=th.dataset.sort===playerSort.key;
    th.textContent=th.dataset.label+(active?(playerSort.desc?' ▼':' ▲'):'');
    th.style.color=active?'var(--gold)':'';
  });
}

async function loadPlayers(){
  const q=encodeURIComponent(el('playerSearch').value||'');
  const data=await api(`/players?search=${q}&limit=100&sort=${playerSort.key}&desc=${playerSort.desc}`);
  paintSortIndicators();
  el('playersCount').textContent=`${data.total} account`;
  el('playersBody').innerHTML=data.players.map(p=>`
    <tr data-id="${esc(p.playerId)}">
      <td>${esc(p.username)}${p.online?' <span class="tag win">online</span>':''}${p.nickname&&p.nickname!==p.username?`<div class="muted" style="font-size:12px;margin-top:3px">${esc(p.nickname)}</div>`:''}</td>
      <td><span class="tag">${esc(p.source)}</span>${p.source==='external'?` <span class="tag">${esc(authLabel(p.authMethod))}</span>`:''}${p.email?`<div class="muted" style="font-size:12px;margin-top:3px">${esc(p.email)}</div>`:''}</td>
      <td>${p.accountLevel}<div class="muted" style="font-size:12px">${p.accountExperience}/${p.accountExperienceToNextLevel}</div></td>
      <td>${p.accountTotalExperience}</td>
      <td>${p.honey}</td><td>${p.matches}</td><td>${p.wins}</td><td>${p.losses}</td>
      <td class="muted">${fmtDay(p.createdAt)}</td>
      <td class="muted">${fmtDate(p.lastLoginAt)}</td>
    </tr>`).join('') || `<tr><td colspan="10" class="muted">Nessun risultato</td></tr>`;
  el('playersBody').querySelectorAll('tr[data-id]').forEach(tr=>
    tr.addEventListener('click',()=>openPlayer(tr.dataset.id)));
}

async function openPlayer(id){
  const d=await api('/players/'+encodeURIComponent(id));
  const a=d.account, lt=d.lifetime, sn=d.season, rk=d.ranked, ct=d.campaignTotals, tv=d.tavern;
  const f=(k,v)=>`<div class="field"><div class="k">${k}</div><div class="v">${v}</div></div>`;
  const sub=t=>`<div class="subh">${t}</div>`;
  const tbl=(head,body)=>`<table><thead><tr>${head.map(h=>`<th>${h}</th>`).join('')}</tr></thead><tbody>${body}</tbody></table>`;

  let html=`<h2>${esc(a.username)} ${a.online?'<span class="tag win">online</span>':''}</h2>
    <div class="mono muted" style="margin-bottom:1em">${esc(a.playerId)}</div>
    <div class="grid2">
      ${f('Fonte',esc(a.source)+(a.source==='external'?' <span class="muted" style="font-size:12px">'+esc(authLabel(a.authMethod))+'</span>':''))}
      ${f('Mail',a.email?esc(a.email):'<span class="muted">non registrata</span>')}
      ${f('Registrato',fmtDate(a.createdAt))}
      ${f('Ultimo login',fmtDate(a.lastLoginAt))}
      ${f('Miele',a.honey)}
      ${f('Livello account',a.accountLevel+' <span class="muted" style="font-size:12px">'+a.accountExperience+'/'+a.accountExperienceToNextLevel+' exp</span>')}
      ${f('Esperienza totale',a.accountTotalExperience)}
      ${f('Tutorial',a.tutorialCompleted?'✔':'—')}
      ${f('Hardcore',a.hardcoreUnlocked?'✔':'—')}
      ${a.nickname?f('Nickname',esc(a.nickname)):''}
      ${a.selectedIconId?f('Icona',esc(a.selectedIconId)):''}
    </div>
    ${a.bio?`<div class="field" style="margin-top:.8em"><div class="k">Bio</div><div class="v">${esc(a.bio)}</div></div>`:''}`;

  html+=`<div class="actions">
      <button data-act="rename">Rinomina</button>
      <button data-act="honey">Imposta miele</button>
      <button data-act="reset">Reset progressi</button>
      <button class="danger" data-act="delete">Elimina account</button>
    </div>`;

  /* Taverna */
  html+=sub('Taverna · quest di oggi ('+tv.day+' UTC)');
  if(tv.quests.length){
    html+=tbl(['Quest','Progresso','Stato'], tv.quests.map(q=>`<tr>
      <td><div class="qtitle">${esc(q.title)}</div><div class="qdesc">${esc(q.description)}</div></td>
      <td>${bar(q.current,q.threshold,q.completed)}<span class="muted">${q.current}/${q.threshold}</span></td>
      <td>${q.claimed?'<span class="tag win">riscossa</span>':q.completed?'<span class="tag ranked">da riscuotere</span>':'<span class="tag">in corso</span>'}</td>
    </tr>`).join(''));
  } else {
    html+=`<p class="muted">Non ha ancora aperto la taverna oggi: le quest gli verranno assegnate al primo accesso.</p>`;
  }
  html+=`<div class="grid2">
      ${f('Quest completate oggi',tv.completedCount+' / '+tv.quests.length)}
      ${f('Premio di giornata',tv.bonusClaimed?'riscosso':tv.bonusAvailable?'disponibile':'non ancora')}
      ${f('Quest riscosse in totale',tv.claimsAllTime)}
      ${f('Giornate complete',tv.bonusesAllTime)}
      ${f('Miele guadagnato in taverna',tv.honeyFromQuests)}
    </div>`;

  /* Campagna */
  if(ct) html+=sub('Progressione campagna')+`<div class="grid2">
      ${f('Run concluse',ct.runs)}
      ${f('Stanze superate',ct.roomsCleared)}
      ${f('Nemici sconfitti',ct.enemiesDefeated)}
      ${f('Boss di capitolo',ct.bossesDefeated)}
      ${f('Miniboss',ct.minibossesDefeated)}
      ${f('Prima run',fmtDate(ct.firstRunAt))}
      ${f('Ultima run',fmtDate(ct.lastRunAt))}
    </div>`;
  if(d.counters.length) html+=sub('Contatori (Santuario e quest)')+
    tbl(['Contatore','Valore','Chiave'], d.counters.map(c=>
      `<tr><td>${esc(c.label)}</td><td><b>${c.value}</b></td><td class="mono muted">${esc(c.key)}</td></tr>`).join(''));
  if(d.rewards.length) html+=sub('Ricompense riscattate')+
    tbl(['Tipo','Volte','Miele','Esperienza','Con pubblicità','Ultima'], d.rewards.map(r=>
      `<tr><td>${esc(r.type)}</td><td>${r.count}</td><td>${r.honey}</td><td>${r.experience}</td>
       <td>${r.withAd}</td><td class="muted">${fmtDate(r.lastAt)}</td></tr>`).join(''));

  /* Santuario */
  if(d.unlocks.length) html+=sub('Sblocchi Santuario')+
    tbl(['Tipo','Nome','Id','Quando'], d.unlocks.map(u=>
      `<tr><td>${esc(u.type)}</td><td>${esc(u.name)}</td><td class="mono muted">${esc(u.id)}</td>
       <td class="muted">${fmtDate(u.unlockedAt)}</td></tr>`).join(''));
  if(d.consumables.length) html+=sub('Scorta consumabili')+
    tbl(['Oggetto','Quantità'], d.consumables.map(c=>
      `<tr><td>${esc(c.name)}</td><td>${c.count}</td></tr>`).join(''));
  if(d.bag.length) html+=sub('Bisaccia per la prossima run')+
    `<div class="chips">${d.bag.map(b=>`<span class="tag">${esc(b.name)}</span>`).join('')}</div>`;

  /* PvP */
  if(lt||sn||rk) html+=sub('PvP');
  if(rk) html+=`<div class="grid2">
      ${f('Tier',esc(rk.tier)+' '+esc(rk.division)+' <span class="muted" style="font-size:12px">'+rk.leaguePoints+' LP</span>')}
      ${f('MMR',rk.mmr+' <span class="muted" style="font-size:12px">picco '+rk.peakMmr+'</span>')}
      ${f('Classifica',rk.rank>0?('#'+rk.rank+' su '+rk.players):'—')}
      ${f('Partite ranked',rk.gamesPlayed+(rk.placementDone?'':' (piazzamento in corso)'))}
    </div>`;
  const statsRow=(label,s)=>s?`<tr><td>${label}</td><td>${s.matches}</td><td>${s.wins}</td><td>${s.losses}</td>
      <td>${s.forfeits}</td><td>${s.winRatePercent}%</td><td>${s.bestStreak}</td><td>${s.currentStreak}</td>
      <td>${Math.round(s.totalMatchSeconds/60)} min</td></tr>`:'';
  if(lt||sn) html+=tbl(['Ambito','Match','Vittorie','Sconfitte','Abbandoni','Win rate','Miglior streak','Streak','Tempo'],
    statsRow('Da sempre',lt)+statsRow(esc(d.seasonName||'Stagione'),sn));
  if(d.hallOfFame.length) html+=sub('Hall of Fame')+
    tbl(['Stagione','Posizione','Tier','MMR','V','S'], d.hallOfFame.map(h=>
      `<tr><td>${esc(h.seasonName)}</td><td>#${h.rank}</td><td>${esc(h.tier)} ${esc(h.division)}</td>
       <td>${h.mmr}</td><td>${h.wins}</td><td>${h.losses}</td></tr>`).join(''));
  if(d.friends.length) html+=sub('Amici')+
    `<div class="chips">${d.friends.map(x=>`<span class="tag">${esc(x.status)}: ${x.count}</span>`).join('')}</div>`;

  /* Collezione */
  if(d.achievements.length) html+=sub('Achievement')+
    tbl(['Achievement','Progresso','Sbloccato'], d.achievements.map(x=>
      `<tr><td>${esc(x.name)}</td>
       <td>${x.threshold?bar(x.progress,x.threshold,!!x.unlockedAt)+`<span class="muted">${x.progress}/${x.threshold}</span>`:x.progress}</td>
       <td class="muted">${x.unlockedAt?fmtDate(x.unlockedAt):'—'}</td></tr>`).join(''));
  if(d.icons.length) html+=sub('Icone sbloccate ('+d.icons.length+')')+
    `<div class="chips">${d.icons.map(i=>
      `<span class="tag${i.id===a.selectedIconId?' ranked':''}">${esc(i.name)}</span>`).join('')}</div>`;
  if(d.campaignKills.length) html+=sub('Mostri sconfitti')+
    tbl(['Mostro','Uccisioni','Prima volta'], d.campaignKills.map(k=>
      `<tr><td>${esc(k.id)}</td><td>${k.kills}</td><td class="muted">${fmtDate(k.firstKilledAt)}</td></tr>`).join(''));

  /* Storico */
  if(d.recentMatches.length){ html+=sub('Ultime partite PvP')+
    `<table><tbody>${d.recentMatches.map(m=>{
      const opp=m.playerA===id?m.nameB:m.nameA;
      const cls=m.result==='win'?'win':m.result==='loss'?'loss':'draw';
      return `<tr><td class="muted">${fmtDate(m.endedAt)}</td><td><span class="tag ${cls}">${m.result||'?'}</span></td>
        <td>vs ${esc(opp)}</td><td>${m.scoreA}-${m.scoreB}</td>
        <td>${m.ranked?'<span class="tag ranked">ranked</span>':''} <span class="muted">${esc(m.endedReason)}</span></td></tr>`;
    }).join('')}</tbody></table>`; }
  if(d.recentRuns.length){ html+=sub('Ultime run campagna')+
    tbl(['Quando','Modalità','Capitolo','Stanze','Nemici','Boss','Miele'], d.recentRuns.map(r=>
      `<tr><td class="muted">${fmtDate(r.endedAt)}</td><td>${esc(r.mode)||'—'}</td>
       <td>${esc(r.chapterId)||'—'}</td><td>${r.roomsCleared}</td><td>${r.enemiesDefeated}</td>
       <td>${r.bossesDefeated}</td><td>${r.honeyReward}</td></tr>`).join('')); }
  if(d.recentLogins.length){ html+=sub('Ultimi login')+
    `<table><tbody>${d.recentLogins.map(l=>`<tr><td class="muted">${fmtDate(l.occurredAt)}</td><td>${esc(l.provider)}</td></tr>`).join('')}</tbody></table>`; }
  html=`<div style="text-align:right"><button onclick="closeModal()">✕</button></div>`+html;
  el('modalBox').innerHTML=html;
  el('modal').classList.remove('hidden');
  el('modalBox').querySelectorAll('[data-act]').forEach(btn=>btn.addEventListener('click',()=>playerAction(a,btn.dataset.act)));
}
function closeModal(){ el('modal').classList.add('hidden'); }
el('modal').addEventListener('click',e=>{ if(e.target===el('modal')) closeModal(); });

async function playerAction(a,act){
  try{
    if(act==='rename'){
      const name=prompt('Nuovo nome (3-18 caratteri):',a.username); if(!name) return;
      await api(`/players/${encodeURIComponent(a.playerId)}/rename`,{method:'POST',body:JSON.stringify({name})});
    }else if(act==='honey'){
      const v=prompt('Nuovo valore miele:',a.honey); if(v===null) return;
      await api(`/players/${encodeURIComponent(a.playerId)}/honey`,{method:'POST',body:JSON.stringify({honey:parseInt(v,10)||0})});
    }else if(act==='reset'){
      if(!confirm('Azzerare miele, tutorial, hardcore e sblocchi single player di '+a.username+'?')) return;
      await api(`/players/${encodeURIComponent(a.playerId)}/reset`,{method:'POST'});
    }else if(act==='delete'){
      if(!confirm('ELIMINARE definitivamente l\'account '+a.username+' e tutti i suoi dati? Operazione irreversibile.')) return;
      await api(`/players/${encodeURIComponent(a.playerId)}/delete`,{method:'POST'});
      closeModal(); loadPlayers(); return;
    }
    closeModal(); openPlayer(a.playerId); loadPlayers();
  }catch(err){ alert('Errore: '+err.message); }
}

/* ---- Quest taverna ---- */
const QUEST_METRICS=[
  {key:'players',label:'Giocatori in taverna',color:'var(--blue)'},
  {key:'claims',label:'Quest riscosse',color:'var(--gold)'},
  {key:'bonuses',label:'Giornate complete',color:'var(--green)'},
];
let questHistory=null;
/* Percentuale per le barre: limitata a 100 perche' i progressi possono superare la soglia. */
function pct(part,total){ return total>0?Math.min(100,Math.round(part*100/total)):0; }
function bar(part,total,done){
  return `<div class="bar${done?' done':''}"><i style="width:${pct(part,total)}%"></i></div>`;
}
el('questRange').addEventListener('change',loadQuests);
async function loadQuests(){
  const data=await api('/quests?days='+el('questRange').value);
  el('questDay').textContent=data.day+' (UTC)';
  const h=Math.floor(data.secondsToRefresh/3600), m=Math.floor(data.secondsToRefresh%3600/60);
  el('questRefresh').textContent=`Cambio quest fra ${h}h ${m}m`;

  const kpis=[
    ['In taverna oggi',data.playersToday,`${data.questsPerDay} quest al giorno`],
    ['Quest riscosse oggi',data.claimsToday,`${data.questHoneyReward} miele l'una`],
    ['Giornate complete oggi',data.bonusToday,`${data.bonusHoneyReward} miele di premio`],
    ['Miele erogato oggi',data.honeyToday,'unica fonte di miele del gioco'],
  ];
  el('questKpis').innerHTML=kpis.map(k=>
    `<div class="kpi"><div class="v">${esc(k[1])}</div><div class="l">${esc(k[0])}</div><div class="sub">${esc(k[2])}</div></div>`).join('');

  el('questTodayBody').innerHTML=data.quests.map(q=>`
    <tr>
      <td><div class="qtitle">${esc(q.title)} ${q.advanced?'<span class="tag ranked">avanzata</span>':''}</div>
          <div class="qdesc">${esc(q.description)}</div>
          <div class="mono muted">${esc(q.questId)}</div></td>
      <td>${esc(q.counterLabel)} <b>×${q.threshold}</b></td>
      <td>${q.assigned}</td>
      <td>${q.completed}</td>
      <td>${q.claimed}</td>
      <td>${bar(q.completed,q.assigned,q.assigned>0&&q.completed===q.assigned)}
          <span class="muted">${pct(q.completed,q.assigned)}%</span></td>
    </tr>`).join('') || `<tr><td colspan="6" class="muted">Nessun giocatore ha ancora aperto la taverna oggi</td></tr>`;

  questHistory=data.history;
  el('questLegend').innerHTML=staticLegend(QUEST_METRICS);
  drawQuestChart();
  el('questHistoryBody').innerHTML=data.history.slice().reverse().map(p=>`<tr>
    <td class="muted">${p.date}</td><td>${p.players}</td><td>${p.claims}</td>
    <td>${p.bonuses}</td><td>${p.honey}</td></tr>`).join('');

  el('questCatalogBody').innerHTML=data.catalog.map(q=>`<tr>
    <td><div class="qtitle">${esc(q.title)}</div><div class="qdesc">${esc(q.description)}</div></td>
    <td>${q.advanced?'<span class="tag ranked">avanzata</span>':'<span class="tag">base</span>'}</td>
    <td>${esc(q.counterLabel)} <b>×${q.threshold}</b></td>
    <td>${q.daysOut}</td><td>${q.claims}</td></tr>`).join('');
}
function drawQuestChart(){
  if(!questHistory) return;
  el('questChart').innerHTML=lineChartSvg(questHistory, QUEST_METRICS,
    Math.min(1100, el('questChart').clientWidth||900), 220);
}

/* ---- Matches ---- */
async function loadMatches(){
  const data=await api('/matches?limit=100');
  el('matchesBody').innerHTML=data.matches.map(m=>{
    const aw=m.winner===0, bw=m.winner===1;
    return `<tr><td class="muted">${fmtDate(m.endedAt)}</td>
      <td style="${aw?'color:var(--green)':''}">${esc(m.nameA)}</td>
      <td><b>${m.scoreA} - ${m.scoreB}</b></td>
      <td style="${bw?'color:var(--green)':''}">${esc(m.nameB)}</td>
      <td>${m.ranked?'<span class="tag ranked">ranked</span>':'<span class="tag">normale</span>'}</td>
      <td class="muted">${esc(m.endedReason)}</td></tr>`;
  }).join('') || `<tr><td colspan="6" class="muted">Nessuna partita</td></tr>`;
}

/* ---- Seasons ---- */
async function loadSeasons(){
  const data=await api('/seasons');
  el('seasonsBody').innerHTML=data.seasons.map(s=>`<tr>
    <td>${s.seasonId}</td><td>${esc(s.name)}</td>
    <td class="muted">${fmtDay(s.startsAt)}</td><td class="muted">${fmtDay(s.endsAt)}</td>
    <td>${s.isActive?'<span class="tag ranked">attiva</span>':'—'}</td>
    <td>${s.matches}</td><td>${s.players}</td></tr>`).join('') || `<tr><td colspan="7" class="muted">Nessuna stagione</td></tr>`;
}

/* ---- Boot ---- */
if(token){ showApp().catch(()=>logout()); }
window.addEventListener('resize',()=>{ if(seriesCache) drawChart(); if(questHistory) drawQuestChart(); });
</script>
</body>
</html>
""";
}
