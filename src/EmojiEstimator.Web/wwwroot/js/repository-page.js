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
        issuesRead: root.querySelector("[data-role='issues-read']"),
        totalItemsRead: root.querySelector("[data-role='total-items-read']"),
        updatedAt: root.querySelector("[data-role='updated-at']"),
        completedAt: root.querySelector("[data-role='completed-at']"),
        scannedAt: root.querySelector("[data-role='scanned-at']"),
        repositoryItemCount: root.querySelector("[data-role='repository-item-count']"),
        repositoryItemsWithEmoji: root.querySelector("[data-role='repository-items-with-emoji']"),
        repositoryTotalEmojiCount: root.querySelector("[data-role='repository-total-emoji-count']"),
        repositoryAverageEmojis: root.querySelector("[data-role='repository-average-emojis']"),
        repositoryItemsWithEmDash: root.querySelector("[data-role='repository-items-with-em-dash']"),
        repositoryTotalEmDashCount: root.querySelector("[data-role='repository-total-em-dash-count']"),
        repositoryAverageEmDashes: root.querySelector("[data-role='repository-average-em-dashes']"),
        pullRequestItemCount: root.querySelector("[data-role='pull-request-item-count']"),
        pullRequestItemsWithEmoji: root.querySelector("[data-role='pull-request-items-with-emoji']"),
        pullRequestTotalEmojiCount: root.querySelector("[data-role='pull-request-total-emoji-count']"),
        pullRequestAverageEmojis: root.querySelector("[data-role='pull-request-average-emojis']"),
        pullRequestItemsWithEmDash: root.querySelector("[data-role='pull-request-items-with-em-dash']"),
        pullRequestTotalEmDashCount: root.querySelector("[data-role='pull-request-total-em-dash-count']"),
        pullRequestAverageEmDashes: root.querySelector("[data-role='pull-request-average-em-dashes']"),
        issueItemCount: root.querySelector("[data-role='issue-item-count']"),
        issueItemsWithEmoji: root.querySelector("[data-role='issue-items-with-emoji']"),
        issueTotalEmojiCount: root.querySelector("[data-role='issue-total-emoji-count']"),
        issueAverageEmojis: root.querySelector("[data-role='issue-average-emojis']"),
        issueItemsWithEmDash: root.querySelector("[data-role='issue-items-with-em-dash']"),
        issueTotalEmDashCount: root.querySelector("[data-role='issue-total-em-dash-count']"),
        issueAverageEmDashes: root.querySelector("[data-role='issue-average-em-dashes']"),
        failurePanel: root.querySelector("[data-role='failure-panel']"),
        failureMessage: root.querySelector("[data-role='failure-message']"),
        initialUpdate: root.querySelector("[data-role='initial-update']")
    };

    const owner = root.getAttribute("data-owner") ?? "";
    const repository = root.getAttribute("data-repository") ?? "";
    const liveUpdatesUrl = root.getAttribute("data-live-updates-url") ?? `${window.location.pathname.replace(/\/$/, "")}/live-updates`;
    const ensureScanUrl = root.getAttribute("data-ensure-scan-url") ?? `${window.location.pathname.replace(/\/$/, "")}/ensure-scan`;
    let shouldEnsureScan = (root.getAttribute("data-should-ensure-scan") ?? "false") === "true";
    let latestUpdate = parseInitialUpdate(elements.initialUpdate);
    let isEnsuringScan = false;

    async function connectLiveUpdates() {
        if (typeof window.EventSource !== "function") {
            setConnectionBadge("Live updates unavailable", "error");
            if (elements.sourceNote) {
                elements.sourceNote.textContent = "This browser does not support live repository updates.";
            }

            return;
        }

        setConnectionBadge("Connecting live updates…", "neutral");

        const eventSource = new window.EventSource(liveUpdatesUrl);

        eventSource.addEventListener("open", () => {
            setConnectionBadge("Live updates connected", "success");
            void ensureScan();
        });

        eventSource.addEventListener("scan-update", (event) => {
            try {
                const update = JSON.parse(event.data);
                latestUpdate = update;
                applyUpdate(update);
            }
            catch {
            }
        });

        eventSource.onerror = () => {
            if (eventSource.readyState === window.EventSource.CLOSED) {
                setConnectionBadge("Live updates unavailable", "error");
                return;
            }

            if (getStatusKey(latestUpdate?.status) === "completed") {
                setConnectionBadge("Latest result ready", "success");
                return;
            }

            setConnectionBadge("Reconnecting live updates…", "warning");
        };
    }

    async function ensureScan() {
        if (!shouldEnsureScan || isEnsuringScan) {
            return;
        }

        isEnsuringScan = true;

        try {
            const response = await fetch(ensureScanUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json"
                }
            });

            if (!response.ok) {
                throw new Error("We could not start a repository scan.");
            }

            const ensuredUpdate = await response.json();
            shouldEnsureScan = false;
            latestUpdate = ensuredUpdate;
            applyUpdate(ensuredUpdate);
        }
        catch (error) {
            isEnsuringScan = false;

            if (elements.sourceNote) {
                elements.sourceNote.textContent = error instanceof Error && error.message
                    ? error.message
                    : "We could not start a repository scan. Refresh to try again.";
            }

            return;
        }

        isEnsuringScan = false;
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

        if (elements.issuesRead) {
            elements.issuesRead.textContent = formatCount(update?.issuesRead);
        }

        if (elements.totalItemsRead) {
            elements.totalItemsRead.textContent = formatCount(update?.totalItemsRead);
        }

        if (elements.updatedAt) {
            elements.updatedAt.textContent = formatTimestamp(update?.updatedAtUtc);
        }

        if (elements.completedAt) {
            elements.completedAt.textContent = formatTimestamp(update?.completedAtUtc);
        }

        const result = update?.result;
        const repositorySummary = result?.repositorySummary;
        const pullRequestSummary = result?.pullRequestSummary;
        const issueSummary = result?.issueSummary;

        if (elements.repositoryItemCount) {
            elements.repositoryItemCount.textContent = formatCount(repositorySummary?.itemCount);
        }

        if (elements.repositoryItemsWithEmoji) {
            elements.repositoryItemsWithEmoji.textContent = formatCount(repositorySummary?.itemsWithEmojiCount);
        }

        if (elements.repositoryTotalEmojiCount) {
            elements.repositoryTotalEmojiCount.textContent = formatCount(repositorySummary?.totalEmojiCount);
        }

        if (elements.repositoryAverageEmojis) {
            elements.repositoryAverageEmojis.textContent = formatAverage(repositorySummary?.averageEmojisPerItem);
        }

        if (elements.repositoryItemsWithEmDash) {
            elements.repositoryItemsWithEmDash.textContent = formatCount(repositorySummary?.itemsWithEmDashCount);
        }

        if (elements.repositoryTotalEmDashCount) {
            elements.repositoryTotalEmDashCount.textContent = formatCount(repositorySummary?.totalEmDashCount);
        }

        if (elements.repositoryAverageEmDashes) {
            elements.repositoryAverageEmDashes.textContent = formatAverage(repositorySummary?.averageEmDashesPerItem);
        }

        if (elements.pullRequestItemCount) {
            elements.pullRequestItemCount.textContent = formatCount(pullRequestSummary?.itemCount);
        }

        if (elements.pullRequestItemsWithEmoji) {
            elements.pullRequestItemsWithEmoji.textContent = formatCount(pullRequestSummary?.itemsWithEmojiCount);
        }

        if (elements.pullRequestTotalEmojiCount) {
            elements.pullRequestTotalEmojiCount.textContent = formatCount(pullRequestSummary?.totalEmojiCount);
        }

        if (elements.pullRequestAverageEmojis) {
            elements.pullRequestAverageEmojis.textContent = formatAverage(pullRequestSummary?.averageEmojisPerItem);
        }

        if (elements.pullRequestItemsWithEmDash) {
            elements.pullRequestItemsWithEmDash.textContent = formatCount(pullRequestSummary?.itemsWithEmDashCount);
        }

        if (elements.pullRequestTotalEmDashCount) {
            elements.pullRequestTotalEmDashCount.textContent = formatCount(pullRequestSummary?.totalEmDashCount);
        }

        if (elements.pullRequestAverageEmDashes) {
            elements.pullRequestAverageEmDashes.textContent = formatAverage(pullRequestSummary?.averageEmDashesPerItem);
        }

        if (elements.issueItemCount) {
            elements.issueItemCount.textContent = formatCount(issueSummary?.itemCount);
        }

        if (elements.issueItemsWithEmoji) {
            elements.issueItemsWithEmoji.textContent = formatCount(issueSummary?.itemsWithEmojiCount);
        }

        if (elements.issueTotalEmojiCount) {
            elements.issueTotalEmojiCount.textContent = formatCount(issueSummary?.totalEmojiCount);
        }

        if (elements.issueAverageEmojis) {
            elements.issueAverageEmojis.textContent = formatAverage(issueSummary?.averageEmojisPerItem);
        }

        if (elements.issueItemsWithEmDash) {
            elements.issueItemsWithEmDash.textContent = formatCount(issueSummary?.itemsWithEmDashCount);
        }

        if (elements.issueTotalEmDashCount) {
            elements.issueTotalEmDashCount.textContent = formatCount(issueSummary?.totalEmDashCount);
        }

        if (elements.issueAverageEmDashes) {
            elements.issueAverageEmDashes.textContent = formatAverage(issueSummary?.averageEmDashesPerItem);
        }

        if (elements.scannedAt) {
            elements.scannedAt.textContent = result?.scannedAtUtc
                ? `Scanned ${formatTimestamp(result.scannedAtUtc)}`
                : "Completed scan details appear here once a repository summary is ready.";
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
                return "Scanning repository content";
            case "completed":
                return "Repository summary ready";
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
                return "Scanning pull requests and issues...";
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

    applyUpdate(latestUpdate);
    void connectLiveUpdates();
})();
