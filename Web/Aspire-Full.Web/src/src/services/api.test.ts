import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest'
import axios from 'axios'
import { itemsApi, type Item, type CreateItemDto, type UpdateItemDto } from '../services/api'

// Mock axios
vi.mock('axios', () => {
  const mockAxios = {
    create: vi.fn(() => mockAxios),
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    interceptors: {
      request: { use: vi.fn() },
      response: { use: vi.fn() },
    },
  }
  return { default: mockAxios }
})

describe('itemsApi', () => {
  const mockItems: Item[] = [
    {
      id: 1,
      name: 'Test Item 1',
      description: 'Description 1',
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-01T00:00:00Z',
    },
    {
      id: 2,
      name: 'Test Item 2',
      description: 'Description 2',
      createdAt: '2024-01-02T00:00:00Z',
      updatedAt: '2024-01-02T00:00:00Z',
    },
  ]

  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('getAll', () => {
    it('should fetch all items', async () => {
      const mockResponse = { data: mockItems }
      ;(axios.get as Mock).mockResolvedValueOnce(mockResponse)

      const result = await itemsApi.getAll()

      expect(axios.get).toHaveBeenCalledWith('/api/items')
      expect(result).toEqual(mockResponse)
    })

    it('should handle empty response', async () => {
      const mockResponse = { data: [] }
      ;(axios.get as Mock).mockResolvedValueOnce(mockResponse)

      const result = await itemsApi.getAll()

      expect(result.data).toEqual([])
    })
  })

  describe('getById', () => {
    it('should fetch item by id', async () => {
      const mockResponse = { data: mockItems[0] }
      ;(axios.get as Mock).mockResolvedValueOnce(mockResponse)

      const result = await itemsApi.getById(1)

      expect(axios.get).toHaveBeenCalledWith('/api/items/1')
      expect(result).toEqual(mockResponse)
    })

    it('should handle non-existent item', async () => {
      const error = new Error('Not found')
      ;(axios.get as Mock).mockRejectedValueOnce(error)

      await expect(itemsApi.getById(999)).rejects.toThrow('Not found')
    })
  })

  describe('create', () => {
    it('should create a new item', async () => {
      const newItem: CreateItemDto = { name: 'New Item', description: 'New Description' }
      const mockResponse = {
        data: { ...newItem, id: 3, createdAt: '2024-01-03T00:00:00Z', updatedAt: '2024-01-03T00:00:00Z' },
      }
      ;(axios.post as Mock).mockResolvedValueOnce(mockResponse)

      const result = await itemsApi.create(newItem)

      expect(axios.post).toHaveBeenCalledWith('/api/items', newItem)
      expect(result.data.name).toBe('New Item')
    })
  })

  describe('update', () => {
    it('should update an existing item', async () => {
      const updateData: UpdateItemDto = { name: 'Updated Name' }
      const mockResponse = {
        data: { ...mockItems[0], name: 'Updated Name', updatedAt: '2024-01-04T00:00:00Z' },
      }
      ;(axios.put as Mock).mockResolvedValueOnce(mockResponse)

      const result = await itemsApi.update(1, updateData)

      expect(axios.put).toHaveBeenCalledWith('/api/items/1', updateData)
      expect(result.data.name).toBe('Updated Name')
    })

    it('should update only description', async () => {
      const updateData: UpdateItemDto = { description: 'Updated Description' }
      const mockResponse = {
        data: { ...mockItems[0], description: 'Updated Description' },
      }
      ;(axios.put as Mock).mockResolvedValueOnce(mockResponse)

      const result = await itemsApi.update(1, updateData)

      expect(result.data.description).toBe('Updated Description')
    })
  })

  describe('delete', () => {
    it('should delete an item', async () => {
      const mockResponse = { data: null, status: 204 }
      ;(axios.delete as Mock).mockResolvedValueOnce(mockResponse)

      const result = await itemsApi.delete(1)

      expect(axios.delete).toHaveBeenCalledWith('/api/items/1')
      expect(result.status).toBe(204)
    })
  })
})
