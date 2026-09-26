// Original vector symbols. Everything is inline SVG so downloads remain self-contained.
const icons = {
    system: ['M4 4h16v12H4zM8 20h8M12 16v4', 'M7 8h4M7 11h7'],
    api: ['m8 6-6 6 6 6m8-12 6 6-6 6m-3-15-2 18'],
    gateway: ['M4 5h16v14H4zM8 5v14M16 5v14M1 12h6m10 0h6'],
    function: ['m14 2-10 12h7l-1 8L20 9h-7z'],
    workflow: ['M3 3h6v6H3zM15 15h6v6h-6zM15 3h6v6h-6zM9 6h6M6 9v9h9'],
    topic: ['M2 9h6v6H2zM16 2h6v6h-6zM16 16h6v6h-6zM8 12h4V5h4m-4 7v7h4'],
    queue: ['M2 5h20v14H2zM7 9v6m5-6v6m5-6v6'],
    event: ['m12 2 10 10-10 10L2 12zM8 12h8m-4-4v8'],
    database: ['M3 6c0-5 18-5 18 0s-18 5-18 0v12c0 5 18 5 18 0V6M3 12c0 5 18 5 18 0'],
    storage: ['M3 5h18v15H3zM2 5V2h20v3M9 10h6'],
    cloud: ['M7 18a5 5 0 0 1-1-10 6 6 0 0 1 11-2 6 6 0 0 1 1 12z'],
    package: ['m12 2 10 5v10l-10 5-10-5V7zM2 7l10 5 10-5M12 12v10M7 4l10 5'],
    transform: ['M3 6h14m-4-4 4 4-4 4M21 18H7m4-4-4 4 4 4'],
    file: ['M5 2h9l5 5v15H5zM14 2v6h5M8 12h8M8 16h8'],
    person: ['M8 6a4 4 0 1 0 8 0 4 4 0 1 0-8 0M4 22v-4a8 8 0 0 1 16 0v4'],
    custom: ['m12 2 10 10-10 10L2 12z', 'M8 12h8M12 8v8']
};
const types = {
    ExternalSystem: ['External system', 'system', '#2563eb', '#eff6ff'],
    InternalSystem: ['Internal system', 'system', '#2563eb', '#eff6ff'],
    Api: ['API', 'api', '#0d9488', '#ecfdf5'],
    ApiManagement: ['API management', 'gateway', '#0d9488', '#ecfdf5'],
    AzureFunction: ['Azure Function', 'function', '#b7790a', '#fffbeb'],
    LogicApp: ['Logic App', 'workflow', '#7c3aed', '#f5f3ff'],
    ServiceBusTopic: ['Service Bus topic', 'topic', '#c16b16', '#fff7ed'],
    ServiceBusQueue: ['Service Bus queue', 'queue', '#c16b16', '#fff7ed'],
    MessageBroker: ['Message broker', 'topic', '#c16b16', '#fff7ed'],
    EventGrid: ['Event Grid', 'event', '#c16b16', '#fff7ed'],
    Database: ['Database', 'database', '#0284c7', '#f0f9ff'],
    Storage: ['Storage', 'storage', '#0284c7', '#f0f9ff'],
    SaaS: ['SaaS', 'cloud', '#2563eb', '#eff6ff'],
    Library: ['Library', 'package', '#7c3aed', '#f5f3ff'],
    NuGetPackage: ['NuGet package', 'package', '#7c3aed', '#f5f3ff'],
    Transformation: ['Transform', 'transform', '#7c3aed', '#f5f3ff'],
    Sftp: ['SFTP', 'file', '#0284c7', '#f0f9ff'],
    FileShare: ['File share', 'file', '#0284c7', '#f0f9ff'],
    ManualProcess: ['Manual process', 'person', '#64748b', '#f1f5f9'],
    Custom: ['Custom component', 'custom', '#64748b', '#f1f5f9']
};
const ns = 'http://www.w3.org/2000/svg';
function element(document, name, attributes = {}, text) {
    const node = document.createElementNS(ns, name);
    for (const [key, value] of Object.entries(attributes)) node.setAttribute(key, value);
    if (text !== undefined) node.textContent = text;
    return node;
}

