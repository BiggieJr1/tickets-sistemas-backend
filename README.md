# Centro de Tickets — Sistemas

## Estructura

```
tickets-sistemas/
├── TicketsSistemas.Api/     API en .NET 8 + EF Core + Postgres (Supabase)
├── frontend/index.html      Frontend estático (HTML/JS puro, sin build)
├── docker-compose.yml       Levanta la API en un contenedor
├── nginx-tickets.conf.example
└── README.md
```

## Desplegar en tu servidor Ubuntu

1. Copia la carpeta `tickets-sistemas/` completa a tu servidor (scp, git, rsync — el que ya usas).

2. Crea un proyecto en [supabase.com](https://supabase.com) (gratis) si no tienes uno,
   y copia la cadena de conexión de **Project Settings → Database → Connection string**
   (usa la variante "Transaction pooler" si vas a correr esto en un contenedor/servicio
   que se reinicia seguido). Ponla en un archivo `.env` junto al `docker-compose.yml`
   (ese archivo ya está en `.gitignore`, nunca se sube a git):
   ```bash
   echo 'SUPABASE_CONNECTION_STRING=Host=...;Database=postgres;Username=...;Password=...' > .env
   ```

3. Levanta la API:
   ```bash
   cd tickets-sistemas
   docker compose up -d --build
   ```
   Esto compila la imagen (necesita internet para restaurar los paquetes NuGet la primera vez)
   y deja la API escuchando en `127.0.0.1:5080` (solo accesible desde el propio servidor,
   nginx la expone hacia afuera). La primera vez que arranca crea las tablas en tu base
   de Supabase automáticamente (`db.Database.EnsureCreated()` en `Program.cs`).

4. Verifica que responde:
   ```bash
   curl http://127.0.0.1:5080/health
   # {"status":"ok"}
   ```

5. Copia el frontend a donde nginx sirve estáticos, por ejemplo:
   ```bash
   sudo mkdir -p /var/www/tickets-sistemas
   sudo cp -r frontend /var/www/tickets-sistemas/
   ```

6. Agrega el bloque de `nginx-tickets.conf.example` (ajustado) a tu `conf.d/`,
   y recarga nginx:
   ```bash
   sudo cp nginx-tickets.conf.example /etc/nginx/conf.d/tickets.conf
   sudo nginx -t && sudo systemctl reload nginx
   ```

7. Cualquiera en la red interna entra por `http://tickets.bisoft.local`
   (o la IP/dominio que hayas puesto en `server_name`) — sin cuenta de Claude,
   sin login, solo la URL.

## Notas para crecerlo después

- **Autenticación**: si más adelante quieres saber quién crea/cierra cada
  ticket, se puede agregar JWT como en BiSoft.Consultorio, o integrarlo con
  las cuentas de Windows/AD de Bisoft.
- **Historial de cambios**: ahora mismo un cambio de estado/prioridad
  sobreescribe el valor. Si quieres auditoría (quién cambió qué y cuándo),
  se agrega una tabla `TicketHistorial` que registre cada cambio.
- **Notificaciones**: correo o mensaje interno cuando entra un ticket
  Crítico, por ejemplo.
- **Angular**: este frontend es HTML/JS plano a propósito, para que sea
  fácil de hostear. Si prefieres integrarlo a tu app Angular existente en
  vez de tenerlo aparte, el backend no cambia — solo consumes los mismos
  endpoints desde un servicio Angular (`HttpClient`) en lugar de `fetch`.

## Importante

Este proyecto se escribió en un entorno sin SDK de .NET ni acceso a NuGet,
así que el código no se compiló ni se probó aquí. Antes de darlo por bueno,
corre `dotnet build` (o el `docker compose up --build`, que hace lo mismo)
en tu servidor y revisa que no haya errores de compilación.
