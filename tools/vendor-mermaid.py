#!/usr/bin/env python3
"""Copy Mermaid's pinned ESM dependency tree, with its license, into static assets."""
from pathlib import Path
import re
import shutil
import sys

source = Path(sys.argv[1]).resolve()
target = Path(sys.argv[2]).resolve()
queue = [source / 'dist/mermaid.esm.min.mjs']
seen = set()
while queue:
    file = queue.pop()
    if file in seen:
        continue
    if not file.is_relative_to(source / 'dist'):
        raise ValueError('Unexpected external module path')
    seen.add(file)
    for dependency in re.findall(r'''["'](\.[^"']+\.mjs)["']''', file.read_text()):
        candidate = (file.parent / dependency).resolve()
        if candidate.is_file():
            queue.append(candidate)
target.mkdir(parents=True, exist_ok=True)
for file in seen:
    destination = target / file.relative_to(source / 'dist')
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(file, destination)
shutil.copy2(source / 'LICENSE', target / 'LICENSE')
print(f'Vendored {len(seen)} Mermaid modules and license.')
