// Coordinates are CSS pixels; the stage transform is translate(x, y) scale(scale).
export function centeredView(width, height, graphWidth, graphHeight, scale) {
    return { x: (width - graphWidth * scale) / 2, y: (height - graphHeight * scale) / 2, scale };
}

export function fitView(width, height, graphWidth, graphHeight, padding = 32) {
    if (width <= 0 || height <= 0 || graphWidth <= 0 || graphHeight <= 0) return null;
    const scale = Math.min(1, Math.max(1, width - padding * 2) / graphWidth,
        Math.max(1, height - padding * 2) / graphHeight);
    return centeredView(width, height, graphWidth, graphHeight, scale);
}

export function zoomAt(view, factor, point, minimum = 0.05, maximum = 4) {
    const scale = Math.min(maximum, Math.max(minimum, view.scale * factor));
    const ratio = scale / view.scale;
    return { x: point.x - (point.x - view.x) * ratio,
        y: point.y - (point.y - view.y) * ratio, scale };
}

export function withLayout(markup, direction) {
    if (!['LR', 'TB'].includes(direction)) throw new Error('Unsupported graph layout.');
    return markup.replace(/^flowchart\s+(LR|TB)\b/, `flowchart ${direction}`);
}
