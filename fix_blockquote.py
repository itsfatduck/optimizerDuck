import glob

for file in glob.glob('README*.md'):
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # We only want to replace lines that start exactly with `| 🇹🇷 |` (no `> ` prefix)
    # The split handles this nicely.
    lines = content.split('\n')
    for i, line in enumerate(lines):
        if line.startswith('| 🇹🇷 |'):
            lines[i] = '> ' + line
            
    with open(file, 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines))
