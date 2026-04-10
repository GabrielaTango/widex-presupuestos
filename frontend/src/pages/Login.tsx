import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { authService } from '../services/authService';
import logo from '../assets/logo.png';

export default function Login() {
  const [usuario, setUsuario] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();
  const { login } = useAuth();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    try {
      const response = await authService.login({ usuario, password });
      if (response.success) {
        login(response.data, response.data.token);
        navigate('/');
      } else {
        setError(response.message);
      }
    } catch {
      setError('Usuario o contrasena incorrectos');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{
      minHeight: '100vh',
      fontFamily: "'Segoe UI', Tahoma, Geneva, Verdana, sans-serif",
      background: 'linear-gradient(135deg, #f5f7fa 0%, #e4e8ed 100%)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
    }}>
      <div className="login-container" style={{
        display: 'flex',
        width: '100%',
        maxWidth: '1000px',
        minHeight: '600px',
        background: 'white',
        borderRadius: '20px',
        overflow: 'hidden',
        boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.15)',
        margin: '2rem',
      }}>
        {/* Brand Panel */}
        <div className="login-brand-panel" style={{
          flex: 1,
          background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)',
          padding: '3rem',
          display: 'flex',
          flexDirection: 'column',
          justifyContent: 'center',
          position: 'relative',
          overflow: 'hidden',
        }}>
          <div style={{ position: 'relative', zIndex: 1 }}>
            <div style={{ display: 'flex', alignItems: 'center', marginBottom: '2rem' }}>
              <img src={logo} alt="Widex" style={{ height: '50px', filter: 'brightness(0) invert(1)', marginRight: '1rem' }} />
            </div>
            <p style={{
              color: 'rgba(255, 255, 255, 0.8)',
              fontSize: '1.1rem',
              marginBottom: '3rem',
              lineHeight: 1.6,
            }}>
              Sistema integral de gestion de presupuestos y pedidos.
              Optimiza tus procesos y mejora la eficiencia de tu organizacion.
            </p>
            <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
              {[
                { icon: 'bi-clipboard2-pulse', text: 'Gestion de presupuestos', gradient: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)' },
                { icon: 'bi-box-seam', text: 'Control de pedidos e inventario', gradient: 'linear-gradient(135deg, #11998e 0%, #38ef7d 100%)' },
                { icon: 'bi-graph-up-arrow', text: 'Reportes y estadisticas en tiempo real', gradient: 'linear-gradient(135deg, #f093fb 0%, #f5576c 100%)' },
                { icon: 'bi-shield-check', text: 'Seguridad y cumplimiento normativo', gradient: 'linear-gradient(135deg, #4facfe 0%, #00f2fe 100%)' },
              ].map((item, i) => (
                <li key={i} style={{ display: 'flex', alignItems: 'center', color: 'rgba(255,255,255,0.9)', marginBottom: '1rem', fontSize: '0.95rem' }}>
                  <span style={{
                    width: '40px', height: '40px', borderRadius: '10px',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    marginRight: '1rem', fontSize: '1.1rem', background: item.gradient,
                  }}>
                    <i className={`bi ${item.icon}`}></i>
                  </span>
                  {item.text}
                </li>
              ))}
            </ul>
          </div>
        </div>

        {/* Login Panel */}
        <div className="login-form-panel" style={{ flex: 1, padding: '3rem', display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
          <div style={{ marginBottom: '2rem' }}>
            <h2 style={{ color: '#212529', fontSize: '1.75rem', fontWeight: 700, marginBottom: '0.5rem' }}>Bienvenido de vuelta</h2>
            <p style={{ color: '#6c757d', margin: 0 }}>Ingresa tus credenciales para acceder al sistema</p>
          </div>

          {error && (
            <div className="alert alert-danger d-flex align-items-center" style={{ borderRadius: '12px', border: 'none', padding: '1rem', marginBottom: '1.5rem' }}>
              <i className="bi bi-exclamation-circle-fill me-3" style={{ fontSize: '1.25rem' }}></i>
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit}>
            <div className="form-floating position-relative mb-3">
              <i className="bi bi-person" style={{ position: 'absolute', left: '1rem', top: '50%', transform: 'translateY(-50%)', color: '#6c757d', fontSize: '1.25rem', zIndex: 5 }}></i>
              <input
                type="text"
                className="form-control"
                id="usuario"
                placeholder="Usuario"
                value={usuario}
                onChange={(e) => setUsuario(e.target.value)}
                required
                style={{ border: '2px solid #e9ecef', borderRadius: '12px', paddingLeft: '3rem', height: '60px', fontSize: '1rem' }}
              />
              <label htmlFor="usuario" style={{ paddingLeft: '3rem', color: '#6c757d' }}>Usuario</label>
            </div>

            <div className="form-floating position-relative mb-3">
              <i className="bi bi-lock" style={{ position: 'absolute', left: '1rem', top: '50%', transform: 'translateY(-50%)', color: '#6c757d', fontSize: '1.25rem', zIndex: 5 }}></i>
              <input
                type={showPassword ? 'text' : 'password'}
                className="form-control"
                id="password"
                placeholder="Contrasena"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                style={{ border: '2px solid #e9ecef', borderRadius: '12px', paddingLeft: '3rem', height: '60px', fontSize: '1rem' }}
              />
              <label htmlFor="password" style={{ paddingLeft: '3rem', color: '#6c757d' }}>Contrasena</label>
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                style={{ position: 'absolute', right: '1rem', top: '50%', transform: 'translateY(-50%)', background: 'none', border: 'none', color: '#6c757d', cursor: 'pointer', zIndex: 5, padding: '0.5rem' }}
              >
                <i className={`bi ${showPassword ? 'bi-eye-slash' : 'bi-eye'}`}></i>
              </button>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
              <div className="form-check">
                <input className="form-check-input" type="checkbox" id="rememberMe" />
                <label className="form-check-label" htmlFor="rememberMe" style={{ color: '#6c757d', fontSize: '0.9rem' }}>Recordarme</label>
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="btn"
              style={{
                width: '100%', padding: '1rem', fontSize: '1rem', fontWeight: 600,
                borderRadius: '12px', background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)', border: 'none',
                color: 'white', transition: 'all 0.3s ease', marginBottom: '1.5rem',
                cursor: loading ? 'not-allowed' : 'pointer', opacity: loading ? 0.7 : 1,
              }}
            >
              {loading && <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>}
              {loading ? 'Ingresando...' : 'Iniciar Sesion'}
            </button>
          </form>

          <div style={{ marginTop: '2rem', textAlign: 'center' }}>
            <p style={{ color: '#6c757d', fontSize: '0.8rem', margin: 0 }}>
              &copy; 2024 Widex. Todos los derechos reservados.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
