import { Tag } from "antd";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { getDesktopStatus, isDesktopApp, type DesktopStatus } from "@/services/desktop";

export function DesktopStatusBadge() {
    const { t } = useTranslation();
    const [status, setStatus] = useState<DesktopStatus | null>(null);
    const [failed, setFailed] = useState(false);
    const desktopApp = isDesktopApp();

    useEffect(() => {
        if (!desktopApp) return;
        getDesktopStatus()
            .then(setStatus)
            .catch(() => setFailed(true));
    }, [desktopApp]);

    if (!desktopApp) return null;

    return (
        <div className="flex flex-wrap items-center gap-2 text-xs text-stone-500">
            <span className="font-medium text-stone-700 dark:text-stone-300">{t("config.desktop.status.title")}</span>
            {failed ? (
                <Tag color="error">{t("config.desktop.status.loadFailed")}</Tag>
            ) : status ? (
                <>
                    <Tag color={status.running ? "success" : "default"}>
                        {status.running ? t("config.desktop.status.running") : t("config.desktop.status.stopped")}
                    </Tag>
                    {status.vitePort != null && <span>{t("config.desktop.status.vitePort", { port: status.vitePort })}</span>}
                    {status.bridgePort != null && <span>{t("config.desktop.status.bridgePort", { port: status.bridgePort })}</span>}
                </>
            ) : null}
        </div>
    );
}
