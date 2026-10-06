import axios from "axios";
import { expect, test } from "bun:test";

import type { AiConfig } from "../src/stores/use-config-store";

const storage = new Map<string, string>();
globalThis.localStorage = {
    getItem: (key) => storage.get(key) || null,
    setItem: (key, value) => void storage.set(key, value),
    removeItem: (key) => void storage.delete(key),
    clear: () => storage.clear(),
    key: (index) => [...storage.keys()][index] || null,
    get length() { return storage.size; },
} as Storage;

const { createVideoGenerationTask, pollVideoGenerationTask } = await import("../src/services/api/video");

test("polls a video task through the channel that created it", async () => {
    const previousPost = axios.post;
    const previousGet = axios.get;
    const requests: string[] = [];
    axios.post = (async (url: string) => {
        requests.push(url);
        return { data: { id: "task-1", status: "queued" } };
    }) as typeof axios.post;
    axios.get = (async (url: string) => {
        requests.push(url);
        return { data: { id: "task-1", status: "completed", url: "https://media.example.test/video.mp4" } };
    }) as typeof axios.get;

    try {
        const config = {
            baseUrl: "https://channel-a.example.test/v1",
            apiKey: "synthetic-key",
            apiFormat: "openai",
            model: "channel-a::video-model",
            videoModel: "channel-a::video-model",
            videoSeconds: "6",
            vquality: "720",
            videoMode: "frames",
            videoGenerateAudio: "true",
            videoWatermark: "false",
            channels: [
                { id: "channel-a", name: "A", baseUrl: "https://channel-a.example.test/v1", apiKey: "synthetic-key", apiFormat: "openai", models: [{ name: "video-model", capability: "video" }] },
                { id: "channel-b", name: "B", baseUrl: "https://channel-b.example.test/v1", apiKey: "synthetic-key", apiFormat: "openai", models: [{ name: "video-model", capability: "video" }] },
            ],
        } as AiConfig;
        const task = await createVideoGenerationTask(config, "test prompt");
        config.channels = [config.channels[1], config.channels[0]];
        config.baseUrl = "https://channel-b.example.test/v1";
        config.model = "channel-b::video-model";

        const state = await pollVideoGenerationTask(config, task);

        expect(state.status).toBe("completed");
        expect(requests[0]).toBe("https://channel-a.example.test/v1/videos");
        expect(requests[1]).toBe("https://channel-a.example.test/v1/videos/task-1");
    } finally {
        axios.post = previousPost;
        axios.get = previousGet;
    }
});
