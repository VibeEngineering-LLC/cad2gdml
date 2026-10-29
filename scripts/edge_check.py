import sys, collections
sys.stdout.reconfigure(encoding='utf-8')
p, name = sys.argv[1], sys.argv[2]
V = []; F = []; cur = None
for l in open(p, encoding='utf-8'):
    t = l.split()
    if not t: continue
    if t[0] == 'v': V.append(tuple(map(float, t[1:4])))
    elif t[0] == 'o': cur = t[1]
    elif t[0] == 'f' and cur == name:
        i = [int(x.split('/')[0]) - 1 for x in t[1:]]
        F += [(i[0], i[k], i[k + 1]) for k in range(1, len(i) - 1)]
key = {}
def vid(i): return key.setdefault(tuple(round(c, 4) for c in V[i]), len(key))
E = collections.Counter(); D = collections.Counter()
for a, b, c in F:
    a, b, c = vid(a), vid(b), vid(c)
    for u, v in ((a, b), (b, c), (c, a)): E[frozenset((u, v))] += 1; D[(u, v)] += 1
inv = {v: k for k, v in key.items()}
print(name, 'tris', len(F), 'welded verts', len(key))
print('edges !=2 faces:', [(tuple(inv[x] for x in e), n) for e, n in E.items() if n != 2])
print('same-direction edges (orientation conflicts):', sum(1 for d, n in D.items() if n > 1))
