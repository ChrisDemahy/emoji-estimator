(() => {
    const root = document.querySelector("[data-repository-page]");
    if (!root) {
        return;
    }

    const elements = {
        statusBadge: root.querySelector("[data-role='status-badge']"),
        connectionBadge: root.querySelector("[data-role='connection-badge']"),
        sourceNote: root.querySelector("[data-role='source-note']"),
        stateTitle: root.querySelector("[data-role='state-title']"),
        stateMessage: root.querySelector("[data-role='state-message']"),
        progressSummary: root.querySelector("[data-role='progress-summary']"),
        progressBar: root.querySelector("[data-role='progress-bar']"),
        currentPage: root.querySelector("[data-role='current-page']"),
        pullRequestsRead: root.querySelector("[data-role='pull-requests-read']"),
        updatedAt: root.querySelector("[data-role='updated-at']"),
        completedAt: root.querySelector("[data-role='completed-at']"),
        average: root.querySelector("[data-role='average']"),
        pullRequestCount: root.querySelector("[data-role='pull-request-count']"),
        pullRequestsWithEmoji: root.querySelector("[data-role='pull-requests-with-emoji']"),
        totalEmojiCount: root.querySelector("[data-role='total-emoji-count']"),
        scannedAt: root.querySelector("[data-role='scanned-at']"),
        failurePanel: root.querySelector("[data-role='failure-panel']"),
        failureMessage: root.querySelector("[data-role='failure-message']"),
        initialUpdate: root.querySelector("[data-role='initial-update']")
    };

    const owner = root.getAttribute("data-owner") ?? "";
    const repository = root.getAttribute("data-repository") ?? "";
    const hubUrl = root.getAttribute("data-hub-url") ?? "/hubs/repository-scans";
    let shouldEnsureScan = (root.getAttribute("data-should-ensure-scan") ?? "false") === "true";
    let latestUpdate = parseInitialUpdate(elements.initialUpdate);

    async function connectLiveUpdates() {
        const client = new SignalRHubClient(hubUrl);

        client.on("ScanUpdated", (update) => {
            latestUpdate = update;
            applyUpdate(update);
        });

        client.onClose = () => {
            if (getStatusKey(latestUpdate?.status) === "completed") {
                setConnectionBadge("Latest result ready", "success");
                return;
            }

            setConnectionBadge("Live updates disconnected", "warning");
        };

        try {
            setConnectionBadge("Connecting live updates…", "neutral");
            await client.start();
            await client.invoke("SubscribeAsync", owner, repository);
            setConnectionBadge("Live updates connected", "success");

            if (shouldEnsureScan) {
                const ensuredUpdate = await client.invoke("EnsureScanAsync", owner, repository);
                shouldEnsureScan = false;
                latestUpdate = ensuredUpdate;
                applyUpdate(ensuredUpdate);
            }
        }
        catch (error) {
            setConnectionBadge("Live updates unavailable", "error");

            if (elements.sourceNote) {
                elements.sourceNote.textContent = error instanceof Error && error.message
                    ? error.message
                    : "We could not connect to live updates. Refresh to try again.";
            }
        }
    }

    function applyUpdate(update) {
        const state = buildState(update);

        if (elements.statusBadge) {
            elements.statusBadge.dataset.tone = state.statusTone;
            elements.statusBadge.textContent = state.statusBadgeText;
        }

        if (elements.sourceNote) {
            elements.sourceNote.textContent = state.sourceNote;
        }

        if (elements.stateTitle) {
            elements.stateTitle.textContent = state.stateTitle;
        }

        if (elements.stateMessage) {
            elements.stateMessage.textContent = state.stateMessage;
        }

        if (elements.progressSummary) {
            elements.progressSummary.textContent = state.progressSummary;
        }

        if (elements.progressBar) {
            elements.progressBar.style.width = state.showIndeterminateProgress
                ? "35%"
                : `${state.progressPercent}%`;
            elements.progressBar.classList.toggle("repository-progress-bar-indeterminate", state.showIndeterminateProgress);
        }

        if (elements.currentPage) {
            elements.currentPage.textContent = formatCount(update?.currentPageNumber);
        }

        if (elements.pullRequestsRead) {
            elements.pullRequestsRead.textContent = formatCount(update?.pullRequestsRead);
        }

        if (elements.updatedAt) {
            elements.updatedAt.textContent = formatTimestamp(update?.updatedAtUtc);
        }

        if (elements.completedAt) {
            elements.completedAt.textContent = formatTimestamp(update?.completedAtUtc);
        }

        const result = update?.result;

        if (elements.average) {
            elements.average.textContent = formatAverage(result?.averageEmojisPerPullRequest);
        }

        if (elements.pullRequestCount) {
            elements.pullRequestCount.textContent = formatCount(result?.pullRequestCount);
        }

        if (elements.pullRequestsWithEmoji) {
            elements.pullRequestsWithEmoji.textContent = formatCount(result?.pullRequestsWithEmojiCount);
        }

        if (elements.totalEmojiCount) {
            elements.totalEmojiCount.textContent = formatCount(result?.totalEmojiCount);
        }

        if (elements.scannedAt) {
            elements.scannedAt.textContent = result?.scannedAtUtc
                ? `Scanned ${formatTimestamp(result.scannedAtUtc)}`
                : "Completed scan details appear here once a result is ready.";
        }

        if (elements.failurePanel) {
            elements.failurePanel.classList.toggle("hidden", !state.showFailure);
        }

        if (elements.failureMessage) {
            elements.failureMessage.textContent = state.failureMessage;
        }
    }

    function buildState(update) {
        const statusKey = getStatusKey(update?.status);
        const hasDeterminateProgress = typeof update?.percentComplete === "number" && Number.isFinite(update.percentComplete);
        const progressPercent = statusKey === "completed"
            ? 100
            : hasDeterminateProgress
                ? clamp(update.percentComplete, 0, 100)
                : 0;

        return {
            statusBadgeText: getStatusBadgeText(statusKey),
            statusTone: getStatusTone(statusKey),
            stateTitle: getStateTitle(statusKey),
            stateMessage: getStateMessage(statusKey, update?.message),
            sourceNote: getSourceNote(statusKey),
            progressSummary: getProgressSummary(statusKey, hasDeterminateProgress, progressPercent),
            progressPercent,
            showIndeterminateProgress: !hasDeterminateProgress && (statusKey === "pending" || statusKey === "running"),
            showFailure: statusKey === "failed",
            failureMessage: update?.failureMessage || "The repository scan failed."
        };
    }

    function getStatusKey(status) {
        switch (status) {
            case "Pending":
                return "pending";
            case "Running":
                return "running";
            case "Completed":
                return "completed";
            case "Failed":
                return "failed";
            default:
                return "starting";
        }
    }

    function getStatusBadgeText(statusKey) {
        switch (statusKey) {
            case "pending":
                return "Queued";
            case "running":
                return "Scanning";
            case "completed":
                return "Completed";
            case "failed":
                return "Failed";
            default:
                return "Starting";
        }
    }

    function getStatusTone(statusKey) {
        switch (statusKey) {
            case "running":
                return "progress";
            case "completed":
                return "success";
            case "failed":
                return "error";
            default:
                return "neutral";
        }
    }

    function getStateTitle(statusKey) {
        switch (statusKey) {
            case "pending":
                return "Preparing repository scan";
            case "running":
                return "Scanning pull requests";
            case "completed":
                return "Emoji estimate ready";
            case "failed":
                return "Scan failed";
            default:
                return "Preparing repository scan";
        }
    }

    function getStateMessage(statusKey, message) {
        if (typeof message === "string" && message.trim().length > 0) {
            return message.trim();
        }

        switch (statusKey) {
            case "pending":
                return "Scan queued.";
            case "running":
                return "Scanning pull requests...";
            case "completed":
                return "Scan completed.";
            case "failed":
                return "The repository scan failed.";
            default:
                return "Checking for a cached result and waiting for live updates.";
        }
    }

    function getSourceNote(statusKey) {
        switch (statusKey) {
            case "completed":
                return "Showing the latest completed scan result.";
            case "failed":
                return "The latest scan ended with an error.";
            case "pending":
            case "running":
                return "This page updates live as the scan progresses.";
            default:
                return "We’ll start or resume a scan as soon as live updates connect.";
        }
    }

    function getProgressSummary(statusKey, hasDeterminateProgress, progressPercent) {
        switch (statusKey) {
            case "completed":
                return "100%";
            case "failed":
                return "Stopped";
            default:
                return hasDeterminateProgress ? `${progressPercent}%` : "Live updates";
        }
    }

    function setConnectionBadge(message, tone) {
        if (!elements.connectionBadge) {
            return;
        }

        elements.connectionBadge.dataset.tone = tone;
        elements.connectionBadge.textContent = message;
    }

    function parseInitialUpdate(element) {
        if (!element || !element.textContent) {
            return null;
        }

        try {
            return JSON.parse(element.textContent);
        }
        catch {
            return null;
        }
    }

    function formatCount(value) {
        return typeof value === "number" && Number.isFinite(value)
            ? value.toLocaleString()
            : "—";
    }

    function formatAverage(value) {
        if (typeof value !== "number" || !Number.isFinite(value)) {
            return "—";
        }

        return value.toLocaleString(undefined, {
            minimumFractionDigits: 0,
            maximumFractionDigits: 2
        });
    }

    function formatTimestamp(value) {
        if (!value) {
            return "—";
        }

        const parsedDate = new Date(value);
        if (Number.isNaN(parsedDate.getTime())) {
            return "—";
        }

        return parsedDate.toLocaleString(undefined, {
            dateStyle: "medium",
            timeStyle: "short"
        });
    }

    function clamp(value, minValue, maxValue) {
        return Math.min(Math.max(value, minValue), maxValue);
    }

    function createMessageRecord(payload) {
        return `${JSON.stringify(payload)}\u001e`;
    }

    class SignalRHubClient {
        constructor(rawHubUrl) {
            this.hubUrl = rawHubUrl;
            this.socket = null;
            this.buffer = "";
            this.handlers = new Map();
            this.pendingInvocations = new Map();
            this.nextInvocationId = 0;
            this.handshakeResolver = null;
            this.handshakeRejecter = null;
            this.pingTimerId = 0;
            this.onClose = null;
        }

        on(target, handler) {
            if (!this.handlers.has(target)) {
                this.handlers.set(target, []);
            }

            this.handlers.get(target).push(handler);
        }

        async start() {
            const negotiation = await this.negotiate();
            const connectionToken = negotiation.connectionToken || negotiation.connectionId;

            if (!connectionToken) {
                throw new Error("Missing SignalR connection token.");
            }

            const webSocketUrl = new URL(this.hubUrl, window.location.origin);
            webSocketUrl.protocol = webSocketUrl.protocol === "https:" ? "wss:" : "ws:";
            webSocketUrl.searchParams.set("id", connectionToken);

            await new Promise((resolve, reject) => {
                let settled = false;
                const socket = new WebSocket(webSocketUrl.toString());

                socket.onopen = () => {
                    this.socket = socket;
                    this.handshakeResolver = () => {
                        this.handshakeResolver = null;
                        this.handshakeRejecter = null;

                        if (!settled) {
                            settled = true;
                            resolve();
                        }
                    };
                    this.handshakeRejecter = (error) => {
                        this.handshakeResolver = null;
                        this.handshakeRejecter = null;

                        if (!settled) {
                            settled = true;
                            reject(error);
                        }
                    };

                    socket.send(createMessageRecord({
                        protocol: "json",
                        version: 1
                    }));
                };

                socket.onmessage = (event) => {
                    void this.handleSocketMessage(event.data);
                };

                socket.onerror = () => {
                    if (!settled) {
                        settled = true;
                        reject(new Error("Unable to connect to live updates."));
                    }
                };

                socket.onclose = () => {
                    this.stopPingTimer();
                    this.rejectPendingInvocations(new Error("The live updates connection closed."));

                    if (!settled) {
                        settled = true;
                        reject(new Error("The live updates connection closed."));
                    }

                    if (typeof this.onClose === "function") {
                        this.onClose();
                    }
                };
            });

            this.pingTimerId = window.setInterval(() => {
                if (this.socket?.readyState === WebSocket.OPEN) {
                    this.socket.send(createMessageRecord({ type: 6 }));
                }
            }, 15000);
        }

        async invoke(target, ...args) {
            if (this.socket?.readyState !== WebSocket.OPEN) {
                throw new Error("The live updates connection is not open.");
            }

            const invocationId = `${++this.nextInvocationId}`;
            const completionPromise = new Promise((resolve, reject) => {
                this.pendingInvocations.set(invocationId, { resolve, reject });
            });

            this.socket.send(createMessageRecord({
                type: 1,
                invocationId,
                target,
                arguments: args
            }));

            return completionPromise;
        }

        async negotiate() {
            const negotiationUrl = new URL(`${this.hubUrl.replace(/\/$/, "")}/negotiate`, window.location.origin);
            negotiationUrl.searchParams.set("negotiateVersion", "1");

            const response = await fetch(negotiationUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "text/plain;charset=UTF-8"
                }
            });

            if (!response.ok) {
                throw new Error("Unable to negotiate live updates.");
            }

            const payload = await response.json();
            const transports = Array.isArray(payload.availableTransports) ? payload.availableTransports : [];
            const supportsWebSockets = transports.some((transport) =>
                transport.transport === "WebSockets" &&
                Array.isArray(transport.transferFormats) &&
                transport.transferFormats.includes("Text"));

            if (!supportsWebSockets) {
                throw new Error("WebSocket transport is unavailable.");
            }

            return payload;
        }

        async handleSocketMessage(rawData) {
            const payload = typeof rawData === "string"
                ? rawData
                : await rawData.text();

            this.buffer += payload;

            let separatorIndex = this.buffer.indexOf("\u001e");
            while (separatorIndex >= 0) {
                const messagePayload = this.buffer.slice(0, separatorIndex);
                this.buffer = this.buffer.slice(separatorIndex + 1);

                if (messagePayload.length > 0) {
                    const message = JSON.parse(messagePayload);
                    this.handleProtocolMessage(message);
                }

                separatorIndex = this.buffer.indexOf("\u001e");
            }
        }

        handleProtocolMessage(message) {
            if ((message.type === undefined || message.type === null) && this.handshakeResolver) {
                if (message.error) {
                    this.handshakeRejecter?.(new Error(message.error));
                    return;
                }

                this.handshakeResolver();
                return;
            }

            switch (message.type) {
                case 1:
                    this.dispatchInvocation(message);
                    break;
                case 3:
                    this.completeInvocation(message);
                    break;
                case 6:
                    break;
                case 7:
                    this.rejectPendingInvocations(new Error(message.error || "The live updates connection closed."));
                    break;
                default:
                    break;
            }
        }

        dispatchInvocation(message) {
            const handlers = this.handlers.get(message.target);
            if (!handlers) {
                return;
            }

            for (const handler of handlers) {
                handler(...(message.arguments ?? []));
            }
        }

        completeInvocation(message) {
            const pendingInvocation = this.pendingInvocations.get(message.invocationId);
            if (!pendingInvocation) {
                return;
            }

            this.pendingInvocations.delete(message.invocationId);

            if (message.error) {
                pendingInvocation.reject(new Error(message.error));
                return;
            }

            pendingInvocation.resolve(message.result);
        }

        rejectPendingInvocations(error) {
            for (const pendingInvocation of this.pendingInvocations.values()) {
                pendingInvocation.reject(error);
            }

            this.pendingInvocations.clear();
        }

        stopPingTimer() {
            if (this.pingTimerId !== 0) {
                window.clearInterval(this.pingTimerId);
                this.pingTimerId = 0;
            }
        }
    }

    applyUpdate(latestUpdate);
    void connectLiveUpdates();
})();
