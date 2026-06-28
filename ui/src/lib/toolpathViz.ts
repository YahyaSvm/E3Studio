import { useStore } from '@/store/useStore'

export function applyToolpathVisualization(operationId: string, data: any) {
  if (!data?.points?.length) return
  const flat: number[] = []
  for (const p of data.points) flat.push(p[0], p[1], p[2])
  const types = new Uint8Array((data.types ?? []).map((v: number) => v))
  useStore.getState().setToolpathVisualization({
    operationId,
    points: new Float32Array(flat),
    types,
    visible: true,
  })
}
