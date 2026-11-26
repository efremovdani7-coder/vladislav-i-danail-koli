"""Builds a compact Bulgarian car catalog (brand -> model -> generation -> engine -> gearbox/drive)
from the public autoevolution.com dataset (github.com/ilyasozkurt/automobile-models-and-specs)."""
import json, ast, re, html, sys, collections, difflib, unicodedata, statistics
src, out = sys.argv[1], sys.argv[2]
BRANDS = {
 'ALFA ROMEO':'Alfa Romeo','AUDI':'Audi','BMW':'BMW','CHEVROLET':'Chevrolet','CHRYSLER':'Chrysler',
 'CITROEN':'Citroen','CUPRA':'Cupra','DACIA':'Dacia','DAEWOO':'Daewoo','DODGE':'Dodge','DS AUTOMOBILES':'DS',
 'FIAT':'Fiat','FORD':'Ford','HONDA':'Honda','HYUNDAI':'Hyundai','INFINITI':'Infiniti','ISUZU':'Isuzu',
 'JAGUAR':'Jaguar','JEEP':'Jeep','KIA':'Kia','LADA':'Lada','LANCIA':'Lancia','LAND ROVER':'Land Rover',
 'LEXUS':'Lexus','MAZDA':'Mazda','MERCEDES BENZ':'Mercedes-Benz','Mercedes-AMG':'Mercedes-Benz','MG':'MG',
 'MINI':'Mini','MITSUBISHI':'Mitsubishi','NISSAN':'Nissan','OPEL':'Opel','PEUGEOT':'Peugeot','PORSCHE':'Porsche',
 'RENAULT':'Renault','SAAB':'Saab','SEAT':'Seat','SKODA':'Skoda','SMART':'Smart','SSANGYONG':'SsangYong',
 'SUBARU':'Subaru','SUZUKI':'Suzuki','TESLA':'Tesla','TOYOTA':'Toyota','VOLKSWAGEN':'Volkswagen','VOLVO':'Volvo',
 'Polestar':'Polestar','GEELY':'Geely',
}
MIN_YEAR, NOW = 1995, 2026
TWO_WORD_FIRST = {'grand','range','model','land','santa','great','space','grande','town','new','id.','e-tron'}
TWO_WORD_SECOND = {'series','class','klasse','cruiser','rover','sport','fe'}
FUEL = {'Gasoline':'Petrol','Ethanol':'Petrol','Diesel':'Diesel','Electric':'Electric','Hybrid':'Hybrid',
        'Hybrid Gasoline':'Hybrid','Mild Hybrid':'Hybrid','Mild Hybrid Diesel':'Hybrid','Hybrid Diesel':'Hybrid',
        'Plug-In Hybrid':'PlugInHybrid','Natural Gas':'CNG','Liquefied Petroleum Gas (Lpg)':'LPG'}
DRIVE = {'Front Wheel Drive':'Предно','Rear Wheel Drive':'Задно','All Wheel Drive':'4x4'}

def specs(y):
    s = y['specs']
    if isinstance(s, str):
        try: s = ast.literal_eval(s)
        except Exception: return {}
    return s or {}

def gearbox_bg(g):
    if not g or g == 'None': return 'Не е посочена'
    gl = g.lower()
    n = re.search(r'(\d+)', g)
    speeds = f'{n.group(1)}-степенна ' if n else ''
    extra = re.search(r'\(([^)]+)\)', g)
    extra = extra.group(1) if extra else ''
    for k in ['steptronic','dsg','s tronic','tiptronic','pdk','g-tronic','g tronic','multitronic','powershift','edc','easytronic']:
        if not extra and k in gl: extra = k
    extra = extra.upper() if extra.lower() in ('dsg','pdk','edc','dct') else extra.title()
    if 'cvt' in gl and 'manual' not in gl:
        return 'Безстепенна (CVT)' + (f' {extra}' if extra and extra.lower() not in ('cvt','ecvt') else '')
    if 'dct' in gl or 'dual' in gl:
        return f'{speeds}автоматична с двоен съединител (DCT)'.capitalize()
    if 'manual' in gl:
        return f'{speeds}ръчна'.capitalize()
    if 'automat' in gl or 'auto' in gl:
        return (f'{speeds}автоматична' + (f' ({extra})' if extra else '')).capitalize()
    return g

