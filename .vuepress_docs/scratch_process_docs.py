import os
import re

docs_dir = "/Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/docs"

def extract_title_and_heading(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Extract frontmatter
    fm_match = re.match(r"^---\s*\n(.*?)\n---\s*\n", content, re.DOTALL)
    if not fm_match:
        return None, None, "No frontmatter"
    
    fm_text = fm_match.group(1)
    
    # Extract title from frontmatter
    title_match = re.search(r"^title:\s*(.*)$", fm_text, re.MULTILINE)
    current_title = title_match.group(1).strip() if title_match else None
    
    # Find first # heading in the rest of the file
    rest_content = content[fm_match.end():]
    heading_match = re.search(r"^#\s+(.*)$", rest_content, re.MULTILINE)
    first_heading = heading_match.group(1).strip() if heading_match else None
    
    return current_title, first_heading, None

md_files = []
for root, dirs, files in os.walk(docs_dir):
    for file in files:
        if file.endswith('.md'):
            filepath = os.path.join(root, file)
            relpath = os.path.relpath(filepath, docs_dir)
            md_files.append((filepath, relpath))

md_files.sort()

for filepath, relpath in md_files:
    curr_title, heading, err = extract_title_and_heading(filepath)
    print(f"File: {relpath}")
    print(f"  Current Title: {curr_title}")
    print(f"  First Heading: {heading}")
    print(f"  Error: {err}")
    print("-" * 40)
