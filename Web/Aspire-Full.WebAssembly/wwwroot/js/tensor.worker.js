/* eslint-disable no-undef */
const sessionCache = new Map();
let ortReady = false;

const providerMap = new Map([
    ["webgpu", "webgpu"],
    ["webgl", "webgl"],
    ["webgl2", "webgl"],
    ["wasm", "wasm"],
    ["wasm-simd", "wasm"],
    ["wasm-cpu", "wasm"],
    ["cpu", "wasm"]
]);

const ensureOnnxRuntime = async () => {
    if (ortReady) {
        return;
    }

    try {
        importScripts("https://cdn.jsdelivr.net/npm/onnxruntime-web/dist/ort.min.js");
        if (typeof ort !== "undefined") {
            ortReady = true;
        }
    } catch (error) {
        console.warn("Unable to load onnxruntime-web", error);
    }
};

const mapProvider = (provider) => providerMap.get((provider ?? "").toLowerCase()) ?? "wasm";

const loadSession = async (modelUri, provider) => {
    if (!modelUri) {
        return null;
    }

    const cacheKey = `${provider}:${modelUri}`;
    if (sessionCache.has(cacheKey)) {
        return sessionCache.get(cacheKey);
    }

    if (typeof ort === "undefined") {
        await ensureOnnxRuntime();
    }

    if (typeof ort === "undefined") {
        return null;
    }

    const executionProvider = mapProvider(provider);
    const session = await ort.InferenceSession.create(modelUri, {
        executionProviders: [executionProvider],
        graphOptimizationLevel: "all",
        enableMemPattern: true,
        executionMode: "parallel"
    });

    const sessionInfo = { session, provider: executionProvider };
    sessionCache.set(cacheKey, sessionInfo);
    return sessionInfo;
};

const encodePrompt = (prompt) => {
    const normalized = (prompt ?? "").trim();
    const encoder = new TextEncoder();
    const bytes = encoder.encode(normalized);
    const length = 512;
    const data = new Float32Array(length);
    for (let i = 0; i < length; i += 1) {
        const source = bytes[i % bytes.length] ?? 0;
        data[i] = source / 255;
    }

    return {
        tensor: data,
        dims: [1, length]
    };
};

const summarizeVector = (vector) => {
    if (!vector || vector.length === 0) {
        return "No vector output produced.";
    }

    const preview = vector.slice(0, Math.min(16, vector.length)).map((value) => Number(value.toFixed(4)));
    return `Vector preview (${preview.length}/${vector.length} dims): ${preview.join(", ")}`;
};

const runOnnxInference = async (payload, encoded) => {
    try {
        const sessionInfo = await loadSession(payload.ModelUri, payload.ExecutionProvider);
        if (!sessionInfo) {
            return null;
        }

        const { session, provider } = sessionInfo;
        const inputName = payload.Metadata?.InputName ?? (session.inputNames?.[0] ?? "input");
        const feeds = {};
        feeds[inputName] = new ort.Tensor("float32", encoded.tensor, encoded.dims);
        const results = await session.run(feeds);
        const outputName = payload.Metadata?.OutputName ?? (session.outputNames?.[0] ?? Object.keys(results)[0]);
        const tensor = results[outputName];
        return {
            vector: Array.from(tensor.data ?? []),
            provider
        };
    } catch (error) {
        console.warn("ONNX execution failed", error);
        return null;
    }
};

self.onmessage = async (event) => {
    const payload = event.data;
    const start = performance.now();
    try {
        const encoded = encodePrompt(payload.Prompt);
        const onnxResult = await runOnnxInference(payload, encoded);
        const vector = onnxResult?.vector ?? Array.from(encoded.tensor);
        const providerUsed = onnxResult?.provider ?? mapProvider(payload.ExecutionProvider);
        const chunks = [
            {
                Type: "vector",
                Content: JSON.stringify(vector.slice(0, Math.min(vector.length, 64))),
                Sequence: 0,
                Confidence: 0.99
            },
            {
                Type: "text",
                Content: summarizeVector(vector),
                Sequence: 1,
                Confidence: 0.75
            }
        ];

        self.postMessage({
            RequestId: payload.RequestId,
            ModelId: payload.ModelId,
            Status: "Completed",
            ExecutionProviderUsed: providerUsed,
            DurationMs: performance.now() - start,
            Chunks: chunks
        });
    } catch (error) {
        self.postMessage({
            RequestId: payload.RequestId,
            ModelId: payload.ModelId,
            Status: "Failed",
            ExecutionProviderUsed: mapProvider(payload.ExecutionProvider),
            DurationMs: performance.now() - start,
            Error: error?.message ?? String(error),
            Chunks: []
        });
    }
};
