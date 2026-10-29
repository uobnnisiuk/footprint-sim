#!/usr/bin/env python3
"""Independent standard-library F19 reference and optional C# artifact verifier.

Public grid construction, predicate evaluation, diagnosis, and algebraic checks
do not consume the original truth fields. Evaluation reads truth only after all
three public arms exist. Native address hashing is deliberately not duplicated.
Do not run with -O: assertions are fail-fast verification checks.
"""

import argparse
import csv
import hashlib
import json
import re
import subprocess
from collections import Counter
from itertools import combinations
from math import isclose
from pathlib import Path
from statistics import mean, median

REPO = Path(__file__).resolve().parents[1]
PREREG = '9039417314df4c11fe5bc088825bf5f25eaf4d98'
BASELINE_SHA = '6c114058f7e9479ae29e3d16030f10843ddfb71b8b8bc861d13f8840c0cd4c81'
EXPERIMENT_SHA = 'e717669c7178dcf8b278b831b6ee049a2aa8dae5a9c9e6366b8c32712649ca39'
POINT_REFERENCE_SHA = 'c5b62f9bbad305e1ee3176b86105d1a631d4e3ebeb6868703d41d5c76c0d4d81'
PROTOCOL_SHA = 'f0669251ab8b747fbd4b7517e169d70723f052ae0465d33cfac6d5fe355c6bbd'
BASELINE_FINAL_COMMIT = '77611bc52976178857ac442be6e07a9caa1a2a9d'
SUPPORT_RULE = 'm1f19-seven-starts-times-registered-regions-v1'
ORDERING_RULE = 'm1f19-point-support-order-v1'
PLATEAU_RULE = 'm1f17-explicit-region-domain-plateau-v1'
TRUTH_RULE = 'unchanged-original-world-truth-resolved-after-all-public-density-arms'
OUTPUT_FILES = ['candidate-density.json', 'candidate-density-runs.jsonl',
                'candidate-density-profiles.csv', 'candidate-density-conditionals.csv',
                'candidate-density-crosswalk.csv', 'candidate-density-manifest.json']
ARMS = ['point-baseline', 'half-missing', 'full-support']
ARM_PAIRS = [(ARMS[0], ARMS[1]), (ARMS[1], ARMS[2]), (ARMS[0], ARMS[2])]
PROFILES = {
    'none': (), 'location': ('location',), 'time': ('time',),
    'participant': ('participant',), 'location-time': ('location', 'time'),
    'location-participant': ('location', 'participant'),
    'time-participant': ('time', 'participant'),
    'location-time-participant': ('location', 'time', 'participant'),
}
FULL = 'location-time-participant'
PROFILE_PAIRS = list(combinations(PROFILES, 2))
ADDITIONS = [
    ('time-added-to-location', 'location', 'location-time', 'time'),
    ('participant-added-to-location', 'location', 'location-participant', 'participant'),
    ('location-added-to-time', 'time', 'location-time', 'location'),
    ('participant-added-to-time', 'time', 'time-participant', 'participant'),
    ('location-added-to-participant', 'participant', 'location-participant', 'location'),
    ('time-added-to-participant', 'participant', 'time-participant', 'time'),
    ('participant-added-to-location-time', 'location-time', FULL, 'participant'),
    ('time-added-to-location-participant', 'location-participant', FULL, 'time'),
    ('location-added-to-time-participant', 'time-participant', FULL, 'location'),
]
INCLUSIONS = [(p, q) for p in PROFILES for q in PROFILES
              if set(PROFILES[q]) < set(PROFILES[p]) and len(PROFILES[p])-len(PROFILES[q]) == 1]
CATEGORIES = ['additionalCandidateReduction', 'saturation', 'observationToConstraintPlateau',
              'constraintOverlapPlateau', 'constraintToCandidatePlateau']


def digest(data):
    return hashlib.sha256(data).hexdigest()


def registered_signature(value):
    """Recompute registered ASCII/integer JSON content hashes, not native ID factories.

    JsonDefaults uses two-space indentation and insertion order. Registered F19
    identity records use only ASCII identifiers, integers, booleans and null.
    Refuse other text or numeric forms rather than guessing .NET escaping or
    decimal serialization. This is an intentionally bounded checksum contract.
    """
    pending = [value]
    while pending:
        item = pending.pop()
        if isinstance(item, dict):
            assert all(isinstance(key, str) for key in item), 'signature keys must be strings'
            pending.extend(item.keys())
            pending.extend(item.values())
        elif isinstance(item, list):
            pending.extend(item)
        elif isinstance(item, str):
            assert all(32 <= ord(c) <= 126 and c not in '\\"\'`<>&+' for c in item), (
                'unsupported registered signature text')
        else:
            assert item is None or type(item) in (bool, int), 'unsupported signature value'
    return digest(json.dumps(value, ensure_ascii=True, indent=2, allow_nan=False).encode('ascii'))


def load_point_reference(path):
    assert path is not None, 'actual verification requires immutable F18 point reference'
    data = path.read_bytes()
    assert digest(data) == POINT_REFERENCE_SHA, 'immutable F18 point reference changed'
    rows = [json.loads(line) for line in data.splitlines()]
    assert [row['seed'] for row in rows] == list(range(200)), 'F18 reference seed sequence changed'
    return rows


def support_signature(public):
    return registered_signature({
        'ruleId': SUPPORT_RULE, 'seed': public['seed'], 'baseTick': public['baseTick'],
        'registeredRegionIds': public['regions'],
        'cells': [{'startTick': tick, 'regionId': region}
                  for tick in range(public['baseTick'] - 3, public['baseTick'] + 4)
                  for region in public['regions']],
    })


def verify_support_identity(support, public):
    assert support['seed'] == public['seed'] and support['baseTick'] == public['baseTick']
    assert support['registeredRegionIds'] == public['regions']
    assert support['supportCellCount'] == len(public['regions']) * 7
    assert support['supportContentSignature'] == support_signature(public), 'support content hash drift'


def verify_public_identity_hashes(arm, public_arm, signature, fixed_constraint_signature):
    candidate_signature = registered_signature(public_arm['candidates'])
    assert arm['candidateInputSignature'] == candidate_signature, 'public candidate input hash drift'
    assert arm['representationId'] == registered_signature({
        'supportContentSignature': signature, 'armId': arm['armId'],
        'candidateInputSignature': candidate_signature,
    }), 'density representation hash drift'
    assert arm['fixedConstraintSignature'] == fixed_constraint_signature, 'fixed constraint signature drift'
    raw = indexed(public_arm['candidates'], 'candidateKey')
    for candidate in arm['candidates']:
        assert candidate['provenanceSignature'] == registered_signature(
            raw[candidate['candidateKey']]['state']['provenance']), 'provenance content hash drift'


def verify_manifest(manifest, actual_worlds, expected_source_sha):
    assert re.fullmatch(r'[0-9a-f]{40}', expected_source_sha or ''), 'expected full source SHA required'
    expected_fixed = {
        'experimentId': 'm1f19-nested-point-support-density',
        'sourceCommitSha': expected_source_sha, 'dirtyWorkingTree': False,
        'protocolContentSha256': PROTOCOL_SHA, 'baselineExperimentContentSha256': EXPERIMENT_SHA,
        'baselineFinalCommit': BASELINE_FINAL_COMMIT, 'supportRuleId': SUPPORT_RULE,
        'orderingRuleId': ORDERING_RULE, 'plateauRuleId': PLATEAU_RULE,
        'seeds': list(range(200)), 'truthMappingRule': TRUTH_RULE, 'outputFiles': OUTPUT_FILES,
    }
    assert set(manifest) == set(expected_fixed) | {'fixedInputSignature', 'publicOutputSignature'}, (
        'unexpected manifest fields')
    for key, expected in expected_fixed.items():
        assert type(manifest[key]) is type(expected) and manifest[key] == expected, ('manifest', key)
    assert [row['seed'] for row in actual_worlds] == list(range(200)), 'manifest input seed sequence'
    fixed_inputs = [{
        'seed': row['seed'], 'originalWorldBeforeSignature': row['originalWorldBeforeSignature'],
        'rawEvidenceSignature': row['rawEvidenceSignature'], 'sourceGraphSignature': row['sourceGraphSignature'],
        'supportContentSignature': row['support']['supportContentSignature'],
        'fixedConstraintSignature': row['arms'][0]['fixedConstraintSignature'],
    } for row in actual_worlds]
    public_outputs = [{'seed': row['seed'], 'support': row['support'], 'arms': row['arms']}
                      for row in actual_worlds]
    assert manifest['fixedInputSignature'] == registered_signature(fixed_inputs), 'fixed input manifest hash drift'
    assert manifest['publicOutputSignature'] == registered_signature(public_outputs), 'public output manifest hash drift'


