import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { cellDetail, matrixView, statuses } from '../docs/assets/js/schema-coverage.js';

const report = JSON.parse(readFileSync(new URL('./fixtures/schema-coverage.v1.json', import.meta.url), 'utf8'));
const [sqlServer, postgres, hive] = report.providers;
const [arrays, defaults] = report.features;

function view(filters = {}) {
    return matrixView(report, {
        databases: new Set(report.providers.map(provider => provider.id)),
        variants: new Map(),
        group: '',
        operation: '',
        ...filters
    });
}

test('both operations and every provider in the snapshot are available', () => {
    const result = view();
    assert.deepEqual(result.operations, ['inspect', 'generate']);
    assert.deepEqual(result.columns.map(column => column.provider), report.providers);
    assert.deepEqual(result.features, report.features);
});

test('database, feature group and operation filters can be combined', () => {
    const result = view({ databases: new Set(['postgresql']), group: 'Structured types', operation: 'inspect' });
    assert.deepEqual(result.columns.map(column => column.provider), [postgres]);
    assert.deepEqual(result.features, [arrays]);
    assert.deepEqual(result.operations, ['inspect']);
    assert.deepEqual(view({ databases: new Set() }).columns, []);
});

test('variant selection switches the test result for that database', () => {
    const original = view().columns[0].target;
    const selected = view({ variants: new Map([['sqlserver', 'SqlServer-2025']]) }).columns[0].target;
    assert.equal(original.coverage['literal-default'].inspect.status, 'supported');
    assert.equal(selected.coverage['literal-default'].inspect.status, 'failed');
    assert.equal(cellDetail(report, defaults, sqlServer, selected, 'inspect').variant, 'SQL Server 2025');
});

test('all result states have distinct indicators and accessible descriptions', () => {
    assert.equal(new Set(Object.values(statuses).map(([mark]) => mark)).size, 5);
    const found = new Set(report.providers.flatMap(provider => provider.targets.flatMap(target =>
        Object.values(target.coverage).flatMap(cells => Object.values(cells).map(cell => cell.status)))));
    assert.deepEqual(found, new Set(Object.keys(statuses)));
    assert.equal(cellDetail(report, arrays, hive, hive.targets[0], 'inspect').result, 'Inspect · Unverified');
    assert.equal(cellDetail(report, arrays, sqlServer, sqlServer.targets[0], 'generate').result,
        'Generate DDL · Verified unsupported');
});

test('cell details contain the actual caveat, variant, artifact and run link', () => {
    const detail = cellDetail(report, arrays, postgres, postgres.targets[0], 'inspect');
    assert.deepEqual(detail, {
        title: 'Native array or list · PostgreSQL',
        result: 'Inspect · Verified with limits',
        variant: 'PostgreSQL 17',
        evidence: [{ test: 'NativeArrayRoundTrips', result: 'Verified with limits · Passed', note: 'Requires rank evidence.' }],
        artifact: 'schema-qa-PostgreSql',
        runUrl: report.runUrl
    });
});
