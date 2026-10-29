#!/usr/bin/env python3
"""Independent standard-library reference and optional artifact verifier for F18.

Run with Python normally; assertions are intentional fail-fast protocol checks.
Do not use Python optimization (-O), which disables those checks.
"""

import argparse
import hashlib
import json
import subprocess
from collections import Counter
from itertools import combinations
from math import isclose
from pathlib import Path
from statistics import mean, median

REPO = Path(__file__).resolve().parents[1]
PREREG = '940d24c8a002564b3d42cf9943aee8fff7467cc2'
PROFILES = {
    'none': (),
    'location': ('location',),
    'time': ('time',),
    'participant': ('participant',),
    'location-time': ('location', 'time'),
    'location-participant': ('location', 'participant'),
    'time-participant': ('time', 'participant'),
    'location-time-participant': ('location', 'time', 'participant'),
}
ADDITIONS = [
    ('time-added-to-location', 'location', 'location-time', 'time', 'nonnegative'),
    ('participant-added-to-location', 'location', 'location-participant', 'participant', 'zero'),
    ('location-added-to-time', 'time', 'location-time', 'location', 'nonpositive'),
    ('participant-added-to-time', 'time', 'time-participant', 'participant', 'nonpositive'),
    ('location-added-to-participant', 'participant', 'location-participant', 'location', 'zero'),
    ('time-added-to-participant', 'participant', 'time-participant', 'time', 'nonnegative'),
    ('participant-added-to-location-time', 'location-time', 'location-time-participant', 'participant', 'nonpositive'),
    ('time-added-to-location-participant', 'location-participant', 'location-time-participant', 'time', 'nonnegative'),
    ('location-added-to-time-participant', 'time-participant', 'location-time-participant', 'location', 'nonpositive'),
]
INCLUSIONS = [
    ('location', 'none'), ('time', 'none'), ('participant', 'none'),
    ('location-time', 'location'), ('location-time', 'time'),
    ('location-participant', 'location'), ('location-participant', 'participant'),
    ('time-participant', 'time'), ('time-participant', 'participant'),
    ('location-time-participant', 'location-time'),
    ('location-time-participant', 'location-participant'),
    ('location-time-participant', 'time-participant'),
]
CATEGORIES = ['additionalCandidateReduction', 'saturation', 'observationToConstraintPlateau',
              'constraintOverlapPlateau', 'constraintToCandidatePlateau']
PROFILE_PAIRS = list(combinations(PROFILES, 2))


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical_digest(value):
    return digest(json.dumps(value, sort_keys=True, separators=(',', ':')).encode())


def stats(values):
    values = list(values)
    return {'mean': mean(values), 'median': median(values), 'minimum': min(values),
            'maximum': max(values), 'distribution': dict(sorted(Counter(values).items()))}


def public_input(run):
    world = run['world']
    candidates = [
        {'key': c['candidateKey'], 'source': c['publicSourceIdentifier'],
         'region': c['regionId'], 'start': c['startTick'], 'end': c['endTick']}
        for c in world['candidateStates']
    ]
    return {
        'seed': run['seed'], 'regions': sorted(world['regionUniverse']),
        'candidates': candidates, 'location': sorted(world['locationObservedRegionIds']),
        'participant': sorted(set().union(*(set(p['allowedRegionIds']) for p in world['participants']))),
        'timeLower': world['laterEventTick'] - world['durationMaximumTicks'],
        'timeUpper': world['laterEventTick'] - world['durationMinimumTicks'],
    }


def infer(public, point):
    location = set(public['location'])
    participant = set(public['participant'])
    lower, upper = public['timeLower'], public['timeUpper']
    sets = {}
    for pid, axes in PROFILES.items():
        sets[pid] = {
            c['key'] for c in public['candidates']
            if ('location' not in axes or c['region'] in location)
            and ('participant' not in axes or c['region'] in participant)
            and ('time' not in axes or (c['start'] <= upper and
                 (c['start'] if point else c['end']) >= lower))
        }
    return sets


def plateau(public, sets, baseline, combined, added, point):
    added_regions = set(public['location'] if added == 'location' else public['participant'])
    top = (all(c['start'] <= public['timeUpper'] and
               (c['start'] if point else c['end']) >= public['timeLower']
               for c in public['candidates']) if added == 'time' else
           set(public['regions']) <= added_regions)
    applicable = added != 'time' and bool(set(PROFILES[baseline]) & {'location', 'participant'})
    base_regions = set(public['regions'])
    for axis in ['location', 'participant']:
        if axis in PROFILES[baseline]:
            base_regions &= set(public[axis])
    witnesses = sorted(base_regions - added_regions) if applicable else []
    implied = applicable and not witnesses
    equal = sets[baseline] == sets[combined]
    category = (
        'additionalCandidateReduction' if not equal else
        'saturation' if len(sets[baseline]) == 1 else
        'observationToConstraintPlateau' if top else
        'constraintOverlapPlateau' if implied else
        'constraintToCandidatePlateau'
    )
    return {'top': top, 'implied': implied, 'regionImplicationApplicable': applicable,
            'implicationCounterexampleRegions': witnesses, 'classification': category}


