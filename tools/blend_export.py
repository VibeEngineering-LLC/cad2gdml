"""cad2gdml: экспорт видимых mesh-объектов .blend в OBJ (мм, мировые координаты; объект -> o, материал -> usemtl).
Запуск: blender -b scene.blend --python-exit-code 3 -P blend_export.py -- out.obj"""
import bpy, bmesh, sys
out = sys.argv[sys.argv.index("--") + 1]
sc = bpy.context.scene; k = sc.unit_settings.scale_length * 1000.0  # 1 единица Blender -> мм
dg = bpy.context.evaluated_depsgraph_get(); n = 0; base = 1
with open(out, "w", encoding="utf-8") as f:
    f.write("# cad2gdml blend_export, mm\n")
    for o in sc.objects:
        if o.type != "MESH" or not o.visible_get(): continue
        bm = bmesh.new(); bm.from_object(o, dg); bm.transform(o.matrix_world); bmesh.ops.triangulate(bm, faces=bm.faces[:])
        if not bm.faces: bm.free(); continue
        f.write("o %s\n" % o.name.replace(" ", "_")); cur = None
        for v in bm.verts: f.write("v %.6f %.6f %.6f\n" % (v.co.x * k, v.co.y * k, v.co.z * k))
        for fc in bm.faces:
            m = o.material_slots[fc.material_index].material if fc.material_index < len(o.material_slots) else None
            name = (m.name if m else "").replace(" ", "_")
            if name != cur: f.write("usemtl %s\n" % name); cur = name
            f.write("f %s\n" % " ".join(str(base + v.index) for v in fc.verts))
        base += len(bm.verts); n += 1; bm.free()
print("EXPORTED", n)
if n == 0: print('{"error":"E_BLEND_EMPTY","message":"В .blend нет видимых mesh-объектов"}'); sys.exit(3)
