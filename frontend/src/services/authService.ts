import api from './api';
import type { LoginRequest, LoginResponse, ApiResponse } from '../types/auth';

export const authService = {
  login: async (data: LoginRequest): Promise<ApiResponse<LoginResponse>> => {
    const response = await api.post<ApiResponse<LoginResponse>>('/auth/login', data);
    return response.data;
  },

  logout: () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
  },

  isAuthenticated: (): boolean => {
    return !!localStorage.getItem('token');
  },

  getUser: (): LoginResponse | null => {
    const user = localStorage.getItem('user');
    return user ? JSON.parse(user) : null;
  },
};
