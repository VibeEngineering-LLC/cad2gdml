#!/usr/bin/env python3
import sys, math, argparse
import xml.etree.ElementTree as ET
sys.stdout.reconfigure(encoding='utf-8')

NIST = {
    'G4_Al': (2.699,[(13,1.0)]), 'G4_Cu': (8.96,[(29,1.0)]),
    'G4_Si': (2.33,[(14,1.0)]), 'G4_Fe': (7.874,[(26,1.0)]),
    'G4_Pb': (11.35,[(82,1.0)]),
    'G4_AIR': (0.00120479,[(6,.000124),(7,.755268),(8,.231781),(18,.012827)]),
    'G4_TEFLON': (2.25,[(6,.240183),(9,.759817)]),
    'G4_POLYCARBONATE': (1.2,[(1,.055491),(6,.755751),(8,.188758)]),
    'G4_WATER': (1.0,[(1,.111898),(8,.888102)]),
    'G4_SODIUM_IODIDE': (3.667,[(11,.153373),(53,.846627)]),
    'G4_CESIUM_IODIDE': (4.51,[(53,.488451),(55,.511549)]),
}
SCINT = ['labr','nai','csi','cebr','sri','bgo','gagg','lyso','yap']
FT = {'Crystal':'Crystal','CrystalCladding':'CrystalCladding','CrystalReflector':'Reflector'}

def f6(v): return format(v, '.6g')
def um(u): return {'mm':1,'cm':10,'m':1000}.get(u or 'mm', 1)

def parse(path):
    try: root = ET.parse(path).getroot()
    except Exception as e: sys.stderr.write(f"Error: invalid GDML: {e}\n"); sys.exit(3)
    pos = {}
    for p in root.iter('position'):
        n = p.get('name')
        if n: pos[n] = tuple(float(p.get(a,0))*um(p.get('unit')) for a in 'xyz')
    els = {}
    for e in root.iter('element'):
        n = e.get('name')
        if n:
            z = e.get('Z')
            if z: els[n] = int(z)
            else:
                a = e.find('atom')
                if a is not None and a.get('value'): els[n] = int(float(a.get('value')))
    mats = {}
    for m in root.iter('material'):
        n = m.get('name')
        if not n: continue
        d = m.find('D'); dens = float(d.get('value',1)) if d is not None else 1.0
        frs = []
        for f in m.findall('fraction'):
            ref = f.get('ref')
            if ref not in els: sys.stderr.write(f"Error: unknown element '{ref}'\n"); sys.exit(3)
            frs.append((els[ref], float(f.get('n',0))))
        mats[n] = (dens, frs)
    sol = {}
    for b in root.iter('box'):
        n = b.get('name')
        if n:
            x,y,z = float(b.get('x',0)),float(b.get('y',0)),float(b.get('z',0))
            sol[n] = (-x/2,-y/2,-z/2,x/2,y/2,z/2)
    for t in root.iter('tessellated'):
        n = t.get('name')
        if not n: continue
        pts = []
        for tr in t.findall('triangular'):
            for a in ('vertex1','vertex2','vertex3'):
                r = tr.get(a)
                if r in pos: pts.append(pos[r])
        if pts: sol[n] = tuple(min(p[i] for p in pts) for i in range(3)) + tuple(max(p[i] for p in pts) for i in range(3))
        else: sol[n] = (0,0,0,0,0,0)
    vols = {}
    for v in root.iter('volume'):
        n = v.get('name')
        if not n: continue
        mr = v.find('materialref'); sr = v.find('solidref')
        mn = mr.get('ref') if mr is not None else None
        sn = sr.get('ref') if sr is not None else n+'_s'
        vols[n] = (mn, sn, sol.get(sn,(0,0,0,0,0,0)))
    setup = root.find('setup')
    world = None
    if setup is not None:
        w = setup.find('world')
        world = w.get('ref') if w is not None else setup.get('world')
    phys = {}
    if world:
        for ve in root.iter('volume'):
            if ve.get('name') == world:
                for pv in ve.findall('physvol'):
                    vr = pv.find('volumeref')
                    if vr is None: continue
                    vn = vr.get('ref'); off = (0.0,0.0,0.0)
                    pr = pv.find('positionref')
                    if pr is not None:
                        pn = pr.get('ref')
                        if pn in pos: off = pos[pn]
                    ip = pv.find('position')
                    if ip is not None:
                        k = {'mm':1.0,'cm':10.0,'m':1000.0}.get(ip.get('unit','mm'),1.0)
                        off = tuple(float(ip.get(c,0))*k for c in 'xyz')
                    phys[vn] = off
    return pos, els, mats, vols, world, phys

