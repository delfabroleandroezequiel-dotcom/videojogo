# Librería del generador de La Pirámide (greybox).
# Cada sala se escribe en COORDENADAS DE MUNDO; al exportar se pasa a coordenadas locales
# (origen = esquina inferior izquierda de la sala, así todo queda con y <= 0 como en el resto del juego).
# Las conexiones se declaran una sola vez (link) y el generador escribe las dos transiciones con su
# punto de llegada: así las puertas siempre coinciden en el mapa del mundo.
import os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUTDIR = 'scenes/world/Mapas/Piramide'
RES_OUT = 'res://' + OUTDIR + '/'

# Paleta greybox (arenisca)
C_MURO = (0.42, 0.34, 0.23, 1)
C_BORDE = (0.66, 0.54, 0.34, 1)
C_REPISA = (0.74, 0.6, 0.38, 1)
C_FONDO = (0.085, 0.065, 0.045, 1)
C_RAMPA = (0.47, 0.38, 0.25, 1)

SCENES = {
    'player': ('PackedScene', 'res://scenes/player/Player.tscn'),
    'transition': ('PackedScene', 'res://scenes/world/LevelTransition.tscn'),
    'savepoint': ('PackedScene', 'res://scenes/world/SavePoint.tscn'),
    'cofre': ('PackedScene', 'res://scenes/world/Rehusables/Chest.tscn'),
    'door': ('PackedScene', 'res://scenes/world/Door.tscn'),
    'lever': ('PackedScene', 'res://scenes/world/Lever.tscn'),
    'ladder': ('PackedScene', 'res://scenes/world/Ladder.tscn'),
    'pickup': ('PackedScene', 'res://scenes/world/ItemPickup.tscn'),
    'barrier': ('PackedScene', 'res://scenes/world/Rehusables/EnergyBarrier.tscn'),
    'muro': ('PackedScene', 'res://scenes/world/Rehusables/MuroCargado.tscn'),
    'agrietado': ('PackedScene', 'res://scenes/world/Rehusables/PisoAgrietado.tscn'),
    'placa': ('PackedScene', 'res://scenes/world/Rehusables/PlacaDePresion.tscn'),
    'relevo': ('PackedScene', 'res://scenes/world/Rehusables/RelevoTrampa.tscn'),
    'flecha': ('PackedScene', 'res://scenes/world/Rehusables/ArrowTrap.tscn'),
    'roca': ('PackedScene', 'res://scenes/world/Rehusables/RocaRodante.tscn'),
    'pinchos': ('PackedScene', 'res://scenes/world/Rehusables/SpikeTrap.tscn'),
    'cuerda': ('PackedScene', 'res://scenes/world/Rehusables/Rope.tscn'),
    'soga': ('PackedScene', 'res://scenes/world/Rehusables/HangRope.tscn'),
    'movediza': ('PackedScene', 'res://scenes/world/Rehusables/ArenasMovedizas.tscn'),
    'cinta': ('PackedScene', 'res://scenes/world/Rehusables/CintaTransportadora.tscn'),
    'prensa': ('PackedScene', 'res://scenes/world/Rehusables/ParedQueSeCierra.tscn'),
    'pendulo': ('PackedScene', 'res://scenes/world/Rehusables/PendulumTrap.tscn'),
    'cortina': ('PackedScene', 'res://scenes/world/Rehusables/CortinaFalsa.tscn'),
    'rafaga': ('PackedScene', 'res://scenes/world/Rehusables/Rafaga.tscn'),
    'sombrabloque': ('PackedScene', 'res://scenes/world/Rehusables/ShadowBlock.tscn'),
    'pozaarena': ('PackedScene', 'res://scenes/world/Rehusables/PozaArena.tscn'),
    # nuevas de la pirámide
    'rayo': ('PackedScene', 'res://scenes/world/Rehusables/RayoSolar.tscn'),
    'espejo': ('PackedScene', 'res://scenes/world/Rehusables/EspejoSolar.tscn'),
    'receptor': ('PackedScene', 'res://scenes/world/Rehusables/ReceptorSolar.tscn'),
    'cascada': ('PackedScene', 'res://scenes/world/Rehusables/CascadaDeArena.tscn'),
    'guillotina': ('PackedScene', 'res://scenes/world/Rehusables/Guillotina.tscn'),
    'losa': ('PackedScene', 'res://scenes/world/Rehusables/LosaBasculante.tscn'),
    # enemigos de ejemplo (se cambian después)
    'escorpion': ('PackedScene', 'res://scenes/world/Enemigos/Escorpion.tscn'),
    'bicho': ('PackedScene', 'res://scenes/world/Enemigos/BichoArena.tscn'),
    'buitre': ('PackedScene', 'res://scenes/world/Enemigos/Buitre.tscn'),
    'rata': ('PackedScene', 'res://scenes/world/Enemigos/RataPlaga.tscn'),
    'murcielago': ('PackedScene', 'res://scenes/world/Enemigos/MurcielagoChico.tscn'),
    'saqueador': ('PackedScene', 'res://scenes/world/Enemigos/Bandido6Cimitarra.tscn'),
    'saqueador_arco': ('PackedScene', 'res://scenes/world/Enemigos/BandidoArquero.tscn'),
    'saqueador_honda': ('PackedScene', 'res://scenes/world/Enemigos/Bandido3Honda.tscn'),
    'saqueador_lanza': ('PackedScene', 'res://scenes/world/Enemigos/Bandido2Lanza.tscn'),
    'corpulento': ('PackedScene', 'res://scenes/world/Enemigos/BandidoCorpulento.tscn'),
    'camera': ('Resource', 'res://resources/camera/OpenZoneCamera.tres'),
    'bootstrap': ('Script', 'res://scripts/save/LevelBootstrap.cs'),
    'marea': ('Script', 'res://scripts/world/Lavas/MareaDeLava.cs'),
    'sello_sol': ('Resource', 'res://resources/items/sello_sol.tres'),
    'sello_arena': ('Resource', 'res://resources/items/sello_arena.tres'),
    'nucleo': ('Resource', 'res://resources/items/nucleo_piramide.tres'),
}

