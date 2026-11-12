import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { authenticationMethods, buildConnectionUrl, connectionForm } from '../docs/assets/js/connection-url.js';

const catalog = JSON.parse(readFileSync(new URL('../docs/schemas/connection-url-builder/v2/catalog.json', import.meta.url), 'utf8'));
const routes = catalog.databases.flatMap(database => database.routes);

function form(routeId, { location, auth, tls } = {}) {
    const route = routes.find(item => item.id === routeId);
    const locationId = location ?? route.locations[0];
    const methods = authenticationMethods(catalog, route, locationId);
    return connectionForm(catalog, route, locationId, auth ? methods.find(item => item.id === auth) : methods[0], tls);
}

test('catalog references resolve for every route and endpoint', () => {
    assert.equal(new Set(catalog.databases.map(database => database.id)).size, catalog.databases.length);
    assert.equal(new Set(routes.map(route => route.id)).size, routes.length);
    for (const route of routes) {
        for (const item of route.auth) {
            const id = typeof item === 'string' ? item : item.id;
            assert.ok(Object.hasOwn(catalog.authentication, id), id);
            for (const endpoint of [...(item.locations ?? []), ...Object.keys(item.queryByLocation ?? {})]) {
                assert.ok(route.locations.includes(endpoint), endpoint);
            }
        }
        for (const endpoint of Object.keys(route.schemeByLocation ?? {})) {
            assert.ok(route.locations.includes(endpoint), endpoint);
        }
        for (const locationId of route.locations) {
            assert.ok(Object.hasOwn(catalog.locations, locationId), locationId);
            const location = catalog.locations[locationId];
            if (route.locations.length > 1) assert.ok(location.label, locationId);
            const pathFields = (location.path ?? []).flatMap(part => typeof part === 'string' ? [part] :
                [part.when, part.field, part.split, part.file, part.relativeFile].filter(Boolean));
            const dependentFields = Object.entries(location.requires ?? {}).flat();
            for (const id of [...pathFields, ...dependentFields, ...(location.exclusive ?? []).flat()]) {
                assert.ok(Object.hasOwn(location.fields ?? {}, id), route.id + '/' + id);
            }
            const methods = authenticationMethods(catalog, route, locationId);
            assert.ok(methods.length, route.id + '/' + locationId);
            for (const auth of methods) {
                const policyId = auth.tlsOverride ?? route.tls ?? 'driverDefault';
                assert.ok(Object.hasOwn(catalog.tlsPolicies, policyId), policyId);
                const policy = catalog.tlsPolicies[policyId];
                if (policy.default) assert.ok(policy.modes.some(mode => mode.id === policy.default), policyId);
                assert.equal(new Set(policy.modes.map(mode => mode.id)).size, policy.modes.length);
                const resolved = connectionForm(catalog, route, locationId, auth);
                assert.ok(resolved.tls, route.id + '/' + auth.id);
                for (const field of Object.values(resolved.fields)) {
                    assert.ok(Object.hasOwn(catalog.layouts, field.layout), field.layout);
                    assert.ok(field.label, route.id + '/' + field.layout);
                }
                for (const mode of Object.keys(route.schemeByLocation?.[locationId] ?? {})) {
                    assert.ok(policy.modes.some(item => item.id === mode), route.id + '/' + mode);
                }
            }
        }
    }
});

test('PostgreSQL encodes URI components and keeps implicit ports out of the URL', () => {
    const connection = form('npgsql');
    assert.equal(connection.fields.port.default, 5432);
    assert.equal(buildConnectionUrl(connection, {
        host: '[::1]', database: 'analytics/data', username: 'qa+analyst', searchPath: 'team & reporting'
    }), 'postgres://qa%2Banalyst@[::1]/analytics%2Fdata?SSL%20Mode=VerifyFull&GSS%20Encryption%20Mode=Disable&Search%20Path=team%20%26%20reporting');
    assert.throws(() => buildConnectionUrl(connection, { host: 'host.example', database: 'db', username: 'qa', port: 65536 }),
        /1 to 65535/);
    assert.throws(() => buildConnectionUrl(connection, { host: '[::1]:443#[x]', database: 'db', username: 'qa' }),
        /valid hostname/);
});

test('MySQL ODBC writes a port as a driver keyword', () => {
    assert.equal(buildConnectionUrl(form('mysql-odbc'), {
        host: 'mysql.example', database: 'db', username: 'qa', port: '03308'
    }), 'odbc+mysql://qa@mysql.example/db?PORT=3308');
});

