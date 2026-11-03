import re,math,sys
GS=float(sys.argv[1]) if len(sys.argv)>1 else 1.0
UB=float(sys.argv[2]) if len(sys.argv)>2 else 1.55
EXPM=float(sys.argv[3]) if len(sys.argv)>3 else 1.0
REGM=float(sys.argv[4]) if len(sys.argv)>4 else 1.0
RESM=float(sys.argv[5]) if len(sys.argv)>5 else 1.0
HORIZON=float(sys.argv[6]) if len(sys.argv)>6 else 14

root='/home/claude/tycon-clouds/Assets/Scripts/Simulation/'
RES=['Coins','Dew','Water','Pollen','Wind','Clouds','Crystals','Essence','Goods','Fruit','Ice','Energy']
VAL=[1,1,4,6,3,10,30,400,55,18,40,70]
R={n:i for i,n in enumerate(RES)}
# ---------- parse buildings
bt=open(root+'Buildings/BuildingDatabase.cs',encoding='utf8').read()
blds={}
for m in re.finditer(r'Add\(new BuildingDef\((.*?)\)\s*;\s*\n',bt,re.S):
    s=m.group(1)
    h=re.match(r'"(\w+)",\s*"[^"]*",\s*(\w+),\s*(\d+),\s*(\d+),\s*([\d\.]+)\)',s)
    if not h: continue
    b=dict(id=h.group(1),cat=h.group(2),w=int(h.group(3)),d=int(h.group(4)),cost=float(h.group(5)),g=1.12,reg=0,res='',tier=0,ins=[],outs=[],cyc=1.0,role='Producer',rv=0,mx=0,mats=[],pre=-1)
    for k in re.finditer(r'\.(\w+)\(([^()]*(?:\([^()]*\)[^()]*)*)\)',s[h.end():]):
        f,a=k.group(1),k.group(2)
        if f=='Growth': b['g']=float(a)
        elif f=='Reg':
            q=re.match(r'(\d+),\s*"([^"]*)"',a); b['reg']=int(q.group(1)); b['res']=q.group(2)
        elif f=='Tr': b['tier']=int(a)
        elif f in('In','Out','Mat'):
            q=re.match(r'R\.(\w+),\s*([\d\.]+)',a); t=(R[q.group(1)],float(q.group(2)))
            b[{'In':'ins','Out':'outs','Mat':'mats'}[f]].append(t)
        elif f=='Cycle': b['cyc']=float(a)
        elif f=='AsRole':
            q=re.match(r'BuildingRole\.(\w+),\s*([\d\.]+)',a); b['role']=q.group(1); b['rv']=float(q.group(2))
        elif f=='Max': b['mx']=int(a)
        elif f=='Prestige': b['pre']=int(a)
    b['g']=1+(b['g']-1)*GS if b['role']=='Producer' else b['g']
    blds[b['id']]=b
# ---------- parse research
rt=open(root+'Research/ResearchDatabase.cs',encoding='utf8').read()
rs={}
for m in re.finditer(r'Add\(new ResearchDef\("(\w+)",\s*"[^"]*",\s*"[^"]*",\s*([\d\.]+),\s*(?:U|ResearchKind\.(\w+)),?\s*([\d\.]*)\)(.*?)\);\s*\n',rt,re.S):
    rid,cost,kind,val,tail=m.group(1),float(m.group(2)),m.group(3) or 'Unlock',m.group(4),m.group(5)
    r=dict(id=rid,cost=cost,kind=kind,val=float(val) if val else 0.0,pre=[],bld='',mats=[],reb=False)
    q=re.search(r'\.Pre\(([^)]*)\)',tail)
    if q: r['pre']=re.findall(r'"(\w+)"',q.group(1))
    q=re.search(r'\.Bld\("(\w+)"\)',tail)
    if q: r['bld']=q.group(1)
    for q in re.finditer(r'\.Mat\(R\.(\w+),\s*([\d\.]+)\)',tail): r['mats'].append((R[q.group(1)],float(q.group(2))))
    if '.Rebirth()' in tail: r['reb']=True
    rs[rid]=r
