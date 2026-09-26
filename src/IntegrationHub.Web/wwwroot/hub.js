window.hub = {
    request: async (method, path, body, csrf) => {
        if (!path.startsWith('/api/')) throw new Error('Only same-origin API paths are permitted.');
        const response = await fetch(path, { method, credentials: 'same-origin', headers: { 'Content-Type': 'application/json', ...(csrf ? { 'X-CSRF-TOKEN': csrf } : {}) }, ...(body === null ? {} : { body }) });
        return { status: response.status, body: await response.text() };
    },
    diagram: async (element, markup, id) => {
        const viewer = await import('/_content/IntegrationHub.Web/diagram-viewer.mjs');
        await viewer.renderDiagram(element, markup, id);
    },
    disposeDiagram: async (id) => {
        const viewer = await import('/_content/IntegrationHub.Web/diagram-viewer.mjs');
        viewer.disposeDiagram(id);
    }
};
