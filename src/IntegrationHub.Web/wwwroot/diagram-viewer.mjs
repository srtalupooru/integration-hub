import mermaid from './vendor/mermaid/mermaid.esm.min.mjs';
import { centeredView, fitView, zoomAt, withLayout } from './diagram-viewport.mjs';
import { decorateNodes } from './diagram-nodes.mjs';

mermaid.initialize({
    startOnLoad: false, securityLevel: 'strict', theme: 'base', suppressErrorRendering: true,
    themeVariables: { primaryColor: '#f1f5f9', primaryBorderColor: '#64748b', primaryTextColor: '#1b2e3d',
        lineColor: '#94a3b8', fontFamily: 'Arial, sans-serif', fontSize: '14px',
        clusterBkg: '#f8fafc', clusterBorder: '#cbd5e1' },
    flowchart: { htmlLabels: false, curve: 'basis', useMaxWidth: false, nodeSpacing: 56, rankSpacing: 100, padding: 28 }
});
const viewers = new Map();

export async function renderDiagram(root, markup, id) {
    if (!root.isConnected) return;
    let viewer = viewers.get(id);
    if (viewer && viewer.root !== root) { disposeDiagram(id); viewer = null; }
    if (!viewer) { viewer = createViewer(root); viewers.set(id, viewer); }
    await viewer.render(markup);
}

export function disposeDiagram(id) {
    viewers.get(id)?.dispose();
    viewers.delete(id);
}

