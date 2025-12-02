import type { SandboxUserResponse, UpdateUserPayload, UpsertUserPayload } from '../types/users';
import { deleteResource, getJson, postJson, putJson } from './http';

export const fetchUsers = (signal?: AbortSignal) => getJson<SandboxUserResponse[]>('/api/users', signal);

export const upsertUser = (payload: UpsertUserPayload) => postJson<UpsertUserPayload, SandboxUserResponse>('/api/users', payload);

export const updateUser = (userId: string, payload: UpdateUserPayload) =>
  putJson<UpdateUserPayload, SandboxUserResponse>(`/api/users/${userId}`, payload);

export const downsertUser = (userId: string) => deleteResource(`/api/users/${userId}`);

export const recordLogin = (userId: string) => postJson<undefined, SandboxUserResponse>(`/api/users/${userId}/login`);
