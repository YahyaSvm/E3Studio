import React from 'react'
import { useTranslation } from 'react-i18next'
import { useStore } from '@/store/useStore'
import ws from '@/lib/wsClient'
import {
  FolderOpen, Save, FilePlus, Box, Layers,
  Activity, Cpu, Settings, ChevronRight, Globe
} from 'lucide-react'
import clsx from 'clsx'
import { setLanguage } from '@/lib/i18n'

interface ToolbarButtonProps {
  icon: React.ReactNode
  label: string
  onClick: () => void
  active?: boolean
  accent?: string
}

function ToolbarButton({ icon, label, onClick, active, accent }: ToolbarButtonProps) {
  return (
    <button
      onClick={onClick}
      title={label}
      className={clsx(
        'flex flex-col items-center gap-1 px-3 py-2 rounded-lg transition-all text-xs',
        active
          ? 'bg-blue-500/20 text-blue-400 border border-blue-500/40'
          : 'text-white/50 hover:text-white/80 hover:bg-white/8',
        accent
      )}
    >
      <span className="w-5 h-5 flex items-center justify-center">{icon}</span>
      <span className="hidden xl:block">{label}</span>
    </button>
  )
}

export default function Toolbar() {
  const { t, i18n } = useTranslation()
  const { activePanel, setActivePanel, viewportMode, setViewportMode, projectName } = useStore()

  const openFile = async () => {
    const input = document.createElement('input')
    input.type = 'file'
    input.accept = '.e3p,.step,.stp,.iges,.igs,.stl'
    input.onchange = async (e) => {
      const file = (e.target as HTMLInputElement).files?.[0]
      if (!file) return
      const path = (file as any).path ?? file.name
      await ws.send('model.load', { filePath: path, role: 'workpiece' })
    }
    input.click()
  }

  const saveProject = () => ws.send('project.save', {})

  const toggleLang = () => {
    setLanguage(i18n.language === 'en' ? 'tr' : 'en')
  }

  return (
    <div className="flex items-center h-12 px-3 gap-1 bg-[#111111] border-b border-white/10">
      <div className="flex items-center gap-2 mr-4">
        <div className="w-7 h-7 rounded-md bg-gradient-to-br from-blue-500 to-violet-600 flex items-center justify-center">
          <span className="text-white text-xs font-bold">E3</span>
        </div>
        <span className="text-sm font-semibold text-white/80 hidden md:block">Studio</span>
      </div>

      <ToolbarButton icon={<FilePlus size={16} />} label={t('toolbar.new')} onClick={() => ws.send('project.new', { name: t('toolbar.new_project') })} />
      <ToolbarButton icon={<FolderOpen size={16} />} label={t('toolbar.open')} onClick={openFile} />
      <ToolbarButton icon={<Save size={16} />} label={t('toolbar.save')} onClick={saveProject} />

      <div className="w-px h-6 bg-white/10 mx-2" />

      <ToolbarButton
        icon={<Layers size={16} />} label={t('toolbar.operations')}
        active={activePanel === 'operations'}
        onClick={() => setActivePanel('operations')}
      />
      <ToolbarButton
        icon={<Activity size={16} />} label={t('toolbar.simulation')}
        active={activePanel === 'simulation'}
        onClick={() => setActivePanel('simulation')}
      />
      <ToolbarButton
        icon={<Cpu size={16} />} label={t('toolbar.ai')}
        active={activePanel === 'ai'}
        onClick={() => setActivePanel('ai')}
      />

      <div className="w-px h-6 bg-white/10 mx-2" />

      <div className="flex rounded-lg border border-white/15 overflow-hidden">
        {(['3d', 'simulation'] as const).map(mode => (
          <button
            key={mode}
            onClick={() => setViewportMode(mode)}
            className={clsx(
              'px-3 py-1 text-xs transition-colors',
              viewportMode === mode
                ? 'bg-blue-600 text-white'
                : 'text-white/50 hover:text-white/70 hover:bg-white/5'
            )}
          >
            {mode === '3d' ? '3D' : t('toolbar.simulation_view')}
          </button>
        ))}
      </div>

      <div className="ml-auto flex items-center gap-3">
        <button
          onClick={toggleLang}
          className="flex items-center gap-1 text-xs text-white/30 hover:text-white/60 transition-colors"
          title={i18n.language === 'en' ? 'Türkçe' : 'English'}
        >
          <Globe size={12} />
          {i18n.language === 'en' ? 'EN' : 'TR'}
        </button>
        <span className="text-xs text-white/30">
          {projectName || t('toolbar.project_not_loaded')}
        </span>
      </div>
    </div>
  )
}
