const defaultApiBase = window.location.origin;

export const sandboxConfig = {
  apiBaseUrl: (import.meta.env.VITE_ARCFACE_SANDBOX_API as string | undefined)?.trim() || defaultApiBase,
};
