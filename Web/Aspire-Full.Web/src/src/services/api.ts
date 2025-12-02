import axios from 'axios'

export const apiClient = axios.create({
  baseURL: '',
  headers: {
    'Content-Type': 'application/json',
  },
})

// Request interceptor for logging
apiClient.interceptors.request.use(
  (config) => {
    console.log(`[API] ${config.method?.toUpperCase()} ${config.url}`)
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Response interceptor for error handling
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('[API Error]', error.response?.data || error.message)
    return Promise.reject(error)
  }
)

// ===== Item Types =====
export interface Item {
  id: number
  name: string
  description: string
  createdAt: string
  updatedAt: string
  createdByUserId?: number
}

export interface CreateItemDto {
  name: string
  description: string
}

export interface UpdateItemDto {
  name?: string
  description?: string
}

// ===== User Types =====
export type UserRole = 'User' | 'Admin'

export interface User {
  id: number
  email: string
  displayName: string
  role: UserRole
  isActive: boolean
  createdAt: string
  updatedAt: string
  lastLoginAt?: string
}

export interface AdminUser extends User {
  deletedAt?: string
  itemCount: number
}

export interface CreateUserDto {
  email: string
  displayName: string
  role?: UserRole
}

export interface UpdateUserDto {
  displayName?: string
  isActive?: boolean
}

export interface AdminStats {
  users: {
    totalUsers: number
    activeUsers: number
    inactiveUsers: number
    admins: number
    regularUsers: number
  }
  items: {
    totalItems: number
    itemsWithOwner: number
    orphanedItems: number
  }
}

// ===== Item API =====
export const itemsApi = {
  getAll: () => apiClient.get<Item[]>('/api/items'),
  getById: (id: number) => apiClient.get<Item>(`/api/items/${id}`),
  create: (data: CreateItemDto) => apiClient.post<Item>('/api/items', data),
  update: (id: number, data: UpdateItemDto) => apiClient.put<Item>(`/api/items/${id}`, data),
  delete: (id: number) => apiClient.delete(`/api/items/${id}`),
}

// ===== User API (Standard User Flow) =====
export const usersApi = {
  /** Get all active users */
  getAll: () => apiClient.get<User[]>('/api/users'),

  /** Get user by ID */
  getById: (id: number) => apiClient.get<User>(`/api/users/${id}`),

  /** Get user by email */
  getByEmail: (email: string) => apiClient.get<User>(`/api/users/by-email/${email}`),

  /** Create or update user (upsert) - reactivates soft-deleted users */
  upsert: (data: CreateUserDto) => apiClient.post<User>('/api/users', data),

  /** Update user details */
  update: (id: number, data: UpdateUserDto) => apiClient.put<User>(`/api/users/${id}`, data),

  /** Soft delete user (downsert) - deactivates but retains data */
  downsert: (id: number) => apiClient.delete(`/api/users/${id}`),

  /** Record user login */
  recordLogin: (id: number) => apiClient.post<User>(`/api/users/${id}/login`),
}

// ===== Admin API (Admin Flow) =====
export const adminApi = {
  /** Get all users including soft-deleted */
  getAllUsers: (params?: { includeDeleted?: boolean; role?: UserRole }) =>
    apiClient.get<AdminUser[]>('/api/admin/users', { params }),

  /** Get user by ID (includes soft-deleted) */
  getUser: (id: number) => apiClient.get<AdminUser>(`/api/admin/users/${id}`),

  /** Promote user to admin */
  promoteToAdmin: (id: number) => apiClient.post<AdminUser>(`/api/admin/users/${id}/promote`),

  /** Demote admin to regular user */
  demoteToUser: (id: number) => apiClient.post<AdminUser>(`/api/admin/users/${id}/demote`),

  /** Reactivate a soft-deleted user */
  reactivateUser: (id: number) => apiClient.post<AdminUser>(`/api/admin/users/${id}/reactivate`),

  /** Hard delete user permanently (irreversible) */
  hardDeleteUser: (id: number) => apiClient.delete(`/api/admin/users/${id}/permanent`),

  /** Bulk deactivate users */
  bulkDeactivate: (userIds: number[]) =>
    apiClient.post<{ deactivatedCount: number }>('/api/admin/users/bulk-deactivate', userIds),

  /** Get admin statistics */
  getStats: () => apiClient.get<AdminStats>('/api/admin/stats'),
}
