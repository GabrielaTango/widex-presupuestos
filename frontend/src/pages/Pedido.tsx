import { useState, useEffect, useRef } from 'react';
import api from '../services/api';

interface Categoria {
  idFolder: string;
  descrip: string;
  idParent: string;
  path: string;
}

interface CategoriaData {
  padres: Categoria[];
  seleccionada: Categoria | null;
  hijos: Categoria[];
}

interface Articulo {
  id: number;
  codigo: string;
  descripcion: string;
  precio: number;
  stock: number;
}

interface ItemPedido {
  id: number;
  codigo: string;
  descripcion: string;
  precio: number;
  cantidad: number;
  importe: number;
}

export default function Pedido() {
  const [categoriaData, setCategoriaData] = useState<CategoriaData>({ padres: [], seleccionada: null, hijos: [] });
  const [articulos, setArticulos] = useState<Articulo[]>([]);
  const [filtrados, setFiltrados] = useState<Articulo[]>([]);
  const [buscar, setBuscar] = useState('');
  const [contadores, setContadores] = useState<Record<number, number>>({});
  const [items, setItems] = useState<ItemPedido[]>([]);
  const [loading, setLoading] = useState(false);
  const modalRef = useRef<HTMLDivElement>(null);

  const total = items.reduce((sum, i) => sum + i.importe, 0);
  const iva = total * 0.21;
  const totalFinal = total + iva;
  const formatMoney = (n: number) => `$ ${n.toFixed(2)}`;
  const today = new Date().toISOString().split('T')[0];

  const cargarCategorias = async (id: string) => {
    try {
      const res = await api.get(`/articulos/categorias/${id}`);
      if (res.data.success) {
        setCategoriaData(res.data.data);
        const path = res.data.data.seleccionada?.path;
        if (path) cargarArticulos(path);
      }
    } catch (err) {
      console.error('Error cargando categorias:', err);
    }
  };

  const cargarArticulos = async (folderPath: string) => {
    setLoading(true);
    try {
      const res = await api.get(`/articulos/listar/${encodeURIComponent(folderPath)}`);
      if (res.data.success) {
        setArticulos(res.data.data);
        setFiltrados(res.data.data);
        setBuscar('');
        setContadores({});
      }
    } catch (err) {
      console.error('Error cargando articulos:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    cargarCategorias('45');
  }, []);

  const buscarProductos = (term: string) => {
    setBuscar(term);
    const upper = term.toUpperCase();
    setFiltrados(articulos.filter(a =>
      a.descripcion.toUpperCase().includes(upper) || a.codigo.toUpperCase().includes(upper)
    ));
  };

  const sumar = (id: number) => setContadores(prev => ({ ...prev, [id]: (prev[id] || 0) + 1 }));
  const restar = (id: number) => setContadores(prev => ({ ...prev, [id]: Math.max((prev[id] || 0) - 1, 0) }));

  const agregarItem = (articulo: Articulo) => {
    const cant = contadores[articulo.id] || 0;
    if (cant === 0) return;

    setItems(prev => {
      const existing = prev.find(i => i.id === articulo.id);
      if (existing) {
        return prev.map(i =>
          i.id === articulo.id
            ? { ...i, cantidad: i.cantidad + cant, importe: (i.cantidad + cant) * i.precio }
            : i
        );
      }
      return [...prev, {
        id: articulo.id,
        codigo: articulo.codigo,
        descripcion: articulo.descripcion,
        precio: articulo.precio,
        cantidad: cant,
        importe: cant * articulo.precio,
      }];
    });
    setContadores(prev => ({ ...prev, [articulo.id]: 0 }));
  };

  const aumentarCantidadCarrito = (id: number) => {
    setItems(prev => prev.map(i =>
      i.id === id ? { ...i, cantidad: i.cantidad + 1, importe: (i.cantidad + 1) * i.precio } : i
    ));
  };

  const disminuirCantidadCarrito = (id: number) => {
    setItems(prev => {
      const item = prev.find(i => i.id === id);
      if (!item) return prev;
      if (item.cantidad <= 1) return prev.filter(i => i.id !== id);
      return prev.map(i =>
        i.id === id ? { ...i, cantidad: i.cantidad - 1, importe: (i.cantidad - 1) * i.precio } : i
      );
    });
  };

  const eliminarItem = (id: number) => {
    setItems(prev => prev.filter(i => i.id !== id));
  };

  const limpiarPedido = () => setItems([]);

  return (
    <>
      <div style={{ background: 'white', borderRadius: '12px', boxShadow: '0 0.125rem 0.25rem rgba(0,0,0,0.075)', border: '1px solid #e9ecef', overflow: 'hidden' }}>
        {/* Header */}
        <div className="presupuesto-header" style={{
          background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)', color: 'white', padding: '1.25rem 1.75rem',
          display: 'flex', justifyContent: 'space-between', alignItems: 'center',
        }}>
          <div>
            <h4 style={{ margin: 0, fontWeight: 600 }}>
              <i className="bi bi-cart3 me-2"></i>Nuevo Pedido
            </h4>
            <small style={{ opacity: 0.75 }}>Carga de Pedido</small>
          </div>
          <div className="d-flex align-items-center gap-3">
            <button
              type="button"
              className="btn btn-light position-relative"
              data-bs-toggle="modal"
              data-bs-target="#pedidoModal"
            >
              <i className="bi bi-basket me-1"></i> Ver Pedido
              {items.length > 0 && (
                <span className="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger">
                  {items.length}
                </span>
              )}
            </button>
            <div>
              <div style={{ background: 'rgba(255,255,255,0.15)', padding: '0.5rem 1rem', borderRadius: '20px', fontSize: '0.9rem' }}>
                <strong>N:</strong> <span>0001-00000001</span>
              </div>
              <div className="mt-2">
                <input type="date" className="form-control form-control-sm" defaultValue={today}
                  style={{ maxWidth: '160px', background: 'rgba(255,255,255,0.1)', color: 'white', border: '1px solid rgba(255,255,255,0.3)', marginLeft: 'auto' }} />
              </div>
            </div>
          </div>
        </div>

        {/* Body */}
        <div style={{ padding: '1.75rem' }}>
          {/* Categorias */}
          <div style={{ background: '#f8f9fa', borderRadius: '10px', padding: '1.25rem', marginBottom: '1.5rem' }}>
            <h6 style={{ color: '#212529', fontWeight: 600, borderBottom: '2px solid #e9ecef', paddingBottom: '0.5rem', marginBottom: '1rem' }}>
              <i className="bi bi-folder me-2"></i>Categorias
            </h6>
            <nav aria-label="breadcrumb">
              <ol className="breadcrumb mb-2">
                {categoriaData.padres.map(p => (
                  <li className="breadcrumb-item" key={p.idFolder}>
                    <a href="#" onClick={(e) => { e.preventDefault(); cargarCategorias(p.idFolder); }} className="text-decoration-none">{p.descrip}</a>
                  </li>
                ))}
                {categoriaData.seleccionada && (
                  <li className="breadcrumb-item active">{categoriaData.seleccionada.descrip}</li>
                )}
              </ol>
            </nav>
            <div className="d-flex flex-wrap gap-2">
              {categoriaData.hijos.map(h => (
                <button
                  key={h.idFolder}
                  type="button"
                  className="btn btn-sm"
                  style={{ background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)', color: 'white', borderRadius: '8px', border: 'none' }}
                  onClick={() => cargarCategorias(h.idFolder)}
                >
                  {h.descrip}
                </button>
              ))}
            </div>
          </div>

          {/* Buscador */}
          <div className="d-flex justify-content-center mb-4">
            <div className="input-group" style={{ maxWidth: '500px' }}>
              <span className="input-group-text"><i className="bi bi-search"></i></span>
              <input
                type="search"
                className="form-control"
                placeholder="Buscar articulo por codigo o descripcion..."
                value={buscar}
                onChange={e => buscarProductos(e.target.value)}
                style={{ borderRadius: '0 8px 8px 0' }}
              />
            </div>
          </div>

          {/* Lista de Articulos */}
          <div>
            <h6 style={{ color: '#212529', fontWeight: 600, borderBottom: '2px solid #e9ecef', paddingBottom: '0.5rem', marginBottom: '1rem' }}>
              <i className="bi bi-list-check me-2"></i>Articulos ({filtrados.length})
            </h6>
            {loading ? (
              <div className="text-center py-5">
                <div className="spinner-border text-secondary" role="status">
                  <span className="visually-hidden">Cargando...</span>
                </div>
              </div>
            ) : (
              <div className="table-responsive">
                <table className="table mb-0" style={{ borderRadius: '10px', overflow: 'hidden' }}>
                  <thead>
                    <tr style={{ background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)', color: 'white' }}>
                      <th className="hide-on-mobile" style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '8%' }}></th>
                      <th style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '12%' }}>Codigo</th>
                      <th style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '35%' }}>Descripcion</th>
                      <th className="hide-on-mobile" style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '13%' }}>Precio</th>
                      <th style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '18%', textAlign: 'center' }}>Cantidad</th>
                      <th style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '14%', textAlign: 'center' }}></th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtrados.map(art => (
                      <tr key={art.id}
                        style={{ transition: 'background 0.2s' }}
                        onMouseEnter={e => e.currentTarget.style.background = '#f8f9fa'}
                        onMouseLeave={e => e.currentTarget.style.background = ''}
                      >
                        <td className="hide-on-mobile" style={{ verticalAlign: 'middle', padding: '0.75rem', textAlign: 'center' }}>
                          <img src="/example.png" alt={art.codigo} style={{ width: '50px', height: '50px', objectFit: 'contain', borderRadius: '6px' }} />
                        </td>
                        <td style={{ verticalAlign: 'middle', padding: '0.75rem', fontWeight: 600 }}>{art.codigo}</td>
                        <td style={{ verticalAlign: 'middle', padding: '0.75rem' }}>{art.descripcion}</td>
                        <td className="hide-on-mobile" style={{ verticalAlign: 'middle', padding: '0.75rem', color: '#198754', fontWeight: 600 }}>{formatMoney(art.precio)}</td>
                        <td style={{ verticalAlign: 'middle', padding: '0.75rem' }}>
                          <div className="d-flex align-items-center justify-content-center gap-2">
                            <button
                              className="btn btn-sm btn-outline-dark"
                              style={{ width: '32px', height: '32px', borderRadius: '8px', padding: 0, fontWeight: 700, fontSize: '1.1rem' }}
                              onClick={() => restar(art.id)}
                            >-</button>
                            <span style={{ minWidth: '30px', textAlign: 'center', fontWeight: 600, fontSize: '1.05rem' }}>
                              {contadores[art.id] || 0}
                            </span>
                            <button
                              className="btn btn-sm btn-outline-dark"
                              style={{ width: '32px', height: '32px', borderRadius: '8px', padding: 0, fontWeight: 700, fontSize: '1.1rem' }}
                              onClick={() => sumar(art.id)}
                            >+</button>
                          </div>
                        </td>
                        <td style={{ verticalAlign: 'middle', padding: '0.75rem', textAlign: 'center' }}>
                          <button
                            className="btn btn-sm"
                            style={{
                              background: (contadores[art.id] || 0) > 0
                                ? 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)'
                                : '#e9ecef',
                              color: (contadores[art.id] || 0) > 0 ? 'white' : '#6c757d',
                              borderRadius: '8px', border: 'none', padding: '0.4rem 1.25rem', fontWeight: 500,
                              transition: 'all 0.2s',
                            }}
                            onClick={() => agregarItem(art)}
                            disabled={(contadores[art.id] || 0) === 0}
                          >
                            <i className="bi bi-plus-circle me-1"></i>Agregar
                          </button>
                        </td>
                      </tr>
                    ))}
                    {filtrados.length === 0 && !loading && (
                      <tr>
                        <td colSpan={6} className="text-center py-4" style={{ color: '#6c757d' }}>
                          <i className="bi bi-inbox" style={{ fontSize: '2rem', display: 'block', marginBottom: '0.5rem' }}></i>
                          No se encontraron articulos en esta categoria
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Modal Pedido */}
      <div className="modal fade" id="pedidoModal" tabIndex={-1} ref={modalRef}>
        <div className="modal-dialog modal-dialog-scrollable modal-xl">
          <div className="modal-content" style={{ borderRadius: '12px', overflow: 'hidden' }}>
            <div className="modal-header" style={{ background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)', color: 'white' }}>
              <h5 className="modal-title"><i className="bi bi-basket me-2"></i>Su Pedido</h5>
              <button type="button" className="btn-close btn-close-white" data-bs-dismiss="modal"></button>
            </div>
            <div className="modal-body" style={{ maxHeight: '500px', overflowY: 'auto' }}>
              {items.length === 0 ? (
                <div className="text-center py-5" style={{ color: '#6c757d' }}>
                  <i className="bi bi-basket" style={{ fontSize: '3rem', display: 'block', marginBottom: '0.75rem' }}></i>
                  <p>No hay articulos en el pedido</p>
                </div>
              ) : (
                <>
                  <table className="table table-hover">
                    <thead>
                      <tr>
                        <th style={{ fontWeight: 600, color: '#495057', backgroundColor: '#f8f9fa' }}>Codigo</th>
                        <th style={{ fontWeight: 600, color: '#495057', backgroundColor: '#f8f9fa' }}>Descripcion</th>
                        <th style={{ fontWeight: 600, color: '#495057', backgroundColor: '#f8f9fa', textAlign: 'center' }}>Cantidad</th>
                        <th style={{ fontWeight: 600, color: '#495057', backgroundColor: '#f8f9fa' }}>Precio</th>
                        <th style={{ fontWeight: 600, color: '#495057', backgroundColor: '#f8f9fa' }}>Importe</th>
                        <th style={{ fontWeight: 600, color: '#495057', backgroundColor: '#f8f9fa' }}></th>
                      </tr>
                    </thead>
                    <tbody>
                      {items.map(item => (
                        <tr key={item.id}>
                          <td style={{ verticalAlign: 'middle', fontWeight: 600 }}>{item.codigo}</td>
                          <td style={{ verticalAlign: 'middle' }}>{item.descripcion}</td>
                          <td style={{ verticalAlign: 'middle' }}>
                            <div className="d-flex align-items-center justify-content-center gap-2">
                              <button className="btn btn-sm btn-outline-dark" style={{ width: '28px', height: '28px', borderRadius: '6px', padding: 0, fontWeight: 700 }}
                                onClick={() => disminuirCantidadCarrito(item.id)}>-</button>
                              <span style={{ minWidth: '25px', textAlign: 'center', fontWeight: 600 }}>{item.cantidad}</span>
                              <button className="btn btn-sm btn-outline-dark" style={{ width: '28px', height: '28px', borderRadius: '6px', padding: 0, fontWeight: 700 }}
                                onClick={() => aumentarCantidadCarrito(item.id)}>+</button>
                            </div>
                          </td>
                          <td style={{ verticalAlign: 'middle' }}>{formatMoney(item.precio)}</td>
                          <td style={{ verticalAlign: 'middle', fontWeight: 600 }}>{formatMoney(item.importe)}</td>
                          <td style={{ verticalAlign: 'middle', textAlign: 'center' }}>
                            <button className="btn btn-sm btn-outline-danger" style={{ borderRadius: '6px' }}
                              onClick={() => eliminarItem(item.id)}>
                              <i className="bi bi-trash"></i>
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>

                  {/* Totales */}
                  <div className="d-flex justify-content-end">
                    <div style={{ background: '#f8f9fa', borderRadius: '10px', padding: '1.25rem', minWidth: '300px' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', padding: '0.5rem 0', borderBottom: '1px solid #e9ecef' }}>
                        <span>Subtotal:</span>
                        <strong>{formatMoney(total)}</strong>
                      </div>
                      <div style={{ display: 'flex', justifyContent: 'space-between', padding: '0.5rem 0', borderBottom: '1px solid #e9ecef' }}>
                        <span>IVA (21%):</span>
                        <strong>{formatMoney(iva)}</strong>
                      </div>
                      <div style={{
                        display: 'flex', justifyContent: 'space-between', padding: '0.75rem 0 0.25rem',
                        fontSize: '1.2rem', fontWeight: 700, color: '#212529',
                        borderTop: '2px solid #212529', marginTop: '0.5rem',
                      }}>
                        <span>TOTAL:</span>
                        <strong>{formatMoney(totalFinal)}</strong>
                      </div>
                    </div>
                  </div>
                </>
              )}
            </div>
            <div className="modal-footer">
              <button type="button" className="btn btn-outline-danger me-auto" onClick={limpiarPedido} disabled={items.length === 0}>
                <i className="bi bi-trash me-1"></i>Limpiar
              </button>
              <button type="button" className="btn btn-outline-secondary" data-bs-dismiss="modal">
                Seguir Agregando
              </button>
              <button type="button" className="btn" data-bs-dismiss="modal" disabled={items.length === 0}
                style={{
                  background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)',
                  color: 'white', borderRadius: '8px', border: 'none', fontWeight: 500,
                }}>
                <i className="bi bi-check-circle me-1"></i>Guardar Pedido
              </button>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
