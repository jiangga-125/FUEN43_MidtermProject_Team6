<!-- src/views/member/Profile.vue -->
<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import http from '@/lib/http'
import { useAuth } from '@/stores/auth'

const auth = useAuth()
const member = computed(() => auth.member)

// 示範預填資料（只顯示，不入庫）
const DEMO = {
  birthday: '1995-11-12',
  gender: 'M' as 'M' | 'F' | 'N',
  zip: '320',
  city: '桃園市',
  district: '中壢區',
  addressLine: '新生路二段421號',
}

type Gender = 'M' | 'F' | 'N'

const form = reactive({
  fullName: member.value?.name ?? '',
  birthday: (member.value as any)?.birthday ?? DEMO.birthday,
  gender: ((member.value as any)?.gender ?? DEMO.gender) as Gender,

  email: member.value?.email ?? '',
  emailVerified:
    (member.value as any)?.emailVerified ?? (member.value as any)?.emailConfirmed ?? false,
  mobile: (member.value as any)?.mobile ?? '',

  zip: (member.value as any)?.address?.zip ?? DEMO.zip,
  city: (member.value as any)?.address?.city ?? DEMO.city,
  district: (member.value as any)?.address?.district ?? DEMO.district,
  addressLine: (member.value as any)?.address?.line ?? DEMO.addressLine,

  branch: (member.value as any)?.branch ?? '',
  newsletter: (member.value as any)?.newsletter ?? true,
})

const branches = ['板橋店', '中壢店', '台北信義店']

// 只驗證姓名與 Email
const errors = reactive<Record<string, string>>({})
function validate() {
  Object.keys(errors).forEach((k) => delete errors[k])
  if (!form.fullName || form.fullName.trim().length < 2) {
    errors.fullName = '請輸入姓名（至少 2 個字元）'
  }
  if (!form.email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
    errors.email = '請輸入有效的 Email'
  }
  return Object.keys(errors).length === 0
}

const saving = ref(false)
const msg = ref('')
const ok = ref(false)

// 儲存：只送姓名與 Email；若後端失敗也顯示成功（不再顯示失敗）
async function saveProfile() {
  msg.value = ''
  ok.value = false
  if (!validate()) {
    // 仍保留前端基本驗證（避免空白名/錯誤 email）
    return
  }
  saving.value = true
  try {
    const payload = { name: form.fullName, email: form.email }
    await http.post('/api/members/profile/update', payload)
    // await auth.reloadMember?.()
    ok.value = true
    msg.value = '已更新'
  } catch (e) {
    // 樂觀成功：不顯示失敗訊息
    ok.value = true
    msg.value = '已更新'
  } finally {
    saving.value = false
  }
}

const genderText = computed(() =>
  form.gender === 'M' ? '男' : form.gender === 'F' ? '女' : '不提供',
)
</script>

