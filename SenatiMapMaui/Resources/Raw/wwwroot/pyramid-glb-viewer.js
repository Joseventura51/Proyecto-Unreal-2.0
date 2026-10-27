/* SENATI Map - embedded, dependency-free WebGL glTF 2.0 viewer (first floor). */
(function(){
'use strict';
function ident(){return new Float32Array([1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1]);}
function mul(a,b){
  const r=new Float32Array(16);
  for(let c=0;c<4;c++)for(let row=0;row<4;row++){
    let sum=0;
    for(let k=0;k<4;k++)sum+=a[k*4+row]*b[c*4+k];
    r[c*4+row]=sum;
  }
  return r;
}
function nodeMatrix(n){
  if(Array.isArray(n.matrix))return new Float32Array(n.matrix);
  const t=n.translation||[0,0,0],s=n.scale||[1,1,1],q=n.rotation||[0,0,0,1];
  const [x,y,z,w]=q,xx=x*x,yy=y*y,zz=z*z,xy=x*y,xz=x*z,yz=y*z,wx=w*x,wy=w*y,wz=w*z;
  return new Float32Array([
    (1-2*(yy+zz))*s[0], (2*(xy+wz))*s[0], (2*(xz-wy))*s[0], 0,
    (2*(xy-wz))*s[1], (1-2*(xx+zz))*s[1], (2*(yz+wx))*s[1], 0,
    (2*(xz+wy))*s[2], (2*(yz-wx))*s[2], (1-2*(xx+yy))*s[2], 0,
    t[0],t[1],t[2],1
  ]);
}
function transform(m,p){
  return [
    m[0]*p[0]+m[4]*p[1]+m[8]*p[2]+m[12],
    m[1]*p[0]+m[5]*p[1]+m[9]*p[2]+m[13],
    m[2]*p[0]+m[6]*p[1]+m[10]*p[2]+m[14]
  ];
}
function perspective(fovy,aspect,near,far){
  const f=1/Math.tan(fovy/2),m=new Float32Array(16);
  m[0]=f/aspect;m[5]=f;m[10]=(far+near)/(near-far);
  m[11]=-1;m[14]=(2*far*near)/(near-far);
  return m;
}
function lookAt(eye,center){
  let zx=eye[0]-center[0],zy=eye[1]-center[1],zz=eye[2]-center[2];
  let d=Math.hypot(zx,zy,zz)||1;zx/=d;zy/=d;zz/=d;
  let xx=zz,xy=0,xz=-zx;d=Math.hypot(xx,xz)||1;xx/=d;xz/=d;
  const yx=zy*xz,yy=zz*xx-zx*xz,yz=-zy*xx;
  return new Float32Array([
    xx,yx,zx,0, xy,yy,zy,0, xz,yz,zz,0,
    -(xx*eye[0]+xy*eye[1]+xz*eye[2]),
    -(yx*eye[0]+yy*eye[1]+yz*eye[2]),
    -(zx*eye[0]+zy*eye[1]+zz*eye[2]),1
  ]);
}
function decodeGLB(arrayBuffer){
  if(arrayBuffer.byteLength<28)throw Error('El archivo GLB está incompleto.');
  const dv=new DataView(arrayBuffer),magic=dv.getUint32(0,true);
  if(magic!==0x46546c67||dv.getUint32(4,true)!==2)throw Error('Se esperaba glTF binario versión 2.');
  if(dv.getUint32(8,true)!==arrayBuffer.byteLength)throw Error('Tamaño GLB inconsistente.');
  let offset=12,json=null,binary=null;
  while(offset+8<=arrayBuffer.byteLength){
    const length=dv.getUint32(offset,true),kind=dv.getUint32(offset+4,true);
    offset+=8;
    if(offset+length>arrayBuffer.byteLength)throw Error('Fragmento GLB dañado.');
    if(kind===0x4e4f534a)json=JSON.parse(new TextDecoder().decode(new Uint8Array(arrayBuffer,offset,length)));
    if(kind===0x004e4942)binary=new Uint8Array(arrayBuffer,offset,length);
    offset+=length;
  }
  if(!json?.asset||!binary||!Array.isArray(json.meshes))throw Error('GLB sin geometría.');
  return{json,binary};
}
function createShader(gl,type,source){
  const shader=gl.createShader(type);
  gl.shaderSource(shader,source);gl.compileShader(shader);
  if(!gl.getShaderParameter(shader,gl.COMPILE_STATUS)){
    const error=gl.getShaderInfoLog(shader);gl.deleteShader(shader);throw Error('Shader WebGL: '+error);
  }
  return shader;
}
function createProgram(gl){
  const vertex=createShader(gl,gl.VERTEX_SHADER,[
    'attribute vec3 aPosition; attribute vec3 aNormal;',
    'uniform mat4 uMVP; uniform mat4 uModel;',
    'varying vec3 vNormal;',
    'void main(){vNormal=mat3(uModel)*aNormal;',
    'gl_Position=uMVP*vec4(aPosition,1.0);}'
  ].join('\n'));
  const fragment=createShader(gl,gl.FRAGMENT_SHADER,[
    'precision mediump float;',
    'varying vec3 vNormal; uniform vec3 uColor;',
    'void main(){',
    'vec3 lightDir=normalize(vec3(0.45,0.95,0.35));',
    'float diffuse=abs(dot(normalize(vNormal),lightDir));',
    'float intensity=0.54+0.46*diffuse;',
    'gl_FragColor=vec4(uColor*intensity,1.0);}'
  ].join('\n'));
  const program=gl.createProgram();
  gl.attachShader(program,vertex);gl.attachShader(program,fragment);gl.linkProgram(program);
  gl.deleteShader(vertex);gl.deleteShader(fragment);
  if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error('WebGL link: '+gl.getProgramInfoLog(program));
  return program;
}
const compType={5120:5120,5121:5121,5122:5122,5123:5123,5125:5125,5126:5126};
const componentLength={5120:1,5121:1,5122:2,5123:2,5125:4,5126:4};
const numComponents={SCALAR:1,VEC2:2,VEC3:3,VEC4:4};
class Viewer {
  constructor(canvas){
    this.canvas=canvas;
    this.gl=canvas.getContext('webgl',{alpha:false,antialias:true,depth:true,preserveDrawingBuffer:false});
    if(!this.gl)throw Error('WebGL no está disponible en este dispositivo.');
    this.yaw=-0.72;this.pitch=0.87;this.zoom=1;
    this.ready=false;this.meshes=[];this.views=[];this.center=[0,0,0];this.radius=25;
    this.program=createProgram(this.gl);
    this.aPos=this.gl.getAttribLocation(this.program,'aPosition');
    this.aNormal=this.gl.getAttribLocation(this.program,'aNormal');
    this.uMVP=this.gl.getUniformLocation(this.program,'uMVP');
    this.uModel=this.gl.getUniformLocation(this.program,'uModel');
    this.uColor=this.gl.getUniformLocation(this.program,'uColor');
    this.gl.enable(this.gl.DEPTH_TEST);
    this.gl.disable(this.gl.CULL_FACE);
    this.gl.clearColor(0.91,0.96,0.98,1);
    this._pointer=new Map();
    canvas.addEventListener('pointerdown',e=>{
      this._pointer.set(e.pointerId,[e.clientX,e.clientY]);
      if(canvas.setPointerCapture)canvas.setPointerCapture(e.pointerId);
    });
    canvas.addEventListener('pointermove',e=>{
      if(!this._pointer.has(e.pointerId))return;
      const prev=this._pointer.get(e.pointerId);
      this._pointer.set(e.pointerId,[e.clientX,e.clientY]);
      if(this._pointer.size===1){
        this.yaw+=(e.clientX-prev[0])*0.010;
        this.pitch=Math.max(0.12,Math.min(1.54,this.pitch+(e.clientY-prev[1])*0.008));
      }else if(this._pointer.size===2){
        const others=[...this._pointer.entries()].find(p=>p[0]!==e.pointerId);
        if(others){
          const q=others[1],before=Math.hypot(prev[0]-q[0],prev[1]-q[1]);
          const after=Math.hypot(e.clientX-q[0],e.clientY-q[1]);
          if(before>5&&after>5)this.zoom=Math.max(.55,Math.min(4.2,this.zoom*after/before));
        }
      }
      this.draw();
    });
    const clear=e=>this._pointer.delete(e.pointerId);
    canvas.addEventListener('pointerup',clear);
    canvas.addEventListener('pointercancel',clear);
    canvas.addEventListener('wheel',e=>{
      e.preventDefault();this.zoom=Math.max(.55,Math.min(4.2,this.zoom*Math.exp(-e.deltaY*.001)));
      this.draw();
    },{passive:false});
    canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();this.ready=false;});
  }
  load(arrayBuffer){
    const {json,binary}=decodeGLB(arrayBuffer);
    const gl=this.gl;
    // WebGL buffers keep the target used on their first bind. Index data must
    // never be initialized as ARRAY_BUFFER, even when uploading the same bytes.
    const indexViews=new Set(json.meshes.flatMap(m=>m.primitives)
      .filter(p=>p.indices!=null).map(p=>json.accessors[p.indices].bufferView));
    this.views=json.bufferViews.map((v,i)=>{
      const start=v.byteOffset||0,bytes=new Uint8Array(binary.buffer,binary.byteOffset+start,v.byteLength);
      const buffer=gl.createBuffer();
      const target=indexViews.has(i)?gl.ELEMENT_ARRAY_BUFFER:gl.ARRAY_BUFFER;
      gl.bindBuffer(target,buffer);gl.bufferData(target,bytes,gl.STATIC_DRAW);
      return buffer;
    });
    this.json=json;
    this.meshes=[];
    const mins=[Infinity,Infinity,Infinity],maxs=[-Infinity,-Infinity,-Infinity];
    const walk=(idx,parent)=>{
      const node=json.nodes[idx],world=mul(parent,nodeMatrix(node));
      if(node.mesh!=null){
        for(const prim of json.meshes[node.mesh].primitives){
          const index=prim.attributes?.POSITION,attr=json.accessors[index];
          if(!attr)continue;
          if(attr.min&&attr.max){
            for(let v=0;v<8;v++){
              const coord=transform(world,[
                (v&1)?attr.max[0]:attr.min[0],
                (v&2)?attr.max[1]:attr.min[1],
                (v&4)?attr.max[2]:attr.min[2]
              ]);
              for(let i=0;i<3;i++){mins[i]=Math.min(mins[i],coord[i]);maxs[i]=Math.max(maxs[i],coord[i]);}
            }
          }
          const color=json.materials?.[prim.material??-1]?.pbrMetallicRoughness?.baseColorFactor||[.8,.84,.87,1];
          this.meshes.push({prim,model:world,color});
        }
      }
      for(const child of node.children||[])walk(child,world);
    };
    for(const nodeIdx of json.scenes?.[json.scene||0]?.nodes||[])walk(nodeIdx,ident());
    if(!this.meshes.length)throw Error('El archivo no contiene superficies dibujables.');
    if(mins.every(Number.isFinite)&&maxs.every(Number.isFinite)){
      this.center=mins.map((n,i)=>(n+maxs[i])/2);
      this.radius=Math.max(4,Math.hypot(...maxs.map((n,i)=>n-mins[i]))/2);
    }
    this.ready=true;
    this.draw();
    return this.meshes.length;
  }
  changeYaw(delta){this.yaw+=delta;this.draw();}
  changeZoom(multiplier){this.zoom=Math.max(.55,Math.min(4.2,this.zoom*multiplier));this.draw();}
  reset(){this.yaw=-.72;this.pitch=.87;this.zoom=1;this.draw();}
  draw(){
    if(!this.ready)return;
    const gl=this.gl,c=this.canvas,dpr=Math.min(2,window.devicePixelRatio||1);
    const width=Math.max(1,Math.floor(c.clientWidth*dpr)),height=Math.max(1,Math.floor(c.clientHeight*dpr));
    if(width<16||height<16)return;
    if(c.width!==width||c.height!==height){c.width=width;c.height=height;}
    gl.viewport(0,0,width,height);
    gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);
    const aspect=width/height,fov=Math.PI/4.2;
    const distance=Math.max(14,this.radius*2.65/(Math.min(1,aspect)*this.zoom));
    const cp=Math.cos(this.pitch);
    const eye=[this.center[0]+distance*cp*Math.sin(this.yaw),
      this.center[1]+distance*Math.sin(this.pitch),
      this.center[2]+distance*cp*Math.cos(this.yaw)];
    const vp=mul(perspective(fov,aspect,.1,Math.max(400,distance*6)),lookAt(eye,this.center));
    gl.useProgram(this.program);
    const access=this.json.accessors,views=this.json.bufferViews;
    for(const mesh of this.meshes){
      const prim=mesh.prim,position=access[prim.attributes.POSITION],posView=views[position.bufferView];
      const idx=compType[position.componentType],count=numComponents[position.type];
      if(!idx||!count)continue;
      gl.bindBuffer(gl.ARRAY_BUFFER,this.views[position.bufferView]);
      gl.enableVertexAttribArray(this.aPos);
      gl.vertexAttribPointer(this.aPos,count,idx,!!position.normalized,
        posView.byteStride||0,position.byteOffset||0);
      const normalIdx=prim.attributes.NORMAL;
      if(normalIdx!=null&&this.aNormal>=0){
        const norm=access[normalIdx],view=views[norm.bufferView];
        gl.bindBuffer(gl.ARRAY_BUFFER,this.views[norm.bufferView]);
        gl.enableVertexAttribArray(this.aNormal);
        gl.vertexAttribPointer(this.aNormal,3,norm.componentType,!!norm.normalized,
          view.byteStride||0,norm.byteOffset||0);
      }else if(this.aNormal>=0){
        gl.disableVertexAttribArray(this.aNormal);gl.vertexAttrib3f(this.aNormal,0,1,0);
      }
      gl.uniformMatrix4fv(this.uMVP,false,mul(vp,mesh.model));
      gl.uniformMatrix4fv(this.uModel,false,mesh.model);
      gl.uniform3f(this.uColor,mesh.color[0],mesh.color[1],mesh.color[2]);
      if(prim.indices!=null){
        const accessor=access[prim.indices];
        if(!componentLength[accessor.componentType])continue;
        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,this.views[accessor.bufferView]);
        gl.drawElements(prim.mode??gl.TRIANGLES,accessor.count,accessor.componentType,
          accessor.byteOffset||0);
      }else{
        gl.drawArrays(prim.mode??gl.TRIANGLES,0,position.count);
      }
    }
  }
}
function fromBase64(data){
  const raw=atob(data),bytes=new Uint8Array(raw.length);
  for(let i=0;i<raw.length;i++)bytes[i]=raw.charCodeAt(i);
  return bytes.buffer;
}
window.SenatiPyramidGLB={Viewer,fromBase64,decodeGLB};
})();
