import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parent / 'code'
files = subprocess.check_output(['git', 'ls-files'], cwd=REPO, text=True).splitlines()

def matching(prefix, suffix):
    return [p for p in files if p.startswith(prefix) and p.endswith(suffix)]

features = sorted({p.split('/')[3] for p in files if p.startswith('src/NOIR.Application/Features/') and len(p.split('/')) > 4})
entities = sorted({p.split('/')[3] for p in files if p.startswith('src/NOIR.Domain/Entities/') and len(p.split('/')) > 4})
frontend = sorted({p.split('/')[5] for p in files if p.startswith('src/NOIR.Web/frontend/src/portal-app/') and len(p.split('/')) > 6})
snapshot = (REPO / 'src/NOIR.Infrastructure/Migrations/App/ApplicationDbContextModelSnapshot.cs').read_text(encoding='utf8')
program = (REPO / 'src/NOIR.Web/Program.cs').read_text(encoding='utf8')
endpoint_files = matching('src/NOIR.Web/Endpoints/', '.cs')
custom_domains = ['Accounting', 'Commercial', 'Complaints', 'Engineering', 'Estimation', 'Fsm', 'Fulfillment', 'Helpdesk', 'Intake', 'Invoicing', 'Manufacturing', 'MasterData', 'Procurement', 'Qa', 'Stock', 'Stores']
domain_rows = []
for domain in custom_domains:
    domain_files = matching(f'src/NOIR.Domain/Entities/{domain}/', '.cs')
    app_files = matching(f'src/NOIR.Application/Features/{domain}/', '.cs')
    test_files = [p for p in files if p.startswith('tests/') and (f'/Entities/{domain}/' in p or f'/Features/{domain}/' in p)]
    classes = []
    for path in domain_files:
        classes.extend(re.findall(r'public\s+(?:sealed\s+|abstract\s+)?class\s+(\w+)', (REPO / path).read_text(encoding='utf8')))
    domain_rows.append({
        'domain': domain, 'entitySourceFiles': len(domain_files), 'entityClasses': classes,
        'applicationSourceFiles': len(app_files),
        'handlerSourceFiles': len([p for p in app_files if p.endswith('Handler.cs')]),
        'validatorSourceFiles': len([p for p in app_files if p.endswith('Validator.cs')]),
        'directDomainFeatureTestFiles': len(test_files),
        'classesPresentInSnapshot': [c for c in classes if f'NOIR.Domain.Entities.{domain}.{c}"' in snapshot],
    })
api_rows = []
for path in endpoint_files:
    text = (REPO / path).read_text(encoding='utf8')
    api_rows.append({'file': path, 'groups': re.findall(r'MapGroup\("([^"]+)"\)', text),
                     'httpRegistrations': len(re.findall(r'\.Map(?:Get|Post|Put|Patch|Delete)\(', text)),
                     'permissionReferences': sorted(set(re.findall(r'Permissions\.(\w+)', text))),
                     'featureGates': re.findall(r'RequireFeature\(([^\)]+)\)', text),
                     'directDbContext': 'IApplicationDbContext' in text})
result = {
    'commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=REPO, text=True).strip(),
    'trackedFiles': len(files),
    'trackedCsFiles': len([p for p in files if p.endswith('.cs')]),
    'frontendTsTsxFiles': len([p for p in files if p.startswith('src/NOIR.Web/frontend/') and p.endswith(('.ts', '.tsx'))]),
    'endpointSourceFiles': len(endpoint_files),
    'endpointMappingCallsInProgram': len(re.findall(r'^app\.Map\w+Endpoints\(', program, re.M)),
    'httpRegistrationsInEndpointSourceFiles': sum(r['httpRegistrations'] for r in api_rows),
    'applicationFeatures': features,
    'domainAreas': entities,
    'portalAreas': frontend,
    'appMigrationFiles': len([p for p in matching('src/NOIR.Infrastructure/Migrations/App/', '.cs') if not p.endswith(('.Designer.cs', 'ModelSnapshot.cs'))]),
    'testCsFiles': len(matching('tests/', '.cs')),
    'customDomains': domain_rows,
    'endpoints': api_rows,
    'method': 'Tracked source inventory only. File counts and registration patterns do not prove completeness, successful compilation, database readiness, runtime security or SRS acceptance.',
}
(ROOT / 'repo_inventory.json').write_text(json.dumps(result, indent=2), encoding='utf8')
print(json.dumps({k: v for k, v in result.items() if k not in ['endpoints', 'applicationFeatures', 'domainAreas', 'portalAreas']}, indent=2))
