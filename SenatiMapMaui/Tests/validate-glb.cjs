const fs=require('fs'),vm=require('vm');
const calls=[];
const targets=new Map();
let elementBuffer=null;
let serial=0;
const gl=new Proxy({ARRAY_BUFFER:34962,ELEMENT_ARRAY_BUFFER:34963,
bindBuffer:(target,buffer)=>{
  if(targets.has(buffer)&&targets.get(buffer)!==target)throw Error('WebGL INVALID_OPERATION: buffer target changed');
  targets.set(buffer,target);
  if(target===34963)elementBuffer=buffer;
},COMPILE_STATUS:35713,LINK_STATUS:35714,TRIANGLES:4,
getShaderParameter:()=>true,getProgramParameter:()=>true,getAttribLocation:()=>0,
getUniformLocation:()=>1,createBuffer:()=>++serial,
drawElements:(mode,count,type,offset)=>{
  if(!elementBuffer)throw Error('Missing element buffer');
  calls.push({mode,count,type,offset});
},
drawArrays:(mode,start,count)=>calls.push({mode,count})},
{get:(o,k)=>k in o?o[k]:(...args)=>{if(String(k).startsWith('create'))return ++serial;}});
const context={window:{devicePixelRatio:1},TextDecoder,Float32Array,Uint8Array,DataView,Map,Math,Number};
vm.createContext(context);
vm.runInContext(fs.readFileSync(process.argv[2]||'SenatiMapMaui/Resources/Raw/wwwroot/pyramid-glb-viewer.js','utf8'),context);
const canvas={clientWidth:400,clientHeight:600,getContext:()=>gl,addEventListener(){}};
const viewer=new context.window.SenatiPyramidGLB.Viewer(canvas);
const data=require('zlib').gunzipSync(fs.readFileSync('SenatiMapMaui/Resources/Raw/wwwroot/piramide-piso1.glb.gz'));
const count=viewer.load(data.buffer.slice(data.byteOffset,data.byteOffset+data.byteLength));
if(count!==61||calls.length!==61||!calls.every(c=>c.count>0))throw Error('Draw regression');
if(!viewer.center.every(Number.isFinite)||!Number.isFinite(viewer.radius))throw Error('Bad bounds');
viewer.changeYaw(.2); viewer.changeZoom(1.1); viewer.reset();
console.log(JSON.stringify({meshes:count,drawCalls:calls.length,center:viewer.center,radius:viewer.radius,controls:'OK'}));

