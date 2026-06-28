// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: UI :: Store
// Zustand ile global state yönetimi.
// Tüm UI state buradan okunur/yazılır.
// ─────────────────────────────────────────────────────────────────────────────
import { create } from 'zustand'
import { immer } from 'zustand/middleware/immer'

// ─── Tipler ──────────────────────────────────────────────────────────────────
export interface Tool {
  id: string
  name: string
  diameter: number
  type: 'FlatEndmill' | 'BallEndmill' | 'BullNose' | 'Drill'
}

export interface Operation {
  id: string
  name: string
  type: string
  toolId: string
  feedrateXY: number
  feedrateZ: number
  spindleSpeed: number
  depthOfCut: number
  stepover: number
  isDirty: boolean
  toolpathId?: string
}

export interface Model {
  id: string
  filePath: string
  role: 'workpiece' | 'stock' | 'fixture'
  meshBuffer?: Float32Array
  bbox?: { min: [number,number,number], max: [number,number,number] }
}

export interface ToolpathVisualization {
  operationId: string
  points: Float32Array    // XYZ düz liste
  types: Uint8Array       // hareket tipi (0=rapid,1=feed)
  visible: boolean
}

export interface AIPrediction {
  operationId: string
  feedrate: number
  spindleSpeed: number
  predictedRoughness: number
  toolLifeMinutes: number
  confidence: number
}

export interface SimulationState {
  isRunning: boolean
  progress: number
  remainingMaterial: number
}

export interface UIState {
  // Proje
  projectName: string
  projectPath: string | null
  hasProject: boolean

  // Modeller
  models: Model[]
  selectedModelId: string | null

  // Operasyonlar
  operations: Operation[]
  selectedOperationId: string | null

  // Takım kütüphanesi
  tools: Tool[]

  // Toolpath görselleştirme
  toolpaths: Record<string, ToolpathVisualization>

  // Simülasyon
  simulation: SimulationState

  // AI
  aiPredictions: Record<string, AIPrediction>

  // UI
  activePanel: 'operations' | 'tools' | 'simulation' | 'export' | 'ai'
  viewportMode: '3d' | 'simulation'
  isLoading: boolean
  loadingMessage: string
  notifications: Array<{id: string, type: 'info'|'success'|'warning'|'error', message: string}>

  // Actions
  setProjectName: (name: string) => void
  addTool: (tool: Tool) => void
  removeTool: (id: string) => void
  setProjectFromServer: (data: any) => void
  addModel: (model: Model) => void
  addOperation: (op: Operation) => void
  updateOperation: (id: string, updates: Partial<Operation>) => void
  removeOperation: (id: string) => void
  selectOperation: (id: string | null) => void
  setToolpathVisualization: (tp: ToolpathVisualization) => void
  setAIPrediction: (pred: AIPrediction) => void
  setSimulation: (sim: Partial<SimulationState>) => void
  setActivePanel: (panel: UIState['activePanel']) => void
  setViewportMode: (mode: UIState['viewportMode']) => void
  setLoading: (loading: boolean, message?: string) => void
  pushNotification: (type: 'info'|'success'|'warning'|'error', message: string) => void
  dismissNotification: (id: string) => void
}

let notifId = 0

export const useStore = create<UIState>()(
  immer((set) => ({
    projectName: '',
    projectPath: null,
    hasProject: false,
    models: [],
    selectedModelId: null,
    operations: [],
    selectedOperationId: null,
    tools: [],
    toolpaths: {},
    simulation: { isRunning: false, progress: 0, remainingMaterial: 100 },
    aiPredictions: {},
    activePanel: 'operations',
    viewportMode: '3d',
    isLoading: false,
    loadingMessage: '',
    notifications: [],

    setProjectName: (name) => set((s) => { s.projectName = name }),

    addTool: (tool) => set((s) => { s.tools.push(tool) }),

    removeTool: (id) => set((s) => { s.tools = s.tools.filter(t => t.id !== id) }),

    setProjectFromServer: (data) => set((s) => {
      s.projectName = data.name ?? s.projectName
      s.hasProject = true
      s.tools = (data.toolLibrary ?? []).map((t: any) => ({
        id: t.id,
        name: t.name,
        diameter: t.diameter ?? 6,
        type: t.typeName ?? 'FlatEndmill',
      }))
      s.operations = (data.operations ?? []).map((op: any) => ({
        id: op.id,
        name: op.name,
        type: op.typeName ?? 'Pocket2D',
        toolId: op.toolId ?? '',
        feedrateXY: op.feedrateXY ?? 1200,
        feedrateZ: op.feedrateZ ?? 400,
        spindleSpeed: op.spindleSpeed ?? 8000,
        depthOfCut: op.depthOfCut ?? 2,
        stepover: op.stepover ?? 4,
        isDirty: op.isDirty ?? true,
        toolpathId: op.toolpathId,
      }))
    }),

    addModel: (model) => set((s) => { s.models.push(model) }),

    addOperation: (op) => set((s) => { s.operations.push(op) }),

    updateOperation: (id, updates) => set((s) => {
      const op = s.operations.find(o => o.id === id)
      if (op) Object.assign(op, updates, { isDirty: true })
    }),

    removeOperation: (id) => set((s) => {
      s.operations = s.operations.filter(o => o.id !== id)
    }),

    selectOperation: (id) => set((s) => { s.selectedOperationId = id }),

    setToolpathVisualization: (tp) => set((s) => { s.toolpaths[tp.operationId] = tp }),

    setAIPrediction: (pred) => set((s) => { s.aiPredictions[pred.operationId] = pred }),

    setSimulation: (sim) => set((s) => { Object.assign(s.simulation, sim) }),

    setActivePanel: (panel) => set((s) => { s.activePanel = panel }),

    setViewportMode: (mode) => set((s) => { s.viewportMode = mode }),

    setLoading: (loading, message = '') => set((s) => {
      s.isLoading = loading
      s.loadingMessage = message
    }),

    pushNotification: (type, message) => set((s) => {
      s.notifications.push({ id: String(++notifId), type, message })
    }),

    dismissNotification: (id) => set((s) => {
      s.notifications = s.notifications.filter(n => n.id !== id)
    }),
  }))
)
