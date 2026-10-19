"""Builds Wick and his skins as rigged, game-ready blockouts from the locked references.

Usage (repo root):
    blender -b --factory-startup --python tools/blender/build_skins.py -- [asset_id ...] [--no-previews]

With no ids it builds them all. Each skin is exported to
unity/Assets/_Wick/Art/Incoming/char/<id>/ as <id>.fbx, <id>_albedo.png, <id>_mask.png and SOURCES.md,
and previews (front, three-quarter, side, 64 px silhouette) go to tools/blender/previews/<id>/.

These are scripted blockouts: the references' shapes, proportions, colours and face, on the shared rig
with the five face shapes and (for char_wick_default) the twelve clips. They are meant to be replaced
or refined by an artist; they never ship the hero renders' glossy detail.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

# Palette (docs/art-direction.md plus the references' face colours).
SWATCHES = {
    "wax": C.hexc("F7E6C2"), "wax_honey": C.hexc("F2D07A"), "wax_gold": C.hexc("F2C14E"),
    "drip": C.hexc("F8EDD2"), "wick": C.hexc("3A2A1E"),
    "eye_white": C.hexc("FBF7EF"), "iris": C.hexc("9C5A1C"), "pupil": C.hexc("1B120D"), "glow_highlight": C.hexc("FFFFFF"),
    "brow": C.hexc("3B2416"), "mouth": C.hexc("4A1C14"), "tongue": C.hexc("E27C70"), "teeth": C.hexc("FBF7EF"),
    "cheek": C.hexc("EFA090"), "leather": C.hexc("7B4A27"), "strap": C.hexc("5C3620"), "gold": C.hexc("F2C14E"),
    "gem": C.hexc("D93A45"), "tin": C.hexc("B9BEC6"), "mask": C.hexc("2A1E19"), "rope": C.hexc("B88A55"),
    "flame": C.hexc("FF9F1C"),
}

SKINS = {
    "char_wick_default": dict(wax="wax", satchel=True, brows="confident", mouth="half_smile", gaze=0.010, anim=True),
    "char_drippy": dict(wax="wax_honey", satchel=True, brows="worried", mouth="grimace", gaze=0.012, drips=11, puddle=True,
                        flame_sweep=0.05, flame_back=0.05),
    "char_cinder": dict(wax="wax", satchel=True, rope=True, mask=True, brows="sly", mouth="smirk", gaze=0.016),
    "char_pip": dict(wax="wax", cup=True, height=0.45, width=0.95, flame=2.0, brows="determined", mouth="grin_open", gaze=-0.006,
                     satchel=False, eye_scale=1.15),
    "char_twist": dict(wax="wax", spiral=True, satchel=False, brows="sharp", mouth="toothy", gaze=0.014, flame_curl=True),
    "char_ember": dict(wax="wax", height=0.7, width=1.3, flame=1.4, chipped=True, satchel=True, brows="determined",
                       mouth="half_smile", gaze=0.012),
}

# Paint-only variants: the base skin's model with different wax (Heist Pass rewards).
VARIANTS = {"char_twist_gilded": ("char_twist", "wax_gold")}


def build(asset_id, spec, previews=True):
    C.reset()
    rnd = random.Random(asset_id)
    swatches = dict(SWATCHES)
    swatches["drip"] = tuple(min(1.0, c * 1.05 + 0.02) for c in SWATCHES[spec["wax"]])  # drips are the skin's own wax, a touch lighter
    pal = C.Palette(swatches)
    H = spec.get("height", 1.0)       # body height factor
    W = spec.get("width", 1.0)        # body width factor
    F = spec.get("flame", 1.0)        # flame size factor
    a, b = 0.155 * W, 0.128 * W       # half width (x) and half depth (y) of the pillar
    cup = spec.get("cup", False)
    z0 = 0.17 if cup else 0.10        # pillar bottom (Pip sits in his cup)
    top = z0 + 0.42 * H               # rim height
    wax = spec["wax"]
    parts = []                        # (object, swatch, bone or bands, marker)

    def add(obj, swatch, bone, marker=None):
        parts.append((obj, swatch, bone, marker))
        return obj

    def surface(theta, z, out=0.0):
        """A point on the pillar's surface at angle theta (0 = +X, -90 deg = front) and height z."""
        x, y = C.superellipse(1, 1, 1)[0]  # placeholder for type
        p = Vector((a * math.copysign(abs(math.cos(theta)) ** 0.62, math.cos(theta)),
                    b * math.copysign(abs(math.sin(theta)) ** 0.62, math.sin(theta)), z))
        if out:
            n = Vector((p.x / a, p.y / b, 0)).normalized()
            p += n * out
        return p

    # ---- Body: a soft rounded-rectangle pillar with a melted crown and a recessed top ------------
    rings = []
    profile = [(0.0, 0.86), (0.015, 0.97), (0.05, 1.0), (0.25, 1.03), (0.5, 1.035), (0.75, 1.02), (0.93, 1.0)]
    if spec.get("spiral"):
        # A spiral needs height resolution to read: sample the same outline on 17 rings.
        def width_at(t):
            for (t0, s0), (t1, s1) in zip(profile, profile[1:]):
                if t <= t1:
                    return s0 + (s1 - s0) * (t - t0) / (t1 - t0)
            return profile[-1][1]
        profile = [(0.0, 0.86), (0.015, 0.97)] + [(t, width_at(t)) for t in [0.05 + 0.88 * k / 14 for k in range(15)]]
    for t, s in profile:
        pts = C.superellipse(28, a * s, b * s, power=3.2)
        if spec.get("spiral"):
            z = z0 + t * (top - z0)
            pts = [(x * (1 + 0.13 * math.cos(2 * math.atan2(y, x) + z * 30)),
                    y * (1 + 0.13 * math.cos(2 * math.atan2(y, x) + z * 30))) for x, y in pts]
        if spec.get("chipped") and t < 0.02:
            pts = [(x * (1 - 0.06 * rnd.random()), y * (1 - 0.06 * rnd.random())) for x, y in pts]
        rings.append((z0 + t * (top - z0), pts))
    lip = 1.09 if not spec.get("spiral") else 1.13

    def lumpy(scale, wobble):
        """A melted ring: the lip bulges unevenly, like wax that has run over the rim."""
        pts = C.superellipse(28, a * scale, b * scale, power=3.2)
        out = []
        for i, (x, y) in enumerate(pts):
            k = 1 + wobble * (0.6 * math.sin(i * 1.9 + 0.7) + 0.4 * math.sin(i * 0.8 + 2.1))
            out.append((x * k, y * k))
        return out

    for z, s, wob in [(top - 0.006, 1.06, 0.03), (top + 0.014, lip, 0.045), (top + 0.034, lip * 0.99, 0.035), (top + 0.044, 0.9, 0.02),
                      (top + 0.036, 0.74, 0.0), (top + 0.022, 0.62, 0.0)]:
        rings.append((z, lumpy(s, wob)))
    body = C.loft("pillar", rings)
    if spec.get("chipped"):
        for v in body.data.vertices:  # a broken, chipped lower edge
            if v.co.z < z0 + 0.03:
                v.co.z += 0.025 * rnd.random()
    add(body, wax, [(0, z0 + 0.55 * (top - z0), "body"), (z0 + 0.55 * (top - z0), 9, "head")])

    # The wick, standing out of the recess.
    add(C.tube_along("wick", [(0, 0, top + 0.015), (0.004, 0, top + 0.045), (0.012, 0, top + 0.065)], 0.008, seg=6), "wick", "head")

    # Drips: 4-6 chunky ones over the rim (Drippy: many, down the body), each ending in a bulb.
    drips = spec.get("drips", 5)
    # Long runs at the corners and sides, short ones across the front so the face stays clear.
    # Many drips (Drippy) go all round but leave the face (front, -90 +/- 38 degrees) clear.
    angles = [-150, -122, -62, -30, 25] if drips <= 6 else         [d for d in (-175 + 340 * k / (drips + 3) for k in range(drips + 3)) if abs(d + 90) > 38][:drips]
    for i in range(drips):
        deg = angles[i % len(angles)] + rnd.uniform(-8, 8)
        theta = math.radians(deg)
        start_z = top + 0.02 if drips <= 6 or i % 2 == 0 else rnd.uniform(z0 + 0.15, top - 0.05)
        front = abs(deg + 90) < 40
        length = (rnd.uniform(0.035, 0.06) if front else rnd.uniform(0.08, 0.17)) * (1.3 if drips > 6 else 1.0)
        r = rnd.uniform(0.018, 0.026)
        r = rnd.uniform(0.024, 0.032)
        bone = "head" if start_z > top - 0.04 else "body"
        if start_z > top:
            # Over the lip and down the side, fattening into a bulb.
            path = [surface(theta, top + 0.03, out=0.012), surface(theta, top + 0.004, out=0.02), surface(theta, top - length * 0.5, out=0.012),
                    surface(theta, top - length, out=0.01)]
        else:
            path = [surface(theta, start_z, out=0.006), surface(theta, start_z - length, out=0.01)]
        add(C.tube_along("drip", [tuple(p) for p in path], r, seg=7), "drip", bone)
        add(C.blob("drip_end", tuple(path[-1]), (r * 1.1, r * 0.85, r * 1.2), seg=7, rings=5), "drip", bone)

    # ---- Face -------------------------------------------------------------------------------------
    es = spec.get("eye_scale", 1.0)
    ez = z0 + 0.70 * (top - z0)      # eye line
    ex = 0.062 * W * (1.0 if not spec.get("cup") else 1.05)
    fy = -b * 1.03                   # the front surface
    gaze = spec.get("gaze", 0.0)
    for side, bone in ((1, "eye.L"), (-1, "eye.R")):
        cx = side * ex
        add(C.blob("eye_white", (cx, fy + 0.012, ez), (0.042 * es, 0.026, 0.052 * es), seg=12, rings=8), "eye_white", bone, "eye")
        add(C.blob("iris", (cx + gaze, fy - 0.012, ez - 0.004), (0.028 * es, 0.008, 0.034 * es), seg=10, rings=6), "iris", bone, "eye")
        add(C.blob("pupil", (cx + gaze * 1.1, fy - 0.019, ez - 0.004), (0.016 * es, 0.006, 0.02 * es), seg=8, rings=5), "pupil", bone, "eye")
        add(C.blob("highlight", (cx + gaze - 0.011 * es, fy - 0.025, ez + 0.012 * es), (0.008 * es, 0.004, 0.008 * es), seg=6, rings=4),
            "glow_highlight", bone, "eye")
        add(C.blob("highlight", (cx + gaze + 0.008 * es, fy - 0.025, ez - 0.012 * es), (0.004 * es, 0.003, 0.004 * es), seg=6, rings=4),
            "glow_highlight", bone, "eye")
        # Rosy cheeks.
        add(C.blob("cheek", (side * (ex + 0.045 * W), fy + 0.004, ez - 0.05), (0.026 * W, 0.006, 0.017), seg=8, rings=5), "cheek", "head", "cheek")

    by = fy - 0.006
    bz = ez + 0.06 * es
    brows = {
        # (left brow inner, outer heights), (right brow inner, outer heights), relative to bz
        "confident": ((-0.005, 0.022), (-0.012, -0.002)),   # his left raised
        "worried": ((0.018, -0.012), (0.018, -0.012)),      # inner ends pulled up
        "sly": ((-0.004, 0.02), (-0.016, -0.006)),
        "determined": ((-0.016, 0.006), (-0.016, 0.006)),   # angled down to the centre
        "sharp": ((-0.02, 0.016), (-0.006, 0.012)),
    }[spec["brows"]]
    for side, (inner, outer), marker in ((1, brows[0], "brow_L"), (-1, brows[1], "brow_R")):
        pts = C.arc((side * (ex - 0.03), by, bz + inner), (side * (ex + 0.035), by + 0.006, bz + outer), 0.006, n=7)
        add(C.tube_along("brow", pts, 0.0085, seg=6), "brow", "head", marker)

    mz = ez - 0.075
    my = fy - 0.004
    mouth = spec["mouth"]
    if mouth in ("half_smile", "smirk"):
        lift = 0.012 if mouth == "smirk" else 0.006
        pts = C.arc((-0.035 * W + 0.01, my, mz + 0.002), (0.04 * W + 0.01, my, mz + lift), -0.014, n=8)
        add(C.tube_along("mouth", pts, 0.0055, seg=6), "mouth", "head", "mouth")
    elif mouth in ("grin_open", "grimace"):
        open_z = 0.028 if mouth == "grin_open" else 0.022
        add(C.blob("mouth", (0.0, my + 0.006, mz - 0.006), (0.034 * W, 0.01, open_z * 0.5), seg=10, rings=6), "mouth", "head", "mouth")
        add(C.blob("tongue", (0.004, my + 0.002, mz - 0.016), (0.02 * W, 0.006, 0.008), seg=8, rings=5), "tongue", "head", "mouth")
    elif mouth == "toothy":
        pts = C.arc((-0.045 * W, my, mz + 0.004), (0.045 * W, my, mz + 0.014), -0.016, n=8)
        add(C.tube_along("mouth", pts, 0.0075, seg=6), "mouth", "head", "mouth")
        add(C.tube_along("teeth", [(p[0], p[1] - 0.004, p[2] + 0.004) for p in pts[1:-1]], 0.0045, seg=6), "teeth", "head", "mouth")

    if spec.get("mask"):
        # Cinder's eye mask: a band round the head at eye height, knotted at his right with two ties.
        band = [(ez + dz, C.superellipse(28, a * 1.055, b * 1.075, power=3.2)) for dz in (-0.03, 0.0, 0.03)]
        add(C.loft("mask", band, cap_bottom=False, cap_top=False), "mask", "head", "mask")
        knot = surface(math.radians(195), ez, out=0.01)
        add(C.blob("knot", tuple(knot), (0.022, 0.022, 0.02), seg=8, rings=5), "mask", "head")
        for dz in (-0.03, -0.06):
            add(C.tube_along("tie", [tuple(knot), tuple(knot + Vector((-0.05, 0.03, dz)))], 0.009, seg=5), "mask", "head")

    # ---- Limbs ------------------------------------------------------------------------------------
    sz = z0 + 0.55 * (top - z0)
    for side, arm in ((1, "arm.L"), (-1, "arm.R")):
        shoulder = (side * a * 0.98, -0.01, sz)
        hand = (side * (a + 0.05), -0.03, sz - 0.085)
        add(C.capsule("arm", shoulder, hand, 0.032 * min(W, 1.15), seg=8), wax, arm)
        add(C.blob("hand", (hand[0] + side * 0.006, hand[1] - 0.006, hand[2] - 0.018), (0.04, 0.036, 0.042), seg=8, rings=6), wax, arm)
        add(C.blob("thumb", (hand[0] - side * 0.016, hand[1] - 0.03, hand[2] - 0.006), (0.014, 0.014, 0.018), seg=6, rings=4), wax, arm)
    for side, foot in ((1, "foot.L"), (-1, "foot.R")):
        fx = side * 0.072 * min(W, 1.2)
        add(C.capsule("leg", (fx, 0.0, 0.05), (fx, 0.0, z0 + 0.02), 0.04, seg=8), wax, foot)
        add(C.blob("boot", (fx, -0.022, 0.035), (0.055, 0.075, 0.037), seg=10, rings=7), wax, foot)

    if cup:
        # Pip's brushed tin tealight cup, with wax over the rim.
        add(C.lathe("cup", [(0.13 * W, 0.075), (0.15 * W, 0.08), (0.158 * W, 0.2), (0.165 * W, 0.215), (0.15 * W, 0.215)], 20,
                    cap=True), "tin", "body")
        for ang in (-60, -110, 20):
            p0 = Vector((0.16 * W * math.cos(math.radians(ang)), 0.16 * W * math.sin(math.radians(ang)), 0.215))
            p1 = p0 + Vector((0.006 * math.cos(math.radians(ang)), 0.006 * math.sin(math.radians(ang)), -0.05))
            add(C.capsule("cup_drip", p0, p1, 0.016, seg=6), "drip", "body")
    if spec.get("puddle"):
        add(C.blob("puddle", (0.0, -0.01, 0.006), (0.2, 0.17, 0.008), seg=14, rings=4), "drip", "root")

    # ---- Kit: strap, buckle, satchel (Wick, Drippy, Cinder, Ember) ---------------------------------
    if spec.get("satchel"):
        hip = sz - 0.12
        strap = [surface(math.radians(a_), z, out=0.008) for a_, z in
                 ((-20, sz + 0.06), (-55, sz - 0.0), (-90, sz - 0.06), (-125, hip + 0.02), (-160, hip))]
        add(C.tube_along("strap", [tuple(p) for p in strap], 0.016, seg=6), "strap", "body")
        buckle = surface(math.radians(-118), sz - 0.075, out=0.022)
        add(C.box("buckle", tuple(buckle), (0.05, 0.014, 0.042), bevel=0.006, segments=1), "gold", "body")
        sx = a + 0.065
        add(C.box("satchel", (sx, -0.005, hip), (0.12, 0.085, 0.12), bevel=0.028, segments=2), "leather", "body")
        add(C.box("flap", (sx, -0.04, hip + 0.035), (0.122, 0.022, 0.055), bevel=0.012, segments=1), "strap", "body")
        add(C.blob("stud", (sx, -0.054, hip + 0.018), (0.01, 0.006, 0.01), seg=6, rings=4), "gold", "body")
        for k in range(4):
            add(C.cylinder("coin", (sx - 0.03 + 0.02 * k, -0.005 + 0.01 * (k % 2), hip + 0.068 + 0.008 * (k % 3)), 0.022, 0.007, seg=10,
                           rot=(math.radians(rnd.uniform(-40, 40)), math.radians(rnd.uniform(-35, 35)), 0)), "gold", "body")
        add(C.blob("gem", (sx + 0.025, -0.01, hip + 0.088), (0.02, 0.02, 0.026), seg=6, rings=4), "gem", "body")
        if spec.get("rope"):
            add(C.lathe("rope", [(0.05, hip + 0.04), (0.052, hip + 0.048)], 10, offset=(sx, 0.01, 0), cap=False), "rope", "body")

    # ---- UVs, weights, markers, join ----------------------------------------------------------------
    for obj, swatch, bone, marker in parts:
        C.uv_to_cell(obj, pal, swatch)
        if isinstance(bone, list):
            C.weight_by_height(obj, bone)
        else:
            C.weight_all(obj, bone)
        if marker:
            C.tag(obj, marker)
        if marker == "eye":
            C.tag(obj, "eye_L" if (bone == "eye.L") else "eye_R")
        if marker in ("brow_L", "brow_R"):
            C.tag(obj, "brow")
    character = C.join([p[0] for p in parts], "body_mesh")  # not "body": that's the bone

    # ---- The flame: its own mesh (Wick/Flame), UV v from base (0) to tip (1) --------------------------
    fh = 0.27 * F
    fz = top + 0.03
    sweep_x = spec.get("flame_sweep", 0.12) * F
    back = spec.get("flame_back", 0.0)

    def flame_sweep(t):
        curl = 0.02 * F * math.sin(math.pi * t * 1.2) if spec.get("flame_curl") else 0.0
        return (-sweep_x * t ** 1.5 + curl, back * t ** 1.5)

    prof = [(0.09 * F * (math.sin(math.pi * (0.12 + 0.88 * t)) ** 0.6) * (1 - 0.82 * t) + 0.003, fz + fh * t) for t in [i / 9 for i in range(10)]]
    flame = C.lathe("flame", prof, 12, sweep=flame_sweep)
    # A smaller second tongue curling the other way gives the quiff its flicker.
    prof2 = [(0.04 * F * (math.sin(math.pi * (0.12 + 0.88 * t)) ** 0.6) * (1 - 0.8 * t) + 0.002, fz + 0.02 + fh * 0.55 * t) for t in [i / 7 for i in range(8)]]
    tongue = C.lathe("flame_tongue", prof2, 10, offset=(0.035 * F, -0.01, 0), sweep=lambda t: (0.04 * F * t ** 1.4, 0.0))
    flame = C.join([flame, tongue], "flame")
    me = flame.data
    me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    for poly in me.polygons:
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv[li].uv = (0.5, max(0.0, min(1.0, (co.z - fz) / fh)))
    C.weight_by_height(flame, [(0, fz + fh * 0.33, "flame_base"), (fz + fh * 0.33, fz + fh * 0.66, "flame_mid"), (fz + fh * 0.66, 9, "flame_tip")])

    # ---- Rig ---------------------------------------------------------------------------------------
    hz = z0 + 0.55 * (top - z0)
    arm = C.armature([
        ("root", (0, 0, 0), (0, 0, 0.05), None),
        ("body", (0, 0, 0.05), (0, 0, hz), "root"),
        ("head", (0, 0, hz), (0, 0, top), "body"),
        ("eye.L", (ex, fy, ez), (ex, fy - 0.05, ez), "head"),
        ("eye.R", (-ex, fy, ez), (-ex, fy - 0.05, ez), "head"),
        ("flame_base", (0, 0, fz), (0, 0, fz + fh * 0.33), "head"),
        ("flame_mid", (0, 0, fz + fh * 0.33), (-sweep_x * 0.2, 0, fz + fh * 0.66), "flame_base"),
        ("flame_tip", (-sweep_x * 0.2, 0, fz + fh * 0.66), (-sweep_x, 0, fz + fh), "flame_mid"),
        ("arm.L", (a * 0.98, -0.01, sz), (a + 0.05, -0.03, sz - 0.085), "body"),
        ("arm.R", (-a * 0.98, -0.01, sz), (-(a + 0.05), -0.03, sz - 0.085), "body"),
        ("foot.L", (0.072, 0, z0 + 0.02), (0.072, -0.05, 0.02), "root"),
        ("foot.R", (-0.072, 0, z0 + 0.02), (-0.072, -0.05, 0.02), "root"),
    ])

    # ---- Face shape keys (fear, greed, smug, wince, blink) --------------------------------------------
    eye_c = {"eye_L": Vector((ex, fy, ez)), "eye_R": Vector((-ex, fy, ez))}

    def scale_about(c, sx, sz):
        return lambda co: Vector((c.x + (co.x - c.x) * sx, co.y, c.z + (co.z - c.z) * sz))

    def shift(dx=0.0, dz=0.0, side_only=0):
        return lambda co: co + Vector((dx * (1 if co.x >= 0 else -1) if side_only == 0 else dx, 0, dz)) \
            if side_only == 0 or (side_only > 0) == (co.x > 0) else co

    C.shape_key(character, "fear", [("eye_L", scale_about(eye_c["eye_L"], 1.1, 1.18)), ("eye_R", scale_about(eye_c["eye_R"], 1.1, 1.18)),
                                    ("brow", lambda co: co + Vector((0, 0, 0.016))), ("mouth", scale_about(Vector((0, fy, mz)), 0.7, 1.4))])
    C.shape_key(character, "greed", [("mouth", lambda co: Vector((co.x * 1.25, co.y, co.z + 0.18 * abs(co.x)))),
                                     ("cheek", lambda co: co + Vector((0, 0, 0.008))), ("brow", lambda co: co + Vector((0, 0, 0.006)))])
    C.shape_key(character, "smug", [("brow_L", lambda co: co + Vector((0, 0, 0.012))), ("brow_R", lambda co: co + Vector((0, 0, -0.006))),
                                    ("mouth", lambda co: co + Vector((0, 0, 0.15 * max(0.0, co.x)))),
                                    ("eye_L", scale_about(eye_c["eye_L"], 1.0, 0.8)), ("eye_R", scale_about(eye_c["eye_R"], 1.0, 0.8))])
    C.shape_key(character, "wince", [("eye_L", scale_about(eye_c["eye_L"], 1.05, 0.35)), ("eye_R", scale_about(eye_c["eye_R"], 1.05, 0.35)),
                                     ("brow", lambda co: co + Vector((-0.2 * co.x * 0.05, 0, -0.01))),
                                     ("mouth", scale_about(Vector((0, fy, mz)), 0.8, 1.0))])
    C.shape_key(character, "blink", [("eye_L", scale_about(eye_c["eye_L"], 1.0, 0.08)), ("eye_R", scale_about(eye_c["eye_R"], 1.0, 0.08))])
    C.drop_markers(character)

    C.bind(character, arm)
    C.bind(flame, arm)

    # ---- Materials, textures, export ----------------------------------------------------------------
    out = os.path.join(C.INCOMING, "char", asset_id)
    os.makedirs(out, exist_ok=True)
    albedo = os.path.join(out, asset_id + "_albedo.png")
    pal.paint(albedo)
    pal.paint(os.path.join(out, asset_id + "_mask.png"), size=256, mask=True)
    character.data.materials.append(C.atlas_material(asset_id, albedo))
    fm = C.bpy.data.materials.new("flame")
    fm.use_nodes = True  # previews only: Unity gives the flame Wick/Flame
    em = fm.node_tree.nodes.get("Principled BSDF")
    em.inputs["Base Color"].default_value = (1.0, 0.35, 0.03, 1)
    em.inputs["Emission Color"].default_value = (1.0, 0.42, 0.06, 1)
    em.inputs["Emission Strength"].default_value = 1.6
    flame.data.materials.append(fm)

    tris = C.triangles(character) + C.triangles(flame)
    if spec.get("anim"):
        clips(arm)
    # Unity's characters face +Z; built facing -Y here, so turn everything (meshes, face shapes, rest pose)
    # half a turn for the export, then back for the previews. A rigid turn keeps every clip's meaning.
    turn = Matrix.Rotation(math.pi, 4, "Z")
    for m in (character.data, flame.data):
        m.transform(turn, shape_keys=True)
    arm.data.transform(turn)
    C.export_fbx(os.path.join(out, asset_id + ".fbx"), with_animation=bool(spec.get("anim")))
    for m in (character.data, flame.data):
        m.transform(turn.inverted(), shape_keys=True)
    arm.data.transform(turn.inverted())
    sources(out, asset_id)
    print("BUILT", asset_id, "triangles", tris, "(body", C.triangles(character), "+ flame", C.triangles(flame), ")")
    if previews:
        for o in (character, flame):
            pass
        C.previews(asset_id, albedo, os.path.join(C.PREVIEWS, asset_id))
    return tris


