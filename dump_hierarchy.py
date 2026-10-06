import yaml

with open('Assets/Testing/MainMenu.unity', 'r', encoding='utf-8') as f:
    lines = f.readlines()

objects = {}
current_id = None
for line in lines:
    if line.startswith('--- !u!'):
        parts = line.split('&')
        if len(parts) > 1:
            current_id = parts[1].strip()
            objects[current_id] = {'id': current_id, 'type': line.split(' ')[2], 'name': '', 'transform': None, 'children': [], 'components': []}
    elif current_id:
        if line.startswith('GameObject:'):
            objects[current_id]['type'] = 'GameObject'
        elif line.startswith('Transform:') or line.startswith('RectTransform:'):
            objects[current_id]['type'] = 'Transform'
        elif line.startswith('  m_Name:'):
            objects[current_id]['name'] = line.split(':', 1)[1].strip()
        elif line.startswith('  m_GameObject:'):
            ref = line.split('fileID:')[1].split('}')[0].strip()
            if ref != '0':
                objects[current_id]['gameObject'] = ref
        elif line.startswith('    - component:'):
            ref = line.split('fileID:')[1].split('}')[0].strip()
            if ref != '0':
                objects[current_id]['components'].append(ref)
        elif line.startswith('  m_Father:'):
            ref = line.split('fileID:')[1].split('}')[0].strip()
            if ref != '0':
                objects[current_id]['father'] = ref
        elif line.startswith('    - {fileID:'):
            if 'm_Children:' in ''.join(lines[lines.index(line)-5:lines.index(line)]):
                ref = line.split('fileID:')[1].split('}')[0].strip()
                if ref != '0':
                    objects[current_id]['children'].append(ref)

# associate transforms with gameobjects
for obj_id, obj in objects.items():
    if obj['type'] == 'Transform' or obj['type'] == 'RectTransform':
        if 'gameObject' in obj:
            go_id = obj['gameObject']
            if go_id in objects:
                objects[go_id]['transform'] = obj_id
                objects[obj_id]['is_transform'] = True

# build hierarchy
def print_hierarchy(go_id, indent=''):
    go = objects.get(go_id)
    if not go: return
    print(indent + go.get('name', 'Unnamed'))
    t_id = go.get('transform')
    if t_id and t_id in objects:
        for child_t_id in objects[t_id].get('children', []):
            if child_t_id in objects and 'gameObject' in objects[child_t_id]:
                child_go_id = objects[child_t_id]['gameObject']
                print_hierarchy(child_go_id, indent + '  ')

print('Hierarchy for customizeUI:')
for obj_id, obj in objects.items():
    if obj.get('name') == 'customizeUI' and obj.get('type') == 'GameObject':
        print_hierarchy(obj_id)
