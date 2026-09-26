import type { ReactNode } from "react";
import { useEffect, useRef, useState } from "react";
import { App } from "antd";
import { useTranslation } from "react-i18next";

import { decodeChannelModel, encodeChannelModel, guessCapability, modelOptionsFromChannels, useConfigStore } from "@/stores/use-config-store";
import { usePromptSourceScheduler } from "@/hooks/use-prompt-source-scheduler";
import { hydrateDesktopCredentials, initializeDesktopSession, isDesktopApp, loadDesktopApiConfig, saveDesktopApiConfig, syncDesktopCredentials } from "@/services/desktop";

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
            try {
                const current = useConfigStore.getState().config;
                const desktopConfig = await loadDesktopApiConfig();
                let channels = current.channels;
                if (desktopConfig.baseUrl || desktopConfig.apiKey || desktopConfig.model) {
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
                const credentials = await hydrateDesktopCredentials(channels);
                channels = channels.map((channel) => ({ ...channel, apiKey: credentials.channels[channel.id] || channel.apiKey }));
                const models = modelOptionsFromChannels(channels);
                const imageModel = desktopConfig.model && channels[0]?.models.some((model) => model.name === desktopConfig.model)
                    ? encodeChannelModel(channels[0].id, desktopConfig.model)
                    : current.imageModel;
                useConfigStore.setState({ config: { ...current, channels, models, imageModel, model: imageModel || current.model } });
                if (!desktopConfig.apiKey && channels[0]?.apiKey) {
                    await saveDesktopApiConfig({ baseUrl: channels[0].baseUrl, apiKey: channels[0].apiKey, model: desktopConfig.model || channels[0].models[0]?.name || "" });
                }
                setDesktopCredentialsReady(true);
            } catch (error) {
                setDesktopCredentialsReady(true);
                message.error(error instanceof Error ? error.message : String(error));
            }
        };
        void initialize();
    }, [message]);

    useEffect(() => {
        if (!desktopCredentialsReady || !isDesktopApp()) return;
        const primary = config.channels[0];
        const selectedImageModel = decodeChannelModel(config.imageModel);
        const primaryModel = selectedImageModel?.channelId === primary?.id ? selectedImageModel.model : primary?.models[0]?.name || "";
        const fingerprint = JSON.stringify({
            channels: config.channels.map(({ id, apiKey }) => ({ id, apiKey })),
            primaryBaseUrl: primary?.baseUrl || "",
            primaryModel,
        });
        if (lastCredentialFingerprint.current === fingerprint) return;
        lastCredentialFingerprint.current = fingerprint;
        const sync = primary
            ? Promise.all([
                syncDesktopCredentials(config),
                saveDesktopApiConfig({ baseUrl: primary.baseUrl, apiKey: primary.apiKey, model: primaryModel }),
            ])
            : syncDesktopCredentials(config);
        void sync.catch((error) => message.error(error instanceof Error ? error.message : String(error)));
    }, [config, desktopCredentialsReady, message]);

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
