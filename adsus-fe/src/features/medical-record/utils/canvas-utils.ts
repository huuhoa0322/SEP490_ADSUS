import { Lesion, Point } from "../stores/use-diagnostic-store";

export const checkIntersection = (pair_a: Point[], pair_b: Point[]) => {
  const x1 = pair_a[0].x, y1 = pair_a[0].y, x2 = pair_a[1].x, y2 = pair_a[1].y;
  const x3 = pair_b[0].x, y3 = pair_b[0].y, x4 = pair_b[1].x, y4 = pair_b[1].y;
  const denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
  if (Math.abs(denom) < 1e-6) return false;
  const t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
  const s = ((x1 - x3) * (y1 - y2) - (y1 - y3) * (x1 - x2)) / denom;
  return t >= 0 && t <= 1 && s >= 0 && s <= 1;
};

const MARKER_SIZE = 5;

const drawMarker = (c: CanvasRenderingContext2D, pt: {x: number, y: number}, shape: '+' | 'x') => {
  c.beginPath();
  if (shape === '+') {
    c.moveTo(pt.x - MARKER_SIZE, pt.y);
    c.lineTo(pt.x + MARKER_SIZE, pt.y);
    c.moveTo(pt.x, pt.y - MARKER_SIZE);
    c.lineTo(pt.x, pt.y + MARKER_SIZE);
  } else {
    c.moveTo(pt.x - MARKER_SIZE, pt.y - MARKER_SIZE);
    c.lineTo(pt.x + MARKER_SIZE, pt.y + MARKER_SIZE);
    c.moveTo(pt.x - MARKER_SIZE, pt.y + MARKER_SIZE);
    c.lineTo(pt.x + MARKER_SIZE, pt.y - MARKER_SIZE);
  }
  c.stroke();
};

export const generateBurntImage = async (file: File, lesions: Lesion[]): Promise<File | null> => {
  const url = URL.createObjectURL(file);
  const img = new Image();
  img.src = url;
  
  await new Promise((resolve, reject) => {
    img.onload = resolve;
    img.onerror = reject;
  });

  const canvas = document.createElement('canvas');
  canvas.width = img.width;
  canvas.height = img.height;
  const ctx = canvas.getContext('2d');
  if (!ctx) {
    URL.revokeObjectURL(url);
    return null;
  }

  ctx.drawImage(img, 0, 0);

  // Draw calipers
  ctx.strokeStyle = '#00ff00';
  ctx.lineWidth = 2.5;

  lesions.filter(l => !l.rejected).forEach(l => {
    // Draw lines (dashed)
    ctx.setLineDash([14, 10]);
    ctx.beginPath();
    ctx.moveTo(l.pair_a[0].x, l.pair_a[0].y);
    ctx.lineTo(l.pair_a[1].x, l.pair_a[1].y);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(l.pair_b[0].x, l.pair_b[0].y);
    ctx.lineTo(l.pair_b[1].x, l.pair_b[1].y);
    ctx.stroke();

    // Draw markers (solid)
    ctx.setLineDash([]);
    drawMarker(ctx, l.pair_a[0], '+');
    drawMarker(ctx, l.pair_a[1], '+');
    drawMarker(ctx, l.pair_b[0], 'x');
    drawMarker(ctx, l.pair_b[1], 'x');
  });

  const blob = await new Promise<Blob | null>(r => canvas.toBlob(r, 'image/jpeg', 0.95));
  URL.revokeObjectURL(url);
  
  if (!blob) return null;
  return new File([blob], `burnt_${file.name}`, { type: "image/jpeg" });
};