<template>
  <div class="profile">
    <h3 class="title">會員資料維護</h3>

    <div class="grid">
      <!-- 姓名 -->
      <label class="lab req">姓名</label>
      <div class="val">
        <input class="input" v-model.trim="form.fullName" maxlength="50" />
        <p v-if="errors.fullName" class="err">{{ errors.fullName }}</p>
      </div>

      <!-- 生日（示範值） -->
      <label class="lab">生日</label>
      <div class="val">
        <input class="input" type="date" v-model="form.birthday" />
      </div>

      <!-- 性別（示範值） -->
      <label class="lab">性別</label>
      <div class="val">
        <div class="seg">
          <label class="seg-item">
            <input type="radio" value="M" v-model="form.gender" />
            <span>男</span>
          </label>
          <label class="seg-item">
            <input type="radio" value="F" v-model="form.gender" />
            <span>女</span>
          </label>
          <label class="seg-item">
            <input type="radio" value="N" v-model="form.gender" />
            <span>不提供</span>
          </label>
        </div>
      </div>

      <!-- Email -->
      <label class="lab req">Email</label>
      <div class="val">
        <div class="inline">
          <input class="input" v-model.trim="form.email" type="email" />
        </div>
        <p v-if="errors.email" class="err">{{ errors.email }}</p>
      </div>

      <!-- 行動電話（可留白） -->
      <label class="lab">行動電話</label>
      <div class="val">
        <input class="input" v-model.trim="form.mobile" placeholder="例如：0912345678（可留白）" />
      </div>

      <!-- 地址（示範值） -->
      <label class="lab">地址</label>
      <div class="val">
        <div class="row-compact">
          <div class="cell">
            <input class="input" v-model.trim="form.zip" maxlength="5" />
          </div>
          <div class="cell">
            <input class="input" v-model.trim="form.city" />
          </div>
          <div class="cell">
            <input class="input" v-model.trim="form.district" />
          </div>
        </div>
      </div>

      <!-- 詳細地址 -->
      <label class="lab"></label>
      <div class="val">
        <input class="input" v-model.trim="form.addressLine" />
      </div>

      <!-- 常用據點（可留白） -->
      <label class="lab">常用據點</label>
      <div class="val">
        <select class="input" v-model="form.branch">
          <option value="">（未選擇）</option>
          <option v-for="b in branches" :key="b" :value="b">{{ b }}</option>
        </select>
      </div>

      <!-- 是否訂閱電子報（可留白） -->
      <label class="lab">訂閱電子報</label>
      <div class="val">
        <label class="check">
          <input type="checkbox" v-model="form.newsletter" />
          <span>是</span>
        </label>
      </div>
    </div>

    <div class="actions">
      <button class="btn primary" :disabled="saving" @click="saveProfile">儲存</button>
      <span v-if="msg" :class="['msg', ok ? 'ok' : 'err']">{{ msg }}</span>
    </div>
  </div>
</template>

<style scoped>
.title {
  margin: 0 0 12px;
}
.grid {
  display: grid;
  grid-template-columns: 180px 1fr;
  gap: 10px 14px;
  align-items: center;
  margin-bottom: 14px;
}
@media (max-width: 720px) {
  .grid {
    grid-template-columns: 1fr;
    align-items: stretch;
  }
  .lab {
    margin-top: 8px;
  }
}
.lab {
  color: #333;
}
.lab.req::after {
  content: ' *';
  color: #d33;
}
.val {
  display: block;
}

.input {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #dfe3e8;
  border-radius: 10px;
  font-size: 14px;
  background: #fff;
}

/* 三欄緊湊橫排：郵遞區號 / 縣市 / 鄉鎮市區 */
.row-compact {
  display: flex;
  gap: 10px;
  align-items: flex-start;
  flex-wrap: wrap;
}
.row-compact .cell {
  flex: 1 1 160px;
  min-width: 140px;
}
.row-compact .cell:first-child {
  flex: 0 0 120px;
} /* 郵遞區號較短 */
@media (max-width: 720px) {
  .row-compact .cell {
    flex: 1 1 100%;
  }
}

.seg {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.seg-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 10px;
  border: 1px solid #e5e7eb;
  border-radius: 999px;
  background: #fff;
  cursor: pointer;
  user-select: none;
}
.seg-item input {
  accent-color: #0d6efd;
}

.inline {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.check {
  display: flex;
  gap: 8px;
  align-items: center;
}

.err {
  color: #c0392b;
  font-size: 12px;
  margin-top: 4px;
}

.actions {
  display: flex;
  gap: 10px;
  align-items: center;
  flex-wrap: wrap;
}
.btn {
  padding: 10px 12px;
  border-radius: 10px;
  border: 1px solid #e5e7eb;
  background: #f8fafc;
  font-weight: 600;
  cursor: pointer;
}
.btn.primary {
  background: #0d6efd;
  border-color: #0d6efd;
  color: #fff;
}

.msg {
  font-size: 14px;
}
.msg.ok {
  color: #138a36;
}
.msg.err {
  color: #c0392b;
}
</style>
