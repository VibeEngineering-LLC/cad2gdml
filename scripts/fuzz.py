#!/usr/bin/env python3
import sys, os, struct, math, random, subprocess, shutil, zipfile, io, re
from pathlib import Path

def make_cylinder_tris():
    r, h, n = 25.4, 50.8, 32
    zt, zb = h / 2.0, -h / 2.0
    tris = []
    def v(theta, z):
        return (r * math.cos(theta), r * math.sin(theta), z)
    for i in range(n):
        t0, t1 = 2*math.pi*i/n, 2*math.pi*(i+1)/n
        v0b, v0t, v1b, v1t = v(t0,zb), v(t0,zt), v(t1,zb), v(t1,zt)
        tris.append((v0b, v1b, v0t))
        tris.append((v0t, v1b, v1t))
    ct, cb = (0.0,0.0,zt), (0.0,0.0,zb)
    for i in range(n):
        t0, t1 = 2*math.pi*i/n, 2*math.pi*(i+1)/n
        tris.append((ct, v(t0,zt), v(t1,zt)))
        tris.append((cb, v(t1,zb), v(t0,zb)))
    return tris

def normal(v0,v1,v2):
    e1=(v1[0]-v0[0],v1[1]-v0[1],v1[2]-v0[2])
    e2=(v2[0]-v0[0],v2[1]-v0[1],v2[2]-v0[2])
    nx=e1[1]*e2[2]-e1[2]*e2[1]; ny=e1[2]*e2[0]-e1[0]*e2[2]; nz=e1[0]*e2[1]-e1[1]*e2[0]
    L=math.sqrt(nx*nx+ny*ny+nz*nz)
    if L==0: return (0.0,0.0,0.0)
    return (nx/L,ny/L,nz/L)

def write_binary_stl(path, tris):
    d = b'\x00'*80
    d += struct.pack('<I', len(tris))
    for v0,v1,v2 in tris:
        n = normal(v0,v1,v2)
        d += struct.pack('<3f',*n)+struct.pack('<3f',*v0)+struct.pack('<3f',*v1)+struct.pack('<3f',*v2)+struct.pack('<H',0)
    with open(path,'wb') as f: f.write(d)

def write_ascii_stl(path, tris):
    lines = ["solid cyl"]
    for v0,v1,v2 in tris:
        n = normal(v0,v1,v2)
        lines.append(f"  facet normal {n[0]:.6e} {n[1]:.6e} {n[2]:.6e}")
        lines.append("    outer loop")
        for v in (v0,v1,v2):
            lines.append(f"      vertex {v[0]:.6e} {v[1]:.6e} {v[2]:.6e}")
        lines.append("    endloop")
        lines.append("  endfacet")
    lines.append("endsolid cyl")
    with open(path,'w') as f: f.write('\n'.join(lines)+'\n')

CUBE_V = [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]
CUBE_F = [(0,3,2),(0,2,1),(4,5,6),(4,6,7),(0,5,4),(0,1,5),(2,3,7),(2,7,6),(1,6,5),(1,2,6),(3,0,4),(3,4,7)]

def write_cube_obj(path):
    lines = ["o cube"]
    for v in CUBE_V: lines.append(f"v {v[0]} {v[1]} {v[2]}")
    for f in CUBE_F: lines.append(f"f {f[0]+1} {f[1]+1} {f[2]+1}")
    with open(path,'w') as fh: fh.write('\n'.join(lines)+'\n')

def write_3mf(path):
    ct = '<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="model" ContentType="application/vnd.ms-package.3dmanufacturing-3dmodel+xml"/></Types>'
    rels = '<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rel0" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel" Target="/3D/3dmodel.model"/></Relationships>'
    verts = ''.join(f'<vertex x="{v[0]}" y="{v[1]}" z="{v[2]}"/>' for v in CUBE_V)
    tris = ''.join(f'<triangle v1="{f[0]}" v2="{f[1]}" v3="{f[2]}"/>' for f in CUBE_F)
    model = f'<?xml version="1.0" encoding="UTF-8"?><model unit="millimeter" xml:lang="en-US" xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources><object id="1" type="model"><mesh><vertices>{verts}</vertices><triangles>{tris}</triangles></mesh></object></resources><build><item objectid="1"/></build></model>'
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('[Content_Types].xml', ct)
        z.writestr('_rels/.rels', rels)
        z.writestr('3D/3dmodel.model', model)

