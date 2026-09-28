# Migrar a servidor propio (solo red local)

Guía para sacar el Centro de Tickets de Netlify + Railway + Supabase y dejarlo
corriendo en tu servidor Ubuntu, solo accesible desde la red interna de
oficina. Requiere `docker-compose.yml` + `nginx-tickets.conf.example`
(actualizados en esta misma sesión) y acceso al DNS de `bisoft.com.mx` para
sacar un certificado real sin exponer el servidor a internet.

## 0. En el servidor: requisitos

```bash
# Docker + Docker Compose (si no los tienes ya)
curl -fsSL https://get.docker.com | sudo sh
sudo apt install docker-compose-plugin nginx certbot -y
```

## 1. Copiar el proyecto

```bash
git clone https://github.com/BiggieJr1/tickets-sistemas-backend.git tickets-sistemas
cd tickets-sistemas
```

## 2. Migrar la base de datos (Supabase → local)

**2.1 Saca el dump de Supabase.** Puede ser desde tu máquina o desde el
servidor nuevo, lo que importa es tener `pg_dump` instalado (viene con
`postgresql-client`). Usa la conexión **directa** de Supabase (puerto
`5432`, "Session mode" o "Direct connection" en el dashboard), **no** la de
"Transaction pooler" (puerto `6543`) — `pg_dump` no funciona bien a través
del pooler de transacciones.

```bash
pg_dump "postgresql://postgres.xxxxxxxx:TU-PASSWORD@aws-0-us-east-1.pooler.supabase.com:5432/postgres" \
  --format=custom --no-owner --no-privileges --file=tickets-supabase.dump
```

**2.2 Levanta solo la base nueva** (todavía no la API, para restaurar antes
de que intente correr migraciones sobre una base vacía):

```bash
# .env junto al docker-compose.yml (nunca se sube a git). Llena los valores
# siguiendo los comentarios de la plantilla:
cp .env.example .env
nano .env

# Espera a que el healthcheck de Postgres pase a "healthy":
docker compose up -d --wait tickets-db
```

**2.3 Restaura el dump** dentro del contenedor:

```bash
docker cp tickets-supabase.dump tickets-sistemas-db:/tmp/tickets.dump
docker exec tickets-sistemas-db pg_restore -U tickets -d tickets \
  --no-owner --no-privileges /tmp/tickets.dump
```

Esto incluye la tabla `__EFMigrationsHistory`, así que cuando levantes la
API en el paso 3, `db.Database.Migrate()` va a ver que todas las
migraciones ya están aplicadas y no va a intentar recrear nada.

## 3. Levantar la API

```bash
docker compose up -d --build
curl http://127.0.0.1:5080/health
# {"status":"ok"}
```

## 4. Compilar y copiar el frontend

Desde tu máquina (o donde tengas Node/Angular CLI):

```bash
cd ../tickets-sistemas   # el repo del frontend, no el del backend
npm install
npm run build
scp -r dist/tickets-sistemas usuario@servidor:/tmp/
```

En el servidor:

```bash
sudo mkdir -p /var/www/tickets-sistemas
sudo mv /tmp/tickets-sistemas /var/www/tickets-sistemas/dist
```

## 5. Certificado real por validación DNS (sin abrir puertos)

```bash
sudo certbot certonly --manual --preferred-challenges dns \
  -d tickets.bisoft.com.mx
```

Te va a pedir crear un registro TXT temporal (`_acme-challenge.tickets`) en
el DNS de `bisoft.com.mx` — con eso valida que controlas el dominio, sin
necesidad de que el servidor sea alcanzable desde internet. El certificado
queda en `/etc/letsencrypt/live/tickets.bisoft.com.mx/`. Repite cada ~90
días (o automatiza con el plugin DNS de tu proveedor, si `certbot` tiene uno
para él).

## 6. nginx

```bash
sudo cp nginx-tickets.conf.example /etc/nginx/conf.d/tickets.conf
sudo nginx -t && sudo systemctl reload nginx
```

## 7. Azure — agregar el redirect URI

Entra admin center → **App registrations** → `tickets-sistemas-frontend`
(ClientId `c50e32a0-dd31-4e62-a160-2e9169da72d3`) → **Authentication** →
agrega `https://tickets.bisoft.com.mx` a la lista de Redirect URIs (plataforma
SPA). Puedes dejar la de Netlify también, no estorban entre sí.

## 8. Resolver el dominio dentro de la oficina

`tickets.bisoft.com.mx` tiene que apuntar a la IP interna del servidor
**solo para la gente de la oficina** — dos formas:

- Si tienen un DNS interno (router/servidor DNS de la red), agrega ahí un
  registro A `tickets.bisoft.com.mx` → IP interna del servidor.
- Si no, en cada máquina de oficina agrega una línea al archivo `hosts`
  (`C:\Windows\System32\drivers\etc\hosts` en Windows):
  ```
  192.168.x.x    tickets.bisoft.com.mx
  ```

(El registro DNS *público* de `bisoft.com.mx` no se toca — el TXT del paso 5
fue solo para validar el certificado, no hace falta un A público.)

## 9. Probar todo

Desde una máquina de oficina: `https://tickets.bisoft.com.mx` → login con
Microsoft → debe entrar sin advertencias de certificado y ver los tickets
reales (los que migraste de Supabase en el paso 2).
