"""Read-only Windows display and NVIDIA presentation settings.

No driver Set/Save API is loaded. No display, power, registry, or process state is
changed. Settings IDs/layouts follow NVIDIA's public nvapi headers; display
layouts follow Microsoft's DEVMODEW/DISPLAY_DEVICEW definitions.
"""
import argparse
import ctypes as c
from ctypes import wintypes as w
import datetime as dt
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys


class DisplayDevice(c.Structure):
    _fields_ = [("size", w.DWORD), ("name", w.WCHAR * 32), ("description", w.WCHAR * 128),
                ("flags", w.DWORD), ("id", w.WCHAR * 128), ("key", w.WCHAR * 128)]


class DevMode(c.Structure):
    _fields_ = [("name", w.WCHAR * 32), ("spec", w.WORD), ("driver", w.WORD),
                ("size", w.WORD), ("extra", w.WORD), ("fields", w.DWORD),
                ("x", w.LONG), ("y", w.LONG), ("orientation", w.DWORD), ("fixed", w.DWORD),
                ("color", c.c_short), ("duplex", c.c_short), ("yres", c.c_short),
                ("tt", c.c_short), ("collate", c.c_short), ("form", w.WCHAR * 32),
                ("logpixels", w.WORD), ("bits", w.DWORD), ("width", w.DWORD),
                ("height", w.DWORD), ("displayflags", w.DWORD), ("frequency", w.DWORD),
                ("icm", w.DWORD), ("intent", w.DWORD), ("media", w.DWORD), ("dither", w.DWORD),
                ("reserved1", w.DWORD), ("reserved2", w.DWORD), ("panwidth", w.DWORD), ("panheight", w.DWORD)]


def displays():
    user = c.WinDLL("user32")
    enum = user.EnumDisplayDevicesW
    enum.argtypes = [w.LPCWSTR, w.DWORD, c.POINTER(DisplayDevice), w.DWORD]
    modes = user.EnumDisplaySettingsW
    modes.argtypes = [w.LPCWSTR, w.DWORD, c.POINTER(DevMode)]
    values = []
    for index in range(32):
        device = DisplayDevice(size=c.sizeof(DisplayDevice))
        if not enum(None, index, c.byref(device), 0):
            break
        if not device.flags & 1:
            continue
        mode = DevMode(size=c.sizeof(DevMode))
        valid = bool(modes(device.name, 0xFFFFFFFF, c.byref(mode)))
        values.append({"device": device.name, "adapter": device.description,
                       "primary": bool(device.flags & 4), "modeAvailable": valid,
                       "width": mode.width, "height": mode.height, "refreshHz": mode.frequency,
                       "x": mode.x, "y": mode.y})
    return {"displays": values, "remoteSession": bool(user.GetSystemMetrics(0x1000))}


class Binary(c.Structure):
    _pack_ = 4
    _fields_ = [("length", c.c_uint32), ("data", c.c_uint8 * 4096)]


class Value(c.Union):
    _pack_ = 4
    _fields_ = [("dword", c.c_uint32), ("binary", Binary), ("text", c.c_wchar * 2048), ("qword", c.c_uint64)]


class Setting(c.Structure):
    _pack_ = 4
    _fields_ = [("version", c.c_uint32), ("name", c.c_wchar * 2048),
                ("id", c.c_uint32), ("type", c.c_uint32), ("location", c.c_uint32),
                ("predefined", c.c_uint32), ("predefinedValid", c.c_uint32),
                ("default", Value), ("current", Value)]


class Application(c.Structure):
    _pack_ = 4
    _fields_ = [("version", c.c_uint32), ("predefined", c.c_uint32),
                ("name", c.c_wchar * 2048), ("friendly", c.c_wchar * 2048), ("launcher", c.c_wchar * 2048),
                ("fileInFolder", c.c_wchar * 2048), ("flags", c.c_uint32), ("commandLine", c.c_wchar * 2048)]