def indexed(rows, field):
    result = {row[field]: row for row in rows}
    assert len(result) == len(rows), ('duplicate', field)
    return result


def indexed_pair(rows, left, right):
    result = {(row[left], row[right]): row for row in rows}
    assert len(result) == len(rows), ('duplicate', left, right)
    return result


def stats(values):
    values = list(values)
    return {'mean': mean(values), 'median': median(values), 'minimum': min(values),
            'maximum': max(values), 'distribution': dict(sorted(Counter(values).items()))}


def public_input(run):
    """Whitelist projection; no truth field is accepted by downstream functions."""
    world = run['world']
    possible = set(world['observedPossibleParticipantIds'])
    participants = indexed(world['participants'], 'participantId')
    assert possible and possible <= set(participants), 'unknown observed participant'
    return {
        'seed': run['seed'], 'baseTick': world['candidateBaseTick'],
        'regions': sorted(world['regionUniverse']),
        'baselineCandidates': [
            {'key': c['candidateKey'], 'source': c['publicSourceIdentifier'],
             'region': c['regionId'], 'start': c['startTick'], 'introductionArm': ARMS[0]}
            for c in world['candidateStates']],
        'location': sorted(world['locationObservedRegionIds']),
        'participant': sorted(set().union(*(set(participants[pid]['allowedRegionIds']) for pid in possible))),
        'timeLower': world['laterEventTick']-world['durationMaximumTicks'],
        'timeUpper': world['laterEventTick']-world['durationMinimumTicks'],
    }


def construct(public):
    """Add a ranked prefix of missing point cells; no predicates or truth used."""
    regions, base, seed = public['regions'], public['baseTick'], public['seed']
    baseline = public['baselineCandidates']
    coords = {(c['start'], c['region']) for c in baseline}
    assert len(coords) == len(baseline)
    assert len({c['key'] for c in baseline}) == len(baseline)
    assert len({c['source'] for c in baseline}) == len(baseline)
    grid = {(base+offset, region) for offset in range(-3, 4) for region in regions}
    assert coords <= grid
    missing = []
    for start, region in grid-coords:
        key = f'density-t{start}-r{regions.index(region):02d}'
        rank_input = f'm1f19-point-support-order-v1|{seed}|{base}|{start-base}|{region}'
        missing.append({'key': key, 'source': f'seed-{seed:03d}-{key}-public-model',
                        'region': region, 'start': start, 'rankHash': digest(rank_input.encode())})
    missing.sort(key=lambda c: (c['rankHash'], c['start'], c['region']))
    halfway = len(missing)//2
    for ordinal, c in enumerate(missing):
        c['rank'] = ordinal
        c['introductionArm'] = ARMS[1] if ordinal < halfway else ARMS[2]
        c['sharedExternalSource'] = f'm1f19-seed-{seed:03d}-point-support-model'
    assert len({c['key'] for c in baseline+missing}) == len(grid)
    assert len({c['source'] for c in baseline+missing}) == len(grid)
    arms = {ARMS[0]: baseline, ARMS[1]: baseline+missing[:halfway], ARMS[2]: baseline+missing}
    return arms, missing


def infer(public, candidates):
    return {pid: {c['key'] for c in candidates
                  if ('location' not in axes or c['region'] in public['location'])
                  and ('participant' not in axes or c['region'] in public['participant'])
                  and ('time' not in axes or public['timeLower'] <= c['start'] <= public['timeUpper'])}
            for pid, axes in PROFILES.items()}


def diagnostic(public, candidates, sets, baseline, combined, added):
    added_regions = set(public['location'] if added == 'location' else public['participant'])
    top = (all(public['timeLower'] <= c['start'] <= public['timeUpper'] for c in candidates)
           if added == 'time' else set(public['regions']) <= added_regions)
    applicable = added != 'time' and bool(set(PROFILES[baseline]) & {'location', 'participant'})
    allowed = set(public['regions'])
    for axis in ['location', 'participant']:
        if axis in PROFILES[baseline]:
            allowed &= set(public[axis])
    witnesses = sorted(allowed-added_regions) if applicable else []
    implied = applicable and not witnesses
    category = ('additionalCandidateReduction' if sets[baseline] != sets[combined] else
                'saturation' if len(sets[baseline]) == 1 else
                'observationToConstraintPlateau' if top else
                'constraintOverlapPlateau' if implied else 'constraintToCandidatePlateau')
    return {'classification': category, 'top': top, 'implied': implied,
            'regionImplicationApplicable': applicable, 'implicationCounterexampleRegions': witnesses}


def minima(sets):
    count = min(map(len, sets.values()))
    at_min = [p for p in PROFILES if len(sets[p]) == count]
    minimal = [p for p in at_min if not any(set(PROFILES[q]) < set(PROFILES[p]) for q in at_min)]
    assert all(sets[p] == sets[FULL] for p in at_min)
    return {'minimumCount': count, 'profilesAtMinimum': at_min,
            'inclusionMinimalProfiles': minimal, 'minimumFieldCount': min(len(PROFILES[p]) for p in at_min)}


def occupancy(public, candidates):
    ticks = list(range(public['baseTick']-3, public['baseTick']+4))
    start_counts = Counter(c['start'] for c in candidates)
    region_counts = Counter(c['region'] for c in candidates)
    size = len(public['regions'])*7
    return {'supportCount': size, 'candidateCount': len(candidates), 'density': len(candidates)/size,
            'occupiedTickCount': len(start_counts), 'occupiedRegionCount': len(region_counts),
            'unusedRegionCount': len(public['regions'])-len(region_counts),
            'unusedRegionFraction': (len(public['regions'])-len(region_counts))/len(public['regions']),
            'startHistogram': {tick: start_counts[tick] for tick in ticks},
            'regionHistogram': {region: region_counts[region] for region in public['regions']},
            'widthHistogram': {0: len(candidates)}}