def paint_variant(variant_id, base_id, wax_swatch):
    """A repaint: the base skin's atlas with different wax."""
    sw = dict(SWATCHES)
    sw["wax"] = SWATCHES[wax_swatch]
    sw["drip"] = tuple(min(1.0, c * 1.06) for c in SWATCHES[wax_swatch])
    out = os.path.join(C.INCOMING, "char", variant_id)
    os.makedirs(out, exist_ok=True)
    C.reset()
    C.Palette(sw).paint(os.path.join(out, variant_id + "_albedo.png"))
    C.Palette(sw).paint(os.path.join(out, variant_id + "_mask.png"), size=256, mask=True)
    sources(out, variant_id, base=base_id)
    print("PAINTED", variant_id, "on", base_id)


def sources(out, asset_id, base=None):
    with open(os.path.join(out, "SOURCES.md"), "w", encoding="utf-8") as f:
        f.write("# " + asset_id + "\n\n")
        if base:
            f.write("Repaint of `" + base + "`: only the albedo atlas differs.\n\n")
        f.write("Scripted blockout built by `tools/blender/build_skins.py` (Blender 4.5 LTS, GPL; its output is ours) from the locked "
                "reference in `docs/art/references/`. No third-party meshes, textures or generators were used. Replace or refine by hand; "
                "keep the bone names, face shape keys and one-material atlas.\n")


