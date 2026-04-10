import { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { useNavigate, useLocation } from 'react-router-dom';
import logo from '../assets/logo.png';

const menuItems = [
  { icon: 'bi-grid-1x2-fill', label: 'Dashboard', path: '/' },
  { icon: 'bi-file-earmark-text', label: 'Presupuestos', path: '/presupuestos' },
  { icon: 'bi-cart3', label: 'Pedidos', path: '/pedidos' },
];

export default function Layout({ children }: { children: React.ReactNode }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const isActive = (path: string) => {
    if (path === '/') return location.pathname === '/';
    return location.pathname.startsWith(path);
  };

  const handleNav = (path: string) => {
    navigate(path);
    setSidebarOpen(false);
  };

  return (
    <div style={{ fontFamily: "'Segoe UI', Tahoma, Geneva, Verdana, sans-serif", backgroundColor: '#f8f9fa', minHeight: '100vh' }}>
      {/* Hamburger button - mobile only */}
      <button
        className="hamburger-btn"
        onClick={() => setSidebarOpen(true)}
        style={{
          display: 'none',
          position: 'fixed', top: '0.75rem', left: '0.75rem', zIndex: 1100,
          width: '40px', height: '40px', borderRadius: '8px',
          background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)',
          color: 'white', border: 'none', alignItems: 'center', justifyContent: 'center',
          fontSize: '1.2rem', cursor: 'pointer',
          boxShadow: '0 2px 8px rgba(0,0,0,0.2)',
        }}
      >
        <i className="bi bi-list"></i>
      </button>

      {/* Overlay - mobile only */}
      <div
        className="sidebar-overlay"
        onClick={() => setSidebarOpen(false)}
        style={{
          display: 'none',
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(0,0,0,0.5)', zIndex: 1050,
          opacity: sidebarOpen ? 1 : 0,
          pointerEvents: sidebarOpen ? 'auto' : 'none',
          transition: 'opacity 0.3s',
        }}
      />

      {/* Sidebar */}
      <nav
        className={`sidebar-nav${sidebarOpen ? ' open' : ''}`}
        style={{
          minHeight: '100vh', background: 'linear-gradient(180deg, #ffffff 0%, #f0f4f8 100%)',
          borderRight: '1px solid #dee2e6', position: 'fixed', width: '260px', zIndex: 1060,
          display: 'flex', flexDirection: 'column', top: 0, left: 0,
        }}
      >
        <div style={{ padding: '1.5rem', borderBottom: '1px solid #dee2e6', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ cursor: 'pointer', display: 'flex', alignItems: 'center' }} onClick={() => handleNav('/')}>
            <img src={logo} alt="Widex" style={{ height: '30px', marginRight: '0.75rem' }} />
          </div>
          {/* Close button - visible on mobile when open */}
          <button
            className="hamburger-btn"
            onClick={() => setSidebarOpen(false)}
            style={{
              display: 'none',
              width: '32px', height: '32px', borderRadius: '6px',
              background: 'transparent', color: '#495057', border: '1px solid #dee2e6',
              alignItems: 'center', justifyContent: 'center',
              fontSize: '1rem', cursor: 'pointer',
            }}
          >
            <i className="bi bi-x-lg"></i>
          </button>
        </div>

        <div style={{ fontSize: '0.7rem', textTransform: 'uppercase', letterSpacing: '1px', color: '#adb5bd', padding: '1rem 1.25rem 0.5rem', fontWeight: 600 }}>
          Menu Principal
        </div>
        <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
          {menuItems.map((item, i) => (
            <li key={i}>
              <a
                href="#"
                onClick={(e) => { e.preventDefault(); handleNav(item.path); }}
                style={{
                  color: isActive(item.path) ? 'white' : '#495057',
                  padding: '0.75rem 1.25rem', borderRadius: '8px', margin: '0.25rem 0.75rem',
                  background: isActive(item.path) ? 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)' : 'transparent',
                  transition: 'all 0.2s ease', textDecoration: 'none', display: 'flex', alignItems: 'center',
                }}
              >
                <i className={`bi ${item.icon}`} style={{ marginRight: '10px', fontSize: '1.1rem' }}></i>
                {item.label}
              </a>
            </li>
          ))}
        </ul>

        <div style={{ padding: '1rem 1.25rem', borderTop: '1px solid #dee2e6', marginTop: 'auto' }}>
          <div className="d-flex align-items-center gap-2 mb-2">
            <img
              src={`https://ui-avatars.com/api/?name=${encodeURIComponent(user?.nombre || 'User')}&background=212529&color=fff&size=36`}
              alt="User"
              style={{ width: '36px', height: '36px', borderRadius: '50%' }}
            />
            <div style={{ overflow: 'hidden' }}>
              <div style={{ fontWeight: 600, fontSize: '0.9rem', color: '#212529', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                {user?.nombre || 'Usuario'}
              </div>
              <div style={{ fontSize: '0.75rem', color: '#6c757d' }}>{user?.mail || ''}</div>
            </div>
          </div>
          <button
            onClick={handleLogout}
            style={{
              width: '100%', padding: '0.5rem', borderRadius: '8px', border: '1px solid #dee2e6',
              background: 'transparent', color: '#dc3545', cursor: 'pointer', fontSize: '0.85rem',
              fontWeight: 500, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.5rem',
              transition: 'all 0.2s',
            }}
            onMouseEnter={e => { e.currentTarget.style.background = '#dc3545'; e.currentTarget.style.color = 'white'; }}
            onMouseLeave={e => { e.currentTarget.style.background = 'transparent'; e.currentTarget.style.color = '#dc3545'; }}
          >
            <i className="bi bi-box-arrow-right"></i>Cerrar Sesion
          </button>
        </div>
      </nav>

      {/* Main Content */}
      <main className="main-content" style={{ marginLeft: '260px', padding: '2rem' }}>
        {children}
      </main>
    </div>
  );
}
