"""Shared helpers for the Wick Blender toolkit (Blender 4.5, run headless).

Conventions (docs/asset-production.md, The look decoded):
  1 Blender unit = 1 m, Z up, characters face -Y, pivot at the base centre.
  One material per asset: every part is UV-mapped into a swatch of a painted palette atlas
  (top-down gradient painted in), so a whole character is one texture and one draw.
  Rig: the shared Wick skeleton (root, body, head, arm.L/R, foot.L/R, flame_base/mid/tip,
  eye.L/R) with rigid per-part weights.
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
INCOMING = os.path.join(REPO, "unity", "Assets", "_Wick", "Art", "Incoming")
PREVIEWS = os.path.join(REPO, "tools", "blender", "previews")


def hexc(h):
    """sRGB hex to a 0-1 sRGB tuple."""
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


# ---- Scene -----------------------------------------------------------------------------------

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s = bpy.context.scene
    s.unit_settings.system = "METRIC"
    s.unit_settings.scale_length = 1.0
    s.render.fps = 30


def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


def mesh_object(name, verts, faces):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], faces)
    me.validate()
    me.update()
    return link(bpy.data.objects.new(name, me))


def apply_all(obj):
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.selected_objects:
        o.select_set(False)
    obj.select_set(True)
    for m in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def shade_smooth(obj):
    for p in obj.data.polygons:
        p.use_smooth = True


# ---- Shapes ----------------------------------------------------------------------------------

def superellipse(n, a, b, power=4.0):
    """n points of a rounded square (power 2 = ellipse, higher = squarer)."""
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        c, s = math.cos(t), math.sin(t)
        x = a * math.copysign(abs(c) ** (2 / power), c)
        y = b * math.copysign(abs(s) ** (2 / power), s)
        pts.append((x, y))
    return pts


def loft(name, rings, cap_bottom=True, cap_top=True):
    """rings: list of (z, [(x, y), ...]) with equal point counts, bottom to top."""
    n = len(rings[0][1])
    verts, faces = [], []
    for z, pts in rings:
        verts += [(x, y, z) for x, y in pts]
    for r in range(len(rings) - 1):
        for i in range(n):
            a, b = r * n + i, r * n + (i + 1) % n
            faces.append((a, b, b + n, a + n))
    if cap_bottom:
        cz = rings[0][0]
        verts.append((0, 0, cz))
        c = len(verts) - 1
        faces += [(c, (i + 1) % n, i) for i in range(n)]
    if cap_top:
        cz = rings[-1][0]
        top = (len(rings) - 1) * n
        verts.append((0, 0, cz))
        c = len(verts) - 1
        faces += [(c, top + i, top + (i + 1) % n) for i in range(n)]
    obj = mesh_object(name, verts, faces)
    shade_smooth(obj)
    return obj


def lathe(name, profile, segments, offset=(0, 0, 0), sweep=None, cap=True):
    """Surface of revolution. profile: [(radius, z)] bottom to top; sweep(t) -> (dx, dy) bends it."""
    rings = []
    k = len(profile) - 1
    for i, (r, z) in enumerate(profile):
        t = i / k if k else 0
        dx, dy = sweep(t) if sweep else (0, 0)
        rings.append((z + offset[2], [(offset[0] + dx + r * math.cos(2 * math.pi * s / segments),
                                       offset[1] + dy + r * math.sin(2 * math.pi * s / segments)) for s in range(segments)]))
    return loft(name, rings, cap_bottom=cap, cap_top=cap)


def blob(name, center, radii, seg=10, rings=7, rot=(0, 0, 0)):
    """An ellipsoid (UV sphere) at center with per-axis radii."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1.0, location=center, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = radii
    apply_all(obj)
    shade_smooth(obj)
    return obj


