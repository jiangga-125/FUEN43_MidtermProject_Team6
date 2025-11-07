<!-- src/views/member/Profile.vue -->
<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import http from '@/lib/http'
import { useAuth } from '@/stores/auth'

// 會員資料（從 auth store）
const auth = useAuth()
const member = computed(() => auth.member)

// ====== 表單模型 ======
type Gender = 'M' | 'F' | 'N' // 男/女/不提供

const form = reactive({
  // 1) 基本資料
  fullName: member.value?.name ?? '',
  birthday: member.value?.birthday ?? '',            // 'YYYY-MM-DD'
  gender: (member.value as any)?.gender ?? 'N' as Gender,

  // 2) 聯絡資料
  email: member.value?.email ?? '',
  emailVerified: (member.value as any)?.emailVerified ?? (member.value as any)?.emailConfirmed ?? false,
  mobile: (member.value as any)?.mobile ?? '',

  // 3) 地址（郵遞區號 / 縣市 / 區 / 詳細地址）
  zip: (member.value as any)?.address?.zip ?? '',
  city: (member.value as any)?.address?.city ?? '',
  district: (member.value as any)?.address?.district ?? '',
  addressLine: (member.value as any)?.address?.line ?? '',

  // 4) 常用據點 + 電子報
  branch: (member.value as any)?.branch ?? '',       // 板橋/中和/三峽
  newsletter: (member.value as any)?.newsletter ?? false
})

// 常用據點選單（你需求指定三個）
const branches = ['板橋', '中和', '三峽']

// ====== 驗證 ======
const errors = reactive<Record<string, string>>({})

function validate() {
  Object.keys(errors).forEach(k => delete errors[k])

  // 姓名：必填，至少 2 個字元
  if (!form.fullName || form.fullName.trim().length < 2) {
    errors.fullName = '請輸入姓名（至少 2 個字元）'
  }
  // 生日：可選，但若有值需是 YYYY-MM-DD
  if (form.birthday && !/^\d{4}-\d{2}-\d{2}$/.test(form.birthday)) {
    errors.birthday = '生日格式須為 YYYY-MM-DD'
  }
  // 性別：必為 M/F/N
  if (!['M','F','N'].includes(form.gender)) {
    errors.gender = '請選擇性別'
  }
  // Email：格式檢查
  if (!form.email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
    errors.email = '請輸入有效的 Email'
  }
  // 行動電話：台灣手機 09 開頭 10 碼
  if (!/^09\d{8}$/.test(form.mobile)) {
    errors.mobile = '請輸入有效的行動電話（09 開頭 10 碼）'
  }
  // 郵遞區號：3 或 5 碼（可空）
  if (form.zip && !/^\d{3}(\d{2})?$/.test(form.zip)) {
    errors.zip = '郵遞區號需為 3 或 5 碼數字'
  }
  // 縣市：必填（自由輸入）
  if (!form.city || !form.city.trim()) {
    errors.city = '請輸入縣市'
  }
  // 鄉鎮市區：可空；若填至少 1 個字
  if (form.district && form.district.trim().length < 1) {
    errors.district = '請輸入正確的行政區'
  }
  // 地址：若填寫，至少 4 字元
  if (form.addressLine && form.addressLine.trim().length < 4) {
    errors.addressLine = '請輸入更完整的地址'
  }
  // 常用據點：可空；若有值需在選單內
  if (form.branch && !branches.includes(form.branch)) {
    errors.branch = '常用據點必須為選單項目（板橋 / 中和 / 三峽）'
  }
  return Object.keys(errors).length === 0
}

// ====== 送出 ======
const saving = ref(false)
const msg = ref('')
const ok = ref(false)

async function saveProfile() {
  msg.value = ''
  ok.value = false
  if (!validate()) {
    msg.value = '請修正表單錯誤後再儲存'
    return
  }
  saving.value = true
  try {
    const payload = {
      name: form.fullName,
      birthday: form.birthday || null,
      gender: form.gender,                // 'M'|'F'|'N'
      email: form.email,
      mobile: form.mobile,
      address: {
        zip: form.zip,
        city: form.city,
        district: form.district,
        line: form.addressLine
      },
      branch: form.branch || null,
      newsletter: !!form.newsletter
    }

    await http.post('/api/members/profile/update', payload)
    await auth.reloadMember?.()

    msg.value = '已更新'
    ok.value = true
  } catch (e: any) {
    msg.value = e?.response?.data?.message || '更新失敗，請稍後再試'
    ok.value = false
  } finally {
    saving.value = false
  }
}

