"""Original SiliconSandbox mesh/atlas source. Run with Blender --background --factory-startup."""
import bpy, math, json, pathlib, hashlib
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[1]
for p in ['Meshes','Textures','Review~']: (ROOT/p).mkdir(exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials): bpy.data.materials.remove(m)
# Unity (east,up,north) -> Blender (right,back,up); FBX exports Y-up/-Z-forward.
def B(p): return (p[0],-p[2],p[1])
def U(p): return (p[0],p[2],-p[1])
FONT={'0':['111','101','101','101','111'],'1':['010','110','010','010','111'],'X':['101','101','010','101','101'],'Z':['111','001','010','100','111'],'A':['010','101','111','101','101'],'N':['101','111','111','111','101'],'D':['110','101','101','101','110'],'S':['111','100','111','001','111'],'R':['110','101','110','101','101']}
N=64; pix=[0.0]*(N*N*4)
C={'shell':(.43,.48,.52,1),'edge':(.29,.34,.38,1),'panel':(.065,.09,.12,1),'rim':(.19,.25,.29,1),'ink':(.9,.94,.92,1)}
def pixel(tile,x,y,col):
 tx=(tile%4)*16;ty=(tile//4)*16;i=((ty+y)*N+tx+x)*4;pix[i:i+4]=col
for t in range(16):
 for y in range(16):
  for x in range(16): pixel(t,x,y,C['shell'] if t==0 else C['edge'] if t==1 else C['panel'])
for t in range(2,9):
 for y in range(16):
  for x in range(16):
   if x in (1,14) or y in (1,14):pixel(t,x,y,C['rim'])
def word(tile,s,scale=1):
 w=(len(s)*4-1)*scale;x0=(16-w)//2;y0=(16-5*scale)//2
 for k,ch in enumerate(s):
  for row,line in enumerate(FONT[ch]):
   for col,on in enumerate(line):
    if on=='1':
     for dx in range(scale):
      for dy in range(scale):pixel(tile,x0+(k*4+col)*scale+dx,y0+(4-row)*scale+dy,C['ink'])
for i,s in enumerate(['0','1','X','Z']):word(i+2,s,2)
word(6,'AND');word(7,'SR')
# Empty module-name field with corner registration details, no baked placeholder text.
for x in (3,12):
 for y in (3,12):pixel(8,x,y,C['ink'])
img=bpy.data.images.new('SS_SymbolAtlas_64',width=N,height=N,alpha=False);img.pixels=pix;img.filepath_raw=str(ROOT/'Textures/SS_SymbolAtlas_64.png');img.file_format='PNG';img.save()
def material(name,color):
 m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*color,1);p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.72;return m
