export function authenticationMethods(catalog, route, locationId) {
    return route.auth.map(item => {
        const override = typeof item === 'string' ? { id: item } : item;
        return { ...catalog.authentication[override.id], ...override };
    }).filter(method => !method.locations || method.locations.includes(locationId));
}

export function connectionForm(catalog, route, locationId, auth, selectedTls) {
    const location = catalog.locations[locationId];
    const policy = catalog.tlsPolicies[auth.tlsOverride ?? route.tls ?? 'driverDefault'];
    const tlsModes = policy.modes.filter(mode => !auth.requiresTls || mode.secure);
    const tls = tlsModes.find(mode => mode.id === (selectedTls ?? policy.default)) ?? tlsModes[0];
    const driverFields = catalog.driverFields[route.api] ?? {};
    const connectionFields = Object.keys(location.fields ?? {});
    const routeOptions = new Set(Object.keys(route.query ?? {}).map(key => key.toLowerCase()));
    const advancedFields = Object.keys(driverFields).filter(id => !routeOptions.has(driverFields[id].queryKey.toLowerCase()));
    const authFields = Object.keys(auth.fields ?? {});
    const fields = Object.fromEntries([...connectionFields, ...advancedFields, ...authFields].map(id => {
        const field = { ...location.fields?.[id], ...driverFields[id], ...route.fields?.[id], ...auth.fields?.[id] };
        return [id, { ...catalog.layouts[field.layout], ...field }];
    }));
    return {
        route, locationId, location, auth, tls, tlsModes, fields, connectionFields, advancedFields, authFields,
        transport: tls?.transport ?? tls?.transportByProtocol?.[location.transport] ?? location.transport
    };
}

