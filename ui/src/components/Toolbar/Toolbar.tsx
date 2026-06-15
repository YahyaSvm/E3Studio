import React from 'react'
import { useStore } from '@/store/useStore'
import ws from '@/lib/wsClient'
import {
  FolderOpen, Save, FilePlus, Box, Layers,
  Activity, Cpu, Settings, ChevronRight
} from 'lucide-react'
import clsx from 'clsx'

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
  const { activePanel, setActivePanel, viewportMode, setViewportMode, projectName } = useStore()

  const openFile = async () => {
    // Tarayıcı File API — path'i C++'a gönder
    const input = document.createElement('input')
    input.type = 'file'
    input.accept = '.e3p,.step,.stp,.iges,.igs,.stl'
    input.onchange = async (e) => {
      const file = (e.target as HTMLInputElement).files?.[0]
      if (!file) return
      const path = (file as any).path ?? file.name // Electron'da gerçek path
      await ws.send('model.load', { filePath: path, role: 'workpiece' })
    }
    input.click()
  }

  const saveProject = () => ws.send('project.save', {})

  return (
    <div className="flex items-center h-12 px-3 gap-1 bg-[#111111] border-b border-white/10">
      {/* Logo */}
      <div className="flex items-center gap-2 mr-4">
        <div className="w-7 h-7 rounded-md bg-gradient-to-br from-blue-500 to-violet-600 flex items-center justify-center">
          <span className="text-white text-xs font-bold">E3</span>
        </div>
        <span className="text-sm font-semibold text-white/80 hidden md:block">Studio</span>
      </div>

      {/* Dosya işlemleri */}
      <ToolbarButton icon={<FilePlus size={16} />} label="Yeni" onClick={() => ws.send('project.new', { name: 'Yeni Proje' })} />
      <ToolbarButton icon={<FolderOpen size={16} />} label="Aç" onClick={openFile} />
      <ToolbarButton icon={<Save size={16} />} label="Kaydet" onClick={saveProject} />

      <div className="w-px h-6 bg-white/10 mx-2" />

      {/* Panel seçici */}
      <ToolbarButton
        icon={<Layers size={16} />} label="Operasyonlar"
        active={activePanel === 'operations'}
        onClick={() => setActivePanel('operations')}
      />
      <ToolbarButton
        icon={<Activity size={16} />} label="Simülasyon"
        active={activePanel === 'simulation'}
        onClick={() => setActivePanel('simulation')}
      />
      <ToolbarButton
        icon={<Cpu size={16} />} label="AI"
        active={activePanel === 'ai'}
        onClick={() => setActivePanel('ai')}
      />

      <div className="w-px h-6 bg-white/10 mx-2" />

      {/* Viewport modu */}
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
            {mode === '3d' ? '3D' : 'Simülasyon'}
          </button>
        ))}
      </div>

      {/* Proje adı */}
      <div className="ml-auto text-xs text-white/30">
        {projectName || 'Proje yüklenmedi'}
      </div>
    </div>
  )
}
