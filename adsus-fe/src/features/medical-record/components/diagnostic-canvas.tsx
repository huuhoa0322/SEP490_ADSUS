import { useEffect, useRef, useState, type MouseEvent as ReactMouseEvent } from "react";
import { apiClient, getApiErrorMessage } from "@/lib/api-client";
import { Loader2, AlertCircle, CheckCircle2, X } from "lucide-react";
import { cn } from "@/lib/utils";
import { useDiagnosticStore, type AiDetection, type Lesion, type Point } from "../stores/use-diagnostic-store";
import { checkIntersection, generateBurntImage } from "../utils/canvas-utils";

interface DiagnosticCanvasProps {
  caseId: string;
  file: File;
  onConfirm: () => void;
}

interface PanZoomController {
  fit: () => void;
  zoomIn: () => void;
  zoomOut: () => void;
  reset: () => void;
  destroy: () => void;
}

const getErrorMessage = (err: unknown): string =>
  err instanceof Error ? err.message : String(err);

export function DiagnosticCanvas({ caseId, file, onConfirm }: DiagnosticCanvasProps) {
  const { currentIndex, aiResults, isProcessing, setAiResult, setIsProcessing, drafts, setDraft } = useDiagnosticStore();
  
  const cachedResult = aiResults[currentIndex];
  const isAnalyzing = isProcessing[currentIndex] || false;
  const sessionId = cachedResult?.sessionId || null;
  const aiDetections = cachedResult?.detections || [];

  const [isConfirming, setIsConfirming] = useState(false);
  
  const [imgUrl, setImgUrl] = useState<string>('');
  const [imgDims, setImgDims] = useState({ w: 0, h: 0 });

  const currentDraft = drafts[currentIndex] || { lesions: [], note: "" };
  const lesions = currentDraft.lesions;
  const note = currentDraft.note;

  const setLesions = (newLesions: Lesion[] | ((prev: Lesion[]) => Lesion[])) => {
    const resolved = typeof newLesions === 'function' ? newLesions(lesions) : newLesions;
    setDraft(currentIndex, { ...currentDraft, lesions: resolved });
  };

  const setNote = (newNote: string) => {
    setDraft(currentIndex, { ...currentDraft, note: newNote });
  };

  const [addingMode, setAddingMode] = useState(false);
  const [addingClicks, setAddingClicks] = useState<Point[]>([]);
  
  const [toastMessage, setToastMessage] = useState<{type: 'error' | 'success', text: string} | null>(null);

  const showToast = (type: 'error' | 'success', text: string) => {
    setToastMessage({ type, text });
    setTimeout(() => setToastMessage(null), 3500);
  };

  // Refs for DOM nodes
  const aiWrapRef = useRef<HTMLDivElement>(null);
  const editWrapRef = useRef<HTMLDivElement>(null);
  
  // Zoom info state
  const [aiZoom, setAiZoom] = useState("—");
  const [editZoom, setEditZoom] = useState("—");
  
  // Expose PZ controllers to React scope
  const aiPzRef = useRef<PanZoomController | null>(null);
  const editPzRef = useRef<PanZoomController | null>(null);

  // Reset per-image UI state when `file` changes — done during render (not in the effect
  // below) so it doesn't trigger an extra synchronous re-render just to reset constants.
  const [resetForFile, setResetForFile] = useState(file);
  if (resetForFile !== file) {
    setResetForFile(file);
    setAiZoom("Fit");
    setEditZoom("Fit");
    setAddingMode(false);
    setAddingClicks([]);
  }

  useEffect(() => {
    if (!file) return;
    const url = URL.createObjectURL(file);
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setImgUrl(url);

    const img = new Image();
    img.onload = () => {
      setImgDims({ w: img.width, h: img.height });
    };
    img.src = url;

    return () => URL.revokeObjectURL(url);
  }, [file]);

  const handleRunAi = async () => {
    setIsProcessing(currentIndex, true);
    try {
      const formData = new FormData();
      formData.append("image", file);

      const res = await apiClient.post(`/api/v1/cases/${caseId}/analyze`, formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });

      if (res.data.code === 200 && res.data.data) {
        const payload = res.data.data;
        setAiResult(currentIndex, {
          sessionId: payload.session_id || 'completed',
          detections: payload.detections || []
        });
      } else {
        setAiResult(currentIndex, { sessionId: 'failed', detections: [], error: res.data.message });
        showToast('error', "Kết nối tới model AI thất bại");
      }
    } catch (err) {
      setAiResult(currentIndex, { sessionId: 'failed', detections: [], error: getApiErrorMessage(err, "Lỗi hệ thống") });
      showToast('error', "Kết nối tới model AI thất bại");
    } finally {
      setIsProcessing(currentIndex, false);
    }
  };

  // Sync lesions when AI result arrives and draft doesn't exist yet
  useEffect(() => {
    if (cachedResult && cachedResult.sessionId !== 'failed' && cachedResult.detections && !drafts[currentIndex]) {
      const initialLesions = cachedResult.detections.map((d: AiDetection, i: number) => ({
        pair_a: d.suggested_calipers.pair_a.map(([x, y]: number[]) => ({ x, y })),
        pair_b: d.suggested_calipers.pair_b.map(([x, y]: number[]) => ({ x, y })),
        source: 'ai' as const,
        ai_detection_index: i,
        rejected: false,
        isValid: true,
      }));
      setDraft(currentIndex, { lesions: initialLesions, note: "" });
    }
  }, [cachedResult, drafts, currentIndex, setDraft]);

  // SVG DOM builder functions
  const clamp = (v: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, v));

  const svgPoint = (svg: SVGSVGElement, evt: PointerEvent | MouseEvent) => {
    const r = svg.getBoundingClientRect();
    return {
      x: (evt.clientX - r.left) * (imgDims.w / r.width),
      y: (evt.clientY - r.top)  * (imgDims.h / r.height),
    };
  };

  const svgEl = (tag: string, attrs: Record<string, string | number>) => {
    const el = document.createElementNS('http://www.w3.org/2000/svg', tag);
    for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, String(v));
    return el;
  };

  const makeSVG = () => {
    const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    s.setAttribute('viewBox', `0 0 ${imgDims.w} ${imgDims.h}`);
    s.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    s.style.position = 'absolute';
    s.style.top = '0';
    s.style.left = '0';
    s.style.width = '100%';
    s.style.height = '100%';
    s.style.overflow = 'visible';
    return s;
  };

  // Pan Zoom Implementation (Vanilla JS injected)
  const makePanZoom = (wrap: HTMLDivElement, setZoomText: (t: string) => void, isEdit: boolean) => {
    let zoom = 1, tx = 0, ty = 0;
    let active = false, startX = 0, startY = 0, startTx = 0, startTy = 0, moved = false;

    const inner = () => wrap.querySelector('.canvas-inner') as HTMLDivElement;

    const applyTransform = () => {
      const el = inner();
      if (el) el.style.transform = `translate(${tx}px,${ty}px) scale(${zoom})`;
      setZoomText(Math.round(zoom * 100) + '%');
    };

    const fit = () => {
      const el = inner();
      if (!el) return;
      const ww = wrap.clientWidth, wh = wrap.clientHeight;
      const iw = el.offsetWidth,  ih = el.offsetHeight;
      if (!iw || !ih) return;
      zoom = Math.min(ww / iw, wh / ih) * 0.93;
      tx = (ww - iw * zoom) / 2;
      ty = (wh - ih * zoom) / 2;
      applyTransform();
    };

    const zoomAt = (cx: number, cy: number, factor: number) => {
      const el = inner();
      if (!el) return;
      const nz = Math.max(0.08, Math.min(20, zoom * factor));
      tx = cx - (cx - tx) / zoom * nz;
      ty = cy - (cy - ty) / zoom * nz;
      zoom = nz;
      applyTransform();
    };

    const handleWheel = (e: WheelEvent) => {
      e.preventDefault();
      const r = wrap.getBoundingClientRect();
      zoomAt(e.clientX - r.left, e.clientY - r.top, e.deltaY < 0 ? 1.12 : 1 / 1.12);
    };

    const handlePointerDown = (e: PointerEvent) => {
      if ((e.target as Element).closest('.caliper-point, .caliper-line, [data-reject-for], line, path, circle')) return;
      active = true; moved = false;
      startX = e.clientX; startY = e.clientY;
      startTx = tx; startTy = ty;
      wrap.setPointerCapture(e.pointerId);
      wrap.style.cursor = 'grabbing';
    };

    const handlePointerMove = (e: PointerEvent) => {
      if (!active) return;
      const dx = e.clientX - startX, dy = e.clientY - startY;
      if (Math.abs(dx) > 3 || Math.abs(dy) > 3) moved = true;
      if (!moved) return;
      tx = startTx + dx;
      ty = startTy + dy;
      applyTransform();
    };

    const endPan = () => {
      active = false;
      // Cursor logic will be overwritten by adding mode
      wrap.style.cursor = isEdit && document.body.getAttribute('data-adding') === 'true' ? 'crosshair' : 'grab';
    };

    const handleClick = (e: MouseEvent) => {
      if (moved) { moved = false; e.stopImmediatePropagation(); }
    };

    wrap.addEventListener('wheel', handleWheel, { passive: false });
    wrap.addEventListener('pointerdown', handlePointerDown);
    wrap.addEventListener('pointermove', handlePointerMove);
    wrap.addEventListener('pointerup', endPan);
    wrap.addEventListener('pointercancel', endPan);
    wrap.addEventListener('click', handleClick, true);

    const centerZoom = (f: number) => {
      const r = wrap.getBoundingClientRect();
      zoomAt(r.width / 2, r.height / 2, f);
    };

    return {
      fit,
      zoomIn: () => centerZoom(1.25),
      zoomOut: () => centerZoom(1 / 1.25),
      reset: fit,
      destroy: () => {
        wrap.removeEventListener('wheel', handleWheel);
        wrap.removeEventListener('pointerdown', handlePointerDown);
        wrap.removeEventListener('pointermove', handlePointerMove);
        wrap.removeEventListener('pointerup', endPan);
        wrap.removeEventListener('pointercancel', endPan);
        wrap.removeEventListener('click', handleClick, true);
      }
    };
  };

  // Re-render logic using Vanilla SVG DOM to avoid React performance hits on drag
  useEffect(() => {
    if (!aiWrapRef.current || !imgDims.w) return;
    
    const wrap = aiWrapRef.current;
    
    // Build AI Canvas Inner
    const inner = document.createElement('div');
    inner.className = 'canvas-inner';
    inner.style.position = 'absolute';
    inner.style.transformOrigin = '0 0';
    inner.style.width = imgDims.w + 'px';
    inner.style.height = imgDims.h + 'px';
    
    const img = new Image(imgDims.w, imgDims.h);
    img.src = imgUrl;
    img.draggable = false;
    img.style.display = 'block';
    
    inner.appendChild(img);
    wrap.innerHTML = '';
    wrap.appendChild(inner);

    const svg = makeSVG();
    const fontSize = Math.max(18, Math.round(imgDims.h * 0.05));
    const padX = 6, padY = 4;

    aiDetections.forEach(d => {
      const b = {
        x1: d.bbox.xmin * imgDims.w,
        y1: d.bbox.ymin * imgDims.h,
        x2: d.bbox.xmax * imgDims.w,
        y2: d.bbox.ymax * imgDims.h,
      };
      const confStr = (d.confidence * 100).toFixed(0) + '%';

      const rect = svgEl('rect', {
        x: b.x1, y: b.y1,
        width: b.x2 - b.x1, height: b.y2 - b.y1,
        fill: 'rgba(232, 147, 74, 0.16)',
        stroke: '#e8934a',
        'stroke-width': 2,
      });
      svg.appendChild(rect);

      const lblH = fontSize + padY * 2;
      const lblW = confStr.length * fontSize * 0.62 + padX * 2;
      const lblTop = Math.max(b.y1 - lblH - 2, 0);

      const bg = svgEl('rect', {
        x: b.x1, y: lblTop, width: lblW, height: lblH,
        fill: '#000000cc', rx: 4,
      });
      svg.appendChild(bg);

      const lbl = svgEl('text', {
        x: b.x1 + padX, y: lblTop + fontSize + padY * 0.5,
        fill: '#e8934a',
        'font-size': fontSize,
        'font-weight': 'bold',
        'font-family': 'monospace',
      });
      lbl.textContent = confStr;
      svg.appendChild(lbl);
    });

    inner.appendChild(svg);

    // Setup PZ
    if (aiPzRef.current) aiPzRef.current.destroy();
    const pz = makePanZoom(wrap, setAiZoom, false);
    aiPzRef.current = pz;
    
    // Use requestAnimationFrame to fit after layout
    requestAnimationFrame(() => requestAnimationFrame(() => pz.fit()));

    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [imgDims, aiDetections, imgUrl]);

  // Make elements draggable
  const makeDraggable = (
    el: Element,
    svg: SVGSVGElement,
    onPointMove: ((x: number, y: number) => void) | null,
    onDeltaMove: ((dx: number, dy: number) => void) | null
  ) => {
    let active = false, lastPt: Point | null = null;
    const pd = (e: PointerEvent) => {
      active = true; lastPt = svgPoint(svg, e);
      el.setPointerCapture(e.pointerId);
      el.classList.add('dragging');
      e.stopPropagation();
    };
    const pm = (e: PointerEvent) => {
      if (!active || !lastPt) return;
      const pt = svgPoint(svg, e);
      if (onPointMove) onPointMove(pt.x, pt.y);
      else if (onDeltaMove) { onDeltaMove(pt.x - lastPt.x, pt.y - lastPt.y); lastPt = pt; }
    };
    const end = () => { active = false; el.classList.remove('dragging'); };
    // Element's own addEventListener overloads don't include PointerEvent — cast to the
    // HTMLElement one, which SVG elements satisfy at runtime (they support pointer capture).
    const elWithPointerEvents = el as unknown as HTMLElement;
    elWithPointerEvents.addEventListener('pointerdown', pd);
    elWithPointerEvents.addEventListener('pointermove', pm);
    elWithPointerEvents.addEventListener('pointerup', end);
    elWithPointerEvents.addEventListener('pointercancel', end);
  };

  // Re-render Edit Canvas
  useEffect(() => {
    if (!editWrapRef.current || !imgDims.w) return;
    const wrap = editWrapRef.current;
    
    // Make inner if missing
    let inner = wrap.querySelector('.canvas-inner') as HTMLDivElement;
    if (!inner) {
      inner = document.createElement('div');
      inner.className = 'canvas-inner';
      inner.style.position = 'absolute';
      inner.style.transformOrigin = '0 0';
      
      const img = document.createElement('img'); // No width/height yet
      img.draggable = false;
      img.style.display = 'block';
      
      inner.appendChild(img);
      wrap.appendChild(inner);

      if (editPzRef.current) editPzRef.current.destroy();
      const pz = makePanZoom(wrap, setEditZoom, true);
      editPzRef.current = pz;
      requestAnimationFrame(() => requestAnimationFrame(() => pz.fit()));
    }

    // Always ensure size and src are correct
    inner.style.width = imgDims.w + 'px';
    inner.style.height = imgDims.h + 'px';
    const imgEl = inner.querySelector('img') as HTMLImageElement;
    if (imgEl && imgEl.src !== imgUrl) {
       imgEl.src = imgUrl;
    }
    if (imgEl) {
       imgEl.width = imgDims.w;
       imgEl.height = imgDims.h;
    }

    // Clean old SVG
    inner.querySelectorAll('svg').forEach(e => e.remove());
    
    const svg = makeSVG();
    svg.id = 'editSVG';

    const MARKER_SIZE = 5;
    const STROKE_W    = 2.5;
    const RING_R      = 7;
    const HIT_R       = 18;

    const markerPath = (shape: string, sz: number) => {
      return shape === '+'
        ? `M${-sz} 0 L${sz} 0 M 0 ${-sz} L 0 ${sz}`
        : `M${-sz} ${-sz} L${sz} ${sz} M${-sz} ${sz} L${sz} ${-sz}`;
    };

    const drawPair = (lesion: Lesion, lesionIdx: number, pairKey: 'pair_a' | 'pair_b', markerShape: string) => {
      const pair = lesion[pairKey];
      const line = svgEl('line', {
        x1: pair[0].x, y1: pair[0].y, x2: pair[1].x, y2: pair[1].y,
        stroke: '#00ff00', 'stroke-width': STROKE_W,
        'stroke-dasharray': '14 10', class: 'caliper-line',
      });
      svg.appendChild(line);

      const lineHit = svgEl('line', {
        x1: pair[0].x, y1: pair[0].y, x2: pair[1].x, y2: pair[1].y,
        stroke: 'transparent', 'stroke-width': 16,
      });
      (lineHit as unknown as HTMLElement).style.cursor = 'move';
      svg.appendChild(lineHit);

      const pointEls = pair.map((pt: Point) => {
        const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        g.setAttribute('class', 'caliper-point');
        g.setAttribute('transform', `translate(${pt.x},${pt.y})`);
        
        const ring = svgEl('circle', {
          r: RING_R, fill: 'none', stroke: '#00ff00', 'stroke-width': 0, opacity: 0.45,
        });
        g.appendChild(ring);
        
        const marker = svgEl('path', {
          d: markerPath(markerShape, MARKER_SIZE),
          stroke: '#00ff00', 'stroke-width': 2.0, 'stroke-linecap': 'round',
        });
        g.appendChild(marker);
        g.appendChild(svgEl('circle', { r: HIT_R, fill: 'transparent' }));

        g.addEventListener('pointerenter', () => ring.setAttribute('stroke-width', '2'));
        g.addEventListener('pointerleave', () => {
          if (!g.classList.contains('dragging')) ring.setAttribute('stroke-width', '0');
        });
        svg.appendChild(g);
        return g;
      });

      // Drag point
      pair.forEach((_: Point, ptIdx: number) => {
        const g = pointEls[ptIdx];
        makeDraggable(g, svg, (rawX: number, rawY: number) => {
          const nx = clamp(rawX, 0, imgDims.w);
          const ny = clamp(rawY, 0, imgDims.h);
          
          // MUTATE STATE DIRECTLY FOR DRAG PERFORMANCE
          lesion[pairKey][ptIdx] = { x: nx, y: ny };
          g.setAttribute('transform', `translate(${nx},${ny})`);
          
          const s = ptIdx === 0 ? '1' : '2';
          line.setAttribute('x' + s, nx.toString()); line.setAttribute('y' + s, ny.toString());
          lineHit.setAttribute('x' + s, nx.toString()); lineHit.setAttribute('y' + s, ny.toString());
          
          updateRejectBtnPos(svg, lesion, lesionIdx);
          checkAndShowIntersectionWarning();
        }, null);
      });

      // Drag line
      makeDraggable(lineHit, svg, null, (dx: number, dy: number) => {
        const p0 = lesion[pairKey][0], p1 = lesion[pairKey][1];
        const nx0 = clamp(p0.x + dx, 0, imgDims.w), ny0 = clamp(p0.y + dy, 0, imgDims.h);
        const nx1 = clamp(p1.x + dx, 0, imgDims.w), ny1 = clamp(p1.y + dy, 0, imgDims.h);
        
        lesion[pairKey] = [{ x: nx0, y: ny0 }, { x: nx1, y: ny1 }];
        line.setAttribute('x1', nx0.toString()); line.setAttribute('y1', ny0.toString());
        line.setAttribute('x2', nx1.toString()); line.setAttribute('y2', ny1.toString());
        lineHit.setAttribute('x1', nx0.toString()); lineHit.setAttribute('y1', ny0.toString());
        lineHit.setAttribute('x2', nx1.toString()); lineHit.setAttribute('y2', ny1.toString());
        pointEls[0].setAttribute('transform', `translate(${nx0},${ny0})`);
        pointEls[1].setAttribute('transform', `translate(${nx1},${ny1})`);
        
        updateRejectBtnPos(svg, lesion, lesionIdx);
        checkAndShowIntersectionWarning();
      });
    };

    const lesionCentroid = (lesion: Lesion) => {
      const pts = [...lesion.pair_a, ...lesion.pair_b];
      return {
        cx: pts.reduce((s: number, p: Point) => s + p.x, 0) / 4,
        cy: pts.reduce((s: number, p: Point) => s + p.y, 0) / 4,
      };
    };

    const updateRejectBtnPos = (svg: SVGSVGElement, lesion: Lesion, lesionIdx: number) => {
      const g = svg.querySelector(`[data-reject-for="${lesionIdx}"]`);
      if (!g) return;
      const { cx, cy } = lesionCentroid(lesion);
      g.setAttribute('transform', `translate(${cx + 34},${cy - 34})`);
    };

    const drawRejectButton = (svg: SVGSVGElement, lesion: Lesion, lesionIdx: number) => {
      const { cx, cy } = lesionCentroid(lesion);
      const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
      g.setAttribute('transform', `translate(${cx + 34},${cy - 34})`);
      g.setAttribute('data-reject-for', lesionIdx.toString());
      g.style.cursor = 'pointer';
      g.appendChild(svgEl('circle', { r: 11, fill: '#e8564a', opacity: 0.9 }));
      const lbl = svgEl('text', {
        x: 0, y: 4, 'text-anchor': 'middle', fill: '#fff', 'font-size': 14,
      });
      lbl.textContent = '×';
      g.appendChild(lbl);
      g.addEventListener('click', e => { 
        e.stopPropagation(); 
        // Force React re-render for reject
        setLesions(prev => {
          const next = [...prev];
          next[lesionIdx].rejected = true;
          return next;
        });
      });
      svg.appendChild(g);
    };

    const checkAndShowIntersectionWarning = () => {
      // Direct DOM warning logic can go here if needed
      // But we also update state for the save button
      const newLesions = [...lesions];
      newLesions.forEach(l => {
        if (!l.rejected) {
          l.isValid = checkIntersection(l.pair_a, l.pair_b);
        }
      });
      // We don't call setLesions here during drag to avoid lag, 
      // but the state mutated array will be used on Save.
    };

    lesions.forEach((lesion, lesionIdx) => {
      if (lesion.rejected) return;
      drawPair(lesion, lesionIdx, 'pair_a', '+');
      drawPair(lesion, lesionIdx, 'pair_b', 'x');
      drawRejectButton(svg, lesion, lesionIdx);
    });

    // Draw temporary adding points
    addingClicks.forEach((pt, idx) => {
      const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
      g.setAttribute('transform', `translate(${pt.x},${pt.y})`);
      const markerShape = idx < 2 ? '+' : 'x';
      const marker = svgEl('path', {
        d: markerPath(markerShape, MARKER_SIZE),
        stroke: '#00ff00', 'stroke-width': 2.0, 'stroke-linecap': 'round',
      });
      g.appendChild(marker);
      svg.appendChild(g);
    });

    inner.appendChild(svg);
    checkAndShowIntersectionWarning();

    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lesions, imgDims, addingClicks]);

  // Adding caliper logic
  const toggleAdding = () => {
    setAddingMode(!addingMode);
    setAddingClicks([]);
    document.body.setAttribute('data-adding', (!addingMode).toString());
    if (editWrapRef.current) {
      editWrapRef.current.style.cursor = !addingMode ? 'crosshair' : 'grab';
    }
  };

  const handleEditWrapClick = (e: ReactMouseEvent<HTMLDivElement>) => {
    if (!addingMode) return;
    const svg = editWrapRef.current?.querySelector('svg');
    if (!svg) return;
    if ((e.target as HTMLElement).closest('[data-reject-for]')) return;

    const pt = svgPoint(svg, e as unknown as MouseEvent);
    const newClicks = [...addingClicks, pt];
    setAddingClicks(newClicks);
    
    if (newClicks.length === 4) {
      const [a1, a2, b1, b2] = newClicks;
      setLesions(prev => [...prev, {
        pair_a: [a1, a2], pair_b: [b1, b2], source: 'doctor_added', rejected: false, isValid: true
      }]);
      setAddingMode(false);
      setAddingClicks([]);
      document.body.setAttribute('data-adding', 'false');
      if (editWrapRef.current) editWrapRef.current.style.cursor = 'grab';
    }
  };

  const handleConfirm = async () => {
    // Validate
    const confirmedLesions = lesions.filter(l => !l.rejected);
    const hasError = confirmedLesions.some(l => !checkIntersection(l.pair_a, l.pair_b));
    if (hasError) {
      showToast('error', "⚠️ Có thước đo chưa cắt nhau (chưa tạo thành hình). Vui lòng điều chỉnh trước khi lưu!");
      return;
    }

    setIsConfirming(true);
    try {
      const burntFile = await generateBurntImage(file, confirmedLesions);
      if (!burntFile) throw new Error("Không thể tạo burnt image");

      // Calculate BBox for C# backend from calipers (normalize to 0-1)
      const doctorBboxes = confirmedLesions.map(l => {
        const pts = [...l.pair_a, ...l.pair_b];
        const xs = pts.map(p => p.x / imgDims.w);
        const ys = pts.map(p => p.y / imgDims.h);
        return {
          xmin: Math.min(...xs),
          ymin: Math.min(...ys),
          xmax: Math.max(...xs),
          ymax: Math.max(...ys),
          confidence: 1.0
        };
      });

      // Format AI BBox correctly for C# backend
      const mappedAiBboxes = aiDetections.map(d => ({
        xmin: d.bbox.xmin,
        ymin: d.bbox.ymin,
        xmax: d.bbox.xmax,
        ymax: d.bbox.ymax,
        confidence: d.confidence
      }));

      const formData = new FormData();
      formData.append("OriginalImage", file);
      formData.append("BurntImage", burntFile);
      formData.append("AiPredictionsJson", JSON.stringify(mappedAiBboxes));
      formData.append("DoctorAnnotationsJson", JSON.stringify(doctorBboxes));
      formData.append("ModelVersionId", "00000000-0000-0000-0000-000000000000");
      if (note.trim()) {
        formData.append("Note", note.trim());
      }

      const res = await apiClient.post(`/api/v1/cases/${caseId}/images/confirm`, formData, {
        headers: { "Content-Type": "multipart/form-data" },
        timeout: 60000, // Supabase uploads can take longer than the default 15s
      });

      if (res.data.code === 200) {
        showToast('success', "Đã chốt ảnh thành công!");
        onConfirm(); // Trigger next image
      } else {
        showToast('error', res.data.message);
      }
    } catch (err) {
      showToast('error', "Lỗi lưu ảnh: " + getErrorMessage(err));
    } finally {
      setIsConfirming(false);
    }
  };

  return (
    <div className="flex flex-1 overflow-hidden bg-background text-foreground">
      <style dangerouslySetInnerHTML={{__html: `
        .canvas-inner { position: absolute; transform-origin: 0 0; }
        .canvas-inner img { display: block; }
        .canvas-inner svg { position: absolute; top: 0; left: 0; width: 100%; height: 100%; overflow: visible; }
        .caliper-point { cursor: grab; }
        .caliper-point.dragging { cursor: grabbing; }
        .caliper-line { pointer-events: none; }
      `}} />

      {/* AI Panel (40%) */}
      <section className="flex flex-col border-r border-border" style={{ flex: '4 4 40%', maxWidth: '40%' }}>
        <div className="flex shrink-0 items-center gap-2 border-b border-border px-3 py-2 text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
          <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-[#e8934a]"></span>
          Kết quả AI phát hiện
        </div>
        <div className="flex shrink-0 flex-wrap items-center gap-2 border-b border-border px-3 py-2">
          
          <button
            onClick={handleRunAi}
            disabled={isAnalyzing}
            className="rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-indigo-700 disabled:opacity-50 flex items-center gap-1"
          >
            {isAnalyzing ? <Loader2 className="h-3 w-3 animate-spin" /> : null}
            Chạy AI
          </button>
                    <span 
              className={`text-[13px] font-bold uppercase tracking-wide ${
                sessionId === 'failed'
                  ? 'text-red-500'
                  : sessionId 
                    ? (aiDetections.length > 0 ? 'text-red-500' : 'text-green-500') 
                    : 'text-amber-500'
              }`}
            >
              {sessionId === 'failed'
                ? 'Kết nối tới model AI thất bại'
                : sessionId 
                  ? (aiDetections.length > 0 ? `Có ${aiDetections.length} vùng abnormal` : 'Không có vùng abnormal')
                  : (isAnalyzing ? 'Đang phân tích...' : 'Chưa phân tích')}
            </span>
          
          <div className="ml-auto flex items-center gap-1">
            <button className="flex h-6 w-6 items-center justify-center rounded bg-background text-foreground border border-border hover:bg-accent" onClick={() => aiPzRef.current?.zoomOut()}>−</button>
            <span className="min-w-[38px] text-center font-mono text-[11px] text-muted-foreground">{aiZoom}</span>
            <button className="flex h-6 w-6 items-center justify-center rounded bg-background text-foreground border border-border hover:bg-accent" onClick={() => aiPzRef.current?.zoomIn()}>+</button>
            <button className="px-2 h-6 text-[11px] rounded bg-background text-foreground border border-border hover:bg-accent" onClick={() => aiPzRef.current?.reset()}>↺ Fit</button>
          </div>
        </div>
        
        <div className="relative flex-1 overflow-hidden bg-muted/20 select-none cursor-grab" ref={aiWrapRef}>
          {!imgDims.w && (
            <div className="absolute inset-0 flex items-center justify-center text-[13px] text-muted-foreground">
              Đang tải ảnh...
            </div>
          )}
        </div>
      </section>

      {/* Edit Panel (60%) */}
      <section className="flex flex-col" style={{ flex: '6 6 60%', maxWidth: '60%' }}>
        <div className="flex shrink-0 items-center gap-2 border-b border-border px-3 py-2 text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
          <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-[#00ff00]"></span>
          Ảnh gốc — xác nhận / chỉnh sửa caliper
        </div>
        
        <div className="flex shrink-0 flex-wrap items-center gap-2 border-b border-border px-3 py-2">
          <button 
            onClick={toggleAdding}
            className={`rounded-md border px-3 py-1.5 text-xs font-semibold transition-colors ${
              addingMode ? 'bg-[#00ff00] text-black border-[#00ff00]' : 'border-border text-foreground hover:bg-accent'
            }`}
          >
            + Thêm caliper
          </button>
          <span className="font-mono text-[11px] text-muted-foreground">
            {addingMode ? `Click 4 điểm: ${addingClicks.length}/4` : ''}
          </span>

          <div className="ml-auto flex items-center gap-1">
            <button className="flex h-6 w-6 items-center justify-center rounded bg-background text-foreground border border-border hover:bg-accent" onClick={() => editPzRef.current?.zoomOut()}>−</button>
            <span className="min-w-[38px] text-center font-mono text-[11px] text-muted-foreground">{editZoom}</span>
            <button className="flex h-6 w-6 items-center justify-center rounded bg-background text-foreground border border-border hover:bg-accent" onClick={() => editPzRef.current?.zoomIn()}>+</button>
            <button className="px-2 h-6 text-[11px] rounded bg-background text-foreground border border-border hover:bg-accent" onClick={() => editPzRef.current?.reset()}>↺ Fit</button>
          </div>
        </div>

        <div 
          className={`relative flex-1 overflow-hidden bg-muted/20 select-none ${addingMode ? 'cursor-crosshair' : 'cursor-grab'}`} 
          ref={editWrapRef}
          onClick={handleEditWrapClick}
        >
          {!imgDims.w && (
            <div className="absolute inset-0 flex items-center justify-center text-[13px] text-muted-foreground">
              Đang tải ảnh...
            </div>
          )}
        </div>

        {/* Footer (Confirm area) */}
        <div className="flex shrink-0 items-center gap-3 border-t border-border bg-card p-3">
          {sessionId && (
            <span className="font-mono text-[12px] font-semibold text-[#00ff00]">
              Sẵn sàng lưu ({lesions.filter(l => !l.rejected).length} vùng)
            </span>
          )}
          
          <input
            type="text"
            placeholder="Ghi chú cho ảnh này (tuỳ chọn)..."
            value={note}
            onChange={(e) => setNote(e.target.value)}
            className="ml-auto h-9 w-64 md:w-80 rounded-md border border-input bg-background px-3 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          />

          <button
            onClick={handleConfirm}
            disabled={isConfirming}
            className="flex items-center gap-2 rounded-md bg-[#00ff00] px-4 py-2 text-[13px] font-bold text-black hover:opacity-80 disabled:opacity-35"
          >
            {isConfirming ? <Loader2 className="h-4 w-4 animate-spin text-black" /> : null}
            Lưu xác nhận
          </button>
        </div>
      </section>

      {/* Custom Modal Notification */}
      {toastMessage && (
        <>
          <div 
            className="fixed inset-0 z-[150] bg-black/20 backdrop-blur-sm animate-in fade-in duration-200" 
            onClick={() => setToastMessage(null)} 
          />
          <div className="fixed top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 z-[200] w-full max-w-md animate-in fade-in zoom-in-95 duration-200">
            <div className="flex flex-col gap-4 rounded-2xl bg-white p-6 shadow-2xl ring-1 ring-slate-900/5">
              <div className="flex items-start gap-4">
                <div className={cn(
                  "flex h-10 w-10 shrink-0 items-center justify-center rounded-full",
                  toastMessage.type === 'error' ? "bg-red-100 text-red-600" : "bg-green-100 text-green-600"
                )}>
                  {toastMessage.type === 'error' ? <AlertCircle className="h-6 w-6" /> : <CheckCircle2 className="h-6 w-6" />}
                </div>
                <div className="flex-1 pt-1">
                  <h3 className={cn(
                    "text-lg font-semibold",
                    toastMessage.type === 'error' ? "text-red-600" : "text-green-600"
                  )}>
                    {toastMessage.type === 'error' ? 'Lưu ý' : 'Thành công'}
                  </h3>
                  <p className="mt-1 text-sm font-medium text-slate-600">
                    {toastMessage.text}
                  </p>
                </div>
                <button 
                  onClick={() => setToastMessage(null)} 
                  className="rounded-md p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 transition-colors"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>
              <div className="flex justify-end gap-3 pt-2">
                <button 
                  onClick={() => setToastMessage(null)} 
                  className="rounded-lg bg-slate-900 px-5 py-2.5 text-sm font-semibold text-white hover:bg-slate-800 transition-colors"
                >
                  Đã hiểu
                </button>
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
