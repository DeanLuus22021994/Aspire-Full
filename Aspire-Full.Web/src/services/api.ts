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

// API types
export interface Item {
  id: number
  name: string
  description: string
  createdAt: string
  updatedAt: string
}

export interface CreateItemDto {
  name: string
  description: string
}

export interface UpdateItemDto {
  name?: string
  description?: string
}

// API methods
export const itemsApi = {
  getAll: () => apiClient.get<Item[]>('/api/items'),
  getById: (id: number) => apiClient.get<Item>(`/api/items/${id}`),
  create: (data: CreateItemDto) => apiClient.post<Item>('/api/items', data),
  update: (id: number, data: UpdateItemDto) => apiClient.put<Item>(`/api/items/${id}`, data),
  delete: (id: number) => apiClient.delete(`/api/items/${id}`),
}
