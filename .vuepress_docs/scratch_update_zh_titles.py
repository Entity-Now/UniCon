import os
import re

docs_dir = "/Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/docs"

def clean_slug(filename):
    # Remove number prefix and .md
    name = re.sub(r'^\d+\.\s*', '', filename)
    name = name.replace('.md', '').lower().strip()
    name = re.sub(r'[^a-z0-9\-]', '-', name)
    name = re.sub(r'-+', '-', name).strip('-')
    return name

def process_file(filepath, filename):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Extract first heading
    heading_match = re.search(r"^#\s+(.*)$", content, re.MULTILINE)
    first_heading = heading_match.group(1).strip() if heading_match else None
    
    if not first_heading:
        print(f"Skipping {filename}: no heading found")
        return
    
    # Check if there is existing frontmatter
    fm_match = re.match(r"^---\s*\n(.*?)\n---\s*\n", content, re.DOTALL)
    
    if fm_match:
        fm_text = fm_match.group(1)
        # Update or add title in frontmatter
        if re.search(r"^title:\s*(.*)$", fm_text, re.MULTILINE):
            new_fm_text = re.sub(r"^title:\s*.*$", f"title: {first_heading}", fm_text, flags=re.MULTILINE)
        else:
            new_fm_text = fm_text + f"\ntitle: {first_heading}"
        
        # Reconstruct content
        new_content = "---\n" + new_fm_text + "\n---\n" + content[fm_match.end():]
    else:
        # Create new frontmatter
        slug = clean_slug(filename)
        new_fm = f"---\ntitle: {first_heading}\ncreateTime: 2026/05/23 20:25:00\npermalink: /docs/{slug}/\n---\n"
        new_content = new_fm + content
    
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(new_content)
    print(f"Updated {filename} with title: '{first_heading}'")

md_files = []
for root, dirs, files in os.walk(docs_dir):
    for file in files:
        if file.endswith('.md'):
            filepath = os.path.join(root, file)
            md_files.append((filepath, file))

for filepath, filename in md_files:
    process_file(filepath, filename)