def capsule(name, a, b, radius, seg=8):
    """A rounded tube from point a to point b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    obj = blob(name, (0, 0, 0), (radius, radius, d.length / 2 + radius), seg=seg, rings=8)
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized())
    obj.matrix_world = Matrix.Translation((a + b) / 2) @ rot.to_matrix().to_4x4()
    apply_all(obj)
    return obj


def box(name, center, size, bevel=0.0, segments=2, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=center, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    apply_all(obj)
    if bevel > 0:
        m = obj.modifiers.new("bevel", "BEVEL")
        m.width = bevel
        m.segments = segments
        m.limit_method = "NONE"
        apply_all(obj)
    shade_smooth(obj)
    return obj


def cylinder(name, center, radius, depth, seg=12, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=seg, radius=radius, depth=depth, location=center, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    apply_all(obj)
    return obj


def tube_along(name, points, radius, seg=6):
    """A tube through a list of points (brows, mouths, straps), rounded at both ends."""
    verts, faces = [], []
    pts = [Vector(p) for p in points]
    n = len(pts)
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        up = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        x = t.cross(up).normalized()
        y = t.cross(x).normalized()
        taper = math.sin(math.pi * (0.15 + 0.7 * i / max(1, n - 1)))  # thinner at the ends
        r = radius * (0.55 + 0.45 * taper)
        for s in range(seg):
            a = 2 * math.pi * s / seg
            verts.append(tuple(p + (x * math.cos(a) + y * math.sin(a)) * r))
    for i in range(n - 1):
        for s in range(seg):
            a, b = i * seg + s, i * seg + (s + 1) % seg
            faces.append((a, b, b + seg, a + seg))
    faces.append(tuple(range(seg - 1, -1, -1)))
    faces.append(tuple(range((n - 1) * seg, n * seg)))
    obj = mesh_object(name, verts, faces)
    shade_smooth(obj)
    return obj


def arc(a, b, bulge, n=8, towards=(0, -1, 0)):
    """Points on a curve from a to b bowed by `bulge` (positive bows up)."""
    a, b = Vector(a), Vector(b)
    pts = []
    for i in range(n):
        t = i / (n - 1)
        p = a.lerp(b, t)
        p.z += bulge * math.sin(math.pi * t)
        pts.append(tuple(p))
    return pts


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name
    return obj


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# ---- Palette atlas -----------------------------------------------------------------------------

CELLS = 8  # 8 x 8 swatches on a 1024 px atlas


class Palette:
    """Named swatches; each swatch is a cell with the look's painted top-down gradient."""

    def __init__(self, colours):
        self.names = list(colours.keys())
        self.colours = colours

    def cell(self, name):
        i = self.names.index(name)
        return i % CELLS, i // CELLS

    def paint(self, path, size=1024, mask=False):
        """Writes the albedo (or the mask: G = rim strength, R = emissive) as a PNG."""
        img = bpy.data.images.new(os.path.basename(path), size, size, alpha=False)
        px = [0.0] * (size * size * 4)
        cs = size // CELLS
        for name in self.names:
            cx, cy = self.cell(name)
            col = self.colours[name]
            for y in range(cs):
                v = y / (cs - 1)
                if mask:
                    rgb = (1.0 if name.startswith("glow") else 0.0, 0.85 if name.startswith("wax") else 0.35, 0.0)
                else:
                    # About 15% lighter at the top, 20% darker at the base, softer in between.
                    k = 0.80 + 0.35 * (v ** 0.8)
                    rgb = tuple(min(1.0, c * k) for c in col)
                    rgb = tuple(c ** 2.2 for c in rgb)  # Blender images take linear values
                row = (cy * cs + y) * size
                for x in range(cs):
                    i = (row + cx * cs + x) * 4
                    px[i:i + 4] = (rgb[0], rgb[1], rgb[2], 1.0)
        img.pixels[:] = px
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
        return img


def uv_to_cell(obj, palette, swatch, margin=0.12):
    """Maps a part's UVs into its swatch: u around the part, v up its height (so the gradient reads top-down)."""
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    zs = [v.co.z for v in me.vertices]
    zmin, zmax = min(zs), max(zs)
    cx, cy = palette.cell(swatch)
    for poly in me.polygons:
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            v = (co.z - zmin) / (zmax - zmin) if zmax > zmin else 0.5
            u = 0.5 + 0.5 * math.atan2(co.y - obj.location.y, co.x - obj.location.x) / math.pi
            uv[li].uv = ((cx + margin + u * (1 - 2 * margin)) / CELLS, (cy + margin + v * (1 - 2 * margin)) / CELLS)


def atlas_material(name, albedo_path):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(albedo_path, check_existing=True)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8
    return mat


# ---- Rig ---------------------------------------------------------------------------------------

def armature(bones):
    """bones: [(name, head, tail, parent)] in metres. Returns the armature object named "Armature"."""
    arm = bpy.data.armatures.new("Armature")
    obj = link(bpy.data.objects.new("Armature", arm))
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, parent in bones:
        b = arm.edit_bones.new(name)
        b.head, b.tail = head, tail
        b.roll = 0.0
        if parent:
            b.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in obj.pose.bones:
        pb.rotation_mode = "XYZ"
    return obj


def weight_all(obj, bone):
    """Every vertex of a part follows one bone (rigid parts: eyes, hands, feet, satchel)."""
    g = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
    g.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")


def weight_by_height(obj, bands):
    """bands: [(z_from, z_to, bone)] bottom to top; vertices take the band they're in."""
    groups = {b: (obj.vertex_groups.get(b) or obj.vertex_groups.new(name=b)) for _, _, b in bands}
    for v in obj.data.vertices:
        z = v.co.z
        for lo, hi, b in bands:
            if lo <= z <= hi:
                groups[b].add([v.index], 1.0, "REPLACE")
                break
        else:
            groups[bands[-1][2] if z > bands[-1][1] else bands[0][2]].add([v.index], 1.0, "REPLACE")


def tag(obj, name):
    """A marker group (not a bone) so face shape keys can find a part after the join."""
    g = obj.vertex_groups.new(name="fx_" + name)
    g.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")


