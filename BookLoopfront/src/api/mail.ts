import http from "./http";

export type MailNudgeResponse = {
    hasItem: boolean;
    subject?: string;
    preview?: string;
    name?: string;
    viewUrl?: string;
    rid?: number;
};

// 取得未開信件提示
export async function getUnopenedMail() {
    const { data } = await http.get<MailNudgeResponse>("/mail/unopened");
    return data;
}
