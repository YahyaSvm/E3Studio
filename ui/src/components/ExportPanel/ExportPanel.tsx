import React, { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useStore } from '@/store/useStore'
import ws from '@/lib/wsClient'
import { Download } from 'lucide-react'

const POSTS = ['fanuc', 'haas', 'heidenhain', 'generic']

export default function ExportPanel() {
  const { t } = useTranslation()
  const { operations, selectedOperationId, projectName } = useStore()
  const selectedOp = operations.find(o => o.id === selectedOperationId)
  const [postProcessor, setPostProcessor] = useState('fanuc')
  const [outputPath, setOutputPath] = useState('output.nc')

  const exportGcode = async () => {
    if (!selectedOp?.toolpathId) {
      useStore.getState().pushNotification('warning', t('export_panel.no_toolpath'))
      return
    }
    useStore.getState().setLoading(true, t('export_panel.exporting'))
    try {
      const result = await ws.send('toolpath.export', {
        toolpathId: selectedOp.toolpathId,
        outputPath: outputPath || `${projectName || 'project'}.nc`,
        postProcessor,
      })
      useStore.getState().pushNotification('success', t('export_panel.success', { path: result?.path ?? outputPath }))
    } catch (e: any) {
      useStore.getState().pushNotification('error', e.message ?? t('export_panel.failed'))
    } finally {
      useStore.getState().setLoading(false)
    }
  }

  return (
    <div className="flex flex-col h-full p-4 gap-4">
      <div>
        <h3 className="text-sm font-semibold text-white/80">{t('export_panel.title')}</h3>
        <p className="text-xs text-white/40 mt-1">{t('export_panel.subtitle')}</p>
      </div>

      <label className="text-xs text-white/50 space-y-1 block">
        {t('export_panel.post_processor')}
        <select
          value={postProcessor}
          onChange={e => setPostProcessor(e.target.value)}
          className="w-full rounded-lg bg-white/5 border border-white/10 px-3 py-2 text-sm text-white/80"
        >
          {POSTS.map(p => <option key={p} value={p}>{p.toUpperCase()}</option>)}
        </select>
      </label>

      <label className="text-xs text-white/50 space-y-1 block">
        {t('export_panel.output_path')}
        <input
          value={outputPath}
          onChange={e => setOutputPath(e.target.value)}
          className="w-full rounded-lg bg-white/5 border border-white/10 px-3 py-2 text-sm text-white/80 font-mono"
        />
      </label>

      <button
        onClick={exportGcode}
        className="flex items-center justify-center gap-2 rounded-lg bg-green-500/20 border border-green-500/40 py-2.5 text-sm text-green-300 hover:bg-green-500/30"
      >
        <Download size={16} /> {t('export_panel.export')}
      </button>
    </div>
  )
}