def minima(sets):
    count = min(len(value) for value in sets.values())
    at_min = [pid for pid in PROFILES if len(sets[pid]) == count]
    minimal = [pid for pid in at_min if not any(
        set(PROFILES[other]) < set(PROFILES[pid]) for other in at_min)]
    assert all(sets[pid] == sets[at_min[0]] for pid in at_min)
    return {'minimumCount': count, 'profilesAtMinimum': at_min,
            'inclusionMinimalProfiles': minimal, 'minimumCandidateKeys': sorted(sets[at_min[0]]),
            'minimumProfilesReturnSameKeys': True}


def transition_matrix(entries):
    counter = Counter((item['originalPlateau']['classification'],
                       item['pointPlateau']['classification']) for item in entries)
    return [{'original': original, 'point': point, 'count': counter[original, point]}
            for original in CATEGORIES for point in CATEGORIES]



def parse_arguments():
    parser = argparse.ArgumentParser(
        description='Independently reconstruct F18 memberships from raw public F16 inputs.')
    parser.add_argument('--baseline-runs', type=Path, required=True,
                        help='F16 common-world-field-combination-runs.jsonl')
    parser.add_argument('--output', type=Path, required=True,
                        help='Directory for independent summary.json and worlds.jsonl')
    parser.add_argument('--protocol', type=Path,
                        default=REPO / 'experiments/m1f18_candidate_interval_width/experiment.json')
    parser.add_argument('--actual-runs', type=Path,
                        help='Optional C# F18 JSONL; also checks its sibling report JSON when present')
    return parser.parse_args()


def indexed(values, key):
    result = {value[key]: value for value in values}
    assert len(result) == len(values), ('duplicate identifier', key)
    return result