def nvidia_settings(executables=()):
    assert c.sizeof(Setting) == 12320 and c.sizeof(Application) == 20492
    library = c.CDLL(str(Path(os.environ["SystemRoot"]) / "System32/nvapi64.dll"))
    query = library.nvapi_QueryInterface
    query.argtypes, query.restype = [c.c_uint32], c.c_void_p

    def function(identifier, *types):
        address = query(identifier)
        if not address:
            raise RuntimeError("NVIDIA read API unavailable: " + hex(identifier))
        return c.CFUNCTYPE(c.c_int, *types)(address)

    def require(status):
        if status:
            raise RuntimeError("NVIDIA read API status: " + str(status))

    require(function(0x0150e828)())
    create = function(0x0694d52e, c.POINTER(c.c_void_p))
    destroy = function(0xdad9cff8, c.c_void_p)
    load = function(0x375dbd6b, c.c_void_p)
    current_global = function(0x617bff9f, c.c_void_p, c.POINTER(c.c_void_p))
    get_base = function(0xda8466a0, c.c_void_p, c.POINTER(c.c_void_p))
    find_app = function(0xeee566b2, c.c_void_p, c.c_wchar_p, c.POINTER(c.c_void_p), c.POINTER(Application))
    get_setting = function(0x73bf8338, c.c_void_p, c.c_void_p, c.c_uint32, c.POINTER(Setting))
    enum_settings = function(0xae3039da, c.c_void_p, c.c_void_p, c.c_uint32, c.POINTER(c.c_uint32), c.POINTER(Setting))
    ids = {"frameRateLimit": 0x10835002, "backgroundFrameRateLimit": 0x10835016,
           "backgroundThresholdSeconds": 0x10835017, "verticalSync": 0x00A879CF,
           "powerManagement": 0x1057EB71, "preRenderLimit": 0x007BA09E,
           "gSyncOverride": 0x10A879CF, "gSyncRequestedState": 0x10A879AC,
           "batteryBoostFps": 0x10115C8C, "externalQuietMode": 0x10115C8D,
           "gpuMaximumPower": 0x10D1EF29, "preferredRefreshRate": 0x0064B541,
           "gSyncGlobalMode": 0x1194F158}
    session = c.c_void_p()
    require(create(c.byref(session)))
    try:
        require(load(session))
        profile = c.c_void_p()
        require(current_global(session, c.byref(profile)))

        def read(profile):
            settings = {}
            for name, identifier in ids.items():
                value = Setting(version=c.sizeof(Setting) | (1 << 16))
                status = get_setting(session, profile, identifier, c.byref(value))
                row = {"status": status}
                if status == 0:
                    row.update(name=value.name, type=value.type, location=value.location,
                               predefined=bool(value.predefined), value=value.current.dword,
                               hex=hex(value.current.dword))
                settings[name] = row
            return settings

        def relevant_explicit(profile):
            # Include only numeric presentation/power settings. Never enumerate
            # other application profiles or emit arbitrary string values.
            result, count, index = [], 0, 0
            while True:
                batch = (Setting * 64)()
                for value in batch:
                    value.version = c.sizeof(Setting) | (1 << 16)
                length = c.c_uint32(len(batch))
                status = enum_settings(session, profile, index, c.byref(length), batch)
                if status == -7:
                    break
                require(status)
                count += length.value
                for value in batch[:length.value]:
                    name = value.name.lower()
                    if value.type == 0 and (value.current.dword == 20 or any(x in name for x in ("frame", "fps", "sync", "power", "refresh", "latency", "quiet"))):
                        result.append({"id": hex(value.id), "name": value.name,
                                       "value": value.current.dword, "hex": hex(value.current.dword),
                                       "location": value.location, "predefined": bool(value.predefined)})
                if length.value < len(batch):
                    break
                index += length.value
            return {"enumeratedCount": count, "matchingNumericSettings": result}

        result = {"global": read(profile), "globalEnumerated": relevant_explicit(profile)}
        base = c.c_void_p()
        require(get_base(session, c.byref(base)))
        result["base"] = read(base)
        result["baseEnumerated"] = relevant_explicit(base)
        for executable in ("RuinRunner.exe", "Unity.exe", *executables):
            application = Application(version=c.sizeof(Application) | (4 << 16))
            application_profile = c.c_void_p()
            status = find_app(session, executable, c.byref(application_profile), c.byref(application))
            result[executable] = {"lookupStatus": status, "settings": read(application_profile) if status == 0 else None,
                                  "enumerated": relevant_explicit(application_profile) if status == 0 else None}
        return result
    finally:
        destroy(session)


def run(command):
    result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=20)
    try:
        output = result.stdout.decode("utf-8")
    except UnicodeDecodeError:
        output = result.stdout.decode("mbcs", errors="replace")
    return {"exitCode": result.returncode, "output": output.strip()}


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--executable", action="append", default=[], help="Full existing candidate path for exact NVIDIA profile lookup")
    args = parser.parse_args()
    output = Path(args.output).resolve()
    if output.exists():
        raise ValueError("Choose a new output file; earlier evidence is preserved.")
    report = {"utc": dt.datetime.now(dt.timezone.utc).isoformat(), "readOnly": True,
              "scope": "Current environment snapshot, not retrospective failed-run GPU telemetry",
              **displays(), "activePowerPlan": run(["powercfg", "/getactivescheme"]),
              "cpuMinimumPower": run(["powercfg", "/query", "SCHEME_CURRENT", "SUB_PROCESSOR", "PROCTHROTTLEMIN"]),
              "pciePower": run(["powercfg", "/query", "SCHEME_CURRENT", "SUB_PCIEXPRESS", "ASPM"])}
    try:
        executables = [str(Path(path).resolve(strict=True)) for path in args.executable]
        report["nvidiaPresentationSettings"] = nvidia_settings(executables)
    except (OSError, RuntimeError) as error:
        report["nvidiaPresentationSettings"] = {"unavailable": str(error)}
    smi = shutil.which("nvidia-smi.exe")
    if smi:
        report["gpuCurrent"] = run([smi, "--query-gpu=name,driver_version,display_active,pstate,power.draw,power.limit,clocks.gr,clocks.mem,utilization.gpu,temperature.gpu", "--format=csv"])
    powershell = str(Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe")
    report["unityProcesses"] = run([powershell, "-NoProfile", "-Command",
        "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe' OR Name='RuinRunner.exe'\" | ForEach-Object {[pscustomobject]@{id=$_.ProcessId;name=$_.Name;isAssetWorker=($_.CommandLine -match 'AssetImportWorker');isBatch=($_.CommandLine -match 'batchmode');isThisProject=($_.CommandLine -match 'LoglikeCap')}} | ConvertTo-Json -Compress"])
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
