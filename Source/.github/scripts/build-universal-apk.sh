#!/usr/bin/env bash
set -euo pipefail

bundle="${1:-Builds/Android/SeaVillage.aab}"
output="${2:-Builds/Android/SeaVillage.apk}"
for name in ANDROID_KEYSTORE_BASE64 ANDROID_KEYSTORE_PASS ANDROID_KEYALIAS_NAME ANDROID_KEYALIAS_PASS; do
  if [[ -z "${!name:-}" ]]; then
    echo "Missing signing secret: $name" >&2
    exit 1
  fi
done
[[ -s "$bundle" ]] || { echo "AAB not found: $bundle" >&2; exit 1; }
[[ ! -e "$output" ]] || { echo "APK already exists: $output" >&2; exit 1; }

output_dir="$(dirname "$output")"
mkdir -p "$output_dir"
[[ -w "$output_dir" ]] || { echo "APK output directory is not writable: $output_dir" >&2; exit 1; }

umask 077
work_dir="$(mktemp -d)"
trap 'rm -rf -- "$work_dir"' EXIT
bundletool_version="1.18.3"
curl --fail --location --retry 3 --silent --show-error \
  "https://github.com/google/bundletool/releases/download/${bundletool_version}/bundletool-all-${bundletool_version}.jar" \
  --output "$work_dir/bundletool.jar"

# 비밀번호는 명령행 대신 임시 파일로 전달
printf '%s' "$ANDROID_KEYSTORE_BASE64" | base64 --decode > "$work_dir/upload.keystore"
printf '%s' "$ANDROID_KEYSTORE_PASS" > "$work_dir/keystore.pass"
printf '%s' "$ANDROID_KEYALIAS_PASS" > "$work_dir/alias.pass"
java -jar "$work_dir/bundletool.jar" validate --bundle="$bundle"
java -jar "$work_dir/bundletool.jar" build-apks \
  --bundle="$bundle" \
  --output="$work_dir/release.apks" \
  --mode=universal \
  --ks="$work_dir/upload.keystore" \
  --ks-key-alias="$ANDROID_KEYALIAS_NAME" \
  --ks-pass="file:$work_dir/keystore.pass" \
  --key-pass="file:$work_dir/alias.pass"
unzip -p "$work_dir/release.apks" universal.apk > "$work_dir/SeaVillage.apk"
[[ -s "$work_dir/SeaVillage.apk" ]] || { echo "Universal APK is empty" >&2; exit 1; }
unzip -tq "$work_dir/SeaVillage.apk"
mv -- "$work_dir/SeaVillage.apk" "$output"
echo "Signed release APK created: $output"
