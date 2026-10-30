"""Export an editable Blender 5.2+ game character, including its Actions, to BDAE.

blender --background character.blend --python-exit-code 1 --python tools/blender/export_bdae.py -- \
  --template Build/bin/Scenes/Goblin/goblin.bdae --output build/models/Goblin/goblin.bdae
"""
import argparse
from array import array
import hashlib
import json
import math
import os
from pathlib import Path
import sys
import tempfile

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from bdae_reader import Bdae, BdaeError
from bdae_writer import serialize, bounds
from import_bdae import Importer, matrix


def flat(matrix):
    return [matrix[row][col] for col in range(4) for row in range(4)]


def transform(matrix):
    location, rotation, scale = matrix.decompose()
    rebuilt = Matrix.LocRotScale(location, rotation, scale)
    if max(abs(a-b) for row, other in zip(matrix, rebuilt) for a, b in zip(row, other)) > 0.0002:
        raise BdaeError('Animated shear cannot be represented by the game. Apply object scales or remove shearing constraints.')
    return dict(position=list(location), rotation=[-rotation.x, -rotation.y, -rotation.z, rotation.w], scale=list(scale))


def action_curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves


def set_frame(frame):
    whole = math.floor(frame)
    bpy.context.scene.frame_set(whole, subframe=frame-whole)
    bpy.context.view_layer.update()