def public_evaluation(public, checks):
    candidates_by_arm, missing = construct(public)
    sets_by_arm = {aid: infer(public, candidates) for aid, candidates in candidates_by_arm.items()}
    arms = {}
    assert public['timeLower'] <= public['timeUpper'] and public['timeUpper']-public['timeLower'] <= 3
    for axis in ['location', 'participant']:
        assert set(public[axis]) <= set(public['regions'])
    for aid in ARMS:
        candidates, sets = candidates_by_arm[aid], sets_by_arm[aid]
        for subset, superset in INCLUSIONS:
            assert sets[subset] <= sets[superset]
            checks['jointInclusionChecks'] += 1
        profiles = {}
        for pid, axes in PROFILES.items():
            excluded = sets['none']-sets[pid]
            profiles[pid] = {'keys': sorted(sets[pid]), 'count': len(sets[pid]),
                             'excludedKeys': sorted(excluded), 'reductionCount': len(excluded),
                             'retainedFraction': len(sets[pid])/len(candidates),
                             'reductionFraction': len(excluded)/len(candidates),
                             'work': len(candidates)*(len(axes)+1)}
        conditionals = {}
        for cid, before, after, axis in ADDITIONS:
            excluded = sets[before]-sets[after]
            diag = diagnostic(public, candidates, sets, before, after, axis)
            conditionals[cid] = {'excludedKeys': sorted(excluded), 'count': len(excluded),
                                 'fraction': len(excluded)/len(sets[before]), 'diagnostic': diag}
            if aid == ARMS[2]:
                assert diag['classification'] != 'constraintToCandidatePlateau'
                if axis == 'time':
                    assert excluded
                checks['fullSupportConditionalMechanismChecks'] += 1
        equivalent = {f'{p}|{q}': sets[p] == sets[q] for p, q in PROFILE_PAIRS}
        arms[aid] = {'candidates': sorted(candidates, key=lambda c: c['key']), 'profiles': profiles,
                     'conditionals': conditionals, 'equivalences': equivalent,
                     'minimum': minima(sets), 'occupancy': occupancy(public, candidates)}
    analytic = {}
    for pid, axes in PROFILES.items():
        region_set = set(public['regions'])
        for axis in ['location', 'participant']:
            if axis in axes:
                region_set &= set(public[axis])
        tick_count = (sum(public['timeLower'] <= tick <= public['timeUpper']
                          for tick in range(public['baseTick']-3, public['baseTick']+4))
                      if 'time' in axes else 7)
        analytic[pid] = tick_count*len(region_set)
        assert len(sets_by_arm[ARMS[2]][pid]) == analytic[pid]
        checks['fullSupportAnalyticProfileChecks'] += 1
    pairs = []
    for older, denser in ARM_PAIRS:
        a, b = arms[older], arms[denser]
        old_sets, new_sets = sets_by_arm[older], sets_by_arm[denser]
        introduced = new_sets['none']-old_sets['none']
        assert old_sets['none'] < new_sets['none']
        profile_changes, conditional_changes = {}, {}
        for pid in PROFILES:
            assert old_sets[pid] <= new_sets[pid]
            assert new_sets[pid] & old_sets['none'] == old_sets[pid]
            old_ex, new_ex = old_sets['none']-old_sets[pid], new_sets['none']-new_sets[pid]
            assert old_ex <= new_ex
            assert new_sets[pid]-old_sets[pid] == introduced & new_sets[pid]
            checks['pairedProfileNestingRestrictionAndExcludedChecks'] += 1
            profile_changes[pid] = {
                'newKeys': sorted(new_sets[pid]-old_sets[pid]),
                'countDelta': len(new_sets[pid])-len(old_sets[pid]),
                'reductionDelta': len(new_ex)-len(old_ex),
                'reductionFractionDelta': b['profiles'][pid]['reductionFraction']-a['profiles'][pid]['reductionFraction']}
        for cid, _, _, axis in ADDITIONS:
            old, new = a['conditionals'][cid], b['conditionals'][cid]
            assert set(old['excludedKeys']) <= set(new['excludedKeys'])
            assert new['count'] >= old['count']
            od, nd = old['diagnostic'], new['diagnostic']
            assert od['implied'] == nd['implied']
            assert od['implicationCounterexampleRegions'] == nd['implicationCounterexampleRegions']
            if axis == 'time':
                assert od['top'] or not nd['top']
            else:
                assert od['top'] == nd['top']
            assert od['classification'] == 'saturation' or nd['classification'] != 'saturation'
            checks['conditionalNestingAndFlagChecks'] += 1
            conditional_changes[cid] = {
                'countDelta': new['count']-old['count'],
                'fractionDelta': new['fraction']-old['fraction'],
                'fromClassification': od['classification'], 'toClassification': nd['classification'],
                'newlyDetectable': old['count'] == 0 and new['count'] > 0,
                'lostEffect': old['count'] > 0 and new['count'] == 0,
                'newExcludedKeys': sorted(set(new['excludedKeys'])-set(old['excludedKeys']))}
        for key in a['equivalences']:
            assert a['equivalences'][key] or not b['equivalences'][key]
            checks['equivalenceDirectionChecks'] += 1
        am, bm = a['minimum'], b['minimum']
        assert am['minimumCount'] <= bm['minimumCount']
        assert set(bm['profilesAtMinimum']) <= set(am['profilesAtMinimum'])
        assert am['minimumFieldCount'] <= bm['minimumFieldCount']
        for p in bm['inclusionMinimalProfiles']:
            assert any(set(PROFILES[q]) <= set(PROFILES[p]) for q in am['inclusionMinimalProfiles'])
        assert FULL not in am['inclusionMinimalProfiles'] or FULL in bm['inclusionMinimalProfiles']
        checks['minimumDirectionChecks'] += 1
        pairs.append({'olderArmId': older, 'denserArmId': denser,
                      'introducedKeys': sorted(introduced), 'profiles': profile_changes,
                      'conditionals': conditional_changes,
                      'minimumCountChanged': am['minimumCount'] != bm['minimumCount'],
                      'minimumFamilyChanged': am['inclusionMinimalProfiles'] != bm['inclusionMinimalProfiles'],
                      'lostAtMinimum': sorted(set(am['profilesAtMinimum'])-set(bm['profilesAtMinimum'])),
                      'lostMinimal': sorted(set(am['inclusionMinimalProfiles'])-set(bm['inclusionMinimalProfiles'])),
                      'gainedMinimal': sorted(set(bm['inclusionMinimalProfiles'])-set(am['inclusionMinimalProfiles'])),
                      'equivalencesLost': [key for key in a['equivalences'] if a['equivalences'][key] and not b['equivalences'][key]]})
    return {'seed': public['seed'], 'publicInput': public, 'missingCellsInRankOrder': missing,
            'arms': arms, 'pairs': pairs, 'fullSupportAnalyticCounts': analytic}


def summarize(worlds):
    arm_summaries = {}
    for aid in ARMS:
        selected = [w['arms'][aid] for w in worlds]
        arm_summaries[aid] = {
            'profiles': {pid: {field: stats(a['profiles'][pid][field] for a in selected)
                                for field in ['count', 'reductionCount', 'reductionFraction', 'retainedFraction', 'work']}
                         for pid in PROFILES},
            'conditionals': {cid: {field: stats(a['conditionals'][cid][field] for a in selected)
                                   for field in ['count', 'fraction']} for cid, _, _, _ in ADDITIONS},
            'plateauCounts': dict(Counter(c['diagnostic']['classification'] for a in selected
                                         for c in a['conditionals'].values())),
            'minimumCounts': stats(a['minimum']['minimumCount'] for a in selected),
            'minimumFieldCounts': stats(a['minimum']['minimumFieldCount'] for a in selected),
            'atMinimumFrequencies': {pid: sum(pid in a['minimum']['profilesAtMinimum'] for a in selected) for pid in PROFILES},
            'inclusionMinimalFrequencies': {pid: sum(pid in a['minimum']['inclusionMinimalProfiles'] for a in selected) for pid in PROFILES},
            'minimumFamilyFrequencies': dict(sorted(Counter('|'.join(a['minimum']['inclusionMinimalProfiles']) for a in selected).items())),
            'equivalenceFrequencies': {key: sum(a['equivalences'][key] for a in selected) for key in selected[0]['equivalences']},
            'occupancy': {field: stats(a['occupancy'][field] for a in selected)
                          for field in ['candidateCount', 'supportCount', 'density', 'occupiedTickCount', 'occupiedRegionCount', 'unusedRegionCount', 'unusedRegionFraction']},
        }
    pair_summaries = []
    for ordinal, (older, denser) in enumerate(ARM_PAIRS):
        selected = [(w['seed'], w['pairs'][ordinal]) for w in worlds]
        conditionals = {}
        for cid in ['all']+[row[0] for row in ADDITIONS]:
            cs = [(seed, c) for seed, p in selected for key, c in p['conditionals'].items() if cid == 'all' or key == cid]
            conditionals[cid] = {
                'delta': stats(c['countDelta'] for _, c in cs),
                'changedCount': sum(c['countDelta'] != 0 for _, c in cs),
                'newlyDetectableCount': sum(c['newlyDetectable'] for _, c in cs),
                'lostEffectCount': sum(c['lostEffect'] for _, c in cs),
                'classificationChangedCount': sum(c['fromClassification'] != c['toClassification'] for _, c in cs),
                'classificationAffectedSeeds': sorted({s for s, c in cs if c['fromClassification'] != c['toClassification']}),
                'transitions': [{'fromClassification': a, 'toClassification': b,
                                 'count': sum(c['fromClassification'] == a and c['toClassification'] == b for _, c in cs),
                                 'seeds': sorted({s for s, c in cs if c['fromClassification'] == a and c['toClassification'] == b})}
                                for a in CATEGORIES for b in CATEGORIES],
            }
        pair_summaries.append({
            'olderArmId': older, 'denserArmId': denser,
            'profiles': {pid: {'countDelta': stats(p['profiles'][pid]['countDelta'] for _, p in selected),
                               'reductionDelta': stats(p['profiles'][pid]['reductionDelta'] for _, p in selected),
                               'fractionDelta': stats(p['profiles'][pid]['reductionFractionDelta'] for _, p in selected),
                               'changedWorldCount': sum(p['profiles'][pid]['countDelta'] != 0 for _, p in selected)} for pid in PROFILES},
            'conditionals': conditionals,
            'minimumCountChangedWorlds': sum(p['minimumCountChanged'] for _, p in selected),
            'minimumFamilyChangedWorlds': sum(p['minimumFamilyChanged'] for _, p in selected),
            'minimumFamilyChangedSeeds': [s for s, p in selected if p['minimumFamilyChanged']],
            'equivalenceLostCount': sum(len(p['equivalencesLost']) for _, p in selected),
            'equivalenceAffectedSeeds': [s for s, p in selected if p['equivalencesLost']],
        })
    return {'armSummaries': arm_summaries, 'pairSummaries': pair_summaries}


