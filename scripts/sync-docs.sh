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
# Find the repository root dynamically instead of hardcoding
REPO_DIR=$(git rev-parse --show-toplevel 2>/dev/null || pwd)
WORKTREE_DIR="$REPO_DIR/.git-docs-worktree"
DOCS_BRANCH="docs"


echo "=== Starting UniCon Documentation Sync ==="

# 1. Ensure we are in the repository root
cd "$REPO_DIR"

# 2. Check/Setup Git Worktree for docs branch
if [ ! -d "$WORKTREE_DIR" ]; then
    echo "Creating temporary git worktree at $WORKTREE_DIR..."
    
    # Try to fetch the docs branch from origin to make it available locally
    git fetch origin "$DOCS_BRANCH:$DOCS_BRANCH" 2>/dev/null || true
    
    # Check if docs branch exists locally now
    if git show-ref --verify --quiet "refs/heads/$DOCS_BRANCH"; then
        git worktree add "$WORKTREE_DIR" "$DOCS_BRANCH"
    else
        # Self-healing: If docs branch doesn't exist, initialize it as an orphan
        echo "Branch '$DOCS_BRANCH' not found. Initializing branch..."
        # Try using --orphan flag (modern git)
        if git worktree add --orphan "$WORKTREE_DIR" 2>/dev/null; then
            echo "Created orphan worktree."
        else
            # Fallback for older git versions: create branch off main and clean it
            git worktree add -b "$DOCS_BRANCH" "$WORKTREE_DIR"
            git -C "$WORKTREE_DIR" rm -rf .
        fi
    fi
else
    echo "Using existing git worktree at $WORKTREE_DIR..."
    git fetch origin "$DOCS_BRANCH:$DOCS_BRANCH" 2>/dev/null || true
    git -C "$WORKTREE_DIR" checkout -f "$DOCS_BRANCH"
    git -C "$WORKTREE_DIR" reset --hard HEAD
fi


# 3. Clear target directories in the worktree root
# We also clean up any legacy nested docs/ folder if it exists in the worktree
rm -rf "$WORKTREE_DIR/docs"

echo "Syncing documentation directories..."

for dir in docs/*/; do
    dir=${dir%/}
    base=$(basename "$dir")
    
    # Skip VuePress internal directory if present in main docs
    if [ "$base" = ".vuepress" ] || [ "$base" = "scripts" ]; then
        continue
    fi
    
    echo " -> Syncing module: $base"
    
    # Clean up target folder in docs branch to handle deleted/renamed files
    rm -rf "$WORKTREE_DIR/$base"
    mkdir -p "$WORKTREE_DIR/$base"
    
    # Copy fresh content
    cp -r "$dir/"* "$WORKTREE_DIR/$base/"
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
