import type { AiConfig, ModelChannel } from "@/stores/use-config-store";

type DesktopSession = { bridgeUrl: string; token: string };
export type DesktopApiConfig = { baseUrl: string; apiKey: string; model: string };
export type DesktopConfigurationSnapshot = {
    schemaVersion: number;
    channels: DesktopChannelSnapshot[];
    selectedModels: Record<string, string>;
    localProxy?: { enabled: boolean; url: string };
};
export type DesktopChannelSnapshot = {
    id: string;
    name: string;
    baseUrl: string;
    apiFormat: string;
    models: DesktopModelSnapshot[];
};
export type DesktopModelSnapshot = { name: string; capability: string; script?: string };
type DesktopCredentialResponse = { channels: Record<string, string> };

const SESSION_KEY = "infinite-canvas:desktop-session";
const DESKTOP_BRIDGE_UNAVAILABLE_MESSAGE = "桌面管理连接不可用，请从桌面启动器重新打开画布。";

export class DesktopBridgeUnavailableError extends Error {
    constructor() {
        super(DESKTOP_BRIDGE_UNAVAILABLE_MESSAGE);
        this.name = "DesktopBridgeUnavailableError";
    }
}

export function isDesktopBridgeUnavailable(error: unknown): error is DesktopBridgeUnavailableError {
    return error instanceof DesktopBridgeUnavailableError;
}

export function initializeDesktopSession() {
    if (typeof window === "undefined") return null;
    const hash = window.location.hash.startsWith("#") ? window.location.hash.slice(1) : window.location.hash;
    const params = new URLSearchParams(hash);
    const bridgeUrl = params.get("desktopBridge")?.trim() || "";
    const token = params.get("desktopToken")?.trim() || "";
    if (bridgeUrl && token && isLoopbackUrl(bridgeUrl)) {
        const session = { bridgeUrl: bridgeUrl.replace(/\/+$/, ""), token };
        sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
        window.history.replaceState(null, "", `${window.location.pathname}${window.location.search}`);
        return session;
    }
    return readDesktopSession();
}

export function isDesktopApp() {
    if (typeof window === "undefined") return false;
    return Boolean(readDesktopSession() || desktopParamsFromLocation());
}

export async function loadDesktopApiConfig() {
    const value = await desktopRequest<Record<string, unknown>>("/api/config");
    return {
        baseUrl: readString(value, "baseUrl", "BaseUrl"),
        apiKey: readString(value, "apiKey", "ApiKey"),
        model: readString(value, "model", "Model"),
    } satisfies DesktopApiConfig;
}

export async function saveDesktopApiConfig(config: DesktopApiConfig) {
    await desktopRequest("/api/config", { method: "POST", body: JSON.stringify(config) });
}

export async function loadDesktopConfigurationSnapshot() {
    const value = await desktopRequest<unknown>("/api/config/snapshot");
    return normalizeDesktopConfigurationSnapshot(value);
}

export function normalizeDesktopConfigurationSnapshot(value: unknown): DesktopConfigurationSnapshot | null {
    if (!value || typeof value !== "object") return null;
    const root = value as Record<string, unknown>;
    const rawChannels = readValue(root, "channels", "Channels");
    const rawSelectedModels = readValue(root, "selectedModels", "SelectedModels");
    const rawLocalProxy = readValue(root, "localProxy", "LocalProxy");
    const channels = Array.isArray(rawChannels)
        ? rawChannels.filter((item): item is Record<string, unknown> => Boolean(item && typeof item === "object")).map((channel) => {
            const rawModels = readValue(channel, "models", "Models");
            return {
                id: readString(channel, "id", "Id"),
                name: readString(channel, "name", "Name"),
                baseUrl: readString(channel, "baseUrl", "BaseUrl"),
                apiFormat: readString(channel, "apiFormat", "ApiFormat"),
                models: Array.isArray(rawModels)
                    ? rawModels.filter((item): item is Record<string, unknown> => Boolean(item && typeof item === "object")).map((model) => ({
                        name: readString(model, "name", "Name"),
                        capability: readString(model, "capability", "Capability"),
                        script: readOptionalString(model, "script", "Script"),
                    }))
                    : [],
            };
        })
        : [];
    const selectedModels: Record<string, string> = {};
    if (rawSelectedModels && typeof rawSelectedModels === "object") {
        const selected = rawSelectedModels as Record<string, unknown>;
        for (const key of ["default", "image", "video", "text", "audio"]) {
            const valueForKey = readOptionalString(selected, key, key[0].toUpperCase() + key.slice(1));
            if (valueForKey) selectedModels[key] = valueForKey;
        }
    }
    const localProxy = rawLocalProxy && typeof rawLocalProxy === "object"
        ? { enabled: Boolean(readValue(rawLocalProxy as Record<string, unknown>, "enabled", "Enabled")), url: readString(rawLocalProxy as Record<string, unknown>, "url", "Url") }
        : undefined;
    return {
        schemaVersion: Number(readValue(root, "schemaVersion", "SchemaVersion") || 1),
        channels,
        selectedModels,
        ...(localProxy ? { localProxy } : {}),
    };
}

export async function saveDesktopConfigurationSnapshot(snapshot: DesktopConfigurationSnapshot) {
    await desktopRequest("/api/config/snapshot", { method: "POST", body: JSON.stringify(snapshot) });
}

