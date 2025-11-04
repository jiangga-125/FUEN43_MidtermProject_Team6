<!-- src/views/AuthCallback.vue -->
<template><div class="shell">正在處理外部登入回應…</div></template>
<script setup lang="ts">
import { useAuth } from '@/stores/auth'
import { useRouter } from 'vue-router'
const auth = useAuth()
const router = useRouter()

const hash = new URLSearchParams(location.hash.replace(/^#/, ''))
const token = hash.get('access_token')
const redirect = new URLSearchParams(location.search).get('redirect') || '/member'

;(async () => {
  if (!token) { router.replace('/login?err=oauth'); return }
  try { await auth.setTokenAndLoadMember(token); router.replace(redirect) }
  catch { router.replace('/login?err=oauth') }
})()
</script>
<style scoped>.shell{padding:40px;text-align:center;color:#555}</style>
