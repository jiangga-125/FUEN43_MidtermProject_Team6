<!-- src/views/Member.vue -->
<script setup lang="ts">
import { computed, ref } from 'vue'
import { useAuth } from '@/stores/auth'
import { useRouter } from 'vue-router'

const auth = useAuth()
const router = useRouter()

const member = computed(() => auth.member)

// 後端若未回 twoFactorEnabled/lastLoginAt，就不顯示或改成預設
const twofaEnabled = computed(() => (member.value as any)?.twoFactorEnabled === true)

const oldPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const changing = ref(false)
const msg = ref('')


async function changePassword () {
  msg.value = ''
  if (!newPassword.value || newPassword.value !== confirmPassword.value) {
    msg.value = '新密碼與確認密碼不一致'
    return
  }
  try {
    changing.value = true
    await fetch('/api/auth/password/change', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ Old: oldPassword.value, New: newPassword.value }),
    }).then(r => { if (!r.ok) throw new Error('變更失敗') })
    msg.value = '✅ 密碼已變更'
    oldPassword.value = newPassword.value = confirmPassword.value = ''
  } catch (e: any) {
    msg.value = e?.message || '密碼變更失敗'
  } finally {
    changing.value = false
  }
}

function goto2FA () { router.push('/2fa/setup') }
async function signout () { try { await auth.logout?.() } finally { router.push('/login') } }




</script>

<template>
  <div class="member">
    <h2>會員中心</h2>

    <div v-if="member" class="cards">
      <div class="card">
        <h3>基本資料</h3>
        <div class="row"><span class="k">名稱</span><span class="v">{{ member.name || '-' }}</span></div>
        <div class="row"><span class="k">Email</span><span class="v">{{ member.email }}</span></div>
        <div class="row" v-if="(member as any)?.lastLoginAt">
          <span class="k">最近登入</span><span class="v">{{ (member as any).lastLoginAt }}</span>
        </div>
        <div class="row">
          <span class="k">兩步驗證</span>
          <span class="v">
            <template v-if="twofaEnabled">已啟用</template>
            <template v-else>未啟用</template>
          </span>
        </div>

        <div class="actions">
          <button class="btn" @click="goto2FA">設定 / 綁定 2FA</button>
          <button class="btn danger" @click="signout">登出</button>
        </div>
      </div>

      <div class="card">
        <h3>變更密碼</h3>
        <div class="field">
          <label>舊密碼</label>
          <input v-model="oldPassword" type="password" autocomplete="current-password" />
        </div>
        <div class="field">
          <label>新密碼</label>
          <input v-model="newPassword" type="password" autocomplete="new-password" />
        </div>
        <div class="field">
          <label>確認新密碼</label>
          <input v-model="confirmPassword" type="password" autocomplete="new-password" />
        </div>
        <button class="btn primary" :disabled="changing" @click="changePassword">儲存變更</button>
        <p v-if="msg" class="msg">{{ msg }}</p>
      </div>
    </div>

    <div v-else class="guest">
      <p>尚未登入，請先 <a href="/login">登入</a>。</p>
    </div>
  </div>

</template>

<style scoped>
.member { max-width: 900px; margin: 24px auto; padding: 0 16px; }
.cards { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
.card { background: #fff; border: 1px solid #e9ecef; border-radius: 12px; padding: 16px; }
h2 { margin-bottom: 12px; }
h3 { margin: 0 0 10px; }
.row { display: grid; grid-template-columns: 120px 1fr; padding: 6px 0; border-bottom: 1px dashed #eee; }
.row:last-child { border-bottom: 0; }
.k { color: #666; }
.v { color: #222; }
.actions { margin-top: 12px; display: flex; gap: 8px; }
.field { display: grid; gap: 6px; margin-bottom: 10px; }
.field input { padding: 10px 12px; border: 1px solid #dfe3e8; border-radius: 10px; }
.btn { padding: 10px 12px; border: 1px solid #e5e7eb; background: #f5f5f5; border-radius: 10px; cursor: pointer; }
.btn.primary { background: #0d6efd; color: #fff; border-color: #0d6efd; }
.btn.danger { background: #fee2e2; color: #b91c1c; border-color: #fecaca; }
.msg { margin-top: 8px; color: #333; }
@media (max-width: 900px) { .cards { grid-template-columns: 1fr; } }

</style>
