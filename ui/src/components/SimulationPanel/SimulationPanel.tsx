import React from 'react'
import { useTranslation } from 'react-i18next'
import { useStore } from '@/store/useStore'
import ws from '@/lib/wsClient'
import { Play, Pause, StepForward } from 'lucide-react'

export default function SimulationPanel() {
  const { t } = useTranslation()
  const { simulation, operations, selectedOperationId } = useStore()
  const selectedOp = operations.find(o => o.id === selectedOperationId)

  const start = async () => {
    if (!selectedOp?.toolpathId) {
      useStore.getState().pushNotification('warning', t('simulation_panel.no_toolpath'))
      return
    }
    await ws.send('simulation.start', { toolpathId: selectedOp.toolpathId })
    useStore.getState().setSimulation({ isRunning: true, progress: 0 })
  }

  const step = async () => {
    await ws.send('simulation.step', { steps: 5, maxMoves: 10000 })
  }

  const pause = async () => {
    await ws.send('simulation.pause')
    useStore.getState().setSimulation({ isRunning: false })
  }

  return (
    <div className="flex flex-col h-full p-4 gap-4">
      <div>
        <h3 className="text-sm font-semibold text-white/80">{t('simulation_panel.title')}</h3>
        <p className="text-xs text-white/40 mt-1">{t('simulation_panel.subtitle')}</p>
      </div>

      <div className="rounded-lg border border-white/10 bg-white/5 p-3 space-y-2">
        <div className="flex justify-between text-xs text-white/50">
          <span>{t('simulation_panel.progress')}</span>
          <span>{Math.round(simulation.progress * 100)}%</span>
        </div>
        <div className="h-2 rounded bg-white/10 overflow-hidden">
          <div className="h-full bg-blue-500 transition-all" style={{ width: `${simulation.progress * 100}%` }} />
        </div>
        <div className="text-xs text-white/40">
          {t('simulation_panel.remaining')}: {simulation.remainingMaterial.toFixed(1)}%
        </div>
      </div>

      <div className="flex gap-2">
        <button onClick={start} className="flex-1 flex items-center justify-center gap-1 rounded-lg bg-blue-500/20 border border-blue-500/40 py-2 text-xs text-blue-300">
          <Play size={14} /> {t('simulation_panel.start')}
        </button>
        <button onClick={step} className="flex items-center justify-center rounded-lg border border-white/15 px-3 py-2 text-white/60 hover:text-white/80">
          <StepForward size={14} />
        </button>
        <button onClick={pause} className="flex items-center justify-center rounded-lg border border-white/15 px-3 py-2 text-white/60 hover:text-white/80">
          <Pause size={14} />
        </button>
      </div>
    </div>
  )
}