def bind(mesh, arm):
    mesh.parent = arm
    m = mesh.modifiers.new("Armature", "ARMATURE")
    m.object = arm


def marked(obj, name):
    g = obj.vertex_groups.get("fx_" + name)
    if g is None:
        return []
    out = []
    for v in obj.data.vertices:
        for ge in v.groups:
            if ge.group == g.index and ge.weight > 0.5:
                out.append(v.index)
    return out


def shape_key(obj, name, moves):
    """moves: [(marker, fn(co) -> new co)] applied to the basis to make a named shape key."""
    if obj.data.shape_keys is None:
        obj.shape_key_add(name="Basis")
    key = obj.shape_key_add(name=name, from_mix=False)
    basis = obj.data.shape_keys.key_blocks["Basis"].data
    for marker, fn in moves:
        for i in marked(obj, marker):
            key.data[i].co = fn(Vector(basis[i].co))
    return key


def drop_markers(obj):
    for g in list(obj.vertex_groups):
        if g.name.startswith("fx_"):
            obj.vertex_groups.remove(g)


# ---- Animation ---------------------------------------------------------------------------------

def action(arm, name, frames, keys, loop):
    """keys: {bone: {"rot": [(frame, (x,y,z) degrees)], "scale": [(f, (x,y,z))], "loc": [(f, (x,y,z))]}}"""
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    for bone, chans in keys.items():
        pb = arm.pose.bones[bone]
        for f, deg in chans.get("rot", []):
            pb.rotation_euler = tuple(math.radians(d) for d in deg)
            pb.keyframe_insert("rotation_euler", frame=f, group=bone)
        for f, s in chans.get("scale", []):
            pb.scale = s
            pb.keyframe_insert("scale", frame=f, group=bone)
        for f, l in chans.get("loc", []):
            pb.location = l
            pb.keyframe_insert("location", frame=f, group=bone)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)
        pb.location = (0, 0, 0)
    act.frame_range = (0, frames)
    act["wick_loop"] = 1 if loop else 0
    return act


# ---- Export and previews -----------------------------------------------------------------------

def export_fbx(path, with_animation):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False, primary_bone_axis="Y",
        secondary_bone_axis="X", use_armature_deform_only=True, armature_nodetype="NULL",
        bake_anim=with_animation, bake_anim_use_all_actions=with_animation, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode="STRIP")


def previews(asset_id, albedo_path, out_dir):
    """Front, three-quarter and side renders, plus the 64 px silhouette test."""
    os.makedirs(out_dir, exist_ok=True)
    s = bpy.context.scene
    s.render.engine = "BLENDER_EEVEE_NEXT"
    s.render.resolution_x = s.render.resolution_y = 768
    s.render.film_transparent = False
    world = bpy.data.worlds.new("Vault")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.02, 0.018, 0.05, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    s.world = world
    sun = link(bpy.data.objects.new("Key", bpy.data.lights.new("Key", "SUN")))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    warm = link(bpy.data.objects.new("Flame light", bpy.data.lights.new("Flame light", "POINT")))
    warm.data.energy = 25
    warm.data.color = hexc("FF9F1C")
    warm.location = (0.25, -0.35, 0.75)

    cam = link(bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera")))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 1.0
    s.camera = cam
    views = {"front": (0, -3, 0.4, 90, 0), "three-quarter": (1.8, -2.4, 0.9, 77, 37), "side": (3, 0, 0.4, 90, 90)}
    for label, (x, y, z, pitch, yaw) in views.items():
        cam.location = (x, y, z)
        cam.rotation_euler = (math.radians(pitch), 0, math.radians(yaw))
        s.render.filepath = os.path.join(out_dir, asset_id + "-" + label + ".png")
        bpy.ops.render.render(write_still=True)

    # 64 px silhouette: everything black on white, front view.
    for o in s.objects:
        if o.type == "MESH":
            o.hide_render = False
    s.render.resolution_x = s.render.resolution_y = 64
    world.node_tree.nodes["Background"].inputs[0].default_value = (1, 1, 1, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    black = bpy.data.materials.new("Silhouette")
    black.use_nodes = True
    bs = black.node_tree.nodes.get("Principled BSDF")
    bs.inputs["Base Color"].default_value = (0, 0, 0, 1)
    bs.inputs["Roughness"].default_value = 1.0
    saved = {}
    for o in s.objects:
        if o.type == "MESH":
            saved[o.name] = list(o.data.materials)
            o.data.materials.clear()
            o.data.materials.append(black)
    sun.data.energy = 0
    warm.data.energy = 0
    cam.location = (0, -3, 0.4)
    cam.rotation_euler = (math.radians(90), 0, 0)
    s.render.filepath = os.path.join(out_dir, asset_id + "-silhouette-64.png")
    bpy.ops.render.render(write_still=True)
    for o in s.objects:
        if o.name in saved:
            o.data.materials.clear()
            for m in saved[o.name]:
                o.data.materials.append(m)
