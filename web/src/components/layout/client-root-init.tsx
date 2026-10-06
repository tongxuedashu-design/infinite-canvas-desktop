import type { ReactNode } from "react";
import { useEffect, useRef, useState } from "react";
import { App } from "antd";
import { useTranslation } from "react-i18next";

import { decodeChannelModel, guessCapability, modelOptionsFromChannels, normalizeModelOptionValue, useConfigStore } from "@/stores/use-config-store";
import { testLocalProxy } from "@/services/api/local-proxy";
import { usePromptSourceScheduler } from "@/hooks/use-prompt-source-scheduler";
import { createDesktopConfigurationSnapshot, hydrateDesktopCredentials, initializeDesktopSession, isDesktopApp, isDesktopBridgeUnavailable, loadDesktopApiConfig, loadDesktopConfigurationSnapshot, saveDesktopConfiguration, saveDesktopApiConfig } from "@/services/desktop";

export function ClientRootInit({ children }: { children: ReactNode }) {
    const { message } = App.useApp();
    const { t } = useTranslation();
    const handledConfigParams = useRef(false);
    const importChannelCredentials = useConfigStore((state) => state.importChannelCredentials);
    const openConfigDialog = useConfigStore((state) => state.openConfigDialog);
    const config = useConfigStore((state) => state.config);
    const [desktopCredentialsReady, setDesktopCredentialsReady] = useState(false);
    const desktopStarted = useRef(false);
    const lastCredentialFingerprint = useRef("");

    usePromptSourceScheduler();

    useEffect(() => {
        if (desktopStarted.current) return;
        initializeDesktopSession();
        if (!isDesktopApp()) return;
        desktopStarted.current = true;
        const initialize = async () => {
            let phase: "load" | "hydrate" | "save" = "load";
            try {
                const current = useConfigStore.getState().config;
                const [desktopConfig, desktopSnapshot] = await Promise.all([
                    loadDesktopApiConfig(),
                    loadDesktopConfigurationSnapshot(),
                ]);
                const snapshotChannels = desktopSnapshot?.channels || [];
                let channels = snapshotChannels.length
                    ? snapshotChannels.map((channel) => ({
                        id: channel.id,
                        name: channel.name,
                        baseUrl: channel.baseUrl,
                        apiKey: current.channels.find((item) => item.id === channel.id)?.apiKey || "",
                        apiFormat: channel.apiFormat === "gemini" ? "gemini" as const : "openai" as const,
                        models: channel.models.map((model) => ({
                            name: model.name,
                            capability: model.capability as "image" | "video" | "text" | "audio",
                            script: model.script,
                        })),
                    }))
                    : current.channels;
                if (!snapshotChannels.length && (desktopConfig.baseUrl || desktopConfig.apiKey || desktopConfig.model)) {
                    const primary = channels[0];
                    if (primary) {
                        const models = desktopConfig.model && !primary.models.some((model) => model.name === desktopConfig.model)
                            ? [...primary.models, { name: desktopConfig.model, capability: guessCapability(desktopConfig.model) }]
                            : primary.models;
                        channels = [{
                            ...primary,
                            baseUrl: desktopConfig.baseUrl || primary.baseUrl,
                            apiKey: desktopConfig.apiKey || primary.apiKey,
                            models,
                        }, ...channels.slice(1)];
                    }
                }
                phase = "hydrate";
                const credentials = await hydrateDesktopCredentials(channels);
                channels = channels.map((channel) => ({ ...channel, apiKey: credentials.channels[channel.id] || channel.apiKey }));
                const models = modelOptionsFromChannels(channels);
                const selectedModels = desktopSnapshot?.selectedModels || {};
                const imageModel = normalizeModelOptionValue(selectedModels.image || current.imageModel, channels);
                const videoModel = normalizeModelOptionValue(selectedModels.video || current.videoModel, channels);
                const textModel = normalizeModelOptionValue(selectedModels.text || current.textModel, channels);
                const audioModel = normalizeModelOptionValue(selectedModels.audio || current.audioModel, channels);
                const defaultModel = [selectedModels.default, imageModel, videoModel, textModel, audioModel, current.model]
                    .map((value) => normalizeModelOptionValue(value, channels))
                    .find(Boolean) || models[0] || "";
                const primary = channels[0];
                const nextConfig = {
                    ...current,
                    baseUrl: primary?.baseUrl || current.baseUrl,
                    apiKey: primary?.apiKey || current.apiKey,
                    apiFormat: primary?.apiFormat || current.apiFormat,
                    channels,
                    models,
                    imageModel,
                    videoModel,
                    textModel,
                    audioModel,
                    model: defaultModel,
                    proxyEnabled: desktopSnapshot?.localProxy?.enabled ?? current.proxyEnabled,
                    proxyUrl: desktopSnapshot?.localProxy?.url || current.proxyUrl,
                };
                let proxyDisabled = false;
                if (nextConfig.proxyEnabled) {
                    try {
                        await testLocalProxy(nextConfig.proxyUrl);
                    } catch {
                        nextConfig.proxyEnabled = false;
                        proxyDisabled = true;
                    }
                }
                useConfigStore.setState({ config: nextConfig });
                if (proxyDisabled) {
                    useConfigStore.getState().updateConfig("proxyEnabled", false);
                    message.warning(t("config.proxy.autoDisabled"));
                }
                if (!desktopSnapshot && !desktopConfig.apiKey && channels[0]?.apiKey) {
                    phase = "save";
                    await saveDesktopApiConfig({ baseUrl: channels[0].baseUrl, apiKey: channels[0].apiKey, model: desktopConfig.model || channels[0].models[0]?.name || "" });
                }
                setDesktopCredentialsReady(true);
            } catch (error) {
                setDesktopCredentialsReady(true);
                message.error(isDesktopBridgeUnavailable(error) ? t("config.desktop.error.disconnected") : t(`config.desktop.error.${phase}`));
            }
        };
        void initialize();
    }, [message, t]);

    useEffect(() => {
        if (!desktopCredentialsReady || !isDesktopApp()) return;
        const primary = config.channels[0];
        const selectedImageModel = decodeChannelModel(config.imageModel);
        const primaryModel = selectedImageModel?.channelId === primary?.id ? selectedImageModel.model : primary?.models[0]?.name || "";
        const snapshot = createDesktopConfigurationSnapshot(config);
        const fingerprint = JSON.stringify({
            credentials: config.channels.map(({ id, apiKey }) => ({ id, apiKey })),
            snapshot,
        });
        if (lastCredentialFingerprint.current === fingerprint) return;
        lastCredentialFingerprint.current = fingerprint;
        const sync = saveDesktopConfiguration(config, primaryModel);
        void sync.catch((error) => message.error(isDesktopBridgeUnavailable(error) ? t("config.desktop.error.disconnected") : t("config.desktop.error.sync")));
    }, [config, desktopCredentialsReady, message, t]);

    useEffect(() => {
        if (handledConfigParams.current) return;
        const searchParams = new URLSearchParams(window.location.search);
        const baseUrl = searchParams.get("baseUrl") || searchParams.get("baseurl");
        const apiKey = searchParams.get("apiKey") || searchParams.get("apikey");
        if (!baseUrl && !apiKey) return;
        handledConfigParams.current = true;
        searchParams.delete("baseUrl");
        searchParams.delete("baseurl");
        searchParams.delete("apiKey");
        searchParams.delete("apikey");
        window.history.replaceState(null, "", `${window.location.pathname}${searchParams.size ? `?${searchParams}` : ""}${window.location.hash}`);
        const result = importChannelCredentials({ baseUrl, apiKey });
        openConfigDialog(false, "channels");
        if (result.status === "created") message.success(t("config.importedChannelCreated", { name: result.channelName }));
        else if (result.status === "updated") message.success(t("config.importedChannelUpdated", { name: result.channelName }));
        else if (result.status === "missing-base-url") message.error(t("config.importedChannelBaseUrlRequired"));
        else message.error(t("config.importedChannelBaseUrlInvalid"));
    }, [importChannelCredentials, message, openConfigDialog, t]);

    return <>{children}</>;
}
