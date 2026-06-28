import React from 'react'
import { useTranslation } from 'react-i18next'
import { useStore, Operation } from '@/store/useStore'
import ws from '@/lib/wsClient'
import { Play, Trash2, Plus, Cpu, ChevronRight } from 'lucide-react'
import clsx from 'clsx'
import { applyToolpathVisualization } from '@/lib/toolpathViz'

const OP_TYPE_LABELS: Record<string, string> = {
  Pocket2D: 'operation_type.Pocket2D',
  Contour2D: 'operation_type.Contour2D',
  AdaptiveClearing: 'operation_type.AdaptiveClearing',
  SurfaceFinishing: 'operation_type.SurfaceFinishing',
  Drilling: 'operation_type.Drilling',
}

function OperationCard({ op }: { op: Operation }) {
  const { t } = useTranslation()
  const { selectOperation, selectedOperationId, removeOperation } = useStore()
  const isSelected = selectedOperationId === op.id

  const compute = async () => {
    useStore.getState().setLoading(true, t('operation_panel.loading_toolpath'))
    try {
      const result = await ws.send('operation.compute', { operationId: op.id })
      if (result?.toolpathId) {
        useStore.getState().updateOperation(op.id, {
          isDirty: false,
          toolpathId: result.toolpathId,
        })
        applyToolpathVisualization(op.id, result)
      }
    } finally {
      useStore.getState().setLoading(false)
    }
  }

  const aiOptimize = async () => {
    useStore.getState().setLoading(true, t('operation_panel.loading_ai'))
    const result = await ws.send('ai.optimize', {
      toolDiameter: 10,
      hardnessHRC: 30,
      axialDepth: op.depthOfCut,
      radialStepover: op.stepover,
      operationType: op.type === 'SurfaceFinishing' ? 2 : 0,
    })
    useStore.getState().setAIPrediction({ operationId: op.id, ...result })
    useStore.getState().setLoading(false)
  }

  const prediction = useStore(s => s.aiPredictions[op.id])

  return (
    <div
      onClick={() => selectOperation(op.id)}
      className={clsx(
        'group relative rounded-lg border p-3 cursor-pointer transition-all',
        isSelected
          ? 'border-blue-500 bg-blue-500/10'
          : 'border-white/10 bg-white/5 hover:border-white/20 hover:bg-white/8'
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className={clsx(
              'w-2 h-2 rounded-full flex-shrink-0',
              op.isDirty ? 'bg-yellow-400' : 'bg-green-400'
            )} />
            <span className="text-sm font-medium text-white truncate">{op.name}</span>
          </div>
          <span className="text-xs text-white/40 mt-0.5 block">
            {t(OP_TYPE_LABELS[op.type] ?? op.type)}
          </span>

          <div className="mt-2 grid grid-cols-3 gap-1">
            {[
              ['Feed', `${op.feedrateXY} mm/m`],
              ['Spindle', `${op.spindleSpeed} rpm`],
              ['DoC', `${op.depthOfCut} mm`],
            ].map(([label, val]) => (
              <div key={label} className="rounded bg-white/5 px-1.5 py-0.5">
                <div className="text-[10px] text-white/30">{label}</div>
                <div className="text-[11px] text-white/70 font-mono">{val}</div>
              </div>
            ))}
          </div>

          {prediction && (
            <div className="mt-2 rounded bg-violet-500/15 border border-violet-500/30 px-2 py-1">
              <div className="text-[10px] text-violet-300 font-medium mb-0.5">{t('operation_panel.ai_suggestion')}</div>
              <div className="grid grid-cols-2 gap-x-3 text-[10px] text-violet-200/80">
                <span>Feed: {prediction.feedrate.toFixed(0)} mm/m</span>
                <span>Ra: {prediction.predictedRoughness.toFixed(2)} µm</span>
                <span>RPM: {prediction.spindleSpeed.toFixed(0)}</span>
                <span>{t('operation_panel.tool_life', { minutes: prediction.toolLifeMinutes.toFixed(0) })}</span>
              </div>
            </div>
          )}
        </div>

        <div className="flex flex-col gap-1">
          <button
            onClick={(e) => { e.stopPropagation(); compute() }}
            title={t('operation_panel.compute')}
            className="p-1.5 rounded bg-green-500/20 hover:bg-green-500/40 text-green-400 transition-colors"
          >
            <Play size={12} />
          </button>
          <button
            onClick={(e) => { e.stopPropagation(); aiOptimize() }}
            title={t('operation_panel.optimize_ai')}
            className="p-1.5 rounded bg-violet-500/20 hover:bg-violet-500/40 text-violet-400 transition-colors"
          >
            <Cpu size={12} />
          </button>
          <button
            onClick={(e) => { e.stopPropagation(); removeOperation(op.id) }}
            title={t('operation_panel.delete')}
            className="p-1.5 rounded bg-red-500/10 hover:bg-red-500/30 text-red-400 transition-colors opacity-0 group-hover:opacity-100"
          >
            <Trash2 size={12} />
          </button>
        </div>
      </div>
    </div>
  )
}

export default function OperationPanel() {
  const { t } = useTranslation()
  const operations = useStore(s => s.operations)

  const addOperation = async () => {
    const name = `${t('operation_panel.operation')} ${operations.length + 1}`
    const tools = useStore.getState().tools
    const models = useStore.getState().models
    const result = await ws.send('operation.add', {
      name,
      type: 'Pocket2D',
      toolId: tools[0]?.id ?? '',
      geometryRef: models[0]?.id ?? '',
      feedrateXY: 1200,
      feedrateZ: 400,
      spindleSpeed: 8000,
      depthOfCut: 2.0,
      stepover: 4.0,
      stockToLeave: 0.0,
      tolerance: 0.01,
    })
    if (result?.id) {
      useStore.getState().addOperation({
        id: result.id,
        name,
        type: 'Pocket2D',
        toolId: '',
        feedrateXY: 1200,
        feedrateZ: 400,
        spindleSpeed: 8000,
        depthOfCut: 2.0,
        stepover: 4.0,
        isDirty: true,
      })
    }
  }

  return (
    <div className="flex flex-col h-full">
      <div className="flex items-center justify-between px-4 py-3 border-b border-white/10">
        <span className="text-sm font-semibold text-white/80">{t('operation_panel.title')}</span>
        <button
          onClick={addOperation}
          className="flex items-center gap-1 text-xs text-blue-400 hover:text-blue-300 transition-colors"
        >
          <Plus size={14} />
          {t('operation_panel.add')}
        </button>
      </div>

      <div className="flex-1 overflow-y-auto p-3 space-y-2">
        {operations.length === 0 ? (
          <div className="text-center text-white/25 text-sm mt-8">
            {t('operation_panel.empty')}
            <br />
            <span className="text-xs">{t('operation_panel.empty_hint')}</span>
          </div>
        ) : (
          operations.map(op => <OperationCard key={op.id} op={op} />)
        )}
      </div>

      {operations.some(o => o.isDirty) && (
        <div className="p-3 border-t border-white/10">
          <button
            onClick={() => operations.filter(o => o.isDirty)
              .forEach(o => ws.send('operation.compute', { operationId: o.id }))}
            className="w-full py-2 rounded-lg bg-green-600/80 hover:bg-green-600 text-white text-sm font-medium transition-colors flex items-center justify-center gap-2"
          >
            <Play size={14} />
            {t('operation_panel.compute_all')}
          </button>
        </div>
      )}
    </div>
  )
}
