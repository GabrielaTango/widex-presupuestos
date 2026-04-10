import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import ProtectedRoute from './components/ProtectedRoute';
import Layout from './components/Layout';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Presupuestos from './pages/Presupuestos';
import Presupuesto from './pages/Presupuesto';
import Pedidos from './pages/Pedidos';
import Pedido from './pages/Pedido';
import './index.css';
import 'bootstrap/dist/css/bootstrap.min.css';
import 'bootstrap-icons/font/bootstrap-icons.css';
import 'bootstrap/dist/js/bootstrap.bundle.min.js';

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/" element={
            <ProtectedRoute>
              <Layout><Dashboard /></Layout>
            </ProtectedRoute>
          } />
          <Route path="/presupuestos" element={
            <ProtectedRoute>
              <Layout><Presupuestos /></Layout>
            </ProtectedRoute>
          } />
          <Route path="/presupuestos/nuevo" element={
            <ProtectedRoute>
              <Layout><Presupuesto /></Layout>
            </ProtectedRoute>
          } />
          <Route path="/pedidos" element={
            <ProtectedRoute>
              <Layout><Pedidos /></Layout>
            </ProtectedRoute>
          } />
          <Route path="/pedidos/nuevo" element={
            <ProtectedRoute>
              <Layout><Pedido /></Layout>
            </ProtectedRoute>
          } />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}

export default App;
