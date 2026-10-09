// Reduce raster work under sustained load; CSS geometry and simulation stay unchanged.
export function adaptiveResolution(base, level) {
  const scales = [1, 0.85, 0.7, 0.6];
  const scale = scales[Math.max(0, Math.min(3, Math.floor(level)))];
  return Math.min(base, Math.max(0.75, base * scale));
}
