// ─────────────────────────────────────────────────────────────────────────────
// E3Studio :: UI :: WebSocketClient
// C++ backend ile bağlantı yönetimi.
// Otomatik yeniden bağlanma, mesaj kuyruğu, tip güvenli mesajlaşma.
// ─────────────────────────────────────────────────────────────────────────────
import { useStore } from '@/store/useStore'

type MessageCallback = (data: any) => void

class WSClient {
  private ws: WebSocket | null = null
  private listeners = new Map<string, MessageCallback[]>()
  private pendingRequests = new Map<string, (data: any) => void>()
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null
  private url: string

  constructor(url: string) {
    this.url = url
  }

  connect() {
    this.ws = new WebSocket(this.url)

    this.ws.onopen = () => {
      console.log('[E3] Backend bağlantısı kuruldu')
      useStore.getState().pushNotification('success', 'Backend bağlantısı kuruldu')
      if (this.reconnectTimer) {
        clearTimeout(this.reconnectTimer)
        this.reconnectTimer = null
      }
    }

    this.ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(event.data)
        this.dispatch(msg)
      } catch (e) {
        console.error('[E3] JSON parse hatası:', e)
      }
    }

    this.ws.onclose = () => {
      console.warn('[E3] Bağlantı kesildi, 2s sonra yeniden denenecek...')
      this.reconnectTimer = setTimeout(() => this.connect(), 2000)
    }

    this.ws.onerror = (err) => {
      console.error('[E3] WebSocket hatası:', err)
    }
  }

  // Mesaj gönder + yanıt bekle (Promise tabanlı)
  async send<T = any>(type: string, payload: any = {}): Promise<T> {
    const id = crypto.randomUUID()
    return new Promise((resolve, reject) => {
      this.pendingRequests.set(id, resolve)
      setTimeout(() => {
        if (this.pendingRequests.has(id)) {
          this.pendingRequests.delete(id)
          reject(new Error(`Zaman aşımı: ${type}`))
        }
      }, 30000)

      const msg = JSON.stringify({ type, id, payload })
      if (this.ws?.readyState === WebSocket.OPEN) {
        this.ws.send(msg)
      } else {
        reject(new Error('WebSocket bağlı değil'))
      }
    })
  }

  // Push event dinle
  on(type: string, cb: MessageCallback) {
    if (!this.listeners.has(type)) this.listeners.set(type, [])
    this.listeners.get(type)!.push(cb)
  }

  off(type: string, cb: MessageCallback) {
    const arr = this.listeners.get(type)
    if (arr) this.listeners.set(type, arr.filter(f => f !== cb))
  }

  private dispatch(msg: any) {
    const { type, id, data, payload } = msg

    // Beklenen yanıt mı?
    if (id && this.pendingRequests.has(id)) {
      const resolve = this.pendingRequests.get(id)!
      this.pendingRequests.delete(id)
      resolve(data ?? payload ?? msg)
      return
    }

    // Push event — store'u güncelle
    this.handlePushEvent(type, payload ?? data ?? msg)

    // Özel listener'lar
    const handlers = this.listeners.get(type)
    if (handlers) handlers.forEach(h => h(payload ?? data))
  }

  private handlePushEvent(type: string, payload: any) {
    const store = useStore.getState()

    switch (type) {
      case 'system.ready':
        store.pushNotification('info', 'E3Studio hazır')
        break

      case 'toolpath.generated':
        store.updateOperation(payload.operationId, {
          isDirty: false,
          toolpathId: payload.toolpathId
        })
        store.pushNotification('success',
          `Toolpath hazır — ${payload.pointCount} nokta, ~${payload.estimatedMinutes?.toFixed(1)} dk`)
        break

      case 'simulation.frame':
        store.setSimulation({
          progress: payload.progress,
          remainingMaterial: payload.remainingMaterial
        })
        break

      case 'ai.prediction':
        store.setAIPrediction({ operationId: payload.operationId, ...payload })
        break

      case 'error':
        store.pushNotification('error', payload?.message ?? 'Bilinmeyen hata')
        break
    }
  }
}

export const ws = new WSClient('ws://localhost:9001')
ws.connect()

export default ws
