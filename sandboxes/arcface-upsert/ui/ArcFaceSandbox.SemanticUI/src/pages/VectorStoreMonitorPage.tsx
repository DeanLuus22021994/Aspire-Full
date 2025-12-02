import type { ChangeEvent } from 'react';
import { useMemo, useState } from 'react';
import {
    Button,
    Feed,
    Form,
    Grid,
    Header,
    Icon,
    Label,
    Message,
    Segment,
    Statistic,
    Table,
    TextArea,
} from 'semantic-ui-react';
import ErrorState from '../components/ErrorState';
import LastUpdatedTag from '../components/LastUpdatedTag';
import LoadingState from '../components/LoadingState';
import PageHeader from '../components/PageHeader';
import { usePollingData } from '../hooks/usePollingData';
import { formatUtc } from '../lib/dates';
import { getJson, postJson } from '../lib/http';
import type { VectorDocumentStatus, VectorStoreStatusResponse } from '../types/diagnostics';

interface VectorSearchResult {
  id: string;
  score: number;
  userEmail?: string;
}

interface VectorSearchResponse {
  results?: VectorSearchResult[];
}

interface VectorSearchRequest {
  embedding: number[];
  topK: number;
}

const parseVectorInput = (input: string): number[] =>
  input
    .split(/[\s,]+/)
    .map((value) => Number(value.trim()))
    .filter((value) => Number.isFinite(value));

