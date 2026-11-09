"""Refresh the three bundled UI files in an UNSIGNED IPA; preserve all native bytes and ZIP modes."""
import argparse
import hashlib
import json
from pathlib import Path
import plistlib
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("input", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
if args.input.resolve() == args.output.resolve():
    parser.error("Use a separate output file; the downloaded build is retained unchanged")
web = Path(__file__).resolve().parent / "Web"
with zipfile.ZipFile(args.input) as source:
    if any("/_CodeSignature/" in n or n.endswith("embedded.mobileprovision") for n in source.namelist()):
        parser.error("Input must be unsigned and unprovisioned")
    infos = [n for n in source.namelist() if n.startswith("Payload/") and n.endswith(".app/Info.plist") and n.count("/") == 2]
    if len(infos) != 1:
        parser.error("Expected exactly one iOS application bundle")
    bundle = infos[0].rsplit("/", 1)[0]
    manifest = plistlib.loads(source.read(infos[0]))
    native_path = bundle + "/" + manifest["CFBundleExecutable"]
    native_hash = hashlib.sha256(source.read(native_path)).hexdigest()
    replacements = {bundle + "/Web/" + name: (web / name).read_bytes() for name in ("index.html", "style.css", "app.js")}
    if not replacements.keys() <= set(source.namelist()):
        parser.error("Input does not contain the expected bundled UI files")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.output, "w") as target:
        for entry in source.infolist():
            target.writestr(entry, replacements.get(entry.filename, source.read(entry.filename)))
with zipfile.ZipFile(args.output) as verify, zipfile.ZipFile(args.input) as original:
    for entry in original.infolist():
        expected = replacements.get(entry.filename, original.read(entry.filename))
        if verify.read(entry.filename) != expected or verify.getinfo(entry.filename).external_attr != entry.external_attr:
            raise RuntimeError("Bundle verification failed: " + entry.filename)
    if hashlib.sha256(verify.read(native_path)).hexdigest() != native_hash:
        raise RuntimeError("Native executable changed")
result = {
    "ipa": args.output.name,
    "sha256": hashlib.sha256(args.output.read_bytes()).hexdigest(),
    "nativeExecutableSha256": native_hash,
    "deviceFamilies": manifest["UIDeviceFamily"],
    "signed": False,
    "updatedAssets": list(replacements),
    "baseBuild": "https://github.com/dupermariobro/SoloStone-iOS/actions/runs/42622414323",
}
args.output.with_suffix(".manifest.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