// 性別顯示文字
const genderText = computed(() => {
  switch (form.gender) {
    case 'M': return '男'
    case 'F': return '女'
    default: return '不提供'
  }
})
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

      <!-- 生日 -->
      <label class="lab">生日</label>
      <div class="val">
        <input class="input" type="date" v-model="form.birthday" />
        <p v-if="errors.birthday" class="err">{{ errors.birthday }}</p>
      </div>

      <!-- 性別 -->
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
        <small class="muted">目前選擇：{{ genderText }}</small>
        <p v-if="errors.gender" class="err">{{ errors.gender }}</p>
      </div>

      <!-- Email + 驗證徽章 -->
      <label class="lab req">Email</label>
      <div class="val">
        <div class="inline">
          <input class="input" v-model.trim="form.email" type="email" />
          <span class="badge" :class="form.emailVerified ? 'ok':'warn'">
            {{ form.emailVerified ? '已驗證' : '未驗證' }}
          </span>
        </div>
        <p v-if="errors.email" class="err">{{ errors.email }}</p>
      </div>

      <!-- 行動電話 -->
      <label class="lab req">行動電話</label>
      <div class="val">
        <input class="input" v-model.trim="form.mobile" placeholder="例如：0912345678" />
        <p v-if="errors.mobile" class="err">{{ errors.mobile }}</p>
      </div>

      <!-- 地址（同一橫排：郵遞區號 / 縣市 / 鄉鎮市區） -->
      <label class="lab">地址</label>
      <div class="val">
        <div class="row-compact">
          <div class="cell">
            <input class="input" v-model.trim="form.zip" maxlength="5" placeholder="郵遞區號" />
            <p v-if="errors.zip" class="err">{{ errors.zip }}</p>
          </div>
          <div class="cell">
            <input class="input" v-model.trim="form.city" placeholder="縣市（輸入）" />
            <p v-if="errors.city" class="err">{{ errors.city }}</p>
          </div>
          <div class="cell">
            <input class="input" v-model.trim="form.district" placeholder="鄉鎮市區（輸入）" />
            <p v-if="errors.district" class="err">{{ errors.district }}</p>
          </div>
        </div>
      </div>

      <!-- 詳細地址 -->
      <label class="lab"></label>
      <div class="val">
        <input class="input" v-model.trim="form.addressLine" placeholder="路/街、段巷弄號樓..." />
        <p v-if="errors.addressLine" class="err">{{ errors.addressLine }}</p>
      </div>

      <!-- 常用據點 -->
      <label class="lab">常用據點</label>
      <div class="val">
        <select class="input" v-model="form.branch">
          <option value="">（未選擇）</option>
          <option v-for="b in branches" :key="b" :value="b">{{ b }}</option>
        </select>
        <p v-if="errors.branch" class="err">{{ errors.branch }}</p>
      </div>

      <!-- 是否訂閱電子報 -->
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
      <span v-if="msg" :class="['msg', ok ? 'ok':'err']">{{ msg }}</span>
    </div>
  </div>
</template>

<style scoped>
.title{margin:0 0 12px}
.grid{
  display:grid;
  grid-template-columns:180px 1fr;
  gap:10px 14px;
  align-items:center;
  margin-bottom:14px;
}
@media (max-width: 720px){
  .grid{grid-template-columns:1fr;align-items:stretch}
  .lab{margin-top:8px}
}
.lab{color:#333}
.lab.req::after{content:' *'; color:#d33}
.val{display:block}

.input{
  width:100%; padding:10px 12px; border:1px solid #dfe3e8; border-radius:10px; font-size:14px;
  background:#fff;
}

/* 三欄緊湊橫排：郵遞區號 / 縣市 / 鄉鎮市區 */
.row-compact{
  display:flex; gap:10px; align-items:flex-start; flex-wrap:wrap;
}
.row-compact .cell{ flex: 1 1 160px; min-width: 140px; }
.row-compact .cell:first-child{ flex: 0 0 120px; } /* 郵遞區號較短 */
@media (max-width: 720px){
  .row-compact .cell{ flex:1 1 100%; }
}

.seg{display:flex; gap:8px; flex-wrap:wrap}
.seg-item{
  display:flex; align-items:center; gap:6px;
  padding:8px 10px; border:1px solid #e5e7eb; border-radius:999px;
  background:#fff; cursor:pointer; user-select:none;
}
.seg-item input{accent-color:#0d6efd}

.inline{display:flex; align-items:center; gap:10px; flex-wrap:wrap}
.badge{
  display:inline-block; padding:2px 8px; border-radius:999px; font-size:12px; border:1px solid transparent;
}
.badge.ok{background:#e8f7ef; color:#139a50; border-color:#bfead0}
.badge.warn{background:#fff8e6; color:#9a6b13; border-color:#ffe2a1}

.check{display:flex; gap:8px; align-items:center}

.err{color:#c0392b; font-size:12px; margin-top:4px}
.muted{color:#6b7280; font-size:12px}

.actions{display:flex; gap:10px; align-items:center}
.btn{
  padding:10px 12px; border-radius:10px; border:1px solid #e5e7eb; background:#f8fafc; font-weight:600; cursor:pointer;
}
.btn.primary{background:#0d6efd; border-color:#0d6efd; color:#fff}

.msg{font-size:14px}
.msg.ok{color:#138a36}
.msg.err{color:#c0392b}
</style>
