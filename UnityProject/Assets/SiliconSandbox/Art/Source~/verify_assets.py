"""Independent literal dimensions and exported FBX structure checks; no Unity runtime claim."""
import bpy, pathlib, json, math, hashlib
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[1]
expected={
'SS_SourceBody':(44,[-.39]*3,[.39]*3),'SS_AndBody':(44,[-.39]*3,[.39]*3),'SS_SrBody':(44,[-.39]*3,[.39]*3),'SS_ModuleBody':(44,[-.41]*3,[.41]*3),
'SS_Pin':(44,[-.1,-.14,-.1],[.1,.0625,.1]),'SS_WireStraight':(44,[-.125,-.5,-.125],[.125,.5,.125]),
'SS_WireElbow':(68,[-.5,-.125,-.125],[.125,.125,.5]),'SS_IdentityRing':(96,[-.128,-.018,-.128],[.128,.018,.128]),
'SS_Junction':(80,None,None)}
for s in '01XZ':expected['SS_SourceValue_'+s]=(2,[-.37,.3905,-.37],[.37,.3905,.37])
results=[]
for name,(tri,lo,hi) in expected.items():

 for existing in list(bpy.data.objects):bpy.data.objects.remove(existing,do_unlink=True)
 bpy.ops.import_scene.fbx(filepath=str(ROOT/'Meshes'/f'{name}.fbx'))
 obs=list(bpy.context.scene.objects);assert len(obs)==1 and obs[0].type=='MESH',(name,'extra objects')
 o=obs[0];m=o.data;assert len(m.polygons)==tri,(name,'triangles');assert len(m.materials)==1,(name,'materials')
 points=[]
 for v in m.vertices:
  p=o.matrix_world@v.co;points.append((p.x,p.z,-p.y))
 bounds=[[min(v[k] for v in points) for k in range(3)],[max(v[k] for v in points) for k in range(3)]]
 if lo:
  assert all(abs(bounds[j][k]-[lo,hi][j][k])<1e-5 for j in range(2) for k in range(3)),(name,bounds)
 assert all(len(p.vertices)==3 and p.area>1e-9 for p in m.polygons),(name,'degenerate')
 assert len(m.uv_layers)==1,(name,'missing UV')
 assert all(math.isfinite(v) and 0<=v<=1 for d in m.uv_layers.active.data for v in d.uv),(name,'UV range')
 # Signed volume catches flipped normals on all closed assets; plates intentionally open.
 vol=sum(m.vertices[p.vertices[0]].co.dot(m.vertices[p.vertices[1]].co.cross(m.vertices[p.vertices[2]].co))/6 for p in m.polygons)
 assert name.startswith('SS_SourceValue') or vol>0,(name,'inside out',vol)
 edges={}
 for p in m.polygons:
  for e in p.edge_keys:edges[e]=edges.get(e,0)+1
 if not name.startswith('SS_SourceValue'):assert all(n==2 for n in edges.values()),(name,'nonmanifold')
 results.append({'name':name,'result':'PASS','triangles':tri,'exportedBoundsUnity':bounds,'materialSlots':1,'signedVolume':vol})
# Readable original references remained byte-identical, including their FBX counterparts.
repo=ROOT.parents[3]
refs=json.loads((ROOT/'Review~/reference-inspection.json').read_text())
for item in refs:assert hashlib.sha256((repo/item['file']).read_bytes()).hexdigest()==item['sha256'],item['file']
image=bpy.data.images.load(str(ROOT/'Textures/SS_SymbolAtlas_64.png'));assert tuple(image.size)==(64,64)
report={'result':'PASS','checks':['13 FBX round trips','literal cell dimensions and pin origin','triangle counts','one material slot each','finite in-range UVs','nondegenerate triangles','outward closed meshes and manifold edge incidence','64x64 atlas','8 inspected reference file SHA-256 hashes unchanged'],'assets':results,'notTested':['Unity import axis/scale/material mapping','Unity interaction and runtime signal update','Windows/macOS player performance','X pulse animation (owned by renderer)']}
(ROOT/'Review~/verification.json').write_text(json.dumps(report,indent=2));print('VERIFY_PASS: 13 FBX meshes, atlas, 8 unchanged references')