def close(actual, expected):
    assert isclose(actual, expected, rel_tol=0, abs_tol=1e-12), (actual, expected)


def verify_statistics(actual, values):
    values = list(values)
    expected = stats(values)
    for field in ['mean', 'median', 'minimum', 'maximum']:
        close(actual[field], expected[field])
    if all(isinstance(value, int) for value in values):
        assert actual['minimum'] == expected['minimum'] and actual['maximum'] == expected['maximum']
        distribution = indexed(actual['distribution'], 'value')
        assert {value: row['count'] for value, row in distribution.items()} == Counter(values)
        return
    # Decimal divisions/subtractions can have distinct last-place residues.
    # Compare every distribution observation without conflating integer counts
    # or requiring Python binary floats to share decimal rounding partitions.
    expanded = [row['value'] for row in actual['distribution'] for _ in range(row['count'])]
    assert len(expanded) == len(values)
    for found, wanted in zip(sorted(expanded), sorted(values)):
        close(found, wanted)


def verify_diagnostic(actual, expected, regions):
    assert actual['domainId'] == 'registered'
    assert actual['domainRegionIds'] == regions and actual['implicationDomainRegionIds'] == regions
    for found, wanted in [('classification', 'classification'),
                           ('addedPredicateIsTopForCandidateDomain', 'top'),
                           ('addedPredicateImpliedByBaselinePredicates', 'implied'),
                           ('regionImplicationApplicable', 'regionImplicationApplicable'),
                           ('implicationCounterexampleRegionIds', 'implicationCounterexampleRegions')]:
        assert actual[found] == expected[wanted], (found, actual[found], expected[wanted])


def assert_true(row, fields):
    for field in fields:
        assert row[field] is True, field


def verify_profile_membership(profile, expected, native_by_id, axes):
    assert profile['candidateKeys'] == expected['keys'], 'profile key membership'
    assert profile['candidateIds'] == sorted(set(profile['candidateIds'])), 'duplicate or unordered native IDs'
    assert {native_by_id[n] for n in profile['candidateIds']} == set(expected['keys']), 'native membership'
    assert profile['candidateCount'] == expected['count'], 'profile count'
    assert profile['candidateGenerationWork'] == expected['work'], 'profile work'
    assert set(profile['activeAxes']) == set(axes), 'profile axes'


def verify_minimum(actual, expected):
    assert actual['minimumCandidateCount'] == expected['minimumCount'], 'minimum candidate count'
    assert actual['profilesAtMinimumCount'] == expected['profilesAtMinimum'], 'profiles at minimum'
    assert actual['inclusionMinimalProfileIds'] == expected['inclusionMinimalProfiles'], 'minimal family'
    assert actual['allMinimumProfilesReturnSameKeySet'] is True, 'minimum set equality'


