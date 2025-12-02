import { Navigate, NavLink, Route, Routes } from 'react-router-dom';
import type { SemanticICONS } from 'semantic-ui-react';
import { Container, Icon, Menu } from 'semantic-ui-react';
import EmbeddingDiagnosticsPage from './pages/EmbeddingDiagnosticsPage';
import UsersSandboxPage from './pages/UsersSandboxPage';
import VectorStoreMonitorPage from './pages/VectorStoreMonitorPage';

type MenuItem = { to: string; label: string; icon: SemanticICONS };

const menuItems: MenuItem[] = [
  { to: '/embedding', label: 'Embedding Diagnostics', icon: 'microchip' },
  { to: '/users', label: 'Users Sandbox', icon: 'users' },
  { to: '/vector-store', label: 'Vector Store Monitor', icon: 'database' },
];

const App = () => (
  <div className="page-container">
    <Menu inverted secondary pointing stackable className="app-menu">
      <Menu.Item header>
        <Icon name="camera retro" /> ArcFace Sandbox
      </Menu.Item>
      {menuItems.map((item) => (
        <Menu.Item key={item.to} as={NavLink} to={item.to} end>
          <Icon name={item.icon} />
          {item.label}
        </Menu.Item>
      ))}
      <Menu.Menu position="right">
        <Menu.Item as="a" href="/docs" target="_blank" rel="noreferrer">
          <Icon name="book" /> Docs
        </Menu.Item>
      </Menu.Menu>
    </Menu>

    <Container>
      <Routes>
        <Route path="/" element={<Navigate to="/embedding" replace />} />
        <Route path="/embedding" element={<EmbeddingDiagnosticsPage />} />
        <Route path="/users" element={<UsersSandboxPage />} />
        <Route path="/vector-store" element={<VectorStoreMonitorPage />} />
      </Routes>
    </Container>
  </div>
);

export default App;
