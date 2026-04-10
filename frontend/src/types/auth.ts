export interface LoginRequest {
  usuario: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  nombre: string;
  mail: string;
  usuario: string;
}

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
}

export interface User {
  id: number;
  nombre: string;
  mail: string;
  usuario: string;
  activo: boolean;
  fechaCreacion: string;
}