# --- valores .tscn -----------------------------------------------------------------------------
class NP:      # NodePath relativo desde el nodo
    def __init__(self, *names): self.names = names
class Res:
    def __init__(self, key): self.key = key
class Raw:
    def __init__(self, s): self.s = s

def fmt(v, room=None):
    if isinstance(v, bool): return 'true' if v else 'false'
    if isinstance(v, int): return str(v)
    if isinstance(v, float):
        s = ('%.3f' % v).rstrip('0')
        return s + '0' if s.endswith('.') else s
    if isinstance(v, str): return '"' + v.replace('"', '\\"') + '"'
    if isinstance(v, tuple) and len(v) == 2: return 'Vector2(%s, %s)' % (fmt(float(v[0])), fmt(float(v[1])))
    if isinstance(v, tuple) and len(v) == 4: return 'Color(%s, %s, %s, %s)' % tuple(fmt(float(x)) for x in v)
    if isinstance(v, list): return 'PackedFloat32Array(%s)' % ', '.join(fmt(float(x)) for x in v)
    if isinstance(v, NP):
        if len(v.names) == 1 and isinstance(v.names[0], str) and v.names[0].startswith('@'):
            return 'NodePath("../%s")' % v.names[0][1:]
        return 'Array[NodePath]([%s])' % ', '.join('NodePath("../%s")' % n for n in v.names)
    if isinstance(v, Res):
        room.use(v.key); return 'ExtResource("%s")' % v.key
    if isinstance(v, Raw): return v.s
    raise ValueError(repr(v))

ROOMS = {}
LINKS = []

