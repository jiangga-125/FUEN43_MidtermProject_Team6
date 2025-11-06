// src/utils/nudgeStore.ts
const KEY_SEEN = "mail-nudge-seen";
const KEY_SNOOZE = "mail-nudge-snooze";

export function markSeen(rid: number) {
    const seen = new Set<number>(JSON.parse(localStorage.getItem(KEY_SEEN) || "[]"));
    seen.add(rid);
    localStorage.setItem(KEY_SEEN, JSON.stringify([...seen]));
}

export function hasSeen(rid: number): boolean {
    const seen = new Set<number>(JSON.parse(localStorage.getItem(KEY_SEEN) || "[]"));
    return seen.has(rid);
}

export function setSnooze(ms: number) {
    localStorage.setItem(KEY_SNOOZE, String(Date.now() + ms));
}

export function snoozed(): boolean {
    const until = Number(localStorage.getItem(KEY_SNOOZE) || "0");
    return until > Date.now();
}

export function clearSnooze() {
    localStorage.removeItem('nudge_snooze_until');
}

