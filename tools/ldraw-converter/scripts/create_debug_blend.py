import bpy
import os
import math

# Clear existing objects
bpy.ops.wm.read_factory_settings(use_empty=True)

# Import OBJ
obj_path = os.path.abspath("output_test2_final5/temp/main.ldr_step_1.obj")
if os.path.exists(obj_path):
    # Blender 4.0+ uses wm.obj_import
    if hasattr(bpy.ops.wm, 'obj_import'):
        bpy.ops.wm.obj_import(filepath=obj_path)
    else:
        # Fallback for older versions (just in case)
        bpy.ops.import_scene.obj(filepath=obj_path)
        
    imported_objects = bpy.context.selected_objects
    for obj in imported_objects:
        obj.location = (0, 0, 0) # Ensure no extra offset
else:
    print(f"Error: OBJ not found at {obj_path}")

# Setup Camera
cam_data = bpy.data.cameras.new(name='Camera')
cam_obj = bpy.data.objects.new(name='Camera', object_data=cam_data)
bpy.context.scene.collection.objects.link(cam_obj)
bpy.context.view_layer.objects.active = cam_obj
cam_obj.select_set(True)

# Camera Settings from Log (0, 0, 0 input -> 0, -550, 0 loc)
# Location: (0, -550, 0)
# Looking at (0, 0, 0) with Up (0, 0, -1)
# In Blender, Camera points down -Z, Up is +Y.
# We need to rotate the camera object to match our look vector.
# To look along +Y (from -Y), with Up -Z.
# Standard Camera: -Z view, +Y up.
# We want: +Y view, -Z up.
# Rotate X by 90 (to look -Y -> -Z? No.)

# Using LookAt Logic is safer
cam_obj.location = (0, -550, 0)
target = (0, 0, 0)
up_vector = (0, 0, -1)

import mathutils

# Setup Camera Rotation
# We want Camera -Z to point to Target (0,0,0) from (0, -550, 0) => Direction +Y
# We want Camera +Y to point roughly to -Z (Up)
direction = mathutils.Vector((0, 1, 0)) # Looking +Y (from -550 to 0)
up_vector = mathutils.Vector((0, 0, -1)) # Top is -Z

# To track quat: 'TRACK_AXIS', 'UP_AXIS'
# We want -Z of camera to track Target (+Y)
# We want +Y of camera to be Up (-Z)
rot_quat = direction.to_track_quat('-Z', 'Y')

cam_obj.rotation_mode = 'QUATERNION'
cam_obj.rotation_quaternion = rot_quat

# Set Orthographic
cam_data.type = 'ORTHO'
cam_data.ortho_scale = 120

# Save Blend
save_path = os.path.abspath("output_test2_final5/debug_step_1.blend")
bpy.ops.wm.save_as_mainfile(filepath=save_path)
print(f"Saved debug file to {save_path}")
