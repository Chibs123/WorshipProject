namespace WorshipProjectionApp.Services;

// Página web que o telemóvel abre para controlar a projeção
public static class RemotePage
{
    public const string Html = """
<!doctype html><html lang="pt"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<meta name="theme-color" content="#0b0d10"><title>Remoto</title>
<style>
:root{--bg:#0b0d10;--panel:#15191f;--card:#1c2128;--line:#2c333d;--fg:#f4f6f8;--mut:#9aa4b2;--acc:#22d3ee;--onacc:#06222a;--red:#ef4444;--sel:#0f3d4a}
*{box-sizing:border-box;-webkit-tap-highlight-color:transparent}
html,body{height:100%}
body{margin:0;background:var(--bg);color:var(--fg);font:16px system-ui,-apple-system,sans-serif;display:flex;flex-direction:column;height:100dvh;overscroll-behavior:none}
header{display:flex;align-items:center;gap:10px;padding:max(10px,env(safe-area-inset-top)) 14px 10px;background:var(--panel);border-bottom:1px solid var(--line)}
header h1{font-size:15px;margin:0;flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-weight:700}
.pill{font-size:11px;font-weight:800;letter-spacing:.06em;padding:4px 10px;border-radius:99px;background:var(--card);border:1px solid var(--line);color:var(--mut)}
.pill.live{background:#3a1518;border-color:var(--red);color:#ff8a8a}
.pill.black{background:#000;border-color:#555;color:#ddd}
main{flex:1;overflow-y:auto;padding:12px 14px 16px;-webkit-overflow-scrolling:touch}
nav{display:flex;background:var(--panel);border-top:1px solid var(--line);padding-bottom:env(safe-area-inset-bottom)}
nav button{flex:1;background:none;border:0;color:var(--mut);font:600 13px system-ui;padding:12px 4px}
nav button.on{color:var(--acc);box-shadow:inset 0 2px 0 var(--acc)}
.now{background:#000;border:1px solid var(--line);border-radius:12px;padding:14px;min-height:110px;max-height:34vh;overflow:auto;white-space:pre-wrap;text-align:center;font-weight:700;font-size:19px;line-height:1.35}
.now small{display:block;margin-top:8px;color:var(--mut);font-weight:500;font-size:12px}
.nxt{margin:10px 2px 14px;color:var(--mut);font-size:13px;white-space:pre-wrap;max-height:4.6em;overflow:hidden}
.nxt b{color:var(--fg)}
.row{display:flex;gap:10px;margin-bottom:10px}
button.b{flex:1;border:1px solid var(--line);background:var(--card);color:var(--fg);border-radius:12px;font:700 15px system-ui;padding:14px 6px}
button.b:active{transform:scale(.97);filter:brightness(1.2)}
button.b.big{height:92px;font-size:20px}
button.b.pri{background:var(--acc);border-color:var(--acc);color:var(--onacc);flex:2}
button.b.on{background:var(--sel);border-color:var(--acc);color:var(--acc)}
button.b.red.on{background:#3a1518;border-color:var(--red);color:#ff8a8a}
h2{font-size:11px;letter-spacing:.08em;text-transform:uppercase;color:var(--mut);margin:16px 2px 8px}
.it{display:flex;flex-direction:column;padding:11px 12px;border-radius:10px;background:var(--card);border:1px solid transparent;margin-bottom:6px}
.it b{font-size:15px}.it small{color:var(--mut);font-size:12px;margin-top:2px;white-space:pre-wrap}
.it.act{border-color:var(--acc);background:var(--sel)}.it.act b{color:var(--acc)}
.it.sub{margin-left:18px}.it.fold{background:none;color:var(--mut);font-weight:700;font-size:12px;letter-spacing:.06em;text-transform:uppercase;padding:8px 4px}
.it.sl{white-space:pre-wrap;font-size:14px}
.seg{display:flex;gap:6px;margin-bottom:10px}
.seg button{flex:1;padding:10px;border-radius:10px;border:1px solid var(--line);background:var(--card);color:var(--mut);font:700 14px system-ui}
.seg button.on{border-color:var(--acc);color:var(--acc);background:var(--sel)}
input[type=search],select{width:100%;padding:13px 12px;border-radius:10px;border:1px solid var(--line);background:var(--card);color:var(--fg);font:16px system-ui;margin-bottom:8px}
.res{display:flex;gap:8px;align-items:stretch;margin-bottom:6px}
.res .it{flex:1;margin:0}
.res button{width:48px;border-radius:10px;border:1px solid var(--line);background:var(--card);color:var(--fg);font-size:18px}
.res button.p{background:var(--acc);color:var(--onacc);border-color:var(--acc)}
label.ck{display:flex;align-items:center;gap:8px;color:var(--mut);font-size:13px;margin:0 2px 10px}
#toast{position:fixed;left:50%;bottom:84px;transform:translateX(-50%);background:var(--acc);color:var(--onacc);font-weight:700;padding:9px 16px;border-radius:99px;opacity:0;transition:opacity .2s;pointer-events:none}
#toast.on{opacity:1}
#pin{position:fixed;inset:0;background:var(--bg);display:none;flex-direction:column;justify-content:center;gap:12px;padding:28px;z-index:9}
#pin input{font-size:28px;text-align:center;letter-spacing:.3em}
.empty{color:var(--mut);text-align:center;padding:24px 8px;font-size:14px}
</style></head><body>
<header><h1 id="ttl">Remoto</h1><span class="pill" id="pill">...</span></header>
<main id="main"></main>
<nav><button data-t="ctl" class="on">Controlo</button><button data-t="prg">Programação</button><button data-t="src">Procurar</button></nav>
<div id="toast"></div>
<div id="pin"><h2 style="text-align:center">PIN do PC</h2><input id="pinin" type="tel" inputmode="numeric" autocomplete="off"><button class="b pri" id="pinok" style="flex:none">Entrar</button></div>
<script>
const $=id=>document.getElementById(id);
const el=(t,c,x)=>{const e=document.createElement(t);if(c)e.className=c;if(x!=null)e.textContent=x;return e};
let pin='',S=null,tab='ctl',sMode='songs',sRes=[],busy=false,lastSig='';
try{const q=new URLSearchParams(location.search);if(q.get('k')){pin=q.get('k');localStorage.setItem('pin',pin);history.replaceState(null,'',location.pathname)}else pin=localStorage.getItem('pin')||''}catch(e){}
function toast(m){const t=$('toast');t.textContent=m;t.classList.add('on');clearTimeout(toast.h);toast.h=setTimeout(()=>t.classList.remove('on'),1400)}
function askPin(){$('pin').style.display='flex'}
$('pinok').onclick=()=>{pin=$('pinin').value.trim();try{localStorage.setItem('pin',pin)}catch(e){}$('pin').style.display='none';poll()};
async function api(p,body){
  const o={headers:{'X-Pin':pin}};
  if(body!==undefined){o.method='POST';o.headers['Content-Type']='application/json';o.body=JSON.stringify(body)}
  const r=await fetch('/api/'+p,o);
  if(r.status===401){askPin();throw new Error('pin')}
  if(!r.ok)throw new Error(String(r.status));
  return r.json();
}
function buzz(){try{navigator.vibrate&&navigator.vibrate(12)}catch(e){}}
async function cmd(n){buzz();try{await api('cmd/'+n,{});await poll()}catch(e){}}
async function pick(id){buzz();try{await api('item/'+id,{});await poll()}catch(e){}}
async function slide(i){buzz();try{await api('slide/'+i,{});await poll()}catch(e){}}
async function poll(){
  if(busy)return;busy=true;
  try{S=await api('state');$('pill').style.opacity=1;render()}catch(e){$('pill').textContent='SEM LIGAÇÃO';$('pill').className='pill'}
  busy=false;
}
function render(){
  if(!S)return;
  const p=$('pill');
  const m=S.black?'black':S.mode;
  p.textContent=m==='live'?'EM DIRETO':m==='black'?'ECRÃ PRETO':m==='logo'?'LOGO':'LIMPO';
  p.className='pill '+(m==='live'?'live':m==='black'?'black':'');
  $('ttl').textContent=S.title||'Remoto';
  const sig=JSON.stringify([tab,S]);
  if(tab==='src'||sig===lastSig)return;
  lastSig=sig;
  const mn=$('main');const sc=mn.scrollTop;mn.textContent='';
  if(tab==='ctl')drawCtl(mn);else drawPrg(mn);
  mn.scrollTop=sc;
}
function btn(t,c,f){const b=el('button','b '+(c||''),t);b.onclick=f;return b}
function drawCtl(mn){
  const now=el('div','now',S.text||'(vazio)');
  if(S.reference)now.appendChild(el('small','',S.reference));
  mn.appendChild(now);
  const nx=S.slides[S.slideIndex+1];
  const n=el('div','nxt');n.appendChild(el('b','','Seguinte: '));n.appendChild(document.createTextNode(nx?nx.text:'(fim deste item)'));
  mn.appendChild(n);
  const r1=el('div','row');r1.appendChild(btn('◀ Anterior','big',()=>cmd('prev')));r1.appendChild(btn('Seguinte ▶','big pri',()=>cmd('next')));mn.appendChild(r1);
  const r2=el('div','row');
  r2.appendChild(btn('Preto','red'+(S.black?' on':''),()=>cmd('black')));
  r2.appendChild(btn('Limpar',(!S.black&&S.mode==='clear')?'on':'',()=>cmd('clear')));
  r2.appendChild(btn('Logo',(!S.black&&S.mode==='logo')?'on':'',()=>cmd('logo')));
  r2.appendChild(btn('Live',(!S.black&&S.mode==='live')?'on':'',()=>cmd('live')));
  mn.appendChild(r2);
  if(S.slides.length){mn.appendChild(el('h2','','Slides'));
    S.slides.forEach(s=>{const d=el('div','it sl'+(s.i===S.slideIndex?' act':''));d.appendChild(el('b','',s.header||('Slide '+(s.i+1))));d.appendChild(el('small','',s.text));d.onclick=()=>slide(s.i);mn.appendChild(d)})}
}
function drawPrg(mn){
  if(!S.schedule.length){mn.appendChild(el('div','empty','A programação está vazia. Usa o separador Procurar para adicionar músicas.'));return}
  S.schedule.forEach(it=>{
    if(it.folder||it.separator){mn.appendChild(el('div','it fold',(it.icon||'')+' '+it.title))}
    else mn.appendChild(row(it,false));
    (it.subs||[]).forEach(s=>mn.appendChild(row(s,true)));
  });
}
function row(it,sub){
  const d=el('div','it'+(sub?' sub':'')+(it.active?' act':''));
  d.appendChild(el('b','',(it.icon?it.icon+' ':'')+it.title));
  if(it.subtitle)d.appendChild(el('small','',it.subtitle));
  d.onclick=()=>pick(it.id);return d;
}
/* ---- Procurar ---- */
let vers=[],tmr=null;
function drawSrc(){
  const mn=$('main');mn.textContent='';
  const seg=el('div','seg');
  [['songs','Músicas'],['bible','Bíblia']].forEach(([k,t])=>{const b=el('button',sMode===k?'on':'',t);b.onclick=()=>{sMode=k;sRes=[];drawSrc()};seg.appendChild(b)});
  mn.appendChild(seg);
  if(sMode==='bible'){const s=el('select');s.id='ver';vers.forEach(v=>{const o=el('option','',v);o.value=v;s.appendChild(o)});if(window._ver)s.value=window._ver;s.onchange=()=>{window._ver=s.value;doSearch()};mn.appendChild(s)}
  const i=el('input');i.type='search';i.id='q';i.placeholder=sMode==='songs'?'Título da música…':'Ex.: Jo 3:16 ou Salmos 23';i.autocomplete='off';
  i.value=window._q||'';i.oninput=()=>{window._q=i.value;clearTimeout(tmr);tmr=setTimeout(doSearch,350)};mn.appendChild(i);
  if(sMode==='songs'){const l=el('label','ck');const c=el('input');c.type='checkbox';c.id='lyr';c.checked=!!window._lyr;c.onchange=()=>{window._lyr=c.checked;doSearch()};l.appendChild(c);l.appendChild(document.createTextNode('Procurar também na letra'));mn.appendChild(l)}
  const out=el('div');out.id='out';mn.appendChild(out);showRes();
}
function showRes(){
  const out=$('out');if(!out)return;out.textContent='';
  if(!sRes.length){out.appendChild(el('div','empty',(window._q||'').trim()?'Sem resultados.':'Escreve para procurar.'));return}
  if(sMode==='bible'&&sRes.length>1){const r=el('div','row');r.appendChild(btn('+ Adicionar todos','',()=>addBible(0,sRes.length-1,false)));r.appendChild(btn('▶ Projetar todos','pri',()=>addBible(0,sRes.length-1,true)));out.appendChild(r)}
  sRes.forEach((x,k)=>{
    const r=el('div','res');const d=el('div','it');
    if(sMode==='songs'){d.appendChild(el('b','',x.title));if(x.artist)d.appendChild(el('small','',x.artist))}
    else{d.appendChild(el('b','',x.ref));d.appendChild(el('small','',x.text))}
    const a=el('button','','+');a.onclick=()=>sMode==='songs'?addSong(x.id,false):addBible(k,k,false);
    const p=el('button','p','▶');p.onclick=()=>sMode==='songs'?addSong(x.id,true):addBible(k,k,true);
    r.appendChild(d);r.appendChild(a);r.appendChild(p);out.appendChild(r);
  });
}
async function doSearch(){
  const q=(window._q||'').trim();
  if(!q){sRes=[];showRes();return}
  try{
    if(sMode==='songs')sRes=await api('songs?q='+encodeURIComponent(q)+(window._lyr?'&lyrics=1':''));
    else sRes=await api('bible?q='+encodeURIComponent(q)+'&v='+encodeURIComponent(window._ver||($('ver')&&$('ver').value)||''));
  }catch(e){sRes=[]}
  showRes();
}
async function addSong(id,project){buzz();try{await api('songs/add',{id,project});toast(project?'A projetar ✓':'Adicionado ✓');lastSig='';poll()}catch(e){toast('Erro')}}
async function addBible(from,to,project){buzz();try{await api('bible/add',{q:(window._q||'').trim(),v:window._ver||$('ver').value,from,to,project});toast(project?'A projetar ✓':'Adicionado ✓');lastSig='';poll()}catch(e){toast('Erro')}}
document.querySelectorAll('nav button').forEach(b=>b.onclick=()=>{
  tab=b.dataset.t;document.querySelectorAll('nav button').forEach(x=>x.classList.toggle('on',x===b));lastSig='';
  if(tab==='src')drawSrc();else render();
});
(async()=>{try{vers=await api('bible/versions');if(vers.length&&!window._ver)window._ver=vers[0]}catch(e){}})();
poll();setInterval(()=>{if(!document.hidden)poll()},1000);
document.addEventListener('visibilitychange',()=>{if(!document.hidden)poll()});
</script></body></html>
""";
}
