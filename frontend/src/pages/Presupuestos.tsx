import { useNavigate } from 'react-router-dom';

export default function Presupuestos() {
  const navigate = useNavigate();

  const presupuestos = [
    { id: 'PRE-001', cliente: 'Hospital Central', paciente: 'Juan Perez', fecha: '15/03/2024', monto: '$45,000', estado: 'Aprobado' },
    { id: 'PRE-002', cliente: 'Clinica del Sur', paciente: 'Maria Garcia', fecha: '14/03/2024', monto: '$23,500', estado: 'Pendiente' },
    { id: 'PRE-003', cliente: 'Centro Medico Norte', paciente: 'Carlos Lopez', fecha: '13/03/2024', monto: '$67,800', estado: 'En revision' },
    { id: 'PRE-004', cliente: 'Sanatorio San Jose', paciente: 'Ana Martinez', fecha: '12/03/2024', monto: '$12,300', estado: 'Aprobado' },
    { id: 'PRE-005', cliente: 'Hospital Regional', paciente: 'Roberto Diaz', fecha: '11/03/2024', monto: '$89,200', estado: 'Rechazado' },
    { id: 'PRE-006', cliente: 'Clinica Modelo', paciente: 'Laura Fernandez', fecha: '10/03/2024', monto: '$34,100', estado: 'Aprobado' },
    { id: 'PRE-007', cliente: 'Hospital Italiano', paciente: 'Diego Ruiz', fecha: '09/03/2024', monto: '$56,700', estado: 'Pendiente' },
    { id: 'PRE-008', cliente: 'Centro Audiologico', paciente: 'Sofia Torres', fecha: '08/03/2024', monto: '$28,900', estado: 'Aprobado' },
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
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2 className="mb-0">Presupuestos</h2>
        <button
          className="btn"
          style={{ background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)', color: 'white', borderRadius: '8px', padding: '0.6rem 1.5rem', fontWeight: 500, border: 'none' }}
          onClick={() => navigate('/presupuestos/nuevo')}
        >
          <i className="bi bi-plus-lg me-2"></i>Nuevo Presupuesto
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
            <input type="text" className="form-control" placeholder="Codigo, cliente, paciente..." style={{ borderRadius: '8px' }} />
          </div>
          <div className="col-md-2">
            <label className="form-label" style={{ fontSize: '0.85rem', fontWeight: 500, color: '#495057' }}>Estado</label>
            <select className="form-select" style={{ borderRadius: '8px' }}>
              <option value="">Todos</option>
              <option>Aprobado</option>
              <option>Pendiente</option>
              <option>En revision</option>
              <option>Rechazado</option>
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
                  {['Codigo', 'Cliente', 'Paciente', 'Fecha', 'Monto', 'Estado', 'Acciones'].map((h) => (
                    <th key={h} style={{ fontWeight: 600, color: '#495057', backgroundColor: '#f8f9fa', padding: '0.875rem 0.75rem' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {presupuestos.map((item) => (
                  <tr key={item.id}>
                    <td><strong>{item.id}</strong></td>
                    <td>{item.cliente}</td>
                    <td>{item.paciente}</td>
                    <td>{item.fecha}</td>
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