const VectorStoreMonitorPage = () => {
  const { data, isLoading, error, lastUpdated, refresh } = usePollingData<VectorStoreStatusResponse>(
    (signal) => getJson('/api/diagnostics/vector-store', signal),
    { intervalMs: 20000 },
  );

  const [searchInput, setSearchInput] = useState('');
  const [searchResults, setSearchResults] = useState<VectorSearchResult[]>([]);
  const [searchSource, setSearchSource] = useState<'api' | 'placeholder' | null>(null);
  const [isSearching, setIsSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const documents: VectorDocumentStatus[] = data?.documents ?? [];
  const issues: VectorStoreStatusResponse['issues'] = data?.issues ?? [];

  const feedItems = useMemo(
    () =>
      documents.map((doc) => ({
        key: doc.vectorDocumentId,
        date: formatUtc(doc.vectorUpdatedAt ?? doc.vectorDeletedAt),
        summary: doc.isDeleted ? 'Downserted vector' : doc.vectorExists ? 'Upserted vector' : 'Missing vector',
        extraText: doc.userEmail,
        icon: doc.isDeleted ? 'trash' : doc.vectorExists ? 'upload' : 'warning sign',
      })),
    [documents],
  );

  const collectionTags = (
    <div className="status-strip">
      <Label content={`Endpoint: ${data?.endpoint ?? 'unknown'}`} />
      <Label content={`Collection: ${data?.collectionName ?? 'n/a'}`} />
      <Label content={`Vector size: ${data?.vectorSize ?? 512}`} />
      <Label color={data?.autoCreateCollection ? 'green' : 'grey'} content={data?.autoCreateCollection ? 'Auto-create enabled' : 'Manual provision'} />
      <Label color={data?.isReachable ? 'green' : 'red'} content={data?.isReachable ? 'Reachable' : 'Offline'} />
    </div>
  );

  const handleSearch = async () => {
    if (!searchInput.trim()) {
      setSearchError('Provide an embedding vector before searching.');
      return;
    }

    const vector = parseVectorInput(searchInput);
    if (vector.length === 0) {
      setSearchError('Provide at least one numeric value.');
      return;
    }

    setIsSearching(true);
    setSearchError(null);
    try {
      const response = await postJson<VectorSearchRequest, VectorSearchResponse>('/api/vector-store/search', {
        embedding: vector,
        topK: 5,
      });
      setSearchResults(response.results ?? []);
      setSearchSource('api');
    } catch {
      setSearchSource('placeholder');
      setSearchError('Search endpoint is not available yet; showing the latest documents instead.');
      setSearchResults(
        documents.slice(0, 5).map((doc, index) => ({
          id: doc.vectorDocumentId,
          userEmail: doc.userEmail,
          score: Number((0.5 - index * 0.05).toFixed(3)),
        })),
      );
    } finally {
      setIsSearching(false);
    }
  };

  if (isLoading && !data) {
    return <LoadingState message="Loading vector store status..." />;
  }

  if (error) {
    return <ErrorState error={error} onRetry={refresh} />;
  }

  return (
    <div>
      <PageHeader
        icon="database"
        title="Vector Store Monitor"
        subtitle="Track collection health, recent document activity, and run ad-hoc similarity checks."
        lastUpdated={lastUpdated}
      />

      {collectionTags}

      {data && !data.isReachable && (
        <Message negative icon="plug" header="Vector store unreachable" content="Confirm that Qdrant is running and the ArcFace sandbox can reach it." />
      )}

      {issues.length ? (
        <Message warning icon="warning circle" header="Diagnostics issues" list={issues.map((issue) => issue.message)} />
      ) : null}

      <Segment inverted>
        <Grid stackable columns={3} divided>
          <Grid.Column>
            <Statistic label="Documents probed" value={documents.length} />
          </Grid.Column>
          <Grid.Column>
            <Statistic label="Auto-create" value={data?.autoCreateCollection ? 'Enabled' : 'Disabled'} />
          </Grid.Column>
          <Grid.Column>
            <Statistic label="Vector size" value={data?.vectorSize ?? 512} />
          </Grid.Column>
        </Grid>
        <LastUpdatedTag timestamp={lastUpdated} />
      </Segment>

      <Segment inverted>
        <Header as="h3">
          <Icon name="list" /> Recent operations
        </Header>
        {feedItems.length > 0 ? (
          <Feed>
            {feedItems.map((item) => (
              <Feed.Event key={item.key}>
                <Feed.Label icon={item.icon as never} />
                <Feed.Content>
                  <Feed.Summary>
                    {item.summary}
                    <Feed.Date>{item.date}</Feed.Date>
                  </Feed.Summary>
                  {item.extraText ? <Feed.Extra text>{item.extraText}</Feed.Extra> : null}
                </Feed.Content>
              </Feed.Event>
            ))}
          </Feed>
        ) : (
          <Message info content="No document probes were returned." />
        )}
      </Segment>

      <Segment inverted>
        <Header as="h3">
          <Icon name="table" /> Document probes
        </Header>
        <Table celled inverted compact>
          <Table.Header>
            <Table.Row>
              <Table.HeaderCell>User</Table.HeaderCell>
              <Table.HeaderCell>Document Id</Table.HeaderCell>
              <Table.HeaderCell>Status</Table.HeaderCell>
              <Table.HeaderCell>Updated</Table.HeaderCell>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {documents.map((doc) => (
              <Table.Row key={doc.vectorDocumentId} warning={!doc.vectorExists} negative={doc.isDeleted}>
                <Table.Cell>
                  <strong>{doc.displayName}</strong>
                  <div className="meta-text">{doc.userEmail}</div>
                </Table.Cell>
                <Table.Cell>{doc.vectorDocumentId}</Table.Cell>
                <Table.Cell>
                  {doc.isDeleted ? <Label color="red" content="Deleted" /> : null}
                  {doc.vectorExists && !doc.isDeleted ? <Label color="green" content="Active" /> : null}
                  {!doc.vectorExists && !doc.isDeleted ? <Label color="yellow" content="Missing" /> : null}
                </Table.Cell>
                <Table.Cell>{formatUtc(doc.vectorUpdatedAt ?? doc.vectorDeletedAt)}</Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      </Segment>

      <Segment inverted>
        <Header as="h3">
          <Icon name="search" /> Similarity search
        </Header>
        <Form>
          <TextArea
            rows={4}
            placeholder="Paste a comma or space separated embedding"
            value={searchInput}
            onChange={(event: ChangeEvent<HTMLTextAreaElement>) => setSearchInput(event.currentTarget.value)}
          />
          <Button
            type="button"
            primary
            icon="search"
            content="Run search"
            loading={isSearching}
            disabled={isSearching || !searchInput.trim()}
            onClick={handleSearch}
          />
        </Form>
        {searchError && <Message warning icon="info circle" content={searchError} />}
        {searchResults.length > 0 && (
          <Table basic="very" inverted compact>
            <Table.Header>
              <Table.Row>
                <Table.HeaderCell>Document</Table.HeaderCell>
                <Table.HeaderCell>Score</Table.HeaderCell>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {searchResults.map((result: VectorSearchResult) => (
                <Table.Row key={result.id}>
                  <Table.Cell>
                    {result.userEmail ?? 'Unknown user'}
                    <div className="meta-text">{result.id}</div>
                  </Table.Cell>
                  <Table.Cell>{result.score.toFixed(3)}</Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        )}
        {searchSource && <Label content={`Results source: ${searchSource}`} />}
      </Segment>
    </div>
  );
};

export default VectorStoreMonitorPage;
