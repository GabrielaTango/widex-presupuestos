import { useNavigate } from 'react-router-dom';

export default function Pedidos() {
  const navigate = useNavigate();

  const pedidos = [
    { id: 'PED-001', fecha: '15/03/2024', items: 3, monto: '$45,000', estado: 'Confirmado' },
    { id: 'PED-002', fecha: '14/03/2024', items: 5, monto: '$23,500', estado: 'Pendiente' },
    { id: 'PED-003', fecha: '13/03/2024', items: 2, monto: '$67,800', estado: 'Enviado' },
    { id: 'PED-004', fecha: '12/03/2024', items: 1, monto: '$12,300', estado: 'Confirmado' },
    { id: 'PED-005', fecha: '11/03/2024', items: 4, monto: '$89,200', estado: 'Cancelado' },
    { id: 'PED-006', fecha: '10/03/2024', items: 6, monto: '$34,100', estado: 'Confirmado' },
    { id: 'PED-007', fecha: '09/03/2024', items: 2, monto: '$56,700', estado: 'Pendiente' },
    { id: 'PED-008', fecha: '08/03/2024', items: 3, monto: '$28,900', estado: 'Enviado' },
  ];

  const getStatusBadge = (estado: string) => {
    const styles: Record<string, string> = {
      'Confirmado': 'bg-success',
      'Pendiente': 'bg-warning text-dark',
      'Enviado': 'bg-info',
      'Cancelado': 'bg-danger',
    };
    return styles[estado] || 'bg-secondary';
  };

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2 className="mb-0">Pedidos</h2>
        <button
          className="btn"
          style={{ background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)', color: 'white', borderRadius: '8px', padding: '0.6rem 1.5rem', fontWeight: 500, border: 'none' }}
          onClick={() => navigate('/pedidos/nuevo')}
        >
          <i className="bi bi-plus-lg me-2"></i>Nuevo Pedido
        </button>
      </div>

      {/* Filtros */}
      <div style={{
        background: 'white', borderRadius: '12px', padding: '1.25rem 1.5rem', marginBottom: '1.5rem',
        boxShadow: '0 0.125rem 0.25rem rgba(0,0,0,0.075)', border: '1px solid #e9ecef',
      }}>
        <div className="row g-3 align-items-end">
          <div className="col-md-3">
            <label className="form-label" style={{ fontSize: '0.85rem', fontWeight: 500, color: '#495057' }}>Buscar</label>
            <input type="text" className="form-control" placeholder="Codigo de pedido..." style={{ borderRadius: '8px' }} />
          </div>
          <div className="col-md-2">
            <label className="form-label" style={{ fontSize: '0.85rem', fontWeight: 500, color: '#495057' }}>Estado</label>
            <select className="form-select" style={{ borderRadius: '8px' }}>
              <option value="">Todos</option>
              <option>Confirmado</option>
              <option>Pendiente</option>
              <option>Enviado</option>
              <option>Cancelado</option>
            </select>
          </div>
          <div className="col-md-2">
            <label className="form-label" style={{ fontSize: '0.85rem', fontWeight: 500, color: '#495057' }}>Desde</label>
            <input type="date" className="form-control" style={{ borderRadius: '8px' }} />
          </div>
          <div className="col-md-2">
            <label className="form-label" style={{ fontSize: '0.85rem', fontWeight: 500, color: '#495057' }}>Hasta</label>
            <input type="date" className="form-control" style={{ borderRadius: '8px' }} />
          </div>
          <div className="col-md-3 d-flex gap-2">
            <button className="btn btn-outline-secondary" style={{ borderRadius: '8px' }}>
              <i className="bi bi-funnel me-1"></i>Filtrar
            </button>
            <button className="btn btn-outline-secondary" style={{ borderRadius: '8px' }}>
              <i className="bi bi-arrow-counterclockwise"></i>
            </button>
          </div>
        </div>
      </div>

      {/* Tabla */}
      <div style={{
        background: 'white', borderRadius: '12px',
        boxShadow: '0 0.125rem 0.25rem rgba(0,0,0,0.075)', border: '1px solid #e9ecef',
      }}>
        <div style={{ padding: '1.5rem' }}>
          <div className="table-responsive">
            <table className="table table-hover mb-0">
              <thead>
                <tr>
                  {['Codigo', 'Fecha', 'Items', 'Monto', 'Estado', 'Acciones'].map((h) => (
                    <th key={h} style={{ fontWeight: 600, color: '#495057', backgroundColor: '#f8f9fa', padding: '0.875rem 0.75rem' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {pedidos.map((item) => (
                  <tr key={item.id}>
                    <td><strong>{item.id}</strong></td>
                    <td>{item.fecha}</td>
                    <td>{item.items}</td>
                    <td>{item.monto}</td>
                    <td>
                      <span className={`badge ${getStatusBadge(item.estado)}`} style={{ padding: '0.5rem 0.75rem', borderRadius: '6px', fontWeight: 500 }}>
                        {item.estado}
                      </span>
                    </td>
                    <td>
                      <button className="btn btn-sm btn-outline-primary me-1" style={{ borderRadius: '6px' }} title="Ver">
                        <i className="bi bi-eye"></i>
                      </button>
                      <button className="btn btn-sm btn-outline-secondary me-1" style={{ borderRadius: '6px' }} title="Editar">
                        <i className="bi bi-pencil"></i>
                      </button>
                      <button className="btn btn-sm btn-outline-danger" style={{ borderRadius: '6px' }} title="Eliminar">
                        <i className="bi bi-trash"></i>
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
