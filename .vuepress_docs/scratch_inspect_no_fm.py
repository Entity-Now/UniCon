import os

docs_dir = "/Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/docs"

files_no_fm = [
    "2. core/10.IDriverRegistry.md",
    "2. core/2.driver-registry.md",
    "2. core/8.IUniconDriver.md",
    "2. core/8.MemoryCacheProvider.md",
    "2. core/9.RedisCacheProviderPlaceholder.md"
]

for relpath in files_no_fm:
    filepath = os.path.join(docs_dir, relpath)
    if os.path.exists(filepath):
        print(f"File: {relpath}")
        with open(filepath, 'r', encoding='utf-8') as f:
            lines = [f.readline().strip() for _ in range(5)]
        print(f"  First 5 lines: {lines}")
        print("-" * 40)