def clips(arm):
    """The twelve clips (30 fps), keyed on rotations and scales, with only the body bone moving (hops).
    flame_base's scale is never keyed: the sim's light radius owns it."""
    up, down = (0, 0, 0), (0, 0, 0)
    C.action(arm, "idle", 60, {
        "body": {"scale": [(0, (1, 1, 1)), (30, (0.985, 1.03, 0.985)), (60, (1, 1, 1))]},
        "head": {"rot": [(0, (0, 0, 0)), (20, (0, 0, 3)), (40, (0, 0, -2)), (60, (0, 0, 0))]},
    }, loop=True)
    C.action(arm, "idle_fidget_pat", 45, {
        "arm.R": {"rot": [(0, up), (12, (-150, 0, 0)), (22, (-140, 0, 0)), (30, (-150, 0, 0)), (45, up)]},
        "head": {"rot": [(0, up), (14, (0, 0, -8)), (30, (0, 0, -8)), (45, up)]},
    }, loop=False)
    C.action(arm, "idle_fidget_count", 60, {
        "arm.L": {"rot": [(0, up), (15, (-60, 0, 0)), (45, (-60, 0, 0)), (60, up)]},
        "head": {"rot": [(0, up), (15, (18, 0, 0)), (45, (18, 0, 0)), (60, up)]},
    }, loop=False)
    C.action(arm, "dash", 8, {
        "body": {"scale": [(0, (1, 1, 1)), (2, (1.12, 0.8, 1.12)), (5, (0.92, 1.2, 0.92)), (8, (1, 1, 1))],
                 "rot": [(0, up), (3, (14, 0, 0)), (8, up)]},
    }, loop=False)
    C.action(arm, "run", 16, {
        "body": {"rot": [(0, (12, 0, 0)), (16, (12, 0, 0))], "loc": [(0, (0, 0, 0)), (4, (0, 0.015, 0)), (8, (0, 0, 0)), (12, (0, 0.015, 0)), (16, (0, 0, 0))]},
        "foot.L": {"rot": [(0, (25, 0, 0)), (8, (-25, 0, 0)), (16, (25, 0, 0))]},
        "foot.R": {"rot": [(0, (-25, 0, 0)), (8, (25, 0, 0)), (16, (-25, 0, 0))]},
        "arm.L": {"rot": [(0, (-25, 0, 0)), (8, (25, 0, 0)), (16, (-25, 0, 0))]},
        "arm.R": {"rot": [(0, (25, 0, 0)), (8, (-25, 0, 0)), (16, (25, 0, 0))]},
    }, loop=True)
    C.action(arm, "tiptoe", 24, {
        "body": {"scale": [(0, (1.04, 0.92, 1.04)), (24, (1.04, 0.92, 1.04))], "loc": [(0, (0, 0.01, 0)), (12, (0, 0.02, 0)), (24, (0, 0.01, 0))]},
        "foot.L": {"rot": [(0, (12, 0, 0)), (12, (-8, 0, 0)), (24, (12, 0, 0))]},
        "foot.R": {"rot": [(0, (-8, 0, 0)), (12, (12, 0, 0)), (24, (-8, 0, 0))]},
        "arm.L": {"rot": [(0, (-30, 0, 20)), (24, (-30, 0, 20))]},
        "arm.R": {"rot": [(0, (-30, 0, -20)), (24, (-30, 0, -20))]},
        "head": {"rot": [(0, (0, 0, 6)), (12, (0, 0, -6)), (24, (0, 0, 6))]},
    }, loop=True)
    C.action(arm, "hit", 12, {
        "body": {"scale": [(0, (1, 1, 1)), (3, (1.2, 0.75, 1.2)), (12, (1, 1, 1))]},
        "head": {"rot": [(0, up), (3, (-10, 0, 8)), (12, up)]},
    }, loop=False)
    C.action(arm, "low_health", 20, {
        "body": {"rot": [(0, (0, 0, -4)), (5, (0, 0, 4)), (10, (0, 0, -4)), (15, (0, 0, 4)), (20, (0, 0, -4))]},
        "head": {"rot": [(0, (6, 0, 0)), (20, (6, 0, 0))]},
    }, loop=True)
    C.action(arm, "grab", 18, {
        "arm.L": {"rot": [(0, up), (6, (-90, 0, 0)), (18, (-40, 0, -30))]},
        "arm.R": {"rot": [(0, up), (6, (-90, 0, 0)), (18, (-40, 0, 30))]},
        "body": {"rot": [(0, up), (6, (12, 0, 0)), (18, (4, 0, 0))]},
    }, loop=False)
    C.action(arm, "escape", 24, {
        "body": {"loc": [(0, (0, 0, 0)), (9, (0, 0.12, 0)), (18, (0, 0, 0)), (24, (0, 0, 0))],
                 "scale": [(0, (1.1, 0.88, 1.1)), (6, (0.94, 1.12, 0.94)), (18, (1.12, 0.85, 1.12)), (24, (1, 1, 1))]},
        "arm.L": {"rot": [(0, up), (9, (0, 0, 120)), (24, up)]},
        "arm.R": {"rot": [(0, up), (9, (0, 0, -120)), (24, up)]},
    }, loop=False)
    C.action(arm, "snuff", 30, {
        "body": {"scale": [(0, (1, 1, 1)), (18, (1.08, 0.85, 1.08)), (30, (1.05, 0.9, 1.05))],
                 "rot": [(0, up), (15, (0, 0, 10)), (21, (0, 0, -6)), (30, (0, 0, 4))]},
        "head": {"rot": [(0, up), (18, (20, 0, 0)), (30, (16, 0, 0))]},
    }, loop=False)
    C.action(arm, "victory", 48, {
        "body": {"loc": [(0, (0, 0, 0)), (10, (0, 0.1, 0)), (20, (0, 0, 0)), (30, (0, 0.1, 0)), (40, (0, 0, 0)), (48, (0, 0, 0))]},
        "arm.L": {"rot": [(0, up), (8, (0, 0, 150)), (48, (0, 0, 150))]},
        "arm.R": {"rot": [(0, up), (8, (0, 0, -150)), (48, (0, 0, -150))]},
    }, loop=False)
    arm.animation_data.action = None


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    previews = "--no-previews" not in argv
    ids = [a for a in argv if not a.startswith("--")] or list(SKINS) + list(VARIANTS)
    for i in ids:
        if i in SKINS:
            build(i, SKINS[i], previews)
        elif i in VARIANTS:
            paint_variant(i, *VARIANTS[i])
        else:
            print("Unknown skin", i)


if __name__ == "__main__":
    main()
