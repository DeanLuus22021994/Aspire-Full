import axios from 'axios';
import { sandboxConfig } from './config';

const client = axios.create({
  baseURL: sandboxConfig.apiBaseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
});

export async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await client.get<T>(path, { signal });
  return response.data;
}

export async function postJson<TRequest, TResponse>(path: string, payload?: TRequest) {
  const response = await client.post<TResponse>(path, payload);
  return response.data;
}

export async function putJson<TRequest, TResponse>(path: string, payload: TRequest) {
  const response = await client.put<TResponse>(path, payload);
  return response.data;
}

export async function deleteResource(path: string) {
  await client.delete(path);
}