def num(s, unit):
    m = re.search(r'([\d.,]+)\s*' + unit, s or '', re.I)
    return float(m.group(1).replace(',', '')) if m else None

def engine_label(name, sp, fuel):
    n = re.sub(r'\s+', ' ', name).strip()
    n = re.sub(r'\be-?CVT\b', '', n, flags=re.I)
    n = re.sub(r'\b\d*\s?(MT|AT|DCT|AMT|DSG|CVT|SMG|PDK|AMG SPEEDSHIFT)\b', '', n, flags=re.I)
    n = re.sub(r'\b(FWD|RWD|AWD|4WD|4X4)\b', '', n, flags=re.I)
    n = re.sub(r'\s+', ' ', n).replace('( ', '(').strip()
    hp = num(sp.get('Power:'), 'Hp'); cc = num(sp.get('Displacement:'), 'Cm3')
    n = re.sub(r'\be-\s', '', n).strip()
    if '(' not in n and hp: n += f' ({int(hp)} HP)'
    if cc and not re.search(r'\d\.\d', n): n = f'{cc/1000:.1f}L ' + n
    tag = {'Hybrid': 'Хибрид', 'PlugInHybrid': 'Plug-in хибрид', 'Electric': 'Електрически', 'LPG': 'Газ', 'CNG': 'Метан'}.get(fuel)
    if tag and not re.search(r'hybrid|hev|phev|electric|ev\b|kw', n, re.I): n = (n.split('(')[0].strip() + f' {tag} (' + n.split('(',1)[1]) if '(' in n else n + ' ' + tag
    n = re.sub(r'(\d+)\s*HP', r'\1 к.с.', n, flags=re.I)
    return n.strip(' -')

MB_CLASSES = {'A','B','C','CL','CLA','CLC','CLE','CLK','CLS','E','G','GL','GLA','GLB','GLC','GLE','GLK','GLS','M','ML','R','S','SL','SLC','SLK','SLR','SLS','V','X'}
BODY_WORDS = [
    ('Convertible', r'cabrio|convertible|roadster|spider|spyder|volante|targa|open air'),
    ('Pickup', r'pick-?up|double cab|crew cab|king cab|single cab|extended cab|club cab|regular cab'),
    ('Van', r'\bvan\b|minivan|\bmpv\b|multivan|caravelle|transporter|tourneo|kangoo|berlingo|partner|caddy|sharan|galaxy|espace|zafira|touran|scenic|alhambra|picasso|\bvito\b|v-class|odyssey|sienna|carnival|grand c-max|s-max|c-max'),
    ('Wagon', r'wagon|estate|touring|avant|variant|kombi|combi|sports ?tourer|\bsw\b|break|shooting brake|allroad|sportbrake|sportswagon|cross country|grandtour|caravan'),
    ('Coupe', r'coup[eé]|gran coupe'),
    ('Sedan', r'sedan|saloon|limousine|\blimo\b|notchback'),
    ('Hatchback', r'hatchback|\bhatch\b|[35] ?doors|sportback|liftback|fastback'),
]

SUV_NAMES = (r'\bcross\b|crossover|\bx[1-7]\b|\bq[2-8]\b|tiguan|touareg|t-roc|t-cross|taigo|kodiaq|karoq|kamiq|duster|bigster|rav ?4|cr-v|hr-v|zr-v|'
             r'qashqai|x-trail|juke|ariya|tucson|santa fe|sportage|sorento|kuga|puma|ecosport|captur|kadjar|koleos|austral|arkana|'
             r'\b[235]008\b|outlander|\basx\b|forester|outback|xv\b|xc[469]0|range rover|discovery|defender|evoque|velar|freelander|macan|cayenne|'
             r'\bgl[abcesk]?\b|\bml\b|g-class|eq[abcs] suv|niro|kona|mokka|grandland|crossland|antara|vitara|jimny|s-cross|cx-[0-9]+|land cruiser|'
             r'highlander|c-hr|bz4x|bayon|ix35|ix55|stonic|seltos|ev9|ev6|ateca|arona|tarraco|formentor|enyaq|id\.4|id\.5|id\.7|\bq4\b|e-tron|model x|model y|'
             r'wrangler|cherokee|compass|renegade|avenger|tivoli|korando|rexton|torres|bronco|explorer|edge|escape|pathfinder|patrol|murano|'
             r'pajero|shogun|captiva|trax|tahoe|suburban|sandero stepway|spring|ux\b|nx\b|rx\b|gx\b|lx\b|fx\b|qx[0-9]+|stelvio|tonale|levante|'
             r'xceed|tonale|\bcx\b|countryman|\bdr\b|niva|xray|polestar 3|polestar 4|c5 aircross|c3 aircross|ds ?7|ds ?3 crossback|kamiq')

