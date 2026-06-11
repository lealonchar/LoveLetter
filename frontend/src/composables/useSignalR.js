import { ref, readonly } from 'vue'
import * as signalR from '@microsoft/signalr'

const SERVER_URL = import.meta.env.VITE_SERVER_URL || 'http://localhost:5000'

// Shared singleton connection
let connection = null

export function useSignalR() {
  const isConnected = ref(false)
  const error = ref(null)

  async function connect() {
    if (connection?.state === signalR.HubConnectionState.Connected) return

    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${SERVER_URL}/gamehub`)
      .withAutomaticReconnect()
      .build()

    connection.onclose(() => { isConnected.value = false })
    connection.onreconnected(() => { isConnected.value = true })

    try {
      await connection.start()
      isConnected.value = true
    } catch (e) {
      error.value = 'Could not connect to server.'
      throw e
    }
  }

  // Register a handler for a server event
  function on(event, handler) {
    connection?.on(event, handler)
  }

  function off(event, handler) {
    connection?.off(event, handler)
  }

  // Invoke a server hub method
  async function invoke(method, ...args) {
    if (!connection) throw new Error('Not connected')
    return await connection.invoke(method, ...args)
  }

  return { isConnected: readonly(isConnected), error: readonly(error), connect, on, off, invoke }
}
