"""Synthetic evidence-boundary tests; no simulator, model, or archived data needed."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

HELPER = Path(__file__).with_name('run_f19_research_validation.py')


def load_helper():
    spec = importlib.util.spec_from_file_location('f19_research_validation', HELPER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


validation = load_helper()


class ResearchValidationTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.before = Path(temporary.name) / 'before'
        self.after = Path(temporary.name) / 'after'
        self.row = b'{"seed":0,"scientificValue":7}\n'
        self.manifest = json.dumps({'sourceCommitSha': 'a' * 40, 'dirtyWorkingTree': False,
                                    'scientificValue': 7}, indent=2).encode() + b'\n'
        for root in (self.before, self.after):
            for campaign, names in validation.REFERENCE_FILES.items():
                directory = root / campaign
                directory.mkdir(parents=True)
                for name in names:
                    if name.endswith('-manifest.json'):
                        data = self.manifest
                    elif name.endswith('.json'):
                        data = b'{"scientificValue":7,"manifest":' + self.manifest.rstrip() + b'}\n'
                    elif name.endswith('.jsonl'):
                        data = self.row
                    else:
                        data = b'scientificValue\n7\n'
                    (directory / name).write_bytes(data)
        immutable = {key: validation.sha256(self.row) for key in validation.REFERENCE_ROW_HASHES}
        self.hash_patch = patch.object(validation, 'REFERENCE_ROW_HASHES', immutable)
        self.hash_patch.start()
        self.addCleanup(self.hash_patch.stop)

    def compare(self, before_sha='a' * 40, after_sha='a' * 40):
        return validation.compare_references(self.before, self.after, before_sha, after_sha)

    def create_f19_outputs(self):
        before, after = self.before.parent / 'f19-a', self.before.parent / 'f19-b'
        for root in (before, after):
            root.mkdir()
            for name in validation.F19_FILES:
                (root / name).write_bytes(b'fixed science\n')
        return before, after

    def test_import_does_not_launch_commands_or_validation(self):
        with patch('subprocess.run', side_effect=AssertionError('unexpected execution')), \
                patch('subprocess.check_output', side_effect=AssertionError('unexpected execution')), \
                patch('argparse.ArgumentParser.parse_args', side_effect=AssertionError('unexpected main')):
            self.assertTrue(callable(load_helper().compare_references))

    def test_only_execution_source_value_is_normalized_without_reformatting(self):
        after = self.manifest.replace(b'a' * 40, b'b' * 40)
        actual, ignored = validation.normalized_reference(after, 'b' * 40)
        expected = self.manifest.replace(b'a' * 40, b'EXECUTION_SOURCE')
        self.assertTrue(ignored)
        self.assertEqual(expected, actual)

    def test_embedded_manifest_is_normalized_without_reformatting(self):
        raw = b'{ "value":7, "manifest" : ' + self.manifest + b'}\n'
        normalized, ignored = validation.normalized_reference(raw, 'a' * 40)
        self.assertTrue(ignored)
        self.assertEqual(raw.replace(b'a' * 40, b'EXECUTION_SOURCE'), normalized)

    def test_json_without_execution_metadata_keeps_exact_bytes(self):
        raw = b'{ "value" : 7 }\n'
        self.assertEqual((raw, False), validation.normalized_reference(raw, 'a' * 40))

    def test_malformed_execution_metadata_is_rejected(self):
        for key, value in [('sourceCommitSha', 'short'), ('sourceCommitSha', 17),
                           ('dirtyWorkingTree', 'false'), ('dirtyWorkingTree', 0),
                           ('dirtyWorkingTree', None)]:
            with self.subTest(key=key, value=value):
                record = json.loads(self.manifest)
                record[key] = value
                with self.assertRaises(ValueError):
                    validation.normalized_reference(json.dumps(record).encode(), 'a' * 40)
        record = json.loads(self.manifest)
        del record['dirtyWorkingTree']
        with self.assertRaises(ValueError):
            validation.normalized_reference(json.dumps(record).encode(), 'a' * 40)

    def test_duplicate_or_nested_metadata_occurrences_are_rejected(self):
        for additional in [b'"sourceCommitSha":"' + b'b' * 40 + b'",',
                           b'"dirtyWorkingTree":true,',
                           b'"other":{"sourceCommitSha":"' + b'b' * 40 + b'"},']:
            with self.subTest(additional=additional):
                with self.assertRaises(ValueError):
                    validation.normalized_reference(b'{' + additional + self.manifest[1:], 'a' * 40)

    def test_all_18_artifacts_allow_only_metadata_changes(self):
        for path in self.after.rglob('*.json'):
            path.write_bytes(path.read_bytes().replace(b'a' * 40, b'b' * 40))
        comparisons = self.compare(after_sha='b' * 40)
        self.assertEqual(18, len(comparisons))
        self.assertTrue(all(item['equal'] for item in comparisons))
        changed = [item for item in comparisons if item['ignored_execution_metadata_only']]
        self.assertEqual(6, len(changed))
        self.assertTrue(all(item['baseline_sha256'] != item['current_sha256'] for item in changed))

    def test_wrong_source_or_dirty_reference_manifest_is_rejected(self):
        for root in (self.before, self.after):
            for changed in [self.manifest.replace(b'a' * 40, b'b' * 40),
                            self.manifest.replace(b'false', b'true')]:
                with self.subTest(root=root, changed=changed):
                    path = root / 'f16/common-world-field-combination-manifest.json'
                    path.write_bytes(changed)
                    with self.assertRaisesRegex(ValueError, 'expected clean source'):
                        self.compare()
                    path.write_bytes(self.manifest)

    def test_missing_manifest_cannot_hide_reference_provenance(self):
        for root in (self.before, self.after):
            (root / 'f16/common-world-field-combination-manifest.json').write_bytes(b'{"value":7}\n')
        with self.assertRaisesRegex(ValueError, 'manifest is missing'):
            self.compare()

    def test_scientific_value_drift_is_rejected(self):
        for name in ['candidate-interval-width.json', 'candidate-interval-width-profiles.csv']:
            with self.subTest(name=name):
                path = self.after / 'f18' / name
                original = path.read_bytes()
                path.write_bytes(original.replace(b'7', b'8'))
                with self.assertRaisesRegex(ValueError, 'scientific output drift'):
                    self.compare()
                path.write_bytes(original)

    def test_byte_changes_outside_metadata_are_not_hidden_by_json_reencoding(self):
        path = self.after / 'f18/candidate-interval-width.json'
        path.write_bytes(path.read_bytes() + b'\n')
        with self.assertRaisesRegex(ValueError, 'scientific output drift'):
            self.compare()

    def test_unknown_extra_artifact_is_rejected(self):
        (self.after / 'f18/unregistered.csv').write_bytes(b'7\n')
        with self.assertRaisesRegex(ValueError, 'Unexpected artifact set'):
            self.compare()

    def test_unknown_replacement_on_both_sides_is_rejected(self):
        for root in (self.before, self.after):
            (root / 'f18/candidate-interval-width-profiles.csv').rename(root / 'f18/unregistered.csv')
        with self.assertRaisesRegex(ValueError, 'Unexpected artifact set'):
            self.compare()

    def test_missing_artifact_is_rejected(self):
        (self.after / 'f17/plateau-domain-audit.csv').unlink()
        with self.assertRaisesRegex(ValueError, 'Unexpected artifact set'):
            self.compare()

    def test_directory_disguised_as_artifact_is_rejected(self):
        path = self.after / 'f17/plateau-domain-audit.csv'
        path.unlink()
        path.mkdir()
        with self.assertRaisesRegex(ValueError, 'Unexpected artifact set'):
            self.compare()

    def test_unregistered_campaign_is_rejected(self):
        (self.after / 'unregistered').mkdir()
        with self.assertRaisesRegex(ValueError, 'Unexpected reference campaign set'):
            self.compare()

    def test_immutable_rows_are_checked_on_each_side_even_when_outputs_match(self):
        for roots in [(self.before,), (self.after,), (self.before, self.after)]:
            for campaign, name in validation.REFERENCE_ROW_HASHES:
                with self.subTest(roots=roots, campaign=campaign):
                    for root in roots:
                        (root / campaign / name).write_bytes(self.row + b'\n')
                    with self.assertRaisesRegex(ValueError, 'immutable reference changed'):
                        self.compare()
                    for root in roots:
                        (root / campaign / name).write_bytes(self.row)

    def test_nonimmutable_reference_rows_still_require_exact_equality(self):
        path = self.after / 'f17/plateau-domain-audit-runs.jsonl'
        path.write_bytes(self.row.replace(b':7', b':8'))
        with self.assertRaisesRegex(ValueError, 'scientific output drift'):
            self.compare()

    def test_f19_requires_exact_six_registered_outputs_on_both_sides(self):
        before, after = self.create_f19_outputs()
        self.assertEqual(6, len(validation.compare_f19_repeats(before, after)))
        for root in (before, after):
            for directory in (False, True):
                with self.subTest(root=root, directory=directory):
                    unknown = root / 'unregistered'
                    if directory:
                        unknown.mkdir()
                    else:
                        unknown.write_bytes(b'unknown')
                    with self.assertRaisesRegex(ValueError, 'Unexpected artifact set'):
                        validation.compare_f19_repeats(before, after)
                    if directory:
                        unknown.rmdir()
                    else:
                        unknown.unlink()
        for root in (before, after):
            (root / validation.F19_FILES[0]).rename(root / 'unknown-replacement')
        with self.assertRaisesRegex(ValueError, 'Unexpected artifact set'):
            validation.compare_f19_repeats(before, after)

    def test_reference_and_f19_outputs_reject_registered_file_symlinks(self):
        before, after = self.create_f19_outputs()
        for target, link, compare in [
                (self.before / 'f17/plateau-domain-audit.csv',
                 self.after / 'f17/plateau-domain-audit.csv', self.compare),
                (before / validation.F19_FILES[0], after / validation.F19_FILES[0],
                 lambda: validation.compare_f19_repeats(before, after))]:
            with self.subTest(link=link):
                data = link.read_bytes()
                link.unlink()
                try:
                    link.symlink_to(target)
                except (OSError, NotImplementedError):
                    self.skipTest('File symlinks are unavailable')
                with self.assertRaisesRegex(ValueError, 'Unexpected artifact set'):
                    compare()
                link.unlink()
                link.write_bytes(data)

    def test_evidence_collector_rejects_file_and_directory_symlinks(self):
        before, after = self.create_f19_outputs()
        self.assertEqual(6, len(validation.collect_artifact_hashes(after)))
        for target in [before / validation.F19_FILES[0], before]:
            with self.subTest(target=target):
                link = after / 'outside-evidence'
                try:
                    link.symlink_to(target, target_is_directory=target.is_dir())
                except (OSError, NotImplementedError):
                    self.skipTest('File symlinks are unavailable')
                with self.assertRaisesRegex(ValueError, 'nonregular evidence entry'):
                    validation.collect_artifact_hashes(after)
                link.unlink()

    def test_complete_registered_primary_counts_are_accepted(self):
        self.assertIsNone(validation.validate_primary_counts(200, 4800))

    def test_partial_inflated_or_noninteger_primary_counts_are_rejected(self):
        for worlds, evaluations in [(199, 4776), (201, 4824), (200, 4799), (200, 9600),
                                     (200.0, 4800), (200, 4800.0), ('200', 4800),
                                     (True, 4800), (200, False)]:
            with self.subTest(worlds=worlds, evaluations=evaluations):
                with self.assertRaisesRegex(ValueError, 'all registered worlds and profiles'):
                    validation.validate_primary_counts(worlds, evaluations)


if __name__ == '__main__':
    unittest.main()
