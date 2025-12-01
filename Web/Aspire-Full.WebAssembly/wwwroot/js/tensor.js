(function (global) {
    const metadataKeysToCapture = [
        'hardwareConcurrency',
        'deviceMemory',
        'platform'
    ];

    const detectWebGl2 = () => {
        try {
            const canvas = document.createElement('canvas');
            return !!canvas.getContext('webgl2');
        } catch (err) {
            console.warn('Unable to probe WebGL2', err);
            return false;
        }
    };

    const detectSimd = () => {
        try {
            if (typeof WebAssembly === 'undefined' || typeof WebAssembly.validate !== 'function') {
                return false;
            }

            const bytes = new Uint8Array([
                0, 97, 115, 109, 1, 0, 0, 0,
                1, 7, 1, 96, 0, 0,
                3, 2, 1, 0,
                10, 11, 1, 9, 0,
                65, 0, 253, 15,
                65, 0, 253, 15,
                11
            ]);

            return WebAssembly.validate(bytes);
        } catch (err) {
            console.warn('SIMD detection failed', err);
            return false;
        }
    };

    const captureMetadata = (supportsWebGpu, supportsWebGl2, supportsSimd) => {
        const metadata = Object.create(null);
        metadata['ua'] = navigator.userAgent;
        metadata['supports.webgpu'] = String(supportsWebGpu);
        metadata['supports.webgl2'] = String(supportsWebGl2);
        metadata['supports.simd'] = String(supportsSimd);

        metadataKeysToCapture.forEach((key) => {
            if (key in navigator) {
                metadata[`navigator.${key}`] = String(navigator[key]);
            }
        });

        if (navigator?.gpu && typeof navigator.gpu.requestAdapter === 'function') {
            metadata['gpu.entry'] = 'navigator.gpu';
        }

        return metadata;
    };

    const resolveRecommendation = (supportsWebGpu, supportsWebGl2, supportsSimd) => {
        if (supportsWebGpu) {
            return 'webgpu';
        }

        if (supportsWebGl2 && supportsSimd) {
            return 'webgl2-simd';
        }

        if (supportsWebGl2) {
            return 'webgl2';
        }

        return 'wasm-cpu';
    };

    const enrichGpuMetadata = async (metadata) => {
        if (!navigator?.gpu || typeof navigator.gpu.requestAdapter !== 'function') {
            return metadata;
        }

        try {
            const adapter = await navigator.gpu.requestAdapter();
            if (adapter) {
                metadata['gpu.name'] = adapter.name;
                metadata['gpu.features'] = Array.from(adapter.features.values()).join(', ');
                const limitEntries = Object.entries(adapter.limits ?? {});
                limitEntries.forEach(([key, value]) => {
                    metadata[`gpu.limits.${key}`] = String(value);
                });
            }
        } catch (err) {
            metadata['gpu.error'] = String(err);
        }

        return metadata;
    };

    global.AspireTensor = {
        determineRuntime: async () => {
            const supportsWebGpu = !!navigator?.gpu;
            const supportsWebGl2 = detectWebGl2();
            const supportsSimd = detectSimd();
            const metadata = await enrichGpuMetadata(
                captureMetadata(supportsWebGpu, supportsWebGl2, supportsSimd)
            );

            return {
                supportsWebGpu,
                supportsWebGl2,
                supportsSimd,
                recommendedExecutionProvider: resolveRecommendation(supportsWebGpu, supportsWebGl2, supportsSimd),
                metadata
            };
        }
    };
})(globalThis);
