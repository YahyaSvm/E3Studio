import i18n from './i18n'
import { useStore } from '@/store/useStore'
import { applyToolpathVisualization } from '@/lib/toolpathViz'

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
      console.log(i18n.t('console.connected'))
      useStore.getState().pushNotification('success', i18n.t('notify.connected'))
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
        console.error(i18n.t('console.parse_error'), e)
      }
    }

    this.ws.onclose = () => {
      console.warn(i18n.t('console.reconnecting'))
      this.reconnectTimer = setTimeout(() => this.connect(), 2000)
    }

    this.ws.onerror = (err) => {
      console.error(i18n.t('console.ws_error'), err)
    }
  }

  async send<T = any>(type: string, payload: any = {}): Promise<T> {
    const id = crypto.randomUUID()
    return new Promise((resolve, reject) => {
      this.pendingRequests.set(id, resolve)
      setTimeout(() => {
        if (this.pendingRequests.has(id)) {
          this.pendingRequests.delete(id)
          reject(new Error(i18n.t('notify.timeout', { type })))
        }
      }, 30000)

      const msg = JSON.stringify({ type, id, payload })
      if (this.ws?.readyState === WebSocket.OPEN) {
        this.ws.send(msg)
      } else {
        reject(new Error(i18n.t('notify.not_connected')))
      }
    })
  }

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

    if (id && this.pendingRequests.has(id)) {
      const resolve = this.pendingRequests.get(id)!
      this.pendingRequests.delete(id)
      resolve(data ?? payload ?? msg)
      return
    }

    this.handlePushEvent(type, payload ?? data ?? msg)

    const handlers = this.listeners.get(type)
    if (handlers) handlers.forEach(h => h(payload ?? data))
  }

  private handlePushEvent(type: string, payload: any) {
    const store = useStore.getState()

    switch (type) {
      case 'system.ready':
        store.pushNotification('info', i18n.t('notify.ready'))
        break

      case 'toolpath.generated':
        store.updateOperation(payload.operationId, {
          isDirty: false,
          toolpathId: payload.toolpathId
        })
        if (payload.points) applyToolpathVisualization(payload.operationId, payload)
        store.pushNotification('success',
          i18n.t('notify.toolpath_ready', {
            count: payload.pointCount,
            time: payload.estimatedMinutes?.toFixed(1)
          }))
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
        store.pushNotification('error', payload?.message ?? i18n.t('notify.unknown_error'))
        break
    }
  }
}

export const ws = new WSClient('ws://localhost:9001')
ws.connect()

export default ws
