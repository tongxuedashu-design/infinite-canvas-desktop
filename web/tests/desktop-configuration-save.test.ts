import { expect, test } from "bun:test";

import type { AiConfig } from "../src/stores/use-config-store";
import { saveDesktopConfiguration } from "../src/services/desktop";

test("canvas configuration save writes channel credentials and metadata through the bridge", async () => {
    const calls: Array<{ url: string; body: Record<string, unknown> }> = [];
    const storage = new Map<string, string>([["infinite-canvas:desktop-session", JSON.stringify({ bridgeUrl: "http://127.0.0.1:43123", token: "synthetic-token" })]]);
    const previousSessionStorage = globalThis.sessionStorage;
    const previousFetch = globalThis.fetch;
    globalThis.sessionStorage = {
        getItem: (key) => storage.get(key) || null,
        setItem: (key, value) => void storage.set(key, value),
        removeItem: (key) => void storage.delete(key),
        clear: () => storage.clear(),
        key: (index) => [...storage.keys()][index] || null,
        get length() { return storage.size; },
    } as Storage;
    globalThis.fetch = (async (input, init) => {
        calls.push({ url: String(input), body: JSON.parse(String(init?.body || "{}")) });
        return new Response(null, { status: 204 });
    }) as typeof fetch;

    try {
        const config = {
            channels: [{ id: "channel-1", name: "渠道 1", baseUrl: "https://example.test/v1", apiKey: "synthetic-key", apiFormat: "openai", models: [{ name: "model-1", capability: "text" }] }],
            imageModel: "channel-1::model-1",
            videoModel: "",
            textModel: "channel-1::model-1",
            audioModel: "",
            model: "channel-1::model-1",
        } as AiConfig;
        await saveDesktopConfiguration(config, "model-1");

        expect(calls.map((call) => new URL(call.url).pathname)).toEqual(["/api/credentials/sync", "/api/config", "/api/config/snapshot"]);
        expect(calls[0].body).toEqual({ channels: [{ id: "channel-1", apiKey: "synthetic-key" }] });
        expect(calls[1].body).toMatchObject({ baseUrl: "https://example.test/v1", apiKey: "synthetic-key", model: "model-1" });
        expect(calls[2].body).toMatchObject({ channels: [{ id: "channel-1", baseUrl: "https://example.test/v1" }] });
    } finally {
        globalThis.sessionStorage = previousSessionStorage;
        globalThis.fetch = previousFetch;
    }
});
