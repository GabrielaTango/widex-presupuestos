import { useState, useCallback, useEffect } from 'react';
import Select from 'react-select';
import AsyncSelect from 'react-select/async';
import api from '../services/api';

interface Item {
  id: number;
  codigo: string;
  descripcion: string;
  cantidad: number;
  precioUnitario: number;
  importe: number;
  cobertura: number;
  coberturaAplicable: boolean;
  codArticuDif: string;
}

interface ArticuloOption {
  value: string;
  label: string;
  descripcio: string;
  descAdic: string;
  codBarra: string;
  coberturaAplicable: boolean;
  codArticuDif: string;
}

interface ClienteOption {
  value: string;
  label: string;
  razonSoci: string;
  cuit: string;
  nroCarnet?: string;
  obraSocial?: string;
}

interface ObraSocialOption {
  value: string;
  label: string;
  cuit: string;
}

interface TalonarioOption {
  value: string;
  label: string;
  sucursal: string;
}

interface VendedorOption {
  value: string;
  label: string;
  porcComis: number;
}

const selectStyles = {
  control: (base: any) => ({
    ...base,
    borderRadius: '8px',
    borderColor: '#dee2e6',
    minHeight: '38px',
    '&:hover': { borderColor: '#86b7fe' },
  }),
  option: (base: any, state: any) => ({
    ...base,
    backgroundColor: state.isSelected ? '#212529' : state.isFocused ? '#f8f9fa' : 'white',
    color: state.isSelected ? 'white' : '#212529',
    fontSize: '0.9rem',
  }),
  placeholder: (base: any) => ({ ...base, fontSize: '0.9rem', color: '#6c757d' }),
  singleValue: (base: any) => ({ ...base, fontSize: '0.9rem' }),
};