def resolve_mat(name, mats):
    if name in mats: return name, mats[name]
    if name and name.startswith('G4_'):
        if name in NIST: return name, NIST[name]
    return name, None

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('gdml'); ap.add_argument('out')
    ap.add_argument('--crystal', default=None)
    ap.add_argument('--distance-cm', type=float, default=10.0)
    ap.add_argument('--dump', action='store_true')
    a = ap.parse_args()

    pos, els, mats, vols, world, phys = parse(a.gdml)

    # world bbox (union of all non-world volumes with their physvol offsets)
    def vol_bbox(vn):
        if vn not in vols: return None
        mn, sn, sb = vols[vn]
        off = phys.get(vn, (0,0,0))
        return tuple(sb[i]+off[i%3] for i in range(6))

    non_world = {k: v for k, v in vols.items() if k != world}
    all_bboxes = [(vn, vol_bbox(vn)) for vn in non_world]
    all_bboxes = [(vn, bb) for vn, bb in all_bboxes if bb is not None]

    # find crystal
    cands = []
    for vn, (mn, sn, sb) in non_world.items():
        if mn and any(kw in mn.lower() for kw in SCINT):
            cands.append(vn)
    if a.crystal:
        if a.crystal not in vols:
            sys.stderr.write(f"Error: crystal '{a.crystal}' not found\n"); sys.exit(2)
        cands = [a.crystal]
    if len(cands) == 0:
        sys.stderr.write("Error: no scintillator crystal found\n"); sys.exit(2)
    if len(cands) > 1:
        sys.stderr.write(f"Error: multiple crystals: {', '.join(sorted(cands))}\n"); sys.exit(2)
    cry_name = cands[0]
    cry_mat_name, cry_mat = resolve_mat(vols[cry_name][0], mats)
    if cry_mat is None:
        sys.stderr.write(f"Error: unknown material for crystal\n"); sys.exit(3)
    cry_bb = vol_bbox(cry_name)
    cry_center = tuple((cry_bb[i]+cry_bb[i+3])/2 for i in range(3))

    # extents
    ext = tuple(cry_bb[i+3]-cry_bb[i] for i in range(3))
    long_axis = max(range(3), key=lambda i: ext[i])
    transverse = [i for i in range(3) if i != long_axis]
    t1, t2 = transverse

    for vn, (mn, _, _) in non_world.items():  # неизвестное G4_ вокруг кристалла нельзя молча пропускать
        bb = vol_bbox(vn)
        if vn != cry_name and bb and mn and resolve_mat(mn, mats)[1] is None and all(bb[i] <= cry_bb[i] and cry_bb[i+3] <= bb[i+3] for i in range(3)):
            sys.stderr.write(f"Error: unknown material '{mn}' for volume '{vn}' around the crystal"); sys.exit(3)
    # find cladding (Al only: single element Z=13)
    clad_name = None; clad_bb = None; best_vol = -1
    for vn in non_world:
        if vn == cry_name: continue
        mn = vols[vn][0]
        if mn is None: continue
        _, mdata = resolve_mat(mn, mats)
        if mdata is None: continue
        dens, frs = mdata
        if len(frs) == 1 and frs[0][0] == 13:
            bb = vol_bbox(vn)
            if bb is None: continue
            if all(bb[i] <= cry_bb[i] and cry_bb[i+3] <= bb[i+3] for i in range(3)):
                vol = (bb[3]-bb[0])*(bb[4]-bb[1])*(bb[5]-bb[2])
                if best_vol < 0 or vol < best_vol: best_vol = vol; clad_name = vn; clad_bb = bb
    clad_mat_name, clad_mat = resolve_mat(vols[clad_name][0], mats) if clad_name else (None, None)
    if clad_name and clad_mat is None:
        sys.stderr.write(f"Error: unknown G4_ material '{clad_mat_name}' for cladding\n"); sys.exit(3)

    # find reflector (G4_TEFLON or C+F composition)
    refl_name = None; refl_bb = None
    for vn in non_world:
        if vn in (cry_name, clad_name): continue
        mn = vols[vn][0]
        if mn is None: continue
        _, mdata = resolve_mat(mn, mats)
        if mdata is None: continue
        dens, frs = mdata
        zs = sorted(z for z, _ in frs)
        if mn == 'G4_TEFLON' or (zs == [6, 9]):
            bb = vol_bbox(vn)
            if bb is None: continue
            if all(bb[i] <= cry_bb[i] and cry_bb[i+3] <= bb[i+3] for i in range(3)):
                refl_name = vn; refl_bb = bb; break
    refl_mat_name, refl_mat = resolve_mat(vols[refl_name][0], mats) if refl_name else (None, None)
    if refl_name and refl_mat is None:
        sys.stderr.write(f"Error: unknown G4_ material '{refl_mat_name}' for reflector\n"); sys.exit(3)

    # thicknesses (mm)
    def side_thick(bb, axis_t):
        vals = []
        for ax in axis_t:
            vals.append((bb[ax+3]-bb[ax]) - (cry_bb[ax+3]-cry_bb[ax]))
        return min(v/2.0 for v in vals)
    rd = [vol_bbox(v) for v, (m, _, _) in non_world.items() if v != cry_name and (resolve_mat(m, mats)[1] and [z for z, _ in resolve_mat(m, mats)[1][1]] == [14] or 'sipm' in v.lower() or 'pmt' in v.lower())]
    rd = [b for b in rd if b]
    fsign = -1 if rd and sum((b[long_axis]+b[long_axis+3])/2 for b in rd)/len(rd) > cry_center[long_axis] else 1
    def front_thick(bb, ax):
        return bb[ax+3] - cry_bb[ax+3] if fsign > 0 else cry_bb[ax] - bb[ax]

    clad_side = max(side_thick(clad_bb, transverse), 0) if clad_bb else 0
    clad_front = max(front_thick(clad_bb, long_axis), 0) if clad_bb else 0
    refl_side = max(side_thick(refl_bb, transverse), 0) if refl_bb else 0
    refl_front = max(front_thick(refl_bb, long_axis), 0) if refl_bb else 0
    if refl_bb: clad_side = max(clad_side-refl_side, 0); clad_front = max(clad_front-refl_front, 0)  # слои идут друг за другом

    # cm values
    X = ext[t1]/10; Y = ext[t2]/10; Z = ext[long_axis]/10
    diam = math.sqrt(4*X*Y/math.pi)
    clad_side_cm = clad_side/10; clad_front_cm = clad_front/10
    refl_side_cm = refl_side/10; refl_front_cm = refl_front/10

    # material data for output
    def get_mat_data(name, data):
        if data is None: return None
        dens, frs = data
        total = sum(f for _, f in frs)
        frs_n = [(z, f/total) for z, f in frs] if total > 0 else frs
        return dens, frs_n

    cry_data = get_mat_data(cry_mat_name, cry_mat)
    if clad_name: clad_data = get_mat_data(clad_mat_name, clad_mat)
    else: clad_data = (2.7, [(13, 1.0)])
    if refl_name: refl_data = get_mat_data(refl_mat_name, refl_mat)
    else: refl_data = (2.25, [(6, .240183), (9, .759817)])

    # build output
    lines = []
    lines.append('//DETECTOR PARAMETERS BLOCK:')
    lines.append('//Detector types: COAXIAL, SCINTILLATOR')
    lines.append('')
    lines.append('DetectorType = SCINTILLATOR')
    lines.append('')
    lines.append(f'// Scintillator detector (front={'+' if fsign>0 else '-'}{"xyz"[long_axis]})')
    lines.append(f'DS_CrystalDiameter = {f6(diam)} cm')
    lines.append(f'DS_CrystalHeight = {f6(Z)} cm')
    lines.append(f'DS_CrystalFrontReflectorThickness = {f6(refl_front_cm)} cm')
    lines.append(f'DS_CrystalSideReflectorThickness = {f6(refl_side_cm)} cm')
    lines.append(f'DS_CrystalFrontCladdingThickness = {f6(clad_front_cm)} cm')
    lines.append(f'DS_CrystalSideCladdingThickness = {f6(clad_side_cm)} cm')
    lines.append('DS_DetectorMountingThickness = 0.2 cm')
    lines.append('')
    lines.append('//SOURCE PARAMETERS BLOCK:')
    lines.append('//Source types: POINT, CYLINDER, MARINELLI')
    lines.append('')
    lines.append('SourceType = POINT')
    lines.append('')
    lines.append('//Point source')
    lines.append(f'pdistance = {f6(a.distance_cm)} cm')
    lines.append('')

    # material blocks
    blocks = [
        ('Crystal', 'Crystal', cry_data, cry_mat_name or 'crystal'),
        ('CrystalCladding', 'Crystal_Cladding', clad_data, clad_mat_name or 'Aluminium'),
        ('CrystalReflector', 'Reflector', refl_data, refl_mat_name or 'PTFE'),
    ]
    for part, role, mdata, mname in blocks:
        dens, frs = mdata
        n = len(frs)
        lines.append(f'// {role}')
        lines.append(f'DS_n{part}Elements = {n}')
        lines.append(f'DS_Ro{part} = {f6(dens)}')
        for i in range(n):
            lines.append(f'DS_Z{part}[{i}] = {frs[i][0]}')
        for i in range(n):
            lines.append(f'DS_Fractions{part}[{i}] = {f6(frs[i][1])}')
        lines.append(f'DS_FractionType{FT[part]} = MASS')
        lines.append(f'M_DS_{role}.MName = {mname}')
        lines.append(f'M_DS_{role}.Nmaterials = 1')
        lines.append(f'M_DS_{role}.Name[0] = {mname}')
        lines.append(f'M_DS_{role}.MatRelWeight[0] = 1')
        lines.append('')

    lines.append(f'DS_CrystalBoxX = {f6(X)} cm')
    lines.append(f'DS_CrystalBoxY = {f6(Y)} cm')
    lines.append(f'DS_CrystalBoxZ = {f6(Z)} cm')

    out_text = '\n'.join(lines) + '\n'
    with open(a.out, 'w', encoding='utf-8', newline='\n') as fh:
        fh.write(out_text)

    if a.dump:
        print(f"crystal={cry_name} mat={cry_mat_name} density={cry_data[0]}")
        print(f"bbox_mm={cry_bb}")
        print(f"cladding={clad_name} reflector={refl_name}")
        print(f"thicknesses_mm: clad_s={clad_side:.2f} clad_f={clad_front:.2f} refl_s={refl_side:.2f} refl_f={refl_front:.2f}")

    print(f"OK crystal={cry_name} box={ext[t1]:.0f}x{ext[t2]:.0f}x{ext[long_axis]:.0f}mm "
          f"refl={refl_side:.1f}/{refl_front:.1f}mm clad={clad_side:.1f}/{clad_front:.1f}mm")

if __name__ == '__main__':
    main()
