# merchant-web static → nginx
FROM node:20-alpine AS build
WORKDIR /app
COPY merchant-web/package.json merchant-web/package-lock.json* ./
RUN npm ci || npm install
COPY merchant-web/ ./
# empty base → same-origin /api via nginx proxy
ARG VITE_API_BASE_URL=
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY docker/merchant-web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