print(len(blds),'buildings',len(rs),'research',file=sys.stderr)
REG=[dict(cost=0,res='',mats=[],exp=3000),
 dict(cost=30000,res='sky_navigation',mats=[(2,150),(3,100)],exp=50000),
 dict(cost=600000,res='polar_expedition',mats=[(3,1500),(9,800),(8,300)],exp=8e5),
 dict(cost=12e6,res='storm_chasing',mats=[(6,2500),(10,1500),(8,2500)],exp=8e6),
 dict(cost=150e6,res='celestial_ascension',mats=[(11,8000),(6,6000),(8,10000)],exp=80e6)]
# ---------- state
coins=15.0; amt=[0.0]*12; blist=[]  # list of [id,level]
done=set(); regs=[True,False,False,False,False]; expn=[0]*5; gu=[0,0,0,0]
auto=[False]*12; auto[1]=True
t=0.0; log=[]; earned=0.0
def count(i): return sum(1 for b in blist if b[0]==i)
def mods():
    prod=0;sto=0;build=0;upg=0;sell=0;click=0;off=2;clim=0;obs=0;flat=0;proc=0;mk=False;mb=0;rc=0
    for r in done:
        x=rs[r];k=x['kind']
        if k=='Production':prod+=x['val']
        elif k=='Storage':sto+=x['val']
        elif k=='BuildDiscount':build+=x['val']
        elif k=='UpgradeDiscount':upg+=x['val']
        elif k=='SellBonus':sell+=x['val']
    prod+=0.08*gu[0]; sto+=0.12*gu[1]
    for i,l in blist:
        b=blds[i]
        if b['role']=='Storage': flat+=b['rv']*(1+0.5*(l-1))*(1.5 if l>=10 else 1)
        elif b['role']=='Market': mk=True; mb=max(mb,b['rv']*(l-1))
        elif b['role']=='Observatory': obs+=b['rv']*l
        elif b['role']=='ClimateCore': clim+=b['rv']*l
        elif b['role']=='IndustrialCenter': proc+=b['rv']*l
        elif b['role']=='ResearchCenter': rc=max(rc,l)
    return dict(gp=(1+prod+obs)*(1+clim),cap=(200+flat)*(1+sto),build=min(.6,build),upg=min(.6,upg),sell=(1+sell)*(1+mb),mk=mk,proc=proc,inp=min(.5,.025*gu[2]),rc=min(.6,.03*rc))
def lv(l):
    return (1+0.1*(l-1)),(1+0.3*(l-1))*(1.5 if l>=10 else 1),(1+0.04*(l-1))
def unlocked(b):
    if b['pre']>=0: return False
    return regs[b['reg']] and (b['res']=='' or b['res'] in done)
M=mods(); DT=1.0
def step():
    global coins,earned,M
    order=sorted(blist,key=lambda x:blds[x[0]]['tier'])
    for i,l in order:
        b=blds[i]
        if b['role']!='Producer': continue
        sp,am,ef=lv(l)
        if b['cat'] in('Processing','Industry'): sp*=1+M['proc']
        cyc=DT*sp/b['cyc']
        sc=1.0
        for r,a in b['ins']:
            need=a*am*(1-M['inp'])*cyc
            if need>1e-12: sc=min(sc,amt[r]/need)
        for r,a in b['outs']:
            if r==0: continue
            pr=a*am*ef*M['gp']*cyc
            if pr>1e-12: sc=min(sc,max(0,M['cap']-amt[r])/pr)
        sc=max(0,min(1,sc))
        if sc>0:
            for r,a in b['ins']: amt[r]=max(0,amt[r]-a*am*(1-M['inp'])*cyc*sc)
            for r,a in b['outs']:
                v=a*am*ef*M['gp']*cyc*sc
                if r==0: coins+=v; earned+=v
                else: amt[r]=min(M['cap'],amt[r]+v)
    if M['mk']:
        for r in range(1,12):
            if auto[r]:
                ex=amt[r]-M['cap']*0.5*0  # sell above 0 reserve; bot manages auto flags
                if ex>0:
                    c=ex*VAL[r]*M['sell']; coins+=c; earned+=c; amt[r]-=ex
