import re, sys, collections
sys.stdout.reconfigure(encoding='utf-8')
s = open(sys.argv[1], encoding='utf-8').read()
P = {m[0]: (float(m[1]), float(m[2]), float(m[3])) for m in re.findall(r'<position name="([^"]+)" x="([^"]+)" y="([^"]+)" z="([^"]+)"', s)}
bad = collections.Counter(); tot = collections.Counter()
for sol in re.finditer(r'<tessellated name="([^"]+)".*?</tessellated>', s, re.S):
    for v in re.findall(r'vertex1="([^"]+)" vertex2="([^"]+)" vertex3="([^"]+)"', sol.group(0)):
        a, b, c = (P[x] for x in v)
        u = [b[i] - a[i] for i in range(3)]; w = [c[i] - a[i] for i in range(3)]
        cr = (u[1]*w[2]-u[2]*w[1], u[2]*w[0]-u[0]*w[2], u[0]*w[1]-u[1]*w[0])
        tot[sol.group(1)] += 1
        if (cr[0]**2 + cr[1]**2 + cr[2]**2) ** 0.5 / 2 < 1e-9: bad[sol.group(1)] += 1
for k in tot: print(k, 'tris', tot[k], 'zero-area', bad[k])
print('total zero-area', sum(bad.values()), 'of', sum(tot.values()))
