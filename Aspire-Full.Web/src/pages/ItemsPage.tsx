import { useState, useEffect, useCallback } from 'react'
import {
  Header,
  Table,
  Button,
  Icon,
  Segment,
  Modal,
  Form,
  Message,
  Loader,
  Dimmer,
} from 'semantic-ui-react'
import { itemsApi, Item, CreateItemDto } from '../services/api'

export default function ItemsPage() {
  const [items, setItems] = useState<Item[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [modalOpen, setModalOpen] = useState(false)
  const [editingItem, setEditingItem] = useState<Item | null>(null)
  const [formData, setFormData] = useState<CreateItemDto>({ name: '', description: '' })

  const fetchItems = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const response = await itemsApi.getAll()
      setItems(response.data)
    } catch {
      setError('Failed to fetch items. Is the API running?')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchItems()
  }, [fetchItems])

  const handleSubmit = async () => {
    try {
      if (editingItem) {
        await itemsApi.update(editingItem.id, formData)
      } else {
        await itemsApi.create(formData)
      }
      setModalOpen(false)
      setFormData({ name: '', description: '' })
      setEditingItem(null)
      fetchItems()
    } catch {
      setError('Failed to save item')
    }
  }

  const handleDelete = async (id: number) => {
    if (window.confirm('Are you sure you want to delete this item?')) {
      try {
        await itemsApi.delete(id)
        fetchItems()
      } catch {
        setError('Failed to delete item')
      }
    }
  }

  const openEditModal = (item: Item) => {
    setEditingItem(item)
    setFormData({ name: item.name, description: item.description })
    setModalOpen(true)
  }

  const openCreateModal = () => {
    setEditingItem(null)
    setFormData({ name: '', description: '' })
    setModalOpen(true)
  }

  return (
    <>
      <Header as="h1">
        <Icon name="list" />
        <Header.Content>
          Items
          <Header.Subheader>Manage your items</Header.Subheader>
        </Header.Content>
      </Header>

      {error && (
        <Message negative>
          <Message.Header>Error</Message.Header>
          <p>{error}</p>
        </Message>
      )}

      <Button primary onClick={openCreateModal}>
        <Icon name="plus" /> Add Item
      </Button>

      <Segment>
        {loading ? (
          <Dimmer active inverted>
            <Loader>Loading items...</Loader>
          </Dimmer>
        ) : (
          <Table celled striped>
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>ID</Table.HeaderCell>
                <Table.HeaderCell>Name</Table.HeaderCell>
                <Table.HeaderCell>Description</Table.HeaderCell>
                <Table.HeaderCell>Created</Table.HeaderCell>
                <Table.HeaderCell>Actions</Table.HeaderCell>
              </Table.Row>
            </Table.Header>

            <Table.Body>
              {items.length === 0 ? (
                <Table.Row>
                  <Table.Cell colSpan={5} textAlign="center">
                    No items found. Create one to get started!
                  </Table.Cell>
                </Table.Row>
              ) : (
                items.map((item) => (
                  <Table.Row key={item.id}>
                    <Table.Cell>{item.id}</Table.Cell>
                    <Table.Cell>{item.name}</Table.Cell>
                    <Table.Cell>{item.description}</Table.Cell>
                    <Table.Cell>{new Date(item.createdAt).toLocaleDateString()}</Table.Cell>
                    <Table.Cell>
                      <Button icon size="small" onClick={() => openEditModal(item)}>
                        <Icon name="edit" />
                      </Button>
                      <Button icon size="small" color="red" onClick={() => handleDelete(item.id)}>
                        <Icon name="trash" />
                      </Button>
                    </Table.Cell>
                  </Table.Row>
                ))
              )}
            </Table.Body>
          </Table>
        )}
      </Segment>

      <Modal open={modalOpen} onClose={() => setModalOpen(false)} size="small">
        <Modal.Header>{editingItem ? 'Edit Item' : 'Create Item'}</Modal.Header>
        <Modal.Content>
          <Form>
            <Form.Input
              label="Name"
              placeholder="Item name"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            />
            <Form.TextArea
              label="Description"
              placeholder="Item description"
              value={formData.description}
              onChange={(e) => setFormData({ ...formData, description: e.target.value })}
            />
          </Form>
        </Modal.Content>
        <Modal.Actions>
          <Button onClick={() => setModalOpen(false)}>Cancel</Button>
          <Button primary onClick={handleSubmit}>
            {editingItem ? 'Update' : 'Create'}
          </Button>
        </Modal.Actions>
      </Modal>
    </>
  )
}
