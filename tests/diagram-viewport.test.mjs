import test from 'node:test';
import assert from 'node:assert/strict';
import { centeredView, fitView, zoomAt, withLayout } from '../src/IntegrationHub.Web/wwwroot/diagram-viewport.mjs';

const close = (actual, expected) => assert.ok(Math.abs(actual - expected) < 1e-9, `${actual} != ${expected}`);

test('wide and tall graphs fit inside the padded viewport and remain centred', () => {
    for (const [width, height] of [[2400, 150], [200, 4000], [25000, 15000]]) {
        const view = fitView(800, 460, width, height);
        assert.ok(width * view.scale <= 736);
        assert.ok(height * view.scale <= 396);
        close(view.x * 2 + width * view.scale, 800);
        close(view.y * 2 + height * view.scale, 460);
    }
});

test('fit preserves natural size for small graphs', () => {
    assert.deepEqual(fitView(800, 460, 200, 100), { x: 300, y: 180, scale: 1 });
});

test('hidden or empty graphs do not create an invalid transform', () => {
    for (const dimensions of [[0, 460, 200, 100], [800, 0, 200, 100], [800, 460, 0, 100], [800, 460, 200, 0]]) {
        assert.equal(fitView(...dimensions), null);
    }
    assert.ok(fitView(10, 10, 200, 100).scale > 0);
});

test('zoom keeps the graph point beneath the pointer stationary', () => {
    const view = { x: -120, y: 30, scale: 0.4 }, point = { x: 231, y: 190 };
    const next = zoomAt(view, 1.25, point);
    close((point.x - next.x) / next.scale, (point.x - view.x) / view.scale);
    close((point.y - next.y) / next.scale, (point.y - view.y) / view.scale);
    const restored = zoomAt(next, 0.8, point);
    close(restored.x, view.x); close(restored.y, view.y); close(restored.scale, view.scale);
});

test('zoom respects bounds including the fit scale of a very large graph', () => {
    const view = fitView(800, 460, 100000, 4000);
    const minimum = Math.min(0.05, view.scale / 2);
    assert.equal(zoomAt(view, 0.001, { x: 400, y: 230 }, minimum).scale, minimum);
    assert.equal(zoomAt(view, 100000, { x: 400, y: 230 }, minimum).scale, 4);
});

test('100 percent centres the full graph even when larger than the canvas', () => {
    assert.deepEqual(centeredView(800, 460, 2000, 1000, 1), { x: -600, y: -270, scale: 1 });
});

test('changing direction only changes the flowchart header', () => {
    const original = 'flowchart LR\n  n0["flowchart LR in a label"]\n  n0 -.-> n1\n';
    const vertical = withLayout(original, 'TB');
    assert.equal(vertical, 'flowchart TB\n  n0["flowchart LR in a label"]\n  n0 -.-> n1\n');
    assert.equal(withLayout(vertical, 'LR'), original);
    assert.throws(() => withLayout(original, 'TB\n arbitrary code'), /Unsupported/);
});