def verify_actual(expected_worlds, baseline_runs, actual_path, expected_summary):
    """Compare independently inferred keys and diagnostics, then audit native crosswalks.

    Point native hashes are not reimplemented: their coordinate/key bijection,
    retained provenance, collision freedom, and required equal/different identity
    relations are checked against the independently reconstructed memberships.
    """
    actual_bytes = actual_path.read_bytes()
    actual_rows = [json.loads(line) for line in actual_bytes.splitlines()]
    actual_by_seed = indexed(actual_rows, 'seed')
    baseline_by_seed = indexed(baseline_runs, 'seed')
    assert sorted(actual_by_seed) == list(range(200))
    checks = Counter()
    for expected in expected_worlds:
        seed = expected['seed']
        actual = actual_by_seed[seed]
        baseline = baseline_by_seed[seed]
        public = expected['publicInput']
        assert actual['originalWorld'] == baseline['world'], (seed, 'original world changed')
        assert actual['referenceMatches'] and actual['allInvariantsSatisfied']
        assert actual['originalWorldBeforeSignature'] == actual['originalWorldAfterSignature']
        assert actual['original']['fixedConstraintSignature'] == actual['point']['fixedConstraintSignature']
        public_by_key = indexed(public['candidates'], 'key')
        expected_profiles = indexed(expected['profiles'], 'profileId')
        baseline_profiles = indexed(baseline['profiles'], 'profileId')
        original_native_by_coordinates = {
            (c['timeWindow']['startTick'], c['timeWindow']['endTick'], c['regionId']): c
            for c in baseline_profiles['none']['candidates']
        }
        arm_candidates = {}
        for arm_name, membership, minimum_key in [
                ('original', 'originalKeys', 'originalMinimum'),
                ('point', 'pointKeys', 'pointMinimum')]:
            arm = actual[arm_name]
            candidates = indexed(arm['candidates'], 'candidateKey')
            arm_candidates[arm_name] = candidates
            assert set(candidates) == set(public_by_key)
            assert len({c['candidateId'] for c in candidates.values()}) == len(candidates)
            assert len({c['stateId'] for c in candidates.values()}) == len(candidates)
            native_to_key = {c['candidateId']: key for key, c in candidates.items()}
            for key, candidate in candidates.items():
                raw = public_by_key[key]
                assert candidate['regionId'] == raw['region']
                assert candidate['publicSourceIdentifier'] == raw['source']
                assert candidate['timeWindow'] == {
                    'startTick': raw['start'],
                    'endTick': raw['end'] if arm_name == 'original' else raw['start']}
                expected_kind = 'stateInterval' if arm_name == 'original' and raw['end'] > raw['start'] else 'eventPoint'
                assert candidate['temporalKind'] == expected_kind
                if arm_name == 'original':
                    original_native = original_native_by_coordinates[raw['start'], raw['end'], raw['region']]
                    assert candidate['candidateId'] == original_native['candidateId']
                    assert candidate['stateId'] == original_native['stateId']
                checks['nativeCandidateCoordinateChecks'] += 1
            profiles = indexed(arm['profiles'], 'profileId')
            assert set(profiles) == set(PROFILES)
            for pid, profile in profiles.items():
                wanted = expected_profiles[pid]
                assert profile['candidateKeys'] == wanted[membership], (seed, arm_name, pid, 'membership')
                assert len(profile['candidateIds']) == len(set(profile['candidateIds']))
                assert {native_to_key[nid] for nid in profile['candidateIds']} == set(wanted[membership])
                assert profile['candidateCount'] == len(wanted[membership])
                assert set(profile['activeAxes']) == set(PROFILES[pid])
                assert profile['candidateGenerationWork'] == wanted['originalCandidateGenerationWork']
                assert profile['compositionId'] == baseline_profiles[pid]['compositionId']
                assert profile['compositionAudit'] == baseline_profiles[pid]['compositionAudit']
                checks['profileMembershipAndWorkChecks'] += 1
            conditional = indexed(arm['conditionals'], 'comparisonId')
            for wanted in expected['conditionalComparisons']:
                found = conditional[wanted['comparisonId']]
                expected_diagnostic = wanted['originalPlateau' if arm_name == 'original' else 'pointPlateau']
                verify_diagnostic(found['diagnostic'], expected_diagnostic, public['regions'])
                assert found['additionalCandidateReduction'] == wanted[
                    'originalAdditionalReduction' if arm_name == 'original' else 'pointAdditionalReduction']
                checks['plateauDiagnosticChecks'] += 1
            equivalences = {(e['leftProfileId'], e['rightProfileId']): e for e in arm['profileEquivalences']}
            assert set(equivalences) == set(PROFILE_PAIRS)
            for wanted in expected['profileEquivalences']:
                found = equivalences[wanted['leftProfileId'], wanted['rightProfileId']]
                assert found['candidateKeySetsEqual'] == wanted['originalEqual' if arm_name == 'original' else 'pointEqual']
                checks['equivalenceChecks'] += 1
            wanted_minimum = expected[minimum_key]
            found_minimum = arm['minimum']
            assert found_minimum['minimumCandidateCount'] == wanted_minimum['minimumCount']
            assert found_minimum['profilesAtMinimumCount'] == wanted_minimum['profilesAtMinimum']
            assert found_minimum['inclusionMinimalProfileIds'] == wanted_minimum['inclusionMinimalProfiles']
            assert found_minimum['allMinimumProfilesReturnSameKeySet']
            inclusions = {(e['subsetProfileId'], e['supersetProfileId']): e for e in arm['setInclusions']}
            assert set(inclusions) == set(INCLUSIONS)
            assert all(e['candidateKeysAreSubset'] for e in inclusions.values())
            checks['minimumFamilyChecks'] += 1
        crosswalk = indexed(actual['crosswalk'], 'candidateKey')
        assert set(crosswalk) == set(public_by_key)
        for key, row in crosswalk.items():
            assert row['seed'] == seed
            original, point = arm_candidates['original'][key], arm_candidates['point'][key]
            assert row['original'] == original and row['point'] == point
            assert original['provenanceSignature'] == point['provenanceSignature']
            same_window = public_by_key[key]['start'] == public_by_key[key]['end']
            assert (original['stateId'] == point['stateId']) == same_window
            assert (original['candidateId'] == point['candidateId']) == same_window
            checks['nativeIdentityCrosswalkChecks'] += 1
        changes = indexed(actual['profileChanges'], 'profileId')
        assert set(changes) == set(PROFILES)
        for wanted in expected['profiles']:
            pid = wanted['profileId']
            found = changes[pid]
            assert found['originalCandidateKeys'] == wanted['originalKeys']
            assert found['pointCandidateKeys'] == wanted['pointKeys']
            assert found['lostKeys'] == wanted['lostKeys'] and found['newKeys'] == wanted['newKeys']
            assert found['originalCandidateCount'] == wanted['originalCount']
            assert found['pointCandidateCount'] == wanted['pointCount']
            assert found['pointMinusOriginalCount'] == -wanted['lostCount']
            assert found['expectedEquality'] == ('time' not in PROFILES[pid])
            assert found['keySetsEqual'] == wanted['sameKeys']
            assert found['pointKeysAreSubset'] and found['lostKeysMatchBoundaryRule']
            assert found['workAndCompositionUnchanged']
            assert found['originalContainsTruth'] and found['pointContainsTruth']
            witnesses = indexed(found['lostWitnesses'], 'candidateKey')
            assert set(witnesses) == set(wanted['lostKeys'])
            for expected_witness in wanted['lostKeyWitnesses']:
                witness = witnesses[expected_witness['key']]
                assert witness['originalStartTick'] == expected_witness['originalStart']
                assert witness['originalEndTick'] == expected_witness['originalEnd']
                assert witness['derivedTimeLowerBound'] == expected_witness['derivedLower']
                assert witness['endTouchesLowerBound'] == expected_witness['endEqualsLower']
                checks['lostKeyWitnessChecks'] += 1
        conditional_changes = indexed(actual['conditionalChanges'], 'comparisonId')
        assert set(conditional_changes) == {row[0] for row in ADDITIONS}
        for wanted in expected['conditionalComparisons']:
            found = conditional_changes[wanted['comparisonId']]
            assert found['originalAdditionalReduction'] == wanted['originalAdditionalReduction']
            assert found['pointAdditionalReduction'] == wanted['pointAdditionalReduction']
            assert found['pointMinusOriginalReduction'] == wanted['delta']
            assert found['signedDeltaInvariant']
            verify_diagnostic(found['originalDiagnostic'], wanted['originalPlateau'], public['regions'])
            verify_diagnostic(found['pointDiagnostic'], wanted['pointPlateau'], public['regions'])
            checks['conditionalDeltaChecks'] += 1
        minimum_change = actual['minimumChange']
        for field, expected_field in [
                ('minimumCountChanged', 'minimumCountChanged'),
                ('inclusionMinimalFamilyChanged', 'inclusionMinimalFamilyChanged'),
                ('gainedMinimumProfiles', 'gainedProfilesAtMinimum'),
                ('lostMinimumProfiles', 'lostProfilesAtMinimum'),
                ('gainedInclusionMinimalProfiles', 'gainedInclusionMinimalProfiles'),
                ('lostInclusionMinimalProfiles', 'lostInclusionMinimalProfiles')]:
            found_value = minimum_change[field]
            expected_value = expected[expected_field]
            context = (seed, 'minimumChange', field, found_value, expected_value)
            if isinstance(expected_value, list):
                # These fields are sets. The C# writer canonically sorts them
                # by ordinal identifier; the reference retains profile order.
                assert found_value == sorted(set(found_value)), (
                    seed, 'minimumChange', field, 'not unique ordinal order', found_value)
                assert set(found_value) == set(expected_value), context
            else:
                assert found_value == expected_value, context
        truth = actual['fixedTruth']
        truth_key = expected['truthEvaluation']['originalTruthCandidateKey']
        assert truth['originalTruthCandidateKey'] == truth_key
        assert truth['originalCandidateId'] == arm_candidates['original'][truth_key]['candidateId']
        assert truth['pointCandidateId'] == arm_candidates['point'][truth_key]['candidateId']
        assert truth['originalTruthWorldId'] == baseline['world']['truthWorldId']
        assert truth['originalTruthStartTick'] == baseline['world']['truthCandidateStartTick']
        assert truth['originalTruthEndTick'] == baseline['world']['truthCandidateEndTick']
        assert truth['originalTruthRegionId'] == baseline['world']['truthRegionId']
        assert truth['originalTruthParticipantId'] == baseline['world']['truthParticipantId']
        assert truth['originalEncounterTick'] == baseline['world']['encounterTick']
        assert truth['originalRealizedDurationTicks'] == baseline['world']['actualTruthDurationTicks']
        assert truth['resolvedAfterBothPublicArms']
        checks['fixedTruthMappingChecks'] += 1
    report_path = actual_path.with_name('candidate-interval-width.json')
    report_result = None
    if report_path.is_file():
        report_bytes = report_path.read_bytes()
        verify_report(json.loads(report_bytes), expected_worlds, expected_summary)
        report_result = {'reportSha256': digest(report_bytes), 'allVerified': True}
    return {'actualRowsSha256': digest(actual_bytes), 'worldCount': len(actual_rows),
            'checks': dict(checks), 'allVerified': True, 'summaryVerification': report_result}


