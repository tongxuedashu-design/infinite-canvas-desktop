import type { AiConfig, ModelChannel } from "@/stores/use-config-store";

type DesktopSession = { bridgeUrl: string; token: string };
type DesktopApiConfig = { baseUrl: string; apiKey: string; model: string };
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
    return desktopRequest<DesktopApiConfig>("/api/config");
}

export async function saveDesktopApiConfig(config: DesktopApiConfig) {
    await desktopRequest("/api/config", { method: "POST", body: JSON.stringify(config) });
}

export async function hydrateDesktopCredentials(channels: ModelChannel[]) {
    return desktopRequest<DesktopCredentialResponse>("/api/credentials/hydrate", {
        method: "POST",
        body: JSON.stringify({ channels: channelCredentials(channels) }),
    });
}

export async function syncDesktopCredentials(config: AiConfig) {
    await desktopRequest("/api/credentials/sync", {
        method: "POST",
        body: JSON.stringify({ channels: channelCredentials(config.channels) }),
    });
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

function isLoopbackUrl(value: string) {
    try {
        const url = new URL(value);
        return url.protocol === "http:" && (url.hostname === "127.0.0.1" || url.hostname === "localhost");
    } catch {
        return false;
    }
}
