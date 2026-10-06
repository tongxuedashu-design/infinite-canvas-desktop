import { expect, test } from "bun:test";

import { normalizeDesktopConfigurationSnapshot } from "../src/services/desktop";

test("normalizes the persisted PascalCase desktop snapshot", () => {
    expect(normalizeDesktopConfigurationSnapshot({
        SchemaVersion: 1,
        Channels: [{
            Id: "channel-1",
            Name: "主渠道",
            BaseUrl: "https://example.test/v1",
            ApiFormat: "openai",
            Models: [{ Name: "gpt-image-2", Capability: "image", Script: "" }],
        }],
        SelectedModels: { Default: "channel-1::gpt-image-2", Image: "channel-1::gpt-image-2" },
    })).toEqual({
        schemaVersion: 1,
        channels: [{
            id: "channel-1",
            name: "主渠道",
            baseUrl: "https://example.test/v1",
            apiFormat: "openai",
            models: [{ name: "gpt-image-2", capability: "image", script: undefined }],
        }],
        selectedModels: { default: "channel-1::gpt-image-2", image: "channel-1::gpt-image-2" },
    });
});

test("keeps an empty or invalid snapshot safe to consume", () => {
    expect(normalizeDesktopConfigurationSnapshot(null)).toBeNull();
    expect(normalizeDesktopConfigurationSnapshot({ Channels: "invalid" })).toEqual({
        schemaVersion: 1,
        channels: [],
        selectedModels: {},
    });
});

test("keeps the local proxy settings in the desktop snapshot", () => {
    expect(normalizeDesktopConfigurationSnapshot({
        Channels: [],
        SelectedModels: {},
        LocalProxy: { Enabled: true, Url: "http://127.0.0.1:23210" },
    })?.localProxy).toEqual({ enabled: true, url: "http://127.0.0.1:23210" });
});

