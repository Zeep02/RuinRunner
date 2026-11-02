"""Publish/default-version switch checks on an explicit dedicated isolated instance.

Stops/restarts that instance only. Never target the live game validation instance.
"""
import argparse
import base64
import datetime
import json
import os
from pathlib import Path
import subprocess
import urllib.request
import uuid

ROOT=Path(__file__).resolve().parents[2]


def main(instance):
    assert instance.startswith("goal-catalog-") and all(c.isalnum() or c=="-" for c in instance)
    marker=json.loads((ROOT/"Backend/.local/isolated"/instance/"instance.json").read_text(encoding="utf-8-sig"))
    assert marker["contentVersion"]=="campaign-v1", "Start the dedicated catalog instance with campaign-v1 first"
    base=f"http://127.0.0.1:{int(marker['apiPort'])}"
    checks=[]
    def check(name,condition=True):
        assert condition,name
        checks.append(dict(name=name,status="PASS"));print("PASS",name)
    def uid():return str(uuid.uuid4())
    def call(method,path,body=None,token=None):
        headers={"Content-Type":"application/json"}
        if token:headers["Authorization"]="Bearer "+token
        request=urllib.request.Request(base+path,None if body is None else json.dumps(body,ensure_ascii=False).encode("utf-8"),headers,method=method)
        with urllib.request.urlopen(request,timeout=20) as response:return json.load(response)
    token=call("POST","/v1/auth/guest",dict(installationId=uid(),guestSecret=base64.urlsafe_b64encode(os.urandom(32)).decode().rstrip("=")))["accessToken"]
    old_catalog=call("GET","/v1/catalogs/current",token=token)
    old=call("POST","/v1/profiles",dict(operationId=uid(),slot=1,name="이전 버전 고정 격리 검사"),token)
    def start(p,version):
        return call("POST","/v1/runs",dict(operationId=uid(),profileId=p["profileId"],expectedProfileRevision=p["revision"],requestedSeed=20261009,contentVersion=version,generatorVersion="modules-v1.0.0",buildId="catalog-pinning-isolated"),token)
    old_run=start(old,"campaign-v1")
    old_economy=call("GET","/v1/runs/"+old_run["runId"]+"/economy",token=token)
    check("existing expedition starts with published campaign-v1",old_run["contentVersion"]=="campaign-v1")
    pwsh="pwsh"
    common=[pwsh,"-NoProfile","-File",str(ROOT/"Tools/Backend/Start-IsolatedBackend.ps1"),"-Instance",instance,"-ApiPort",str(marker["apiPort"]),"-DatabasePort",str(marker["databasePort"])]
    # Daemons must not inherit a communicate() pipe: on Windows that pipe can stay
    # open after the launcher exits. File handles let run() await only the launcher.
    launch_log=ROOT/"Backend/.local/isolated"/instance/"logs/catalog-switch.log"
    with launch_log.open("w",encoding="utf-8") as log:
        subprocess.run(common+["-Stop"],check=True,stdout=log,stderr=log,timeout=60)
        subprocess.run(common+["-SkipBuild","-ContentVersion","campaign-v2"],check=True,stdout=log,stderr=log,timeout=60)
    current=call("GET","/v1/catalogs/current",token=token)
    check("new current catalog is campaign-v2",current["version"]=="campaign-v2" and current["accessoryTuningVersion"]==1)
    check("published v1 contents are unchanged",call("GET","/v1/catalogs/campaign-v1",token=token)==old_catalog)
    check("existing expedition retains exact old run snapshot after API and DB restart",call("GET","/v1/runs/"+old_run["runId"],token=token)==old_run)
    check("existing economy retains same items gold and shops",call("GET","/v1/runs/"+old_run["runId"]+"/economy",token=token)==old_economy)
    new=call("POST","/v1/profiles",dict(operationId=uid(),slot=2,name="새 버전 고정 격리 검사"),token)
    new_run=start(new,"campaign-v2")
    check("new expedition pins campaign-v2",new_run["contentVersion"]=="campaign-v2")
    check("new catalog contains every accessory and set tuning",len([x for x in current["items"] if x["kind"]=="accessory" and len(x["stats"])==13])==30 and len(current["itemSets"])==8)
    old_result=call("POST","/v1/runs/"+old_run["runId"]+"/settle",dict(operationId=uid(),expectedRunRevision=old_run["revision"],outcome="defeat",enemiesDefeated=12,roomsReached=2,stagesCleared=0),token)
    rules=old_catalog["settlement"]
    expected=max(rules["minimumReward"],12//rules["killsPerShard"]+2*rules["rewardPerRoom"])
    check("old expedition still settles under pinned original rules",old_result["run"]["legacyReward"]==expected and old_result["run"]["contentVersion"]=="campaign-v1")
    check("new slot is isolated from old settlement",call("GET","/v1/profiles/"+new["profileId"],token=token)==new)
    output=ROOT/"Backend/Validation"/("catalog-pinning-"+datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d-%H%M%S")+".json")
    output.write_text(json.dumps(dict(status="PASS",normalGameplay=False,scope="isolated HTTP/PostgreSQL catalog switch and restart",instance=instance,checks=checks),ensure_ascii=False,indent=2),encoding="utf-8")
    print(output)


if __name__=="__main__":
    parser=argparse.ArgumentParser();parser.add_argument("--instance",default="goal-catalog-20261009");main(parser.parse_args().instance)