def body_type(name, dim):
    n = name.lower()
    for body, pattern in BODY_WORDS:
        if re.search(pattern, n): return body
    if re.search(SUV_NAMES, n): return 'Suv'
    h, gc, l = dim.get('h', 0), dim.get('gc', 0), dim.get('l', 0)
    if h >= 1580 and (gc >= 165 or h >= 1650): return 'Suv'
    if h and h < 1340: return 'Coupe'
    if l and l < 4350: return 'Hatchback'
    return 'Sedan' if l else 'Hatchback'

def family(model, brand=None):
    w = model.split()
    if brand == 'Mercedes-Benz':
        m = re.match(r'^([A-Z]{1,3})(?:-?Class|-?Klasse|\d{2,3}[a-z]?)?$', w[0])
        if m and m.group(1) in MB_CLASSES:
            return ('M' if m.group(1) == 'ML' else m.group(1)) + '-Class'
        if w[0] == 'AMG' and len(w) > 1: return 'AMG ' + w[1]
    if len(w) > 1 and (w[0].lower() in TWO_WORD_FIRST or w[1].lower() in TWO_WORD_SECOND):
        return ' '.join(w[:2])
    return w[0]

brands = {x['id']: x['name'] for x in json.load(open(src + '/brands.json'))}
autos = json.load(open(src + '/automobiles.json'))
engines = collections.defaultdict(list)
for y in json.load(open(src + '/engines.json')):
    engines[str(y['automobile_id'])].append(y)

cat = collections.defaultdict(lambda: collections.defaultdict(dict))
gen_dims = {}
fam_names = collections.defaultdict(collections.Counter)
for a in autos:
    raw = brands.get(int(a['brand_id']))
    if raw not in BRANDS: continue
    bname = BRANDS[raw]
    n = html.unescape(a['name']).replace('Photos, engines & full specs', '').strip()
    yf = yt = None
    m = re.search(r'\s(\d{4})\s*-\s*(\d{4}|Present)$', n)
    if m:
        yf, yt = int(m.group(1)), (NOW if m.group(2) == 'Present' else int(m.group(2))); n = n[:m.start()]
    else:
        m = re.match(r'^(\d{4})\s+', n)
        if m: yf = yt = int(m.group(1)); n = n[m.end():]
    if not yf or yt < MIN_YEAR: continue
    for p in sorted({raw, bname, raw.replace(' ', '-'), 'Mercedes-Benz', 'Mercedes Benz', 'Mercedes-AMG'} if bname=='Mercedes-Benz' else {raw, bname, raw.replace(' ', '-')}, key=len, reverse=True):
        if n.lower().startswith(p.lower() + ' '): n = n[len(p):].strip(); break
    n = re.sub(r'\s+', ' ', n).strip()
    if not n: continue
    # source typos like "Audu A4" / "Meredes-Benz C" / "Peugeot 308": drop the brand-like first word
    w0 = n.split()[0]
    if len(n.split()) > 1 and difflib.SequenceMatcher(None, w0.lower(), bname.lower()).ratio() > 0.7:
        n = n.split(' ', 1)[1]
    if re.match(r'^(Mercedes)?.?Maybach', n.split()[0], re.I): n = 'Maybach ' + n.split(' ', 1)[-1] if ' ' in n else 'Maybach'
    fam = family(n, bname)
    fkey = unicodedata.normalize('NFKD', fam).encode('ascii', 'ignore').decode().lower().replace('-', ' ')
    fam_names[(bname, fkey)][fam] += 1
    gen_key = (n, yf, yt)
    gen = cat[bname][fkey].setdefault(gen_key, {})
    dims = gen_dims.setdefault((bname, fkey, gen_key), collections.defaultdict(list))
    for y in engines.get(str(a['id']), []):
        sp = specs(y); es = sp.get('Engine Specs', {}); ts = sp.get('Transmission Specs', {})
        ds = sp.get('Dimensions', {}); ws = sp.get('Weight Specs', {})
        for key, field, unit in [('l', 'Length:', 'Mm'), ('w', 'Width:', 'Mm'), ('h', 'Height:', 'Mm'),
                                 ('wb', 'Wheelbase:', 'Mm'), ('gc', 'Ground Clearance:', 'Mm'), ('cargo', 'Cargo Volume:', 'L')]:
            v = num(ds.get(field), unit)
            if v: dims[key].append(v)
        v = num(ws.get('Unladen Weight:'), 'Kg')
        if v: dims['kg'].append(v)
        fuel = FUEL.get(es.get('Fuel:'))
        if not fuel: continue
        label = engine_label(y['name'], es, fuel)
        e = gen.setdefault(label, {'f': fuel, 'cc': num(es.get('Displacement:'), 'Cm3'),
                                   'hp': num(es.get('Power:'), 'Hp'), 'nm': num(es.get('Torque:'), 'Nm'), 'v': set()})
        e['v'].add((gearbox_bg(ts.get('Gearbox:')), DRIVE.get(ts.get('Drive Type:'), 'Не е посочено')))

