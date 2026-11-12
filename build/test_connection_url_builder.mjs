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

test('driver profile IDs are unique', () => {
    assert.equal(new Set(catalog.databases.map(database => database.id)).size, catalog.databases.length);
    assert.equal(new Set(routes.map(route => route.id)).size, routes.length);
});

test('driver profiles reference known authentication methods', () => {
    for (const route of routes) {
        for (const item of route.auth) {
            const id = typeof item === 'string' ? item : item.id;
            assert.ok(Object.hasOwn(catalog.authentication, id), id);
            for (const endpoint of [...(item.locations ?? []), ...Object.keys(item.queryByLocation ?? {})]) {
                assert.ok(route.locations.includes(endpoint), endpoint);
            }
        }
    }
});

test('driver profiles reference known endpoints', () => {
    for (const route of routes) {
        for (const endpoint of Object.keys(route.schemeByLocation ?? {})) {
            assert.ok(route.locations.includes(endpoint), endpoint);
        }
        for (const locationId of route.locations) {
            assert.ok(Object.hasOwn(catalog.locations, locationId), locationId);
            if (route.locations.length > 1) assert.ok(catalog.locations[locationId].label, locationId);
        }
    }
});

test('endpoint rules reference declared fields', () => {
    for (const [locationId, location] of Object.entries(catalog.locations)) {
        const pathFields = (location.path ?? []).flatMap(part => typeof part === 'string' ? [part] :
            [part.when, part.field, part.split, part.file, part.relativeFile, ...(part.fallbackWhen ?? [])].filter(Boolean));
        const dependentFields = Object.entries(location.requires ?? {}).flat();
        const portExceptions = location.fields?.port?.defaultUnless ?? [];
        for (const id of [...pathFields, ...dependentFields, ...portExceptions, ...(location.exclusive ?? []).flat()]) {
            assert.ok(Object.hasOwn(location.fields ?? {}, id), locationId + '/' + id);
        }
    }
});

test('TLS policies have unique modes and valid defaults', () => {
    for (const [policyId, policy] of Object.entries(catalog.tlsPolicies)) {
        const ids = new Set(policy.modes.map(mode => mode.id));
        assert.equal(ids.size, policy.modes.length, policyId);
        if (policy.default) assert.ok(ids.has(policy.default), policyId);
    }
});

test('initial TLS and authentication choices leave optional query settings implicit', () => {
    for (const route of routes) {
        for (const locationId of route.locations) {
            const [auth] = authenticationMethods(catalog, route, locationId);
            const { tls } = connectionForm(catalog, route, locationId, auth);
            assert.deepEqual({ ...tls.query, ...auth.query, ...auth.queryByLocation?.[locationId] }, {},
                route.id + '/' + locationId);
        }
    }
});

