import axios from "axios";
import { expect, test } from "bun:test";

const storage = new Map<string, string>();
globalThis.localStorage = {
    getItem: (key) => storage.get(key) || null,
    setItem: (key, value) => void storage.set(key, value),
    removeItem: (key) => void storage.delete(key),
    clear: () => storage.clear(),
    key: (index) => [...storage.keys()][index] || null,
    get length() {
        return storage.size;
    },
} as Storage;

const { requestGeneration } = await import("../src/services/api/image");

test("routes image generation to imageModel when the default model is from another capability", async () => {
    const previousPost = axios.post;
    let request: { url: string; body: Record<string, unknown> } | undefined;
    axios.post = (async (url: string, body: Record<string, unknown>) => {
        request = { url, body };
        return { data: { data: [{ b64_json: "c3ludGhldGlj" }] } };
    }) as typeof axios.post;

    try {
        await requestGeneration({
            channelMode: "local",
            baseUrl: "https://default.example.test/v1",
            apiKey: "synthetic-key",
            apiFormat: "openai",
            channels: [
                { id: "video-channel", name: "视频", baseUrl: "https://video.example.test/v1", apiKey: "video-key", apiFormat: "openai", models: [{ name: "video-model", capability: "video" }] },
                { id: "image-channel", name: "图片", baseUrl: "https://image.example.test/v1", apiKey: "image-key", apiFormat: "openai", models: [{ name: "image-model", capability: "image" }] },
            ],
            model: "video-channel::video-model",
            imageModel: "image-channel::image-model",
            videoModel: "video-channel::video-model",
            textModel: "",
            audioModel: "",
            audioVoice: "alloy",
            audioFormat: "mp3",
            audioSpeed: "1",
            audioInstructions: "",
            videoSeconds: "6",
            vquality: "720",
            videoGenerateAudio: "true",
            videoWatermark: "false",
            videoMode: "frames",
            systemPrompt: "",
            reasoningEffort: "auto",
            models: ["video-channel::video-model", "image-channel::image-model"],
            quality: "auto",
            size: "1:1",
            background: "",
            count: "1",
            canvasImageCount: "1",
            proxyEnabled: false,
            proxyUrl: "http://127.0.0.1:23210",
        });
    } finally {
        axios.post = previousPost;
    }

    expect(request).toMatchObject({
        url: "https://image.example.test/v1/images/generations",
        body: { model: "image-model" },
    });
});

test("keeps an explicitly selected per-node image model", async () => {
    const previousPost = axios.post;
    let request: { url: string; body: Record<string, unknown> } | undefined;
    axios.post = (async (url: string, body: Record<string, unknown>) => {
        request = { url, body };
        return { data: { data: [{ b64_json: "c3ludGhldGlj" }] } };
    }) as typeof axios.post;

    try {
        await requestGeneration({
            channelMode: "local",
            baseUrl: "https://default.example.test/v1",
            apiKey: "synthetic-key",
            apiFormat: "openai",
            channels: [
                { id: "channel-a", name: "A", baseUrl: "https://a.example.test/v1", apiKey: "a-key", apiFormat: "openai", models: [{ name: "image-a", capability: "image" }] },
                { id: "channel-b", name: "B", baseUrl: "https://b.example.test/v1/chat/completions?api-version=test", apiKey: "b-key", apiFormat: "openai", models: [{ name: "image-b", capability: "image" }] },
            ],
            model: "channel-b::image-b",
            imageModel: "channel-a::image-a",
            videoModel: "",
            textModel: "",
            audioModel: "",
            audioVoice: "alloy",
            audioFormat: "mp3",
            audioSpeed: "1",
            audioInstructions: "",
            videoSeconds: "6",
            vquality: "720",
            videoGenerateAudio: "true",
            videoWatermark: "false",
            videoMode: "frames",
            systemPrompt: "",
            reasoningEffort: "auto",
            models: ["channel-a::image-a", "channel-b::image-b"],
            quality: "auto",
            size: "1:1",
            background: "",
            count: "1",
            canvasImageCount: "1",
            proxyEnabled: false,
            proxyUrl: "http://127.0.0.1:23210",
        });
    } finally {
        axios.post = previousPost;
    }

    expect(request).toMatchObject({
        url: "https://b.example.test/v1/images/generations?api-version=test",
        body: { model: "image-b" },
    });
});

test("accepts a single image object and a data URL returned by a compatible provider", async () => {
    const previousPost = axios.post;
    axios.post = (async () => ({ data: { data: { b64_json: "data:image/png;base64,c3ludGhldGlj" } } })) as typeof axios.post;

    try {
        const result = await requestGeneration({
            channelMode: "local",
            baseUrl: "https://image.example.test/v1",
            apiKey: "synthetic-key",
            apiFormat: "openai",
            channels: [{ id: "image-channel", name: "图片", baseUrl: "https://image.example.test/v1", apiKey: "image-key", apiFormat: "openai", models: [{ name: "image-model", capability: "image" }] }],
            model: "image-channel::image-model",
            imageModel: "image-channel::image-model",
            videoModel: "",
            textModel: "",
            audioModel: "",
            audioVoice: "alloy",
            audioFormat: "mp3",
            audioSpeed: "1",
            audioInstructions: "",
            videoSeconds: "6",
            vquality: "720",
            videoGenerateAudio: "true",
            videoWatermark: "false",
            videoMode: "frames",
            systemPrompt: "",
            reasoningEffort: "auto",
            models: ["image-channel::image-model"],
            quality: "auto",
            size: "1:1",
            background: "",
            count: "1",
            canvasImageCount: "1",
            proxyEnabled: false,
            proxyUrl: "http://127.0.0.1:23210",
        }, "test prompt");

        expect(result).toHaveLength(1);
        expect(result[0]?.dataUrl).toBe("data:image/png;base64,c3ludGhldGlj");
    } finally {
        axios.post = previousPost;
    }
});