def bcost(i):
    b=blds[i]; return b['cost']*(b['g']**count(i))*(1-M['build'])
def free_tiles():
    tot=sum((4+e)*(4+e)*4 for e,u in zip(expn,regs) if u)
    used=sum(blds[i]['w']*blds[i]['d'] for i,l in blist)
    return tot-used
def value_rate(i,l=1):
    b=blds[i]; sp,am,ef=lv(l)
    if b['cat'] in('Processing','Industry'): sp*=1+M['proc']
    o=sum(a*am*ef*M['gp']*(VAL[r]*M['sell']) for r,a in b['outs'])*sp/b['cyc']
    n=sum(a*am*(1-M['inp'])*VAL[r] for r,a in b['ins'])*sp/b['cyc']
    return o-n
def supply_ok(i):
    # does current production support another consumer? rough: stock of each input must be >5% cap or produced
    b=blds[i]
    for r,a in b['ins']:
        prod=sum(1 for j,l in blist if any(o[0]==r for o in blds[j]['outs']))
        if prod==0: return False
    return True
def decide():
    global coins,M,earned
    if not M['mk']:
        for r in range(1,12):
            if r==1 or amt[r]>0.9*M['cap']:
                c=amt[r]*VAL[r]*M['sell']; coins+=c; earned+=c; amt[r]=0
    acted=True
    while acted:
        acted=False
        best=None
        # candidates: (score = gain/cost)
        for i,b in blds.items():
            if not unlocked(b): continue
            if b['mx'] and count(i)>=b['mx']: continue
            reserve=0
            if b['role']=='Producer' or b['role']=='Storage':
                for gi in ('research_center','air_port','observatory','comm_tower','market','climate_core','industrial_center'):
                    if count(gi)==0 and gi in blds and (regs[blds[gi]['reg']]): reserve+=blds[gi]['w']*blds[gi]['d']
            if free_tiles()<b['w']*b['d']+reserve: continue
            c=bcost(i)
            if c>coins: continue
            mm=all(amt[r]>=a*(1.08**count(i)) for r,a in b['mats'])
            if not mm: continue
            if b['role']=='Producer':
                if not supply_ok(i): continue
                g=value_rate(i)
                # penalise duplicates of consumers when input stock is empty
                for r,a in b['ins']:
                    if amt[r]<0.05*M['cap'] and count(i)>0: g*=0.1
            elif b['role']=='Market': g=50 if count(i)==0 else 0
            elif b['role']=='Storage':
                full=sum(1 for r in range(1,12) if amt[r]>0.9*M['cap'])
                g=(full*50)/max(1,(count(i)+1)) if full else 0
            elif b['role']=='Port': g=5 if count(i)==0 else 0
            elif b['role']=='ResearchCenter': g=3 if count(i)==0 else 0
            elif b['role'] in('Observatory','ClimateCore','IndustrialCenter','CommTower'):
                g=2 if count(i)==0 else 0
            else: g=0
            if g>0:
                s=g/c
                if best is None or s>best[0]: best=(s,'b',i,c)
        # upgrades
        for k,(i,l) in enumerate(blist):
            b=blds[i]
            if l>=10 or b['role']!='Producer': continue
            c=b['cost']*0.8*UB**l*(1-M['upg'])
            if c>coins: continue
            g=value_rate(i,l+1)-value_rate(i,l)
            sup=all(amt[r]>0.02*M['cap'] or sum(1 for j,l2 in blist if any(o[0]==r for o in blds[j]['outs']))>0 for r,a in b['ins'])
            if g>0 and sup:
                s=g/c
                if best is None or s>best[0]: best=(s,'u',k,c)
        for rid,x in rs.items():
            if rid in done or x['reb']: continue
            if any(p not in done for p in x['pre']): continue
            if x['bld'] and count(x['bld'])==0: continue
            c=x['cost']*RESM*(1-M['rc'])
            if c>coins or not all(amt[r]>=a for r,a in x['mats']): continue
            # value of research: unlocks + bonuses -> assume decent priority
            k=x['kind']
            g=0
            if k=='Production': g=value_rate_total()*x['val']
            elif k in('Storage','Offline','SellBonus','BuildDiscount','UpgradeDiscount','TradeBonus','ClickBonus'): g=income()*0.05
            elif k=='RegionGate': g=income()*0.3
            else: g=income()*0.12+1
            s=g/max(c,1)*3
            if best is None or s>best[0]: best=(s,'r',rid,c)
        for r in range(1,5):
            if regs[r] or not regs[r-1]: continue
            x=REG[r]
            if x['res'] in done and coins>=x['cost']*REGM and all(amt[q]>=a for q,a in x['mats']):
                best=(1e9,'g',r,x['cost']*REGM)
        for r in range(5):
            if regs[r] and expn[r]<4:
                c=REG[r]['exp']*EXPM*3.2**expn[r]
                if c<=coins and free_tiles()<8: best=(1e6,'e',r,c)
        for k in range(4):
            mxl=[25,20,16,10][k]
            if gu[k]<mxl and k in(0,1):
                c=[5000,3000,20000,200][k]*[1.9,1.8,2,1.7][k]**gu[k]
                if c<=coins:
                    g=income()*(0.08 if k==0 else 0.03)
                    s=g/c
                    if best is None or s>best[0]: best=(s,'gu',k,c)
        if best:
            _,kind,x,c=best
            coins-=c
            if kind=='b':
                b=blds[x]
                for r,a in b['mats']: amt[r]-=a*(1.08**count(x))
                blist.append([x,1])
            elif kind=='u': blist[x][1]+=1
            elif kind=='r':
                done.add(x)
                for r,a in rs[x]['mats']: amt[r]-=a
            elif kind=='g':
                regs[x]=True
                for r,a in REG[x]['mats']: amt[r]-=a
                log.append((t,'REGION %d unlocked'%x))
            elif kind=='e': expn[x]+=1
            elif kind=='gu': gu[x]+=1
            M=mods(); acted=True
    # auto sell flags: sell what no owned consumer needs
    needed=set(r for i,l in blist for r,a in blds[i]['ins'])
    hold=set()
    for r in range(1,5):
        if not regs[r] and regs[r-1]:
            for q,a in REG[r]['mats']: hold.add(q)
    for rid,x in rs.items():
        if rid not in done and not x['reb'] and all(p in done for p in x['pre']):
            for q,a in x['mats']: hold.add(q)
    for r in range(1,12):
        auto[r]=((r not in needed) and (r not in hold)) or amt[r]>0.9*M['cap']
def income(): 
    return sum(max(0,value_rate(i,l)) for i,l in blist)*0.7+0.1
def value_rate_total(): return income()
last=0;nextlog=3600
while t<HORIZON*3600:
    step(); t+=DT
    if int(t)%5==0: decide()
    if t>=nextlog:
        print('%.1fh coins=%.3g income=%.3g bld=%d research=%d regs=%s expn=%s'%(t/3600,coins,income(),len(blist),len(done),''.join('1' if r else '0' for r in regs),expn)); nextlog+=3600
for l in log: print('%.2fh %s'%(l[0]/3600,l[1]))
print('final earned %.3g'%earned)