def rezip_3mf(path, new_model_xml):
    with zipfile.ZipFile(path, 'r') as zin:
        ct = zin.read('[Content_Types].xml')
        rels = zin.read('_rels/.rels')
    tmp = path + '.tmp'
    with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('[Content_Types].xml', ct)
        z.writestr('_rels/.rels', rels)
        z.writestr('3D/3dmodel.model', new_model_xml)
    os.replace(tmp, path)

NUM_RE = re.compile(rb'[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?')
BAD_TOKENS = [b'NaN', b'inf', b'-inf', b'1e999', b'-0', b'99999999999', b'0,5', b'0x10', b'']

def do_flip(d, rng):
    if len(d) == 0: return d
    pos = rng.randrange(len(d))
    n = rng.randint(1, 8)
    arr = bytearray(d)
    for i in range(n):
        arr[(pos + i) % len(arr)] ^= (1 << rng.randint(0, 7))
    return bytes(arr)

def do_overwrite(d, rng):
    if len(d) == 0: return d
    pos = rng.randrange(len(d))
    ln = rng.randint(1, min(64, len(d) - pos))
    fill = rng.choice([b'\x00', b'\xff'])
    return d[:pos] + fill * ln + d[pos+ln:]

def do_delete(d, rng):
    if len(d) <= 1: return b''
    pos = rng.randrange(len(d))
    ln = rng.randint(1, min(64, len(d) - pos))
    return d[:pos] + d[pos+ln:]

def do_insert(d, rng):
    if len(d) == 0:
        return bytes(rng.getrandbits(8) for _ in range(rng.randint(1, 16)))
    pos = rng.randrange(len(d) + 1)
    ln = rng.randint(1, 64)
    ins = bytes(rng.getrandbits(8) for _ in range(ln))
    return d[:pos] + ins + d[pos:]

def do_truncate(d, rng):
    if len(d) == 0: return d
    pos = rng.randint(1, len(d))
    return d[:pos]

def do_duplicate(d, rng):
    if len(d) == 0: return d
    pos = rng.randrange(len(d))
    ln = rng.randint(1, min(64, len(d) - pos))
    return d[:pos] + d[pos:pos+ln] + d[pos:pos+ln] + d[pos+ln:]

def do_replace_num(d, rng):
    tokens = list(NUM_RE.finditer(d))
    if not tokens:
        return d
    tok = rng.choice(tokens)
    rep = rng.choice(BAD_TOKENS)
    return d[:tok.start()] + rep + d[tok.end():]

BYTE_MUTS = [do_flip, do_overwrite, do_delete, do_insert, do_truncate, do_duplicate]
TEXT_MUTS = BYTE_MUTS + [do_replace_num]

def mutate_bytes(data, rng, text=False):
    pool = TEXT_MUTS if text else BYTE_MUTS
    fn = rng.choice(pool)
    return fn(data, rng)

def mutate_3mf(data, rng):
    if rng.random() < 0.5:
        # mutate decompressed XML
        buf = io.BytesIO(data)
        with zipfile.ZipFile(buf, 'r') as z:
            xml = z.read('3D/3dmodel.model')
            new_xml = mutate_bytes(xml, rng, text=True)
            tmp = buf  # rebuild
            out = io.BytesIO()
            with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as zo:
                for info in z.infolist():
                    if info.filename == '3D/3dmodel.model':
                        zo.writestr(info, new_xml)
                    else:
                        zo.writestr(info, z.read(info.filename))
            return out.getvalue()
    else:
        return mutate_bytes(data, rng, text=False)

def get_e_line(stdout_bytes):
    try:
        text = stdout_bytes.decode('utf-8', errors='replace')
    except Exception:
        return ''
    for line in text.splitlines():
        if 'E_' in line:
            return line.strip()
    return ''

