# Desplegar Centro de Tickets en la nube (Railway + Netlify)

Guía paso a paso para sacar este proyecto de la red interna y dejarlo accesible para oficina + gente remota, con la protección de contraseña compartida (`API_KEY`) que ya quedó agregada en `Program.cs` y `frontend/index.html`.

## 0. Subir el proyecto a GitHub

```bash
cd tickets-sistemas
git init
git add .
git commit -m "initial commit"
```

Crea un repositorio nuevo en GitHub (puede ser privado) y haz push:

```bash
git remote add origin https://github.com/tu-usuario/tickets-sistemas.git
git branch -M main
git push -u origin main
```

## 1. Base de datos: crear el proyecto en Supabase

1. Entra a [supabase.com](https://supabase.com), crea cuenta/inicia sesión, **New Project**.
2. Elige nombre, contraseña de la base (guárdala, la necesitas para la cadena de conexión) y región (la más cercana a donde vaya a vivir tu API — igual que Railway, ej. `us-east`).
3. Cuando termine de aprovisionar (1-2 min), ve a **Project Settings → Database → Connection string** y copia la variante **"Transaction pooler"** (puerto 6543) — es la recomendada para apps/contenedores que abren y cierran conexiones seguido, como la tuya en Railway. Se ve algo así:
   ```
   postgresql://postgres.xxxxxxxx:[TU-PASSWORD]@aws-0-us-east-1.pooler.supabase.com:6543/postgres
   ```
4. Tu API espera el formato de cadena de conexión de Npgsql, no la URL de `postgresql://`. Conviértela a este formato (mismos datos, distinto orden):
   ```
   Host=aws-0-us-east-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.xxxxxxxx;Password=TU-PASSWORD;SSL Mode=Require;Trust Server Certificate=true
   ```
   Guarda esta cadena completa, la usas en el paso 3 de Railway. No la subas nunca a GitHub (por eso `.env` ya está en `.gitignore`).

## 2. Backend (`TicketsSistemas.Api`) en Railway

1. Entra a railway.app e inicia sesión (puedes usar tu cuenta de GitHub).
2. **New Project → Deploy from GitHub repo** → selecciona `tickets-sistemas`.
3. En la configuración del servicio que se crea:
   - **Settings → Root Directory** = `TicketsSistemas.Api` (ahí está el `Dockerfile`; Railway lo detecta solo).
   - **Settings → Networking** → "Generate Domain" para obtener una URL pública (algo como `tickets-sistemas-api-production.up.railway.app`). En la misma sección, configura el **Target Port** = `8080` (coincide con el `EXPOSE 8080` del Dockerfile).
   - Ya no necesitas agregar ningún Volume — la base de datos vive en Supabase, no en un archivo local.
4. **Variables** (variables de entorno del servicio):
   - `ASPNETCORE_ENVIRONMENT` = `Production`
   - `ConnectionStrings__Default` = la cadena de conexión de Supabase que armaste en el paso 1.4 (doble guion bajo, es la notación de ASP.NET Core para configuración anidada).
   - `API_KEY` = elige una contraseña (ej. `Bisoft-Tickets-2026!`) — anótala, la vas a necesitar al entrar a la app.
   - `ALLOWED_ORIGINS` = déjala pendiente por ahora, la llenamos en el paso 4 cuando tengas la URL de Netlify.
5. Deploy (Railway lo hace automático al detectar el push). Cuando termine, prueba desde una terminal:
   ```bash
   curl https://tu-app.up.railway.app/health
   # {"status":"ok"}
   ```
   Si en vez de eso ves un error 500, casi siempre es la cadena de conexión mal armada — revisa host, puerto (6543) y password.
   Anota la URL completa de Railway, la necesitas en el siguiente paso.

## 3. Apuntar el frontend a la API de Railway

En `frontend/index.html`, busca esta línea (cerca del inicio del `<script>`):

```js
const API_BASE = '/api';
```

Cámbiala por la URL de Railway que anotaste, agregando `/api` al final:

```js
const API_BASE = 'https://tu-app.up.railway.app/api';
```

Guarda, haz commit y push:

```bash
git add frontend/index.html
git commit -m "apuntar frontend a la API en Railway"
git push
```

## 4. Frontend (`frontend/index.html`) en Netlify

1. Entra a netlify.com e inicia sesión con GitHub.
2. **Add new site → Import an existing project → Deploy with GitHub** → selecciona `tickets-sistemas`.
3. En la configuración del sitio:
   - **Base directory** = `frontend`
   - **Build command** = (déjalo vacío, es HTML/JS plano, no necesita build)
   - **Publish directory** = `frontend` (o `.` si Netlify ya te puso `frontend` como base directory, entonces publish directory queda como `.`)
4. Deploy. Netlify te da una URL tipo `https://tickets-bisoft.netlify.app` (puedes cambiar el nombre en **Site settings → Change site name**).

## 5. Cerrar el círculo: restringir CORS

Regresa a Railway → tu servicio de la API → **Variables** → edita `ALLOWED_ORIGINS` con la URL exacta de Netlify (sin `/` al final):

```
ALLOWED_ORIGINS=https://tickets-bisoft.netlify.app
```

Guarda — Railway vuelve a desplegar automáticamente con la variable nueva.

## 6. Probar todo junto

1. Abre la URL de Netlify en el navegador.
2. Te va a aparecer un cuadro pidiendo la "Contraseña de acceso al Centro de Tickets" — pon el valor que pusiste en `API_KEY` (paso 1.4).
3. Intenta crear un ticket nuevo y verifica que aparezca en la lista.
4. Si algo falla, abre las DevTools del navegador (F12) → pestaña **Network** → revisa la llamada a `/api/tickets`:
   - **Error de CORS**: `ALLOWED_ORIGINS` en Railway no coincide exactamente con la URL de Netlify.
   - **401 Unauthorized**: la contraseña no coincide con `API_KEY` en Railway (recarga la página, te la vuelve a pedir).
   - **Failed to fetch / no conecta**: revisa que `API_BASE` en `index.html` tenga la URL correcta de Railway y que el deploy de Railway esté "Active".

## 7. Compartir el acceso con tu equipo

Comparte la URL de Netlify y la contraseña (`API_KEY`) por un canal seguro (no por correo abierto ni chat público) con la gente de oficina y remota. Cada quien la captura una sola vez en su navegador — queda guardada ahí hasta que borren datos del sitio o usen otro navegador/dispositivo.

## Notas

- Tu forma original de desplegar (servidor Ubuntu interno + `docker-compose.yml` + nginx, documentada en `README.md`) sigue funcionando igual, solo que ahora también usa Supabase como base de datos en vez del archivo SQLite — no necesitas Railway/Netlify para eso, nada más agregar el `.env` con `SUPABASE_CONNECTION_STRING` junto al `docker-compose.yml`.
- `API_KEY` y `ALLOWED_ORIGINS` son opcionales — si no las configuras, el backend deja todo abierto (CORS sin restricción, sin exigir contraseña), pensado para cuando la API solo es accesible desde tu red interna.
- La cadena de conexión (`ConnectionStrings__Default`) sí es obligatoria ahora — sin ella la API no arranca (falla rápido con un mensaje claro en vez de fallar a medias, es a propósito).
- Si más adelante el equipo crece o quieres saber quién crea/cierra cada ticket, vale la pena migrar de la contraseña compartida a Cloudflare Access (login con correo @bisoft.com.mx) — lo platicamos como "Opción B" en su momento.
