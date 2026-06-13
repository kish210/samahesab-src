#!/usr/bin/env python3
# Remove Telerik from WPF XAML: RadGridView->DataGrid, GridViewDataColumn->DataGrid*Column,
# RadComboBox->ComboBox, RadMaskedNumericInput->TextBox. Drop SumFunction/GroupDescriptor.
import re, sys, glob, os

TELERIK_XMLNS = re.compile(r'[ \t]*xmlns:telerik="http://schemas\.telerik\.com/2008/xaml/presentation"\r?\n')

def strip_attrs(tag_open, names):
    for n in names:
        tag_open = re.sub(r'\s' + n + r'="[^"]*"', '', tag_open)
    return tag_open

def convert_column(m):
    """Convert one <telerik:GridViewDataColumn ...>...</...> or self-closing."""
    block = m.group(0)
    # attributes on the opening tag
    open_m = re.match(r'<telerik:GridViewDataColumn\b([^>]*?)(/?)>', block, re.S)
    attrs = open_m.group(1)
    self_close = open_m.group(2) == '/'
    header = (re.search(r'Header="([^"]*)"', attrs) or [None, None])[1]
    width  = (re.search(r'Width="([^"]*)"', attrs) or [None, None])[1]
    bind   = (re.search(r'DataMemberBinding="(\{[^"]*\})"', attrs) or [None, None])[1]
    fmt    = (re.search(r'DataFormatString="(\{\}[^"]*)"', attrs) or [None, None])[1]
    hdr = f' Header="{header}"' if header else ''
    wid = f' Width="{width}"' if width else ''
    has_celltemplate = 'GridViewDataColumn.CellTemplate' in block
    if has_celltemplate:
        inner = block
        inner = re.sub(r'</?telerik:GridViewDataColumn\.CellTemplate>',
                       lambda x: x.group(0).replace('telerik:GridViewDataColumn', 'DataGridTemplateColumn'), inner)
        # drop the opening/closing column tags, keep the CellTemplate block
        inner = re.sub(r'^<telerik:GridViewDataColumn\b[^>]*>', '', inner, flags=re.S)
        inner = re.sub(r'</telerik:GridViewDataColumn>\s*$', '', inner, flags=re.S)
        return f'<DataGridTemplateColumn{hdr}{wid}>{inner}</DataGridTemplateColumn>'
    # simple text column
    if bind and fmt:
        inner_bind = bind[:-1] + f', StringFormat={fmt}' + '}'
        bcol = f' Binding="{inner_bind}"'
    elif bind:
        bcol = f' Binding="{bind}"'
    else:
        bcol = ''
    return f'<DataGridTextColumn{hdr}{bcol}{wid}/>'

def transform(s):
    s = TELERIK_XMLNS.sub('', s)
    # remove group descriptors + aggregate functions blocks
    s = re.sub(r'<telerik:RadGridView\.GroupDescriptors>.*?</telerik:RadGridView\.GroupDescriptors>', '', s, flags=re.S)
    s = re.sub(r'<telerik:GridViewDataColumn\.AggregateFunctions>.*?</telerik:GridViewDataColumn\.AggregateFunctions>', '', s, flags=re.S)
    # convert columns: container form (open tag NOT self-closing → ends with non-'/') first, then self-closing
    s = re.sub(r'<telerik:GridViewDataColumn\b[^>]*?[^/]>.*?</telerik:GridViewDataColumn>', convert_column, s, flags=re.S)
    s = re.sub(r'<telerik:GridViewDataColumn\b[^>]*?/>', convert_column, s, flags=re.S)
    # columns property element
    s = s.replace('telerik:RadGridView.Columns', 'DataGrid.Columns')
    # RadGridView element -> DataGrid (+ strip telerik-only attrs)
    def grid_open(m):
        o = strip_attrs(m.group(0), ['RowIndicatorVisibility','ShowGroupPanel','GroupRenderMode',
            'ShowColumnFooters','ShowColumnHeaders','CanUserFreezeColumns','EnableColumnVirtualization',
            'EnableRowVirtualization','ShowGroupFooters','AutoExpandGroups','IsFilteringAllowed',
            'CanUserSortColumns','RowDetailsVisibilityMode','ValidatesOnDataErrors',
            'AutoGenerateColumns','IsReadOnly','CanUserAddRows'])
        o = o.replace('<telerik:RadGridView', '<DataGrid AutoGenerateColumns="False" CanUserAddRows="False" IsReadOnly="True"', 1)
        return o
    s = re.sub(r'<telerik:RadGridView\b[^>]*>', grid_open, s)
    s = s.replace('</telerik:RadGridView>', '</DataGrid>')
    # RadComboBox -> ComboBox (+ strip telerik-only attrs)
    def combo_open(m):
        o = strip_attrs(m.group(0), ['IsFilteringEnabled','TextSearchMode','CanAutocompleteSelectItems',
            'OpenDropDownOnFocus','EmptyText','ClearSelectionButtonVisibility','ClearSelectionButtonContent',
            'CanKeyboardNavigationSelectItems','AutocompleteMode','OpenDropDownOnTextChanged'])
        o = o.replace('<telerik:RadComboBox', '<ComboBox', 1)
        return o
    s = re.sub(r'<telerik:RadComboBox\b[^>]*?>', combo_open, s)
    s = s.replace('</telerik:RadComboBox>', '</ComboBox>')
    s = s.replace('<telerik:RadComboBoxItem', '<ComboBoxItem').replace('</telerik:RadComboBoxItem>', '</ComboBoxItem>')
    # RadMaskedNumericInput -> TextBox (Value-> Text, strip mask attrs)
    def masked_open(m):
        o = strip_attrs(m.group(0), ['Mask','FormatString','EmptyContent','SelectionOnFocus',
            'SpinMode','IsClearButtonVisible','Culture','UpdateValueEvent'])
        o = o.replace('Value="', 'Text="')
        o = o.replace('<telerik:RadMaskedNumericInput', '<TextBox', 1)
        return o
    s = re.sub(r'<telerik:RadMaskedNumericInput\b[^>]*?/?>', masked_open, s)
    s = s.replace('</telerik:RadMaskedNumericInput>', '</TextBox>')
    return s

files = glob.glob('src/SamaHesab.WPF/**/*.xaml', recursive=True)
changed = 0
for f in files:
    base = os.path.basename(f)
    if base in ('App.xaml', 'Styles.xaml'):   # handled manually (theme merges + grid styles)
        continue
    txt = open(f, encoding='utf-8').read()
    if 'telerik' not in txt.lower():
        continue
    new = transform(txt)
    if new != txt:
        open(f, 'w', encoding='utf-8').write(new)
        changed += 1
        print('converted:', f)
print(f'--- {changed} files converted ---')