def main():
    if len(sys.argv) < 3:
        print("Usage: python fuzz.py <path_to_cad2gdml.exe> <work_dir> [per_seed=300] [seed=1]", file=sys.stderr)
        sys.exit(0)
    exe = sys.argv[1]
    work_dir = Path(sys.argv[2])
    per_seed = int(sys.argv[3]) if len(sys.argv) > 3 else 300
    seed = int(sys.argv[4]) if len(sys.argv) > 4 else 1

    work_dir.mkdir(parents=True, exist_ok=True)
    seeds_dir = work_dir / 'seeds'
    mutants_dir = work_dir / 'mutants'
    findings_dir = work_dir / 'findings'
    out_dir = work_dir / 'out'
    for d in (seeds_dir, mutants_dir, findings_dir, out_dir):
        d.mkdir(parents=True, exist_ok=True)

    # 1. Create seeds
    tris = make_cylinder_tris()
    write_binary_stl(seeds_dir / 'cyl_bin.stl', tris)
    write_ascii_stl(seeds_dir / 'cyl_ascii.stl', tris)
    write_cube_obj(seeds_dir / 'cube.obj')
    write_3mf(seeds_dir / 'one.3mf')

    seeds = ['cyl_bin.stl', 'cyl_ascii.stl', 'cube.obj', 'one.3mf']
    text_seeds = {'cyl_ascii.stl', 'cube.obj'}

    total_runs = 0
    exit_counts = {}
    findings = []  # list of dicts
    class_count = {}  # key -> count
    class_examples = {}  # key -> example file path

    for seed_name in seeds:
        seed_path = seeds_dir / seed_name
        seed_data = seed_path.read_bytes()
        ext = seed_path.suffix
        base = seed_path.stem
        rng = random.Random(seed)

        for idx in range(per_seed):
            # Choose mutation
            if seed_name == 'one.3mf':
                mut_data = mutate_3mf(seed_data, rng)
            elif seed_name in text_seeds:
                mut_data = mutate_bytes(seed_data, rng, text=True)
            else:
                mut_data = mutate_bytes(seed_data, rng, text=False)

            mut_path = mutants_dir / f"{base}_{idx}{ext}"
            mut_path.write_bytes(mut_data)

            # 3. Run exe
            is_3mf = seed_name.endswith('.3mf')
            if is_3mf:
                cmd = [exe, str(mut_path), "--front", "+z", "--allow-default-material", "--out", str(out_dir)]
            else:
                cmd = [exe, str(mut_path), "--unit", "mm", "--front", "+z", "--allow-default-material", "--out", str(out_dir)]

            try:
                result = subprocess.run(cmd, capture_output=True, timeout=30, shell=False)
                exit_code = result.returncode
                stdout_bytes = result.stdout
                timed_out = False
            except subprocess.TimeoutExpired as e:
                exit_code = -1
                stdout_bytes = e.stdout or b''
                timed_out = True
            except Exception as e:
                exit_code = -1
                stdout_bytes = b''
                timed_out = False

            total_runs += 1
            ec_key = exit_code if not timed_out else 'timeout'
            exit_counts[ec_key] = exit_counts.get(ec_key, 0) + 1

            # Oracle
            is_finding = False
            if timed_out:
                is_finding = True
            else:
                if exit_code < 0 or exit_code > 100:
                    is_finding = True
                elif exit_code not in (0, 2, 3, 4, 5):
                    is_finding = True
                elif exit_code == 9:
                    is_finding = True

            if is_finding:
                e_line = get_e_line(stdout_bytes)
                dedup_key = (seed_name, exit_code, e_line)
                cnt = class_count.get(dedup_key, 0)
                if cnt < 40:
                    # Last 3 stdout lines
                    try:
                        text_out = stdout_bytes.decode('utf-8', errors='replace')
                    except Exception:
                        text_out = ''
                    lines_out = text_out.splitlines()
                    last3 = lines_out[-3:] if len(lines_out) >= 3 else lines_out

                    finding_name = f"{base}_{idx}{ext}"
                    finding_path = findings_dir / finding_name
                    shutil.copy2(str(mut_path), str(finding_path))

                    findings.append({
                        'seed': seed_name,
                        'index': idx,
                        'file': finding_name,
                        'exit_code': exit_code,
                        'last3': last3,
                        'e_line': e_line,
                        'timeout': timed_out,
                    })
                    class_count[dedup_key] = cnt + 1
                    if dedup_key not in class_examples:
                        class_examples[dedup_key] = finding_name

    # 5. Summary
    summary_lines = []
    summary_lines.append(f"Total runs: {total_runs}")
    summary_lines.append("")
    summary_lines.append("Exit code distribution:")
    for k in sorted(exit_counts.keys(), key=lambda x: (isinstance(x, str), x)):
        summary_lines.append(f"  {k}: {exit_counts[k]}")
    summary_lines.append("")
    summary_lines.append(f"Total findings: {len(findings)}")
    summary_lines.append("")
    summary_lines.append(f"Deduplicated finding classes ({len(class_examples)}):")
    for key in sorted(class_examples.keys()):
        seed_n, ec, eline = key
        example = class_examples[key]
        cnt = class_count[key]
        summary_lines.append(f"  seed={seed_n} exit={ec} e_line={eline[:80]} count={cnt} example={example}")

    summary_text = '\n'.join(summary_lines) + '\n'
    print(summary_text, end='')
    (work_dir / 'summary.txt').write_text(summary_text, encoding='utf-8')
    sys.exit(0)

if __name__ == '__main__':
    main()
