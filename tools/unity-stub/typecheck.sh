#!/usr/bin/env bash
# Type-checks the whole RILL codebase without Unity, in about two seconds.
#
# Unity's own compiler is only reachable through the editor, which locks the project and takes
# minutes to boot. These stubs declare the shape of every Unity API the game touches — no
# behaviour, just signatures — so a plain C# compiler can catch the entire class of errors that
# matter most while iterating: typos, wrong overloads, bad conversions, dead fields.
#
# It does NOT check shaders, and it does NOT prove anything runs. See docs/VERIFICATION.md.
#
# Requires: mono (brew install mono), which provides csc.
set -euo pipefail

cd "$(dirname "$0")/../.."
OUT="${TMPDIR:-/tmp}/rill-typecheck"
mkdir -p "$OUT"

echo "building stubs..."
csc -nologo -target:library -out:"$OUT/UnityStub.dll" tools/unity-stub/UnityStub.cs
csc -nologo -target:library -out:"$OUT/UnityEditorStub.dll" -r:"$OUT/UnityStub.dll" \
    tools/unity-stub/UnityEditorStub.cs

# The touch and mouse input paths are behind #if, so both are compiled or half the input
# code is never checked at all.
echo "runtime (device / touch path)..."
csc -nologo -warn:4 -target:library -out:"$OUT/rill.dll" -r:"$OUT/UnityStub.dll" \
    $(find Assets/Scripts -name '*.cs')

echo "runtime (editor / mouse path)..."
csc -nologo -warn:4 -target:library -out:"$OUT/rill_ed.dll" -define:UNITY_EDITOR \
    -r:"$OUT/UnityStub.dll" $(find Assets/Scripts -name '*.cs')

echo "editor tools..."
csc -nologo -warn:4 -target:library -out:"$OUT/rill_tools.dll" -define:UNITY_EDITOR \
    -r:"$OUT/UnityStub.dll" -r:"$OUT/UnityEditorStub.dll" -r:"$OUT/rill_ed.dll" \
    Assets/Editor/*.cs

echo "clean."
