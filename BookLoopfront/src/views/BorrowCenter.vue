<template>
  <div class="userdata-tabs">
    <!-- Nav Tabs -->
    <div role="tablist" class="tabs">
      <button :class="['tab', active==='borrow' && 'active']" @click="activate('borrow')">借閱紀錄</button>
      <button :class="['tab', active==='reserve' && 'active']" @click="activate('reserve')">預約紀錄</button>
      <button :class="['tab', active==='penalty' && 'active']" @click="activate('penalty')">罰金紀錄</button>
    </div>

    <!-- 借閱紀錄 -->
    <section v-show="active==='borrow'" aria-labelledby="borrow-tab">
      <DataPanel :state="borrow.state" empty-text="目前沒有借閱紀錄。">
        <table class="table">
          <thead>
            <tr>
              <th>書籍</th>
              <th>借閱人</th>
              <th>借閱日</th>
              <th>歸還日</th>
              <th>逾期日</th>
              <th>借閱情況</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in borrow.state.data" :key="r.recordID">
              <td>{{ r.bookTitle }}</td>
              <td>{{ r.memberName }}</td>
              <td>{{ safeDate(r.borrowDate) }}</td>
              <td>{{ r.returnDate ? safeDate(r.returnDate) : '' }}</td>
              <td>{{ r.dueDate ? safeDate(r.dueDate) : '—' }}</td>
              <td :class="statusClass(r.conditionName)">{{ r.conditionName }}</td>
            </tr>
          </tbody>
        </table>
      </DataPanel>
    </section>

    <!-- 預約紀錄 -->
    <section v-show="active==='reserve'" aria-labelledby="reserve-tab">
      <DataPanel :state="reserve.state" empty-text="目前沒有預約紀錄。">
        <table class="table">
          <thead>
            <tr>
              <th>書籍</th>
              <th>預約人</th>
              <th>預約時間</th>
              <th>狀態</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in reserve.state.data" :key="r.reservationID">
              <td>{{ r.bookTitle }}</td>
              <td>{{ r.memberName }}</td>
              <td>{{ safeDateTime(r.reservationAt) }}</td>
              <td>{{ r.statusName }}</td>
            </tr>
          </tbody>
        </table>
      </DataPanel>
    </section>

    <!-- 罰金紀錄 -->
    <section v-show="active==='penalty'" aria-labelledby="penalty-tab">
      <DataPanel :state="penalty.state" empty-text="目前沒有罰金紀錄。">
        <table class="table">
          <thead>
            <tr>
              <th>書名</th>
              <th>會員名</th>
              <th>原因</th>
              <th>總金額</th>
              <th>建立時間</th>
              <th>繳清時間</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in penalty.state.data" :key="p.penaltyID"> 
              <td>{{ p.bookTitle }}</td>             
              <td>{{ p.memberName }}</td>
              <td>{{ p.reason }}</td>
              <td>{{ p.totalMoney }}</td>
              <td>{{ safeDateTime(p.createdAt) }}</td>
              <td>{{ p.paidAt ? safeDateTime(p.paidAt) : '' }}</td>
            </tr>
          </tbody>
        </table>
      </DataPanel>
    </section>
  </div>
</template>

<script setup>
import { reactive, ref, onMounted } from 'vue'
import DataPanel from '@/components/DataPanel.vue'
import http from '@/lib/http'

/** 取得資料的可重用 composable */
function useTabFetcher(path) {
  const state = reactive({ loading: false, error: '', data: [] })
  let loaded = false

  const load = async () => {
    if (loaded) return
    state.loading = true
    state.error = ''
    try {
      const { data } = await http.get(path) // 會自動加 Authorization: Bearer ...
      console.log('[API OK]', path, { length: Array.isArray(data) ? data.length : 'not-array' }, data?.[0])
      state.data = Array.isArray(data) ? data : []
      loaded = true
    } catch (e) {
      state.error = e?.message || String(e)
    } finally {
      state.loading = false
    }
  }

  return { state, load }
}

/** 分頁 tab 狀態 */
const active  = ref('borrow')
const borrow  = useTabFetcher('/api/BorrowRecords/records?memberId=616')
const reserve = useTabFetcher('/api/reservations/redata?memberId=616')
const penalty = useTabFetcher('/api/PenaltyTransactions/penalties?memberId=616')

function activate(name) {
  active.value = name
  if (name === 'borrow')  borrow.load()
  if (name === 'reserve') reserve.load()
  if (name === 'penalty') penalty.load()
}

onMounted(() => {
  activate('borrow')
})

/** ---------------- 日期處理：不再使用 toISOString() ---------------- */
function coerceDate(input) {
  if (!input) return null

  // 已是 Date
  if (input instanceof Date) {
    return Number.isNaN(input.getTime()) ? null : input
  }

  // number timestamp（秒或毫秒）
  if (typeof input === 'number') {
    const ms = input > 1e12 ? input : input * 1000
    const dt = new Date(ms)
    return Number.isNaN(dt.getTime()) ? null : dt
  }

  if (typeof input === 'string') {
    // /Date(1730908800000)/
    const m = input.match(/^\/Date\((\d+)\)\/$/)
    if (m) {
      const dt = new Date(Number(m[1]))
      return Number.isNaN(dt.getTime()) ? null : dt
    }

    // 換掉斜線，處理 YYYY/MM/DD 或 YYYY/MM/DD HH:mm:ss
    const normalized = input.trim().replace(/\//g, '-')
    const dt = new Date(normalized)
    if (!Number.isNaN(dt.getTime())) return dt

    // 再嘗試只取日期部分
    const onlyDate = normalized.slice(0, 10)
    const dt2 = new Date(onlyDate)
    return Number.isNaN(dt2.getTime()) ? null : dt2
  }

  return null
}

function pad2(n) {
  return String(n).padStart(2, '0')
}

function safeDate(val) {
  const dt = coerceDate(val)
  if (!dt) return '—'
  return `${dt.getFullYear()}-${pad2(dt.getMonth() + 1)}-${pad2(dt.getDate())}`
}

function safeDateTime(val) {
  const dt = coerceDate(val)
  if (!dt) return '—'
  return `${dt.getFullYear()}-${pad2(dt.getMonth() + 1)}-${pad2(dt.getDate())} ${pad2(dt.getHours())}:${pad2(dt.getMinutes())}`
}

/** 樣式工具 */
function statusClass(name) {
  return [
    'status',
    name === '借出' && 'text-primary',
    name === '逾期' && 'text-danger',
    name === '歸還' && 'text-success'
  ]
}
</script>

<style scoped>
.tabs { display: flex; gap: .5rem; margin-bottom: 1rem; }
.tab { padding: .5rem .75rem; border: 1px solid #ddd; background: #f7f7f7; cursor: pointer; }
.tab.active { background: white; border-bottom-color: white; font-weight: 600; }
.table { width: 100%; border-collapse: collapse; }
.table th, .table td { border: 1px solid #e5e5e5; padding: .5rem .75rem; }
.text-primary { color: #0d6efd; }
.text-danger { color: #dc3545; }
.text-success { color: #198754; }
</style>
