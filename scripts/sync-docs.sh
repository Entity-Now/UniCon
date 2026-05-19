#!/bin/bash
# ==============================================================================
# UniCon Documentation Auto-Sync Script
# ==============================================================================
# This script uses Git Worktree to sync the `/docs` folder from the current
# branch (usually `main`) to the independent `docs` branch without switching
# the developer's working directory.
#
# Usage:
#   bash scripts/sync-docs.sh [--push]
# ==============================================================================

set -e

# Unset Git environment variables to prevent conflicts when run from Git Hooks
unset GIT_DIR GIT_INDEX_FILE GIT_WORK_TREE GIT_QUARANTINE_PATH


# Configuration
REPO_DIR="/Users/entity/Desktop/Language/CSharp/UniGateway/UniCon"
WORKTREE_DIR="$REPO_DIR/.git-docs-worktree"
DOCS_BRANCH="docs"

echo "=== Starting UniCon Documentation Sync ==="

# 1. Ensure we are in the repository root
cd "$REPO_DIR"

# 2. Check/Setup Git Worktree for docs branch
if [ ! -d "$WORKTREE_DIR" ]; then
    echo "Creating temporary git worktree at $WORKTREE_DIR..."
    git worktree add -B "$DOCS_BRANCH" "$WORKTREE_DIR" "$DOCS_BRANCH"
else
    echo "Using existing git worktree at $WORKTREE_DIR..."
    # Ensure it's clean and checkout the correct branch
    git -C "$WORKTREE_DIR" checkout -f "$DOCS_BRANCH"
    git -C "$WORKTREE_DIR" reset --hard HEAD
fi

# 3. Clear target directories in the worktree docs content folder
# We only clear folders that exist in our main docs directory to avoid erasing
# other configurations like .vuepress or README.md in the docs branch.
echo "Syncing documentation directories..."
mkdir -p "$WORKTREE_DIR/docs"

for dir in docs/*/; do
    dir=${dir%/}
    base=$(basename "$dir")
    
    # Skip VuePress internal directory if present in main docs
    if [ "$base" = ".vuepress" ] || [ "$base" = "scripts" ]; then
        continue
    fi
    
    echo " -> Syncing module: $base"
    
    # Clean up target folder in docs branch to handle deleted/renamed files
    rm -rf "$WORKTREE_DIR/docs/$base"
    mkdir -p "$WORKTREE_DIR/docs/$base"
    
    # Copy fresh content
    cp -r "$dir/"* "$WORKTREE_DIR/docs/$base/"
done

# 4. Check for changes in worktree
git -C "$WORKTREE_DIR" add .

if [ -z "$(git -C "$WORKTREE_DIR" status --porcelain)" ]; then
    echo "No documentation changes detected. Sync complete."
else
    # Get current commit info from main branch
    MAIN_COMMIT=$(git rev-parse --short HEAD)
    MAIN_MSG=$(git log -1 --pretty=%B | head -n 1)
    
    echo "Commiting changes in docs branch..."
    git -C "$WORKTREE_DIR" commit -m "docs: auto-sync from main commit $MAIN_COMMIT ($MAIN_MSG)"
    
    # 5. Push if --push is provided
    if [ "$1" = "--push" ]; then
        echo "Pushing changes to remote 'origin $DOCS_BRANCH'..."
        # We try pushing, but let it proceed if the remote is not available yet
        git -C "$WORKTREE_DIR" push origin "$DOCS_BRANCH" || echo "Warning: Push to origin failed. Remote may not be configured."
    fi
    echo "Successfully synchronized docs branch!"
fi

echo "=== Documentation Sync Complete ==="