class Room:
    def __init__(self, code, nombre, x0, y0, x1, y1, idea=''):
        self.code, self.nombre = code, nombre
        self.x0, self.y0, self.x1, self.y1 = x0, y0, x1, y1
        self.idea = idea
        self.solids = []      # (name, x0,y0,x1,y1)
        self.polys = []       # (name, [pts])
        self.shelves = []     # (name, x0,x1,y)
        self.ents = []        # (key, name, x, y, props, parent)
        self.notes = []
        self.conns = []       # (from, signal, to, method)
        self.gaps = {'W': [], 'E': [], 'T': [], 'B': []}
        self.transitions = [] # filled by links
        self.used = set(['bootstrap', 'camera', 'player', 'transition'])
        self.spawn_default = None
        self.counts = {}
        self.modulate = (1.0, 0.93, 0.82, 1)
        self.zone = 'LA PIRÁMIDE'
        ROOMS[code] = self

    # nombres únicos
    def nm(self, base):
        n = self.counts.get(base, 0) + 1
        self.counts[base] = n
        return base if n == 1 else '%s%d' % (base, n)

    def use(self, key): self.used.add(key)

    @property
    def scene_name(self): return '%s_%s' % (self.code, self.nombre)
    @property
    def res_path(self): return RES_OUT + self.scene_name + '.tscn'
    def loc(self, x, y): return (x - self.x0, y - self.y1)

    # --- geometría (mundo) ---
    def block(self, x0, y0, x1, y1, name='Bloque'):
        assert x1 > x0 and y1 > y0, (self.code, name, x0, y0, x1, y1)
        self.solids.append((self.nm(name), x0, y0, x1, y1)); return self
    def floor(self, x0, x1, y, name='Piso', to=None):
        return self.block(x0, y, x1, self.y1 if to is None else to, name)
    def ceil(self, x0, x1, y, name='Techo'):
        return self.block(x0, self.y0, x1, y, name)
    def ramp(self, xa, ya, xb, yb, name='Rampa', to=None):
        # rampa sólida: la línea (xa,ya)-(xb,yb) es la superficie, rellena hasta "to" (o el fondo)
        base = self.y1 if to is None else to
        self.polys.append((self.nm(name), [(xa, ya), (xb, yb), (xb, base), (xa, base)])); return self
    def poly(self, pts, name='Pieza'):
        self.polys.append((self.nm(name), pts)); return self
    def shelf(self, x0, x1, y, name='Repisa'):
        self.shelves.append((self.nm(name), x0, x1, y)); return self
    def ent(self, key, name, x, y, **props):
        n = self.nm(name)
        self.use(key)
        self.ents.append((key, n, x, y, props)); return n
    def note(self, x, y, text): self.notes.append((x, y, text))
    def connect(self, frm, sig, to, method): self.conns.append((frm, sig, to, method))
    def gap(self, side, a, b): self.gaps[side].append((a, b))

    # --- piezas compuestas ---
    def pinchos(self, x0, x1, y, name='Pinchos', **kw):
        # fila de pinchos siempre afuera (receta de La Sima): un SpikeTrap cada ~98 px
        n = max(1, int((x1 - x0) // 98))
        step = (x1 - x0) / n
        out = []
        for i in range(n):
            props = dict(RetractedDuration=0.0, ExtendedDuration=1000000000.0, WarningDuration=0.0,
                         HasFloorCollision=False, Damage=30, InstantKill=False)
            props.update(kw)
            out.append(self.ent('pinchos', name, x0 + step * (i + 0.5), y, **props))
        return out
    def band(self, xa, ya, xb, yb, th=60, name='Rampa'):
        # rampa flotante (banda de espesor th): la superficie es (xa,ya)-(xb,yb)
        self.polys.append((self.nm(name), [(xa, ya), (xb, yb), (xb, yb + th), (xa, ya + th)])); return self
    def ladder(self, x, ytop, ybot, name='Escalera'):
        return self.ent('ladder', name, x, (ytop + ybot) / 2, Length=float(ybot - ytop), Width=24.0)
    def door(self, name, x, y, width=200, horizontal=False, pid='', starts_open=False, lever=True):
        props = dict(RequiresLever=lever, Width=float(width), SlideDistance=float(width + 10))
        if horizontal: props['Horizontal'] = True
        if pid: props['PersistenceId'] = pid
        if starts_open: props['StartsOpen'] = True
        return self.ent('door', name, x, y, **props)
    def cofre(self, x, y, oro, name='Cofre'):
        return self.ent('cofre', name, x, y, GoldReward=oro)
    def save(self, x, y):
        return self.ent('savepoint', 'Hoguera', x, y)

# --- conexiones ----------------------------------------------------------------------------------
SIDE_DOOR_H = 200

def link(a, sa, pa, b, sb, pb=None, w=120, spawn_a=None, spawn_b=None, name_a=None, name_b=None,
         interact_a=False, interact_b=False):
    """a/b: códigos. sa/sb: 'W','E','T','B'. pa: y del piso (W/E) o x del hueco (T/B), en mundo.
    pb por defecto = pa (mismo y o misma x). spawn_*: punto de llegada EN ESA SALA (mundo)."""
    LINKS.append(dict(a=a, sa=sa, pa=pa, b=b, sb=sb, pb=pa if pb is None else pb, w=w,
                      spawn_a=spawn_a, spawn_b=spawn_b, name_a=name_a, name_b=name_b,
                      interact_a=interact_a, interact_b=interact_b))

EXTERNAL = {}  # code -> (scene_path, dict) salas fuera de la pirámide

def default_spawn(room, side, p, w):
    if side == 'W': return (room.x0 + 170, p - 40)
    if side == 'E': return (room.x1 - 170, p - 40)
    if side == 'T': return (p, room.y0 + 140)
    if side == 'B': raise ValueError('llegada por abajo necesita spawn explícito (%s)' % room.code)

def build_links():
    for L in LINKS:
        for me, side, p, other, oside, op, sp_other, nm, inter in (
                (L['a'], L['sa'], L['pa'], L['b'], L['sb'], L['pb'], L['spawn_b'], L['name_a'], L['interact_a']),
                (L['b'], L['sb'], L['pb'], L['a'], L['sa'], L['pa'], L['spawn_a'], L['name_b'], L['interact_b'])):
            if me in EXTERNAL:
                continue
            R = ROOMS[me]
            if other in EXTERNAL:
                path, info = EXTERNAL[other]
                target_path = path
                spawn_local = info['spawn']
            else:
                O = ROOMS[other]
                sp = sp_other or default_spawn(O, oside, op, L['w'])
                spawn_local = O.loc(*sp)
                target_path = O.res_path
            w = L['w']
            if side in ('W', 'E'):
                x = R.x0 + 15 if side == 'W' else R.x1 - 15
                pos = (x, p); size = (30, SIDE_DOOR_H)
                R.gap(side, p - SIDE_DOOR_H - 10, p)
            elif side == 'T':
                pos = (p, R.y0 + 57); size = (w, 80)
                R.gap('T', p - w / 2, p + w / 2)
            else:
                pos = (p, R.y1 + 47); size = (w, 60)
                R.gap('B', p - w / 2, p + w / 2)
            base = nm or ('Hacia' + (other if other in EXTERNAL else ROOMS[other].nombre))
            R.transitions.append(dict(name=R.nm(base), pos=pos, size=size, target=target_path,
                                      spawn=spawn_local, interact=inter, side=side, other=other))

# --- bordes con huecos ---------------------------------------------------------------------------
def split(a, b, gaps, maxlen=400):
    segs = [(a, b)]
    for g0, g1 in gaps:
        nxt = []
        for s0, s1 in segs:
            if g1 <= s0 or g0 >= s1: nxt.append((s0, s1)); continue
            if g0 > s0: nxt.append((s0, g0))
            if g1 < s1: nxt.append((g1, s1))
        segs = nxt
    out = []
    for s0, s1 in segs:
        n = max(1, int((s1 - s0 + maxlen - 1) // maxlen))
        for i in range(n):
            out.append((s0 + (s1 - s0) * i / n, s0 + (s1 - s0) * (i + 1) / n))
    return [s for s in out if s[1] - s[0] > 0.5]

def borders(R):
    T = 40
    for i, (a, b) in enumerate(split(R.y0 - T, R.y1 + T, R.gaps['W'])):
        R.solids.append(('ParedOeste%d' % (i + 1), R.x0 - T, a, R.x0, b))
    for i, (a, b) in enumerate(split(R.y0 - T, R.y1 + T, R.gaps['E'])):
        R.solids.append(('ParedEste%d' % (i + 1), R.x1, a, R.x1 + T, b))
    for i, (a, b) in enumerate(split(R.x0, R.x1, R.gaps['T'], 1200)):
        R.solids.append(('TechoBorde%d' % (i + 1), a, R.y0 - T, b, R.y0))
    for i, (a, b) in enumerate(split(R.x0, R.x1, R.gaps['B'], 1200)):
        R.solids.append(('FondoBorde%d' % (i + 1), a, R.y1, b, R.y1 + T))

# --- validación ----------------------------------------------------------------------------------
def rect_hit(R, x, y):
    for n, a, b, c, d in R.solids:
        if a <= x <= c and b <= y <= d: return n
    return None

def ground_below(R, x, y, maxd=900):
    best = None
    for n, a, b, c, d in R.solids:
        if a <= x <= c and b >= y - 1 and b - y <= maxd:
            best = b if best is None else min(best, b)
    for n, a, c, yy in R.shelves:
        if a <= x <= c and yy >= y - 1 and yy - y <= maxd:
            best = yy if best is None else min(best, yy)
    for n, pts in R.polys:
        # superficie superior aproximada: interpolación en el primer tramo (rampas)
        (xa, ya), (xb, yb) = pts[0], pts[1]
        lo, hi = min(xa, xb), max(xa, xb)
        if lo <= x <= hi and xa != xb:
            ys = ya + (yb - ya) * (x - xa) / (xb - xa)
            if ys >= y - 1 and ys - y <= maxd:
                best = ys if best is None else min(best, ys)
    return best

def validate():
    errs = []
    codes = list(ROOMS)
    for i, a in enumerate(codes):
        A = ROOMS[a]
        for b in codes[i + 1:]:
            B = ROOMS[b]
            if A.x0 < B.x1 and B.x0 < A.x1 and A.y0 < B.y1 and B.y0 < A.y1:
                errs.append('SOLAPE %s / %s' % (a, b))
    for L in LINKS:
        for me, side, p, other, oside, op in ((L['a'], L['sa'], L['pa'], L['b'], L['sb'], L['pb']),):
            if me in EXTERNAL or other in EXTERNAL: continue
            A, B = ROOMS[me], ROOMS[other]
            pair = side + oside
            if pair in ('EW', 'WE'):
                xa = A.x1 if side == 'E' else A.x0
                xb = B.x0 if oside == 'W' else B.x1
                if xa != xb or p != op: errs.append('PUERTA lateral desalineada %s-%s' % (me, other))
                for R, s in ((A, side), (B, oside)):
                    if not (R.y0 + 200 <= p <= R.y1): errs.append('PUERTA fuera de rango %s y=%s' % (R.code, p))
            elif pair in ('BT', 'TB'):
                ya = A.y1 if side == 'B' else A.y0
                yb = B.y0 if oside == 'T' else B.y1
                if ya != yb or p != op: errs.append('HUECO desalineado %s-%s' % (me, other))
            else:
                errs.append('PAR raro %s %s-%s %s' % (me, side, oside, other))
    return errs

def validate_spawns():
    errs = []
    for R in ROOMS.values():
        for t in R.transitions:
            if t['other'] in EXTERNAL: continue
            O = ROOMS[t['other']]
            wx, wy = t['spawn'][0] + O.x0, t['spawn'][1] + O.y1
            if rect_hit(O, wx, wy) or rect_hit(O, wx, wy - 30):
                errs.append('LLEGADA dentro de un bloque: %s en %s (%d,%d)' % (t['name'], O.code, wx, wy))
            g = ground_below(O, wx, wy)
            if g is None:
                errs.append('LLEGADA sin piso debajo: %s -> %s (%d,%d)' % (R.code, O.code, wx, wy))
            # distancia a sus propias transiciones
            for t2 in O.transitions:
                px, py = t2['pos']; sw, sh = t2['size']
                cy = py - 47
                dx = max(0, abs(wx - px) - sw / 2); dy = max(0, abs(wy - cy) - sh / 2)
                if (dx * dx + dy * dy) ** 0.5 < 60 and t2['side'] != 'T':
                    errs.append('LLEGADA pegada a la salida %s en %s' % (t2['name'], O.code))
    return errs

# --- exportar ------------------------------------------------------------------------------------
def write_room(R, uid_seed=0):
    borders(R)
    shapes = {}
    def shape_id(w, h):
        k = 'R_%dx%d' % (round(w), round(h))
        shapes[k] = (w, h); return k
    body = []
    L = R.loc
    W, H = R.x1 - R.x0, R.y1 - R.y0
    cl, ct = L(R.x0, R.y0)
    hdr = []
    body.append('[node name="%s" type="Node2D"]' % R.scene_name)
    body.append('script = ExtResource("bootstrap")')
    body.append('ScenePath = "%s"' % R.res_path)
    body.append('ZoneName = "%s"' % R.zone)
    body.append('CameraProfile = ExtResource("camera")')
    body.append('CameraLimitLeft = 0')
    body.append('CameraLimitRight = %d' % W)
    body.append('CameraLimitTop = %d' % (-H))
    body.append('CameraLimitBottom = 0')
    body.append('')
    body.append('[node name="Fondo" type="ColorRect" parent="."]\nz_index = -50\noffset_left = -400\noffset_top = %d\noffset_right = %d\noffset_bottom = 400\ncolor = %s\n'
                % (-H - 400, W + 400, fmt(C_FONDO)))
    body.append('[node name="Ambiente" type="CanvasModulate" parent="."]\ncolor = %s\n' % fmt(R.modulate))
    body.append('[node name="Colisiones" type="StaticBody2D" parent="."]\n')
    body.append('[node name="PlataformasOneWay" type="StaticBody2D" parent="."]\ncollision_layer = 49\n')
    body.append('[node name="Visible" type="Node2D" parent="."]\nz_index = -1\n')
    body.append('[node name="Trampas" type="Node2D" parent="."]\n')
    body.append('[node name="Enemigos" type="Node2D" parent="."]\n')
    body.append('[node name="Objetos" type="Node2D" parent="."]\n')
    for n, a, b, c, d in R.solids:
        w, h = c - a, d - b
        cx, cy = L((a + c) / 2, (b + d) / 2)
        body.append('[node name="%s" type="CollisionShape2D" parent="Colisiones"]\nposition = %s\nshape = SubResource("%s")\n'
                    % (n, fmt((cx, cy)), shape_id(w, h)))
    for n, pts in R.polys:
        lp = [L(*p) for p in pts]
        arr = ', '.join('%s, %s' % (fmt(float(x)), fmt(float(y))) for x, y in lp)
        body.append('[node name="%s" type="CollisionPolygon2D" parent="Colisiones"]\npolygon = PackedVector2Array(%s)\n' % (n, arr))
    for n, a, c, y in R.shelves:
        cx, cy = L((a + c) / 2, y + 6)
        body.append('[node name="%s" type="CollisionShape2D" parent="PlataformasOneWay"]\nposition = %s\nshape = SubResource("%s")\none_way_collision = true\n'
                    % (n, fmt((cx, cy)), shape_id(c - a, 12)))
    # visibles
    for n, a, b, c, d in R.solids:
        la, lb = L(a, b); lc, ld = L(c, d)
        body.append('[node name="%s" type="ColorRect" parent="Visible"]\noffset_left = %s\noffset_top = %s\noffset_right = %s\noffset_bottom = %s\ncolor = %s\n'
                    % (n, fmt(float(la)), fmt(float(lb)), fmt(float(lc)), fmt(float(ld)), fmt(C_MURO)))
        if not n.startswith(('ParedOeste', 'ParedEste', 'FondoBorde')):
            body.append('[node name="%sBorde" type="ColorRect" parent="Visible"]\noffset_left = %s\noffset_top = %s\noffset_right = %s\noffset_bottom = %s\ncolor = %s\n'
                        % (n, fmt(float(la)), fmt(float(lb)), fmt(float(lc)), fmt(float(lb + 5)), fmt(C_BORDE)))
    for n, pts in R.polys:
        lp = [L(*p) for p in pts]
        arr = ', '.join('%s, %s' % (fmt(float(x)), fmt(float(y))) for x, y in lp)
        body.append('[node name="%s" type="Polygon2D" parent="Visible"]\ncolor = %s\npolygon = PackedVector2Array(%s)\n' % (n, fmt(C_RAMPA), arr))
    for n, a, c, y in R.shelves:
        la, lb = L(a, y); lc, _ = L(c, y)
        body.append('[node name="%s" type="ColorRect" parent="Visible"]\noffset_left = %s\noffset_top = %s\noffset_right = %s\noffset_bottom = %s\ncolor = %s\n'
                    % (n, fmt(float(la)), fmt(float(lb)), fmt(float(lc)), fmt(float(lb + 12)), fmt(C_REPISA)))
    # entidades
    for key, n, x, y, props in R.ents:
        parent = '.'
        if key in ('escorpion', 'bicho', 'buitre', 'rata', 'murcielago', 'saqueador', 'saqueador_arco', 'saqueador_honda', 'saqueador_lanza', 'corpulento'):
            parent = 'Enemigos'
        elif key in ('rayo', 'espejo', 'receptor', 'cascada', 'guillotina', 'losa', 'placa', 'relevo', 'flecha', 'roca', 'pinchos',
                     'movediza', 'cinta', 'prensa', 'pendulo', 'rafaga', 'pozaarena', 'marea_node'):
            parent = '.'  # trampas en la raíz: los NodePath "../X" entre ellas funcionan sin rodeos
        if key == 'marea':
            body.append('[node name="%s" type="Node2D" parent="."]\nscript = ExtResource("marea")\nposition = %s' % (n, fmt(L(x, y))))
        else:
            body.append('[node name="%s" parent="%s" instance=ExtResource("%s")]\nposition = %s' % (n, parent, key, fmt(L(x, y))))
        for k, v in props.items():
            body.append('%s = %s' % (k, fmt(v, R)))
        body.append('')
    # transiciones
    for t in R.transitions:
        body.append('[node name="%s" parent="." instance=ExtResource("transition")]\nposition = %s\nTargetScenePath = "%s"\nTargetSpawnPosition = %s\nWidth = %s\nHeight = %s%s\n'
                    % (t['name'], fmt(L(*t['pos'])), t['target'], fmt(t['spawn']), fmt(float(t['size'][0])), fmt(float(t['size'][1])),
                       '\nRequireInteract = true' if t['interact'] else ''))
    # notas de diseño (ocultas en el juego; prender "NotasDiseno" en el editor)
    body.append('[node name="NotasDiseno" type="Node2D" parent="."]\nvisible = false\nz_index = 20\n')
    for i, (x, y, text) in enumerate(R.notes):
        lx, ly = L(x, y)
        body.append('[node name="Nota%d" type="Label" parent="NotasDiseno"]\noffset_left = %s\noffset_top = %s\noffset_right = %s\noffset_bottom = %s\ntheme_override_colors/font_color = Color(1, 0.9, 0.5, 1)\ntext = %s\nautowrap_mode = 2\n'
                    % (i + 1, fmt(float(lx)), fmt(float(ly)), fmt(float(lx + 420)), fmt(float(ly + 120)), fmt(text)))
    # jugador (el bootstrap lo mueve al punto de llegada)
    sp = R.spawn_default or (R.transitions[0]['spawn'] if R.transitions else (100, -40))
    if R.spawn_default: sp = L(*R.spawn_default)
    else:
        # punto de llegada de la primera conexión hacia esta sala
        for O in ROOMS.values():
            for t in O.transitions:
                if t['other'] == R.code: sp = t['spawn']; break
            else: continue
            break
    body.append('[node name="Player" parent="." instance=ExtResource("player")]\nposition = %s\n' % fmt(sp))
    for frm, sig, to, method in R.conns:
        body.append('[connection signal="%s" from="%s" to="%s" method="%s"]' % (sig, frm, to, method))

    lines = ['[gd_scene format=3]', '']
    for key in sorted(R.used):
        typ, path = SCENES[key]
        lines.append('[ext_resource type="%s" path="%s" id="%s"]' % (typ, path, key))
    lines.append('')
    for k, (w, h) in sorted(shapes.items()):
        lines.append('[sub_resource type="RectangleShape2D" id="%s"]\nsize = Vector2(%s, %s)\n' % (k, fmt(float(w)), fmt(float(h))))
    text = '\n'.join(lines + body) + '\n'
    # props que referencian ExtResource se resolvieron durante fmt: volver a armar cabecera
    lines = ['[gd_scene format=3]', '']
    for key in sorted(R.used):
        typ, path = SCENES[key]
        lines.append('[ext_resource type="%s" path="%s" id="%s"]' % (typ, path, key))
    lines.append('')
    for k, (w, h) in sorted(shapes.items()):
        lines.append('[sub_resource type="RectangleShape2D" id="%s"]\nsize = Vector2(%s, %s)\n' % (k, fmt(float(w)), fmt(float(h))))
    text = '\n'.join(lines + body) + '\n'
    path = os.path.join(ROOT, OUTDIR, R.scene_name + '.tscn')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    return path
