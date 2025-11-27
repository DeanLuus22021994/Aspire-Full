import axios from 'axios';
import { FormEvent, useMemo, useState } from 'react';
import {
    Button,
    Checkbox,
    Form,
    Grid,
    Header,
    Icon,
    Input,
    Label,
    Message,
    Modal,
    Segment,
    Table,
} from 'semantic-ui-react';
import ErrorState from '../components/ErrorState';
import LastUpdatedTag from '../components/LastUpdatedTag';
import LoadingState from '../components/LoadingState';
import PageHeader from '../components/PageHeader';
import { usePollingData } from '../hooks/usePollingData';
import { formatUtc } from '../lib/dates';
import { getJson } from '../lib/http';
import { downsertUser, fetchUsers, recordLogin, updateUser, upsertUser } from '../lib/usersApi';
import type { VectorStoreStatusResponse } from '../types/diagnostics';
import type { SandboxUserResponse } from '../types/users';
import type { SandboxUserRole } from '../types/usersKernel';

const roleOptions = [
  { key: 'user', value: 'User', text: 'User' },
  { key: 'admin', value: 'Admin', text: 'Admin' },
];

const defaultFaceSample = 'data:image/png;base64,';

const UsersSandboxPage = () => {
  const usersQuery = usePollingData<SandboxUserResponse[]>(fetchUsers, { intervalMs: 12000 });
  const vectorQuery = usePollingData<VectorStoreStatusResponse>(
    (signal) => getJson('/api/diagnostics/vector-store', signal),
    { intervalMs: 20000 },
  );

  const [searchTerm, setSearchTerm] = useState('');
  const [showInactive, setShowInactive] = useState(true);
  const [upsertEmail, setUpsertEmail] = useState('');
  const [upsertDisplayName, setUpsertDisplayName] = useState('');
  const [upsertRole, setUpsertRole] = useState<SandboxUserRole>('User');
  const [upsertFace, setUpsertFace] = useState(defaultFaceSample);
  const [editUser, setEditUser] = useState<SandboxUserResponse | null>(null);
  const [editDisplayName, setEditDisplayName] = useState('');
  const [editRole, setEditRole] = useState<SandboxUserRole>('User');
  const [editIsActive, setEditIsActive] = useState(true);
  const [editFace, setEditFace] = useState<string>('');
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const vectorMap = useMemo(() => {
    const map = new Map<string, VectorStoreStatusResponse['documents'][number]>();
    vectorQuery.data?.documents.forEach((doc) => {
      map.set(doc.userId.toLowerCase(), doc);
    });
    return map;
  }, [vectorQuery.data]);

  const filteredUsers = useMemo(() => {
    if (!usersQuery.data) {
      return [] as SandboxUserResponse[];
    }

    return usersQuery.data.filter((user) => {
      const matchesQuery = [user.email, user.displayName]
        .join(' ')
        .toLowerCase()
        .includes(searchTerm.toLowerCase());
      const matchesActive = showInactive || user.isActive;
      return matchesQuery && matchesActive;
    });
  }, [usersQuery.data, searchTerm, showInactive]);

  const isUpsertValid = upsertEmail.trim().length > 0 && upsertDisplayName.trim().length > 0 && upsertFace.trim().length > 0;

  const resetEditState = () => {
    setEditUser(null);
    setEditDisplayName('');
    setEditRole('User');
    setEditIsActive(true);
    setEditFace('');
  };

  const openEditModal = (user: SandboxUserResponse) => {
    setEditUser(user);
    setEditDisplayName(user.displayName);
    setEditRole(user.role);
    setEditIsActive(user.isActive);
    setEditFace('');
  };

  const handleActionError = (err: unknown) => {
    if (axios.isAxiosError(err)) {
      const detail = err.response?.data?.detail ?? err.message;
      setActionError(detail);
    } else if (err instanceof Error) {
      setActionError(err.message);
    } else {
      setActionError('Unexpected error while calling the sandbox API.');
    }
    setActionMessage(null);
  };

  const handleUpsert = async (event?: FormEvent) => {
    event?.preventDefault();
    if (!isUpsertValid) {
      return;
    }

    setActionError(null);
    setActionMessage(null);
    setIsSubmitting(true);
    try {
      await upsertUser({
        email: upsertEmail.trim(),
        displayName: upsertDisplayName.trim(),
        role: upsertRole,
        faceImageBase64: upsertFace.trim(),
      });
      setActionMessage('User upsert completed.');
      setUpsertDisplayName('');
      setUpsertEmail('');
      setUpsertFace(defaultFaceSample);
      usersQuery.refresh();
      vectorQuery.refresh();
    } catch (err) {
      handleActionError(err);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleUpdate = async () => {
    if (!editUser) {
      return;
    }

    setIsSubmitting(true);
    setActionError(null);
    setActionMessage(null);
    try {
      await updateUser(editUser.id, {
        displayName: editDisplayName.trim(),
        role: editRole,
        isActive: editIsActive,
        faceImageBase64: editFace.trim() || undefined,
      });
      setActionMessage('User updated successfully.');
      resetEditState();
      usersQuery.refresh();
      vectorQuery.refresh();
    } catch (err) {
      handleActionError(err);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDownsert = async (user: SandboxUserResponse) => {
    const confirmed = window.confirm(`Downsert (soft delete) ${user.displayName}?`);
    if (!confirmed) {
      return;
    }

    setActionError(null);
    setActionMessage(null);
    setIsSubmitting(true);
    try {
      await downsertUser(user.id);
      setActionMessage('User downserted.');
      usersQuery.refresh();
      vectorQuery.refresh();
    } catch (err) {
      handleActionError(err);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleLogin = async (user: SandboxUserResponse) => {
    setActionError(null);
    setActionMessage(null);
    setIsSubmitting(true);
    try {
      await recordLogin(user.id);
      setActionMessage('Login recorded.');
      usersQuery.refresh();
    } catch (err) {
      handleActionError(err);
    } finally {
      setIsSubmitting(false);
    }
  };

  const renderSyncBadge = (user: SandboxUserResponse) => {
    const entry = vectorMap.get(user.id.toLowerCase());
    if (!entry) {
      return <Label color="grey" content="Not probed" icon="question circle" />;
    }

    const inSync = (user.isActive && entry.vectorExists && !entry.isDeleted) || (!user.isActive && entry.isDeleted);
    const color = inSync ? 'green' : 'red';
    const text = inSync ? 'In sync' : 'Mismatch';
    const icon = inSync ? 'check' : 'warning sign';

    return <Label color={color} content={text} icon={icon} />;
  };

  if (usersQuery.isLoading && !usersQuery.data) {
    return <LoadingState message="Loading sandbox users…" />;
  }

  if (usersQuery.error) {
    return <ErrorState error={usersQuery.error} onRetry={usersQuery.refresh} />;
  }

  return (
    <div>
      <PageHeader
        icon="users"
        title="Users Sandbox"
        subtitle="Seed, update, and validate sandbox users before crossing into Aspire."
        lastUpdated={usersQuery.lastUpdated}
      />

      {actionMessage && <Message positive content={actionMessage} />}
      {actionError && <Message negative content={actionError} />}

      <Segment inverted>
        <Header as="h3" dividing>
          <Icon name="add user" /> Upsert Sandbox User
        </Header>
        <Form inverted onSubmit={handleUpsert}>
          <Form.Group widths="equal">
            <Form.Input
              label="Email"
              required
              value={upsertEmail}
              onChange={(_, data) => setUpsertEmail(String(data.value))}
            />
            <Form.Input
              label="Display Name"
              required
              value={upsertDisplayName}
              onChange={(_, data) => setUpsertDisplayName(String(data.value))}
            />
            <Form.Select
              label="Role"
              options={roleOptions}
              value={upsertRole}
              onChange={(_, data) => setUpsertRole(data.value as SandboxUserRole)}
            />
          </Form.Group>
          <Form.TextArea
            label="Aligned Face Image (Base64)"
            value={upsertFace}
            onChange={(_, data) => setUpsertFace(String(data.value))}
            placeholder="Paste the aligned face payload"
            rows={3}
            required
          />
          <Button
            type="submit"
            primary
            icon="upload"
            content="Upsert user"
            disabled={!isUpsertValid || isSubmitting}
            loading={isSubmitting}
          />
        </Form>
      </Segment>

      <Segment inverted>
        <Grid stackable>
          <Grid.Row columns={2} stretched>
            <Grid.Column width={10}>
              <Input
                fluid
                icon="search"
                placeholder="Search name or email"
                value={searchTerm}
                onChange={(_, data) => setSearchTerm(String(data.value))}
              />
            </Grid.Column>
            <Grid.Column width={6} textAlign="right">
              <Checkbox
                toggle
                label="Show inactive"
                checked={showInactive}
                onChange={(_, data) => setShowInactive(Boolean(data.checked))}
              />
              <Button icon="refresh" content="Refresh" onClick={() => {
                usersQuery.refresh();
                vectorQuery.refresh();
              }} />
            </Grid.Column>
          </Grid.Row>
        </Grid>
        <div className="status-strip">
          <LastUpdatedTag timestamp={usersQuery.lastUpdated} />
          <LastUpdatedTag timestamp={vectorQuery.lastUpdated} />
        </div>
        {vectorQuery.error && (
          <Message
            negative
            icon="plug"
            header="Vector diagnostics unavailable"
            content={vectorQuery.error.message}
          />
        )}
        {vectorQuery.data?.issues?.length ? (
          <Message warning icon="warning circle" header="Vector store issues" list={vectorQuery.data.issues.map((issue) => issue.message)} />
        ) : null}

        <Table celled inverted selectable compact>
          <Table.Header>
            <Table.Row>
              <Table.HeaderCell>User</Table.HeaderCell>
              <Table.HeaderCell>Role</Table.HeaderCell>
              <Table.HeaderCell>Status</Table.HeaderCell>
              <Table.HeaderCell>Vector Sync</Table.HeaderCell>
              <Table.HeaderCell>Updated</Table.HeaderCell>
              <Table.HeaderCell collapsing>Actions</Table.HeaderCell>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {filteredUsers.map((user) => (
              <Table.Row key={user.id} disabled={!user.isActive}>
                <Table.Cell>
                  <Header as="h4" inverted>
                    {user.displayName}
                    <Header.Subheader>{user.email}</Header.Subheader>
                  </Header>
                </Table.Cell>
                <Table.Cell>{user.role}</Table.Cell>
                <Table.Cell>
                  {user.isActive ? <Label color="green" content="Active" /> : <Label color="grey" content="Inactive" />}
                </Table.Cell>
                <Table.Cell>
                  {renderSyncBadge(user)}
                  <div className="meta-text">
                    {formatUtc(vectorMap.get(user.id.toLowerCase())?.vectorUpdatedAt)}
                  </div>
                </Table.Cell>
                <Table.Cell>
                  <div>Created {formatUtc(user.createdAt)}</div>
                  <div>Updated {formatUtc(user.updatedAt)}</div>
                </Table.Cell>
                <Table.Cell>
                  <Button.Group size="tiny">
                    <Button icon="edit" onClick={() => openEditModal(user)} disabled={isSubmitting} />
                    <Button icon="sign in" onClick={() => handleLogin(user)} disabled={isSubmitting} />
                    <Button icon="trash" color="red" onClick={() => handleDownsert(user)} disabled={isSubmitting} />
                  </Button.Group>
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
        {filteredUsers.length === 0 && <Message info content="No users match the current filters." />}
      </Segment>

      <Modal open={Boolean(editUser)} onClose={resetEditState} size="small" closeIcon>
        <Modal.Header>Edit sandbox user</Modal.Header>
        <Modal.Content>
          <Form inverted>
            <Form.Input label="Display Name" value={editDisplayName} onChange={(_, data) => setEditDisplayName(String(data.value))} />
            <Form.Select
              label="Role"
              options={roleOptions}
              value={editRole}
              onChange={(_, data) => setEditRole(data.value as SandboxUserRole)}
            />
            <Form.Field>
              <label>Active state</label>
              <Checkbox toggle checked={editIsActive} onChange={(_, data) => setEditIsActive(Boolean(data.checked))} />
            </Form.Field>
            <Form.TextArea
              label="New Face Image (optional)"
              value={editFace}
              onChange={(_, data) => setEditFace(String(data.value))}
              rows={3}
            />
          </Form>
        </Modal.Content>
        <Modal.Actions>
          <Button onClick={resetEditState}>Cancel</Button>
          <Button primary onClick={handleUpdate} loading={isSubmitting} disabled={isSubmitting} icon="save" content="Save changes" />
        </Modal.Actions>
      </Modal>
    </div>
  );
};

export default UsersSandboxPage;
