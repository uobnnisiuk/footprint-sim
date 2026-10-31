#!/usr/bin/env python3
"""Independent exhaustive reference analysis for the preregistered F21 family.

This does not execute or certify the C# producer. GitHub C# tests independently
compare that producer with recursive path enumeration on the same frozen family.
"""
import itertools
import json


def endpoints(edges, node, tick, target):
    if tick == target:
        return {node}
    result = endpoints(edges, node, tick + 1, target)
    for origin, destination, duration, transit in edges:
        if origin != node:
            continue
        arrival = tick + duration
        if arrival > target:
            result.add(transit)
        else:
            result.update(endpoints(edges, destination, arrival, target))
    return result


def analyze():
    nodes = ("a", "b", "c")
    links = (("a", "b", "ab"), ("a", "c", "ac"), ("b", "c", "bc"))
    windows = [(s, e) for s in range(3) for e in range(s, 3)]
    queries = 0
    nontrivial = 0
    transit_queries = 0
    endpoint_mutant_misses = 0
    duration_mutant_misses = 0
    histogram = {}
    for durations in itertools.product(range(3), repeat=3):
        edges = [(a, b, d, tr) for (a, b, tr), d in zip(links, durations) if d]
        fast = [(a, b, 1, tr) for a, b, d, tr in edges]
        for node, (s, e), target in itertools.product(nodes, windows, range(3, 7)):
            expected = set().union(*(endpoints(edges, node, t, target) for t in range(s, e + 1)))
            last_only = endpoints(edges, node, e, target)
            instant_travel = set().union(*(endpoints(fast, node, t, target) for t in range(s, e + 1)))
            queries += 1
            nontrivial += 0 < len(expected) < 6
            transit_queries += bool(expected.intersection({"ab", "ac", "bc"}))
            endpoint_mutant_misses += bool(expected - last_only)
            duration_mutant_misses += bool(expected - instant_travel)
            histogram[str(len(expected))] = histogram.get(str(len(expected)), 0) + 1
    witness_edges = [("a", "b", 2, "ab")]
    witness = sorted(endpoints(witness_edges, "a", 0, 2) | endpoints(witness_edges, "a", 1, 2))
    mutant = sorted(endpoints(witness_edges, "a", 1, 2))
    assert queries == 1944
    assert witness == ["a", "ab", "b"] and mutant == ["a", "ab"]
    assert endpoints(witness_edges, "a", 0, 1) == {"a", "ab"}
    assert endpoints(witness_edges, "c", 0, 6) == {"c"}
    return {
        "protocol": "f21-public-transition-coverage-v1",
        "kind": "independent-reference-analysis-not-production-execution",
        "graphs": 27,
        "queries": queries,
        "nonempty_proper_subset_queries": nontrivial,
        "queries_including_transit": transit_queries,
        "endpoint_only_mutant_missed_queries": endpoint_mutant_misses,
        "duration_one_mutant_missed_queries": duration_mutant_misses,
        "region_count_histogram": histogram,
        "endpoint_witness": {"full_window": witness, "end_only": mutant},
    }


if __name__ == "__main__":
    print(json.dumps(analyze(), sort_keys=True, indent=2))
