"""Uploads release Addressables content (unity/ServerData/<platform>/) to Cloudflare R2.

Usage (from the repo root, after `npx wrangler login` with your own Cloudflare account):
    python tools/content/upload_r2.py Android [--bucket wick-content]

The game loads remote content from https://cdn.uprisestudios.co.uk/wick/<platform>/ (see
unity/Assets/_Wick/Editor/AddressablesSetup.cs), so the bucket needs that custom domain and objects
are stored under wick/<platform>/. Bundles have content hashes in their names, so they're cached
forever; the catalog files are not, so a content update shows up on the next launch.
"""
import argparse
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("platform", help="Android or iOS")
    parser.add_argument("--bucket", default="wick-content")
    args = parser.parse_args()

    folder = os.path.join(ROOT, "unity", "ServerData", args.platform)
    if not os.path.isdir(folder):
        sys.exit("Nothing to upload: build it first (Wick > Art > Build Release Content).")

    files = sorted(f for f in os.listdir(folder) if os.path.isfile(os.path.join(folder, f)))
    for name in files:
        catalog = name.startswith("catalog")
        cache = "no-cache" if catalog else "public, max-age=31536000, immutable"
        key = "{}/wick/{}/{}".format(args.bucket, args.platform, name)
        print("Uploading", key)
        subprocess.run(["npx", "wrangler", "r2", "object", "put", key, "--file", os.path.join(folder, name),
                        "--cache-control", cache, "--remote"], check=True, shell=(os.name == "nt"))

    print("Uploaded {} files. Check: https://cdn.uprisestudios.co.uk/wick/{}/".format(len(files), args.platform))


if __name__ == "__main__":
    main()
