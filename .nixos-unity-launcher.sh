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
