import React, { useRef, useEffect, useMemo } from 'react'
import { Canvas, useThree, useFrame } from '@react-three/fiber'
import { OrbitControls, Grid, GizmoHelper, GizmoViewport } from '@react-three/drei'
import * as THREE from 'three'
import { useStore } from '@/store/useStore'

// ─── Model Görüntüleyici ──────────────────────────────────────────────────────
function ModelMesh({ model }: { model: any }) {
  const geometry = useMemo(() => {
    if (!model.meshBuffer) return null
    const geo = new THREE.BufferGeometry()
    const buf = new Float32Array(model.meshBuffer)
    // Interleaved buffer: [x,y,z, nx,ny,nz, ...]
    const stride = 6
    const count = buf.length / stride
    const positions = new Float32Array(count * 3)
    const normals   = new Float32Array(count * 3)
    for (let i = 0; i < count; i++) {
      positions[i*3]   = buf[i*stride]
      positions[i*3+1] = buf[i*stride+1]
      positions[i*3+2] = buf[i*stride+2]
      normals[i*3]     = buf[i*stride+3]
      normals[i*3+1]   = buf[i*stride+4]
      normals[i*3+2]   = buf[i*stride+5]
    }
    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3))
    geo.setAttribute('normal',   new THREE.BufferAttribute(normals, 3))
    return geo
  }, [model.meshBuffer])

  if (!geometry) return null

  return (
    <mesh geometry={geometry} castShadow receiveShadow>
      <meshStandardMaterial
        color={model.role === 'stock' ? '#4a6fa5' : '#b0bec5'}
        transparent
        opacity={model.role === 'stock' ? 0.35 : 1}
        roughness={0.4}
        metalness={0.6}
        side={THREE.DoubleSide}
      />
    </mesh>
  )
}

// ─── Toolpath Çizgisi ─────────────────────────────────────────────────────────
function ToolpathLines({ tp }: { tp: any }) {
  const rapidColor  = new THREE.Color('#ef4444') // kırmızı — rapid
  const feedColor   = new THREE.Color('#22c55e') // yeşil — kesme

  const { rapidGeo, feedGeo } = useMemo(() => {
    if (!tp?.points) return { rapidGeo: null, feedGeo: null }
    const pts = tp.points as Float32Array
    const types = tp.types as Uint8Array
    const rapidPts: number[] = []
    const feedPts: number[] = []

    for (let i = 0; i + 5 < pts.length; i += 3) {
      const t = types[i/3]
      const seg = [pts[i], pts[i+1], pts[i+2], pts[i+3], pts[i+4], pts[i+5]]
      if (t === 0) rapidPts.push(...seg)
      else feedPts.push(...seg)
    }

    const makeGeo = (arr: number[]) => {
      if (!arr.length) return null
      const g = new THREE.BufferGeometry()
      g.setAttribute('position', new THREE.BufferAttribute(new Float32Array(arr), 3))
      return g
    }

    return { rapidGeo: makeGeo(rapidPts), feedGeo: makeGeo(feedPts) }
  }, [tp])

  if (!tp?.visible) return null

  return (
    <group>
      {rapidGeo && (
        <lineSegments geometry={rapidGeo}>
          <lineBasicMaterial color={rapidColor} linewidth={1} />
        </lineSegments>
      )}
      {feedGeo && (
        <lineSegments geometry={feedGeo}>
          <lineBasicMaterial color={feedColor} linewidth={1.5} />
        </lineSegments>
      )}
    </group>
  )
}

// ─── Ana Viewport ─────────────────────────────────────────────────────────────
export default function Viewport3D() {
  const models    = useStore(s => s.models)
  const toolpaths = useStore(s => s.toolpaths)

  return (
    <div className="w-full h-full bg-[#0d0d0d] rounded-xl overflow-hidden">
      <Canvas
        shadows
        camera={{ position: [150, 120, 150], fov: 45, near: 0.1, far: 50000 }}
        gl={{ antialias: true, toneMapping: THREE.ACESFilmicToneMapping }}
      >
        {/* Işıklar */}
        <ambientLight intensity={0.4} />
        <directionalLight
          position={[200, 300, 200]}
          intensity={1.2}
          castShadow
          shadow-mapSize={[2048, 2048]}
        />
        <directionalLight position={[-100, 100, -100]} intensity={0.3} />

        {/* Referans Izgara */}
        <Grid
          args={[500, 500]}
          cellSize={10}
          cellThickness={0.5}
          cellColor="#1e293b"
          sectionSize={50}
          sectionThickness={1}
          sectionColor="#334155"
          fadeDistance={800}
          position={[0, -0.01, 0]}
        />

        {/* Orijin eksenleri */}
        <axesHelper args={[30]} />

        {/* Modeller */}
        {models.map(m => <ModelMesh key={m.id} model={m} />)}

        {/* Toolpath'lar */}
        {Object.values(toolpaths).map(tp =>
          <ToolpathLines key={tp.operationId} tp={tp} />
        )}

        {/* Kamera kontrolü */}
        <OrbitControls
          makeDefault
          enableDamping
          dampingFactor={0.05}
          minDistance={5}
          maxDistance={5000}
        />

        {/* Gizmo (sağ alt köşe navigasyon küpü) */}
        <GizmoHelper alignment="bottom-right" margin={[80, 80]}>
          <GizmoViewport
            axisColors={['#ef4444', '#22c55e', '#3b82f6']}
            labelColor="white"
          />
        </GizmoHelper>
      </Canvas>
    </div>
  )
}
