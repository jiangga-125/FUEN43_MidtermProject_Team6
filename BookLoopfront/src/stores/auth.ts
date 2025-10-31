import { reactive } from 'vue'
import { me } from '@/api/auth'

export const authState = reactive<{
  inited: boolean
  user: null | { userId: string; name: string; email: string }
  roles: string[]
  permissions: string[]
}>({ inited: false, user: null, roles: [], permissions: [] })

export async function initAuth() {
  try {
    const data = await me()
    authState.user = data.user
    authState.roles = data.roles ?? []
    authState.permissions = data.permissions ?? []
  } catch {
    authState.user = null
    authState.roles = []
    authState.permissions = []
  } finally {
    authState.inited = true
  }
}
