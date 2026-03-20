FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:22-alpine
WORKDIR /app
COPY --from=build /app/dist ./dist
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/package.json ./

RUN addgroup -S appuser && adduser -S appuser -G appuser
RUN mkdir -p /app/logs && chown -R appuser:appuser /app/logs
USER appuser

EXPOSE 4000
ENV NODE_ENV=production
ENV PORT=4000

HEALTHCHECK --interval=15s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -qO- http://localhost:4000/health || exit 1

CMD ["node", "dist/main.js"]