export default function Presupuesto() {
  const [items, setItems] = useState<Item[]>([
    { id: 1, codigo: '', descripcion: '', cantidad: 1, precioUnitario: 0, importe: 0, cobertura: 0, coberturaAplicable: false, codArticuDif: '' },
  ]);
  const [nextId, setNextId] = useState(2);
  const [tipoCoberturaGeneral, setTipoCoberturaGeneral] = useState<'%' | '$'>('%');
  const [coberturaValor, setCoberturaValor] = useState(0);
  const [coberturaPorItem, setCoberturaPorItem] = useState(false);
  const [certificadoDiscapacidad, setCertificadoDiscapacidad] = useState(false);
  const [tipoDescuento, setTipoDescuento] = useState<'%' | '$'>('%');
  const [descuentoValor, setDescuentoValor] = useState(0);
  const [leyendas, setLeyendas] = useState(['', '', '', '', '']);

  // Select options
  const [obrasSocialesOptions, setObrasSocialesOptions] = useState<ObraSocialOption[]>([]);
  const [selectedPaciente, setSelectedPaciente] = useState<ClienteOption | null>(null);
  const [selectedRazonSocial, setSelectedRazonSocial] = useState<ClienteOption | null>(null);
  const [selectedObraSocial, setSelectedObraSocial] = useState<ObraSocialOption | null>(null);
  const [nroObraSocial, setNroObraSocial] = useState('');
  const [articulosOptions, setArticulosOptions] = useState<ArticuloOption[]>([]);
  const [talonariosOptions, setTalonariosOptions] = useState<TalonarioOption[]>([]);
  const [selectedTalonario, setSelectedTalonario] = useState<TalonarioOption | null>(null);
  const [vendedoresSeleccionOptions, setVendedoresSeleccionOptions] = useState<VendedorOption[]>([]);
  const [vendedoresNoSeleccionOptions, setVendedoresNoSeleccionOptions] = useState<VendedorOption[]>([]);
  const [selectedVendedoresSeleccion, setSelectedVendedoresSeleccion] = useState<VendedorOption[]>([]);
  const [selectedVendedoresNoSeleccion, setSelectedVendedoresNoSeleccion] = useState<VendedorOption[]>([]);

  const buscarPacientes = useCallback(async (inputValue: string): Promise<ClienteOption[]> => {
    try {
      const res = await api.get('/clientes/pacientes', { params: { busqueda: inputValue || undefined } });
      return res.data.data.map((p: any) => ({
        value: p.codClient,
        label: `${p.codClient} - ${p.razonSoci}`,
        razonSoci: p.razonSoci,
        cuit: p.cuit,
        nroCarnet: p.nroCarnet,
        obraSocial: p.obraSocial,
      }));
    } catch {
      return [];
    }
  }, []);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [obrasRes, articulosRes, talonariosRes, vendSeleccionRes, vendNoSeleccionRes] = await Promise.all([
          api.get('/clientes/obras-sociales'),
          api.get('/articulos/presupuesto'),
          api.get('/articulos/talonarios'),
          api.get('/vendedores/seleccion'),
          api.get('/vendedores/no-seleccion'),
        ]);

        const obras = obrasRes.data.data.map((o: any) => ({
          value: o.codClient,
          label: `${o.codClient} - ${o.razonSoci}`,
          cuit: o.cuit,
        }));
        setObrasSocialesOptions(obras);

        const articulos = articulosRes.data.data.map((a: any) => ({
          value: a.codArticu,
          label: `${a.codArticu.trim()} - ${a.descripcio.trim()} ${a.descAdic?.trim() || ''}`.trim(),
          descripcio: a.descripcio,
          descAdic: a.descAdic,
          codBarra: a.codBarra,
          coberturaAplicable: a.coberturaAplicable,
          codArticuDif: a.codArticuDif,
        }));
        setArticulosOptions(articulos);

        const talonarios = talonariosRes.data.data.map((t: any) => ({
          value: t.talonarioId,
          label: t.descrip.trim(),
          sucursal: t.sucursal,
        }));
        setTalonariosOptions(talonarios);

        const mapVendedores = (data: any[]) => data.map((v: any) => ({
          value: v.codVended,
          label: `${v.codVended.trim()} - ${v.nombreVen.trim()}`,
          porcComis: v.porcComis,
        }));
        setVendedoresSeleccionOptions(mapVendedores(vendSeleccionRes.data.data));
        setVendedoresNoSeleccionOptions(mapVendedores(vendNoSeleccionRes.data.data));
      } catch (err) {
        console.error('Error cargando datos de clientes:', err);
      }
    };
    fetchData();
  }, []);

  const handlePacienteChange = (option: ClienteOption | null) => {
    setSelectedPaciente(option);
    if (option) {
      // If patient has obra social and nro carnet, fill them
      if (option.obraSocial) {
        const obraMatch = obrasSocialesOptions.find(
          o => o.value === option.obraSocial || o.label.includes(option.obraSocial!)
        );
        if (obraMatch) {
          setSelectedObraSocial(obraMatch);
        } else if (option.obraSocial.trim()) {
          // Set as a custom value if not found in the list
          setSelectedObraSocial({
            value: option.obraSocial,
            label: option.obraSocial,
            cuit: '',
          });
        }
      }
      if (option.nroCarnet) {
        setNroObraSocial(option.nroCarnet);
      }
    } else {
      setSelectedObraSocial(null);
      setNroObraSocial('');
    }
  };

  const handleRazonSocialChange = (option: ClienteOption | null) => {
    setSelectedRazonSocial(option);
  };

  // Calcular cobertura según modo
  const calcularCobertura = useCallback((allItems: Item[]): Item[] => {
    // Certificado de discapacidad: 100% sobre todos los items
    if (certificadoDiscapacidad) {
      return allItems.map(item => ({ ...item, cobertura: item.importe }));
    }
    if (coberturaPorItem) {
      // Cada item aplicable recibe el mismo valor de cobertura
      return allItems.map(item => {
        if (!item.coberturaAplicable) return { ...item, cobertura: 0 };
        const cob = tipoCoberturaGeneral === '%'
          ? item.importe * coberturaValor / 100
          : coberturaValor;
        return { ...item, cobertura: Math.round(Math.min(cob, item.importe) * 100) / 100 };
      });
    }
    // General
    if (tipoCoberturaGeneral === '%') {
      return allItems.map(item => {
        if (!item.coberturaAplicable) return { ...item, cobertura: 0 };
        const cob = item.importe * coberturaValor / 100;
        return { ...item, cobertura: Math.round(Math.min(cob, item.importe) * 100) / 100 };
      });
    }
    // $: descontar item a item hasta agotar el monto
    let restante = coberturaValor;
    return allItems.map(item => {
      if (!item.coberturaAplicable || restante <= 0) return { ...item, cobertura: 0 };
      const cob = Math.min(item.importe, restante);
      restante -= cob;
      return { ...item, cobertura: Math.round(cob * 100) / 100 };
    });
  }, [tipoCoberturaGeneral, coberturaValor, coberturaPorItem, certificadoDiscapacidad]);

  const handleArticuloChange = (itemId: number, option: ArticuloOption | null) => {
    setItems(prev => {
      const updated = prev.map(item => {
        if (item.id !== itemId) return item;
        if (option) {
          return {
            ...item,
            codigo: option.value.trim(),
            descripcion: `${option.descripcio.trim()} ${option.descAdic?.trim() || ''}`.trim(),
            coberturaAplicable: option.coberturaAplicable,
            codArticuDif: option.codArticuDif,
          };
        }
        return { ...item, codigo: '', descripcion: '', coberturaAplicable: false, codArticuDif: '', cobertura: 0 };
      });
      return calcularCobertura(updated);
    });
  };

  const updateItem = useCallback((id: number, field: keyof Item, value: string | number) => {
    setItems(prev => {
      const updated = prev.map(item => {
        if (item.id !== id) return item;
        const u = { ...item, [field]: value };
        if (field === 'cantidad' || field === 'precioUnitario') {
          u.importe = u.cantidad * u.precioUnitario;
        }
        return u;
      });
      if (field === 'cantidad' || field === 'precioUnitario') {
        return calcularCobertura(updated);
      }
      return updated;
    });
  }, [calcularCobertura]);

  const addItem = () => {
    setItems(prev => [...prev, { id: nextId, codigo: '', descripcion: '', cantidad: 1, precioUnitario: 0, importe: 0, cobertura: 0, coberturaAplicable: false, codArticuDif: '' }]);
    setNextId(prev => prev + 1);
  };

  const removeItem = (id: number) => {
    setItems(prev => {
      const updated = prev.filter(item => item.id !== id);
      return calcularCobertura(updated);
    });
  };

  const updateLeyenda = (index: number, value: string) => {
    setLeyendas(prev => prev.map((l, i) => i === index ? value : l));
  };

  // Recalcular cobertura cuando cambian los parámetros
  useEffect(() => {
    setItems(prev => calcularCobertura(prev));
  }, [calcularCobertura]);

  // Calculos
  const subtotal = items.reduce((sum, item) => sum + item.importe, 0);
  const coberturaTotal = items.reduce((sum, item) => sum + item.cobertura, 0);
  const descuento = tipoDescuento === '%' ? (subtotal * descuentoValor / 100) : descuentoValor;
  const baseImponible = subtotal - coberturaTotal - descuento;
  const iva = baseImponible * 0.21;
  const total = baseImponible + iva;

  const formatMoney = (n: number) => `$ ${n.toFixed(2)}`;

  const today = new Date().toISOString().split('T')[0];

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
              <i className="bi bi-file-earmark-medical me-2"></i>Nuevo Presupuesto
            </h4>
            <small style={{ opacity: 0.75 }}>Carga de Presupuesto</small>
          </div>
        </div>

        {/* Body */}
        <div style={{ padding: '1.75rem' }}>
          {/* Datos Principales */}
          <div className="row">
            {/* Paciente / Cliente */}
            <div className="col-lg-8">
              <div style={{ background: '#f8f9fa', borderRadius: '10px', padding: '1.25rem', marginBottom: '1.5rem', height: 'calc(100% - 1.5rem)' }}>
                <h6 style={{ color: '#212529', fontWeight: 600, borderBottom: '2px solid #e9ecef', paddingBottom: '0.5rem', marginBottom: '1rem' }}>
                  <i className="bi bi-person-fill me-2"></i>Datos del Paciente / Cliente
                </h6>
                <div className="mb-3">
                  <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Paciente</label>
                  <AsyncSelect<ClienteOption>
                    loadOptions={buscarPacientes}
                    defaultOptions
                    value={selectedPaciente}
                    onChange={handlePacienteChange}
                    placeholder="Buscar paciente..."
                    isClearable
                    isSearchable
                    noOptionsMessage={() => 'No se encontraron pacientes'}
                    loadingMessage={() => 'Buscando...'}
                    styles={selectStyles}
                  />
                </div>
                <div className="mb-3">
                  <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Razon Social (Cliente)</label>
                  <AsyncSelect<ClienteOption>
                    loadOptions={buscarPacientes}
                    defaultOptions
                    value={selectedRazonSocial}
                    onChange={handleRazonSocialChange}
                    placeholder="Buscar razon social..."
                    isClearable
                    isSearchable
                    noOptionsMessage={() => 'No se encontraron clientes'}
                    loadingMessage={() => 'Buscando...'}
                    styles={selectStyles}
                  />
                </div>
                <div className="row">
                  <div className="col-md-7 mb-3">
                    <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Obra Social</label>
                    <Select<ObraSocialOption>
                      options={obrasSocialesOptions}
                      value={selectedObraSocial}
                      onChange={(option) => setSelectedObraSocial(option)}
                      placeholder="Buscar obra social..."
                      isClearable
                      isSearchable
                      noOptionsMessage={() => 'No se encontraron obras sociales'}
                      styles={selectStyles}
                    />
                  </div>
                  <div className="col-md-5 mb-3">
                    <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>N Obra Social</label>
                    <input
                      type="text"
                      className="form-control"
                      placeholder="N afiliado"
                      value={nroObraSocial}
                      onChange={e => setNroObraSocial(e.target.value)}
                      style={{ borderRadius: '8px' }}
                    />
                  </div>
                </div>
              </div>
            </div>

            {/* Nro Presupuesto y Fecha */}
            <div className="col-lg-4">
              <div style={{ background: '#f8f9fa', borderRadius: '10px', padding: '1.25rem', marginBottom: '1.5rem', height: 'calc(100% - 1.5rem)' }}>
                <h6 style={{ color: '#212529', fontWeight: 600, borderBottom: '2px solid #e9ecef', paddingBottom: '0.5rem', marginBottom: '1rem' }}>
                  <i className="bi bi-hash me-2"></i>Datos del Presupuesto
                </h6>
                <div className="mb-3">
                  <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Talonario</label>
                  <Select<TalonarioOption>
                    options={talonariosOptions}
                    value={selectedTalonario}
                    onChange={(option) => setSelectedTalonario(option)}
                    placeholder="Seleccionar talonario..."
                    isClearable
                    isSearchable
                    noOptionsMessage={() => 'No se encontraron talonarios'}
                    styles={selectStyles}
                  />
                </div>
                <div className="mb-3">
                  <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Nro. Presupuesto</label>
                  <input type="text" className="form-control" readOnly
                    value={`${selectedTalonario ? selectedTalonario.sucursal.trim().padStart(4, '0') : '0000'}-00000125`}
                    style={{ borderRadius: '8px', background: '#e9ecef', fontWeight: 600 }} />
                </div>
                <div className="mb-3">
                  <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Fecha</label>
                  <input type="date" className="form-control" defaultValue={today}
                    style={{ borderRadius: '8px' }} />
                </div>
              </div>
            </div>
          </div>

          {/* Vendedores */}
          <div style={{ background: '#f8f9fa', borderRadius: '10px', padding: '1.25rem', marginBottom: '1.5rem' }}>
            <h6 style={{ color: '#212529', fontWeight: 600, borderBottom: '2px solid #e9ecef', paddingBottom: '0.5rem', marginBottom: '1rem' }}>
              <i className="bi bi-people-fill me-2"></i>Vendedores
            </h6>
            <div className="row">
              {/* Vendedor Selección */}
              <div className="col-md-6 mb-3">
                <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Seleccion Widex</label>
                <Select<VendedorOption>
                  options={vendedoresSeleccionOptions.filter(o => !selectedVendedoresSeleccion.some(s => s.value === o.value))}
                  value={null}
                  onChange={(option) => {
                    if (option) setSelectedVendedoresSeleccion(prev => [...prev, option]);
                  }}
                  placeholder="Agregar vendedor..."
                  isSearchable
                  noOptionsMessage={() => 'No se encontraron vendedores'}
                  styles={selectStyles}
                />
                {selectedVendedoresSeleccion.length > 0 && (
                  <table className="table table-sm mt-2 mb-0" style={{ fontSize: '0.85rem' }}>
                    <thead>
                      <tr style={{ background: '#e9ecef' }}>
                        <th style={{ fontWeight: 500, padding: '0.4rem 0.6rem' }}>Codigo</th>
                        <th style={{ fontWeight: 500, padding: '0.4rem 0.6rem' }}>Nombre</th>
                        <th style={{ fontWeight: 500, padding: '0.4rem 0.6rem', textAlign: 'right' }}>% Comis.</th>
                        <th style={{ fontWeight: 500, padding: '0.4rem 0.6rem', width: '30px' }}></th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedVendedoresSeleccion.map(v => (
                        <tr key={v.value}>
                          <td style={{ padding: '0.4rem 0.6rem' }}>{v.value.trim()}</td>
                          <td style={{ padding: '0.4rem 0.6rem' }}>{v.label.split(' - ').slice(1).join(' - ')}</td>
                          <td style={{ padding: '0.4rem 0.6rem', textAlign: 'right' }}>{v.porcComis}%</td>
                          <td style={{ padding: '0.4rem 0.6rem', textAlign: 'center' }}>
                            <button type="button" onClick={() => setSelectedVendedoresSeleccion(prev => prev.filter(x => x.value !== v.value))}
                              style={{ color: '#dc3545', background: 'none', border: 'none', cursor: 'pointer', padding: 0 }}>
                              <i className="bi bi-x-circle"></i>
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>

              {/* Vendedores */}
              <div className="col-md-6 mb-3">
                <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Vendedores</label>
                <Select<VendedorOption>
                  options={vendedoresNoSeleccionOptions.filter(o => !selectedVendedoresNoSeleccion.some(s => s.value === o.value))}
                  value={null}
                  onChange={(option) => {
                    if (option) setSelectedVendedoresNoSeleccion(prev => [...prev, option]);
                  }}
                  placeholder="Agregar vendedor..."
                  isSearchable
                  noOptionsMessage={() => 'No se encontraron vendedores'}
                  styles={selectStyles}
                />
                {selectedVendedoresNoSeleccion.length > 0 && (
                  <table className="table table-sm mt-2 mb-0" style={{ fontSize: '0.85rem' }}>
                    <thead>
                      <tr style={{ background: '#e9ecef' }}>
                        <th style={{ fontWeight: 500, padding: '0.4rem 0.6rem' }}>Codigo</th>
                        <th style={{ fontWeight: 500, padding: '0.4rem 0.6rem' }}>Nombre</th>
                        <th style={{ fontWeight: 500, padding: '0.4rem 0.6rem', textAlign: 'right' }}>% Comis.</th>
                        <th style={{ fontWeight: 500, padding: '0.4rem 0.6rem', width: '30px' }}></th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedVendedoresNoSeleccion.map(v => (
                        <tr key={v.value}>
                          <td style={{ padding: '0.4rem 0.6rem' }}>{v.value.trim()}</td>
                          <td style={{ padding: '0.4rem 0.6rem' }}>{v.label.split(' - ').slice(1).join(' - ')}</td>
                          <td style={{ padding: '0.4rem 0.6rem', textAlign: 'right' }}>{v.porcComis}%</td>
                          <td style={{ padding: '0.4rem 0.6rem', textAlign: 'center' }}>
                            <button type="button" onClick={() => setSelectedVendedoresNoSeleccion(prev => prev.filter(x => x.value !== v.value))}
                              style={{ color: '#dc3545', background: 'none', border: 'none', cursor: 'pointer', padding: 0 }}>
                              <i className="bi bi-x-circle"></i>
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>
          </div>

          {/* Documentos Adjuntos */}
          <div style={{ background: '#f8f9fa', borderRadius: '10px', padding: '1.25rem', marginBottom: '1.5rem' }}>
            <h6 style={{ color: '#212529', fontWeight: 600, borderBottom: '2px solid #e9ecef', paddingBottom: '0.5rem', marginBottom: '1rem' }}>
              <i className="bi bi-paperclip me-2"></i>Documentos Adjuntos
            </h6>
            <div className="row">
              {['Orden de Compra', 'Certificado Discapacidad', 'Orden Medica'].map((doc, i) => (
                <div className="col-md-4 mb-3" key={i}>
                  <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>{doc}</label>
                  <div
                    style={{
                      border: `2px dashed ${doc === 'Certificado Discapacidad' && certificadoDiscapacidad ? '#198754' : '#ced4da'}`,
                      borderRadius: '10px', padding: '1rem',
                      textAlign: 'center', cursor: 'pointer',
                      background: doc === 'Certificado Discapacidad' && certificadoDiscapacidad ? '#d1e7dd' : '#fff',
                      transition: 'all 0.3s',
                    }}
                    onClick={(e) => {
                      const input = (e.currentTarget as HTMLElement).querySelector('input[type="file"]') as HTMLInputElement;
                      input?.click();
                    }}
                    onMouseEnter={(e) => { e.currentTarget.style.borderColor = '#6c757d'; e.currentTarget.style.background = '#f8f9fa'; }}
                    onMouseLeave={(e) => {
                      const isCert = doc === 'Certificado Discapacidad' && certificadoDiscapacidad;
                      e.currentTarget.style.borderColor = isCert ? '#198754' : '#ced4da';
                      e.currentTarget.style.background = isCert ? '#d1e7dd' : '#fff';
                    }}
                  >
                    {doc === 'Certificado Discapacidad' && certificadoDiscapacidad
                      ? <i className="bi bi-check-circle-fill" style={{ fontSize: '1.5rem', color: '#198754' }}></i>
                      : <i className="bi bi-cloud-upload" style={{ fontSize: '1.5rem', color: '#495057' }}></i>
                    }
                    <p style={{ margin: 0, fontSize: '0.8rem', color: '#6c757d', marginTop: '0.25rem' }}>
                      {doc === 'Certificado Discapacidad' && certificadoDiscapacidad ? 'Archivo cargado' : 'Subir archivo'}
                    </p>
                    <input type="file" style={{ display: 'none' }}
                      onChange={doc === 'Certificado Discapacidad' ? (e) => {
                        setCertificadoDiscapacidad(!!e.target.files?.length);
                      } : undefined}
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Cobertura y Descuento */}
          <div className="row">
            <div className="col-md-6">
              <div style={{ background: '#f8f9fa', borderRadius: '10px', padding: '1.25rem', marginBottom: '1.5rem' }}>
                <h6 style={{ color: '#212529', fontWeight: 600, borderBottom: '2px solid #e9ecef', paddingBottom: '0.5rem', marginBottom: '1rem' }}>
                  <i className="bi bi-shield-check me-2"></i>Cobertura
                </h6>
                {certificadoDiscapacidad && (
                  <div className="alert alert-success py-2 mb-3" style={{ fontSize: '0.85rem' }}>
                    <i className="bi bi-shield-fill-check me-1"></i>
                    Cobertura total 100% por Certificado de Discapacidad
                  </div>
                )}
                {!certificadoDiscapacidad && (
                <>
                <div className="form-check mb-3">
                  <input
                    className="form-check-input"
                    type="checkbox"
                    id="coberturaPorItem"
                    checked={coberturaPorItem}
                    onChange={e => setCoberturaPorItem(e.target.checked)}
                  />
                  <label className="form-check-label" htmlFor="coberturaPorItem" style={{ fontSize: '0.9rem', color: '#495057' }}>
                    Cobertura por cada item
                  </label>
                </div>
                <div className="row align-items-end">
                  <div className="col-md-6 mb-3">
                    <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Tipo de Cobertura</label>
                    <div className="btn-group w-100" role="group">
                      <button
                        type="button"
                        className={`btn ${tipoCoberturaGeneral === '%' ? 'btn-dark' : 'btn-outline-secondary'}`}
                        onClick={() => setTipoCoberturaGeneral('%')}
                      >%</button>
                      <button
                        type="button"
                        className={`btn ${tipoCoberturaGeneral === '$' ? 'btn-dark' : 'btn-outline-secondary'}`}
                        onClick={() => setTipoCoberturaGeneral('$')}
                      >$</button>
                    </div>
                  </div>
                  <div className="col-md-6 mb-3">
                    <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Valor Cobertura</label>
                    <div className="input-group">
                      <span className="input-group-text">{tipoCoberturaGeneral}</span>
                      <input type="number" className="form-control" placeholder="0.00" value={coberturaValor || ''} onChange={e => setCoberturaValor(parseFloat(e.target.value) || 0)} style={{ borderRadius: '0 8px 8px 0' }} />
                    </div>
                  </div>
                </div>
                </>
                )}
              </div>
            </div>
            <div className="col-md-6">
              <div style={{ background: '#f8f9fa', borderRadius: '10px', padding: '1.25rem', marginBottom: '1.5rem' }}>
                <h6 style={{ color: '#212529', fontWeight: 600, borderBottom: '2px solid #e9ecef', paddingBottom: '0.5rem', marginBottom: '1rem' }}>
                  <i className="bi bi-tag me-2"></i>Descuento
                </h6>
                <div className="row align-items-end">
                  <div className="col-md-6 mb-3">
                    <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Tipo de Descuento</label>
                    <div className="btn-group w-100" role="group">
                      <button
                        type="button"
                        className={`btn ${tipoDescuento === '%' ? 'btn-dark' : 'btn-outline-secondary'}`}
                        onClick={() => setTipoDescuento('%')}
                      >%</button>
                      <button
                        type="button"
                        className={`btn ${tipoDescuento === '$' ? 'btn-dark' : 'btn-outline-secondary'}`}
                        onClick={() => setTipoDescuento('$')}
                      >$</button>
                    </div>
                  </div>
                  <div className="col-md-6 mb-3">
                    <label className="form-label" style={{ fontWeight: 500, color: '#495057', fontSize: '0.9rem' }}>Valor Descuento</label>
                    <div className="input-group">
                      <span className="input-group-text">{tipoDescuento}</span>
                      <input type="number" className="form-control" placeholder="0.00" value={descuentoValor || ''} onChange={e => setDescuentoValor(parseFloat(e.target.value) || 0)} style={{ borderRadius: '0 8px 8px 0' }} />
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Tabla de Items */}
          <div>
            <h6 style={{ color: '#212529', fontWeight: 600, borderBottom: '2px solid #e9ecef', paddingBottom: '0.5rem', marginBottom: '1rem' }}>
              <i className="bi bi-list-check me-2"></i>Detalle de Items
            </h6>
            <div className="table-responsive">
              <table className="table mb-0" style={{ borderRadius: '10px', overflow: 'hidden' }}>
                <thead>
                  <tr style={{ background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)', color: 'white' }}>
                    <th style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '5%' }}>#</th>
                    <th style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '37%' }}>Articulo</th>
                    <th style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '8%' }}>Cant.</th>
                    <th style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '13%' }}>P. Unitario</th>
                    <th style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '13%' }}>Importe</th>
                    <th style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '15%' }}>Cobertura</th>
                    <th style={{ fontWeight: 500, padding: '0.875rem 0.75rem', border: 'none', width: '5%' }}></th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item, index) => (
                    <tr key={item.id} style={{ transition: 'background 0.2s' }}
                      onMouseEnter={(e) => e.currentTarget.style.background = '#f8f9fa'}
                      onMouseLeave={(e) => e.currentTarget.style.background = ''}
                    >
                      <td style={{ verticalAlign: 'middle', textAlign: 'center', padding: '0.75rem' }}>{index + 1}</td>
                      <td style={{ padding: '0.75rem' }}>
                        <Select<ArticuloOption>
                          options={articulosOptions}
                          value={articulosOptions.find(a => a.value.trim() === item.codigo) || null}
                          onChange={(opt) => handleArticuloChange(item.id, opt)}
                          placeholder="Buscar articulo..."
                          isClearable
                          isSearchable
                          noOptionsMessage={() => 'No se encontraron articulos'}
                          menuPortalTarget={document.body}
                          menuPosition="fixed"
                          styles={{
                            ...selectStyles,
                            control: (base: any) => ({
                              ...base,
                              borderRadius: '6px',
                              borderColor: '#dee2e6',
                              minHeight: '31px',
                              fontSize: '0.875rem',
                              '&:hover': { borderColor: '#86b7fe' },
                            }),
                            singleValue: (base: any) => ({ ...base, fontSize: '0.875rem' }),
                            placeholder: (base: any) => ({ ...base, fontSize: '0.875rem', color: '#6c757d' }),
                            option: (base: any, state: any) => ({
                              ...base,
                              backgroundColor: state.isSelected ? '#212529' : state.isFocused ? '#f8f9fa' : 'white',
                              color: state.isSelected ? 'white' : '#212529',
                              fontSize: '0.85rem',
                              padding: '6px 10px',
                            }),
                            menuPortal: (base: any) => ({ ...base, zIndex: 9999 }),
                            dropdownIndicator: (base: any) => ({ ...base, padding: '2px 4px' }),
                            clearIndicator: (base: any) => ({ ...base, padding: '2px 4px' }),
                            valueContainer: (base: any) => ({ ...base, padding: '0 6px' }),
                          }}
                        />
                        {item.coberturaAplicable && (
                          <span style={{ fontSize: '0.7rem', color: '#198754', fontWeight: 500, marginTop: '2px', display: 'inline-block' }}>
                            <i className="bi bi-shield-check me-1"></i>Cobertura aplicable
                          </span>
                        )}
                      </td>
                      <td style={{ padding: '0.75rem' }}>
                        <input type="number" className="form-control form-control-sm text-center" min="1"
                          value={item.cantidad} onChange={e => updateItem(item.id, 'cantidad', parseInt(e.target.value) || 0)} style={{ borderRadius: '6px' }} />
                      </td>
                      <td style={{ padding: '0.75rem' }}>
                        <input type="number" className="form-control form-control-sm" placeholder="0.00" step="0.01"
                          value={item.precioUnitario || ''} onChange={e => updateItem(item.id, 'precioUnitario', parseFloat(e.target.value) || 0)} style={{ borderRadius: '6px' }} />
                      </td>
                      <td style={{ padding: '0.75rem' }}>
                        <input type="number" className="form-control form-control-sm" readOnly value={item.importe.toFixed(2)}
                          style={{ borderRadius: '6px', background: '#e9ecef' }} />
                      </td>
                      <td style={{ padding: '0.75rem' }}>
                        <input type="number" className="form-control form-control-sm" placeholder="0.00" step="0.01"
                          value={item.cobertura || ''}
                          onChange={e => updateItem(item.id, 'cobertura', parseFloat(e.target.value) || 0)}
                          readOnly={!coberturaPorItem}
                          style={{ borderRadius: '6px', background: !coberturaPorItem ? '#e9ecef' : undefined }} />
                      </td>
                      <td style={{ textAlign: 'center', verticalAlign: 'middle', padding: '0.75rem' }}>
                        <button type="button" onClick={() => removeItem(item.id)}
                          style={{ color: '#dc3545', background: 'none', border: 'none', fontSize: '1.1rem', cursor: 'pointer' }}>
                          <i className="bi bi-trash"></i>
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <button type="button" className="btn mt-3" onClick={addItem}
              style={{
                background: '#f8f9fa', color: '#212529', border: '2px dashed #212529',
                borderRadius: '8px', padding: '0.6rem 1.25rem', fontWeight: 500, transition: 'all 0.3s',
              }}
              onMouseEnter={(e) => { e.currentTarget.style.background = '#212529'; e.currentTarget.style.color = 'white'; }}
              onMouseLeave={(e) => { e.currentTarget.style.background = '#f8f9fa'; e.currentTarget.style.color = '#212529'; }}
            >
              <i className="bi bi-plus-circle me-2"></i>Agregar Item
            </button>
          </div>

          {/* Leyendas y Totales */}
          <div className="row mt-4">
            {/* Leyendas */}
            <div className="col-lg-7">
              <h6 style={{ color: '#212529', fontWeight: 600, borderBottom: '2px solid #e9ecef', paddingBottom: '0.5rem', marginBottom: '1rem' }}>
                <i className="bi bi-chat-left-text me-2"></i>Leyendas
              </h6>
              {leyendas.map((leyenda, i) => (
                <div className="mb-2" key={i}>
                  <div className="input-group">
                    <span className="input-group-text" style={{ background: '#f8f9fa' }}>{i + 1}</span>
                    <input type="text" className="form-control" placeholder={`Ingrese leyenda ${i + 1}...`}
                      value={leyenda} onChange={e => updateLeyenda(i, e.target.value)}
                      style={{ borderRadius: '0 8px 8px 0', border: '1px solid #e9ecef' }} />
                  </div>
                </div>
              ))}
            </div>

            {/* Totales */}
            <div className="col-lg-5">
              <h6 style={{ color: '#212529', fontWeight: 600, borderBottom: '2px solid #e9ecef', paddingBottom: '0.5rem', marginBottom: '1rem' }}>
                <i className="bi bi-calculator me-2"></i>Totales
              </h6>
              <div style={{ background: '#f8f9fa', borderRadius: '10px', padding: '1.25rem' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', padding: '0.5rem 0', borderBottom: '1px solid #e9ecef' }}>
                  <span>Subtotal:</span>
                  <strong>{formatMoney(subtotal)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', padding: '0.5rem 0', borderBottom: '1px solid #e9ecef' }}>
                  <span>Cobertura Total:</span>
                  <strong style={{ color: '#198754' }}>- {formatMoney(coberturaTotal)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', padding: '0.5rem 0', borderBottom: '1px solid #e9ecef' }}>
                  <span>Descuento:</span>
                  <strong style={{ color: '#dc3545' }}>- {formatMoney(descuento)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', padding: '0.5rem 0', borderBottom: '1px solid #e9ecef' }}>
                  <span>IVA (21%):</span>
                  <strong>{formatMoney(iva)}</strong>
                </div>
                <div style={{
                  display: 'flex', justifyContent: 'space-between', padding: '1rem 0 0.5rem',
                  fontSize: '1.3rem', fontWeight: 700, color: '#212529',
                  borderTop: '2px solid #212529', marginTop: '0.5rem',
                }}>
                  <span>TOTAL A PAGAR:</span>
                  <strong>{formatMoney(total)}</strong>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div style={{ background: '#f8f9fa', padding: '1.25rem 1.75rem', textAlign: 'right', borderTop: '1px solid #e9ecef' }}>
          <button type="button" className="btn btn-outline-secondary me-2" style={{ borderRadius: '8px', padding: '0.6rem 1.25rem', fontWeight: 500 }}>
            <i className="bi bi-printer me-1"></i> Imprimir
          </button>
          <button type="button" className="btn btn-outline-secondary me-2" style={{ borderRadius: '8px', padding: '0.6rem 1.25rem', fontWeight: 500 }}>
            <i className="bi bi-file-earmark-pdf me-1"></i> Exportar PDF
          </button>
          <button type="button" className="btn" style={{
            background: 'linear-gradient(135deg, #212529 0%, #343a40 50%, #495057 100%)', color: 'white', borderRadius: '8px',
            padding: '0.6rem 1.5rem', fontWeight: 500, border: 'none',
          }}>
            <i className="bi bi-check-circle me-1"></i> Guardar Presupuesto
          </button>
        </div>
      </div>
    </>
  );
}