const clean = value => String(value ?? '').trim();
const escapeComponent = value => encodeURIComponent(value).replace(/[!'()*]/g,
    character => '%' + character.codePointAt(0).toString(16).toUpperCase());

function encodeHost(value) {
    if (!value) return '';
    try {
        if (value.includes(':')) {
            const ipv6 = value.startsWith('[') ? value : '[' + value + ']';
            const parsed = new URL('https://' + ipv6);
            if (ipv6.endsWith(']') && parsed.hostname.startsWith('[') && !parsed.port && parsed.pathname === '/'
                && !parsed.search && !parsed.hash && !parsed.username && !parsed.password) return parsed.hostname;
        } else if (!/[\s/#?@%\\[\]]/.test(value)) {
            return new URL('https://' + value).hostname;
        }
    } catch {
        // Report a field-specific message for both URL parsing and character validation.
    }
    throw new Error('Enter a valid hostname or IP address.');
}

function splitPath(value) {
    let start = 0;
    let end = value.length;
    while (start < end && value[start] === '/') start++;
    while (end > start && value[end - 1] === '/') end--;
    const parts = value.slice(start, end).split('/');
    if (parts.some(part => !part || part === '.' || part === '..')) {
        throw new Error('Enter a path with valid segments.');
    }
    return parts;
}

function filePath(value, relativeOnly) {
    const normalized = value.replaceAll('\\', '/');
    if (normalized.startsWith('//')) throw new Error('Enter a local path.');
    if (relativeOnly && (normalized.startsWith('/') || /^[A-Za-z]:/.test(normalized))) {
        throw new Error('Enter a relative path for this ODBC driver.');
    }
    if (/^[A-Za-z]:[^/]/.test(normalized)) throw new Error('Enter a complete Windows file path.');
    const parts = splitPath(normalized);
    if (/^[A-Za-z]:$/.test(parts[0])) {
        if (!/^[A-Za-z]:\//.test(normalized) || parts.length < 2) throw new Error('Enter a complete Windows file path.');
        return [parts[0], ...parts.slice(1).map(escapeComponent)];
    }
    return [...(normalized.startsWith('/') ? [''] : []), ...parts.map(escapeComponent)];
}

function endpointPath(path, defaultPath, reserveSuffix) {
    if (!path) return [];
    const parts = splitPath(path);
    if (defaultPath && parts.join('/') === splitPath(defaultPath).join('/')) return [];
    if (reserveSuffix && parts.at(-2) === reserveSuffix) {
        throw new Error('Choose an HTTP endpoint that does not end in /' + reserveSuffix + '/<name>.');
    }
    return parts.map(escapeComponent);
}

function encodePath(path, fields, value) {
    return path.flatMap(part => {
        if (typeof part === 'string' || part.field) {
            const id = part.field ?? part;
            const needsFallback = !part.fallbackWhen || part.fallbackWhen.some(key => value(key));
            const segment = value(id) || (needsFallback ? part.fallback : '');
            if (segment === '.' || segment === '..') {
                throw new Error('Enter a valid ' + fields[id].label.toLowerCase() + '.');
            }
            return segment ? [escapeComponent(segment)] : [];
        }
        if (part.literal) {
            const condition = value(part.when);
            const matches = part.equalsIgnoreCase === undefined || condition.toLowerCase() === part.equalsIgnoreCase.toLowerCase();
            return (!part.when || condition) && matches ? [escapeComponent(part.literal)] : [];
        }
        if (part.file || part.relativeFile) return filePath(value(part.file ?? part.relativeFile), Boolean(part.relativeFile));
        return endpointPath(value(part.split), fields[part.split].default, part.reserveSuffix);
    });
}

function portOverride(port, field, entered) {
    if (!port) return '';
    if (!/^\d{1,5}$/.test(port) || Number(port) < 1 || Number(port) > 65535) {
        throw new Error('Enter a port from 1 to 65535.');
    }
    if (Number(port) === field.default && !field.defaultUnless?.some(entered)) return '';
    return String(Number(port));
}

function validateFields(fields, value) {
    for (const [id, field] of Object.entries(fields)) {
        const input = value(id);
        if (!input && field.required) throw new Error('Enter ' + field.label.toLowerCase() + '.');
        if (input && field.pattern && !new RegExp('^(?:' + field.pattern + ')$').test(input)) {
            throw new Error('Check ' + field.label.toLowerCase() + '.');
        }
    }
}

function validateLocation(location, fields, value, entered) {
    for (const [id, dependency] of Object.entries(location.requires ?? {})) {
        if (value(id) && !value(dependency)) {
            throw new Error(fields[id].label + ' requires ' + fields[dependency].label.toLowerCase() + '.');
        }
    }
    for (const [left, right] of location.exclusive ?? []) {
        if (entered(left) && entered(right)) {
            throw new Error('Choose either ' + fields[left].label.toLowerCase() + ' or ' + fields[right].label.toLowerCase() + '.');
        }
    }
}

function resolveScheme(route, locationId, tls, value) {
    const schemes = route.schemeByLocation?.[locationId];
    if (!route.schemeByExtension) {
        const scheme = schemes?.[tls?.id] ?? schemes?.default ?? route.scheme;
        return scheme + (tls?.schemeSuffix ?? '');
    }

    const extension = /\.([A-Za-z0-9]+)$/.exec(value('file'))?.[1].toLowerCase();
    if (!extension || !Object.hasOwn(route.schemeByExtension, extension)) {
        const extensions = Object.keys(route.schemeByExtension).map(ext => '.' + ext);
        throw new Error('Choose a file ending in ' + extensions.join(', ') + '.');
    }
    return route.schemeByExtension[extension];
}

function encodeOptions(sources, entries, optionValue) {
    const query = new Map();
    const setOption = (key, setting) => query.set(key.toLowerCase(), [key, setting]);
    for (const source of sources) {
        Object.entries(source ?? {}).forEach(([key, setting]) => setOption(key, setting));
    }
    for (const [id, { queryKey }] of entries) {
        const input = optionValue(id);
        if (queryKey && input) setOption(queryKey, input);
    }
    return [...query.values()].map(([key, setting]) => escapeComponent(key) + '=' + escapeComponent(setting));
}

export function buildConnectionUrl(form, values) {
    const { route, locationId, location, auth, tls, fields } = form;
    const entered = id => Object.hasOwn(fields, id) ? clean(values[id]) : '';
    const value = id => entered(id) || clean(fields[id]?.default);
    const entries = Object.entries(fields);
    const role = name => entries.find(([, field]) => field.url === name)?.[0];

    validateFields(fields, value);
    validateLocation(location, fields, value, entered);

    const portField = role('port');
    const portDefinition = fields[portField];
    const explicitPort = portOverride(entered(portField), portDefinition, entered);
    const path = encodePath(location.path ?? [], fields, value);
    const scheme = resolveScheme(route, locationId, tls, value);
    const user = auth.fixedUser ?? value(role('username'));
    const host = encodeHost(location.fixedHost ?? value(role('host')));
    const options = encodeOptions([route.query, tls?.query, auth.query, auth.queryByLocation?.[locationId]],
        entries, id => id === portField ? explicitPort : value(id));
    return scheme + '://' + (user ? escapeComponent(user) + '@' : '') + host
        + (explicitPort && !portDefinition?.queryKey ? ':' + explicitPort : '')
        + (path.length ? '/' + path.join('/') : '')
        + (options.length ? '?' + options.join('&') : '');
}

export function buildConnectionCheckCommand(url, { auth, tls }) {
    if (!auth.cli && !(tls?.cli && auth.via === 'url')) return '';

    const args = [url, ...(auth.cli ?? []), ...(tls?.cli ?? [])];
    const quote = argument => "'" + argument.replaceAll("'", "'\"'\"'") + "'";
    return 'duburl connection check ' + args.map((arg, index) =>
        index === 0 || /\s|['"]/.test(arg) ? quote(arg) : arg).join(' ');
}
