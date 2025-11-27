import type { SandboxUserRole } from './usersKernel.ts';

export interface SandboxUserResponse {
  id: string;
  email: string;
  displayName: string;
  role: SandboxUserRole;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  lastLoginAt?: string | null;
}

export interface UpsertUserPayload {
  email: string;
  displayName: string;
  role: SandboxUserRole;
  faceImageBase64: string;
}

export interface UpdateUserPayload {
  displayName?: string;
  role?: SandboxUserRole;
  isActive?: boolean;
  faceImageBase64?: string | null;
}
