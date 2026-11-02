"""Archived prototype generator. Never overwrites the adopted campaign soundtrack."""
from pathlib import Path
import numpy as np
import wave

ROOT=Path(__file__).resolve().parents[1]/'ArtDirection/Generated/LegacyCampaignAudio'
ROOT.mkdir(parents=True,exist_ok=True)
SR=22050

def write(name,data):
    peak=max(.01,float(np.max(np.abs(data))))
    data=np.asarray(np.clip(data/peak*.74,-1,1)*32767,dtype='<i2')
    with wave.open(str(ROOT/(name+'.wav')),'wb') as wav:
        wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(SR);wav.writeframes(data.tobytes())

def note(track,at,beats,midi,volume,style,beat):
    count=int(beats*beat*SR);t=np.arange(count)/SR;f=440*2**((midi-69)/12)
    env=np.minimum(t/.015,1)*np.minimum((count/SR-t)/.10,1)*np.exp(-t/(beats*beat*.7))
    # Band-limited additive voices instead of harsh aliased square waves.
    voice=np.sin(t*f*2*np.pi)*.72+np.sin(t*f*4*np.pi)*.18+np.sin(t*f*6*np.pi)*.07
    if style=='bell':voice=np.sin(t*f*2*np.pi)+.25*np.sin(t*f*2.01*2*np.pi)*np.exp(-t*4)
    start=int(at*beat*SR);end=min(len(track),start+count)
    track[start:end]+=voice[:end-start]*env[:end-start]*volume

configs=[(80,50,[0,2,3,7,5,3,2,0],[0,5,3,7]),(72,52,[0,1,5,3,7,5,1,0],[0,1,5,3]),(88,48,[0,3,7,10,7,5,3,2],[0,8,5,7])]
for stage,(bpm,root,motif,chords) in enumerate(configs,1):
    beat=60/bpm;count=int(64*beat*SR);track=np.zeros(count);rng=np.random.default_rng(701+stage)
    for bar in range(16):
        chord=chords[(bar//2)%4]
        for pulse in range(4):
            note(track,bar*4+pulse,1.2,root-12+chord,.15,'bass',beat)
        for index in range(8):
            interval=motif[(index+(bar%2)*2)%8]
            if (bar+index)%7==0:continue
            note(track,bar*4+index*.5,.75,root+12+interval,.105 if stage==2 else .085,'bell',beat)
        for degree in (0,3,7):note(track,bar*4,3.85,root+chord+degree,.025,'pad',beat)
        # Sparse low drum and soft noise brush; stage three pulses more frequently.
        for pulse in ([0,2] if stage<3 else [0,1,2,3]):
            n=int(.17*SR);t=np.arange(n)/SR
            drum=np.sin(2*np.pi*(58*t+4*(1-np.exp(-t*25))))*np.exp(-t*28)*.13
            at=int((bar*4+pulse)*beat*SR);track[at:at+n]+=drum[:len(track[at:at+n])]
    track[:220]*=np.linspace(0,1,220);track[-220:]*=np.linspace(1,0,220)
    write(f'stage{stage}-music',track)
    t=np.arange(SR*24)/SR
    # Periodic waves ensure clean ambience loops. Halls have drips; the core has a low pulse.
    amb=np.sin(2*np.pi*45*t)*.015+np.sin(2*np.pi*(71+stage*5)*t)*.012
    amb*=.6+.4*np.sin(2*np.pi*t/24)**2
    if stage==2:
        for at in (1,3.7,7.3,11,15.7,20):
            n=int(.3*SR);u=np.arange(n)/SR
            sound=np.sin(2*np.pi*(850*u-400*u*u))*np.exp(-u*17)*.14
            a=int(at*SR);amb[a:a+n]+=sound
    else:
        for k in range(1,19):
            amb+=np.sin(2*np.pi*(100+k*13)*t+rng.random()*6.28)*.0007*np.sin(np.pi*t/24)**2
    write(f'stage{stage}-ambience',amb)
    print(stage,'music seconds',round(len(track)/SR,2),'ambience seconds',len(amb)//SR)
