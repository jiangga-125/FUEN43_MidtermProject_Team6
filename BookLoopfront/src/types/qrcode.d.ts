// src/types/qrcode.d.ts
declare module 'qrcode' {
    export function toCanvas(
        canvas: HTMLCanvasElement,
        text: string,
        opts?: any
    ): Promise<void>;
    export function toDataURL(text: string, opts?: any): Promise<string>;
    const _default: any;
    export default _default;
}