atlas=material('SS_Atlas',(.43,.48,.52));tex=atlas.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img;tex.interpolation='Closest';atlas.node_tree.links.new(tex.outputs['Color'],atlas.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
tint=material('SS_Tint',(1,1,1))
assets=[]
def uv_tile(o,t,top=False):
 if not o.data.uv_layers:o.data.uv_layers.new()
 for poly in o.data.polygons:
  tile=t if (not top or poly.normal.z>.99) else 0 if len(poly.vertices)==4 else 1
  tx=tile%4;ty=tile//4
  for li in poly.loop_indices:
   v=o.data.vertices[o.data.loops[li].vertex_index].co
   if top and poly.normal.z>.99:
    h=max(abs(v.x),abs(v.y),.01); # full top flat face width derived below
    bounds=max(abs(q.co.x) for q in o.data.vertices if abs(q.co.z-v.z)<1e-5)
    u=.5+v.x/(2*bounds);w=.5+v.y/(2*bounds)
   else:u=w=.5
   o.data.uv_layers.active.data[li].uv=((tx+(0.5+u*15)/16)/4,(ty+(0.5+w*15)/16)/4)
def finish(o,name,mat):
 o.name=name;o.data.name=name;o.data.materials.clear();o.data.materials.append(mat)
 bpy.context.view_layer.objects.active=o;o.select_set(True)
 mod=o.modifiers.new('Explicit triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
 for p in o.data.polygons:p.use_smooth=False
 assets.append(o);return o
def body(name,size,tile):
 bpy.ops.mesh.primitive_cube_add(size=size);o=bpy.context.object
 mod=o.modifiers.new('Small readable edge','BEVEL');mod.width=.02;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
 uv_tile(o,tile,True);return finish(o,name,atlas)
body('SS_SourceBody',.78,9);body('SS_AndBody',.78,6);body('SS_SrBody',.78,7);body('SS_ModuleBody',.82,8)
for i,s in enumerate(['0','1','X','Z']):
 # 0.0005 cell above the top face; no hover gap, opaque panel.
 v=[(-.37,-.37,.3905),(.37,-.37,.3905),(.37,.37,.3905),(-.37,.37,.3905)]
 mesh=bpy.data.meshes.new('Value');mesh.from_pydata(v,[],[(0,1,2,3)]);o=bpy.data.objects.new('Value',mesh);bpy.context.collection.objects.link(o)
 uv=mesh.uv_layers.new();tile=i+2
 for li,(u,w) in enumerate([(0,0),(1,0),(1,1),(0,1)]):uv.data[li].uv=((tile%4+(0.5+u*15)/16)/4,(tile//4+(0.5+w*15)/16)/4)
 finish(o,'SS_SourceValue_'+s,atlas)
def cylinder(name,radius,length,center):
 bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=radius,depth=length,location=(0,0,0));o=bpy.context.object
 for v in o.data.vertices:v.co.z+=center
 uv_tile(o,0);return finish(o,name,tint)
# Origin at exact pin face point; extends inward to meet the existing .78 cell body.
cylinder('SS_Pin',.1,.2025,-.03875) # y=-.14 .. +.0625
cylinder('SS_WireStraight',.125,1,0) # y=-.5 .. +.5
# Joined 90-degree miter elbow: endpoints (-.5,0,0), (0,0,.5), center (0,0,0).
verts=[]
for cx,cy,cz,kind in [(-.5,0,0,0),(0,0,0,1),(0,0,.5,2)]:
 for k in range(12):
  a=2*math.pi*k/12;v=.125*math.cos(a);w=.125*math.sin(a)
  p=(cx,cy+v,cz+w) if kind==0 else (-w,v,w) if kind==1 else (cx-w,cy+v,cz)
  verts.append(B(p))
faces=[]
for j in range(2):
 for k in range(12):a=j*12+k;b=j*12+(k+1)%12;faces.append((a,b,b+12,a+12))
faces += [tuple(range(11,-1,-1)),tuple(range(24,36))]
me=bpy.data.meshes.new('Elbow');me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new('Elbow',me);bpy.context.collection.objects.link(o)
# Normalize normals outward independent of path ring convention.
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT');uv_tile(o,0);finish(o,'SS_WireElbow',tint)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=.17);o=bpy.context.object;uv_tile(o,0);finish(o,'SS_Junction',tint)
# Narrow ring, independent identity color. At straight center, slide along local Y.
verts=[];faces=[]
for z,r in [(-.018,.128),(.018,.128),(-.018,.124),(.018,.124)]:
 for k in range(12):a=2*math.pi*k/12;verts.append((r*math.cos(a),r*math.sin(a),z))
for k in range(12):
 n=(k+1)%12;faces += [(k,n,12+n,12+k),(24+n,24+k,36+k,36+n),(n,k,24+k,24+n),(12+k,12+n,36+n,36+k)]
me=bpy.data.meshes.new('IdentityRing');me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new('IdentityRing',me);bpy.context.collection.objects.link(o);uv_tile(o,0);finish(o,'SS_IdentityRing',tint)
# Save/export only reusable meshes; no colliders, rig, camera, baked pin positions or runtime logic.
manifest=[]
for o in assets:
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 bpy.ops.export_scene.fbx(filepath=str(ROOT/'Meshes'/f'{o.name}.fbx'),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
 points=[U(v.co) for v in o.data.vertices]
 manifest.append({'name':o.name,'triangles':len(o.data.polygons),'vertices':len(o.data.vertices),'material':o.data.materials[0].name,'boundsUnity':[ [round(min(p[k] for p in points),6) for k in range(3)],[round(max(p[k] for p in points),6) for k in range(3)] ]})
(ROOT/'asset-manifest.json').write_text(json.dumps({'units':'one cell = one Unity unit','assets':manifest,'atlas':{'size':[64,64],'tile':[16,16],'origin':'bottom-left','columns':4,'tiles':{'0':'shell','1':'edge','2':'0','3':'1','4':'X','5':'Z','6':'AND','7':'SR','8':'blank module panel','9':'blank source panel'}}},indent=2))
img.filepath='//../Textures/SS_SymbolAtlas_64.png'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source~/SiliconSandbox_Assets.blend'))
# Presentation-only gallery, separate file. Original asset meshes remain untouched.
for o in assets:o.hide_render=True;o.hide_set(True)
lookup={o.name:o for o in assets}
def clone(name,pos,mat=None,quat=None,scale=None):
 src=lookup[name];o=bpy.data.objects.new(name+'_review',src.data.copy() if mat else src.data);bpy.context.collection.objects.link(o);o.location=B(pos)
 if mat:o.data.materials.clear();o.data.materials.append(mat)
 if quat:o.rotation_mode='QUATERNION';o.rotation_quaternion=quat
 if scale:o.scale=scale
 return o
sig={s:material('Review_'+s,c) for s,c in {'0':(.2,.4,.7),'1':(.1,.95,.2),'X':(.8,.025,.025),'Z':(.5,.5,.5)}.items()}
# Aim cylinder's Blender +Z toward a Unity direction.
def aim(d):return Vector(B(d)).to_track_quat('Z','Y')
for i,(kind,val) in enumerate([('Source','0'),('Source','1'),('Source','X'),('Source','Z'),('And',None),('Sr',None),('Module',None)]):
 x=(i%4)*1.65;y=.53;z=0 if i<4 else 2
 clone('SS_'+kind+'Body',(x,y,z))
 if val:clone('SS_SourceValue_'+val,(x,y,z))
 pins= [( .5,-.25,-.25,(1,0,0))] if kind=='Source' else [(-.5,-.25,-.25,(-1,0,0)),(-.5,.25,-.25,(-1,0,0)),(.5,-.25,-.25,(1,0,0))]
 if kind=='Sr':pins += [(-.25,-.25,-.5,(0,0,-1)),(.5,.25,-.25,(1,0,0))]
 if kind=='Module':pins=pins[:1]+pins[2:]
 for px,py,pz,d in pins:clone('SS_Pin',(x+px,y+py,z+pz),sig[val or 'Z'],aim(d))
# Foreground topology strip: straight, elbow, T and separate crossing.
z=4.1;h=.3
clone('SS_WireStraight',(0,h,z),sig['0'],aim((1,0,0)));clone('SS_IdentityRing',(-.32,h,z),sig['Z'],aim((1,0,0)))
clone('SS_WireElbow',(1.65,h,z),sig['1'])
clone('SS_WireStraight',(3.3,h,z),sig['X'],aim((1,0,0)))
clone('SS_WireStraight',(3.3,h,z+.25),sig['X'],aim((0,0,1)),(1,1,.5));clone('SS_Junction',(3.3,h,z),sig['X'])
clone('SS_WireStraight',(4.95,h,z),sig['0'],aim((1,0,0)))
clone('SS_WireStraight',(4.95,h+.32,z),sig['Z'],aim((0,0,1)))
# Small typographic captions are review only, never exported game meshes.
caption=material('Review caption',(.5,.61,.68))
def text(s,loc,size):
 c=bpy.data.curves.new('Review caption','FONT');c.body=s;c.align_x='CENTER';c.size=size;c.extrude=0;o=bpy.data.objects.new('Review caption',c);bpy.context.collection.objects.link(o);o.location=B(loc);c.materials.append(caption)
for x,s in [(0,'SOURCE / 0'),(1.65,'SOURCE / 1'),(3.3,'SOURCE / X'),(4.95,'SOURCE / Z')]:text(s,(x,.008,.75),.12)
for x,s in [(0,'AND'),(1.65,'SR'),(3.3,'MODULE / BLANK')]:text(s,(x,.008,2.76),.12)
for x,s in [(0,'STRAIGHT'),(1.65,'ELBOW'),(3.3,'JOINED'),(4.95,'CROSSING / SEPARATE')]:text(s,(x,.008,4.85),.11)
text('S I L I C O N S A N D B O X',(2.45,.008,-1.15),.24)
text('FIRST PLAYABLE  /  REUSABLE VISUAL ASSETS',(2.45,.008,-.84),.1)
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.data.materials.append(material('Review floor',(.035,.047,.057)))
bpy.ops.object.camera_add(location=B((8,10,12)));cam=bpy.context.object;cam.rotation_euler=(Vector(B((2.45,.1,1.85)))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=9.6;bpy.context.scene.camera=cam
for loc,power,size in [((0,7,-3),1400,7),((6,4,3),1100,5)]:
 bpy.ops.object.light_add(type='AREA',location=B(loc));l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(-l.location).to_track_quat('-Z','Y').to_euler()
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=24;sc.world.color=(.2,.2,.2);sc.render.resolution_x=1600;sc.render.resolution_y=1300;sc.render.resolution_percentage=100;sc.view_settings.view_transform='AgX';sc.render.filepath=str(ROOT/'Review~/asset-overview.png');bpy.ops.render.render(write_still=True)
# True top view makes the surface symbols reviewable without perspective distortion.
cam.location=B((2.45,12,1.85));cam.rotation_euler=(Vector(B((2.45,0,1.85)))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=8.2;sc.render.filepath=str(ROOT/'Review~/asset-top.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source~/SiliconSandbox_Review.blend'))
print('ASSET_BUILD_OK',len(assets),'meshes')
