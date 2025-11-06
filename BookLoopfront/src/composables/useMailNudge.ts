import { ref } from "vue";
import { getUnopenedMail, MailNudgeResponse } from "@/api/mail";
import { hasSeen } from "@/utils/nudgeStore";

export function useMailNudge() {
    const item = ref<MailNudgeResponse | null>(null);
    const loading = ref(false);
    const error = ref<string | null>(null);

    async function fetchOnce() {
        try {
            loading.value = true;
            const res = await getUnopenedMail();
            if (!res.hasItem || !res.rid || hasSeen(res.rid)) {
                item.value = null;
            } else {
                item.value = res;
            }
        } catch (err: any) {
            console.error(err);
            error.value = err.message;
            item.value = null;
        } finally {
            loading.value = false;
        }
    }

    return { item, loading, error, fetchOnce };
}
