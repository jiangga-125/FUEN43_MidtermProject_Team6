function genUuidV4() {
    // 簡易 UUIDv4（符合展示需求）
    const bytes = new Uint8Array(16);
    crypto.getRandomValues(bytes);
    // 版本/variant bits
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    const toHex = (n: number) => n.toString(16).padStart(2, '0');
    const b = Array.from(bytes).map(toHex).join('');
    return `${b.slice(0, 8)}-${b.slice(8, 12)}-${b.slice(12, 16)}-${b.slice(16, 20)}-${b.slice(20)}`;
}

const KEY = 'bookloop_device_hash';

export function getDeviceHash(): string {
    try {
        let v = localStorage.getItem(KEY);
        if (!v) {
            v = genUuidV4();
            localStorage.setItem(KEY, v);
        }
        return v;
    } catch {
        // localStorage 不可用時也能運作（但不保證持久）
        return genUuidV4();
    }
}