class Exporter:
    def __init__(self, template, rig=None):
        self.source = Bdae(template)
        if len(self.source.skins) != 1 or len(self.source.geometries) != 1 or len(self.source.images) != 1:
            raise BdaeError('This character profile requires one skin, one geometry and one diffuse texture')
        rigs = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
        self.rig = bpy.data.objects.get(rig) if rig else next((o for o in rigs if o.get('source_sha256') == self.source.sha256), None)
        if self.rig is None:
            raise BdaeError('Choose the imported game armature with --rig')
        self.helper = Importer(self.source)
        self.convert, self.convert_inv = self.helper.convert, self.helper.convert_inv
        self.axes_inv = self.helper.bone_axes.inverted()
        self.original = {n['id']: n for n in self.source.nodes}
        self.bones = list(self.rig.data.bones)
        self.ids, self.sids = {}, {}
        for bone in self.bones:
            ident = bone.get('bdae_id', bone.name+'-node')
            if ident in self.ids.values():
                raise BdaeError('Duplicate BDAE bone ID')
            self.ids[bone.name] = ident
            self.sids[bone.name] = bone.get('bdae_sid') or 'Blender_'+bone.name
        if len(set(self.sids.values())) != len(self.sids):
            raise BdaeError('Duplicate BDAE joint SID; retain a unique identity for every bone')
        required = set(self.helper.tracks) | {n['id'] for n in self.source.nodes if n['sid'] in self.source.skins[0]['joints']}
        if not required.issubset(set(self.ids.values())):
            raise BdaeError('Original game bone IDs must be retained; add bones or rename Blender bones using their existing BDAE properties')
        self.joints = [b for b in self.bones if b.use_deform]
        if not self.joints or len(self.joints) > 255:
            raise BdaeError('Expected 1..255 deforming bones')
        self.joint_index = {b.name: i for i, b in enumerate(self.joints)}
        self.rest = {b.name: self.convert_inv @ self.rig.matrix_world @ b.matrix_local @ self.axes_inv for b in self.bones}
        self.inverse_bind = [self.rest[b.name].inverted() for b in self.joints]
        self.meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.find_armature() == self.rig and not o.hide_render]
        if not self.meshes:
            raise BdaeError('No visible meshes are bound to the selected rig')
        self.frame_rate = bpy.context.scene.render.fps/bpy.context.scene.render.fps_base
        self.model = dict(vertices=[], indices=[], tracks=[], clips=[], events=[], nodes=[], joints=[],
                          fps=round(bpy.context.scene.render.fps/bpy.context.scene.render.fps_base))
        if not 1 <= self.model['fps'] <= 240:
            raise BdaeError('Sampling FPS must be 1..240')
        self.lookup = []
        self.expected_poses = {}

    def geometry(self):
        image = None
        for obj in self.meshes:
            visible = [m for m in obj.modifiers if m.show_viewport]
            armatures = [m for m in visible if m.type == 'ARMATURE']
            if len(armatures) != 1 or armatures[0].object != self.rig or visible[-1] != armatures[0]:
                raise BdaeError(f'{obj.name}: put modeling modifiers before a single Armature modifier')
            if obj.data.shape_keys and obj.data.shape_keys.animation_data:
                raise BdaeError('Animated shape keys are not supported by this skeletal game profile')
            materials = {slot.material for slot in obj.material_slots if slot.material}
            if len(materials) != 1:
                raise BdaeError(f'{obj.name}: this profile requires one diffuse material')
            material = next(iter(materials))
            shader = next((n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if material.node_tree else None
            links = shader.inputs['Base Color'].links if shader else []
            texture = links[0].from_node if len(links) == 1 else None
            if texture is None or texture.type != 'TEX_IMAGE' or texture.image is None:
                raise BdaeError('Base Color must be connected directly to an Image Texture; bake procedural materials first')
            if image is not None and texture.image != image:
                raise BdaeError('All mesh parts must share one diffuse texture in this profile')
            image = texture.image
            modifier = armatures[0]
            modifier.show_viewport = False
            bpy.context.view_layer.update()
            evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=bpy.context.evaluated_depsgraph_get())
            try:
                mesh.calc_loop_triangles()
                if not mesh.uv_layers.active:
                    raise BdaeError(f'{obj.name}: mesh has no active UV map')
                uv = mesh.uv_layers.active.data
                world = self.convert_inv @ obj.matrix_world
                normal_matrix = world.to_3x3().inverted().transposed()
                group_names = {g.index: g.name for g in obj.vertex_groups}
                cache = {}
                for tri in mesh.loop_triangles:
                    loops = list(tri.loops)
                    if world.determinant() < 0:
                        loops[1], loops[2] = loops[2], loops[1]
                    for loop in loops:
                        index = mesh.loops[loop].vertex_index
                        normal = (normal_matrix @ mesh.corner_normals[loop].vector).normalized()
                        texcoord = [uv[loop].uv.x, 1-uv[loop].uv.y]
                        key = (index, *(round(v, 7) for v in (*normal, *texcoord)))
                        if key not in cache:
                            vertex = mesh.vertices[index]
                            influences = [(self.joint_index[group_names[g.group]], g.weight) for g in vertex.groups
                                          if group_names.get(g.group) in self.joint_index and g.weight > 1e-8]
                            total = sum(w for _, w in influences)
                            if not total:
                                raise BdaeError(f'{obj.name}: vertex {index} has no deforming bone weights')
                            influences = [(j, w/total) for j, w in influences]
                            cache[key] = len(self.model['vertices'])
                            self.model['vertices'].append(dict(position=list(world @ vertex.co), normal=list(normal),
                                                               uv=texcoord, color=[255]*4, weights=influences))
                            self.lookup.append((obj.name, index))
                        self.model['indices'].append(cache[key])
                if len(self.model['vertices']) > 32768:
                    raise BdaeError('The legacy engine supports at most 32768 indexed vertices per model')
            finally:
                evaluated.to_mesh_clear()
                modifier.show_viewport = True
                bpy.context.view_layer.update()
        self.image = image
        for i, bone in enumerate(self.joints):
            local_points = [self.inverse_bind[i] @ Vector(v['position']) for v in self.model['vertices']
                            if any(j == i for j, _ in v['weights'])]
            self.model['joints'].append(dict(id=self.ids[bone.name], sid=self.sids[bone.name],
                                             inverse_bind=flat(self.inverse_bind[i]), bounds=bounds(local_points)))

    def nodes(self):
        result = []
        for bone in self.bones:
            parent = bone.parent
            local = self.rest[parent.name].inverted() @ self.rest[bone.name] if parent else self.rest[bone.name]
            ident = self.ids[bone.name]
            original = self.original.get(ident)
            result.append(dict(id=ident, name=original['name'] if original else bone.name,
                               sid=self.sids[bone.name], parent=self.ids[parent.name] if parent else None,
                               **transform(local)))
        exported_ids = set(self.ids.values())
        result.extend(dict(n) for n in self.source.nodes if n['id'] not in exported_ids)
        self.model['nodes'] = result

    def select(self, action):
        slots = list(action.slots)
        slot = next((s for s in slots if s.name_display == self.rig.name), None)
        if slot is None and len(slots) == 1:
            slot = slots[0]
        if slot is None:
            raise BdaeError(f'{action.name}: cannot identify the armature Action slot')
        self.rig.animation_data.action = action
        self.rig.animation_data.action_slot = slot

    def action_times(self, action):
        return {float(k.co.x) for curve in action_curves(action) for k in curve.keyframe_points}

    def layout(self):
        existing = {c['name']: c for c in self.source.clips}
        actions = []
        for clip in self.source.clips:
            action = bpy.data.actions.get(clip['name'])
            if action is None:
                raise BdaeError(f'Missing required game Action {clip["name"]}')
            actions.append(action)
        for action in bpy.data.actions:
            if action.name not in existing and action.get('bdae_export', False):
                actions.append(action)
        self.master = bpy.data.actions.get('Goblin__ALL_SOURCE_ANIMATION')
        self.clip_actions = []
        cursor = -166
        for action in sorted(actions, key=lambda a: existing.get(a.name, {}).get('start_ms', 1_000_000)):
            frames = self.action_times(action)
            if not frames:
                raise BdaeError(f'{action.name}: Action contains no keyframes')
            first, last = map(float, action.frame_range)
            duration = round((last-first)*1000/self.frame_rate)
            if duration <= 0:
                raise BdaeError(f'{action.name}: animation must contain more than one time sample')
            original = existing.get(action.name, {})
            start = max(original.get('start_ms', cursor+166), cursor+166)
            clip = dict(name=action.name, start_ms=start, end_ms=start+duration,
                        action=action, frame_start=first, frame_end=last)
            self.clip_actions.append(clip)
            cursor = start+duration
        # Preserve the game's named-clip index ordering, even when clips are stored out of time order.
        ordered = [next(c for c in self.clip_actions if c['name'] == a.name) for a in actions]
        self.model['clips'] = [{k: c[k] for k in ('name', 'start_ms', 'end_ms')} for c in ordered]

    def events(self):
        result = {}
        for clip in self.clip_actions:
            action = clip['action']
            originals = json.loads(action.get('game_events_json', '[]'))
            for marker in action.pose_markers:
                frame = float(marker.frame)
                exact = next((e for e in originals if marker.name in e['names'] and round(e['frame']) == marker.frame), None)
                if exact:
                    frame = exact['frame']
                ratio = (frame-clip['frame_start'])/(clip['frame_end']-clip['frame_start'])
                if not 0 <= ratio <= 1:
                    raise BdaeError(f'{action.name}: event {marker.name} lies outside the Action range')
                time = round(clip['start_ms']+ratio*(clip['end_ms']-clip['start_ms']))
                result.setdefault(time, []).append(marker.name)
        if self.master:
            for event in self.source.events:
                if not any(c['start_ms'] <= event['time_ms'] <= c['end_ms'] for c in self.clip_actions):
                    result.setdefault(event['time_ms'], []).extend(event['names'])
        self.model['events'] = [dict(time_ms=t, names=sorted(set(names))) for t, names in sorted(result.items())]

    def choose(self, time):
        for clip in self.clip_actions:
            if clip['start_ms'] <= time <= clip['end_ms']:
                ratio = (time-clip['start_ms'])/(clip['end_ms']-clip['start_ms'])
                return clip['action'], clip['frame_start']+ratio*(clip['frame_end']-clip['frame_start'])
        if self.master:
            return self.master, min(self.master.frame_range[1], 1+time*self.frame_rate/1000)
        clip = min(self.clip_actions, key=lambda c: abs(time-c['start_ms']))
        return clip['action'], clip['frame_start'] if time < clip['start_ms'] else clip['frame_end']

    def animation(self):
        self.layout()
        self.events()
        fps = self.model['fps']
        end = max(c['end_ms'] for c in self.clip_actions)
        if self.master:
            end = max(end, round((self.master.frame_range[1]-1)*1000/self.frame_rate))
        times = {round(i*1000/fps) for i in range(math.ceil(end*fps/1000)+1)}
        times = {t for t in times if 0 <= t <= end} | {0, end}
        for clip in self.clip_actions:
            duration = clip['end_ms']-clip['start_ms']
            times |= {clip['start_ms'], clip['end_ms']}
            times |= {round(clip['start_ms']+(f-clip['frame_start'])*duration/(clip['frame_end']-clip['frame_start']))
                      for f in self.action_times(clip['action'])
                      if clip['frame_start'] <= f <= clip['frame_end']}
        if self.master:
            times |= {round((f-1)*1000/self.frame_rate) for f in self.action_times(self.master) if 0 <= (f-1)*1000/self.frame_rate <= end}
        times = sorted(times)
        values = {b.name: {1: [], 5: [], 10: []} for b in self.bones}
        max_vertex_error = 0.0
        active = None
        for time in times:
            action, frame = self.choose(time)
            if action != active:
                self.select(action)
                active = action
            set_frame(frame)
            evaluated = self.rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
            world = {b.name: self.convert_inv @ evaluated.matrix_world @ evaluated.pose.bones[b.name].matrix @ self.axes_inv for b in self.bones}
            self.expected_poses[time] = {self.ids[name]: m.copy() for name, m in world.items()}
            for bone in self.bones:
                local = world[bone.parent.name].inverted() @ world[bone.name] if bone.parent else world[bone.name]
                parts = transform(local)
                for kind, field in [(1, 'position'), (5, 'rotation'), (10, 'scale')]:
                    values[bone.name][kind].append(parts[field])
            native = {}
            for obj in self.meshes:
                evaluated_mesh = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
                mesh = evaluated_mesh.to_mesh()
                native[obj.name] = [evaluated_mesh.matrix_world @ v.co for v in mesh.vertices]
                evaluated_mesh.to_mesh_clear()
            matrices = [world[b.name] @ inv for b, inv in zip(self.joints, self.inverse_bind)]
            for vertex, (obj_name, index) in zip(self.model['vertices'], self.lookup):
                point = Vector((0, 0, 0))
                for joint, weight in vertex['weights']:
                    point += (matrices[joint] @ Vector(vertex['position']))*weight
                if index >= len(native[obj_name]):
                    raise BdaeError('A modifier changes topology over time; apply or remove that modifier')
                max_vertex_error = max(max_vertex_error, (self.convert @ point-native[obj_name][index]).length)
        if max_vertex_error > 0.0003:
            raise BdaeError(f'Blender deformation is not representable by this skin profile (max vertex error {max_vertex_error})')
        for bone in self.bones:
            for kind, suffix in [(1, 'translation'), (5, 'rotation'), (10, 'scale')]:
                self.model['tracks'].append(dict(name=self.ids[bone.name]+'-'+suffix, target=self.ids[bone.name],
                                                  kind=kind, times=times, values=values[bone.name][kind]))
        return dict(samples=len(times), max_blender_skin_error=max_vertex_error)

    def texture(self, output):
        path = output.with_name(output.stem+'_diffuse.png')
        # Image.copy alone copies the backing file, not unsaved painted pixels.
        # Save a separate pixel buffer so exporting never clears the artist's dirty flag.
        image = self.image.copy()
        try:
            pixels = array('f', [0.0])*len(self.image.pixels)
            self.image.pixels.foreach_get(pixels)
            image.pixels.foreach_set(pixels)
            image.update()
            image.filepath_raw = str(path)
            image.file_format = 'PNG'
            image.save()
        finally:
            bpy.data.images.remove(image)
        self.model['texture_file'] = path.name
        return path

    def verify_binary(self, candidate):
        source = Bdae(candidate)
        reader = Importer(source)
        maximum = 0.0
        for time, expected in self.expected_poses.items():
            actual = reader.worlds(time)
            for ident, matrix in expected.items():
                maximum = max(maximum, max(abs(a-b) for row, other in zip(matrix, actual[ident]) for a, b in zip(row, other)))
        if maximum > 0.0003:
            raise BdaeError(f'Exported binary pose mismatch: {maximum}')
        if source.events != self.model['events'] or source.clips != self.model['clips']:
            raise BdaeError('Exported clip/event metadata differs')
        return dict(max_binary_pose_error=maximum, samples_checked=len(self.expected_poses))


def export_model(template, output, rig=None, texture_format='png'):
    if texture_format != 'png':
        raise BdaeError('Game model textures must use PNG')
    if bpy.context.scene.get('bdae_mesh_scene'):
        from scene_tools import export_scene_meshes
        return export_scene_meshes(template, output)
    output = Path(output).resolve()
    if output.suffix.lower() != '.bdae':
        raise BdaeError('Choose a .bdae output file')
    exporter = Exporter(template, rig)
    if output == exporter.source.path:
        raise BdaeError('Export into a separate directory; retain the original BDAE template')
    anim = exporter.rig.animation_data
    old_action, old_slot = anim.action, anim.action_slot
    scene = bpy.context.scene
    old_frame = scene.frame_current+scene.frame_subframe
    mutes = [(t, t.mute) for t in anim.nla_tracks]
    modifiers = [(m, m.show_viewport) for obj in exporter.meshes for m in obj.modifiers]
    try:
        for track, _ in mutes:
            track.mute = True
        exporter.geometry()
        exporter.nodes()
        verification = exporter.animation()
        output.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='bdae-export-', dir=output.parent) as temp:
            staged = Path(temp)/output.name
            texture = exporter.texture(staged)
            staged.write_bytes(serialize(exporter.source, exporter.model))
            verification.update(exporter.verify_binary(staged))
            os.replace(texture, output.parent/texture.name)
            os.replace(staged, output)
        report = dict(exporter='Blender BDAE', blender=bpy.app.version_string, template=str(exporter.source.path),
                      template_sha256=exporter.source.sha256, blend=bpy.data.filepath,
                      output=str(output), output_sha256=hashlib.sha256(output.read_bytes()).hexdigest(),
                      vertices=len(exporter.model['vertices']), triangles=len(exporter.model['indices'])//3,
                      bones=len(exporter.bones), joints=len(exporter.joints), tracks=len(exporter.model['tracks']),
                      clips=exporter.model['clips'], events=exporter.model['events'],
                      texture=exporter.model['texture_file'], verification=verification,
                      dependencies=sorted({bpy.path.abspath(i.filepath) for i in [exporter.image] if not i.packed_file and i.filepath} |
                                          {bpy.path.abspath(l.filepath) for l in bpy.data.libraries}))
        output.with_suffix('.export.json').write_text(json.dumps(report, indent=2)+'\n')
        print('BDAE_EXPORT_VERIFIED '+json.dumps(report), flush=True)
        return report
    finally:
        anim.action, anim.action_slot = old_action, old_slot
        for track, mute in mutes:
            track.mute = mute
        for modifier, visible in modifiers:
            modifier.show_viewport = visible
        set_frame(old_frame)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--template', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--rig')
    parser.add_argument('--texture-format', choices=['png'], default='png')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    export_model(args.template, args.output, args.rig, args.texture_format)


if __name__ == '__main__':
    main()
