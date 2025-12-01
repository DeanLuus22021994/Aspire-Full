import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ItemsPage from '../pages/ItemsPage'
import { itemsApi, type Item } from '../services/api'

// Mock the API
vi.mock('../services/api', () => ({
  itemsApi: {
    getAll: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    delete: vi.fn(),
  },
}))

// Mock window.confirm
const mockConfirm = vi.fn()
window.confirm = mockConfirm

describe('ItemsPage', () => {
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
    mockConfirm.mockReturnValue(true)
  })

  it('renders page header', async () => {
    ;(itemsApi.getAll as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: [] })

    render(<ItemsPage />)

    expect(screen.getByText('Items')).toBeInTheDocument()
    expect(screen.getByText('Manage your items')).toBeInTheDocument()
  })

  it('displays loading state initially', () => {
    ;(itemsApi.getAll as ReturnType<typeof vi.fn>).mockImplementation(
      () => new Promise(() => {})
    )

    render(<ItemsPage />)

    expect(screen.getByText('Loading items...')).toBeInTheDocument()
  })

  it('displays items in table after loading', async () => {
    ;(itemsApi.getAll as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: mockItems })

    render(<ItemsPage />)

    await waitFor(() => {
      expect(screen.getByText('Test Item 1')).toBeInTheDocument()
      expect(screen.getByText('Test Item 2')).toBeInTheDocument()
    })
  })

  it('displays empty state when no items', async () => {
    ;(itemsApi.getAll as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: [] })

    render(<ItemsPage />)

    await waitFor(() => {
      expect(screen.getByText('No items found. Create one to get started!')).toBeInTheDocument()
    })
  })

  it('displays error message when fetch fails', async () => {
    ;(itemsApi.getAll as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('API Error'))

    render(<ItemsPage />)

    await waitFor(() => {
      expect(screen.getByText('Failed to fetch items. Is the API running?')).toBeInTheDocument()
    })
  })

  it('has Add Item button', async () => {
    ;(itemsApi.getAll as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: [] })

    render(<ItemsPage />)

    expect(screen.getByText('Add Item')).toBeInTheDocument()
  })

  it('opens create modal when Add Item clicked', async () => {
    ;(itemsApi.getAll as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: [] })
    const user = userEvent.setup()

    render(<ItemsPage />)

    await user.click(screen.getByText('Add Item'))

    await waitFor(() => {
      expect(screen.getByText('Create Item')).toBeInTheDocument()
    })
  })

  it('table has correct headers', async () => {
    ;(itemsApi.getAll as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: mockItems })

    render(<ItemsPage />)

    await waitFor(() => {
      expect(screen.getByText('ID')).toBeInTheDocument()
      expect(screen.getByText('Name')).toBeInTheDocument()
      expect(screen.getByText('Description')).toBeInTheDocument()
      expect(screen.getByText('Created')).toBeInTheDocument()
      expect(screen.getByText('Actions')).toBeInTheDocument()
    })
  })

  it('displays item data correctly', async () => {
    ;(itemsApi.getAll as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: mockItems })

    render(<ItemsPage />)

    await waitFor(() => {
      expect(screen.getByText('1')).toBeInTheDocument()
      expect(screen.getByText('Test Item 1')).toBeInTheDocument()
      expect(screen.getByText('Description 1')).toBeInTheDocument()
    })
  })

  it('calls delete API when delete button clicked and confirmed', async () => {
    ;(itemsApi.getAll as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ data: mockItems })
      .mockResolvedValueOnce({ data: [mockItems[1]] })
    ;(itemsApi.delete as ReturnType<typeof vi.fn>).mockResolvedValueOnce({})
    mockConfirm.mockReturnValue(true)

    render(<ItemsPage />)

    await waitFor(() => {
      expect(screen.getByText('Test Item 1')).toBeInTheDocument()
    })

    // Find delete buttons (there should be 2, one for each item)
    const deleteButtons = screen.getAllByRole('button').filter(btn =>
      btn.querySelector('i.trash.icon')
    )

    fireEvent.click(deleteButtons[0])

    await waitFor(() => {
      expect(itemsApi.delete).toHaveBeenCalledWith(1)
    })
  })

  it('does not delete when confirmation cancelled', async () => {
    ;(itemsApi.getAll as ReturnType<typeof vi.fn>).mockResolvedValueOnce({ data: mockItems })
    mockConfirm.mockReturnValue(false)

    render(<ItemsPage />)

    await waitFor(() => {
      expect(screen.getByText('Test Item 1')).toBeInTheDocument()
    })

    const deleteButtons = screen.getAllByRole('button').filter(btn =>
      btn.querySelector('i.trash.icon')
    )

    fireEvent.click(deleteButtons[0])

    expect(itemsApi.delete).not.toHaveBeenCalled()
  })
})
