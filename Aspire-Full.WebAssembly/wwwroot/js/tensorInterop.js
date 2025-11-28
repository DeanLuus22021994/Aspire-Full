const workerPool = [];
const pendingRequests = new Map();

const createRequestId = () => {
    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
        return crypto.randomUUID();
    }

    return `tensor-${Date.now()}-${Math.random().toString(16).slice(2)}`;
};

const acquireWorker = () => {
    const idleEntry = workerPool.find((entry) => entry.busy === false);
    if (idleEntry) {
        idleEntry.busy = true;
        return idleEntry;
    }

    const worker = new Worker("./js/tensor.worker.js", {
        name: `AspireTensorWorker-${workerPool.length + 1}`
    });

    const entry = { worker, busy: true };
    worker.addEventListener("message", (event) => handleWorkerMessage(event, entry));
    worker.addEventListener("error", (event) => handleWorkerError(event, entry));
    workerPool.push(entry);
    return entry;
};

const releaseWorker = (entry) => {
    if (!entry) {
        return;
    }

    entry.busy = false;
};

const handleWorkerMessage = (event, entry) => {
    const payload = event.data;
    const requestId = payload?.RequestId;
    const pending = pendingRequests.get(requestId);
    releaseWorker(entry);

    if (!pending) {
        return;
    }

    clearTimeout(pending.timeoutHandle);
    pendingRequests.delete(requestId);

    if (payload?.Status === "Failed") {
        pending.reject(new Error(payload?.Error ?? "Tensor execution failed."));
        return;
    }

    pending.resolve({
        ModelId: payload?.ModelId ?? pending.request.ModelId,
        ExecutionProvider: payload?.ExecutionProviderUsed ?? pending.request.ExecutionProvider,
        Status: payload?.Status ?? "Completed",
        DurationMs: payload?.DurationMs ?? 0,
        Chunks: payload?.Chunks ?? [],
        Error: payload?.Error
    });
};

const handleWorkerError = (event, entry) => {
    releaseWorker(entry);
    const requestIds = Array.from(pendingRequests.keys());
    requestIds.forEach((requestId) => {
        const pending = pendingRequests.get(requestId);
        if (pending?.workerEntry === entry) {
            clearTimeout(pending.timeoutHandle);
            pendingRequests.delete(requestId);
            pending.reject(new Error(event?.message ?? "Tensor worker error."));
        }
    });
};

export function runTensorExecution(request) {
    const workerEntry = acquireWorker();
    const requestId = createRequestId();
    const timeoutMs = Number(request.Metadata?.TimeoutMs ?? 120_000);

    return new Promise((resolve, reject) => {
        const timeoutHandle = setTimeout(() => {
            pendingRequests.delete(requestId);
            releaseWorker(workerEntry);
            reject(new Error("Tensor execution timed out."));
        }, timeoutMs);

        pendingRequests.set(requestId, {
            resolve,
            reject,
            timeoutHandle,
            request,
            workerEntry
        });

        workerEntry.worker.postMessage({
            ...request,
            RequestId: requestId
        });
    });
}
