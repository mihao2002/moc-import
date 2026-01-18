import bpy
import sys
import math
import os
import argparse
from mathutils import Vector, Matrix

# Blender Internal Utilities
# ...

def reset_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for collection in [bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights]:
        for block in collection:
            collection.remove(block)

def setup_scene(obj_path, output_path, resolution_x, resolution_y, cam_loc, cam_look_at, cam_up, override_color=None):
    print(f"[Blender Debug] STARTING SETUP for {obj_path}")
    sys.stdout.flush()
    reset_scene()

    # Import OBJ with Explicit Orientation (Identity Mapping: LDraw(x,y,z) -> Blender(x,y,z))
    # forward_axis='Y' ensures +Y is Forward (LDraw +Y is Down, but we want to map axes 1:1)
    # The separate objects will be imported as separate Blender objects.
    try:
        bpy.ops.wm.obj_import(filepath=obj_path, forward_axis='Y', up_axis='Z')
    except Exception as e:
        print(f"[Blender Debug] Import FAILED: {e}")
        sys.stdout.flush()
        return

    imported_object = None
    
    # Strategy 1: Check selected objects (Importer usually selects them)
    # Identify Imported Objects
    imported_objects = list(bpy.context.selected_objects)
    
    # Fallback: If nothing selected, scan all meshes (Scene was reset)
    if not imported_objects:
        for obj in bpy.data.objects:
            if obj.type == 'MESH':
                imported_objects.append(obj)
    
    if imported_objects:
        print(f"[Blender Debug] Imported {len(imported_objects)} objects.")
        for obj in imported_objects:
             # Ensure we track them
             obj.select_set(True)
        bpy.context.view_layer.objects.active = imported_objects[0]
    else:
        print("[Blender Debug] CRITICAL: No imported objects found!")
        sys.stdout.flush()
    
    # Center Objects as a GROUP at (0, 0, 0)
    # Calculate group bounds
    group_center = Vector((0, 0, 0))
    group_radius = 0.0

    if imported_objects:
        min_x, max_x = float('inf'), float('-inf')
        min_y, max_y = float('inf'), float('-inf')
        min_z, max_z = float('inf'), float('-inf')

        for obj in imported_objects:
             for corner in obj.bound_box:
                 v = obj.matrix_world @ Vector(corner)
                 if v.x < min_x: min_x = v.x
                 if v.x > max_x: max_x = v.x
                 if v.y < min_y: min_y = v.y
                 if v.y > max_y: max_y = v.y
                 if v.z < min_z: min_z = v.z
                 if v.z > max_z: max_z = v.z
        
        print(f"[Blender Debug] Group Bounds: X[{min_x:.2f}, {max_x:.2f}] Y[{min_y:.2f}, {max_y:.2f}] Z[{min_z:.2f}, {max_z:.2f}]")
        
        center_x = (min_x + max_x) / 2
        center_y = (min_y + max_y) / 2
        center_z = (min_z + max_z) / 2
        group_center = Vector((center_x, center_y, center_z))
        
        # Calculate Radius (Furthest point from/to center)
        # Actually, simple box radius might suffice, but let's be safe
        # Max distance from center to any box corner
        max_dist_sq = 0.0
        for obj in imported_objects:
             for corner in obj.bound_box:
                 v = obj.matrix_world @ Vector(corner)
                 dist_sq = (v - group_center).length_squared
                 if dist_sq > max_dist_sq:
                     max_dist_sq = dist_sq
        group_radius = math.sqrt(max_dist_sq)

        print(f"[Blender Debug] Centering Group from ({center_x:.2f}, {center_y:.2f}, {center_z:.2f}) to (0,0,0) | Radius: {group_radius:.2f}")

        # Move all objects
        for obj in imported_objects:
            obj.location.x -= center_x
            obj.location.y -= center_y
            obj.location.z -= center_z

        # Update Scene
        bpy.context.view_layer.update()
    
    if override_color:
        # Create a new material with the override color
        mat = bpy.data.materials.new(name="OverrideColor")
        mat.use_nodes = True
        nodes = mat.node_tree.nodes
        bsdf = nodes.get("Principled BSDF")
        if bsdf:
            # Alpha 1.0
            bsdf.inputs['Base Color'].default_value = (override_color[0], override_color[1], override_color[2], 1.0)
            bsdf.inputs['Roughness'].default_value = 0.5
        
        # Apply to all imported objects
        for obj in imported_objects:
            if obj.type == 'MESH':
                # Clear existing slots
                obj.data.materials.clear()
                obj.data.materials.append(mat)
    
    # Setup Camera
    cam_data = bpy.data.cameras.new('Camera')
    cam_data.type = 'PERSP'
    # Unity Reference: fieldOfView = 60 (Vertical)
    # Since we use 1:1 aspect ratio, Vertical FOV = Horizontal FOV = 60
    fov_deg = 60.0
    fov_rad = math.radians(fov_deg)
    cam_data.angle = fov_rad 
    cam_data.clip_end = 5000 
    cam_data.clip_start = 0.1 # Unity: 0.01
    cam_obj = bpy.data.objects.new('Camera', cam_data)
    bpy.context.collection.objects.link(cam_obj)
    bpy.context.scene.camera = cam_obj
    
    # Camera Transform
    loc_vec = Vector(cam_loc)
    look_at_vec = Vector(cam_look_at)
    up_vec = Vector(cam_up)

    # Calculate direction, right, and adjusted up
    direction = (look_at_vec - loc_vec).normalized()
    right = direction.cross(up_vec).normalized()
    real_up = right.cross(direction).normalized()

    # Blender Camera: -Z is forward, +Y is up, +X is right
    # Construct Rotation Matrix
    # Cols: X=Right, Y=Real_Up, Z=-Direction
    rot_mat = Matrix((right, real_up, -direction)).transposed()
    
    # Apply Transform
    cam_obj.location = loc_vec
    cam_obj.rotation_euler = rot_mat.to_euler()

    print(f"[Blender Debug] Camera Location: {cam_obj.location}")
    print(f"[Blender Debug] Camera Rotation: {cam_obj.rotation_euler}")
    print(f"[Blender Debug] Camera Matrix World:\n{cam_obj.matrix_world}")
    print(f"[Blender Debug] FOV: {fov_deg} deg")

    # ---------------------------------------------------------
    # Unity Reference Distance Calculation (LDrawCamera.cs)
    # ---------------------------------------------------------
    print(f"[Blender Debug] Group Radius: {group_radius:.2f}")
    sys.stdout.flush()

    if group_radius > 0:
        # Unity: verticalFOV = 60 * Deg2Rad
        vertical_fov = fov_rad
        # aspect was hardcoded to 1.0, but now we respect resolution
        aspect = float(resolution_x) / float(resolution_y)
        
        # Unity: horizontalFOV = 2 * Atan(Tan(vFOV/2) * aspect)
        horizontal_fov = 2.0 * math.atan(math.tan(vertical_fov / 2.0) * aspect)
        
        # Unity: distanceV = radius / Tan(vFOV / 2)
        distance_v = group_radius / math.tan(vertical_fov / 2.0)
        
        # Unity: distanceH = radius / Tan(hFOV / 2)
        distance_h = group_radius / math.tan(horizontal_fov / 2.0)
        
        # Unity: distance = Max(distanceV, distanceH)
        base_distance = max(distance_v, distance_h)
        
        # Unity: distance = Max(distance + nearClip, distance * 1.2)
        # User requested adjustment for truncation, increasing margin to 1.3
        near_clip = cam_data.clip_start
        final_distance = max(base_distance + near_clip, base_distance * 1.3)
        
        print(f"[Blender Debug] Reference Auto-Distance: {final_distance:.2f} (Radius: {group_radius:.2f}, FOV: 60)")
        sys.stdout.flush()
        
        # Apply new distance along the camera's Z axis (backwards from target)
        new_loc = Vector((0,0,0)) - (direction * final_distance)
        cam_obj.location = new_loc
        
        print(f"[Blender Debug] Adjusted Camera Location: {new_loc}")

    # Lighting Setup (Standard Fixed 3-Point around Origin)
    
    # 1. Key Light (Sun)
    key_light_data = bpy.data.lights.new(name="Key Light", type='SUN')
    key_light_data.energy = 2.0 
    key_light = bpy.data.objects.new(name="Key Light", object_data=key_light_data)
    bpy.context.collection.objects.link(key_light)
    key_light.location = (10, -10, 10) 
    key_light.rotation_euler = (math.radians(45), math.radians(0), math.radians(45))

    # 2. Fill Light (Area)
    fill_light_data = bpy.data.lights.new(name="Fill Light", type='AREA')
    fill_light_data.energy = 500
    fill_light_data.size = 10
    fill_light = bpy.data.objects.new(name="Fill Light", object_data=fill_light_data)
    bpy.context.collection.objects.link(fill_light)
    fill_light.location = (-10, -5, 5)
    # Point at origin approximately
    fill_light.rotation_euler = (math.radians(60), math.radians(0), math.radians(-45))

    # 3. Back/Rim Light (Sun)
    back_light_data = bpy.data.lights.new(name="Back Light", type='SUN')
    back_light_data.energy = 1.0
    back_light = bpy.data.objects.new(name="Back Light", object_data=back_light_data)
    bpy.context.collection.objects.link(back_light)
    back_light.location = (0, 10, 5) 
    back_light.rotation_euler = (math.radians(-135), math.radians(0), math.radians(180))

    # World / Ambient
    world = bpy.context.scene.world
    if not world:
        world = bpy.data.worlds.new("World")
        bpy.context.scene.world = world
    world.use_nodes = True
    bg_node = world.node_tree.nodes['Background']
    bg_node.inputs[0].default_value = (1, 1, 1, 1) 
    bg_node.inputs[1].default_value = 0.25 # Reduced from 0.4

    # Render Settings
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = resolution_x
    scene.render.resolution_y = resolution_y
    scene.render.film_transparent = True
    scene.render.filepath = output_path
    
    # Color Management: Standard (avoid Filmic desaturation)
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'

    # Eevee Settings for better quality
    # scene.eevee.taa_render_samples = 64
    # scene.eevee.use_gtao = True # Ambient Occlusion equivalent
    
    # Save Blend file for debugging
    blend_path = output_path.replace(".png", ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"[Blender Debug] Saved .blend file to {blend_path}")

    bpy.ops.render.render(write_still=True)