def verify_actual(worlds, baseline_runs, actual_path, summary, point_reference_path, expected_source_sha):
    """Compare independent scientific outputs, plus preserved native identity."""
    actual_bytes = actual_path.read_bytes()
    actual = [json.loads(line) for line in actual_bytes.splitlines()]
    assert [row['seed'] for row in actual] == list(range(200))
    point_refs = load_point_reference(point_reference_path)
    checks = Counter()
    for wanted, found, baseline in zip(worlds, actual, baseline_runs):
        seed = wanted['seed']
        public = wanted['publicInput']
        assert found['originalWorld'] == baseline['world'], (seed, 'original world drift')
        assert_true(found, ['f18PointReferenceMatches', 'fullSupportAnalyticCountsMatch', 'allInvariantsSatisfied'])
        assert found['originalWorldBeforeSignature'] == found['originalWorldAfterSignature']
        assert found['publicRequestBeforeSignature'] == found['publicRequestAfterSignature']
        support = found['support']
        verify_support_identity(support, public)
        assert len(support['rankedMissingCells']) == len(wanted['missingCellsInRankOrder'])
        for got, exp in zip(support['rankedMissingCells'], wanted['missingCellsInRankOrder']):
            for g, e in [('startTick', 'start'), ('regionId', 'region'), ('candidateKey', 'key'),
                         ('publicSourceIdentifier', 'source'), ('orderHash', 'rankHash')]:
                assert got[g] == exp[e], (seed, 'missing cell order', g)
            assert got['offset'] == exp['start']-public['baseTick']
            assert got['regionIndex'] == public['regions'].index(exp['region'])
            checks['missingCellHashAndCoordinateChecks'] += 1
        arms = indexed(found['arms'], 'armId')
        assert list(arms) == ARMS
        public_arms = indexed(support['arms'], 'armId')
        assert list(public_arms) == ARMS
        assert len({a['fixedConstraintSignature'] for a in arms.values()}) == 1
        assert len({a['candidateUniverseId'] for a in arms.values()}) == 3
        natives = {aid: indexed(arm['candidates'], 'candidateKey') for aid, arm in arms.items()}
        metrics = {(m['armId'], m['profileId']): m for m in found['profileMetrics']}
        assert len(metrics) == len(found['profileMetrics']) == 24
        cmetrics = {(m['armId'], m['comparisonId']): m for m in found['conditionalMetrics']}
        assert len(cmetrics) == len(found['conditionalMetrics']) == 27
        occupancies = indexed(found['occupancy'], 'armId')
        baseline_keys = {c['key'] for c in public['baselineCandidates']}
        bridge = indexed(found['intervalToPointCrosswalk'], 'candidateKey')
        assert set(bridge) == baseline_keys
        baseline_native = {(c['timeWindow']['startTick'], c['timeWindow']['endTick'], c['regionId']): c
                           for c in next(p for p in baseline['profiles'] if p['profileId'] == 'none')['candidates']}
        original_raw = indexed(baseline['world']['candidateStates'], 'candidateKey')
        reference_profiles = indexed(baseline['profiles'], 'profileId')
        for aid, arm in arms.items():
            exp = wanted['arms'][aid]
            by_key = indexed(exp['candidates'], 'key')
            assert set(natives[aid]) == set(by_key)
            assert len({c['candidateId'] for c in natives[aid].values()}) == len(by_key)
            assert len({c['stateId'] for c in natives[aid].values()}) == len(by_key)
            raw_public = indexed(public_arms[aid]['candidates'], 'candidateKey')
            assert set(raw_public) == set(by_key)
            assert public_arms[aid]['addedCandidateCount'] == len(by_key)-len(baseline_keys)
            verify_public_identity_hashes(arm, public_arms[aid], support['supportContentSignature'],
                                          point_refs[seed]['point']['fixedConstraintSignature'])
            for key, candidate in natives[aid].items():
                cell = by_key[key]
                assert candidate['publicSourceIdentifier'] == cell['source']
                assert candidate['regionId'] == cell['region']
                assert candidate['timeWindow'] == {'startTick': cell['start'], 'endTick': cell['start']}
                assert candidate['temporalKind'] == 'eventPoint'
                assert candidate == natives[ARMS[2]][key], (seed, aid, key, 'native changed')
                raw = raw_public[key]
                assert raw == indexed(public_arms[ARMS[2]]['candidates'], 'candidateKey')[key]
                assert raw['publicSourceIdentifier'] == cell['source']
                state = raw['state']
                assert state['timeWindow'] == candidate['timeWindow']
                assert state['temporalKind'] == 'eventPoint' and state['regionIds'] == [cell['region']]
                assert state['evidenceBoundaryState'] == 'confirmed'
                assert len(state['provenance']) == 1
                prov = state['provenance'][0]
                expected_prov = {
                    'sourceKind': 'publicCausalModel', 'sourceIdentifier': cell['source'], 'availableAtTick': 0,
                    'isPersonEvidence': False, 'isDeviceOnlyEvidence': False, 'evidenceBoundaryState': 'confirmed',
                    'visibility': 'publicEvidence', 'regionIds': [cell['region']], 'relationKind': None,
                    'sourceObservationId': None, 'sourceTimeWindow': None, 'participants': [],
                    'interactionProvenance': ([] if key in baseline_keys else [{'kind': 'externalSource',
                        'sourceIdentifier': f'm1f19-seed-{seed:03d}-point-support-model'}]),
                }
                assert prov == expected_prov, (seed, aid, key, 'provenance', prov)
                if key in baseline_keys:
                    assert bridge[key]['point'] == candidate
                    if point_refs:
                        reference_native = indexed(point_refs[seed]['point']['candidates'], 'candidateKey')[key]
                        assert candidate == reference_native
                checks['nativeCoordinatesProvenanceAndRetentionChecks'] += 1
            profiles = indexed(arm['profiles'], 'profileId')
            assert set(profiles) == set(PROFILES)
            native_by_id = {v['candidateId']: k for k, v in natives[aid].items()}
            for pid, profile in profiles.items():
                expected = exp['profiles'][pid]
                verify_profile_membership(profile, expected, native_by_id, PROFILES[pid])
                assert profile['compositionId'] == reference_profiles[pid]['compositionId']
                audit_expected = dict(reference_profiles[pid]['compositionAudit'])
                audit_expected['candidatePredicateChecks'] = len(by_key)*len(PROFILES[pid])
                assert profile['compositionAudit'] == audit_expected, (seed, aid, pid, 'composition audit')
                metric = metrics[aid, pid]
                assert metric['candidateCount'] == expected['count']
                assert metric['originalMemberCount'] == len(set(expected['keys']) & baseline_keys)
                assert metric['introducedMemberCount'] == len(set(expected['keys'])-baseline_keys)
                assert metric['excludedKeys'] == expected['excludedKeys']
                assert metric['reductionVsNone'] == expected['reductionCount']
                close(metric['reductionFraction'], expected['reductionFraction'])
                close(metric['retainedFraction'], expected['retainedFraction'])
                assert metric['candidateGenerationWork'] == expected['work']
                assert_true(metric, ['containsTruth', 'workRuleSatisfied'])
                assert metric['falseExclusionCount'] == 0
                checks['profileMembershipWorkReductionChecks'] += 1
            conditionals = indexed(arm['conditionals'], 'comparisonId')
            assert set(conditionals) == {row[0] for row in ADDITIONS}
            for cid, before, after, axis in ADDITIONS:
                c, ec = conditionals[cid], exp['conditionals'][cid]
                assert c['baselineProfileId'] == before and c['combinedProfileId'] == after and c['addedAxis'] == axis
                assert c['baselineCandidateCount'] == exp['profiles'][before]['count']
                assert c['combinedCandidateCount'] == exp['profiles'][after]['count']
                assert c['additionalCandidateReduction'] == ec['count']
                verify_diagnostic(c['diagnostic'], ec['diagnostic'], public['regions'])
                cm = cmetrics[aid, cid]
                assert cm['excludedKeys'] == ec['excludedKeys']
                assert cm['additionalCandidateReduction'] == ec['count']
                close(cm['reductionFraction'], ec['fraction'])
                checks['conditionalMembershipAndDiagnosticChecks'] += 1
            equivalents = indexed_pair(arm['profileEquivalences'], 'leftProfileId', 'rightProfileId')
            assert set(equivalents) == set(PROFILE_PAIRS)
            for (p, q), item in equivalents.items():
                assert item['candidateKeySetsEqual'] == exp['equivalences'][f'{p}|{q}']
                checks['profileEquivalenceChecks'] += 1
            minimum = arm['minimum']
            verify_minimum(minimum, exp['minimum'])
            inclusions = indexed_pair(arm['setInclusions'], 'subsetProfileId', 'supersetProfileId')
            assert set(inclusions) == set(INCLUSIONS)
            assert all(x['candidateKeysAreSubset'] for x in inclusions.values())
            occ = occupancies[aid]
            eo = exp['occupancy']
            for field in ['candidateCount', 'occupiedRegionCount', 'occupiedTickCount', 'unusedRegionCount']:
                assert occ[field] == eo[field]
            assert occ['addedCandidateCount'] == len(by_key)-len(baseline_keys)
            assert occ['supportCellCount'] == eo['supportCount']
            close(occ['density'], eo['density'])
            close(occ['unusedRegionFraction'], eo['unusedRegionFraction'])
            for actual_field, expected_field in [('regionHistogram', 'regionHistogram'),
                                                  ('startTickHistogram', 'startHistogram'),
                                                  ('widthHistogram', 'widthHistogram')]:
                hist = {x['value']: x['count'] for x in occ[actual_field]}
                assert len(hist) == len(occ[actual_field])
                assert hist == {str(k): v for k, v in eo[expected_field].items()}
            checks['minimumAndOccupancyChecks'] += 1
        for key in baseline_keys:
            row = bridge[key]
            raw = original_raw[key]
            original = baseline_native[raw['startTick'], raw['endTick'], raw['regionId']]
            assert row['seed'] == seed
            for field in ['candidateId', 'stateId', 'regionId', 'timeWindow']:
                assert row['original'][field] == original[field]
            assert row['original']['provenanceSignature'] == row['point']['provenanceSignature']
            assert (row['original']['candidateId'] == row['point']['candidateId']) == (raw['startTick'] == raw['endTick'])
            assert (row['original']['stateId'] == row['point']['stateId']) == (raw['startTick'] == raw['endTick'])
            checks['originalIntervalBridgeChecks'] += 1
        full = indexed(wanted['arms'][ARMS[2]]['candidates'], 'key')
        crosswalk = indexed(found['candidateCrosswalk'], 'candidateKey')
        assert set(crosswalk) == set(full)
        for key, cw in crosswalk.items():
            assert cw['candidate'] == natives[ARMS[2]][key]
            assert cw['introductionArmId'] == full[key]['introductionArm']
            assert cw['presentArmIds'] == [aid for aid in ARMS if key in natives[aid]]
            assert cw['originalIntervalCandidateId'] == (bridge[key]['original']['candidateId'] if key in baseline_keys else None)
            checks['densityIdentityCrosswalkChecks'] += 1
        pairs = indexed_pair(found['pairChanges'], 'fromArmId', 'toArmId')
        assert set(pairs) == set(ARM_PAIRS)
        for expected in wanted['pairs']:
            older, denser = expected['olderArmId'], expected['denserArmId']
            pair = pairs[older, denser]
            assert_true(pair, ['universeKeysNested', 'universeNativeIdsNested', 'oldCandidateDescriptorsUnchanged', 'allInvariantsSatisfied'])
            assert pair['addedUniverseKeys'] == expected['introducedKeys']
            assert {c['candidateId'] for c in natives[older].values()} <= {c['candidateId'] for c in natives[denser].values()}
            pc = indexed(pair['profileChanges'], 'profileId')
            assert set(pc) == set(PROFILES)
            for pid in PROFILES:
                row, ep = pc[pid], expected['profiles'][pid]
                op, np = wanted['arms'][older]['profiles'][pid], wanted['arms'][denser]['profiles'][pid]
                assert row['fromCount'] == op['count'] and row['toCount'] == np['count']
                assert row['countDelta'] == ep['countDelta'] and row['reductionDelta'] == ep['reductionDelta']
                assert row['addedSurvivorKeys'] == ep['newKeys'] and row['expectedAddedSurvivorKeys'] == ep['newKeys']
                assert row['newExcludedKeys'] == sorted(set(np['excludedKeys'])-set(op['excludedKeys']))
                close(row['reductionFractionDelta'], ep['reductionFractionDelta'])
                close(row['retainedFractionDelta'], -ep['reductionFractionDelta'])
                assert_true(row, ['keysNested', 'nativeIdsNested', 'restrictionRecoversOriginal', 'newSurvivorsMatchPredicates', 'excludedKeysNested'])
                checks['pairedProfileChangeChecks'] += 1
            cc = indexed(pair['conditionalChanges'], 'comparisonId')
            assert set(cc) == {row[0] for row in ADDITIONS}
            for cid, _, _, axis in ADDITIONS:
                row, ec = cc[cid], expected['conditionals'][cid]
                oc, nc = wanted['arms'][older]['conditionals'][cid], wanted['arms'][denser]['conditionals'][cid]
                assert row['addedAxis'] == axis
                assert row['fromReduction'] == oc['count'] and row['toReduction'] == nc['count']
                assert row['reductionDelta'] == ec['countDelta']
                close(row['reductionFractionDelta'], ec['fractionDelta'])
                assert row['newExcludedKeys'] == ec['newExcludedKeys']
                assert row['newlyDetectableEffect'] == ec['newlyDetectable'] and row['lostEffect'] == ec['lostEffect']
                assert_true(row, ['excludedKeysNested', 'logicalFlagsValid'])
                verify_diagnostic(row['fromDiagnostic'], oc['diagnostic'], public['regions'])
                verify_diagnostic(row['toDiagnostic'], nc['diagnostic'], public['regions'])
                checks['pairedConditionalChangeChecks'] += 1
            ec = indexed_pair(pair['equivalenceChanges'], 'leftProfileId', 'rightProfileId')
            assert set(ec) == set(PROFILE_PAIRS)
            for (p, q), row in ec.items():
                old_eq = wanted['arms'][older]['equivalences'][f'{p}|{q}']
                new_eq = wanted['arms'][denser]['equivalences'][f'{p}|{q}']
                assert row['fromEquivalent'] == old_eq and row['toEquivalent'] == new_eq
                assert row['becameDistinguishable'] == (old_eq and not new_eq)
                assert row['becameIndistinguishable'] is False
                checks['pairedEquivalenceChangeChecks'] += 1
            mc = pair['minimumChange']
            om, nm = wanted['arms'][older]['minimum'], wanted['arms'][denser]['minimum']
            assert mc['fromMinimumCount'] == om['minimumCount'] and mc['toMinimumCount'] == nm['minimumCount']
            assert mc['fromMinimumFieldCount'] == om['minimumFieldCount'] and mc['toMinimumFieldCount'] == nm['minimumFieldCount']
            assert mc['countChanged'] == expected['minimumCountChanged'] and mc['familyChanged'] == expected['minimumFamilyChanged']
            assert mc['lostAtMinimumProfiles'] == expected['lostAtMinimum'] and mc['gainedAtMinimumProfiles'] == []
            assert mc['lostInclusionMinimalProfiles'] == expected['lostMinimal']
            assert mc['gainedInclusionMinimalProfiles'] == expected['gainedMinimal']
            assert_true(mc, ['atMinimumProfilesNested', 'everyNewMinimalContainsOldMinimal', 'fullNecessityPreserved'])
            checks['pairedMinimumChangeChecks'] += 1
        # Truth remains evaluation-only; every public expected arm above existed first.
        truth = found['fixedTruth']
        truth_fields = [('originalTruthWorldId', 'truthWorldId'), ('originalTruthCandidateKey', 'truthCandidateKey'),
                        ('originalTruthStartTick', 'truthCandidateStartTick'), ('originalTruthEndTick', 'truthCandidateEndTick'),
                        ('originalTruthRegionId', 'truthRegionId'), ('originalTruthParticipantId', 'truthParticipantId'),
                        ('originalEncounterTick', 'encounterTick'), ('originalRealizedDurationTicks', 'actualTruthDurationTicks')]
        for g, e in truth_fields:
            assert truth[g] == baseline['world'][e]
        truth_key = wanted['truthEvaluation']['originalTruthCandidateKey']
        assert truth['originalIntervalCandidateId'] == bridge[truth_key]['original']['candidateId']
        addresses = indexed(truth['armAddresses'], 'armId')
        assert set(addresses) == set(ARMS)
        for aid, address in addresses.items():
            assert address['candidateId'] == natives[aid][truth_key]['candidateId']
            assert all(address['candidateId'] in p['candidateIds'] for p in arms[aid]['profiles'])
        assert truth['resolvedAfterAllPublicArms']
        checks['fixedTruthMappingChecks'] += 1
        if point_refs:
            for field in ['rawEvidenceSignature', 'sourceGraphSignature', 'originalWorldBeforeSignature']:
                assert found[field] == point_refs[seed][field]
            assert found['intervalToPointCrosswalk'] == point_refs[seed]['crosswalk']
    report_path = actual_path.with_name('candidate-density.json')
    report_bytes = report_path.read_bytes()
    verify_report(json.loads(report_bytes), worlds, summary)
    manifest = json.loads(actual_path.with_name('candidate-density-manifest.json').read_bytes())
    assert manifest == json.loads(report_bytes)['manifest']
    verify_manifest(manifest, actual, expected_source_sha)
    csv_checks = verify_csvs(actual, actual_path.parent)
    return {'actualRowsSha256': digest(actual_bytes), 'reportSha256': digest(report_bytes),
            'pointReferenceSha256': POINT_REFERENCE_SHA,
            'expectedSourceCommit': expected_source_sha, 'cleanSourceManifestVerified': True,
            'contentSignatureVerification': 'independent registered ASCII/integer JSON checksums; native ID factory not reimplemented',
            'worldCount': len(actual), 'checks': dict(checks), 'allVerified': True,
            'summaryVerification': {'allVerified': True}, 'csvVerification': csv_checks}


