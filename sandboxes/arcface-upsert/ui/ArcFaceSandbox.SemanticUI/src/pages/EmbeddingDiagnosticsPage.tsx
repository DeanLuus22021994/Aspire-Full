import { useCallback, useEffect, useMemo, useState } from 'react';
import {
    Button,
    Grid,
    Header,
    Icon,
    Label,
    List,
    Message,
    Segment,
    Statistic,
} from 'semantic-ui-react';
import EmbeddingSparkline from '../components/EmbeddingSparkline';
import ErrorState from '../components/ErrorState';
import LastUpdatedTag from '../components/LastUpdatedTag';
import LoadingState from '../components/LoadingState';
import PageHeader from '../components/PageHeader';
import { usePollingData } from '../hooks/usePollingData';
import { formatUtc } from '../lib/dates';
import { getJson } from '../lib/http';
import { generatePlaceholderVector } from '../lib/vectors';
import type { EmbeddingDiagnosticsResponse } from '../types/diagnostics';

interface SampleEmbeddingResponse {
  values?: number[];
  embedding?: number[];
}

const EmbeddingDiagnosticsPage = () => {
  const { data, isLoading, error, lastUpdated, refresh } = usePollingData<EmbeddingDiagnosticsResponse>(
    (signal) => getJson('/api/diagnostics/embedding', signal),
    { intervalMs: 15000 },
  );
  const [sampleVector, setSampleVector] = useState<number[]>([]);
  const [sampleSource, setSampleSource] = useState<'api' | 'placeholder'>('placeholder');
  const [isSampleLoading, setIsSampleLoading] = useState(false);
  const [sampleError, setSampleError] = useState<string | null>(null);

  const loadSample = useCallback(async () => {
    const vectorSize = data?.modelInfo.vectorSize ?? 512;
    setIsSampleLoading(true);
    setSampleError(null);
    try {
      const response = await getJson<SampleEmbeddingResponse>('/api/diagnostics/sample-embedding');
      const payload = response.embedding ?? response.values;
      if (payload && payload.length > 0) {
        setSampleVector(payload);
        setSampleSource('api');
        return;
      }

      throw new Error('Sample endpoint returned an empty vector.');
    } catch (err) {
      setSampleVector(generatePlaceholderVector(vectorSize));
      setSampleSource('placeholder');
      setSampleError('Using a deterministic placeholder vector until the sample endpoint is wired up.');
    } finally {
      setIsSampleLoading(false);
    }
  }, [data?.modelInfo.vectorSize]);

  useEffect(() => {
    if (data && sampleVector.length === 0) {
      loadSample();
    }
  }, [data, loadSample, sampleVector.length]);

  const modelExistsLabel = data?.modelFileExists ? (
    <Label color="green" icon="check" content="Model file detected" />
  ) : (
    <Label color="red" icon="warning circle" content="Model file missing" />
  );

  const telemetrySummary = useMemo(
    () => [
      { label: 'Execution Provider', value: data?.modelInfo.executionProvider ?? 'unknown' },
      { label: 'Model Version', value: data?.modelInfo.modelVersion ?? '—' },
      { label: 'Vector Size', value: data?.modelInfo.vectorSize ?? 512 },
      { label: 'Input Image Size', value: `${data?.modelInfo.inputImageSize ?? 112}px` },
    ],
    [data],
  );

  if (isLoading && !data) {
    return <LoadingState />;
  }

  if (error) {
    return <ErrorState error={error} onRetry={refresh} />;
  }

  return (
    <div>
      <PageHeader
        icon="microchip"
        title="Embedding Diagnostics"
        subtitle="Inspect ArcFace model health, telemetry, and mock embedding samples."
        lastUpdated={lastUpdated}
      />

      <Grid stackable columns={2} divided>
        <Grid.Column>
          <Segment inverted>
            <Header as="h3" dividing>
              <Icon name="microchip" /> Runtime Metadata
            </Header>
            <List inverted relaxed>
              <List.Item>
                <List.Header>Model Name</List.Header>
                {data?.modelInfo.modelName}
              </List.Item>
              <List.Item>
                <List.Header>Loaded At</List.Header>
                {formatUtc(data?.modelInfo.loadedAtUtc)}
              </List.Item>
              <List.Item>
                <List.Header>Model Path</List.Header>
                {data?.modelPath}
              </List.Item>
            </List>
            {modelExistsLabel}
          </Segment>
        </Grid.Column>

        <Grid.Column>
          <Segment inverted>
            <Header as="h3" dividing>
              <Icon name="heartbeat" /> Model Telemetry
            </Header>
            <List horizontal inverted divided>
              {telemetrySummary.map((metric) => (
                <List.Item key={metric.label}>
                  <List.Header>{metric.label}</List.Header>
                  {metric.value}
                </List.Item>
              ))}
            </List>
            <Statistic.Group widths={3} size="tiny" inverted>
              <Statistic label="Active Users" value={data?.activeUsers ?? 0} />
              <Statistic label="Total Users" value={data?.totalUsers ?? 0} />
              <Statistic label="Last User Change" value={formatUtc(data?.lastUserChangeUtc)} />
            </Statistic.Group>
          </Segment>
        </Grid.Column>
      </Grid>

      <Segment inverted>
        <Header as="h3">
          <Icon name="signal" /> Sample Embedding Preview
        </Header>
        <p>
          Generates a lightweight sparkline using the public diagnostics endpoint. Use this to sanity check vector
          magnitudes before shipping a new model build.
        </p>
        {sampleError && <Message warning icon="info circle" header="Placeholder vector" content={sampleError} />}
        {sampleVector.length > 0 && <EmbeddingSparkline values={sampleVector} />}
        <Label color={sampleSource === 'api' ? 'green' : 'blue'} content={`Source: ${sampleSource}`} />
        <Button
          primary
          floated="right"
          icon={isSampleLoading ? 'spinner' : 'refresh'}
          content={isSampleLoading ? 'Sampling…' : 'Refresh sample'}
          onClick={loadSample}
          disabled={isSampleLoading}
        />
        <div className="clearfix">
          <LastUpdatedTag timestamp={lastUpdated} />
        </div>
      </Segment>

      <Button icon="refresh" content="Refresh diagnostics" onClick={refresh} />
    </div>
  );
};

export default EmbeddingDiagnosticsPage;