def verify_statistics(actual, values):
    expected = stats(values)
    for field in ['mean', 'median']:
        assert isclose(actual[field], expected[field], rel_tol=0, abs_tol=1e-12), (
            'statistics', field, actual[field], expected[field])
    assert actual['minimum'] == expected['minimum'], ('statistics', 'minimum', actual, expected)
    assert actual['maximum'] == expected['maximum'], ('statistics', 'maximum', actual, expected)
    distribution = {row['value']: row['count'] for row in actual['distribution']}
    assert len(distribution) == len(actual['distribution'])
    assert distribution == expected['distribution'], ('statistics', 'distribution', distribution, expected)


def verify_report(actual, worlds, independent):
    """Verify published aggregate counts, distributions and full transition matrices."""
    summary = actual['summary']
    expected_counts = {
        'worldCount': len(worlds), 'primaryProfileEvaluationCount': len(worlds) * 16,
        'referenceProfileEvaluationCount': len(worlds) * 8,
        'nonTimeEqualityCheckCount': len(worlds) * 4, 'timeSubsetCheckCount': len(worlds) * 4,
        'jointSetInclusionCheckCount': len(worlds) * 24,
        'truthContainmentCount': len(worlds) * 16, 'falseExclusionCount': 0,
        'minimumCountChangedWorldCount': independent['minimumCountChangedWorlds'],
        'minimumFamilyChangedWorldCount': independent['inclusionMinimalFamilyChangedWorlds'],
        'originalCandidateCount': sum(len(w['publicInput']['candidates']) for w in worlds),
        'positiveWidthCandidateCount': sum(c['start'] != c['end'] for w in worlds
                                           for c in w['publicInput']['candidates']),
        'invariantViolationCount': 0,
    }
    for field, value in expected_counts.items():
        assert summary[field] == value, ('aggregate', field)
    manifest = actual['manifest']
    assert manifest['protocolContentSha256'] == independent['protocolSha256']
    assert manifest['baselineExperimentContentSha256'] == (
        'e717669c7178dcf8b278b831b6ee049a2aa8dae5a9c9e6366b8c32712649ca39')
    assert manifest['seeds'] == list(range(200))
    profiles = indexed(actual['profileAggregates'], 'profileId')
    assert set(profiles) == set(PROFILES)
    for pid, aggregate in profiles.items():
        values = [next(p for p in w['profiles'] if p['profileId'] == pid) for w in worlds]
        verify_statistics(aggregate['original'], (p['originalCount'] for p in values))
        verify_statistics(aggregate['point'], (p['pointCount'] for p in values))
        verify_statistics(aggregate['pointMinusOriginal'], (-p['lostCount'] for p in values))
        assert aggregate['worldCount'] == len(worlds)
        assert aggregate['changedWorldCount'] == sum(not p['sameKeys'] for p in values)
        assert aggregate['zeroEffectWorldCount'] == sum(p['sameKeys'] for p in values)
        assert aggregate['originalContainmentCount'] == len(worlds)
        assert aggregate['pointContainmentCount'] == len(worlds)
        assert aggregate['falseExclusionCount'] == 0
    conditionals = indexed(actual['conditionalAggregates'], 'comparisonId')
    assert set(conditionals) == {'all'} | {row[0] for row in ADDITIONS}
    for cid, aggregate in conditionals.items():
        values = [(w['seed'], c) for w in worlds for c in w['conditionalComparisons']
                  if cid == 'all' or c['comparisonId'] == cid]
        assert aggregate['comparisonCount'] == len(values)
        verify_statistics(aggregate['originalReduction'], (c['originalAdditionalReduction'] for _, c in values))
        verify_statistics(aggregate['pointReduction'], (c['pointAdditionalReduction'] for _, c in values))
        verify_statistics(aggregate['pointMinusOriginal'], (c['delta'] for _, c in values))
        assert aggregate['changedWorldCount'] == len({seed for seed, c in values if c['delta'] != 0})
        changed = [(seed, c) for seed, c in values
                   if c['originalPlateau']['classification'] != c['pointPlateau']['classification']]
        assert aggregate['classificationChangedCount'] == len(changed)
        assert aggregate['classificationAffectedSeeds'] == sorted({seed for seed, _ in changed})
        assert aggregate['topFlagChangedCount'] == sum(
            c['originalPlateau']['top'] != c['pointPlateau']['top'] for _, c in values)
        assert aggregate['implicationFlagChangedCount'] == sum(
            c['originalPlateau']['implied'] != c['pointPlateau']['implied'] for _, c in values)
        transitions = {(r['fromClassification'], r['toClassification']): r
                       for r in aggregate['classificationTransitions']}
        expected_pairs = {(before, after) for before in CATEGORIES for after in CATEGORIES}
        assert len(aggregate['classificationTransitions']) == len(expected_pairs)
        assert set(transitions) == expected_pairs
        for (before, after), transition in transitions.items():
            selected = [(seed, c) for seed, c in values
                        if c['originalPlateau']['classification'] == before
                        and c['pointPlateau']['classification'] == after]
            assert transition['comparisonCount'] == len(selected)
            assert transition['seeds'] == sorted({seed for seed, _ in selected})


