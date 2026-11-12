const fs=require('fs'),d=process.argv[2];
const svg=id=>fs.readFileSync(`${d}/${id}.svg`,'utf8');
const robot=(h,t,al,ar,l,core)=>`<div class="r">
<div style="left:80px;top:280px;width:200px;height:140px">${svg(l)}</div>
<div style="left:0;top:110px;width:90px;height:210px">${svg(al)}</div>
<div style="left:90px;top:110px;width:180px;height:165px">${svg(t)}</div>
<div style="left:270px;top:110px;width:90px;height:210px;transform:scaleX(-1)">${svg(ar)}</div>
<div style="left:120px;top:10px;width:120px;height:100px">${svg(h)}</div>
<div class="core" style="background:${core}"></div></div>`;
const all=fs.readdirSync(d).filter(f=>f.endsWith('.svg')).map(f=>f.slice(0,-4));
fs.writeFileSync(process.argv[3],`<html><body style="background:#0B0D14;margin:0;padding:20px;display:flex;flex-wrap:wrap;gap:30px;width:1600px">
<style>.r{position:relative;width:360px;height:440px}.r>div{position:absolute}.r svg{width:100%;height:100%;display:block}.core{left:159px;top:174px;width:42px;height:42px;border-radius:50%;box-shadow:0 0 20px 8px currentColor}.p{background:#141826;padding:6px}.p svg{display:block}</style>
${robot('otica_sucata','carcaca_remendada','punho_hidraulico','serra_circular','rodas_entrega','#FFE066')}
${robot('elmo_bastion','placa_m2','canhao_30mm','escudo_braco','esteiras','#FFE066')}
${robot('sensor_k7','chassi_aero','lamina_plasma','pulso_tesla','pernas_digitigradas','#FF5A3D')}
${robot('coroa_nyx','torax_refrigerado','injetor','lanca_chamas','hover','#FFE066')}
<div style="display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end">${all.map(i=>'<div class="p">'+svg(i)+'</div>').join('')}</div></body></html>`);
