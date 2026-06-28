import React, { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { PanelGroup, Panel, PanelResizeHandle } from 'react-resizable-panels'
import { useStore } from '@/store/useStore'
import Toolbar from '@/components/Toolbar/Toolbar'
import Viewport3D from '@/components/Viewport3D/Viewport3D'
import OperationPanel from '@/components/OperationPanel/OperationPanel'
import ToolPanel from '@/components/ToolPanel/ToolPanel'
import SimulationPanel from '@/components/SimulationPanel/SimulationPanel'
import ExportPanel from '@/components/ExportPanel/ExportPanel'
import { CheckCircle, XCircle, AlertTriangle, Info, X, Loader2 } from 'lucide-react'
import clsx from 'clsx'

function NotificationStack() {
  const { notifications, dismissNotification } = useStore()

  const icons = {
    info: <Info size={14} className="text-blue-400" />,
    success: <CheckCircle size={14} className="text-green-400" />,
    warning: <AlertTriangle size={14} className="text-yellow-400" />,
    error: <XCircle size={14} className="text-red-400" />,
  }

  useEffect(() => {
    const timers = notifications.map(n =>
      setTimeout(() => dismissNotification(n.id), 4000)
    )
    return () => timers.forEach(clearTimeout)
  }, [notifications])

  return (
    <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 max-w-sm">
      {notifications.map(n => (
        <div key={n.id}
          className={clsx(
            'flex items-start gap-2 rounded-lg border px-3 py-2.5 shadow-xl backdrop-blur-md text-sm',
            'bg-[#1a1a1a]/90 border-white/15 text-white/85',
            'animate-in slide-in-from-right duration-200'
          )}>
          {icons[n.type]}
          <span className="flex-1 leading-snug">{n.message}</span>
          <button onClick={() => dismissNotification(n.id)} className="text-white/30 hover:text-white/60">
            <X size={13} />
          </button>
        </div>
      ))}
    </div>
  )
}

function LoadingOverlay() {
  const { t } = useTranslation()
  const { isLoading, loadingMessage } = useStore()
  if (!isLoading) return null
  return (
    <div className="fixed inset-0 z-40 bg-black/50 backdrop-blur-sm flex items-center justify-center">
      <div className="flex items-center gap-3 bg-[#1a1a1a] border border-white/15 rounded-xl px-6 py-4 shadow-2xl">
        <Loader2 size={20} className="text-blue-400 animate-spin" />
        <span className="text-sm text-white/80">{loadingMessage || t('app.loading')}</span>
      </div>
    </div>
  )
}

export default function App() {
  const { t } = useTranslation()
  const { activePanel } = useStore()

  return (
    <div className="flex flex-col h-screen bg-[#0a0a0a] text-white overflow-hidden">
      <Toolbar />

      <div className="flex-1 overflow-hidden">
        <PanelGroup direction="horizontal" className="h-full">

          <Panel defaultSize={22} minSize={16} maxSize={35}>
            <div className="h-full bg-[#111111] border-r border-white/8">
              {activePanel === 'operations' && <OperationPanel />}
              {activePanel === 'tools' && <ToolPanel />}
              {activePanel === 'simulation' && <SimulationPanel />}
              {activePanel === 'export' && <ExportPanel />}
              {activePanel === 'ai' && (
                <div className="p-4 text-white/50 text-sm">{t('app.ai_panel')}</div>
              )}
            </div>
          </Panel>

          <PanelResizeHandle className="w-1 bg-white/5 hover:bg-blue-500/40 transition-colors cursor-col-resize" />

          <Panel defaultSize={78} minSize={40}>
            <Viewport3D />
          </Panel>

        </PanelGroup>
      </div>

      <NotificationStack />
      <LoadingOverlay />
    </div>
  )
}
