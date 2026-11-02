"""Read-only NVIDIA driver frame limit audit. Never creates, changes or saves profiles.

ABI references: NVIDIA/nvapi main nvapi.h, nvapi_interface.h, NvApiDriverSettings.h.
https://github.com/NVIDIA/nvapi (public API declarations, accessed 2026-09-21).
"""
import ctypes as c
import json
from pathlib import Path

U32=c.c_uint32
Handle=c.c_void_p
WString=c.c_uint16*2048
class Value(c.Union):
    _pack_=4
    _fields_=[('dword',U32),('bytes',c.c_ubyte*4100)]
class Setting(c.Structure):
    _pack_=4
    _fields_=[('version',U32),('name',WString),('id',U32),('type',U32),('location',U32),('predefined',U32),('predefinedValid',U32),('default',Value),('current',Value)]
class Application(c.Structure):
    _fields_=[('version',U32),('predefined',U32),('name',WString),('friendly',WString),('launcher',WString)]

dll=c.CDLL('nvapi64.dll')
query=dll.nvapi_QueryInterface
query.argtypes=[U32];query.restype=Handle
def api(identifier,*params):
    pointer=query(identifier)
    if not pointer:raise RuntimeError(f'Unsupported NVAPI {identifier:x}')
    return c.CFUNCTYPE(c.c_int,*params)(pointer)
def require(status):
    if status:raise RuntimeError(f'NVAPI status {status}')
require(api(0x0150e828)())
session=Handle()
require(api(0x0694d52e,c.POINTER(Handle))(c.byref(session)))
report={'read_only':True,'settings':[]}
try:
    require(api(0x375dbd6b,Handle)(session))
    globalProfile=Handle()
    require(api(0x617bff9f,Handle,c.POINTER(Handle))(session,c.byref(globalProfile)))
    profiles=[('global',globalProfile)]
    baseProfile=Handle()
    require(api(0xda8466a0,Handle,c.POINTER(Handle))(session,c.byref(baseProfile)))
    profiles.append(('base',baseProfile))
    application=Application();application.version=c.sizeof(Application)|(1<<16)
    appProfile=Handle()
    status=api(0xeee566b2,Handle,c.c_wchar_p,c.POINTER(Handle),c.POINTER(Application))(session,'RuinRunner.exe',c.byref(appProfile),c.byref(application))
    report['application_lookup_status']=status
    if status==0:profiles.append(('RuinRunner.exe',appProfile))
    get=api(0x73bf8338,Handle,Handle,U32,c.POINTER(Setting))
    for profileName,profile in profiles:
        for name,identifier in [('frame_limit',0x10835002),('idle_frame_limit',0x10835016),('idle_threshold_seconds',0x10835017),('battery_boost_fps',0x10115c8c)]:
            value=Setting();value.version=c.sizeof(Setting)|(1<<16)
            status=get(session,profile,identifier,c.byref(value))
            row={'profile':profileName,'name':name,'status':status}
            if status:
                message=c.create_string_buffer(64)
                api(0x6c2d048c,c.c_int,c.c_void_p)(status,message)
                row['status_message']=message.value.decode(errors='replace')
            if status==0:row.update(value=value.current.dword,location=value.location,predefined=value.predefined,type=value.type)
            report['settings'].append(row)
finally:
    api(0xdad9cff8,Handle)(session)
    api(0xd22bdd7e)()
output=Path('Logs/Validation/2026-09-21-performance/nvidia-frame-limits.json')
output.write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