export function createDesktopConfigurationSnapshot(config: AiConfig): DesktopConfigurationSnapshot {
    return {
        schemaVersion: 1,
        channels: config.channels.map(({ id, name, baseUrl, apiFormat, models }) => ({
            id,
            name,
            baseUrl,
            apiFormat,
            models: models.map(({ name: modelName, capability, script }) => ({ name: modelName, capability, script: script || undefined })),
        })),
        selectedModels: {
            default: config.model,
            image: config.imageModel,
            video: config.videoModel,
            text: config.textModel,
            audio: config.audioModel,
        },
        localProxy: { enabled: config.proxyEnabled, url: config.proxyUrl },
    };
}

export async function hydrateDesktopCredentials(channels: ModelChannel[]) {
    const value = await desktopRequest<Record<string, unknown>>("/api/credentials/hydrate", {
        method: "POST",
        body: JSON.stringify({ channels: channelCredentials(channels) }),
    });
    const rawChannels = readValue(value, "channels", "Channels");
    return {
        channels: rawChannels && typeof rawChannels === "object" ? rawChannels as Record<string, string> : {},
    } satisfies DesktopCredentialResponse;
}

export async function syncDesktopCredentials(config: AiConfig) {
    await desktopRequest("/api/credentials/sync", {
        method: "POST",
        body: JSON.stringify({ channels: channelCredentials(config.channels) }),
    });
}

/** Persist the complete canvas configuration explicitly before the user leaves the configuration UI. */
export async function saveDesktopConfiguration(config: AiConfig, primaryModel: string) {
    const primary = config.channels[0];
    const requests = [syncDesktopCredentials(config)];
    if (primary) requests.push(saveDesktopApiConfig({ baseUrl: primary.baseUrl, apiKey: primary.apiKey, model: primaryModel }));
    requests.push(saveDesktopConfigurationSnapshot(createDesktopConfigurationSnapshot(config)));
    await Promise.all(requests);
}

export type DesktopStatus = { running: boolean; vitePort?: number; bridgePort?: number };

export async function getDesktopStatus() {
    return desktopRequest<DesktopStatus>("/api/status");
}

export function sanitizeDesktopPersistedConfig(value: string) {
    if (!isDesktopApp()) return value;
    try {
        const persisted = JSON.parse(value) as { state?: { config?: AiConfig } };
        const config = persisted.state?.config;
        if (!config) return value;
        config.apiKey = "";
        config.channels = config.channels.map((channel) => ({ ...channel, apiKey: "" }));
        return JSON.stringify(persisted);
    } catch {
        return value;
    }
}

export function sanitizeDesktopPersistedStorage(name: string) {
    if (typeof window === "undefined") return null;
    const value = window.localStorage.getItem(name);
    if (!value) return value;
    const sanitized = sanitizeDesktopPersistedConfig(value);
    if (sanitized !== value) window.localStorage.setItem(name, sanitized);
    return sanitized;
}

function channelCredentials(channels: ModelChannel[]) {
    return channels.map(({ id, apiKey }) => ({ id, apiKey }));
}

async function desktopRequest<T = void>(path: string, init?: RequestInit) {
    const session = readDesktopSession() || initializeDesktopSession();
    if (!session) throw new DesktopBridgeUnavailableError();
    let response: Response;
    try {
        response = await fetch(`${session.bridgeUrl}${path}`, {
            ...init,
            headers: { Authorization: `Bearer ${session.token}`, "Content-Type": "application/json", ...init?.headers },
        });
    } catch {
        throw new DesktopBridgeUnavailableError();
    }
    if (response.status === 401) throw new DesktopBridgeUnavailableError();
    if (!response.ok) throw new Error(`桌面管理请求失败（HTTP ${response.status}）`);
    if (response.status === 204) return undefined as T;
    return response.json() as Promise<T>;
}

function readDesktopSession(): DesktopSession | null {
    try {
        const value = sessionStorage.getItem(SESSION_KEY);
        if (!value) return null;
        const session = JSON.parse(value) as DesktopSession;
        return session.bridgeUrl && session.token && isLoopbackUrl(session.bridgeUrl) ? session : null;
    } catch {
        return null;
    }
}

function desktopParamsFromLocation() {
    const hash = window.location.hash.startsWith("#") ? window.location.hash.slice(1) : window.location.hash;
    const params = new URLSearchParams(hash);
    return params.has("desktopBridge") && params.has("desktopToken");
}

function readValue(value: Record<string, unknown>, ...keys: string[]) {
    for (const key of keys) {
        if (key in value) return value[key];
    }
    return undefined;
}

function readString(value: Record<string, unknown>, ...keys: string[]) {
    const result = readValue(value, ...keys);
    return typeof result === "string" ? result : "";
}

function readOptionalString(value: Record<string, unknown>, ...keys: string[]) {
    const result = readValue(value, ...keys);
    return typeof result === "string" && result ? result : undefined;
}

function isLoopbackUrl(value: string) {
    try {
        const url = new URL(value);
        return url.protocol === "http:" && (url.hostname === "127.0.0.1" || url.hostname === "localhost");
    } catch {
        return false;
    }
}