export function decorateNodes(svg, direction) {
    const document = svg.ownerDocument;
    svg.classList.add('hub-workflow');
    const shadowId = `${svg.id}-card-shadow`;
    const defs = element(document, 'defs');
    const filter = element(document, 'filter', { id: shadowId, x: '-30%', y: '-30%', width: '160%', height: '180%' });
    filter.append(element(document, 'feDropShadow', { dx: 0, dy: 3, stdDeviation: 4, 'flood-color': '#1e293b', 'flood-opacity': '.09' }));
    defs.append(filter);
    svg.prepend(defs);
    svg.append(element(document, 'style', {}, `
        .hub-workflow .node .hub-surface{transition:stroke .15s,filter .15s}
        .hub-workflow .node:hover,.hub-workflow .node:focus{--node-border:var(--node-accent);--node-stroke-width:2px}
        .hub-workflow .node:focus{outline:none}
        .hub-workflow .node .label text{font-weight:600;fill:#1e293b!important}
        .hub-workflow .flowchart-link{stroke:#94a3b8!important;stroke-width:1.7px!important}
        .hub-workflow .edgeLabel rect{fill:#fff!important;opacity:.96!important;rx:5;ry:5}
        .hub-workflow .edgeLabel text{fill:#536477!important;font-size:12px}
        .hub-workflow .cluster rect{fill:#f5f7fb!important;stroke:#d9e2ed!important;rx:16;ry:16}
        @media(prefers-reduced-motion:reduce){.hub-workflow .node .hub-surface{transition:none}}
    `));

    for (const node of svg.querySelectorAll('g.node')) {
        const typeClass = [...node.classList].find(c => c.startsWith('hub-type-'));
        const type = typeClass?.slice('hub-type-'.length);
        if (!Object.hasOwn(types, type)) continue; // Preserve older or external Mermaid nodes unchanged.
        const shape = node.querySelector(':scope > rect');
        const label = node.querySelector(':scope > .label');
        if (!shape || !label) continue;
        const box = shape.getBBox();
        const [caption, icon, accent, tint] = types[type];
        const fullName = label.textContent.trim();
        node.style.setProperty('--node-accent', accent);
        node.setAttribute('tabindex', '0');
        node.setAttribute('role', 'group');
        node.setAttribute('aria-label', `${caption}: ${fullName}`);
        node.prepend(element(document, 'title', {}, `${caption}: ${fullName}`));
        shape.classList.add('hub-surface');
        shape.setAttribute('rx', '14'); shape.setAttribute('ry', '14');
        shape.style.setProperty('fill', '#fff', 'important');
        shape.style.setProperty('stroke', 'var(--node-border, #d6e0ea)', 'important');
        shape.style.setProperty('stroke-width', 'var(--node-stroke-width, 1.2px)', 'important');
        shape.setAttribute('filter', `url(#${shadowId})`);
        // Align within the measured rectangle without changing its layout or edge anchors.
        const labelBox = label.getBBox();
        label.setAttribute('transform', `translate(${box.x + 66 - labelBox.x} ${10 - labelBox.y - labelBox.height / 2})`);
        const art = element(document, 'g', { 'aria-hidden': 'true', 'pointer-events': 'none' });
        node.append(art);
        const header = element(document, 'text', { x: box.x + 18, y: box.y + 18,
            fill: '#64748b', 'font-family': 'Arial, sans-serif', 'font-size': 9, 'font-weight': 700, 'letter-spacing': '.5' }, caption.toUpperCase());
        art.append(header);
        // Fit the type caption on even a card named with a single letter.
        const captionWidth = header.getComputedTextLength();
        if (captionWidth > box.width - 36) header.setAttribute('font-size', 9 * (box.width - 36) / captionWidth);
        const tileX = box.x + 16, tileY = 10 - 18;
        art.append(element(document, 'rect', { x: tileX, y: tileY, width: 36, height: 36, rx: 10, fill: tint, stroke: 'none' }));
        const symbol = element(document, 'g', { transform: `translate(${tileX + 7} ${tileY + 7}) scale(.9167)`,
            fill: 'none', stroke: accent, 'stroke-width': 1.7, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' });
        for (const d of icons[icon]) symbol.append(element(document, 'path', { d }));
        art.append(symbol);
        const vertical = direction === 'TB';
        const ports = [];
        if (node.classList.contains('hub-input')) ports.push(vertical ? [0, box.y] : [box.x, 0]);
        if (node.classList.contains('hub-output')) ports.push(vertical ? [0, box.y + box.height] : [box.x + box.width, 0]);
        for (const [cx, cy] of ports) art.append(element(document, 'circle', { cx, cy, r: 3.5, fill: '#fff', stroke: accent, 'stroke-width': 1.5 }));
    }
    // Give shadows and ports room even on the outermost nodes. Preserve the layout origin.
    const box = svg.viewBox.baseVal;
    svg.setAttribute('viewBox', `${box.x - 12} ${box.y - 12} ${box.width + 24} ${box.height + 24}`);
}
