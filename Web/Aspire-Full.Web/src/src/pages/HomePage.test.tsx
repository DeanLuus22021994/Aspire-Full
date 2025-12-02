import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import HomePage from '../pages/HomePage'
import { apiClient } from '../services/api'

// Mock the API client
vi.mock('../services/api', () => ({
  apiClient: {
    get: vi.fn(),
  },
}))

describe('HomePage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders welcome message', async () => {
    ;(apiClient.get as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: { status: 'healthy', uptime: '1d 2h' },
    })

    render(<HomePage />)

    expect(screen.getByText('Welcome to Aspire Full')).toBeInTheDocument()
  })

  it('renders feature cards', async () => {
    ;(apiClient.get as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: { status: 'healthy', uptime: '1d 2h' },
    })

    render(<HomePage />)

    expect(screen.getByText('PostgreSQL')).toBeInTheDocument()
    expect(screen.getByText('Redis')).toBeInTheDocument()
    expect(screen.getByText('Telemetry')).toBeInTheDocument()
  })

  it('displays healthy status when API returns healthy', async () => {
    ;(apiClient.get as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: { status: 'healthy', uptime: '1d 2h' },
    })

    render(<HomePage />)

    await waitFor(() => {
      expect(screen.getByText('healthy')).toBeInTheDocument()
      expect(screen.getByText('1d 2h')).toBeInTheDocument()
    })
  })

  it('displays unhealthy status when API fails', async () => {
    ;(apiClient.get as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('API Error'))

    render(<HomePage />)

    await waitFor(() => {
      expect(screen.getByText('unhealthy')).toBeInTheDocument()
    })
  })

  it('shows loading state initially', () => {
    ;(apiClient.get as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise(() => {}) // Never resolves
    )

    render(<HomePage />)

    expect(screen.getByText('API Health Status')).toBeInTheDocument()
  })

  it('calls health API on mount', async () => {
    ;(apiClient.get as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: { status: 'healthy', uptime: '1d 2h' },
    })

    render(<HomePage />)

    await waitFor(() => {
      expect(apiClient.get).toHaveBeenCalledWith('/api/health')
    })
  })
})
