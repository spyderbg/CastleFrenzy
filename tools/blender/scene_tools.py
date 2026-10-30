"""Refine whole Castle Frenzy scenes and export meshes with original engine metadata.

Scene mesh mode preserves original animation/material records. Blender Actions
are editable preview copies; mesh exports retain the template's animation data.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import sys
import tempfile

import bpy
import bmesh
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from bdae_scene import SceneData, write_meshes
from bdae_reader import BdaeError
from bdae_writer import bounds
from import_bdae import Importer, engine_quaternion, matrix, sample

ROOT=Path(__file__).resolve().parents[2]


def frame_scene():
    bpy.context.view_layer.update();points=[]
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH' and not obj.hide_render:
            evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            points.extend(evaluated.matrix_world@Vector(v) for v in evaluated.bound_box)
    low=Vector(tuple(min(v[k] for v in points) for k in range(3))) if points else Vector((0,0,0))
    high=Vector(tuple(max(v[k] for v in points) for k in range(3))) if points else Vector((1,1,1))
    center=(low+high)/2;size=max((high-low).length,1)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active;space.region_3d.view_location=center;space.region_3d.view_distance=size*1.25
                space.region_3d.view_rotation=Quaternion((.88,.38,.11,.25)).normalized()
    return center,size


class SceneImporter(Importer):
    def apply_pose(self,time):
        world=self.worlds(time)
        posed={n['id']:self.convert@world[n['id']]@self.pose_offsets[n['id']] for n in self.bones}
        for n in self.bones:
            ident,parent=n['id'],n['parent']
            rest_local=self.rest[parent].inverted()@self.rest[ident] if parent else self.rest[ident]
            posed_local=posed[parent].inverted_safe()@posed[ident] if parent else posed[ident]
            self.rig.pose.bones[self.names[ident]].matrix_basis=rest_local.inverted()@posed_local
        return posed

    def make_action(self,name,start,end):
        # Write F-curves in bulk. Per-key keyframe_insert causes repeated scene
        # evaluation and is prohibitively slow for long architecture timelines.
        action=bpy.data.actions.new(name);action.use_fake_user=True;action.asset_mark()
        action['source_start_ms']=start;action['source_end_ms']=end;action['source_fps']=self.source.fps
        self.rig.animation_data_create();self.rig.animation_data.action=action
        slot=action.slots.new(id_type='OBJECT',name=self.rig.name)
        self.rig.animation_data.action_slot=slot
        layer=action.layers.new('Source motion');strip=layer.strips.new(type='KEYFRAME');bag=strip.channelbags.new(slot)
        times=sorted({start,end}|{t for track in self.source.tracks for t in track['times'] if start<=t<=end})
        frames=[1+(time-start)*self.source.fps/1000 for time in times]
        animated=[n for n in self.bones if any(1<=t['kind']<=13 for t in self.tracks.get(n['id'],[]))]
        if not hasattr(self,'pose_cache'):self.pose_cache={}
        rest_local={n['id']:(self.rest[n['parent']].inverted()@self.rest[n['id']] if n['parent'] else self.rest[n['id']]).inverted() for n in animated}
        for time in times:
            if time in self.pose_cache:continue
            world=self.worlds(time)
            posed={n['id']:self.convert@world[n['id']]@self.pose_offsets[n['id']] for n in self.bones}
            values={}
            for n in animated:
                local=posed[n['parent']].inverted_safe()@posed[n['id']] if n['parent'] else posed[n['id']]
                loc,rot,scale=(rest_local[n['id']]@local).decompose()
                values[n['id']]=(list(loc),list(rot),list(scale))
            self.pose_cache[time]=values
        def curve(path,index,values,group):
            f=bag.fcurves.new(path,index=index,group_name=group);f.keyframe_points.add(len(times))
            f.keyframe_points.foreach_set('co',[v for pair in zip(frames,values) for v in pair])
            for point in f.keyframe_points:point.interpolation='LINEAR'
            f.update()
        self.rig['source_time_ms']=start
        curve('["source_time_ms"]',0,times,'Source timeline')
        for morph in self.source.morphs:
            for index,target in enumerate(morph['targets']):
                prop='morph_'+morph['id']+'_'+str(index)
                track=next((t for t in self.source.tracks if t['kind']==14 and t['target']==morph['id'] and (t['reserved']&255)==index),None)
                values=[sample(track,max(track['times'][0],min(t,track['times'][-1])))[0] for t in times] if track else [morph['weights'][index]]*len(times)
                curve('["'+prop+'"]',0,values,'Morph targets')
        for n in animated:
            rows=[self.pose_cache[t][n['id']] for t in times]
            previous=None
            rotations=[]
            for row in rows:
                q=Quaternion(row[1])
                if previous is not None and previous.dot(q)<0:q.negate()
                previous=q.copy();rotations.append(list(q))
            for prop,k,width in [('location',0,3),('rotation_quaternion',1,4),('scale',2,3)]:
                for index in range(width):
                    values=[r[index] for r in rotations] if k==1 else [r[k][index] for r in rows]
                    curve(self.rig.pose.bones[self.names[n['id']]].path_from_id()+'.'+prop,index,values,self.names[n['id']])
        selected=[dict(time_ms=e['time_ms'],names=e['names'],frame=1+(e['time_ms']-start)*self.source.fps/1000) for e in self.source.events if start<=e['time_ms']<=end]
        action['game_events_json']=json.dumps(selected)
        for event in selected:
            for marker in event['names']:action.pose_markers.new(marker).frame=round(event['frame'])
        self.actions[action.name]=action;self.slots[action.name]=slot
        print(f'ACTION {action.name}: {len(times)} samples',flush=True)
        return dict(name=action.name,samples=len(times),start_ms=start,end_ms=end,frame_end=frames[-1],events=selected)

    def worlds(self,time=None):
        result={}
        for n in self.source.nodes:
            position=list(n['position']);scale=list(n['scale']);rotation=engine_quaternion(n['rotation'])
            if time is not None:
                for t in self.tracks.get(n['id'],[]):
                    kind=t['kind'];value=sample(t,max(t['times'][0],min(time,t['times'][-1])))
                    if kind==1:position=list(value)
                    elif kind in (2,3,4):position[kind-2]=value[0]
                    elif kind==5:rotation=value
                    elif kind==9:
                        x,y,z,w=n['rotation'];axis=Quaternion((w,x,y,z)).axis
                        rotation=Quaternion(axis,value[0]).conjugated()
                    elif kind==10:scale=list(value)
                    elif kind in (11,12,13):scale[kind-11]=value[0]
            local=Matrix.LocRotScale(Vector(position),rotation,Vector(scale))
            result[n['id']]=result[n['parent']]@local if n['parent'] else local
        return result

    def create_scene_rig(self):
        self.bones=list(self.source.nodes)
        self.joint_ids=[];self.bind_world={}
        sid={n['sid']:n['id'] for n in self.source.nodes if n['sid']}
        for skin in self.source.skins:
            for name,inv in zip(skin['joints'],skin['inverse_bind']):
                ident=sid[name];rest=matrix(inv).inverted()
                if ident in self.bind_world and max(abs(a-b) for row,other in zip(rest,self.bind_world[ident]) for a,b in zip(row,other))>0.001:
                    raise BdaeError('Conflicting inverse binds for joint '+name)
                self.bind_world[ident]=rest
                self.joint_ids.append(ident)
        initial=self.worlds()
        data=bpy.data.armatures.new(self.source.path.stem+'_Skeleton')
        self.rig=bpy.data.objects.new(self.source.path.stem+'_Rig',data)
        bpy.context.collection.objects.link(self.rig)
        bpy.context.view_layer.objects.active=self.rig;self.rig.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT');self.names={}
        for node in self.bones:
            ident=node['id'];bone=data.edit_bones.new(node['name'] or ident);bone.length=.1
            bone.matrix=self.convert@self.bind_world.get(ident,initial[ident])@self.bone_axes
            self.names[ident]=bone.name
            if node['parent']:bone.parent=data.edit_bones[self.names[node['parent']]]
        bpy.ops.object.mode_set(mode='OBJECT')
        self.rest={n['id']:data.bones[self.names[n['id']]].matrix_local.copy() for n in self.bones}
        # Edit bones have unit rest scale. Compensate non-unit inverse binds
        # (e.g. Griffin eye bones) in the posed matrices, not in mesh vertices.
        self.pose_offsets={n['id']:(self.bind_world[n['id']].inverted()@self.convert_inv@self.rest[n['id']]
                                    if n['id'] in self.bind_world else self.bone_axes) for n in self.bones}
        for n in self.bones:
            b=data.bones[self.names[n['id']]];b.use_deform=n['id'] in self.joint_ids;b['bdae_id']=n['id'];b['bdae_sid']=n['sid']
            self.rig.pose.bones[b.name].rotation_mode='QUATERNION'
        self.rig.show_in_front=True;self.rig['bdae_template']=str(self.source.path.relative_to(ROOT))
        self.rig['source_sha256']=self.source.sha256

    def create_scene_actions(self):
        self.apply_pose(0)
        if not self.source.tracks:
            self.apply_pose(0);self.action_report=[];return
        start=min(t['times'][0] for t in self.source.tracks);end=max(t['times'][-1] for t in self.source.tracks)
        self.master=self.source.path.stem+'__ALL_SOURCE_ANIMATION'
        self.action_report=[self.make_action(self.master,start,end)]
        for clip in self.source.clips:
            self.action_report.append(self.make_action(clip['name'],clip['start_ms'],clip['end_ms']))
        first=next((c['name'] for c in self.source.clips if 'walk' in c['name'].lower()),self.master)
        self.select_action(first);bpy.context.scene.frame_start=1
        bpy.context.scene.frame_end=max(2,math.ceil(self.actions[first].frame_range[1]));bpy.context.scene.frame_set(1)


def material(source,ident,cache):
    if ident in cache:return cache[ident]
    data=source.materials.get(ident,dict(name=ident,image=-1,color=(255,255,255,255),render=0))
    mat=bpy.data.materials.new(data['name'] or 'Scene material');mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.8
    shader.inputs['Base Color'].default_value=tuple(v/255 for v in data['color'])
    if 0<=data['image']<len(source.images):
        name=Path(source.images[data['image']]['file'].replace('\\','/')).name
        folder=source.path.parent.name
        png=Path(name).with_suffix('.png')
        candidates=[source.path.parent/png,ROOT/'Data/Scenes'/folder/png,
                    ROOT/'build/linux-release/assets/Scenes'/folder/png,
                    source.path.parent/name,ROOT/'Data/Scenes'/folder/name]
        image_path=next((p for p in candidates if p.is_file() and p.suffix.lower()!='.pvr'),None)
        if image_path:
            image=bpy.data.images.load(str(image_path),check_existing=True);image.pack()
            node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;node.interpolation='Linear'
            mat.node_tree.links.new(node.outputs['Color'],shader.inputs['Base Color'])
            mat.node_tree.links.new(node.outputs['Alpha'],shader.inputs['Alpha'])
            vertex=mat.node_tree.nodes.new('ShaderNodeVertexColor');vertex.layer_name='BDAE_Color'
            multiply=mat.node_tree.nodes.new('ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';multiply.inputs[0].default_value=1
            mat.node_tree.links.new(node.outputs['Color'],multiply.inputs[1]);mat.node_tree.links.new(vertex.outputs['Color'],multiply.inputs[2]);mat.node_tree.links.new(multiply.outputs[0],shader.inputs['Base Color'])
            alpha=mat.node_tree.nodes.new('ShaderNodeMath');alpha.operation='MULTIPLY'
            mat.node_tree.links.new(node.outputs['Alpha'],alpha.inputs[0]);mat.node_tree.links.new(vertex.outputs['Alpha'],alpha.inputs[1]);mat.node_tree.links.new(alpha.outputs[0],shader.inputs['Alpha'])
    mat['bdae_material_id']=ident
    cache[ident]=mat;return mat


def organic(source,geometry):
    name=geometry['name'].lower()
    return any(token in name for token in ['goblin','shaman','troll','boar','dark_elf','skeleton','boulder']) or name in ('griffin','hydra','bomb') or name.startswith('rocks')


def create_refined_meshes(importer,levels):
    source=importer.source;bindings=source.bindings();cache={};reports=[];sid={n['sid']:n['id'] for n in source.nodes if n['sid']}
    morph_ids={i for m in source.morphs for i in [m['source'],*m['targets']]}
    target_ids={i for m in source.morphs for i in m['targets']}
    for geometry in source.geometries:
        skin=next((s for s in source.skins if s['geometry']==geometry['id']),None)
        coordinate=importer.convert@matrix(skin['bind_shape']) if skin else importer.convert
        morph=next((m for m in source.morphs if m['source']==geometry['id']),None)
        link=bindings.get(skin['id'] if skin else morph['id'] if morph else geometry['id'],[None])[0]
        is_organic=organic(source,geometry) and geometry['id'] not in morph_ids
        positions=[coordinate@Vector(v) for v in geometry['buffers'][0]['positions']]
        faces=[];face_material=[]
        for i,buffer in enumerate(geometry['buffers']):
            faces.extend([buffer['indices'][j:j+3] for j in range(0,len(buffer['indices']),3)])
            face_material.extend([i]*(len(buffer['indices'])//3))
        mesh=bpy.data.meshes.new(geometry['name']+'_Smooth_Mesh');mesh.from_pydata(positions,[],faces);mesh.update()
        obj=bpy.data.objects.new(geometry['name'],mesh);bpy.context.collection.objects.link(obj)
        obj['bdae_geometry_id']=geometry['id'];obj['bdae_geometry_from_mesh']=json.dumps([v for row in coordinate.inverted() for v in row])
        obj['bdae_morph_geometry']=geometry['id'] in morph_ids
        for i,buffer in enumerate(geometry['buffers']):
            ids=link['materials'] if link else []
            material_id=ids[i] if i<len(ids) else buffer['material'].lstrip('#')
            if material_id not in source.materials and source.materials:material_id=next(iter(source.materials))
            mesh.materials.append(material(source,material_id,cache))
        for poly,index in zip(mesh.polygons,face_material):poly.material_index=index;poly.use_smooth=True
        for name,key in [('UVMap','uv'),('UVMap_1','uv1')]:
            if not any(b.get(key) for b in geometry['buffers']):continue
            layer=mesh.uv_layers.new(name=name)
            for poly in mesh.polygons:
                values=geometry['buffers'][poly.material_index].get(key)
                for loop in poly.loop_indices:
                    uv=values[mesh.loops[loop].vertex_index] if values else (0,0)
                    layer.data[loop].uv=(uv[0],1-uv[1])
        color=mesh.color_attributes.new(name='BDAE_Color',type='BYTE_COLOR',domain='CORNER')
        for poly in mesh.polygons:
            values=geometry['buffers'][poly.material_index]['colors']
            for loop in poly.loop_indices:
                value=values[mesh.loops[loop].vertex_index] if values else (255,255,255,255)
                color.data[loop].color_srgb=tuple(c/255 for c in value)
        if skin:
            groups=[obj.vertex_groups.new(name=importer.names[sid[s]]) for s in skin['joints']]
            for index,influence in enumerate(skin['influences']):
                for joint,weight in influence:groups[joint].add([index],weight,'REPLACE')
            obj['bdae_skin_id']=skin['id']
        elif link:
            bone=importer.rig.data.bones[importer.names[link['node']]]
            obj.parent=importer.rig;obj.parent_type='BONE';obj.parent_bone=bone.name
            obj.matrix_basis=Matrix.Translation((0,-bone.length,0))@importer.pose_offsets[link['node']].inverted()@importer.convert_inv
        if not is_organic and geometry['buffers'][0]['normals']:
            normal_matrix=coordinate.to_3x3().inverted().transposed()
            mesh.normals_split_custom_set_from_vertices([(normal_matrix@Vector(n)).normalized() for n in geometry['buffers'][0]['normals']])
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        if is_organic:
            bm=bmesh.new();bm.from_mesh(mesh)
            # Merge UV splits only when their skin influences agree. UVs remain per-corner.
            deform=bm.verts.layers.deform.active; buckets={}
            for vertex in bm.verts:
                weights=tuple(sorted((g,round(w,5)) for g,w in vertex[deform].items())) if deform else ()
                key=tuple(round(v,6) for v in vertex.co)+(weights,)
                buckets.setdefault(key,[]).append(vertex)
            targets={v:group[0] for group in buckets.values() for v in group[1:]}
            if targets:bmesh.ops.weld_verts(bm,targetmap=targets)
            crease=bm.edges.layers.float.get('crease_edge') or bm.edges.layers.float.new('crease_edge')
            names={g.index:g.name.lower() for g in obj.vertex_groups}
            for edge in bm.edges:
                rigid=bool(deform) and all(any(any(s in names.get(g,'') for s in ['prop','sword','weapon','bow']) and w>.8 for g,w in vertex[deform].items()) for vertex in edge.verts)
                edge[crease]=1.0 if rigid or edge.is_boundary else (.2 if edge.calc_face_angle(0)>.9 else 0)
            bm.to_mesh(mesh);bm.free();mesh.update()
        modifier=obj.modifiers.new('Applied surface refinement','SUBSURF');modifier.levels=modifier.render_levels=levels
        modifier.subdivision_type='CATMULL_CLARK' if is_organic else 'SIMPLE'
        if hasattr(modifier,'use_custom_normals'):modifier.use_custom_normals=not is_organic
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        if skin:
            modifier=obj.modifiers.new('Original skin','ARMATURE');modifier.object=importer.rig;modifier.use_deform_preserve_volume=False
        if geometry['id'] in target_ids:obj.hide_render=True;obj.hide_set(True)
        mesh=obj.data;mesh.calc_loop_triangles()
        reports.append(dict(id=geometry['id'],name=geometry['name'],method='Catmull-Clark' if is_organic else 'Simple',
                            original_vertices=geometry['vertices'],vertices=len(mesh.vertices),
                            original_triangles=sum(len(b['indices'])//3 for b in geometry['buffers']),triangles=len(mesh.loop_triangles)))
        importer.meshes.append((obj,geometry))
    return reports


def install_morphs(importer):
    objects={g['id']:obj for obj,g in importer.meshes}
    for morph in importer.source.morphs:
        obj=objects[morph['source']]
        obj.shape_key_add(name='Basis')
        for index,ident in enumerate(morph['targets']):
            target=objects[ident]
            if len(target.data.vertices)!=len(obj.data.vertices):raise BdaeError('Morph vertex correspondence differs')
            key=obj.shape_key_add(name=ident)
            for vertex,point in zip(key.data,target.data.vertices):vertex.co=point.co
            prop='morph_'+morph['id']+'_'+str(index);importer.rig[prop]=morph['weights'][index]
            driver=key.driver_add('value').driver;driver.expression='weight'
            variable=driver.variables.new();variable.name='weight';variable.type='SINGLE_PROP'
            variable.targets[0].id=importer.rig;variable.targets[0].data_path='["'+prop+'"]'


def verify_skin(importer):
    """Check every refined vertex at original sample times against native linear skinning."""
    import numpy as np
    source=importer.source
    if not source.skins:return dict(skin_samples=0,max_skin_error=0)
    sid={n['sid']:n['id'] for n in source.nodes if n['sid']};prepared=[]
    for obj,geometry in importer.meshes:
        skin=next((s for s in source.skins if s['geometry']==geometry['id']),None)
        if not skin:continue
        points=np.ones((len(obj.data.vertices),4));points[:,:3]=[v.co for v in obj.data.vertices]
        inverse=Matrix([json.loads(obj['bdae_geometry_from_mesh'])[i:i+4] for i in range(0,16,4)])
        points=points@np.array(inverse).T
        groups={obj.vertex_groups[importer.names[sid[s]]].index:i for i,s in enumerate(skin['joints'])}
        weights=np.zeros((len(points),len(skin['joints'])))
        for v in obj.data.vertices:
            for g in v.groups:
                if g.group in groups:weights[v.index,groups[g.group]]=g.weight
        weights/=weights.sum(axis=1,keepdims=True)
        prepared.append((obj,skin,points,weights))
    times=sorted({t for track in source.tracks for t in track['times']}|{c[k] for c in source.clips for k in ('start_ms','end_ms')}) or [0]
    if importer.actions:
        importer.select_action(importer.master);start=importer.action_report[0]['start_ms']
    maximum=0
    for time in times:
        if importer.actions:
            frame=1+(time-start)*source.fps/1000;bpy.context.scene.frame_set(math.floor(frame),subframe=frame-math.floor(frame))
        else:importer.apply_pose(time)
        bpy.context.view_layer.update();world=importer.worlds(time)
        for obj,skin,points,weights in prepared:
            expected=np.zeros((len(points),3))
            for j,(name,inv) in enumerate(zip(skin['joints'],skin['inverse_bind'])):
                transform=importer.convert@world[sid[name]]@matrix(inv)@matrix(skin['bind_shape'])
                expected+=(points@np.array(transform).T)[:,:3]*weights[:,j,None]
            evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh()
            actual=np.array([obj.matrix_world@v.co for v in mesh.vertices]);evaluated.to_mesh_clear()
            maximum=max(maximum,float(np.linalg.norm(expected-actual,axis=1).max()))
    if maximum>.001:raise BdaeError('Refined skin differs from native engine: '+str(maximum))
    return dict(skin_samples=len(times),max_skin_error=maximum,skin_vertices_checked=sum(len(v[2]) for v in prepared))


def create_scene(template,output,levels=2):
    source=SceneData(template);output=Path(output).resolve()
    if output.exists():raise BdaeError('Preserve existing Blender project: '+str(output))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    importer=SceneImporter(source);importer.create_scene_rig()
    bpy.context.scene.render.fps=source.fps
    meshes=create_refined_meshes(importer,levels);install_morphs(importer);importer.create_scene_actions()
    verification=verify_skin(importer)
    if importer.actions:
        importer.select_action(next((c['name'] for c in source.clips if 'walk' in c['name'].lower()),importer.master))
    bpy.context.scene.frame_set(1)
    scene=bpy.context.scene;scene['bdae_mesh_scene']=True;scene['bdae_template']=str(source.path.relative_to(ROOT))
    scene['bdae_source_sha256']=source.sha256
    report=dict(source=str(source.path),source_sha256=source.sha256,output=str(output),meshes=meshes,
                clips=source.clips,events=source.events,verification=verification,
                preserved_features=['original animations','morph controllers','materials','textures','scene nodes','cameras','splines','engine extensions'])
    text=bpy.data.texts.new('README_Game_Mesh');text.write('Applied high-resolution geometry; UVs, colors and weights retained.\n'
        'Use the rig Actions to preview motion. Source material/UV/morph animation remains in the template.\n'
        'Mesh-mode export writes edited geometry and interpolated weights while preserving ORIGINAL animation, materials and engine metadata.\n'
        'For animation authoring/export, use the Goblin full exporter profile.\n'
        'Save this file, close the game, and run rebuild_Linux.sh --resources-only.\n\n'+json.dumps(report,indent=2))
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active;space.shading.type='MATERIAL';space.overlay.show_stats=True
                space.region_3d.view_distance=5
    bpy.ops.object.select_all(action='DESELECT')
    if importer.meshes:
        chosen=max(importer.meshes,key=lambda row:len(row[0].data.vertices))[0];chosen.hide_set(False);chosen.select_set(True);bpy.context.view_layer.objects.active=chosen
    frame_scene()
    output.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output),compress=True)
    output.with_suffix('.mesh.json').write_text(json.dumps(report,indent=2)+'\n')
    print('SCENE_REFINED '+str(template)+' '+str(sum(m['vertices'] for m in meshes))+' vertices',flush=True)
    return report


def export_scene_meshes(template,output):
    source=SceneData(template);output=Path(output).resolve()
    if output==source.path:raise BdaeError('Retain the original BDAE template')
    scene=bpy.context.scene
    if scene.get('bdae_source_sha256')!=source.sha256:raise BdaeError('Scene template hash differs')
    by_id={g['id']:g for g in source.geometries};models={}
    for obj in scene.objects:
        ident=obj.get('bdae_geometry_id')
        if ident not in by_id:continue
        original=by_id[ident];skin=next((s for s in source.skins if s['geometry']==ident),None)
        disabled=[(m,m.show_viewport) for m in obj.modifiers if m.type=='ARMATURE']
        shape_state=(obj.show_only_shape_key,obj.active_shape_key_index)
        if obj.data.shape_keys:obj.show_only_shape_key=True;obj.active_shape_key_index=0
        for m,_ in disabled:m.show_viewport=False
        bpy.context.view_layer.update();evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh(preserve_all_data_layers=True,depsgraph=bpy.context.evaluated_depsgraph_get())
        try:
            morph=next((m for m in source.morphs if ident in m['targets']),None)
            if morph:
                base=next(o for o in scene.objects if o.get('bdae_geometry_id')==morph['source'])
                if base.data.shape_keys:
                    points=base.data.shape_keys.key_blocks[ident].data
                    if len(points)!=len(mesh.vertices):raise BdaeError('Edited morph topology differs')
                    for vertex,point in zip(mesh.vertices,points):vertex.co=point.co
                    mesh.update()
            mesh.calc_loop_triangles();convert=Matrix([json.loads(obj['bdae_geometry_from_mesh'])[i:i+4] for i in range(0,16,4)])
            normals=convert.to_3x3().inverted().transposed();vertices=[];buffers=[[] for _ in original['buffers']];lookup={}
            rig=next((m.object for m,_ in disabled),None)
            sid_names={b.get('bdae_sid'):b.name for b in rig.data.bones} if rig else {}
            group_map={obj.vertex_groups[sid_names[s]].index:j for j,s in enumerate(skin['joints'])} if skin else {}
            for triangle in mesh.loop_triangles:
                if triangle.material_index>=len(buffers):raise BdaeError('Material-slot count changed')
                for loop in triangle.loops:
                    index=mesh.loops[loop].vertex_index
                    uv=list(mesh.uv_layers['UVMap'].data[loop].uv) if mesh.uv_layers.get('UVMap') else [0,0];uv[1]=1-uv[1]
                    uv1=list(mesh.uv_layers['UVMap_1'].data[loop].uv) if mesh.uv_layers.get('UVMap_1') else [0,0];uv1[1]=1-uv1[1]
                    normal=(normals@(mesh.vertex_normals[index].vector if obj.get('bdae_morph_geometry') else mesh.corner_normals[loop].vector)).normalized()
                    color=mesh.color_attributes.get('BDAE_Color')
                    rgba=[max(0,min(255,round(c*255))) for c in color.data[loop].color_srgb] if color else [255]*4
                    key=(index,) if obj.get('bdae_morph_geometry') else (index,*[round(v,6) for v in (*normal,*uv,*uv1)],*rgba)
                    if key not in lookup:
                        weights=[(group_map[g.group],g.weight) for g in mesh.vertices[index].groups if g.group in group_map and g.weight>1e-8]
                        if skin:
                            total=sum(v for _,v in weights)
                            if not total:raise BdaeError('Unweighted refined vertex')
                            weights=[(j,w/total) for j,w in weights]
                        lookup[key]=len(vertices);vertices.append(dict(position=list(convert@mesh.vertices[index].co),normal=list(normal),uv=uv,uv1=uv1,color=rgba,weights=weights))
                    buffers[triangle.material_index].append(lookup[key])
            model=dict(vertices=vertices,buffers=buffers)
            if skin:
                joint_bounds=[]
                for j,inv in enumerate(skin['inverse_bind']):
                    transform=matrix(inv)@matrix(skin['bind_shape'])
                    joint_bounds.append(bounds([transform@Vector(v['position']) for v in vertices if any(i==j for i,_ in v['weights'])]))
                model['joint_bounds']=joint_bounds
            models[ident]=model
        finally:
            evaluated.to_mesh_clear()
            for m,visible in disabled:m.show_viewport=visible
            obj.show_only_shape_key,obj.active_shape_key_index=shape_state
    if set(models)!=set(by_id):raise BdaeError('A template geometry is missing from the Blender project')
    output.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(dir=output.parent,prefix='scene-export-') as temp:
        candidate=Path(temp)/output.name;verification=write_meshes(source,models,candidate);os.replace(candidate,output)
    report=dict(exporter='Blender BDAE',mode='scene mesh',blender=bpy.app.version_string,template=str(source.path),
                template_sha256=source.sha256,blend=bpy.data.filepath,output=str(output),output_sha256=hashlib.sha256(output.read_bytes()).hexdigest(),
                vertices=sum(len(m['vertices']) for m in models.values()),triangles=sum(len(b)//3 for m in models.values() for b in m['buffers']),
                clips=source.clips,events=source.events,verification=verification,dependencies=[],textures=[])
    output.with_suffix('.export.json').write_text(json.dumps(report,indent=2)+'\n')
    print('SCENE_EXPORT_VERIFIED '+str(output)+' '+json.dumps(verification),flush=True)
    return report


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--input',type=Path);parser.add_argument('--output',type=Path)
    parser.add_argument('--batch',action='store_true');parser.add_argument('--levels',type=int,default=2)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    if args.batch:
        results=[];failures=[]
        for template in sorted((ROOT/'Data/Scenes/bin').rglob('*.bdae')):
            relative=template.relative_to(ROOT/'Data/Scenes/bin')
            if str(relative)=='Goblin/goblin.bdae':continue
            source=SceneData(template)
            if not source.geometries:
                results.append(dict(source=str(relative),status='camera-only; no mesh to refine'));continue
            output=ROOT/'Data/Scenes'/relative.parent/(relative.stem+'_smooth.blend')
            if output.exists():
                results.append(json.loads(output.with_suffix('.mesh.json').read_text()));continue
            try:
                results.append(create_scene(template,output,args.levels))
            except Exception as error:
                failure=dict(source=str(relative),error=str(error));results.append(failure);failures.append(failure)
                print('SCENE_FAILED '+json.dumps(failure),flush=True)
        (ROOT/'build/verification/scenes/refinement-report.json').write_text(json.dumps(results,indent=2)+'\n')
        if failures:raise BdaeError('Scene refinements failed: '+json.dumps(failures))
    else:create_scene(args.input,args.output,args.levels)


if __name__=='__main__':main()