if __name__ == "__main__":
    argv = sys.argv
    if "--" not in argv:
        sys.exit(1)
        
    args = argv[argv.index("--") + 1:]
    
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--width', type=int, default=512)
    parser.add_argument('--height', type=int, default=512)
    
    parser.add_argument('--locX', type=float, default=0)
    parser.add_argument('--locY', type=float, default=0)
    parser.add_argument('--locZ', type=float, default=0)
    
    parser.add_argument('--lookAtX', type=float, default=0)
    parser.add_argument('--lookAtY', type=float, default=0)
    parser.add_argument('--lookAtZ', type=float, default=0)
    
    parser.add_argument('--upX', type=float, default=0)
    parser.add_argument('--upY', type=float, default=1)
    parser.add_argument('--upZ', type=float, default=0)
    
    parser.add_argument('--r', type=float, required=False)
    parser.add_argument('--g', type=float, required=False)
    parser.add_argument('--b', type=float, required=False)
    
    args = parser.parse_args(args)
    
    color = None
    if args.r is not None and args.g is not None and args.b is not None:
        color = (args.r, args.g, args.b)

    # Convert sRGB (0-1) to Linear RGB for Blender
    def srgb_to_linear(c):
        if c <= 0.04045:
            return c / 12.92
        else:
            return pow((c + 0.055) / 1.055, 2.4)

    color_linear = None
    if color:
        color_linear = (
            srgb_to_linear(color[0]), 
            srgb_to_linear(color[1]), 
            srgb_to_linear(color[2])
        )

    setup_scene(
        args.input, 
        args.output, 
        args.width, 
        args.height,
        (args.locX, args.locY, args.locZ),
        (args.lookAtX, args.lookAtY, args.lookAtZ),
        (args.upX, args.upY, args.upZ),
        color_linear
    )
