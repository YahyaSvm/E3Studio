import React, { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useStore, Tool } from '@/store/useStore'
import ws from '@/lib/wsClient'
import { Plus, Trash2 } from 'lucide-react'

export default function ToolPanel() {
  const { t } = useTranslation()
  const tools = useStore(s => s.tools)

  useEffect(() => {
    ws.send('tool.list').then((data) => {
      if (data?.tools) useStore.setState({ tools: data.tools })
    }).catch(() => {})
  }, [])

  const addTool = async () => {
    const result = await ws.send('tool.add', {
      name: `${t('tools_panel.tool')} ${tools.length + 1}`,
      type: 'FlatEndmill',
      diameter: 6,
      flutes: 4,
      overallLength: 50,
      cuttingLength: 20,
      material: 'Carbide',
    })
    if (result?.id) {
      useStore.getState().addTool({
        id: result.id,
        name: `${t('tools_panel.tool')} ${tools.length + 1}`,
        diameter: 6,
        type: 'FlatEndmill',
      })
    }
  }

  const removeTool = async (tool: Tool) => {
    await ws.send('tool.remove', { id: tool.id })
    useStore.setState({ tools: tools.filter(t => t.id !== tool.id) })
  }

  return (
    <div className="flex flex-col h-full">
      <div className="flex items-center justify-between px-4 py-3 border-b border-white/10">
        <span className="text-sm font-semibold text-white/80">{t('tools_panel.title')}</span>
        <button onClick={addTool} className="flex items-center gap-1 text-xs text-blue-400 hover:text-blue-300">
          <Plus size={14} /> {t('tools_panel.add')}
        </button>
      </div>
      <div className="flex-1 overflow-y-auto p-3 space-y-2">
        {tools.length === 0 && (
          <p className="text-xs text-white/40 px-1">{t('tools_panel.empty')}</p>
        )}
        {tools.map(tool => (
          <div key={tool.id} className="rounded-lg border border-white/10 bg-white/5 p-3 flex justify-between gap-2">
            <div>
              <div className="text-sm text-white/85">{tool.name}</div>
              <div className="text-xs text-white/40">Ø{tool.diameter}mm · {tool.type}</div>
            </div>
            <button onClick={() => removeTool(tool)} className="text-white/30 hover:text-red-400">
              <Trash2 size={14} />
            </button>
          </div>
        ))}
      </div>
    </div>
  )
}
