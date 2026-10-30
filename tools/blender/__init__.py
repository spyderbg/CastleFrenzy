"""Install the packaged ZIP as a Blender add-on to export from File > Export."""
bl_info = {
    'name': 'Castle Frenzy BDAE Export',
    'author': 'Castle Frenzy tools',
    'version': (1, 0, 0),
    'blender': (5, 2, 0),
    'location': 'File > Export > Castle Frenzy BDAE',
    'description': 'Export editable game characters, skin weights, textures and animation Actions',
    'category': 'Import-Export',
}

import json
from pathlib import Path
import sys

import bpy
from bpy.props import StringProperty, EnumProperty
from bpy_extras.io_utils import ExportHelper

sys.path.insert(0, str(Path(__file__).resolve().parent))
from export_bdae import export_model


def active_rig(context):
    obj = context.object
    if obj and obj.type == 'ARMATURE':
        return obj
    if obj and obj.type == 'MESH':
        return obj.find_armature()
    rigs = [o for o in context.scene.objects if o.type == 'ARMATURE']
    return rigs[0] if len(rigs) == 1 else None


def project_root():
    path = Path(bpy.data.filepath).parent if bpy.data.filepath else Path.cwd()
    return next((p for p in [path, *path.parents] if (p/'Data/Scenes/models.json').exists()), path)


class EXPORT_SCENE_OT_castle_bdae(bpy.types.Operator, ExportHelper):
    bl_idname = 'export_scene.castle_bdae'
    bl_label = 'Export Castle Frenzy BDAE'
    bl_options = {'PRESET'}
    filename_ext = '.bdae'
    filter_glob: StringProperty(default='*.bdae', options={'HIDDEN'})
    template: StringProperty(name='Original BDAE template', subtype='FILE_PATH')
    rig: StringProperty(name='Armature')
    texture_format: EnumProperty(name='Diffuse texture', items=[('png', 'PNG', 'Game texture')], default='png')

    def invoke(self, context, event):
        rig = active_rig(context)
        if rig:
            self.rig = rig.name
            template = rig.get('bdae_template')
            if not template and context.scene.get('bdae_import_report'):
                template = json.loads(context.scene['bdae_import_report']).get('source')
            if template:
                path = Path(template)
                self.template = str(path if path.is_absolute() else project_root()/path)
            self.filepath = str(project_root()/rig.get('bdae_export_output', 'build/manual-models/'+Path(bpy.data.filepath).stem+'.bdae'))
        return ExportHelper.invoke(self, context, event)

    def execute(self, context):
        try:
            result = export_model(bpy.path.abspath(self.template), bpy.path.abspath(self.filepath), self.rig or None, self.texture_format)
        except Exception as error:
            self.report({'ERROR'}, str(error))
            return {'CANCELLED'}
        self.report({'INFO'}, f'Exported {result["triangles"]} triangles and {len(result["clips"])} clips')
        return {'FINISHED'}


class ANIM_OT_castle_include_action(bpy.types.Operator):
    bl_idname = 'anim.castle_include_action'
    bl_label = 'Include current Action in BDAE export'
    bl_description = 'Mark an additional animation Action for game export; original game clips are always included'

    def execute(self, context):
        rig = active_rig(context)
        action = rig.animation_data.action if rig and rig.animation_data else None
        if action is None:
            self.report({'ERROR'}, 'Select a game armature with an active Action')
            return {'CANCELLED'}
        action['bdae_export'] = not action.get('bdae_export', False)
        action.use_fake_user = True
        return {'FINISHED'}


class VIEW3D_PT_castle_export(bpy.types.Panel):
    bl_label = 'Castle Frenzy BDAE'
    bl_idname = 'VIEW3D_PT_castle_export'
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'Game Export'

    def draw(self, context):
        layout = self.layout
        rig = active_rig(context)
        if rig is None:
            layout.label(text='Select the character or its armature')
            return
        layout.label(text=rig.name, icon='ARMATURE_DATA')
        layout.operator(EXPORT_SCENE_OT_castle_bdae.bl_idname, text='Export .bdae…', icon='EXPORT')
        action = rig.animation_data.action if rig.animation_data else None
        if context.scene.get('bdae_mesh_scene'):
            layout.label(text='Mesh export; original animation retained')
        elif action:
            layout.label(text='Action: '+action.name)
            layout.operator(ANIM_OT_castle_include_action.bl_idname,
                            text='Additional clip: included' if action.get('bdae_export', False) else 'Include as additional clip',
                            depress=bool(action.get('bdae_export', False)))
        layout.label(text='Save .blend, then run rebuild_Linux.sh')


CLASSES = [EXPORT_SCENE_OT_castle_bdae, ANIM_OT_castle_include_action, VIEW3D_PT_castle_export]


def export_menu(self, context):
    self.layout.operator(EXPORT_SCENE_OT_castle_bdae.bl_idname, text='Castle Frenzy (.bdae)')


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.TOPBAR_MT_file_export.append(export_menu)


def unregister():
    bpy.types.TOPBAR_MT_file_export.remove(export_menu)
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)


if __name__ == '__main__':
    register()
