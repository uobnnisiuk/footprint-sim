#!/usr/bin/env python3
"""Fixed F19 research validation for ephemeral CI or an already isolated local runner.

This script is not an OS sandbox. Local callers must provide credential-free
filesystem isolation. No Issue text, arbitrary shell, model or network API is
accepted. Restore uses the repository's existing .NET package configuration.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

REPO = Path(__file__).resolve().parents[2]
BASELINE = '77611bc52976178857ac442be6e07a9caa1a2a9d'
PREREG = '9039417314df4c11fe5bc088825bf5f25eaf4d98'
PROTOCOL = 'experiments/m1f19_nested_point_support_density/experiment.json'
EXPERIMENT = 'experiments/m1f16_common_world_field_combination/experiment.json'
F19_FILES = ['candidate-density.json', 'candidate-density-runs.jsonl',
             'candidate-density-profiles.csv', 'candidate-density-conditionals.csv',
             'candidate-density-crosswalk.csv', 'candidate-density-manifest.json']
REFERENCE_COMMANDS = {
    'f16': ['evaluate-common-world-field-combination', '--experiment', EXPERIMENT],
    'f17': ['audit-common-world-plateau-domain', '--experiment',
            'experiments/m1f17_plateau_domain_audit/experiment.json', '--baseline-experiment', EXPERIMENT],
    'f18': ['evaluate-candidate-interval-width', '--experiment',
            'experiments/m1f18_candidate_interval_width/experiment.json', '--baseline-experiment', EXPERIMENT],
}
REFERENCE_FILES = {
    'f16': {
        'common-world-field-combination.json',
        'common-world-field-combination-profiles.csv',
        'common-world-field-combination-manifest.json',
        'common-world-field-combination-profile-equivalence.csv',
        'common-world-field-combination-set-inclusion.csv',
        'common-world-field-combination-plateau-audit.csv',
        'common-world-field-combination-conditional-reductions.csv',
        'common-world-field-combination-runs.jsonl',
    },
    'f17': {
        'plateau-domain-audit-runs.jsonl', 'plateau-domain-audit.json',
        'plateau-domain-audit.csv', 'plateau-domain-audit-manifest.json',
    },
    'f18': {
        'candidate-interval-width.json', 'candidate-interval-width-profiles.csv',
        'candidate-interval-width-manifest.json', 'candidate-interval-width-runs.jsonl',
        'candidate-interval-width-crosswalk.csv', 'candidate-interval-width-conditionals.csv',
    },
}
REFERENCE_ROW_HASHES = {
    ('f16', 'common-world-field-combination-runs.jsonl'): '6c114058f7e9479ae29e3d16030f10843ddfb71b8b8bc861d13f8840c0cd4c81',
    ('f18', 'candidate-interval-width-runs.jsonl'): 'c5b62f9bbad305e1ee3176b86105d1a631d4e3ebeb6868703d41d5c76c0d4d81',
}


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def clean_environment():
    allowed = {'PATH', 'HOME', 'LANG', 'LC_ALL', 'DOTNET_ROOT', 'DOTNET_CLI_HOME', 'NUGET_PACKAGES',
               'NUGET_HTTP_CACHE_PATH', 'TMPDIR'}
    env = {key: value for key, value in os.environ.items() if key in allowed}
    env.update({'DOTNET_CLI_TELEMETRY_OPTOUT': '1', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE': '1',
                'DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE': '1', 'DOTNET_NOLOGO': '1',
                'PYTHONDONTWRITEBYTECODE': '1', 'GIT_TERMINAL_PROMPT': '0',
                'GIT_CONFIG_NOSYSTEM': '1', 'GIT_CONFIG_GLOBAL': os.devnull, 'GIT_NO_REPLACE_OBJECTS': '1'})
    return env


def git(repo, *args):
    return subprocess.check_output(['git', '--no-optional-locks', '-c', 'core.hooksPath=/dev/null',
                                    '-c', 'core.fsmonitor=false', *args], cwd=repo,
                                   env=clean_environment(), text=True).strip()


def normalized_reference(data, expected_source_sha):
    """Ignore exactly the two execution metadata values; preserve all other bytes."""
    value = json.loads(data)
    if not isinstance(value, dict):
        return data, False
    manifest = value.get('manifest', value)
    if not isinstance(manifest, dict) or 'sourceCommitSha' not in manifest:
        return data, False
    if (not isinstance(manifest['sourceCommitSha'], str)
            or not re.fullmatch(r'[0-9a-f]{40}', manifest['sourceCommitSha'])):
        raise ValueError('Reference source must be a full commit SHA')
    if type(manifest.get('dirtyWorkingTree')) is not bool:
        raise ValueError('Reference dirty state must be explicit')
    if manifest['sourceCommitSha'] != expected_source_sha or manifest['dirtyWorkingTree'] is not False:
        raise ValueError('Reference manifest must identify the expected clean source')
    source = rb'("sourceCommitSha"\s*:\s*)"[0-9a-f]{40}"'
    dirty = rb'("dirtyWorkingTree"\s*:\s*)(?:true|false)'
    if len(re.findall(source, data)) != 1 or len(re.findall(dirty, data)) != 1:
        raise ValueError('Execution metadata must appear exactly once')
    data = re.sub(source, lambda match: match[1] + b'"EXECUTION_SOURCE"', data)
    data = re.sub(dirty, lambda match: match[1] + b'false', data)
    return data, True


def require_artifact_files(directory, names):
    if directory.is_symlink() or not directory.is_dir():
        raise ValueError('Unexpected artifact directory')
    artifacts = list(directory.iterdir())
    if ({path.name for path in artifacts} != set(names)
            or any(path.is_symlink() or not path.is_file() for path in artifacts)):
        raise ValueError('Unexpected artifact set: ' + directory.name)


def compare_references(before, after, before_source_sha, after_source_sha):
    comparisons = []
    for root in (before, after):
        campaigns = list(root.iterdir())
        if ({path.name for path in campaigns} != set(REFERENCE_FILES)
                or any(path.is_symlink() or not path.is_dir() for path in campaigns)):
            raise ValueError('Unexpected reference campaign set')
    for campaign, names in REFERENCE_FILES.items():
        for root in (before, after):
            require_artifact_files(root / campaign, names)
        for name in sorted(names):
            a = (before / campaign / name).read_bytes()
            b = (after / campaign / name).read_bytes()
            expected = REFERENCE_ROW_HASHES.get((campaign, name))
            if expected and (sha256(a) != expected or sha256(b) != expected):
                raise ValueError(f'{campaign}/{name}: immutable reference changed')
            normalized_a, normalized_b, ignored = a, b, False
            if name.endswith('.json'):
                normalized_a, ignored = normalized_reference(a, before_source_sha)
                normalized_b, ignored_b = normalized_reference(b, after_source_sha)
                if not ignored or not ignored_b:
                    raise ValueError('Reference manifest is missing')
            if normalized_a != normalized_b:
                raise ValueError(f'{campaign}/{name}: scientific output drift')
            comparisons.append({'campaign': campaign, 'file': name, 'baseline_sha256': sha256(a),
                                'current_sha256': sha256(b), 'scientific_sha256': sha256(normalized_a),
                                'ignored_execution_metadata_only': ignored, 'equal': True})
    return comparisons


def compare_f19_repeats(before, after):
    for root in (before, after):
        require_artifact_files(root, F19_FILES)
    repeated = []
    for name in F19_FILES:
        a, b = (before / name).read_bytes(), (after / name).read_bytes()
        if a != b:
            raise ValueError('F19 repeat output is not byte-identical: ' + name)
        repeated.append({'file': name, 'sha256': sha256(a), 'bytes': len(a), 'byte_identical': True})
    return repeated


def collect_artifact_hashes(directory):
    """Never follow links when collecting the fixed runner's output evidence."""
    if directory.is_symlink() or not directory.is_dir():
        raise ValueError('Unexpected evidence directory')
    hashes = {}
    for root, directories, files in os.walk(directory, followlinks=False):
        for name in directories + files:
            path = Path(root) / name
            if path.is_symlink() or not (path.is_file() or path.is_dir()):
                raise ValueError('Unexpected nonregular evidence entry')
        for name in files:
            path = Path(root) / name
            hashes[str(path.relative_to(directory))] = sha256(path.read_bytes())
    return dict(sorted(hashes.items()))


