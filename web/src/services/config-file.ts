import { saveAs } from "file-saver";

import i18n from "@/i18n";
import { isDesktopApp } from "@/services/desktop";
import { useConfigStore, type AiConfig, type WebdavSyncConfig } from "@/stores/use-config-store";
import { usePromptSourceStore, type PromptSourceSchedule } from "@/stores/use-prompt-source-store";
import type { PromptSource } from "@/services/api/prompt-source-presets";

type AppConfigFile = {
    app: "infinite-canvas";
    version: 1;
    exportedAt: string;
    config: AiConfig;
    webdav: WebdavSyncConfig;
    promptSources: {
        sources: PromptSource[];
        schedule: PromptSourceSchedule;
    };
};

export function exportAppConfig() {
    const { config, webdav } = useConfigStore.getState();
    const { sources, schedule } = usePromptSourceStore.getState();
    const exportedConfig = isDesktopApp()
        ? { ...config, apiKey: "", channels: config.channels.map((channel) => ({ ...channel, apiKey: "" })) }
        : config;
    const data: AppConfigFile = { app: "infinite-canvas", version: 1, exportedAt: new Date().toISOString(), config: exportedConfig, webdav, promptSources: { sources, schedule } };
    saveAs(new Blob([JSON.stringify(data, null, 2)], { type: "application/json;charset=utf-8" }), "infinite-canvas-config.json");
}

export async function importAppConfig(file: File) {
    let data: AppConfigFile;
    try {
        data = JSON.parse(await file.text()) as AppConfigFile;
    } catch {
        throw new Error(i18n.t("config.invalidFile"));
    }
    if (data.app !== "infinite-canvas" || data.version !== 1 || !data.config || !data.webdav || !data.promptSources) throw new Error(i18n.t("config.invalidFile"));
    const config = isDesktopApp() ? preserveDesktopCredentials(data.config) : data.config;
    useConfigStore.setState({ config, webdav: data.webdav });
    usePromptSourceStore.setState(data.promptSources);
}

function preserveDesktopCredentials(importedConfig: AiConfig): AiConfig {
    const currentConfig = useConfigStore.getState().config;
    const currentById = new Map(currentConfig.channels.map((channel) => [channel.id, channel]));
    const currentByBaseUrl = new Map(currentConfig.channels.map((channel) => [credentialBaseUrl(channel.baseUrl), channel]));
    const channels = importedConfig.channels.map((channel) => {
        if (channel.apiKey) return channel;
        const current = currentById.get(channel.id) || currentByBaseUrl.get(credentialBaseUrl(channel.baseUrl));
        return current?.apiKey ? { ...channel, apiKey: current.apiKey } : channel;
    });
    return { ...importedConfig, apiKey: importedConfig.apiKey || currentConfig.apiKey, channels };
}

function credentialBaseUrl(value: string) {
    return value.trim().replace(/\/+$/, "").replace(/\/v1$/i, "");
}
