import { Routes, Route } from 'react-router-dom'
import { Container } from 'semantic-ui-react'
import Layout from './components/Layout'
import HomePage from './pages/HomePage'
import ItemsPage from './pages/ItemsPage'

function App() {
  return (
    <Layout>
      <Container style={{ marginTop: '2em' }}>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/items" element={<ItemsPage />} />
        </Routes>
      </Container>
    </Layout>
  )
}

export default App
