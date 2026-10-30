"""Export a registered Blender scene to FBX for the Level Editor's model preview.

blender --background scene.blend --python-exit-code 1 --python tools/blender/export_fbx.py -- \
  --output Data/Scenes/Goblin/goblin.fbx

The game never loads this file; it uses the BDAE from export_bdae.py. Each Action is a
FBX take named after the game clip. Everything below changes only this throwaway session.
"""
import argparse
import math
import os
from pathlib import Path
import shutil
import sys
import tempfile

import bpy

# The one long timeline duplicates the per-clip Actions and would dominate the file size.
SOURCE_TIMELINE = '__ALL_SOURCE_ANIMATION'


def object_actions():
    return [a for a in bpy.data.actions if any(s.target_id_type == 'OBJECT' for s in a.slots)]


def bake_shape_keys(rig):
    """Shape key values are driven by rig custom properties, which the exporter does not sample."""
    scene = bpy.context.scene
    for obj in bpy.data.objects:
        keys = obj.type == 'MESH' and obj.data.shape_keys
        if not keys or not keys.animation_data or not keys.animation_data.drivers:
            continue
        blocks = [b for b in keys.key_blocks if b.relative_key != b]
        for action in object_actions():
            rig.animation_data.action = action
            if action.slots:
                rig.animation_data.action_slot = action.slots[0]
            first, last = int(action.frame_range[0]), int(action.frame_range[1])
            samples = {b.name: [] for b in blocks}
            for frame in range(first, last + 1):
                scene.frame_set(frame)
                for b in blocks:
                    samples[b.name].append((frame, b.value))
            baked = bpy.data.actions.new(action.name)
            channels = baked.layers.new('Baked').strips.new(type='KEYFRAME').channelbags.new(
                baked.slots.new(id_type='KEY', name=keys.name))
            for name, values in samples.items():
                curve = channels.fcurves.new(f'key_blocks["{name}"].value')
                curve.keyframe_points.add(len(values))
                curve.keyframe_points.foreach_set('co', [v for pair in values for v in pair])
                curve.update()
        for driver in list(keys.animation_data.drivers):
            keys.animation_data.drivers.remove(driver)


def drawable(obj):
    """Castle has unused planes collapsed to zero scale; the exporter turns their matrices into NaN."""
    matrix = obj.matrix_world
    return all(math.isfinite(v) for row in matrix for v in row) and abs(matrix.determinant()) > 1e-12 and \
        all(math.isfinite(c) for vertex in obj.data.vertices for c in vertex.co)


def prepare_objects():
    """Keep only meshes the preview can draw, under names no bone shares."""
    for obj in [o for o in bpy.data.objects if o.type == 'MESH']:
        # Hidden meshes are morph targets; their shapes already live in the base mesh's shape keys.
        if obj.hide_render or not drawable(obj):
            bpy.data.objects.remove(obj)
    # FBX loaders look nodes up by name, and the original scene nodes name both bones and meshes.
    bones = {b.name for o in bpy.data.objects if o.type == 'ARMATURE' for b in o.data.bones}
    for obj in bpy.data.objects:
        if obj.type == 'MESH' and obj.name in bones:
            obj.name += '_mesh'


def link_textures():
    """The exporter only follows Image Texture -> Principled Base Color. Drop the vertex-color mix."""
    for material in bpy.data.materials:
        if not material.use_nodes:
            continue
        nodes = material.node_tree.nodes
        shader = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None)
        image = next((n for n in nodes if n.type == 'TEX_IMAGE' and n.image), None)
        if shader and image:
            material.node_tree.links.new(image.outputs['Color'], shader.inputs['Base Color'])


def export(output):
    rig = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
    for action in [a for a in bpy.data.actions if a.name.endswith(SOURCE_TIMELINE)]:
        bpy.data.actions.remove(action)
    if rig and rig.animation_data:
        bake_shape_keys(rig)
    prepare_objects()
    link_textures()
    # Export through a temporary folder: the file records the absolute path of each texture,
    # which should not be this checkout's.
    with tempfile.TemporaryDirectory(prefix='castle-fbx-') as directory:
        staged = os.path.join(directory, output.name)
        bpy.ops.export_scene.fbx(
            filepath=staged, use_selection=False, object_types={'ARMATURE', 'MESH'},
            add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
            bake_anim_use_nla_strips=False, bake_anim_simplify_factor=1.0, bake_anim_step=1.0,
            embed_textures=False, path_mode='STRIP', apply_unit_scale=False,
            apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
            axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE')
        output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(staged, output)
    print(f'FBX {output} ({output.stat().st_size} bytes)', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    export(args.output.resolve())


if __name__ == '__main__':
    main()
