import { authenticationMethods, buildConnectionUrl, connectionForm } from './connection-url.js';

const page = document.getElementById('connection-url-builder');
const find = id => page.querySelector('#builder-' + id);

async function loadCatalog() {
    find('loading').hidden = false;
    try {
        const response = await fetch(page.dataset.catalogUrl);
        if (!response.ok) throw new Error('Connection profiles could not be loaded.');
        const catalog = await response.json();
        if (catalog.schemaVersion !== 2) throw new Error('Unsupported connection catalog version.');
        startBuilder(catalog);
        find('loading').hidden = true;
        find('content').hidden = false;
    } catch {
        find('loading').textContent = 'The connection profiles could not be loaded. Reload the page to try again.';
    }
}

function startBuilder(catalog) {
    const state = { database: catalog.databases[0], values: {} };
    let currentUrl = '';
    const copy = find('copy');
    const output = find('result');
    const driverSettings = find('driver-settings');

    function note(id, text) {
        const element = find(id);
        const message = text ?? '';
        if (element.textContent !== message) element.textContent = message;
        element.hidden = !message;
    }

    function selectRoute(route) {
        const previousDriverFields = catalog.driverFields[state.route?.api] ?? {};
        Object.keys(previousDriverFields).forEach(id => delete state.values[id]);
        state.route = route;
        selectLocation(route.locations[0]);
    }

    function selectLocation(locationId) {
        state.locationId = locationId;
        state.authIndex = 0;
        state.tlsId = undefined;
    }

    function addControl(parent, control, id, label, wide) {
        const wrapper = document.createElement('div');
        wrapper.className = wide ? 'url-builder-field url-builder-wide' : 'url-builder-field';
        const title = document.createElement('label');
        control.id = title.htmlFor = 'builder-input-' + id;
        title.textContent = label;
        wrapper.append(title, control);
        parent.append(wrapper);
    }

    function addSelect(parent, id, label, options, value, change, wide = false) {
        const control = document.createElement('select');
        options.forEach(([key, name]) => control.add(new Option(name, key)));
        control.value = value;
        control.addEventListener('change', () => {
            change(control.value);
            render();
            page.querySelector('#' + control.id)?.focus();
        });
        addControl(parent, control, id, label, wide);
    }

    function addField(parent, id, field, form) {
        const input = document.createElement('input');
        input.type = 'text';
        input.spellcheck = false;
        if (field.inputMode) input.inputMode = field.inputMode;
        input.placeholder = String(field.placeholder ?? field.default ?? '');
        input.value = state.values[id] ?? '';
        input.required = Boolean(field.required && field.default === undefined);
        input.addEventListener('input', () => {
            state.values[id] = input.value;
            renderOutput(form);
        });
        addControl(parent, input, id, field.label);
    }

    function render() {
        const { database, route, locationId } = state;
        const providers = find('provider');
        const details = find('location');
        const authPanel = find('auth');
        const driverFields = find('driver-fields');
        [providers, details, authPanel, driverFields].forEach(panel => panel.replaceChildren());

        addSelect(providers, 'db', 'Database', catalog.databases.map(item => [item.id, item.label]), database.id, id => {
            state.database = catalog.databases.find(item => item.id === id);
            state.values = {};
            selectRoute(state.database.routes[0]);
        }, database.routes.length === 1);
        if (database.routes.length > 1) {
            addSelect(providers, 'driver', 'Driver', database.routes.map(item => [item.id, item.api + ' · ' + item.driver]),
                route.id, id => selectRoute(database.routes.find(item => item.id === id)));
        }
        note('driver-note', [
            database.routes.length === 1 ? route.api + ' · ' + route.driver + '.' : '',
            route.platforms ? route.platforms.join(' / ') + '.' : '',
            route.note
        ].filter(Boolean).join(' '));

        const methods = authenticationMethods(catalog, route, locationId);
        const auth = methods[state.authIndex];
        const form = connectionForm(catalog, route, locationId, auth, state.tlsId);
        if (route.locations.length > 1) {
            addSelect(details, 'location', route.schemeByLocation ? 'Transport' : 'Connect by',
                route.locations.map(id => [id, catalog.locations[id].label]), locationId, selectLocation);
        }
        if (form.tlsModes.length > 1) {
            addSelect(details, 'tls', 'TLS / SSL', form.tlsModes.map(mode => [mode.id, mode.label]), form.tls.id,
                id => { state.tlsId = id; });
        }
        find('transport').textContent = form.transport;
        form.connectionFields.forEach(id => addField(details, id, form.fields[id], form));

        driverSettings.hidden = !form.advancedFields.length;
        if (form.advancedFields.some(id => form.fields[id].required)) driverSettings.open = true;
        form.advancedFields.forEach(id => addField(driverFields, id, form.fields[id], form));

        if (methods.length > 1) {
            addSelect(authPanel, 'auth', 'Sign-in method', methods.map((method, index) => [index, method.label]),
                state.authIndex, index => { state.authIndex = Number(index); }, !form.authFields.length);
        } else {
            const name = document.createElement('p');
            name.className = 'url-builder-wide';
            name.textContent = auth.label;
            authPanel.append(name);
        }
        form.authFields.forEach(id => addField(authPanel, id, form.fields[id], form));
        note('auth-note', [
            auth.platforms ? auth.platforms.join(' / ') + '.' : '',
            auth.requiresTls && form.tlsModes.length === 1 ? 'This method requires TLS.' : '',
            auth.note ?? (auth.id === 'password' ? 'Supply the password when opening the connection.' : '')
        ].filter(Boolean).join(' '));

        renderOutput(form);
    }

    function renderOutput(form) {
        const { auth, tls } = form;
        const run = find('run');
        run.hidden = true;
        copy.textContent = 'Copy URL';
        try {
            currentUrl = buildConnectionUrl(form, state.values);
        } catch (error) {
            currentUrl = '';
            output.textContent = 'Complete the connection details';
            copy.disabled = true;
            note('result-note', error instanceof URIError ? 'Enter valid text in the connection fields.' : error.message);
            return;
        }

        output.textContent = currentUrl;
        copy.disabled = false;
        note('result-note', auth.via === 'external' ? 'Complete sign-in in the consuming application.' : '');

        if (auth.cli || (tls?.cli && auth.via === 'url')) {
            const args = [currentUrl, ...(auth.cli ?? []), ...(tls?.cli ?? [])];
            const quote = argument => "'" + argument.replaceAll("'", "'\"'\"'") + "'";
            find('run-heading').textContent = 'DubUrl CLI';
            find('run-text').textContent = 'duburl connection check ' + args.map((arg, index) =>
                index === 0 || /\s|['"]/.test(arg) ? quote(arg) : arg).join(' ');
            note('run-note', auth.id === 'certificate' ? 'Replace the sample certificate and key paths.' : '');
            run.hidden = false;
        } else if (auth.library || tls?.library) {
            find('run-heading').textContent = 'Library configuration';
            find('run-text').textContent = [auth.library, tls?.library].filter(Boolean).join('\n');
            note('run-note', 'Apply these settings when opening the URL.');
            run.hidden = false;
        }
    }

    copy.addEventListener('click', async () => {
        const url = currentUrl;
        try {
            await navigator.clipboard.writeText(url);
            if (currentUrl === url) note('result-note', 'URL copied to the clipboard.');
        } catch {
            if (currentUrl !== url) return;
            const range = document.createRange();
            range.selectNodeContents(output);
            const selection = window.getSelection();
            selection.removeAllRanges();
            selection.addRange(range);
            note('result-note', 'URL selected. Use your system copy shortcut.');
        }
    });

    page.querySelector('form').addEventListener('submit', event => event.preventDefault());
    selectRoute(state.database.routes[0]);
    render();
}

loadCatalog();
