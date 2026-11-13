export const operations = { inspect: 'Inspect', generate: 'Generate DDL' };
export const statuses = {
    supported: ['✓', 'Verified support'],
    limited: ['✓*', 'Verified with limits'],
    unsupported: ['—', 'Verified unsupported'],
    unverified: ['?', 'Unverified'],
    failed: ['×', 'Verification failed']
};

export function matrixView(report, { databases, variants, group, operation }) {
    return {
        columns: report.providers.filter(provider => databases.has(provider.id)).map(provider => ({
            provider,
            target: provider.targets.find(item => item.id === variants.get(provider.id)) ?? provider.targets[0]
        })),
        features: report.features.filter(feature => !group || feature.group === group),
        operations: operation ? [operation] : Object.keys(operations)
    };
}

export function cellDetail(report, feature, provider, target, operation) {
    const cell = target.coverage[feature.id][operation];
    return {
        title: feature.label + ' · ' + provider.label,
        result: operations[operation] + ' · ' + statuses[cell.status][1],
        variant: target.variant ?? 'No schema QA target configured',
        evidence: cell.evidence.map(item => ({
            test: item.test,
            result: statuses[item.status][1] + ' · ' + (item.outcomes.join(', ') || 'No test result'),
            note: item.note
        })),
        runUrl: report.runUrl,
        artifact: target.artifact
    };
}

function element(tag, text, className) {
    const node = document.createElement(tag);
    if (text) node.textContent = text;
    if (className) node.className = className;
    return node;
}

function option(value, label) {
    const node = element('option', label);
    node.value = value;
    return node;
}

function marker(status) {
    const node = element('span', statuses[status][0], 'schema-coverage-mark');
    node.dataset.status = status;
    node.setAttribute('aria-hidden', 'true');
    return node;
}

function link(url, label) {
    const node = element('a', label);
    node.href = url;
    return node;
}