test("decodes a JSON response delivered with a text content type", async () => {
    const previousPost = axios.post;
    axios.post = (async () => ({ data: JSON.stringify({ data: [{ b64_json: "c3ludGhldGlj" }] }) })) as typeof axios.post;

    try {
        const result = await requestGeneration({
            channelMode: "local",
            baseUrl: "https://image.example.test/v1",
            apiKey: "synthetic-key",
            apiFormat: "openai",
            channels: [{ id: "image-channel", name: "图片", baseUrl: "https://image.example.test/v1", apiKey: "image-key", apiFormat: "openai", models: [{ name: "image-model", capability: "image" }] }],
            model: "image-channel::image-model",
            imageModel: "image-channel::image-model",
            videoModel: "",
            textModel: "",
            audioModel: "",
            audioVoice: "alloy",
            audioFormat: "mp3",
            audioSpeed: "1",
            audioInstructions: "",
            videoSeconds: "6",
            vquality: "720",
            videoGenerateAudio: "true",
            videoWatermark: "false",
            videoMode: "frames",
            systemPrompt: "",
            reasoningEffort: "auto",
            models: ["image-channel::image-model"],
            quality: "auto",
            size: "1:1",
            background: "",
            count: "1",
            canvasImageCount: "1",
            proxyEnabled: false,
            proxyUrl: "http://127.0.0.1:23210",
        }, "test prompt");

        expect(result[0]?.dataUrl).toBe("data:image/png;base64,c3ludGhldGlj");
    } finally {
        axios.post = previousPost;
    }
});

test("unwraps nested gateway envelopes and keeps remote image URLs", async () => {
    const previousPost = axios.post;
    axios.post = (async () => ({
        data: JSON.stringify({ result: { data: { image_url: "https://cdn.example.test/generated.png" } } }),
    })) as typeof axios.post;

    try {
        const result = await requestGeneration({
            channelMode: "local",
            baseUrl: "https://image.example.test/v1",
            apiKey: "synthetic-key",
            apiFormat: "openai",
            channels: [{ id: "image-channel", name: "图片", baseUrl: "https://image.example.test/v1", apiKey: "image-key", apiFormat: "openai", models: [{ name: "image-model", capability: "image" }] }],
            model: "image-channel::image-model",
            imageModel: "image-channel::image-model",
            videoModel: "",
            textModel: "",
            audioModel: "",
            audioVoice: "alloy",
            audioFormat: "mp3",
            audioSpeed: "1",
            audioInstructions: "",
            videoSeconds: "6",
            vquality: "720",
            videoGenerateAudio: "true",
            videoWatermark: "false",
            videoMode: "frames",
            systemPrompt: "",
            reasoningEffort: "auto",
            models: ["image-channel::image-model"],
            quality: "auto",
            size: "1:1",
            background: "",
            count: "1",
            canvasImageCount: "1",
            proxyEnabled: false,
            proxyUrl: "http://127.0.0.1:23210",
        }, "test prompt");

        expect(result).toHaveLength(1);
        expect(result[0]?.dataUrl).toBe("https://cdn.example.test/generated.png");
    } finally {
        axios.post = previousPost;
    }
});

test("extracts the final image from an image-generation SSE response", async () => {
    const previousPost = axios.post;
    axios.post = (async () => ({
        status: 200,
        headers: { "content-type": "text/event-stream" },
        data: [
            'event: image_generation.partial_image\ndata: {"type":"image_generation.partial_image","b64_json":"cGFydGlhbA=="}',
            'event: image_generation.completed\ndata: {"type":"image_generation.completed","b64_json":"ZmluYWw="}',
            "data: [DONE]",
        ].join("\n\n"),
    })) as typeof axios.post;

    try {
        const result = await requestGeneration({
            channelMode: "local",
            baseUrl: "https://image.example.test/v1",
            apiKey: "synthetic-key",
            apiFormat: "openai",
            channels: [{ id: "image-channel", name: "图片", baseUrl: "https://image.example.test/v1", apiKey: "image-key", apiFormat: "openai", models: [{ name: "image-model", capability: "image" }] }],
            model: "image-channel::image-model",
            imageModel: "image-channel::image-model",
            videoModel: "",
            textModel: "",
            audioModel: "",
            audioVoice: "alloy",
            audioFormat: "mp3",
            audioSpeed: "1",
            audioInstructions: "",
            videoSeconds: "6",
            vquality: "720",
            videoGenerateAudio: "true",
            videoWatermark: "false",
            videoMode: "frames",
            systemPrompt: "",
            reasoningEffort: "auto",
            models: ["image-channel::image-model"],
            quality: "auto",
            size: "1:1",
            background: "",
            count: "1",
            canvasImageCount: "1",
            proxyEnabled: false,
            proxyUrl: "http://127.0.0.1:23210",
        }, "test prompt");

        expect(result).toHaveLength(1);
        expect(result[0]?.dataUrl).toBe("data:image/png;base64,ZmluYWw=");
    } finally {
        axios.post = previousPost;
    }
});