def verify_report(report, worlds, summary):
    counts = {
        'worldCount': 200, 'primaryProfileEvaluationCount': 4800,
        'referenceProfileEvaluationCount': 4800, 'armPairCount': 600,
        'profileNestingCheckCount': 4800, 'jointSetInclusionCheckCount': 7200,
        'truthContainmentCount': 4800, 'falseExclusionCount': 0,
        'baselineCandidateCount': sum(w['arms'][ARMS[0]]['occupancy']['candidateCount'] for w in worlds),
        'fullSupportCandidateCount': sum(w['arms'][ARMS[2]]['occupancy']['candidateCount'] for w in worlds),
        'invariantViolationCount': 0,
    }
    for field, value in counts.items():
        assert report['summary'][field] == value, (field, report['summary'][field], value)
    assert report['manifest']['protocolContentSha256'] == summary['protocolSha256']
    assert report['manifest']['baselineExperimentContentSha256'] == EXPERIMENT_SHA
    assert report['manifest']['seeds'] == list(range(200))
    profile_aggregates = {(a['armId'], a['profileId']): a for a in report['profileAggregates']}
    assert len(profile_aggregates) == len(report['profileAggregates']) == 24
    for (aid, pid), aggregate in profile_aggregates.items():
        rows = [w['arms'][aid]['profiles'][pid] for w in worlds]
        assert aggregate['worldCount'] == 200
        for actual, expected in [('candidateCount', 'count'), ('reductionVsNone', 'reductionCount'),
                                 ('reductionFraction', 'reductionFraction'), ('retainedFraction', 'retainedFraction'),
                                 ('candidateGenerationWork', 'work')]:
            verify_statistics(aggregate[actual], (r[expected] for r in rows))
        assert aggregate['zeroReductionWorldCount'] == sum(r['reductionCount'] == 0 for r in rows)
        assert aggregate['containsTruthCount'] == 200 and aggregate['falseExclusionCount'] == 0
    ca = indexed_pair(report['conditionalAggregates'], 'armId', 'comparisonId')
    assert set(ca) == {(aid, cid) for aid in ARMS for cid in ['all']+[row[0] for row in ADDITIONS]}
    for (aid, cid), aggregate in ca.items():
        rows = [(w['seed'], c) for w in worlds for key, c in w['arms'][aid]['conditionals'].items() if cid == 'all' or cid == key]
        assert aggregate['comparisonCount'] == len(rows)
        verify_statistics(aggregate['additionalReduction'], (c['count'] for _, c in rows))
        verify_statistics(aggregate['reductionFraction'], (c['fraction'] for _, c in rows))
        assert aggregate['zeroEffectCount'] == sum(c['count'] == 0 for _, c in rows)
        classes = indexed(aggregate['classificationCounts'], 'classification')
        assert set(classes) == set(CATEGORIES)
        for category, item in classes.items():
            selected = [(s, c) for s, c in rows if c['diagnostic']['classification'] == category]
            assert item['count'] == len(selected) and item['seeds'] == sorted({s for s, _ in selected})
    aa = indexed(report['armAggregates'], 'armId')
    assert set(aa) == set(ARMS)
    for aid, aggregate in aa.items():
        rows = [w['arms'][aid] for w in worlds]
        assert aggregate['worldCount'] == 200
        for field, expected in [('universeCount', 'candidateCount'), ('density', 'density'),
                                ('occupiedRegionCount', 'occupiedRegionCount'), ('occupiedTickCount', 'occupiedTickCount'),
                                ('unusedRegionFraction', 'unusedRegionFraction')]:
            verify_statistics(aggregate[field], (r['occupancy'][expected] for r in rows))
        verify_statistics(aggregate['minimumCount'], (r['minimum']['minimumCount'] for r in rows))
        verify_statistics(aggregate['minimumFieldCount'], (r['minimum']['minimumFieldCount'] for r in rows))
        assert aggregate['fullNecessaryWorldCount'] == sum(FULL in r['minimum']['inclusionMinimalProfiles'] for r in rows)
        frequencies = indexed(aggregate['minimumProfileFrequencies'], 'profileId')
        assert set(frequencies) == set(PROFILES)
        for pid, row in frequencies.items():
            assert row['atMinimumWorldCount'] == sum(pid in r['minimum']['profilesAtMinimum'] for r in rows)
            assert row['inclusionMinimalWorldCount'] == sum(pid in r['minimum']['inclusionMinimalProfiles'] for r in rows)
        families = indexed(aggregate['minimumFamilyFrequencies'], 'family')
        expected_families = Counter('|'.join(sorted(r['minimum']['inclusionMinimalProfiles'])) for r in rows)
        assert {k: v['worldCount'] for k, v in families.items()} == expected_families
        for family, row in families.items():
            assert row['seeds'] == [w['seed'] for w in worlds if '|'.join(sorted(w['arms'][aid]['minimum']['inclusionMinimalProfiles'])) == family]
    pa = indexed_pair(report['pairAggregates'], 'fromArmId', 'toArmId')
    assert set(pa) == set(ARM_PAIRS)
    for ordinal, (older, denser) in enumerate(ARM_PAIRS):
        aggregate = pa[older, denser]
        rows = [(w['seed'], w['pairs'][ordinal]) for w in worlds]
        expected_summary = summary['pairSummaries'][ordinal]
        assert aggregate['worldCount'] == 200
        assert aggregate['minimumCountChangedWorldCount'] == expected_summary['minimumCountChangedWorlds']
        assert aggregate['minimumFamilyChangedWorldCount'] == expected_summary['minimumFamilyChangedWorlds']
        assert aggregate['minimumFamilyAffectedSeeds'] == expected_summary['minimumFamilyChangedSeeds']
        assert aggregate['minimumFieldCountChangedWorldCount'] == sum(w['arms'][older]['minimum']['minimumFieldCount'] != w['arms'][denser]['minimum']['minimumFieldCount'] for w in worlds)
        assert aggregate['atMinimumProfilesChangedWorldCount'] == sum(bool(p['lostAtMinimum']) for _, p in rows)
        assert aggregate['equivalenceChangedCount'] == expected_summary['equivalenceLostCount']
        assert aggregate['equivalenceChangedWorldCount'] == len(expected_summary['equivalenceAffectedSeeds'])
        assert aggregate['becameIndistinguishableCount'] == 0
        profiles = indexed(aggregate['profiles'], 'profileId')
        assert set(profiles) == set(PROFILES)
        for pid, row in profiles.items():
            ps = [p['profiles'][pid] for _, p in rows]
            for field in ['countDelta', 'reductionDelta', 'reductionFractionDelta']:
                verify_statistics(row[field], (p[field] for p in ps))
            assert row['changedWorldCount'] == sum(p['countDelta'] != 0 for p in ps)
            assert row['zeroEffectWorldCount'] == sum(p['countDelta'] == 0 for p in ps)
        conditionals = indexed(aggregate['conditionals'], 'comparisonId')
        assert set(conditionals) == {'all'} | {r[0] for r in ADDITIONS}
        for cid, row in conditionals.items():
            cs = [(s, c) for s, p in rows for key, c in p['conditionals'].items() if cid == 'all' or cid == key]
            es = expected_summary['conditionals'][cid]
            assert row['comparisonCount'] == len(cs)
            verify_statistics(row['reductionDelta'], (c['countDelta'] for _, c in cs))
            verify_statistics(row['reductionFractionDelta'], (c['fractionDelta'] for _, c in cs))
            assert row['changedWorldCount'] == len({s for s, c in cs if c['countDelta'] != 0})
            for field in ['classificationChangedCount', 'classificationAffectedSeeds']:
                assert row[field] == es[field]
            assert row['newlyDetectableEffectCount'] == es['newlyDetectableCount']
            assert row['lostEffectCount'] == 0
            flags = [(w['arms'][older]['conditionals'][key]['diagnostic'], w['arms'][denser]['conditionals'][key]['diagnostic'])
                     for w in worlds for key in w['arms'][older]['conditionals'] if cid == 'all' or key == cid]
            assert row['topFlagChangedCount'] == sum(a['top'] != b['top'] for a, b in flags)
            assert row['implicationFlagChangedCount'] == 0
            transitions = indexed_pair(row['classificationTransitions'], 'fromClassification', 'toClassification')
            assert set(transitions) == {(a, b) for a in CATEGORIES for b in CATEGORIES}
            for expected in es['transitions']:
                transition = transitions[expected['fromClassification'], expected['toClassification']]
                assert transition['comparisonCount'] == expected['count'] and transition['seeds'] == expected['seeds']


