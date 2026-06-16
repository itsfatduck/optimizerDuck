import glob

for file in glob.glob('README*.md'):
    with open(file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Replace "Community |" or "Community" with "[MarkesrBetter](https://github.com/MarkesrBetter) |"
    new_content = content.replace('| Community |', '| [MarkesrBetter](https://github.com/MarkesrBetter) |')
    
    with open(file, 'w', encoding='utf-8') as f:
        f.write(new_content)
