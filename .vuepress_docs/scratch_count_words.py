import os

docs_dir = "/Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/docs"

md_files = []
for root, dirs, files in os.walk(docs_dir):
    for file in files:
        if file.endswith('.md'):
            filepath = os.path.join(root, file)
            relpath = os.path.relpath(filepath, docs_dir)
            md_files.append((filepath, relpath))

md_files.sort()

for filepath, relpath in md_files:
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    print(f"{relpath}: {len(lines)} lines, {sum(len(l) for l in lines)} chars")