function setup(root, report) {
    const get = name => root.querySelector('#schema-coverage-' + name);
    const providers = get('providers');
    const group = get('group');
    const operation = get('operation');
    const table = get('table');
    const dialog = get('detail');
    const defaultDatabases = ['sqlserver', 'duckdb', 'oracle', 'databricks', 'postgresql'];
    const available = new Set(report.providers.map(provider => provider.id));
    const databases = new Set(defaultDatabases.filter(id => available.has(id)));
    if (!databases.size) {
        for (const provider of report.providers.slice(0, 5)) databases.add(provider.id);
    }
    const variants = new Map();

    function showDetail(feature, provider, target, mode) {
        const details = cellDetail(report, feature, provider, target, mode);
        get('detail-title').textContent = details.title;
        const body = get('detail-body');
        body.replaceChildren(element('p', details.result), element('p', details.variant, 'schema-coverage-muted'));
        if (details.evidence.length) {
            const tests = element('ul');
            for (const item of details.evidence) {
                const test = element('li');
                test.append(element('code', item.test), element('div', item.result));
                if (item.note) test.append(element('div', item.note));
                tests.append(test);
            }
            body.append(tests);
        } else {
            body.append(element('p', 'No mapped QA evidence for this feature and operation.'));
        }
        if (details.artifact) {
            const artifact = element('p', 'QA artifact ');
            artifact.append(element('code', details.artifact));
            body.append(artifact);
        }
        body.append(link(details.runUrl, 'Open provider QA run'));
        dialog.showModal();
    }

    function fillColumn(heading, provider, target) {
        heading.append(element('div', provider.label));
        if (provider.targets.length > 1) {
            const select = element('select');
            select.setAttribute('aria-label', provider.label + ' engine variant');
            select.append(...provider.targets.map(item => option(item.id, item.variant ?? item.id)));
            select.value = target.id;
            select.addEventListener('change', () => {
                variants.set(provider.id, select.value);
                render(provider.id);
            });
            heading.append(select);
            return select;
        }
        if (target.variant) heading.append(element('small', target.variant, 'schema-coverage-muted'));
        return null;
    }

    function resultCell(row, feature, { provider, target }, modes) {
        const actions = element('div', '', 'schema-coverage-cell');
        for (const mode of modes) {
            const result = target.coverage[feature.id][mode];
            const button = element('button', '', 'schema-coverage-result');
            button.type = 'button';
            button.setAttribute('aria-haspopup', 'dialog');
            button.setAttribute('aria-label', [
                feature.label, provider.label, operations[mode], statuses[result.status][1]
            ].join(', '));
            button.append(element('span', mode === 'inspect' ? 'Inspect' : 'DDL'), marker(result.status));
            button.addEventListener('click', () => showDetail(feature, provider, target, mode));
            actions.append(button);
        }
        row.insertCell().append(actions);
    }

    function renderGroup(name, features, view) {
        const body = table.createTBody();
        const heading = element('th', name);
        heading.scope = 'rowgroup';
        heading.colSpan = view.columns.length + 1;
        body.insertRow().append(heading);
        for (const feature of features) {
            const row = body.insertRow();
            const label = element('th', feature.label);
            label.scope = 'row';
            row.append(label);
            for (const column of view.columns) resultCell(row, feature, column, view.operations);
        }
    }

    function render(focusProvider) {
        const view = matrixView(report, { databases, variants, group: group.value, operation: operation.value });
        get('database-count').textContent = '(' + view.columns.length + ' of ' + report.providers.length + ')';
        get('results').textContent = view.columns.length
            ? view.features.length + ' features across ' + view.columns.length + ' databases'
            : 'Select at least one database to compare.';
        get('scroll').hidden = !view.columns.length;
        for (const body of [...table.tBodies]) body.remove();
        const head = table.tHead.rows[0];
        while (head.cells.length > 1) head.deleteCell(-1);
        if (!view.columns.length) return;

        let focusControl;
        for (const { provider, target } of view.columns) {
            const heading = element('th');
            heading.scope = 'col';
            const variant = fillColumn(heading, provider, target);
            if (provider.id === focusProvider) focusControl = variant;
            head.append(heading);
        }

        const groups = new Map();
        for (const feature of view.features) {
            if (!groups.has(feature.group)) groups.set(feature.group, []);
            groups.get(feature.group).push(feature);
        }
        for (const [name, features] of groups) renderGroup(name, features, view);
        focusControl?.focus({ preventScroll: true });
    }

    for (const provider of report.providers) {
        const label = element('label');
        const input = element('input');
        input.type = 'checkbox';
        input.value = provider.id;
        input.checked = databases.has(provider.id);
        input.addEventListener('change', () => {
            if (input.checked) databases.add(provider.id);
            else databases.delete(provider.id);
            render();
        });
        label.append(input, document.createTextNode(provider.label));
        providers.append(label);
    }

    function setDatabases(selected) {
        databases.clear();
        for (const input of providers.querySelectorAll('input')) {
            input.checked = selected;
            if (selected) databases.add(input.value);
        }
        render();
    }

    group.append(option('', 'All feature groups'));
    for (const name of new Set(report.features.map(feature => feature.group))) group.append(option(name, name));
    for (const [status, [, label]] of Object.entries(statuses)) {
        const item = element('li');
        item.append(marker(status), document.createTextNode(label));
        get('legend').append(item);
    }

    group.addEventListener('change', () => render());
    operation.addEventListener('change', () => render());
    get('select-all').addEventListener('click', () => setDatabases(true));
    get('clear').addEventListener('click', () => setDatabases(false));
    const commit = 'https://github.com/Seddryck/DubUrl.Providers/commit/' + report.providerCommit;
    get('source').append(document.createTextNode(' · '), link(report.runUrl, 'Provider QA run'),
        document.createTextNode(' · '), link(commit, report.providerCommit.slice(0, 8)),
        document.createTextNode(' · DubUrl ' + report.dubUrlVersion + ' · Schema ' + report.dubUrlSchemaVersion));

    render();
    get('content').hidden = false;
    get('status').hidden = true;
}

const root = typeof document === 'undefined' ? null : document.getElementById('schema-coverage');
if (root) {
    try {
        const response = await fetch(root.dataset.reportUrl);
        if (!response.ok) throw new Error('HTTP ' + response.status);
        const report = await response.json();
        if (report.schemaVersion !== 1) throw new Error('Unsupported report version');
        setup(root, report);
    } catch (error) {
        root.querySelector('#schema-coverage-status').textContent = 'Could not load the schema coverage report. ' + error.message;
    }
}
