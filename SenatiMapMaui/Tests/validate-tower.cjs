const fs=require('node:fs'),zlib=require('node:zlib'),assert=require('node:assert/strict');
const html=fs.readFileSync('SenatiMapMaui/Resources/Raw/wwwroot/index.html','utf8');
const start=html.indexOf('/* Torre: local model,'),end=html.indexOf('/* Pirámide: 3 pisos',start);
assert(start>=0&&end>start);
const elements=new Map();
const element=id=>{if(!elements.has(id))elements.set(id,{textContent:'',classList:{toggle(){}},click(){}});return elements.get(id);};
const document={getElementById:element,querySelector:element,querySelectorAll:()=>[]};
let loaded=null;
class Viewer{load(bytes){loaded=Buffer.from(bytes);this.ready=true;}draw(){}}
const window={location:{href:''},addEventListener(){},SenatiPyramidGLB:{Viewer,fromBase64:s=>Buffer.from(s,'base64')}};
const api=new Function('document','window','requestAnimationFrame',
  'const normalizeSearch=v=>String(v||"").normalize("NFD").replace(/[\\u0300-\\u036f]/g,"").trim().toLowerCase();'+
  html.slice(start,end)+'return {showTowerPage,receiveTowerChunk,isTowerName,towerModelFailed};'
)(document,window,cb=>cb());
for(const name of ['Torre','Torres','Centro Tecnológico'])assert(api.isTowerName(name));
assert(!api.isTowerName('Pirámide'));
api.showTowerPage();assert(window.location.href.startsWith('senatimaui://tower/model'));
const raw=zlib.gunzipSync(fs.readFileSync('SenatiMapMaui/Resources/Raw/wwwroot/torre.glb.gz'));
const b64=raw.toString('base64'),size=131072;
for(let i=0;i<b64.length;i+=size)api.receiveTowerChunk(b64.slice(i,i+size),i===0,i+size>=b64.length);
assert.deepEqual(loaded,raw);assert.equal(element('towerStatus').textContent,'');
loaded=null;api.showTowerPage();assert.equal(loaded,null,'Reopening must reuse the loaded model');
api.towerModelFailed('test');assert(element('towerStatus').textContent.includes('test'));
console.log('Tower opening, aliases, chunk transfer, cache and error state OK');