def verify_diagnostic(actual, expected, regions):
    assert actual['domainRegionIds'] == regions
    assert actual['implicationDomainRegionIds'] == regions
    assert actual['addedPredicateIsTopForCandidateDomain'] == expected['top']
    assert actual['addedPredicateImpliedByBaselinePredicates'] == expected['implied']
    assert actual['regionImplicationApplicable'] == expected['regionImplicationApplicable']
    assert actual['implicationCounterexampleRegionIds'] == expected['implicationCounterexampleRegions']
    assert actual['classification'] == expected['classification']


def main():
    args = parse_arguments()
    OUT = args.output
    SOURCE = args.baseline_runs
    PROTOCOL = args.protocol
    OUT.mkdir(parents=True, exist_ok=True)
    start_sha = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=REPO, text=True).strip()
    start_dirty = bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=REPO, text=True).strip())
    assert subprocess.run(['git', 'merge-base', '--is-ancestor', PREREG, 'HEAD'], cwd=REPO).returncode == 0
    protocol = json.loads(PROTOCOL.read_text())
    assert protocol['transformRuleId'] == 'candidate-point-at-existing-start-v1'
    assert protocol['arms'] == ['original', 'point-at-start']
    runs = [json.loads(line) for line in SOURCE.read_text().splitlines()]
    assert [run['seed'] for run in runs] == list(range(200))

    worlds = []
    checks = Counter()
    for run in runs:
        public = public_input(run)
        lower, upper = public['timeLower'], public['timeUpper']
        assert lower <= upper
        assert set(public['location']) <= set(public['regions'])
        assert set(public['participant']) <= set(public['regions'])
        assert set(run['derivedLocationAllowedRegionIds']) == set(public['location'])
        assert set(run['derivedParticipantAllowedRegionIds']) == set(public['participant'])
        assert run['derivedHistoricalTimeWindow'] == {'startTick': lower, 'endTick': upper}
        assert len({c['key'] for c in public['candidates']}) == len(public['candidates'])
        assert len({(c['start'], c['region']) for c in public['candidates']}) == len(public['candidates'])
        raw_by_coordinates = {(c['start'], c['end'], c['region']): c for c in public['candidates']}
        original_none = next(p for p in run['profiles'] if p['profileId'] == 'none')
        native_to_key = {
            c['candidateId']: raw_by_coordinates[
                c['timeWindow']['startTick'], c['timeWindow']['endTick'], c['regionId']]['key']
            for c in original_none['candidates']
        }
        assert len(native_to_key) == len(public['candidates'])
        by_key = {c['key']: c for c in public['candidates']}
        original = infer(public, False)
        point = infer(public, True)
        # Both complete public inference results exist before evaluation truth is read.
        truth_key = run['world']['truthCandidateKey']
        profile_rows = []
        for pid, axes in PROFILES.items():
            reference = next(p for p in run['profiles'] if p['profileId'] == pid)
            assert set(reference['activeAxes']) == set(axes)
            assert original[pid] == {native_to_key[nid] for nid in reference['candidateIds']}
            checks['originalReferenceProfileSetChecks'] += 1
            lost = original[pid] - point[pid]
            added = point[pid] - original[pid]
            if 'time' in axes:
                assert point[pid] <= original[pid]
                assert lost == {key for key in original[pid] if by_key[key]['start'] < lower}
                checks['timeProfileSubsetChecks'] += 1
                checks['exactLostKeyMechanismChecks'] += 1
            else:
                assert original[pid] == point[pid]
                checks['nonTimeProfileEqualityChecks'] += 1
            assert not added
            assert truth_key in original[pid] and truth_key in point[pid]
            checks['containmentChecks'] += 2
            work = len(public['candidates']) * (1 + len(axes))
            assert reference['candidateGenerationWork'] == work
            witnesses = []
            for key in sorted(lost):
                c = by_key[key]
                assert c['start'] < lower <= c['end'] and c['start'] <= upper
                witnesses.append({'key': key, 'region': c['region'],
                    'originalStart': c['start'], 'originalEnd': c['end'], 'pointTick': c['start'],
                    'derivedLower': lower, 'derivedUpper': upper,
                    'endEqualsLower': c['end'] == lower})
            profile_rows.append({
                'profileId': pid, 'originalKeys': sorted(original[pid]), 'pointKeys': sorted(point[pid]),
                'originalCount': len(original[pid]), 'pointCount': len(point[pid]),
                'lostKeys': sorted(lost), 'newKeys': sorted(added), 'lostCount': len(lost),
                'lostKeyWitnesses': witnesses, 'sameKeys': original[pid] == point[pid],
                'pointIsSubsetOfOriginal': point[pid] <= original[pid],
                'originalContainsTruth': truth_key in original[pid], 'pointContainsTruth': truth_key in point[pid],
                'originalCandidateGenerationWork': work, 'pointCandidateGenerationWork': work,
            })
        inclusions = []
        for arm, sets in [('original', original), ('point-at-start', point)]:
            for subset, superset in INCLUSIONS:
                assert sets[subset] <= sets[superset]
                inclusions.append({'arm': arm, 'subsetProfile': subset, 'supersetProfile': superset,
                                   'setInclusion': True})
                checks['jointInclusionChecks'] += 1
        conditional = []
        for cid, baseline, combined, added_axis, sign in ADDITIONS:
            original_delta = len(original[baseline]) - len(original[combined])
            point_delta = len(point[baseline]) - len(point[combined])
            delta = point_delta - original_delta
            assert (delta >= 0 if sign == 'nonnegative' else delta <= 0 if sign == 'nonpositive' else delta == 0)
            checks['conditionalSignChecks'] += 1
            original_plateau = plateau(public, original, baseline, combined, added_axis, False)
            point_plateau = plateau(public, point, baseline, combined, added_axis, True)
            assert original_plateau['implied'] == point_plateau['implied']
            assert original_plateau['implicationCounterexampleRegions'] == point_plateau['implicationCounterexampleRegions']
            if added_axis == 'time':
                assert not (not original_plateau['top'] and point_plateau['top'])
            else:
                assert original_plateau['top'] == point_plateau['top']
            conditional.append({
                'comparisonId': cid, 'baselineProfileId': baseline, 'combinedProfileId': combined,
                'addedAxis': added_axis, 'originalAdditionalReduction': original_delta,
                'pointAdditionalReduction': point_delta, 'delta': delta,
                'requiredDeltaSign': sign, 'originalPlateau': original_plateau,
                'pointPlateau': point_plateau,
            })
        original_min, point_min = minima(original), minima(point)
        assert point_min['minimumCount'] <= original_min['minimumCount']
        original_family, point_family = set(original_min['inclusionMinimalProfiles']), set(point_min['inclusionMinimalProfiles'])
        equivalences = [
            {'leftProfileId': left, 'rightProfileId': right,
             'originalEqual': original[left] == original[right],
             'pointEqual': point[left] == point[right]}
            for left, right in PROFILE_PAIRS
        ]
        worlds.append({
            'seed': run['seed'], 'originalPublicWorldId': run['world']['publicWorldId'],
            'publicInput': public, 'publicInputSignature': canonical_digest(public),
            'truthEvaluation': {'originalTruthCandidateKey': truth_key},
            'profiles': profile_rows, 'jointInclusions': inclusions, 'conditionalComparisons': conditional,
            'profileEquivalences': equivalences, 'originalMinimum': original_min, 'pointMinimum': point_min,
            'minimumCountChanged': original_min['minimumCount'] != point_min['minimumCount'],
            'inclusionMinimalFamilyChanged': original_family != point_family,
            'gainedInclusionMinimalProfiles': [p for p in PROFILES if p in point_family-original_family],
            'lostInclusionMinimalProfiles': [p for p in PROFILES if p in original_family-point_family],
            'gainedProfilesAtMinimum': [p for p in PROFILES if p in set(point_min['profilesAtMinimum'])-set(original_min['profilesAtMinimum'])],
            'lostProfilesAtMinimum': [p for p in PROFILES if p in set(original_min['profilesAtMinimum'])-set(point_min['profilesAtMinimum'])],
        })

    profile_summaries = {}
    for pid in PROFILES:
        selected = [next(p for p in world['profiles'] if p['profileId'] == pid) for world in worlds]
        profile_summaries[pid] = {
            'originalCandidateCounts': stats(p['originalCount'] for p in selected),
            'pointCandidateCounts': stats(p['pointCount'] for p in selected),
            'withinWorldReduction': stats(p['lostCount'] for p in selected),
            'changedWorldCount': sum(not p['sameKeys'] for p in selected),
            'zeroEffectWorldCount': sum(p['sameKeys'] for p in selected),
            'lostMembershipCount': sum(p['lostCount'] for p in selected),
            'closedEndpointLossMembershipCount': sum(w['endEqualsLower'] for p in selected for w in p['lostKeyWitnesses']),
            'originalContainmentCount': sum(p['originalContainsTruth'] for p in selected),
            'pointContainmentCount': sum(p['pointContainsTruth'] for p in selected),
        }
    conditional_summaries = {}
    for cid, _, _, _, _ in ADDITIONS:
        selected = [next(c for c in world['conditionalComparisons'] if c['comparisonId'] == cid) for world in worlds]
        changed_seeds = [world['seed'] for world, c in zip(worlds, selected)
                         if c['originalPlateau']['classification'] != c['pointPlateau']['classification']]
        conditional_summaries[cid] = {
            'originalAdditionalReduction': stats(c['originalAdditionalReduction'] for c in selected),
            'pointAdditionalReduction': stats(c['pointAdditionalReduction'] for c in selected),
            'signedDelta': stats(c['delta'] for c in selected),
            'positiveDeltaWorlds': sum(c['delta'] > 0 for c in selected),
            'negativeDeltaWorlds': sum(c['delta'] < 0 for c in selected),
            'zeroDeltaWorlds': sum(c['delta'] == 0 for c in selected),
            'plateauClassificationChangedWorlds': len(changed_seeds), 'affectedSeeds': changed_seeds,
            'topFlagChangedWorlds': sum(c['originalPlateau']['top'] != c['pointPlateau']['top'] for c in selected),
            'implicationFlagChangedWorlds': sum(c['originalPlateau']['implied'] != c['pointPlateau']['implied'] for c in selected),
            'plateauTransitionMatrix': transition_matrix(selected),
        }
    all_conditionals = [c for world in worlds for c in world['conditionalComparisons']]
    minimum_summaries = {}
    for arm, field in [('original', 'originalMinimum'), ('point-at-start', 'pointMinimum')]:
        minimum_summaries[arm] = {
            'minimumCandidateCounts': stats(world[field]['minimumCount'] for world in worlds),
            'profilesAtMinimumFrequencies': {pid: sum(pid in world[field]['profilesAtMinimum'] for world in worlds) for pid in PROFILES},
            'inclusionMinimalProfileFrequencies': {pid: sum(pid in world[field]['inclusionMinimalProfiles'] for world in worlds) for pid in PROFILES},
            'inclusionMinimalFamilyFrequencies': dict(sorted(Counter('|'.join(world[field]['inclusionMinimalProfiles']) for world in worlds).items())),
        }
    summary = {
        'schemaVersion': 1, 'preregistrationCommit': PREREG,
        'executionSourceCommit': start_sha, 'executionDirtyWorkingTree': start_dirty,
        'protocolSha256': digest(PROTOCOL.read_bytes()), 'originalArtifactSha256': digest(SOURCE.read_bytes()),
        'basis': 'Independent Python public-predicate reference from immutable original F16 records; original native IDs mapped by original coordinates. Both public arm memberships computed before truth-key containment. Point native identity generation is not reproduced here.',
        'worldCount': len(worlds), 'primaryProfileEvaluations': len(worlds)*len(PROFILES)*2,
        'independentChecks': dict(checks), 'profileSummaries': profile_summaries,
        'conditionalSummaries': conditional_summaries,
        'plateauSummary': {
            'original': dict(Counter(c['originalPlateau']['classification'] for c in all_conditionals)),
            'point-at-start': dict(Counter(c['pointPlateau']['classification'] for c in all_conditionals)),
            'classificationChangedCount': sum(c['originalPlateau']['classification'] != c['pointPlateau']['classification'] for c in all_conditionals),
            'affectedSeedCount': sum(any(c['originalPlateau']['classification'] != c['pointPlateau']['classification'] for c in world['conditionalComparisons']) for world in worlds),
            'fullTransitionMatrix': transition_matrix(all_conditionals),
        },
        'minimumSummaries': minimum_summaries,
        'minimumCountChangedWorlds': sum(world['minimumCountChanged'] for world in worlds),
        'inclusionMinimalFamilyChangedWorlds': sum(world['inclusionMinimalFamilyChanged'] for world in worlds),
        'gainedInclusionMinimalProfileFrequencies': {pid: sum(pid in w['gainedInclusionMinimalProfiles'] for w in worlds) for pid in PROFILES},
        'lostInclusionMinimalProfileFrequencies': {pid: sum(pid in w['lostInclusionMinimalProfiles'] for w in worlds) for pid in PROFILES},
        'equivalenceSummaries': [
            {'leftProfileId': left, 'rightProfileId': right,
             'originalEqualWorlds': sum(next(e for e in w['profileEquivalences'] if e['leftProfileId']==left and e['rightProfileId']==right)['originalEqual'] for w in worlds),
             'pointEqualWorlds': sum(next(e for e in w['profileEquivalences'] if e['leftProfileId']==left and e['rightProfileId']==right)['pointEqual'] for w in worlds),
             'changedWorlds': sum(next(e for e in w['profileEquivalences'] if e['leftProfileId']==left and e['rightProfileId']==right)['originalEqual'] != next(e for e in w['profileEquivalences'] if e['leftProfileId']==left and e['rightProfileId']==right)['pointEqual'] for w in worlds)}
            for left, right in PROFILE_PAIRS],
    }
    if args.actual_runs is not None:
        summary['actualVerification'] = verify_actual(worlds, runs, args.actual_runs, summary)
    (OUT/'worlds.jsonl').write_text('\n'.join(json.dumps(world, separators=(',', ':')) for world in worlds)+'\n')
    (OUT/'summary.json').write_text(json.dumps(summary, indent=2)+'\n')
    print(json.dumps({
        'executionSourceCommit': start_sha, 'executionDirtyWorkingTree': start_dirty,
        'protocolSha256': summary['protocolSha256'], 'originalArtifactSha256': summary['originalArtifactSha256'],
        'worldCount': len(worlds), 'checks': dict(checks),
        'profileCounts': {pid: {'originalMean': p['originalCandidateCounts']['mean'], 'pointMean': p['pointCandidateCounts']['mean'],
            'changedWorlds': p['changedWorldCount'], 'losses': p['lostMembershipCount']} for pid, p in profile_summaries.items()},
        'plateauCounts': {k:v for k,v in summary['plateauSummary'].items() if k!='fullTransitionMatrix'},
        'minimumCountChangedWorlds': summary['minimumCountChangedWorlds'],
        'inclusionMinimalFamilyChangedWorlds': summary['inclusionMinimalFamilyChangedWorlds'],
        'pointInclusionMinimalFrequencies': minimum_summaries['point-at-start']['inclusionMinimalProfileFrequencies'],
    }, indent=2))


if __name__ == "__main__":
    if not __debug__:
        raise RuntimeError("Verification requires assertions enabled; omit Python -O.")
    main()
