import { Header, Icon, Segment, Grid, Statistic, Card } from 'semantic-ui-react'
import { useState, useEffect } from 'react'
import { apiClient } from '../services/api'

interface HealthStatus {
  status: string
  uptime: string
}

export default function HomePage() {
  const [health, setHealth] = useState<HealthStatus | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const fetchHealth = async () => {
      try {
        const response = await apiClient.get<HealthStatus>('/api/health')
        setHealth(response.data)
      } catch {
        setHealth({ status: 'unhealthy', uptime: 'N/A' })
      } finally {
        setLoading(false)
      }
    }
    fetchHealth()
  }, [])

  return (
    <>
      <Segment placeholder>
        <Header icon>
          <Icon name="rocket" />
          Welcome to Aspire Full
        </Header>
        <p>A .NET Aspire distributed application with PostgreSQL, Redis, and Semantic UI</p>
      </Segment>

      <Grid columns={3} stackable style={{ marginTop: '2em' }}>
        <Grid.Column>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="database" /> PostgreSQL
              </Card.Header>
              <Card.Description>
                Primary database with pgvector support for semantic search capabilities.
              </Card.Description>
            </Card.Content>
          </Card>
        </Grid.Column>

        <Grid.Column>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="lightning" /> Redis
              </Card.Header>
              <Card.Description>
                In-memory cache for session management and distributed caching.
              </Card.Description>
            </Card.Content>
          </Card>
        </Grid.Column>

        <Grid.Column>
          <Card fluid>
            <Card.Content>
              <Card.Header>
                <Icon name="chart line" /> Telemetry
              </Card.Header>
              <Card.Description>
                OpenTelemetry integration for distributed tracing and metrics.
              </Card.Description>
            </Card.Content>
          </Card>
        </Grid.Column>
      </Grid>

      <Segment loading={loading} style={{ marginTop: '2em' }}>
        <Header as="h3">API Health Status</Header>
        <Statistic.Group widths={2}>
          <Statistic color={health?.status === 'healthy' ? 'green' : 'red'}>
            <Statistic.Value>
              <Icon name={health?.status === 'healthy' ? 'check circle' : 'warning circle'} />
            </Statistic.Value>
            <Statistic.Label>{health?.status || 'Unknown'}</Statistic.Label>
          </Statistic>
          <Statistic>
            <Statistic.Value>{health?.uptime || 'N/A'}</Statistic.Value>
            <Statistic.Label>Uptime</Statistic.Label>
          </Statistic>
        </Statistic.Group>
      </Segment>
    </>
  )
}
