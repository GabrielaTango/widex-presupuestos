# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Widex Presupuestos is a fullstack budgets and orders management system for Widex Argentina. It consists of a .NET 8 REST API backend with SQL Server, and a React + TypeScript frontend.

## Commands

### Backend (.NET 8)
```bash
dotnet restore backend/
dotnet build backend/
dotnet run --project backend/ --launch-profile http   # http://localhost:5062
```

### Frontend (React + Vite)
```bash
cd frontend && npm install
npm run dev      # http://localhost:5173
npm run build    # Production build → dist/
npm run lint     # ESLint
```

### Database
SQL scripts in `database/` must be run in order: `01_create_database.sql` → `02_create_tables.sql` → `03_seed_data.sql`.

## Architecture

### Backend
- **Pattern**: Controllers → Services → Repositories (DI-wired)
- **ORM**: Dapper (raw SQL, not EF Core)
- **Auth**: JWT Bearer (HS256, 8h expiry), passwords hashed with BCrypt
- **Two databases**: `WidexPresupuestos` (app data) and `WIDEX_ARGENTINA_SA` (legacy Tango product catalog via `TangoConnection`)
- **Standardized response**: All endpoints return `ApiResponse<T>` with `success`, `message`, `data` fields
- **CORS**: Configured for `http://localhost:5173`

### Frontend
- **Stack**: React 19, TypeScript, Vite, Bootstrap 5, Axios
- **Auth flow**: AuthContext stores JWT + user in localStorage; ProtectedRoute guards pages; Axios interceptor injects Bearer token and auto-logouts on 401
- **Routing**: React Router DOM — `/login` (public), `/` dashboard, `/presupuestos`, `/pedidos` (protected)
- **API base URL**: Hardcoded in `src/services/api.ts` as `http://localhost:5062/api`

### Key Patterns
- Product catalog queries hit legacy Tango tables (`STA11`, `STA11FLD`, `STA19`, `GVA17`) with hierarchical folder/category structure
- Admin seed endpoint: `POST /api/auth/seed` creates default user `admin`/`Admin123!`
