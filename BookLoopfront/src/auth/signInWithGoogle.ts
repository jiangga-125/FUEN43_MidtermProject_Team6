// 依你的後端位址調整；也可用 import.meta.env
const BACKEND = import.meta.env.VITE_BACKEND_URL ?? "https://localhost:7176";

/**
 * 叫後端啟動 Google 登入，並透過 postMessage 接回 JWT
 */
export function signInWithGoogle(returnUrl = "/") {
    const url = `${BACKEND}/api/auth/external/google?returnUrl=${encodeURIComponent(returnUrl)}`;
    const w = window.open(url, "google_signin", "width=480,height=640");

    return new Promise<{ accessToken: string; expiresAt: number }>((resolve, reject) => {
        function handler(ev: MessageEvent) {
            const data = ev.data;
            if (!data || data.provider !== "google") return; // 只處理我們的事件

            window.removeEventListener("message", handler);
            try { w?.close(); } catch { }

            if (data.type === "oauth-success") {
                resolve({ accessToken: data.accessToken, expiresAt: data.expiresAt });
            } else {
                reject(new Error(data.message || "OAuth error"));
            }
        }

        window.addEventListener("message", handler);

        // 使用者關掉視窗或被封鎖的防呆（可選）
        const timer = setInterval(() => {
            if (w && w.closed) {
                clearInterval(timer);
                window.removeEventListener("message", handler);
            }
        }, 500);
    });
}
