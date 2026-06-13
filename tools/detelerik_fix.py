#!/usr/bin/env python3
# Fixup pass: kill residual telerik tokens (property-element close tags) + dedupe attrs.
import re, glob, os

def dedupe_attrs(m):
    tag = m.group(0)
    seen = set()
    def repl(a):
        name = a.group(1)
        if name in seen:
            return ''
        seen.add(name)
        return a.group(0)
    # iterate attributes ` name="..."`
    return re.sub(r'\s([A-Za-z_][\w\.]*)="[^"]*"', repl, tag)

for f in glob.glob('src/SamaHesab.WPF/**/*.xaml', recursive=True):
    if os.path.basename(f) in ('App.xaml',):
        continue
    s0 = open(f, encoding='utf-8').read()
    if 'telerik' not in s0.lower() and 'AutoGenerateColumns' not in s0:
        continue
    s = s0
    # residual telerik tokens (incl. property-element open/close tags)
    s = s.replace('telerik:RadComboBox', 'ComboBox')
    s = s.replace('telerik:RadMaskedNumericInput', 'TextBox')
    s = s.replace('telerik:GridViewDataColumn', 'DataGridTextColumn')
    s = s.replace('telerik:RadGridView', 'DataGrid')
    s = re.sub(r'[ \t]*xmlns:telerik="[^"]*"\r?\n', '', s)
    # dedupe attributes in DataGrid/ComboBox/TextBox opening tags
    s = re.sub(r'<(DataGrid|ComboBox|TextBox)\b[^>]*?/?>', dedupe_attrs, s, flags=re.S)
    if s != s0:
        open(f, 'w', encoding='utf-8').write(s)
        print('fixed:', f)
print('--- done ---')
