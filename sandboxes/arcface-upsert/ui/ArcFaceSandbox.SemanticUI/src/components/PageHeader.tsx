import type { SemanticICONS } from 'semantic-ui-react';
import { Header, Icon } from 'semantic-ui-react';
import LastUpdatedTag from './LastUpdatedTag';

interface PageHeaderProps {
  icon: SemanticICONS;
  title: string;
  subtitle: string;
  lastUpdated?: Date | null;
}

const PageHeader = ({ icon, title, subtitle, lastUpdated }: PageHeaderProps) => (
  <div className="page-header">
    <Header as="h1" inverted>
      <Icon name={icon} />
      <Header.Content>
        {title}
        <Header.Subheader>{subtitle}</Header.Subheader>
      </Header.Content>
    </Header>
    {lastUpdated && <LastUpdatedTag timestamp={lastUpdated} />}
  </div>
);

export default PageHeader;
