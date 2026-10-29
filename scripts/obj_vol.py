import sys, collections
sys.stdout.reconfigure(encoding='utf-8')
p, name = sys.argv[1], sys.argv[2]
V = []; F = []; cur = None; par = {}
def f(x):
    while par.setdefault(x, x) != x: par[x] = par[par[x]]; x = par[x]
    return x
for l in open(p, encoding='utf-8'):
    t = l.split()
    if not t: continue
    if t[0] == 'v': V.append(tuple(map(float, t[1:4])))
    elif t[0] == 'o': cur = t[1]
    elif t[0] == 'f' and cur == name:
        i = [int(x.split('/')[0]) - 1 for x in t[1:]]
        F += [(i[0], i[k], i[k + 1]) for k in range(1, len(i) - 1)]
for a, b, c in F: par[f(a)] = f(b); par[f(b)] = f(c)
comp = collections.defaultdict(list)
for t in F: comp[f(t[0])].append(t)
for ts in comp.values():
    vol = sum(sum(x * y for x, y in zip(V[a], (V[b][1]*V[c][2]-V[b][2]*V[c][1], V[b][2]*V[c][0]-V[b][0]*V[c][2], V[b][0]*V[c][1]-V[b][1]*V[c][0]))) for a, b, c in ts) / 6
    pts = [V[q] for t in ts for q in t]
    print(name, 'tris', len(ts), 'vol_mm3 %.1f' % vol, 'size', [round(max(p[d] for p in pts) - min(p[d] for p in pts), 2) for d in range(3)])
