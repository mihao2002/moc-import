import bpy
import sys
import math
import os
import argparse
import json
from mathutils import Vector, Matrix

# =================================================================================
# LINE ART UTILITIES
# =================================================================================

def setup_line_art(objects):
    """Setup Freestyle Line Art with Per-Object Color."""
    print("[Blender Debug] Setting up Freestyle Line Art...")
    
    # Create Collections for Line Coloring
    col_light = bpy.data.collections.get("LightParts")
    if not col_light:
        col_light = bpy.data.collections.new("LightParts")
        bpy.context.scene.collection.children.link(col_light)
        
    col_dark = bpy.data.collections.get("DarkParts")
    if not col_dark:
        col_dark = bpy.data.collections.new("DarkParts")
        bpy.context.scene.collection.children.link(col_dark)
    
    # Pre-process Geometry: Merge Vertices to remove internal borders
    # LDraw parts are often separate primitives. Freestyle draws 'Borders' at seams.
    # Merging them turns seams into 'Creases' which we can filter by angle.
    bpy.ops.object.select_all(action='DESELECT')
    
    for obj in objects:
        if obj.type != 'MESH':
            continue
            
        # Select and make active
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        
        # Merge by Distance
        # We can use geometry nodes or edit mode. Edit mode is reliable.
        # Check standard LDraw precision. 0.001 is usually safe.
        try:
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')
            # Increase threshold to 0.05 to aggressively merge LDraw primitives
            bpy.ops.mesh.remove_doubles(threshold=0.05)
            # Recalculate normals to ensure consistent shading after merge
            bpy.ops.mesh.normals_make_consistent(inside=False)
            bpy.ops.object.mode_set(mode='OBJECT')
        except Exception as e:
            print(f"[Blender Debug] Failed to merge vertices for {obj.name}: {e}")
            
        obj.select_set(False)

    # Classify Objects
    for obj in objects:
        if obj.type != 'MESH':
            continue
            
        is_dark = False
        if obj.data.materials:
            mat = obj.data.materials[0]
            if mat and mat.use_nodes and mat.node_tree:
                # Try to find Base Color in Principled BSDF
                bsdf = mat.node_tree.nodes.get("Principled BSDF")
                if bsdf:
                    c = bsdf.inputs['Base Color'].default_value
                    brightness = (c[0] * 0.299) + (c[1] * 0.587) + (c[2] * 0.114)
                    
                    # Debug Color Detection
                    print(f"[Blender Debug] LineArt Color Check: {obj.name} | RGB=({c[0]:.2f}, {c[1]:.2f}, {c[2]:.2f}) | Brightness={brightness:.2f}")

                    if brightness < 0.05: # Strict Threshold for "Black" parts only (Dark Grey is ~0.2)
                        is_dark = True
        
        # Link to appropriate collection
        # Note: Objects are already linked to Main Collection. We add them to these for Freestyle targeting.
        if is_dark:
            if obj.name not in col_dark.objects:
                col_dark.objects.link(obj)
        else:
            if obj.name not in col_light.objects:
                col_light.objects.link(obj)

    # Enable Freestyle
    bpy.context.scene.render.use_freestyle = True
    bpy.context.scene.render.line_thickness = 1.0 
    
    # Configure View Layer
    vl = bpy.context.view_layer
    vl.use_freestyle = True
    
    # Set Crease Angle for Filtering
    # 135 degrees (approx 2.356 rad) is standard to catch 90 degree corners but ignore flat/shallow triangulation
    if hasattr(vl, 'freestyle_settings'):
        vl.freestyle_settings.crease_angle = math.radians(130)
    
    # Apply Shade Smooth to avoid triangulation lines
    for obj in objects:
        if obj.type == 'MESH':
            # Shade Smooth to blend flat faces
            # In Blender 4.1+, this sets polygon smoothing.
            # We don't necessarily need "Auto Smooth" modifier if we trust Freestyle Crease Angle, 
            # but Auto Smooth helps shading.
            # For simplicity, just basic Smooth.
            for poly in obj.data.polygons:
                poly.use_smooth = True
    
    # Clear Default Line Sets
    if hasattr(vl, 'freestyle_settings'):
        fs = vl.freestyle_settings
        # Remove all existing line sets
        while fs.linesets:
            fs.linesets.remove(fs.linesets[0])
            
        # 1. Black Lines for Light Parts
        ls_black = fs.linesets.new("BlackLines")
        ls_black.select_by_collection = True
        ls_black.collection = col_light
        ls_black.select_silhouette = True
        ls_black.select_border = True
        ls_black.select_crease = True # Good for LEGO edges
        # ls_black.select_edge_mark = True
        
        # Create Black Style
        style_black = bpy.data.linestyles.new("StyleBlack")
        style_black.color = (0, 0, 0)
        style_black.thickness = 2.0
        ls_black.linestyle = style_black
        
        # 2. White Lines for Dark Parts
        ls_white = fs.linesets.new("WhiteLines")
        ls_white.select_by_collection = True
        ls_white.collection = col_dark
        ls_white.select_silhouette = True
        ls_white.select_border = True
        ls_white.select_crease = True
        
        # Create White Style
        style_white = bpy.data.linestyles.new("StyleWhite")
        style_white.color = (1, 1, 1)
        style_white.thickness = 2.0
        ls_white.linestyle = style_white
        
        print("[Blender Debug] Freestyle Setup Complete.")