function createViewer(root) {
    const viewport = root.querySelector('[data-graph-viewport]');
    const stage = root.querySelector('[data-graph-stage]');
    const status = root.querySelector('[data-graph-status]');
    const zoomLabel = root.querySelector('[data-graph-zoom]');
    const layout = root.querySelector('[data-graph-layout]');
    const fullscreen = root.querySelector('[data-graph-action="fullscreen"]');
    const controller = new AbortController();
    const on = (element, event, handler, options = {}) => element.addEventListener(event, handler, { ...options, signal: controller.signal });
    let source = '', svg = null, graphWidth = 0, graphHeight = 0;
    let view = { x: 0, y: 0, scale: 1 }, minimum = 0.05, generation = 0, disposed = false, drag = null;
    let lastWidth = 0, lastHeight = 0;
    const controls = [...root.querySelectorAll('[data-graph-action]')];
    function enableControls(ready) {
        controls.forEach(button => button.disabled = !ready || (button === fullscreen && !document.fullscreenEnabled));
        layout.disabled = !ready;
    }
    function message(text) { status.textContent = text; status.hidden = !text; }
    function apply() {
        stage.style.transform = `translate(${view.x}px, ${view.y}px) scale(${view.scale})`;
        zoomLabel.textContent = `${Math.round(view.scale * 100)}%`;
    }
    function fit() {
        if (!svg) return;
        const next = fitView(viewport.clientWidth, viewport.clientHeight, graphWidth, graphHeight);
        if (!next) return; // Hidden Blazor tabs are fitted when their viewport becomes visible.
        minimum = Math.min(0.05, next.scale / 2);
        view = next;
        apply();
    }
    function zoom(factor, point = { x: viewport.clientWidth / 2, y: viewport.clientHeight / 2 }) {
        if (!svg) return;
        view = zoomAt(view, factor, point, minimum);
        apply();
    }
    async function render(markup) {
        source = markup;
        const ticket = ++generation;
        svg = null;
        stage.replaceChildren();
        enableControls(false);
        viewport.setAttribute('aria-busy', 'true');
        message('Loading graph…');
        try {
            if (!markup.trim()) { message('No graph is available yet.'); return; }
            const result = await mermaid.render('mermaid-' + crypto.randomUUID(), withLayout(markup, layout.value));
            if (disposed || ticket !== generation) return;
            // Measure on a laid-out surface even when this graph lives in a hidden tab.
            const measurement = document.createElement('div');
            measurement.style.cssText = 'position:fixed;left:-100000px;top:0;visibility:hidden;pointer-events:none';
            document.body.append(measurement);
            try {
                measurement.innerHTML = result.svg;
                svg = measurement.querySelector('svg');
                decorateNodes(svg, layout.value);
                stage.replaceChildren(svg);
            } finally { measurement.remove(); }
            const box = svg.viewBox.baseVal;
            graphWidth = box.width;
            graphHeight = box.height;
            if (!(graphWidth > 0 && graphHeight > 0)) throw new Error('The graph has no drawable dimensions.');
            svg.setAttribute('width', graphWidth);
            svg.setAttribute('height', graphHeight);
            svg.setAttribute('role', 'img');
            svg.setAttribute('aria-label', 'Integration components and their connections');
            svg.style.maxWidth = 'none';
            stage.style.width = `${graphWidth}px`;
            stage.style.height = `${graphHeight}px`;
            message('');
            fit();
            enableControls(true);
        } catch {
            if (disposed || ticket !== generation) return;
            svg = null;
            stage.replaceChildren();
            message('The graph could not be rendered. Generated Mermaid is shown below.');
            const pre = document.createElement('pre');
            pre.textContent = markup;
            status.append(pre);
        } finally {
            if (!disposed && ticket === generation) viewport.setAttribute('aria-busy', 'false');
        }
    }
    async function action(name) {
        if (!svg) return;
        switch (name) {
            case 'zoom-in': zoom(1.25); break;
            case 'zoom-out': zoom(0.8); break;
            case 'fit': fit(); break;
            case 'actual':
                view = centeredView(viewport.clientWidth, viewport.clientHeight, graphWidth, graphHeight, 1);
                apply(); break;
            case 'fullscreen':
                try {
                    if (document.fullscreenElement === root) await document.exitFullscreen();
                    else await root.requestFullscreen();
                    message('');
                } catch { message('Fullscreen is unavailable in this browser. You can still pan and zoom here.'); }
                break;
            case 'download': {
                const exported = svg.cloneNode(true);
                exported.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
                const url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(exported)], { type: 'image/svg+xml;charset=utf-8' }));
                const link = document.createElement('a');
                link.href = url;
                link.download = 'integration-graph.svg';
                document.body.append(link);
                link.click();
                link.remove();
                setTimeout(() => URL.revokeObjectURL(url), 1000);
                break;
            }
        }
    }
    on(root, 'click', event => {
        const button = event.target.closest('[data-graph-action]');
        if (button && root.contains(button)) void action(button.dataset.graphAction);
    });
    on(layout, 'change', () => { void render(source); });
    on(viewport, 'wheel', event => {
        if (!svg || (!event.ctrlKey && !event.metaKey)) return;
        event.preventDefault();
        const bounds = viewport.getBoundingClientRect();
        zoom(Math.exp(-Math.max(-100, Math.min(100, event.deltaY)) * 0.01), { x: event.clientX - bounds.left, y: event.clientY - bounds.top });
    }, { passive: false });
    on(viewport, 'pointerdown', event => {
        if (!svg || event.button !== 0 || !event.isPrimary) return;
        viewport.focus({ preventScroll: true });
        viewport.setPointerCapture(event.pointerId);
        drag = { id: event.pointerId, x: event.clientX, y: event.clientY };
        viewport.classList.add('is-panning');
        event.preventDefault();
    });
    on(viewport, 'pointermove', event => {
        if (!drag || event.pointerId !== drag.id) return;
        view.x += event.clientX - drag.x;
        view.y += event.clientY - drag.y;
        drag.x = event.clientX;
        drag.y = event.clientY;
        apply();
    });
    const endDrag = () => { drag = null; viewport.classList.remove('is-panning'); };
    on(viewport, 'pointerup', endDrag);
    on(viewport, 'pointercancel', endDrag);
    on(viewport, 'lostpointercapture', endDrag);
    on(viewport, 'dblclick', fit);
    on(viewport, 'keydown', event => {
        if (!svg || event.ctrlKey || event.metaKey || event.altKey) return;
        const moves = { ArrowLeft: [40, 0], ArrowRight: [-40, 0], ArrowUp: [0, 40], ArrowDown: [0, -40] };
        if (moves[event.key]) { const [x, y] = moves[event.key]; view.x += x; view.y += y; apply(); }
        else if (['+', '='].includes(event.key)) zoom(1.25);
        else if (event.key === '-') zoom(0.8);
        else if (['0', 'Home'].includes(event.key)) fit();
        else return;
        event.preventDefault();
    });
    on(document, 'fullscreenchange', () => {
        fullscreen.textContent = document.fullscreenElement === root ? 'Exit fullscreen' : 'Fullscreen';
        fit();
    });
    const observer = new ResizeObserver(() => {
        const width = viewport.clientWidth, height = viewport.clientHeight;
        if (width === lastWidth && height === lastHeight) return;
        lastWidth = width; lastHeight = height;
        fit();
    });
    observer.observe(viewport);
    enableControls(false);
    return { root, render, dispose() { disposed = true; generation++; controller.abort(); observer.disconnect(); } };
}
