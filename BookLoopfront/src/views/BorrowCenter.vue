<template>
  <div class="userdata-tabs">
    <!-- Nav Tabs -->
    <div role="tablist" class="tabs">
      <button :class="['tab', active==='borrow' && 'active']" @click="activate('borrow')"><i class="fa-solid fa-book"></i> 借閱紀錄</button>
      <button :class="['tab', active==='reserve' && 'active']" @click="activate('reserve')"><i class="fa-solid fa-file-medical"></i> 預約紀錄</button>
      <button :class="['tab', active==='penalty' && 'active']" @click="activate('penalty')"><i class="fa-solid fa-money-bill-1-wave"></i> 罰金紀錄</button>
       <button class="view-btn" @click="openOtherModal" aria-haspopup="dialog"><i class="fa-solid fa-scale-balanced"></i> 查看罰款規則</button>
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
    <transition name="backdrop">
  <div
    v-if="showRules"
    class="fixed inset-0 bg-black/40 z-40"
    @click.self="showRules=false"
    aria-hidden="true"
  />
</transition>
    <transition name="modal-top">
  <div v-if="showRules" class="fixed inset-0 z-50 pointer-events-none">
    <div class="pointer-events-auto mx-auto mt-10 w-[90%] max-w-3xl bg-white rounded-lg shadow-xl p-4">

      <div class="flex items-center justify-between mb-3">
        <h2 class="text-lg font-semibold px-3 py-1 rounded bg-green-100 text-green-800">罰款規則</h2>
       
      </div>

      <div v-if="loading" class="py-6 text-center">載入中…</div>
      <div v-else-if="error" class="py-6 text-red-600">{{ error }}</div>

      <div v-else>
        <table class="w-full border-collapse">
          <thead>
            <tr>
              <th class="border px-2 py-1 text-left">ID</th>
              <th class="border px-2 py-1 text-left">原因</th>
              <th class="border px-2 py-1 text-left">罰款類型</th>
              <th class="border px-2 py-1 text-right">單位金額</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in rules" :key="r.ruleID ?? r.RuleID">
              <td class="border px-2 py-1">{{ r.ruleID ?? r.RuleID }}</td>
              <td class="border px-2 py-1">{{ r.reasonCode ?? r.ReasonCode }}</td>
              <td class="border px-2 py-1">{{ r.chargeType ?? r.ChargeType }}</td>
              <td class="border px-2 py-1 text-right">{{ r.unitAmount ?? r.UnitAmount }}</td>
            </tr>
          </tbody>
        </table>

        <div v-if="!rules.length" class="text-center py-6 text-gray-500">
          沒有規則資料
        </div>
      </div>

      <div class="mt-4 text-right">
        <button @click="showRules=false" class="px-3 py-1 border rounded"><i class="fa-solid fa-circle-xmark"></i>關閉</button>
      </div>
    </div>
    </div> 
  </transition>
  </div>  
 
</template>

<script setup>
import { reactive, ref, onMounted, onBeforeUnmount  } from 'vue'
import DataPanel from '@/components/DataPanel.vue'
import http from '@/lib/http'


// 狀態
const showRules = ref(false);
const loading = ref(false);
const error = ref(null)
const rules = ref([])

// 按下按鈕：開 Modal -> 抓資料
async function openOtherModal() {
  showRules.value = true;
  loading.value = true;
  error.value = null;
  try {
    const res = await http.get('/api/PenatlyRules/rule')
    const data = res.data
    rules.value = (Array.isArray(data) ? data : []).map(d => ({
  RuleID: d.ruleID,
  ReasonCode: d.reasonCode,
  ChargeType: d.chargeType,
  UnitAmount: d.unitAmount,
}));
  } catch (e) {
    error.value = e?.response?.data?.message ?? e?.message ?? '載入失敗，請稍後再試'
  } finally {
    loading.value = false
  }
}
// Esc 關閉
function onEsc (e) {
  if (e.key === 'Escape' && showRules.value) showRules.value = false
}
onMounted(() => window.addEventListener("keydown", onEsc));
onBeforeUnmount(() => window.removeEventListener("keydown", onEsc));
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

.view-btn {
  margin-left: auto; padding: .5rem .75rem; border: 1px solid #ddd; background: #fff;
  border-radius: .5rem; cursor: pointer;
}

/* ===== Backdrop（黑色背景） ===== */
.modal-backdrop{
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,.4);
  z-index: 999;
}

/* ===== Modal 視窗 ===== */
.modal{
  position: fixed;
  top: 10%;                 /* 放高一點，營造「上方浮入」 */
  left: 50%;
  transform: translate(-50%, 0);
  width: min(900px, 92vw);
  max-height: 80vh;
  background: #fff;
  border-radius: .75rem;
  box-shadow: 0 12px 30px rgba(0,0,0,.18);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  z-index: 1000;
}

/* ===== Backdrop（黑色背景） ===== */
.modal-backdrop{
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,.4);
  z-index: 999;
}

/* ===== Modal 視窗 ===== */
.modal{
  position: fixed;
  top: 10%;                 /* 放高一點，營造「上方浮入」 */
  left: 50%;
  transform: translate(-50%, 0);
  width: min(900px, 92vw);
  max-height: 80vh;
  background: #fff;
  border-radius: .75rem;
  box-shadow: 0 12px 30px rgba(0,0,0,.18);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  z-index: 1000;
}

/* ===== Header（標題 + 右上角 X） ===== */
.modal-header{
  display: flex;
  align-items: center;
  justify-content: space-between;   /* 標題在左、X 在最右 */
  padding: .75rem 1rem;
  border-bottom: 1px solid #eee;
}
.modal-title{
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
}

/* 右上角關閉按鈕 */
.close-btn{
  margin-left: auto;                /* 保險：推到最右 */
  background: transparent;
  border: none;
  font-size: 1.25rem;
  line-height: 1;
  padding: .25rem .5rem;
  cursor: pointer;
  color: #555;
}
.close-btn:hover{ color: #222; }
.close-btn:focus-visible{
  outline: 2px solid #6aa3ff;
  outline-offset: 2px;
  border-radius: .375rem;
}

/* ===== 內容與頁腳 ===== */
.modal-body{
  overflow: auto;
  padding: 1rem;
}
.modal-footer{
  padding: .75rem 1rem;
  border-top: 1px solid #eee;
  text-align: right;
}

/* ===== 進場/退場動畫（搭配 <transition name="...">） ===== */
/* 背景淡入 */
.backdrop-enter-active, .backdrop-leave-active{ transition: opacity .2s ease; }
.backdrop-enter-from, .backdrop-leave-to{ opacity: 0; }

/* 視窗自上方浮入 */
.modal-top-enter-active, .modal-top-leave-active{ transition: all .25s ease; }
.modal-top-enter-from, .modal-top-leave-to{
  opacity: 0;
  transform: translate(-50%, -24px);   /* 從更上方開始 */
}

.userdata-tabs {
  padding: 40px;            /* 內距，自行調整 */
  /* 常見一起設的 */
  box-sizing: border-box;   /* 讓 padding 不把容器撐大 */
}

</style>
