import { useNavigate } from 'react-router-dom';

export default function Dashboard() {
  const navigate = useNavigate();

  const stats = [
    { label: 'Presupuestos', value: '156', icon: 'bi-file-earmark-text', gradient: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)', shadow: 'rgba(102, 126, 234, 0.4)', change: '+12%' },
    { label: 'Pedidos Activos', value: '43', icon: 'bi-cart3', gradient: 'linear-gradient(135deg, #11998e 0%, #38ef7d 100%)', shadow: 'rgba(17, 153, 142, 0.4)', change: '+8%' },
    { label: 'Clientes', value: '89', icon: 'bi-people', gradient: 'linear-gradient(135deg, #4facfe 0%, #00f2fe 100%)', shadow: 'rgba(79, 172, 254, 0.4)', change: '+5%' },
  ];

  const recentBudgets = [
    { id: 'PRE-001', cliente: 'Hospital Central', fecha: '15/03/2024', monto: '$45,000', estado: 'Aprobado' },
    { id: 'PRE-002', cliente: 'Clinica del Sur', fecha: '14/03/2024', monto: '$23,500', estado: 'Pendiente' },
    { id: 'PRE-003', cliente: 'Centro Medico Norte', fecha: '13/03/2024', monto: '$67,800', estado: 'En revision' },
    { id: 'PRE-004', cliente: 'Sanatorio San Jose', fecha: '12/03/2024', monto: '$12,300', estado: 'Aprobado' },
    { id: 'PRE-005', cliente: 'Hospital Regional', fecha: '11/03/2024', monto: '$89,200', estado: 'Rechazado' },
  ];

  const getStatusBadge = (estado: string) => {
    const styles: Record<string, string> = {
      'Aprobado': 'bg-success',
      'Pendiente': 'bg-warning text-dark',
      'En revision': 'bg-info',
      'Rechazado': 'bg-danger',
    };
    return styles[estado] || 'bg-secondary';
  };

  return (
    <>
      <h2 className="mb-4">Dashboard</h2>

      {/* Stat Cards */}
      <div className="row g-4 mb-4">
        {stats.map((stat, i) => (
          <div className="col-md-6 col-xl-3" key={i}>
            <div style={{
              background: 'white', borderRadius: '12px', padding: '1.5rem',
              boxShadow: '0 0.125rem 0.25rem rgba(0,0,0,0.075)', border: '1px solid #e9ecef',
            }}>
              <div className="d-flex justify-content-between align-items-start">
                <div>
                  <p style={{ color: '#6c757d', fontSize: '0.875rem', marginBottom: '0.5rem' }}>{stat.label}</p>
                  <h3 style={{ fontSize: '1.75rem', fontWeight: 700, color: '#212529', margin: 0 }}>{stat.value}</h3>
                </div>
                <div style={{
                  width: '56px', height: '56px', borderRadius: '14px',
                  display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1.6rem',
                  color: 'white', background: stat.gradient, boxShadow: `0 4px 15px ${stat.shadow}`,
                }}>
                  <i className={`bi ${stat.icon}`}></i>
                </div>
              </div>
              <div className="mt-3">
                <span style={{ color: '#198754', fontSize: '0.85rem', fontWeight: 600 }}>
                  <i className="bi bi-arrow-up-short"></i>{stat.change}
                </span>
                <span style={{ color: '#6c757d', fontSize: '0.85rem', marginLeft: '0.5rem' }}>vs mes anterior</span>
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Recent Budgets Table */}
      <div style={{
        background: 'white', borderRadius: '12px',
        boxShadow: '0 0.125rem 0.25rem rgba(0,0,0,0.075)', border: '1px solid #e9ecef',
      }}>
        <div style={{ background: 'white', borderBottom: '1px solid #e9ecef', padding: '1rem 1.5rem', borderRadius: '12px 12px 0 0' }}>
          <div className="d-flex justify-content-between align-items-center">
            <h5 className="mb-0" style={{ fontWeight: 600 }}>Ultimos Presupuestos</h5>
            <button className="btn btn-sm" style={{ background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)', color: 'white', borderRadius: '8px', padding: '0.5rem 1.25rem', border: 'none' }}
              onClick={() => navigate('/presupuestos/nuevo')}>
              <i className="bi bi-plus-lg me-1"></i>Nuevo
            </button>
          </div>
        </div>
        <div style={{ padding: '1.5rem' }}>
          <div className="table-responsive">
            <table className="table table-hover">
              <thead>
                <tr>
                  {['Codigo', 'Cliente', 'Fecha', 'Monto', 'Estado', 'Acciones'].map((h) => (
                    <th key={h} style={{ fontWeight: 600, color: '#495057', backgroundColor: '#f8f9fa' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {recentBudgets.map((item) => (
                  <tr key={item.id}>
                    <td><strong>{item.id}</strong></td>
                    <td>{item.cliente}</td>
                    <td>{item.fecha}</td>
                    <td>{item.monto}</td>
                    <td>
                      <span className={`badge ${getStatusBadge(item.estado)}`} style={{ padding: '0.5rem 0.75rem', borderRadius: '6px', fontWeight: 500 }}>
                        {item.estado}
                      </span>
                    </td>
                    <td>
                      <button className="btn btn-sm btn-outline-primary me-1" style={{ borderRadius: '6px' }}>
                        <i className="bi bi-eye"></i>
                      </button>
                      <button className="btn btn-sm btn-outline-secondary" style={{ borderRadius: '6px' }}>
                        <i className="bi bi-pencil"></i>
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </>
  );
}
