#!/usr/bin/env bash
set -e

# ================= CONFIGURATION =================
PROJECT_PATH=$(pwd)
ITCH_USER="boredvoidater"
ITCH_GAME="evolve"
VERSION_FILE="version.txt"
UNITY_EDITOR_PATH="$HOME/Unity/Hub/Editor/6000.3.10f1/Editor/Unity"
# =================================================

UPDATE_TYPE="patch"
BUILD_WIN=false
BUILD_MAC=false
BUILD_LINUX=false

# Parse arguments
while [[ "$#" -gt 0 ]]; do
  case $1 in
  --major)
    UPDATE_TYPE="major"
    shift
    ;;
  --minor)
    UPDATE_TYPE="minor"
    shift
    ;;
  --patch)
    UPDATE_TYPE="patch"
    shift
    ;;
  --win)
    BUILD_WIN=true
    shift
    ;;
  --mac)
    BUILD_MAC=true
    shift
    ;;
  --linux)
    BUILD_LINUX=true
    shift
    ;;
  *)
    echo "Unknown parameter passed: $1"
    exit 1
    ;;
  esac
done

# 1. Handle Semantic Versioning
if [ ! -f "$VERSION_FILE" ]; then
  echo "1.0.0" >"$VERSION_FILE"
fi

IFS='.' read -r major minor patch <"$VERSION_FILE"

if [ "$UPDATE_TYPE" == "major" ]; then
  major=$((major + 1))
  minor=0
  patch=0
elif [ "$UPDATE_TYPE" == "minor" ]; then
  minor=$((minor + 1))
  patch=0
else
  patch=$((patch + 1))
fi

NEW_VERSION="$major.$minor.$patch"
echo "$NEW_VERSION" >"$VERSION_FILE"
echo "🚀 Preparing Release: v$NEW_VERSION"

# 2. Build via Unity Batchmode
echo "⚙️  Building projects in Unity... (This may take a while)"

# --- NIXOS FIX: Fake libxml2.so.2 ---
# Create a wrapper script to run inside steam-run's FHS environment
cat <<'EOF' >.nixos-unity-launcher.sh
#!/usr/bin/env bash
mkdir -p .unity-compat-libs

# Find the newer libxml2 (usually .so.16) and symlink it to the old name (.so.2)
REAL_LIBXML=$(find /lib /lib64 /usr/lib /usr/lib64 -name "libxml2.so*" 2>/dev/null | grep -v "libxml2.so.2$" | head -n 1)
if [ -n "$REAL_LIBXML" ]; then
    ln -sf "$REAL_LIBXML" "$PWD/.unity-compat-libs/libxml2.so.2"
fi

# Force Unity to look in our compat folder first
export LD_LIBRARY_PATH="$PWD/.unity-compat-libs:$LD_LIBRARY_PATH"

# Execute the actual Unity Editor with all arguments
exec "$@"
EOF
chmod +x .nixos-unity-launcher.sh
# ------------------------------------

# Wrap the Editor with steam-run AND our compat launcher array
UNITY_EXE=(steam-run "$PROJECT_PATH/.nixos-unity-launcher.sh" "$UNITY_EDITOR_PATH")

UNITY_ARGS=(
  "${UNITY_EXE[@]}" -quit -batchmode -nographics
  -projectPath "$PROJECT_PATH"
  -executeMethod "Builder.Build"
  -buildWebGL # WebGL is always built
)

if [ "$BUILD_WIN" = true ]; then UNITY_ARGS+=("-buildWin"); fi
if [ "$BUILD_MAC" = true ]; then UNITY_ARGS+=("-buildMac"); fi
if [ "$BUILD_LINUX" = true ]; then UNITY_ARGS+=("-buildLinux"); fi

# Execute Unity Build
"${UNITY_ARGS[@]}"

# 3. Push to Itch.io via Butler
echo "☁️  Pushing builds to Itch.io..."

push_to_itch() {
  local folder=$1
  local channel=$2
  if [ -d "Builds/$folder" ]; then
    nix run nixpkgs#butler -- push "Builds/$folder" "$ITCH_USER/$ITCH_GAME:$channel" --userversion "$NEW_VERSION"
    echo "✅ Successfully pushed $channel channel!"
  else
    echo "⚠️  Warning: Builds/$folder not found, skipping $channel..."
  fi
}

# WebGL is guaranteed
push_to_itch "WebGL" "webgl"

# Optional Platforms
if [ "$BUILD_WIN" = true ]; then push_to_itch "Windows" "win"; fi
if [ "$BUILD_MAC" = true ]; then push_to_itch "MacOS" "mac"; fi
if [ "$BUILD_LINUX" = true ]; then push_to_itch "Linux" "linux"; fi

echo "🎉 All done! Game updated to v$NEW_VERSION on itch.io."
