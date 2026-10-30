"""Blender 5+ importer for Castle Frenzy's animated BDAE skin format.

blender --background --factory-startup --python tools/blender/import_bdae.py -- \
  --input Build/bin/Scenes/Goblin/goblin.bdae --output Data/Scenes/Goblin/goblin.blend
"""
import argparse
import bisect
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from bdae_reader import Bdae, BdaeError


def matrix(values):
    """SMatrix is column-major, with translation at indices 12..14."""
    return Matrix([values[i:i+4] for i in range(0, 16, 4)]).transposed()


def engine_quaternion(values):
    # IrrCompileGameConfig enables right-hand convention: getMatrix_transposed.
    x, y, z, w = values
    return Quaternion((w, -x, -y, -z)).normalized()


def sample(track, time):
    times = track['times']
    index = max(0, bisect.bisect_right(times, time)-1)
    a = track['values'][index]
    if index == len(times)-1 or track['interpolation'] == 0 or times[index] == time:
        return engine_quaternion(a) if track['kind'] == 5 else a
    ratio = (time-times[index])/(times[index+1]-times[index])
    b = track['values'][index+1]
    if track['kind'] == 5:
        return engine_quaternion(a).slerp(engine_quaternion(b), ratio)
    return [x+(y-x)*ratio for x, y in zip(a, b)]


