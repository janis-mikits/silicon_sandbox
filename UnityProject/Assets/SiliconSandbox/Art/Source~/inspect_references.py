import bpy, json, pathlib, hashlib
from mathutils import Vector
root=pathlib.Path.cwd(); out=root/'UnityProject/Assets/SiliconSandbox/Art/Review~'
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
names=['current_source_on','AND_gate','wire_straight_power_off','wire_bent_power_off']
report=[]
for i,name in enumerate(names):
 p=root/'art/reference-models'/f'{name}.blend'
 with bpy.data.libraries.load(str(p),link=False) as (src,dst): dst.objects=src.objects
 obs=[o for o in dst.objects if o and o.type=='MESH']
 points=[]
 for o in obs:
  bpy.context.collection.objects.link(o)
  points += [o.matrix_world@Vector(v) for v in o.bound_box]
 lo=Vector([min(v[k] for v in points) for k in range(3)]); hi=Vector([max(v[k] for v in points) for k in range(3)])
 report.append({'file':str(p.relative_to(root)), 'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'objects':[{'name':o.name,'vertices':len(o.data.vertices),'polygons':len(o.data.polygons)} for o in obs], 'bounds':[list(lo),list(hi)]})
 scale=1.5/max(hi-lo); center=(lo+hi)/2
 for o in obs:
  m=o.matrix_world.copy(); m.translation=(m.translation-center)*scale+Vector((i*2.3,0,0.8)); o.matrix_world=m; o.scale*=scale
  o.data.materials.clear()
 for ext in ['fbx']:
  f=root/'art/reference-models'/f'{name}.{ext}'
  before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=str(f)); imported=set(bpy.data.objects)-before
  report.append({'file':str(f.relative_to(root)),'sha256':hashlib.sha256(f.read_bytes()).hexdigest(),'objects':[{'name':o.name,'type':o.type} for o in imported]})
  for o in imported: bpy.data.objects.remove(o,do_unlink=True)
(out/'reference-inspection.json').write_text(json.dumps(report,indent=2))
mat=bpy.data.materials.new('Neutral inspection'); mat.diffuse_color=(.45,.5,.55,1)
for o in bpy.context.scene.objects:
 if o.type=='MESH': o.data.materials.append(mat)
bpy.ops.object.camera_add(location=(8,-10,9)); cam=bpy.context.object; cam.rotation_euler=(Vector((3.45,0,.6))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO';cam.data.ortho_scale=10; bpy.context.scene.camera=cam
bpy.ops.object.light_add(type='AREA',location=(3,-4,8)); bpy.context.object.data.energy=1800; bpy.context.object.data.shape='DISK'; bpy.context.object.data.size=7
sc=bpy.context.scene;sc.render.engine='CYCLES';sc.cycles.samples=24;sc.render.resolution_x=1500;sc.render.resolution_y=600;sc.render.resolution_percentage=100;sc.world.color=(.25,.25,.25);sc.render.filepath=str(out/'reference-shapes.png');bpy.ops.render.render(write_still=True)