# =================================================================================
# MAIN SCENE SETUP
# =================================================================================

def reset_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for collection in [bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights]:
        for block in collection:
            collection.remove(block)

def setup_scene(obj_path, output_path, resolution_x, resolution_y, cam_loc, cam_look_at, cam_up, override_color=None, save_blend=False, delta_path=None):
    print(f"[Blender Debug] STARTING SETUP for {obj_path} (Delta: {delta_path})")
    print(f"[Blender Debug] Script Version: LINE_ART_V1")
    sys.stdout.flush()
    reset_scene()

    # Generic Import Function
    def import_obj(path):
        try:
            bpy.ops.wm.obj_import(filepath=path, forward_axis='Y', up_axis='Z')
            return list(bpy.context.selected_objects)
        except Exception as e:
            print(f"[Blender Debug] Import FAILED for {path}: {e}")
            sys.stdout.flush()
            return []

    imported_objects = []
    
    if obj_path:
        imported_objects = import_obj(obj_path)
    
    delta_objects = []
    if delta_path:
        # Deselect old ones first
        bpy.ops.object.select_all(action='DESELECT')
        delta_objects = import_obj(delta_path)

    all_objects = imported_objects + delta_objects

    if not all_objects:
        print("[Blender Debug] CRITICAL: No imported objects found!")
        sys.stdout.flush()
        return

    # SEPARATION PASS: Ensure Multi-Material Objects (like Submodels) are split
    # so we can assign different Outline Colors to different parts.
    final_objects = []
    
    bpy.ops.object.select_all(action='DESELECT')
    for obj in all_objects:
        if obj.type == 'MESH':
            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            # Check if multiple materials exist
            if len(obj.data.materials) > 1:
                try:
                    bpy.ops.mesh.separate(type='MATERIAL')
                    # The separate op changes selection to include new parts
                    separated = bpy.context.selected_objects
                    final_objects.extend(separated)
                except Exception as e:
                    print(f"[Blender Debug] Separation failed for {obj.name}: {e}")
                    final_objects.append(obj)
            else:
                final_objects.append(obj)
            obj.select_set(False)
    
    all_objects = final_objects 
    # Update context with new objects for subsequent ops
    
    # Center Logic (Same as before)
    min_x, max_x = float('inf'), float('-inf')
    min_y, max_y = float('inf'), float('-inf')
    min_z, max_z = float('inf'), float('-inf')

    for obj in all_objects:
         for corner in obj.bound_box:
             v = obj.matrix_world @ Vector(corner)
             if v.x < min_x: min_x = v.x
             if v.x > max_x: max_x = v.x
             if v.y < min_y: min_y = v.y
             if v.y > max_y: max_y = v.y
             if v.z < min_z: min_z = v.z
             if v.z > max_z: max_z = v.z
    
    center_x = (min_x + max_x) / 2
    center_y = (min_y + max_y) / 2
    center_z = (min_z + max_z) / 2
    group_center = Vector((center_x, center_y, center_z))
    
    max_dist_sq = 0.0
    for obj in all_objects:
         for corner in obj.bound_box:
             v = obj.matrix_world @ Vector(corner)
             dist_sq = (v - group_center).length_squared
             if dist_sq > max_dist_sq:
                 max_dist_sq = dist_sq
    group_radius = math.sqrt(max_dist_sq)

    # DISABLE Auto-Centering for Manual Generator
    # main.ts already centers the geometry around the Global Model Center.
    # Re-centering here causes "shifting" as the bounding box of visible parts changes per step.
    # for obj in all_objects:
    #     obj.location.x -= center_x
    #     obj.location.y -= center_y
    #     obj.location.z -= center_z

    bpy.context.view_layer.update()
    
    # Camera Setup (Same as before)
    cam_data = bpy.data.cameras.new('Camera')
    cam_data.type = 'PERSP'
    fov_deg = 60.0
    fov_rad = math.radians(fov_deg)
    cam_data.angle = fov_rad 
    cam_data.clip_end = 5000 
    cam_data.clip_start = 0.1
    cam_obj = bpy.data.objects.new('Camera', cam_data)
    bpy.context.collection.objects.link(cam_obj)
    bpy.context.scene.camera = cam_obj
    
    loc_vec = Vector(cam_loc)
    look_at_vec = Vector(cam_look_at)
    up_vec = Vector(cam_up)

    direction = (look_at_vec - loc_vec).normalized()
    right = direction.cross(up_vec).normalized()
    real_up = right.cross(direction).normalized()

    rot_mat = Matrix((right, real_up, -direction)).transposed()
    cam_obj.location = loc_vec
    cam_obj.rotation_euler = rot_mat.to_euler()

    if group_radius > 0:
        vertical_fov = fov_rad
        aspect = float(resolution_x) / float(resolution_y)
        horizontal_fov = 2.0 * math.atan(math.tan(vertical_fov / 2.0) * aspect)
        distance_v = group_radius / math.tan(vertical_fov / 2.0)
        distance_h = group_radius / math.tan(horizontal_fov / 2.0)
        base_distance = max(distance_v, distance_h)
        near_clip = cam_data.clip_start
        final_distance = max(base_distance + near_clip, base_distance * 1.4)
        new_loc = group_center - (direction * final_distance)
        cam_obj.location = new_loc

    # ===========================================================================
    # OVERRIDE COLOR (For Parts/Single Item Renders)
    # ===========================================================================
    if override_color:
        # Smart Override: Only modify the "Main Color" (code_16) material.
        # This preserves fixed colors (like code_4 Red) in submodels.
        
        target_mats = [m for m in bpy.data.materials if m.name.startswith("code_16")]
        
        if target_mats:
            r = override_color[0]
            g = override_color[1]
            b = override_color[2]
            
            for mat in target_mats:
                mat.use_nodes = True
                bsdf = mat.node_tree.nodes.get("Principled BSDF")
                if bsdf:
                    # Revert sRGB conversion to match the vibrant "Step" style
                    bsdf.inputs['Base Color'].default_value = (r, g, b, 1.0)
                    # Higher roughness for flatter, more cartoon/instruction look
                    bsdf.inputs['Roughness'].default_value = 0.8
                    # Low Specular to avoid greying out
                    bsdf.inputs['Specular IOR Level'].default_value = 0.2
            
            print(f"[Blender Debug] Applied Override Color to {len(target_mats)} materials (code_16).")
        else:
            print("[Blender Debug] No 'code_16' materials found. Skipping color override.")

    # ===========================================================================
    # LIGHTING STAGE - AMBIENT ONLY
    # ===========================================================================
    
    # 1. World Background: Strong Ambient
    world = bpy.context.scene.world
    if not world:
        world = bpy.data.worlds.new("World")
        bpy.context.scene.world = world

    try:
        if hasattr(world, 'use_nodes'):
             if not world.use_nodes:
                 world.use_nodes = True
        
        w_tree = getattr(world, 'node_tree', None)
        if w_tree and 'Background' in w_tree.nodes:
            bg_node = w_tree.nodes['Background']
            bg_node.inputs[0].default_value = (1, 1, 1, 1) # White
            bg_node.inputs[1].default_value = 1.0 # Full Ambient Strength
    except Exception as e:
        print(f"[Blender Debug] World Setup Failed: {e}")

    # No Directional Lights (Key/Fill/Rim removed)

    # ===========================================================================
    # LINE ART STAGE
    # ===========================================================================
    setup_line_art(all_objects)

    # Render Settings
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = resolution_x
    scene.render.resolution_y = resolution_y
    scene.render.film_transparent = True
    scene.render.filepath = output_path
    
    # Use Standard transform to avoid Filmic desaturation
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'

    if save_blend:
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
    parser.add_argument('--input', required=False)
    parser.add_argument('--model_new', required=False)
    parser.add_argument('--output', required=False)
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
    parser.add_argument('--save_blend', action='store_true')
    parser.add_argument('--batch', type=str, required=False)
    
    args = parser.parse_args(args)
    
    def srgb_to_linear(c):
        if c <= 0.04045:
            return c / 12.92
        else:
            return pow((c + 0.055) / 1.055, 2.4)

    if args.batch:
        try:
            with open(args.batch, 'r') as f:
                batch_jobs = json.load(f)
            
            for job in batch_jobs:
                job_color = None
                if 'color' in job and job['color']:
                    c = job['color']
                    job_color = (
                        srgb_to_linear(c['r']), 
                        srgb_to_linear(c['g']), 
                        srgb_to_linear(c['b'])
                    )
                setup_scene(
                    job.get('input'),
                    job['output'],
                    job['width'],
                    job['height'],
                    (job['locX'], job['locY'], job['locZ']),
                    (job['lookAtX'], job['lookAtY'], job['lookAtZ']),
                    (job['upX'], job['upY'], job['upZ']),
                    job_color,
                    save_blend=job.get('save_blend', False),
                    delta_path=job.get('delta_input')
                )
        except Exception as e:
             print(f"[Blender Debug] Batch Processing Failed: {e}")
             sys.exit(1)
             
    else:            
        if not args.input and not args.batch and not args.model_new:
            print("[Blender Debug] Error: --input or --batch or --model_new required.")
            sys.exit(1)

        color = None
        if args.r is not None and args.g is not None and args.b is not None:
            color = (args.r, args.g, args.b)

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
            color_linear,
            save_blend=args.save_blend,
            delta_path=args.model_new
        )
