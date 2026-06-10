# Build context is the repository root (see docker-compose.yml).
FROM node:22-alpine AS build
WORKDIR /app
COPY src/web/package.json src/web/package-lock.json* ./
RUN npm install
COPY src/web/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY src/web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