def validate_primary_counts(worlds, evaluations):
    """Reject partial runs or counts that include reference/control evaluations."""
    if type(worlds) is not int or type(evaluations) is not int or worlds != 200 or evaluations != 4800:
        raise ValueError('F19 did not execute all registered worlds and profiles')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline-source', type=Path, required=True)
    parser.add_argument('--expected-source-sha', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if not re.fullmatch(r'[0-9a-f]{40}', args.expected_source_sha):
        raise ValueError('Expected a full source SHA')
    baseline = args.baseline_source.resolve(strict=True)
    output = args.output.resolve()
    if output.is_relative_to(REPO) or output.is_relative_to(baseline):
        raise ValueError('Artifacts must be outside both source checkouts')
    output.mkdir(parents=True, exist_ok=True)
    if any(output.iterdir()):
        raise ValueError('Artifact directory must be empty')
    logs = output / 'logs'
    logs.mkdir()
    evidence = {'schema_version': 1, 'source_sha': args.expected_source_sha,
                'baseline_source_sha': BASELINE, 'preregistration_sha': PREREG,
                'seeds': {'start': 0, 'end': 199}, 'primary_evaluations': 4800,
                'complete': False, 'completed_commands': []}

    def run(name, argv, cwd=REPO):
        with (logs / (name + '.log')).open('xb') as stream:
            subprocess.run(argv, cwd=cwd, env=clean_environment(), stdout=stream,
                           stderr=subprocess.STDOUT, check=True)
        evidence['completed_commands'].append(name)

    def cli(source, command, target, name):
        dll = source / 'src/FootprintSim.Cli/bin/Release/net8.0/FootprintSim.Cli.dll'
        run(name, ['dotnet', str(dll), *command, '--seeds', '0..199', '--output', str(target)], source)

    try:
        for source, expected in [(REPO, args.expected_source_sha), (baseline, BASELINE)]:
            if git(source, 'rev-parse', 'HEAD') != expected or git(source, 'status', '--porcelain'):
                raise ValueError('Source revision or clean checkout precondition failed')
        git(REPO, 'merge-base', '--is-ancestor', PREREG, 'HEAD')
        run('environment', ['bash', 'scripts/check-environment.sh'])
        run('python-tests', [sys.executable, '-B', '-m', 'unittest', 'discover', '-s', '.github/scripts', '-p', 'test_*.py', '-v'])
        for label, source in [('current', REPO), ('baseline', baseline)]:
            run(label + '-restore', ['dotnet', 'restore', 'FootprintSim.sln'], source)
            run(label + '-build', ['dotnet', 'build', 'FootprintSim.sln', '--no-restore', '-c', 'Release'], source)
        run('dotnet-tests', ['dotnet', 'test', 'FootprintSim.sln', '--no-build', '--no-restore', '-c', 'Release',
                             '--logger', 'trx', '--results-directory', str(logs / 'test-results')])
        run('format', ['dotnet', 'format', 'FootprintSim.sln', '--verify-no-changes', '--no-restore'])
        for label, source in [('baseline', baseline), ('current', REPO)]:
            for campaign, command in REFERENCE_COMMANDS.items():
                cli(source, command, output / 'references' / label / campaign, label + '-' + campaign)
        evidence['reference_comparisons'] = compare_references(
            output / 'references/baseline', output / 'references/current', BASELINE, args.expected_source_sha)
        command = ['evaluate-candidate-density', '--experiment', PROTOCOL, '--baseline-experiment', EXPERIMENT]
        for label in ('a', 'b'):
            cli(REPO, command, output / ('f19-' + label), 'f19-' + label)
        evidence['f19_repeatability'] = compare_f19_repeats(output / 'f19-a', output / 'f19-b')
        report = json.loads((output / 'f19-a/candidate-density.json').read_bytes())
        validate_primary_counts(report['summary']['worldCount'], report['summary']['primaryProfileEvaluationCount'])
        evidence['actual_world_count'] = report['summary']['worldCount']
        evidence['actual_primary_evaluation_count'] = report['summary']['primaryProfileEvaluationCount']
        run('independent-verifier', [sys.executable, '-B', 'scripts/verify-candidate-density.py',
            '--baseline-runs', str(output / 'references/current/f16/common-world-field-combination-runs.jsonl'),
            '--point-reference-runs', str(output / 'references/current/f18/candidate-interval-width-runs.jsonl'),
            '--actual-runs', str(output / 'f19-a/candidate-density-runs.jsonl'),
            '--expected-source-sha', args.expected_source_sha, '--output', str(output / 'independent')])
        independent = json.loads((output / 'independent/summary.json').read_bytes())
        validate_primary_counts(independent['worldCount'], independent['primaryProfileEvaluations'])
        if (independent['actualVerification']['allVerified'] is not True
                or independent['actualVerification']['cleanSourceManifestVerified'] is not True):
            raise ValueError('Complete independent verification evidence is missing')
        evidence['independent_verification'] = independent['actualVerification']
        if git(REPO, 'status', '--porcelain') or git(baseline, 'status', '--porcelain'):
            raise ValueError('Validation changed a source checkout')
        evidence['input_hashes'] = {path: sha256((REPO / path).read_bytes()) for path in (PROTOCOL, EXPERIMENT)}
        evidence['artifact_hashes'] = collect_artifact_hashes(output)
        evidence['complete'] = True
    finally:
        (output / 'evidence.json').write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'source_sha': evidence['source_sha'], 'complete': evidence['complete'],
                      'reference_artifacts': len(evidence['reference_comparisons']), 'f19_files': len(F19_FILES)}))


if __name__ == '__main__':
    main()
