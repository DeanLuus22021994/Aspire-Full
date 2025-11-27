import { Link, useLocation } from 'react-router-dom'
import { Menu, Container, Icon } from 'semantic-ui-react'

interface LayoutProps {
  children: React.ReactNode
}

export default function Layout({ children }: LayoutProps) {
  const location = useLocation()

  return (
    <>
      <Menu fixed="top" inverted>
        <Container>
          <Menu.Item as={Link} to="/" header>
            <Icon name="rocket" />
            Aspire Full
          </Menu.Item>
          <Menu.Item
            as={Link}
            to="/"
            active={location.pathname === '/'}
          >
            Home
          </Menu.Item>
          <Menu.Item
            as={Link}
            to="/items"
            active={location.pathname === '/items'}
          >
            Items
          </Menu.Item>
        </Container>
      </Menu>

      <div className="main-content">
        {children}
      </div>
    </>
  )
}