class Importer:
    def __init__(self, source):
        self.source = source
        self.nodes = {n['id']: n for n in source.nodes}
        self.tracks = {}
        for track in source.tracks:
            self.tracks.setdefault(track['target'], []).append(track)
        self.convert = Matrix.Rotation(math.pi/2, 4, 'X') if source.up_axis == 1 else Matrix.Identity(4)
        self.convert_inv = self.convert.inverted()
        # 3ds Max bones point along local X; Blender bones point along local Y.
        self.bone_axes = Matrix.Rotation(-math.pi/2, 4, 'Z')
        self.rest = {}
        self.meshes = []
        self.actions = {}
        self.slots = {}

    def worlds(self, time=None):
        result = {}
        for node in self.source.nodes:
            position = node['position']
            rotation = engine_quaternion(node['rotation'])
            scale = node['scale']
            if time is not None:
                for track in self.tracks.get(node['id'], []):
                    value = sample(track, time)
                    if track['kind'] == 1:
                        position = value
                    elif track['kind'] == 5:
                        rotation = value
                    elif track['kind'] == 9:
                        # The scalar angle track retains the node's original rotation axis.
                        x, y, z, w = node['rotation']
                        axis = Quaternion((w, x, y, z)).axis
                        rotation = Quaternion(axis, value[0]).conjugated()
                    elif track['kind'] == 10:
                        scale = value
            local = Matrix.LocRotScale(Vector(position), rotation, Vector(scale))
            result[node['id']] = result[node['parent']] @ local if node['parent'] else local
        return result

    def create_rig(self):
        if len(self.source.skins) != 1:
            raise BdaeError('This importer currently expects one skeletal skin controller')
        self.skin = self.source.skins[0]
        sid_nodes = {n['sid']: n for n in self.source.nodes if n['sid']}
        needed = {sid_nodes[s]['id'] for s in self.skin['joints']} | set(self.tracks)
        for ident in list(needed):
            parent = self.nodes[ident]['parent']
            while parent:
                needed.add(parent)
                parent = self.nodes[parent]['parent']
        self.bones = [n for n in self.source.nodes if n['id'] in needed]
        self.names = {n['id']: n['name'] or n['id'] for n in self.bones}
        if len(set(self.names.values())) != len(self.names):
            raise BdaeError('Duplicate bone names')
        self.joint_ids = [sid_nodes[s]['id'] for s in self.skin['joints']]
        self.bind_world = {sid_nodes[s]['id']: matrix(inv).inverted()
                           for s, inv in zip(self.skin['joints'], self.skin['inverse_bind'])}
        initial = self.worlds()
        data = bpy.data.armatures.new('Goblin_Skeleton')
        self.rig = bpy.data.objects.new('Goblin_Rig', data)
        bpy.context.collection.objects.link(self.rig)
        bpy.context.view_layer.objects.active = self.rig
        self.rig.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        for node in self.bones:
            ident = node['id']
            rest = self.convert @ self.bind_world.get(ident, initial[ident]) @ self.bone_axes
            bone = data.edit_bones.new(self.names[ident])
            child_lengths = [(self.bind_world.get(c['id'], initial[c['id']]).translation -
                              self.bind_world.get(ident, initial[ident]).translation).length
                             for c in self.bones if c['parent'] == ident]
            bone.length = max(0.035, min([n for n in child_lengths if n > 0.02] or [0.12]))
            # A newly-created edit bone has zero length; initialize its length
            # before assigning orientation, otherwise its direction is discarded.
            bone.matrix = rest
            if node['parent'] in needed:
                bone.parent = data.edit_bones[self.names[node['parent']]]
            bone.use_connect = False
        bpy.ops.object.mode_set(mode='OBJECT')
        self.rest = {n['id']: data.bones[self.names[n['id']]].matrix_local.copy() for n in self.bones}
        for node in self.bones:
            bone = data.bones[self.names[node['id']]]
            bone.use_deform = node['id'] in self.joint_ids
            bone['bdae_id'] = node['id']
            bone['bdae_sid'] = node['sid']
            self.rig.pose.bones[bone.name].rotation_mode = 'QUATERNION'
        data.display_type = 'OCTAHEDRAL'
        self.rig.show_in_front = True
        self.rig['source_sha256'] = self.source.sha256
        self.rig['source_layout'] = self.source.version
        self.rig['source_events'] = json.dumps(self.source.events)
        self.rig['source_animation_tracks'] = len(self.source.tracks)

    def material(self):
        if len(self.source.images) != 1:
            raise BdaeError('This importer currently expects one diffuse image')
        filename = Path(self.source.images[0]['file'].replace('\\', '/')).name
        # Original templates retain legacy filenames; authored textures are PNG.
        candidates = [self.source.path.parent/Path(filename).with_suffix('.png'),
                      self.source.path.parent/filename]
        texture = next((p for p in candidates if p.is_file()), None)
        if texture is None:
            raise BdaeError(f'Missing diffuse texture {filename}')
        material = bpy.data.materials.new('Goblin_Diffuse')
        material.use_nodes = True
        nodes = material.node_tree.nodes
        shader = nodes.get('Principled BSDF')
        shader.inputs['Roughness'].default_value = 0.85
        shader.inputs['Metallic'].default_value = 0.0
        image = bpy.data.images.load(str(texture), check_existing=True)
        image.pack()
        sampler = nodes.new('ShaderNodeTexImage')
        sampler.image = image
        sampler.interpolation = 'Closest'
        material.node_tree.links.new(sampler.outputs['Color'], shader.inputs['Base Color'])
        return material, str(texture)

    def create_meshes(self):
        material, texture = self.material()
        self.texture = texture
        geometry = next(g for g in self.source.geometries if g['id'] == self.skin['geometry'])
        bind_shape = matrix(self.skin['bind_shape'])
        for index, buffer in enumerate(geometry['buffers']):
            positions = [self.convert @ bind_shape @ Vector(v) for v in buffer['positions']]
            faces = [buffer['indices'][i:i+3] for i in range(0, len(buffer['indices']), 3)]
            mesh = bpy.data.meshes.new(f'{geometry["name"]}_Mesh_{index}')
            mesh.from_pydata(positions, [], faces)
            mesh.update()
            if buffer['uv']:
                uv = mesh.uv_layers.new(name='UVMap')
                for loop in mesh.loops:
                    u, v = buffer['uv'][loop.vertex_index]
                    uv.data[loop.index].uv = (u, 1-v)  # ColladaMax: UPPER_LEFT texture origin.
            obj = bpy.data.objects.new(geometry['name'] if index == 0 else f'{geometry["name"]}_{index}', mesh)
            bpy.context.collection.objects.link(obj)
            obj.data.materials.append(material)
            for polygon in mesh.polygons:
                polygon.use_smooth = True
            if buffer.get('normals'):
                normal_matrix = (self.convert @ bind_shape).to_3x3().inverted().transposed()
                normals = [(normal_matrix @ Vector(n)).normalized() for n in buffer['normals']]
                mesh.normals_split_custom_set_from_vertices(normals)
            groups = [obj.vertex_groups.new(name=self.names[j]) for j in self.joint_ids]
            for vertex, influences in enumerate(self.skin['influences']):
                for joint, weight in influences:
                    groups[joint].add([vertex], weight, 'REPLACE')
            modifier = obj.modifiers.new('Original Goblin Skin', 'ARMATURE')
            modifier.object = self.rig
            modifier.use_deform_preserve_volume = False
            obj.parent = self.rig
            self.meshes.append((obj, buffer))

    def apply_pose(self, time):
        world = self.worlds(time)
        posed = {n['id']: self.convert @ world[n['id']] @ self.bone_axes for n in self.bones}
        for node in self.bones:
            ident, parent = node['id'], node['parent']
            rest_local = self.rest[parent].inverted() @ self.rest[ident] if parent in self.rest else self.rest[ident]
            posed_local = posed[parent].inverted() @ posed[ident] if parent in posed else posed[ident]
            self.rig.pose.bones[self.names[ident]].matrix_basis = rest_local.inverted() @ posed_local
        return posed

    def make_action(self, name, start, end):
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        action['source_start_ms'] = start
        action['source_end_ms'] = end
        action['source_fps'] = self.source.fps
        action.asset_mark()
        action.asset_data.description = f'Original Goblin animation, source {start}–{end} milliseconds'
        self.rig.animation_data_create()
        self.rig.animation_data.action = action
        times = sorted({start, end} | {t for track in self.source.tracks for t in track['times'] if start <= t <= end})
        previous = {}
        for time in times:
            self.apply_pose(time)
            frame = 1+(time-start)*self.source.fps/1000
            for bone in self.rig.pose.bones:
                quat = bone.rotation_quaternion.copy()
                if bone.name in previous and quat.dot(previous[bone.name]) < 0:
                    quat.negate()
                    bone.rotation_quaternion = quat
                previous[bone.name] = quat
                for prop in ('location', 'rotation_quaternion', 'scale'):
                    if not bone.keyframe_insert(prop, frame=frame, group=bone.name):
                        raise RuntimeError(f'Cannot keyframe {bone.name}.{prop}')
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.interpolation = 'LINEAR'
        selected_events = [dict(time_ms=e['time_ms'], names=e['names'],
                                frame=1+(e['time_ms']-start)*self.source.fps/1000)
                           for e in self.source.events if start <= e['time_ms'] <= end]
        action['game_events_json'] = json.dumps(selected_events)
        for event in selected_events:
            for name in event['names']:
                marker = action.pose_markers.new(name)
                marker.frame = round(event['frame'])
        self.actions[action.name] = action
        self.slots[action.name] = self.rig.animation_data.action_slot
        print(f'ACTION {action.name}: {len(times)} samples, {start}..{end} ms', flush=True)
        return dict(name=action.name, samples=len(times), start_ms=start, end_ms=end,
                    frame_end=1+(end-start)*self.source.fps/1000, events=selected_events)

    def select_action(self, name):
        self.rig.animation_data.action = self.actions[name]
        self.rig.animation_data.action_slot = self.slots[name]

    def create_actions(self):
        start = min(t['times'][0] for t in self.source.tracks)
        end = max(t['times'][-1] for t in self.source.tracks)
        self.master = 'Goblin__ALL_SOURCE_ANIMATION'
        self.action_report = [self.make_action(self.master, start, end)]
        for clip in self.source.clips:
            self.action_report.append(self.make_action(clip['name'], clip['start_ms'], clip['end_ms']))
        # A muted strip track documents the original absolute timeline without double-transforming the rig.
        track = self.rig.animation_data.nla_tracks.new()
        track.name = 'Original clip timeline (muted)'
        track.mute = True
        for clip in sorted(self.source.clips, key=lambda c: c['start_ms']):
            strip = track.strips.new(clip['name'], round(1+clip['start_ms']*self.source.fps/1000), self.actions[clip['name']])
            strip.action_slot = self.slots[clip['name']]
            strip.action_frame_start = 1
            strip.action_frame_end = 1+(clip['end_ms']-clip['start_ms'])*self.source.fps/1000
            strip.frame_start = 1+clip['start_ms']*self.source.fps/1000
            strip.frame_end = 1+clip['end_ms']*self.source.fps/1000
        self.select_action('gob_vihren_walk_03' if 'gob_vihren_walk_03' in self.actions else self.source.clips[0]['name'])

    def verify(self):
        """Compare evaluated Blender skinning with the engine's CPU skin formula."""
        scene = bpy.context.scene
        max_pose_error = max_vertex_error = 0.0
        samples = 0
        bind_shape = matrix(self.skin['bind_shape'])
        inverse_bind = [matrix(v) for v in self.skin['inverse_bind']]
        for report in self.action_report:
            self.select_action(report['name'])
            start, end = report['start_ms'], report['end_ms']
            times = sorted({start, end} | {t for track in self.source.tracks for t in track['times'] if start <= t <= end})
            for time in times:
                frame = 1+(time-start)*self.source.fps/1000
                scene.frame_set(int(frame), subframe=frame-int(frame))
                bpy.context.view_layer.update()
                world = self.worlds(time)
                for node in self.bones:
                    expected = self.convert @ world[node['id']] @ self.bone_axes
                    actual = self.rig.pose.bones[self.names[node['id']]].matrix
                    max_pose_error = max(max_pose_error, max(abs(a-b) for ar, er in zip(actual, expected) for a, b in zip(ar, er)))
                # Check every skinned vertex at each key time, including clip boundaries.
                skin_matrices = [self.convert @ world[j] @ inv @ bind_shape for j, inv in zip(self.joint_ids, inverse_bind)]
                depsgraph = bpy.context.evaluated_depsgraph_get()
                for obj, buffer in self.meshes:
                    evaluated = obj.evaluated_get(depsgraph)
                    mesh = evaluated.to_mesh()
                    try:
                        if len(mesh.vertices) != len(buffer['positions']):
                            raise RuntimeError('Evaluated vertex count changed')
                        for i, original in enumerate(buffer['positions']):
                            expected = Vector((0, 0, 0))
                            for j, weight in self.skin['influences'][i]:
                                expected += (skin_matrices[j] @ Vector(original))*weight
                            max_vertex_error = max(max_vertex_error, (mesh.vertices[i].co-expected).length)
                    finally:
                        evaluated.to_mesh_clear()
                samples += 1
        if max_pose_error > 0.0002 or max_vertex_error > 0.0002:
            raise RuntimeError(f'Animation verification failed: pose {max_pose_error}, vertices {max_vertex_error}')
        return dict(samples_checked=samples, all_bones_checked=len(self.bones),
                    all_vertices_checked=sum(len(b['positions']) for _, b in self.meshes),
                    max_pose_matrix_error=max_pose_error, max_skinned_vertex_error=max_vertex_error,
                    tolerance=0.0002, verified_at='All original key times and clip boundaries')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if args.output.suffix.lower() != '.blend':
        parser.error('Blender projects use the .blend extension')
    output = args.output.resolve()
    if output.exists():
        parser.error(f'Output already exists: {output}; choose a new output path')
    source = Bdae(args.input)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    importer = Importer(source)
    importer.create_rig()
    importer.create_meshes()
    bpy.context.scene.render.fps = source.fps
    importer.create_actions()
    report = source.summary()
    report.update(blender_version=bpy.app.version_string, output=str(output),
                  actions=importer.action_report, texture=importer.texture,
                  verification=importer.verify())
    initial = 'gob_vihren_walk_03' if 'gob_vihren_walk_03' in importer.actions else source.clips[0]['name']
    importer.select_action(initial)
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = math.ceil(importer.actions[initial].frame_range[1])
    scene.frame_set(1)
    bpy.ops.object.select_all(action='DESELECT')
    importer.rig.select_set(True)
    bpy.context.view_layer.objects.active = importer.rig
    scene['bdae_import_report'] = json.dumps(report)
    text = bpy.data.texts.new('README_Goblin')
    text.write('Goblin imported directly from the game BDAE.\n\n'
               'Select Goblin_Rig. In Dope Sheet > Action Editor choose one of the 12 named Actions.\n'
               'Goblin__ALL_SOURCE_ANIMATION retains the complete source timeline, including gaps.\n'
               'Actions are marked as assets and retained with fake users. Original NLA clip timeline is muted.\n'
               'Original hit/blood game events are stored as Action markers and exact JSON metadata.\n'
               'All original key poses and clip boundaries were verified against CPU skinning.\n'
               'Textures are packed. Y-up is converted to Z-up. This is an editable weighted armature, not a vertex cache.\n'
               'Edit this .blend as your new source. Export back into the game is a separate pipeline.\n\n'+json.dumps(report, indent=2))
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'DOPESHEET_EDITOR':
                area.spaces.active.mode = 'ACTION'
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.shading.type = 'MATERIAL'
                space.overlay.show_floor = True
                space.region_3d.view_distance = 4.5
                space.region_3d.view_location = Vector((0, 0, 0.9))
                space.region_3d.view_rotation = Quaternion((0.88, 0.38, 0.11, 0.25)).normalized()
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output), compress=True)
    output.with_suffix('.import.json').write_text(json.dumps(report, indent=2)+'\n')
    print('BDAE_IMPORT_VERIFIED '+json.dumps(report['verification']), flush=True)


if __name__ == '__main__':
    main()