def verify_csvs(actual_worlds, directory):
    """Audit all columns and row order against JSON already independently checked."""
    profiles, conditionals, crosswalks = [], [], []
    join = '|'.join
    for world in actual_worlds:
        seed = world['seed']
        arms = indexed(world['arms'], 'armId')
        occupancies = indexed(world['occupancy'], 'armId')
        for m in world['profileMetrics']:
            arm = arms[m['armId']]
            profile = indexed(arm['profiles'], 'profileId')[m['profileId']]
            occ = occupancies[m['armId']]
            profiles.append({
                'seed': seed, 'arm': m['armId'], 'profile': m['profileId'],
                'universe_count': occ['candidateCount'], 'density': occ['density'],
                'candidate_count': m['candidateCount'], 'original_member_count': m['originalMemberCount'],
                'introduced_member_count': m['introducedMemberCount'], 'reduction_vs_none': m['reductionVsNone'],
                'reduction_fraction': m['reductionFraction'], 'retained_fraction': m['retainedFraction'],
                'candidate_keys': join(profile['candidateKeys']), 'candidate_ids': join(profile['candidateIds']),
                'excluded_keys': join(m['excludedKeys']), 'contains_truth': m['containsTruth'],
                'false_exclusion_count': m['falseExclusionCount'], 'candidate_generation_work': m['candidateGenerationWork'],
                'work_rule_satisfied': m['workRuleSatisfied'], 'universe_id': arm['candidateUniverseId'],
                'composition_id': profile['compositionId'],
            })
        for m in world['conditionalMetrics']:
            conditional = indexed(arms[m['armId']]['conditionals'], 'comparisonId')[m['comparisonId']]
            diagnostic = conditional['diagnostic']
            conditionals.append({
                'seed': seed, 'arm': m['armId'], 'comparison': m['comparisonId'], 'added_axis': conditional['addedAxis'],
                'baseline_profile': conditional['baselineProfileId'], 'combined_profile': conditional['combinedProfileId'],
                'baseline_count': conditional['baselineCandidateCount'], 'combined_count': conditional['combinedCandidateCount'],
                'additional_reduction': m['additionalCandidateReduction'], 'reduction_fraction': m['reductionFraction'],
                'excluded_keys': join(m['excludedKeys']), 'classification': diagnostic['classification'],
                'top': diagnostic['addedPredicateIsTopForCandidateDomain'],
                'implied': diagnostic['addedPredicateImpliedByBaselinePredicates'],
                'implication_applicable': diagnostic['regionImplicationApplicable'],
                'implication_counterexample_regions': join(diagnostic['implicationCounterexampleRegionIds']),
            })
        bridge = indexed(world['intervalToPointCrosswalk'], 'candidateKey')
        public_full = indexed(world['support']['arms'][-1]['candidates'], 'candidateKey')
        for row in world['candidateCrosswalk']:
            c = row['candidate']
            key = row['candidateKey']
            original = bridge[key]['original'] if key in bridge else None
            ancestry = sorted({a['sourceIdentifier'] for p in public_full[key]['state']['provenance']
                               for a in p['interactionProvenance']})
            crosswalks.append({
                'seed': seed, 'key': key, 'introduction_arm': row['introductionArmId'], 'present_arms': join(row['presentArmIds']),
                'source': c['publicSourceIdentifier'], 'region': c['regionId'], 'start': c['timeWindow']['startTick'],
                'end': c['timeWindow']['endTick'], 'kind': c['temporalKind'], 'state_id': c['stateId'],
                'candidate_id': c['candidateId'], 'provenance_signature': c['provenanceSignature'], 'model_ancestry': join(ancestry),
                'original_interval_candidate_id': row['originalIntervalCandidateId'] or '',
                'original_interval_state_id': original['stateId'] if original else '',
                'original_interval_start': original['timeWindow']['startTick'] if original else '',
                'original_interval_end': original['timeWindow']['endTick'] if original else '',
                'original_interval_kind': original['temporalKind'] if original else '',
            })
    results = {}
    for suffix, expected in [('profiles', profiles), ('conditionals', conditionals), ('crosswalk', crosswalks)]:
        path = directory/f'candidate-density-{suffix}.csv'
        with path.open(newline='') as stream:
            reader = csv.DictReader(stream)
            assert reader.fieldnames == list(expected[0]), (suffix, 'header')
            rows = list(reader)
        assert len(rows) == len(expected), (suffix, 'row count')
        for ordinal, (actual, wanted) in enumerate(zip(rows, expected)):
            for field, value in wanted.items():
                if isinstance(value, bool):
                    assert actual[field] == str(value).lower(), (suffix, ordinal, field)
                elif isinstance(value, float):
                    close(float(actual[field]), value)
                else:
                    assert actual[field] == str(value), (suffix, ordinal, field)
        results[suffix] = {'rowCount': len(rows), 'sha256': digest(path.read_bytes()), 'allColumnsVerified': True}
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--baseline-runs', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--protocol', type=Path, default=REPO/'experiments/m1f19_nested_point_support_density/experiment.json')
    parser.add_argument('--baseline-experiment', type=Path,
                        default=REPO/'experiments/m1f16_common_world_field_combination/experiment.json')
    parser.add_argument('--actual-runs', type=Path)
    parser.add_argument('--point-reference-runs', type=Path,
                        help='Immutable F18 JSONL; required for actual-output verification')
    parser.add_argument('--expected-source-sha',
                        help='Full clean execution SHA; required with --actual-runs')
    args = parser.parse_args()
    if args.actual_runs and (args.point_reference_runs is None or args.expected_source_sha is None):
        parser.error('--actual-runs requires --point-reference-runs and --expected-source-sha')
    if args.expected_source_sha is not None and not re.fullmatch(r'[0-9a-f]{40}', args.expected_source_sha):
        parser.error('--expected-source-sha must be a full lowercase SHA')
    assert digest(args.baseline_experiment.read_bytes()) == EXPERIMENT_SHA, 'frozen F16 experiment changed'
    baseline_bytes = args.baseline_runs.read_bytes()
    assert digest(baseline_bytes) == BASELINE_SHA, 'immutable F16 baseline artifact changed'
    registered_protocol = subprocess.check_output(['git', 'show', PREREG+':experiments/m1f19_nested_point_support_density/experiment.json'], cwd=REPO)
    assert args.protocol.read_bytes() == registered_protocol, 'frozen protocol bytes changed'
    assert digest(registered_protocol) == PROTOCOL_SHA, 'unexpected registered protocol hash'
    assert subprocess.run(['git', 'merge-base', '--is-ancestor', PREREG, 'HEAD'], cwd=REPO).returncode == 0
    runs = [json.loads(line) for line in baseline_bytes.splitlines()]
    assert [r['seed'] for r in runs] == list(range(200))
    worlds, checks = [], Counter()
    for run in runs:
        public = public_input(run)
        expected = public_evaluation(public, checks)
        assert run['derivedLocationAllowedRegionIds'] == public['location']
        assert run['derivedParticipantAllowedRegionIds'] == public['participant']
        assert run['derivedHistoricalTimeWindow'] == {'startTick': public['timeLower'], 'endTick': public['timeUpper']}
        # All public arms and their comparisons exist before this truth access.
        truth_key = run['world']['truthCandidateKey']
        for aid in ARMS:
            for pid in PROFILES:
                assert truth_key in expected['arms'][aid]['profiles'][pid]['keys']
                checks['containmentChecks'] += 1
        expected['truthEvaluation'] = {'originalTruthCandidateKey': truth_key}
        worlds.append(expected)
    summary = {
        'schemaVersion': 1, 'preregistrationCommit': PREREG,
        'executionSourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=REPO, text=True).strip(),
        'executionDirtyWorkingTree': bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=REPO, text=True).strip()),
        'protocolSha256': digest(registered_protocol), 'originalArtifactSha256': digest(baseline_bytes),
        'basis': 'Independent Python: fixed public point grid, SHA ordering and raw L/P/T predicates; all three public arms before evaluation truth. Native hash implementation not reproduced.',
        'worldCount': len(worlds), 'primaryProfileEvaluations': len(worlds)*len(PROFILES)*len(ARMS),
        'independentChecks': dict(checks), **summarize(worlds),
    }
    if args.actual_runs:
        assert summary['executionSourceCommit'] == args.expected_source_sha, 'verifier and experiment source differ'
        assert summary['executionDirtyWorkingTree'] is False, 'final verification requires a clean source checkout'
        summary['actualVerification'] = verify_actual(worlds, runs, args.actual_runs, summary,
                                                     args.point_reference_runs, args.expected_source_sha)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output/'worlds.jsonl').write_text(''.join(json.dumps(w, separators=(',', ':'))+'\n' for w in worlds))
    (args.output/'summary.json').write_text(json.dumps(summary, indent=2)+'\n')
    print(json.dumps({
        'worldCount': len(worlds), 'checks': dict(checks),
        'profileMeans': {aid: {pid: a['profiles'][pid]['count']['mean'] for pid in PROFILES}
                         for aid, a in summary['armSummaries'].items()},
        'plateauCounts': {aid: a['plateauCounts'] for aid, a in summary['armSummaries'].items()},
        'minimumFamilies': {aid: a['minimumFamilyFrequencies'] for aid, a in summary['armSummaries'].items()},
        'pairs': [{k: v for k, v in p.items() if k not in ['profiles', 'conditionals', 'minimumFamilyChangedSeeds', 'equivalenceAffectedSeeds']}
                  | {'plateau': {k: v for k, v in p['conditionals']['all'].items() if k not in ['transitions', 'classificationAffectedSeeds']}}
                  for p in summary['pairSummaries']],
        'actualVerification': summary.get('actualVerification'),
    }, indent=2))


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must be enabled; omit Python -O.')
    main()
