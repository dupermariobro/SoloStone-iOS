"""Capture first launch on one available phone and tablet. Run on the macOS CI host."""
import json
from pathlib import Path
import subprocess
import time

root = Path(__file__).resolve().parent.parent
apps = list((root / "mobile/SoloStone.iOS/bin/Debug").glob("net10.0-ios/*/*.app"))
if len(apps) != 1:
    raise RuntimeError(f"Expected one simulator app, found {len(apps)}")
inventory = json.loads(subprocess.check_output(["xcrun", "simctl", "list", "devices", "available", "--json"]))
runtimes = [key for key in inventory["devices"] if "iOS-26-0" in key]
if not runtimes:
    raise RuntimeError("The selected Xcode needs an available iOS 26.0 simulator runtime")
devices = inventory["devices"][runtimes[0]]
out = root / "mobile/out"
out.mkdir(parents=True, exist_ok=True)
for kind in ("iPhone", "iPad"):
    candidates = [d for d in devices if d["name"].startswith(kind) and d.get("isAvailable")]
    if not candidates:
        raise RuntimeError(f"No available {kind} simulator")
    device = candidates[0]
    uid = device["udid"]
    print(f"Capturing {device['name']}", flush=True)
    if device["state"] != "Booted":
        subprocess.run(["xcrun", "simctl", "boot", uid], check=True)
    try:
        subprocess.run(["xcrun", "simctl", "bootstatus", uid, "-b"], check=True)
        subprocess.run(["xcrun", "simctl", "install", uid, str(apps[0])], check=True)
        subprocess.run(["xcrun", "simctl", "launch", uid, "org.solostone.mobile"], check=True)
        time.sleep(5)
        subprocess.run(["xcrun", "simctl", "io", uid, "screenshot", str(out / f"{kind}-launch.png")], check=True)
    finally:
        subprocess.run(["xcrun", "simctl", "shutdown", uid], check=False)