test('each endpoint and authentication choice resolves its TLS modes and field layouts', () => {
    for (const route of routes) {
        for (const locationId of route.locations) {
            const methods = authenticationMethods(catalog, route, locationId);
            assert.ok(methods.length, route.id + '/' + locationId);
            for (const auth of methods) {
                const policyId = auth.tlsOverride ?? route.tls ?? 'driverDefault';
                assert.ok(Object.hasOwn(catalog.tlsPolicies, policyId), policyId);
                const policy = catalog.tlsPolicies[policyId];
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
    const values = { host: '[::1]', database: 'analytics/data', username: 'qa+analyst', searchPath: 'team & reporting' };
    assert.equal(buildConnectionUrl(connection, { ...values, port: '05432' }),
        'postgres://qa%2Banalyst@[::1]/analytics%2Fdata?Search%20Path=team%20%26%20reporting');
    assert.equal(buildConnectionUrl(form('npgsql', { tls: 'verified' }), values),
        'postgres://qa%2Banalyst@[::1]/analytics%2Fdata?SSL%20Mode=VerifyFull&GSS%20Encryption%20Mode=Disable&Search%20Path=team%20%26%20reporting');
    assert.throws(() => buildConnectionUrl(connection, { host: 'host.example', database: 'db', username: 'qa', port: 65536 }),
        /1 to 65535/);
    assert.throws(() => buildConnectionUrl(connection, { host: '[::1]:443#[x]', database: 'db', username: 'qa' }),
        /valid hostname/);
});

test('SqlClient omits default TLS settings and emits an explicit Strict override', () => {
    const values = { host: 'sql.example', database: 'reporting', username: 'analyst' };
    assert.equal(buildConnectionUrl(form('sqlclient'), values), 'mssql://analyst@sql.example/reporting');
    assert.equal(buildConnectionUrl(form('sqlclient', { auth: 'integrated' }), values), 'mssql://sql.example/reporting');
    assert.equal(buildConnectionUrl(form('sqlclient', { tls: 'strict' }), values),
        'mssql://analyst@sql.example/reporting?Encrypt=Strict');
    assert.equal(buildConnectionUrl(form('sqlclient'), { ...values, port: 1433 }),
        'mssql://analyst@sql.example/reporting');
    assert.equal(buildConnectionUrl(form('sqlclient'), { ...values, instance: 'reporting', port: 1433 }),
        'mssql://analyst@sql.example:1433/reporting/reporting');
});

test('ODBC keeps an explicit driver selection and only writes nondefault settings', () => {
    const values = { host: 'db.example', database: 'db', username: 'qa' };
    assert.equal(buildConnectionUrl(form('msodbc18'), { ...values, port: 1433 }),
        'odbc+mssql://qa@db.example/db?Driver=ODBC%20Driver%2018%20for%20SQL%20Server');
    assert.equal(buildConnectionUrl(form('mysql-odbc'), { ...values, port: '3306' }), 'odbc+mysql://qa@db.example/db');
    assert.equal(buildConnectionUrl(form('mysql-odbc'), { ...values, port: '03308' }),
        'odbc+mysql://qa@db.example/db?PORT=3308');
    assert.equal(buildConnectionUrl(form('mysql-odbc', { tls: 'verified' }), values),
        'odbc+mysql://qa@db.example/db?SSLMODE=VERIFY_IDENTITY');
});

test('Trino uses the driver default and emits a TLS override when needed', () => {
    const values = { host: 'trino.example', catalog: 'hive', schema: 'reporting', username: 'qa' };
    assert.equal(buildConnectionUrl(form('nreco'), values), 'trino://qa@trino.example/hive/reporting');
    assert.equal(buildConnectionUrl(form('nreco', { tls: 'verified' }), values),
        'trino://qa@trino.example/hive/reporting?UseSsl=true');
    assert.equal(buildConnectionUrl(form('nreco', { auth: 'password', tls: 'default' }), values),
        'trino://qa@trino.example/hive/reporting?UseSsl=true');
    assert.equal(buildConnectionUrl(form('simba-trino', { auth: 'integrated' }), values),
        'odbc+trino://trino.example/hive/reporting');
});

test('Thrift endpoints change schemes and map HTTP paths separately from schema', () => {
    const values = { host: 'spark.example', httpPath: '/gateway/spark', schema: 'analytics' };
    const http = form('spark-odbc', { location: 'thriftHttp', tls: 'on' });
    assert.equal(http.transport, 'HTTPS');
    assert.equal(http.fields.port.default, 10001);
    assert.equal(buildConnectionUrl(http, values),
        'odbc+spark+https://spark.example/gateway/spark/schema/analytics');
    assert.equal(buildConnectionUrl(http, { ...values, httpPath: '/cliservice', port: 10001 }),
        'odbc+spark+https://spark.example/schema/analytics');
    assert.throws(() => buildConnectionUrl(http, { ...values, httpPath: '/gateway/schema/analytics' }), /does not end/);
    assert.equal(buildConnectionUrl(form('hive-odbc', { auth: 'thriftPassword', tls: 'on' }), { ...values, username: 'qa' }),
        'odbc+hive+tcps://qa@spark.example/schema/analytics?AuthMech=3');
    assert.equal(buildConnectionUrl(form('hive-odbc', { auth: 'thriftNone' }), values),
        'odbc+hive+tcp://spark.example/schema/analytics?AuthMech=0&sasl=0');
    assert.equal(buildConnectionUrl(form('hive-odbc', { auth: 'thriftUsername' }), { ...values, username: 'qa' }),
        'odbc+hive+tcp://qa@spark.example/schema/analytics');
});

test('Databricks emits warehouse and cluster shortcuts with compatible qualifiers', () => {
    const values = { host: 'workspace.example', warehouseId: 'abc123', catalog: 'main', schema: 'reporting' };
    const warehouse = form('databricks-odbc', { auth: 'databricksPat' });
    assert.equal(buildConnectionUrl(warehouse, values),
        'odbc+databricks://token@workspace.example/warehouse/abc123/main/reporting?AuthMech=3');
    assert.equal(buildConnectionUrl(form('databricks-odbc'), values),
        'odbc+databricks://workspace.example/warehouse/abc123/main/reporting');
    assert.equal(buildConnectionUrl(form('databricks-odbc', { auth: 'databricksOAuthToken' }), values),
        'odbc+databricks://workspace.example/warehouse/abc123/main/reporting?AuthMech=11');
    assert.throws(() => buildConnectionUrl(warehouse, { ...values, catalog: '' }), /requires catalog/);
    assert.throws(() => buildConnectionUrl(warehouse, { ...values, warehouseId: 'not an id' }), /warehouse id/);
    assert.equal(buildConnectionUrl(form('databricks-odbc', { location: 'cluster', auth: 'databricksOAuthM2M' }), {
        host: 'workspace.example', workspaceId: '123', clusterId: 'example-012'
    }), 'odbc+databricks://workspace.example/cluster/123/example-012?AuthMech=11&Auth_Flow=1');
});

test('Power BI and SSAS omit optional default path parts and keep required markers', () => {
    assert.equal(buildConnectionUrl(form('pbi-xmla-adomd'), { workspace: 'Team & Sales' }),
        'powerbi:///Team%20%26%20Sales');
    assert.equal(buildConnectionUrl(form('pbi-xmla-adomd'), { workspace: 'V1.0' }),
        'powerbi:///v1.0/myorg/V1.0');
    assert.equal(buildConnectionUrl(form('pbi-desktop-adomd'), { model: 'Sales' }), 'pbidesktop:///Sales');
    const ssas = form('ssas-multidim-adomd');
    const values = { host: 'ssas.example', catalog: 'analytics' };
    assert.equal(buildConnectionUrl(ssas, values), 'ssasmultidim://ssas.example/analytics');
    assert.equal(buildConnectionUrl(ssas, { ...values, cube: 'sales' }), 'ssasmultidim://ssas.example/~/analytics/sales');
    assert.equal(buildConnectionUrl(ssas, { ...values, port: 2383 }), 'ssasmultidim://ssas.example:2383/~/analytics');
});

test('file, memory and local-port profiles use their own URL shapes', () => {
    assert.equal(buildConnectionUrl(form('sqlite-native'), { file: String.raw`C:\Data\example file.db` }),
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