result = []
stats = collections.Counter()
for b in sorted(cat):
    models = []
    for fam in sorted(cat[b], key=str.lower):
        gens = []
        for (gname, yf, yt), engs in sorted(cat[b][fam].items(), key=lambda kv: (kv[0][1], kv[0][0])):
            if not engs: continue
            el = []
            for label, e in sorted(engs.items(), key=lambda kv: ((kv[1]['cc'] or 0), kv[1]['hp'] or 0, kv[0])):
                el.append({'n': label, 'f': e['f'], 'cc': int(e['cc']) if e['cc'] else None,
                           'hp': int(e['hp']) if e['hp'] else None, 'nm': int(e['nm']) if e['nm'] else None,
                           'v': [list(v) for v in sorted(e['v'])]})
                stats['engines'] += 1; stats['variants'] += len(e['v'])
            d = gen_dims.get((b, fam, (gname, yf, yt)), {})
            dim = {k: int(statistics.median(v)) for k, v in d.items() if v}
            # sanity limits (mm / L / kg)
            for k, lo, hi in [('l', 2400, 6500), ('w', 1400, 2300), ('h', 1000, 2300), ('wb', 1700, 4200), ('gc', 80, 350), ('cargo', 50, 3500), ('kg', 500, 4000)]:
                if k in dim and not lo <= dim[k] <= hi: del dim[k]
            if dim.get('l') and dim.get('wb') and dim['wb'] >= dim['l']: dim.pop('wb')
            g = {'n': gname, 'from': yf, 'to': yt, 'body': body_type(gname, dim), 'e': el}
            if dim: g['dim'] = dim; stats['with_dims'] += 1
            gens.append(g); stats['generations'] += 1
        disp = sorted(fam_names[(b, fam)].items(), key=lambda kv: (-kv[1], kv[0] != kv[0].title(), kv[0]))[0][0].replace('-Series', ' Series')
        # поколения без размери: взимаме размерите на най-близкото поколение от същия модел (отбелязани като приблизителни)
        for g in gens:
            if 'dim' in g: continue
            donors = [x for x in gens if 'dim' in x and not x.get('approx')]
            if not donors: continue
            donor = min(donors, key=lambda x: (x['body'] != g['body'], abs(x['from'] - g['from']) + abs(x['to'] - g['to'])))
            g['dim'] = dict(donor['dim']); g['approx'] = 1; stats['approx_dims'] += 1
        if gens: models.append({'n': disp, 'g': gens}); stats['models'] += 1
    if models: result.append({'n': b, 'm': models}); stats['brands'] += 1
json.dump({'source': 'autoevolution.com via github.com/ilyasozkurt/automobile-models-and-specs (23.10.2024)',
           'brands': result}, open(out, 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
print(dict(stats))