test('native Trino emits the selected HTTP or HTTPS mode and requires TLS for passwords', () => {
    const values = { host: 'trino.example', catalog: 'hive', schema: 'reporting', username: 'qa' };
    assert.equal(buildConnectionUrl(form('nreco'), values), 'trino://qa@trino.example/hive/reporting?UseSsl=true');
    assert.equal(buildConnectionUrl(form('nreco', { tls: 'default' }), values),
        'trino://qa@trino.example/hive/reporting?UseSsl=false');
    assert.equal(buildConnectionUrl(form('nreco', { auth: 'password', tls: 'default' }), values),
        'trino://qa@trino.example/hive/reporting?UseSsl=true');
});

test('Thrift endpoints change schemes and map HTTP paths separately from schema', () => {
    const values = { host: 'spark.example', httpPath: '/gateway/spark', schema: 'analytics' };
    const http = form('spark-odbc', { location: 'thriftHttp', tls: 'on' });
    assert.equal(http.transport, 'HTTPS');
    assert.equal(http.fields.port.default, 10001);
    assert.equal(buildConnectionUrl(http, values),
        'odbc+spark+https://spark.example/gateway/spark/schema/analytics?AuthMech=0');
    assert.throws(() => buildConnectionUrl(http, { ...values, httpPath: '/gateway/schema/analytics' }), /does not end/);
    assert.equal(buildConnectionUrl(form('hive-odbc', { auth: 'thriftPassword', tls: 'on' }), { ...values, username: 'qa' }),
        'odbc+hive+tcps://qa@spark.example/schema/analytics?AuthMech=3&sasl=1');
});

test('Databricks emits warehouse and cluster shortcuts with compatible qualifiers', () => {
    const values = { host: 'workspace.example', warehouseId: 'abc123', catalog: 'main', schema: 'reporting' };
    const warehouse = form('databricks-odbc', { auth: 'databricksPat' });
    assert.equal(buildConnectionUrl(warehouse, values),
        'odbc+databricks://token@workspace.example/warehouse/abc123/main/reporting?AuthMech=3');
    assert.throws(() => buildConnectionUrl(warehouse, { ...values, catalog: '' }), /requires catalog/);
    assert.throws(() => buildConnectionUrl(warehouse, { ...values, warehouseId: 'not an id' }), /warehouse id/);
    assert.equal(buildConnectionUrl(form('databricks-odbc', { location: 'cluster' }), {
        host: 'workspace.example', workspaceId: '123', clusterId: 'example-012'
    }), 'odbc+databricks://workspace.example/cluster/123/example-012?AuthMech=11&Auth_Flow=1');
});

test('file, memory and local-port profiles use their own URL shapes', () => {
    assert.equal(buildConnectionUrl(form('sqlite-native'), { file: 'C:\\Data\\example file.db' }),
        'sqlite:///C:/Data/example%20file.db');
    assert.equal(buildConnectionUrl(form('sqlite-native'), { file: '/var/data/example.db' }),
        'sqlite:////var/data/example.db');
    assert.equal(buildConnectionUrl(form('duckdb-adbc', { location: 'memory' }), {}), 'adbc+duckdb://');
    assert.equal(buildConnectionUrl(form('excel-ace'), { file: 'Reports.xlsm' }), 'oledb+xlsm:///Reports.xlsm');
    assert.throws(() => buildConnectionUrl(form('excel-ace'), { file: 'xlsx' }), /file ending in/);
    assert.equal(buildConnectionUrl(form('pbi-desktop-adomd', { location: 'powerBiPort' }), { port: 51234 }),
        'pbidesktop://localhost:51234');
    assert.equal(buildConnectionUrl(form('firebird-native'), {
        host: 'fb.example', serverFile: '/srv/database.fdb', username: 'qa'
    }), 'firebird://qa@fb.example/%2Fsrv%2Fdatabase.fdb');
});

test('runtime certificate authentication leaves TLS and credential material to DubUrl', () => {
    const connection = form('npgsql', { auth: 'certificate' });
    assert.deepEqual(connection.tls.cli, ['--tls']);
    assert.equal(buildConnectionUrl(connection, { host: 'postgres.example', database: 'db', username: 'qa' }),
        'postgres://qa@postgres.example/db');
});
