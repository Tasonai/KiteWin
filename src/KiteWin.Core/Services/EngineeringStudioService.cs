using System.Text;

namespace KiteWin.Core.Services;

public record StudioView(string Name, string Content);

public record StudioPayload(
    string Title,
    string Subtitle,
    IReadOnlyList<StudioView> Views
);

public static class EngineeringStudioService
{
    public static StudioPayload CreateStudio(string studioKey, string projectTitle, string concept, string platform, string style)
    {
        var pTitle = string.IsNullOrWhiteSpace(projectTitle) ? "Kite Project" : projectTitle.Trim();
        var pConcept = string.IsNullOrWhiteSpace(concept) ? "Universal reactive software architecture" : concept.Trim();
        var pPlatform = string.IsNullOrWhiteSpace(platform) ? "Web" : platform.Trim();
        var pStyle = string.IsNullOrWhiteSpace(style) ? "Editorial Swiss Modern" : style.Trim();

        return studioKey switch
        {
            "indexOptimizer" => BuildIndexOptimizer(pTitle),
            "seedData" => BuildSeedData(pTitle),
            "vectorStore" => BuildVectorStore(pTitle),
            "migrations" => BuildMigrations(pTitle),
            "rpcStudio" => BuildRpcStudio(pTitle),
            "eventBus" => BuildEventBus(pTitle),
            "jobQueue" => BuildJobQueue(pTitle),
            "rateLimiter" => BuildRateLimiter(pTitle),
            "queryCache" => BuildQueryCache(pTitle),
            "realtime" => BuildRealtime(pTitle),
            "webhooks" => BuildWebhooks(pTitle),
            "webhookDispatcher" => BuildWebhookDispatcher(pTitle),
            "tokenSandbox" => BuildTokenSandbox(pTitle, pStyle),
            "stateFlow" => BuildStateFlow(pTitle),
            "sitemapRouter" => BuildSitemapRouter(pTitle),
            "accessibility" => BuildAccessibility(pTitle),
            "seoStudio" => BuildSeoStudio(pTitle),
            "interactiveMockup" => BuildInteractiveMockup(pTitle, pConcept),
            "errorDoctor" => BuildErrorDoctor(pTitle),
            "threatModeler" => BuildThreatModeler(pTitle),
            "secretShield" => BuildSecretShield(pTitle),
            "evalHarness" => BuildEvalHarness(pTitle),
            "gitStory" => BuildGitStory(pTitle),
            "deployBlueprint" => BuildDeployBlueprint(pTitle),
            "incidentCommander" => BuildIncidentCommander(pTitle),
            "stressTest" => BuildStressTest(pTitle),
            "runbook" => BuildRunbook(pTitle),
            _ => new StudioPayload(
                pTitle,
                "Инженерная студия Antigravity",
                new List<StudioView>
                {
                    new("Обзор", $"Студия {studioKey} для проекта {pTitle}.\nКонцепция: {pConcept}")
                }
            )
        };
    }

    private static StudioPayload BuildIndexOptimizer(string title)
    {
        var sql = $"""
            -- Индексы базы данных для {title}
            -- Zero-downtime CONCURRENTLY DDL (PostgreSQL 14+)

            CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_users_email_lower
            ON users (LOWER(email));

            CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_projects_owner_created
            ON projects (owner_id, created_at DESC);

            CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_audit_logs_tenant_timestamp
            ON audit_logs (tenant_id, timestamp DESC)
            INCLUDE (action, user_id);

            CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_search_trgm
            ON items USING GIN (title gin_trgm_ops, concept gin_trgm_ops);
            """;

        var md = $"""
            # Стратегия индексации: {title}

            ## 1. Рекомендации по оптимизации EXPLAIN ANALYZE
            - **Покрывающие индексы**: использование `INCLUDE` для предотвращения Heap Fetches.
            - **Партиционирование**: таблицы аудита партиционируются по месяцам (`timestamp`).
            - **GIN Текстовый поиск**: трехсимвольный триграммный поиск по названию и концепции.

            ## 2. Мониторинг bloat-фактора
            - Авто-вакуум настроен с порогом `autovacuum_vacuum_scale_factor = 0.05`.
            """;

        return new StudioPayload(
            "Оптимизатор индексов БД (Index Optimizer)",
            "Рекомендации B-Tree, GIN, BRIN индексов и zero-downtime DDL",
            new List<StudioView>
            {
                new("SQL Миграция (schema.sql)", sql),
                new("Манифест (INDEXING.md)", md)
            }
        );
    }

    private static StudioPayload BuildSeedData(string title)
    {
        var sql = $"""
            -- Генерация тестовых фикстур для {title}
            INSERT INTO users (id, email, name, role, created_at)
            VALUES
              ('usr_101', 'admin@example.com', 'System Administrator', 'admin', NOW()),
              ('usr_102', 'developer@example.com', 'Lead Engineer', 'developer', NOW()),
              ('usr_103', 'auditor@example.com', 'Security Reviewer', 'auditor', NOW())
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO projects (id, owner_id, title, status, created_at)
            VALUES
              ('prj_001', 'usr_101', '{title} Core Engine', 'active', NOW()),
              ('prj_002', 'usr_102', '{title} Edge Client', 'draft', NOW())
            ON CONFLICT (id) DO NOTHING;
            """;

        var json = """
            {
              "users": [
                { "id": "usr_101", "email": "admin@example.com", "role": "admin" },
                { "id": "usr_102", "email": "dev@example.com", "role": "developer" }
              ],
              "settings": {
                "telemetry": true,
                "maxConcurrency": 16,
                "environment": "staging"
              }
            }
            """;

        return new StudioPayload(
            "Генератор тестовых данных (Seed Data)",
            "Детерминированные фикстуры и реалистичные моки",
            new List<StudioView>
            {
                new("SQL Фикстуры", sql),
                new("JSON Mock Data", json)
            }
        );
    }

    private static StudioPayload BuildVectorStore(string title)
    {
        var sql = $"""
            -- Векторная база данных pgvector для {title}
            CREATE EXTENSION IF NOT EXISTS vector;

            CREATE TABLE IF NOT EXISTS embeddings (
              id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
              document_id TEXT NOT NULL,
              chunk_index INT NOT NULL,
              content TEXT NOT NULL,
              embedding vector(1536),
              metadata JSONB DEFAULT '{{}}'::jsonb,
              created_at TIMESTAMPTZ DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_embeddings_hnsw
            ON embeddings USING hnsw (embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64);
            """;

        var client = """
            import { pgPool } from '../db';

            export async function searchSimilarChunks(queryVector: number[], limit = 5) {
              const res = await pgPool.query(
                `SELECT document_id, chunk_index, content,
                        1 - (embedding <=> $1::vector) as similarity
                 FROM embeddings
                 WHERE 1 - (embedding <=> $1::vector) > 0.75
                 ORDER BY embedding <=> $1::vector ASC
                 LIMIT $2;`,
                [JSON.stringify(queryVector), limit]
              );
              return res.rows;
            }
            """;

        return new StudioPayload(
            "Векторная база RAG (Vector Store)",
            "Спецификация pgvector / HNSW, чанкинг и косинусный поиск",
            new List<StudioView>
            {
                new("pgvector DDL", sql),
                new("TypeScript Search Client", client)
            }
        );
    }

    private static StudioPayload BuildMigrations(string title)
    {
        var sql = $"""
            -- Двухфазная миграция Expand & Contract для {title}
            -- Фаза 1: Расширение (Добавление нового поля без ломки старого)
            ALTER TABLE projects ADD COLUMN IF NOT EXISTS status_v2 TEXT DEFAULT 'draft';

            -- Бэкфилл данных порциями (Batching)
            UPDATE projects SET status_v2 = status WHERE status_v2 IS NULL;

            -- Фаза 2: Контракт (Выполняется в следующем релизе)
            -- ALTER TABLE projects DROP COLUMN status;
            -- ALTER TABLE projects RENAME COLUMN status_v2 TO status;
            """;

        return new StudioPayload(
            "Миграции Expand & Contract",
            "Zero-downtime обновления схемы данных и безопасный откат",
            new List<StudioView>
            {
                new("SQL Миграция", sql)
            }
        );
    }

    private static StudioPayload BuildRpcStudio(string title)
    {
        var trpc = $"""
            import {{"initTRPC"}} from '@trpc/server';
            import {{"z"}} from 'zod';

            const t = initTRPC.create();
            export const router = t.router;
            export const publicProcedure = t.procedure;

            export const appRouter = router({{
              getHealth: publicProcedure.query(() => ({{
                status: 'operational',
                service: '{title}',
                uptime: process.uptime()
              }})),

              createItem: publicProcedure
                .input(z.object({{
                  title: z.string().min(1).max(128),
                  concept: z.string().min(10)
                }}))
                .mutation(async ({{ input }}) => {{
                  return {{ id: 'itm_' + Date.now(), ...input, createdAt: new Date() }};
                }})
            }});

            export type AppRouter = typeof appRouter;
            """;

        return new StudioPayload(
            "Типизированный RPC / tRPC",
            "Контракты процедур tRPC, Zod-валидаторы и автокомплит",
            new List<StudioView>
            {
                new("tRPC Router", trpc)
            }
        );
    }

    private static StudioPayload BuildEventBus(string title)
    {
        var ts = $"""
            import {{"EventEmitter"}} from 'node:events';

            export interface DomainEvents {{
              'project.created': {{ id: string; title: string; ownerId: string }};
              'project.deployed': {{ id: string; targetUrl: string; version: string }};
              'alert.triggered': {{ code: string; message: string; severity: 'warning' | 'critical' }};
            }}

            export class TypedEventBus extends EventEmitter {{
              emitTyped<K extends keyof DomainEvents>(event: K, payload: DomainEvents[K]): boolean {{
                return this.emit(event, payload);
              }}

              onTyped<K extends keyof DomainEvents>(event: K, listener: (payload: DomainEvents[K]) => void): this {{
                return this.on(event, listener);
              }}
            }}

            export const eventBus = new TypedEventBus();
            """;

        return new StudioPayload(
            "Шина событий (EventBus)",
            "Типизированные доменные события, Pub/Sub и аудит-трейсинг",
            new List<StudioView>
            {
                new("EventBus TypeScript", ts)
            }
        );
    }

    private static StudioPayload BuildJobQueue(string title)
    {
        var ts = $"""
            import {{"Queue", "Worker"}} from 'bullmq';

            export interface BuildJobData {{
              projectId: string;
              commitHash: string;
              environment: string;
            }}

            export const buildQueue = new Queue<BuildJobData>('{title}-builds', {{
              connection: {{ host: process.env.REDIS_HOST || '127.0.0.1', port: 6379 }}
            }});

            export const worker = new Worker<BuildJobData>('{title}-builds', async job => {{
              console.log(`Processing build ${{job.id}} for project ${{job.data.projectId}}`);
              // Execution logic with automatic exponential backoff
            }}, {{
              concurrency: 4,
              limiter: {{ max: 20, duration: 1000 }}
            }});
            """;

        return new StudioPayload(
            "Очереди фоновых задач (Job Queue)",
            "Асинхронные воркеры BullMQ/Inngest, дедупликация и ретраи",
            new List<StudioView>
            {
                new("BullMQ Worker Setup", ts)
            }
        );
    }

    private static StudioPayload BuildRateLimiter(string title)
    {
        var ts = """
            import { Redis } from '@upstash/redis';
            import { Ratelimit } from '@upstash/ratelimit';

            const redis = Redis.fromEnv();

            export const rateLimiter = new Ratelimit({
              redis,
              limiter: Ratelimit.slidingWindow(60, '1 m'),
              analytics: true,
              prefix: 'ratelimit:api'
            });

            export async function checkLimit(identifier: string) {
              const { success, limit, remaining, reset } = await rateLimiter.limit(identifier);
              return { success, remaining, reset };
            }
            """;

        return new StudioPayload(
            "Rate Limiter и DDoS-защита",
            "Sliding-window лимитер запросов на базе Upstash Redis",
            new List<StudioView>
            {
                new("Rate Limiter Middleware", ts)
            }
        );
    }

    private static StudioPayload BuildQueryCache(string title)
    {
        var ts = """
            import { LRUCache } from 'lru-cache';

            const memoryCache = new LRUCache<string, any>({
              max: 1000,
              ttl: 1000 * 60 * 5, // 5 minutes
              allowStale: true
            });

            export async function withQueryCache<T>(key: string, fetcher: () => Promise<T>): Promise<T> {
              const cached = memoryCache.get(key);
              if (cached !== undefined) return cached as T;

              const fresh = await fetcher();
              memoryCache.set(key, fresh);
              return fresh;
            }
            """;

        return new StudioPayload(
            "Двухуровневый кэш запросов (Query Cache)",
            "Кэширование в оперативной памяти (L1 LRU) и распределенный кэш (L2)",
            new List<StudioView>
            {
                new("L1 In-Memory LRU Cache", ts)
            }
        );
    }

    private static StudioPayload BuildRealtime(string title)
    {
        var ts = """
            import { WebSocketServer, WebSocket } from 'ws';

            export function setupRealtimeServer(port = 8080) {
              const wss = new WebSocketServer({ port });
              const clients = new Set<WebSocket>();

              wss.on('connection', ws => {
                clients.add(ws);
                ws.send(JSON.stringify({ type: 'handshake', status: 'connected' }));

                ws.on('message', data => {
                  for (const client of clients) {
                    if (client !== ws && client.readyState === WebSocket.OPEN) {
                      client.send(data.toString());
                    }
                  }
                });

                ws.on('close', () => clients.delete(ws));
              });

              return wss;
            }
            """;

        return new StudioPayload(
            "Потоковый Realtime WebSockets",
            "Двунаправленный обмен сообщениями, Heartbeat и авто-реконнект",
            new List<StudioView>
            {
                new("WebSocket Server", ts)
            }
        );
    }

    private static StudioPayload BuildWebhooks(string title)
    {
        var ts = """
            import crypto from 'node:crypto';

            export function verifyWebhookSignature(payload: string, signature: string, secret: string): boolean {
              const hmac = crypto.createHmac('sha256', secret);
              const digest = 'sha256=' + hmac.update(payload).digest('hex');
              return crypto.timingSafeEqual(Buffer.from(signature), Buffer.from(digest));
            }
            """;

        return new StudioPayload(
            "Симулятор вебхуков (Webhook Simulator)",
            "Проверка подписи HMAC-SHA256 и защита от Replay-атак",
            new List<StudioView>
            {
                new("HMAC Signature Validator", ts)
            }
        );
    }

    private static StudioPayload BuildWebhookDispatcher(string title)
    {
        var ts = """
            export async function dispatchWebhook(url: string, event: string, payload: any, secret: string) {
              const body = JSON.stringify({ event, payload, timestamp: Date.now() });
              const signature = crypto.createHmac('sha256', secret).update(body).digest('hex');

              const response = await fetch(url, {
                method: 'POST',
                headers: {
                  'Content-Type': 'application/json',
                  'X-Kite-Signature': signature
                },
                body
              });

              if (!response.ok) {
                throw new Error(`Webhook dispatch failed with status ${response.status}`);
              }
            }
            """;

        return new StudioPayload(
            "Диспетчер исходящих вебхуков",
            "Очередь доставки с экспоненциальной задержкой и Dead-Letter Queue",
            new List<StudioView>
            {
                new("Webhook Dispatcher Pipeline", ts)
            }
        );
    }

    private static StudioPayload BuildTokenSandbox(string title, string style)
    {
        var css = """
            :root {
              --bg-primary: #0F172A;
              --bg-surface: #1E293B;
              --border-subtle: #334155;
              --text-primary: #F8FAFC;
              --text-secondary: #94A3B8;
              --accent-brand: #38BDF8;
              --radius-card: 8px;
              --font-sans: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
            }
            """;

        var json = """
            {
              "color": {
                "background": { "default": "#0F172A", "surface": "#1E293B" },
                "text": { "primary": "#F8FAFC", "secondary": "#94A3B8" },
                "accent": { "brand": "#38BDF8" }
              },
              "spacing": { "xs": 4, "sm": 8, "md": 16, "lg": 24, "xl": 32 }
            }
            """;

        return new StudioPayload(
            "Песочница дизайн-токенов",
            $"Токены темы: {style}, экспорт в CSS variables и Tailwind",
            new List<StudioView>
            {
                new("CSS Переменные", css),
                new("Tokens JSON", json)
            }
        );
    }

    private static StudioPayload BuildStateFlow(string title)
    {
        var json = """
            {
              "id": "uiWorkflow",
              "initial": "idle",
              "states": {
                "idle": {
                  "on": { "START": "loading" }
                },
                "loading": {
                  "on": {
                    "SUCCESS": "ready",
                    "ERROR": "failure"
                  }
                },
                "ready": {
                  "on": { "REFRESH": "loading", "RESET": "idle" }
                },
                "failure": {
                  "on": { "RETRY": "loading" }
                }
              }
            }
            """;

        return new StudioPayload(
            "Стейт-машина интерфейса (XState)",
            "Конечный автомат переходов между экранами и состояниями загрузки",
            new List<StudioView>
            {
                new("XState Machine Config", json)
            }
        );
    }

    private static StudioPayload BuildSitemapRouter(string title)
    {
        var md = $"""
            # Карта маршрутов: {title}

            | Путь | Компонент | Доступ | Назначение |
            | :--- | :--- | :--- | :--- |
            | `/` | `LandingPage` | Публичный | Главная страница и онбординг |
            | `/workspace` | `WorkspaceLayout` | Авторизован | Главное рабочее пространство |
            | `/topology` | `CanvasTopology` | Авторизован | Холст узлов и архитектуры |
            | `/translator`| `PromptTranslator`| Авторизован | Генератор и компилятор промптов |
            | `/settings` | `SettingsView` | Авторизован | Параметры темы и P2P-синхронизации |
            """;

        return new StudioPayload(
            "Карта экранов и роутер",
            "Иерархия маршрутов Next.js App Router / React Router",
            new List<StudioView>
            {
                new("Маршруты (SITEMAP.md)", md)
            }
        );
    }

    private static StudioPayload BuildAccessibility(string title)
    {
        var md = """
            # Аудит доступности WCAG 2.2 AAA

            ## 1. Контрастность и читаемость
            - Текст первого плана к фону: коэффициент >= 4.5:1 для обычного текста и >= 3.0:1 для крупного.
            - Индикация фокуса: четкое контрастное кольцо `outline: 2px solid var(--accent)`.

            ## 2. Навигация с клавиатуры
            - `Tab` / `Shift+Tab`: последовательный обход интерактивных элементов.
            - `Escape`: мгновенное закрытие модальных окон и выпадающих списков.
            - `Aria-Live`: озвучивание статусов асинхронных операций скринридерами.
            """;

        return new StudioPayload(
            "Аудит доступности (WCAG 2.2)",
            "Стандарты доступности, фокусные кольца и поддержка скринридеров",
            new List<StudioView>
            {
                new("Спецификация WCAG", md)
            }
        );
    }

    private static StudioPayload BuildSeoStudio(string title)
    {
        var ts = $"""
            import type {{"Metadata"}} from 'next';

            export const metadata: Metadata = {{
              title: '{title} — Modern Architecture Cockpit',
              description: 'Next-generation engineering workspace for high-velocity software delivery.',
              openGraph: {{
                title: '{title}',
                description: 'Build robust reactive architectures.',
                type: 'website',
                images: ['/og-image.png']
              }},
              twitter: {{
                card: 'summary_large_image',
                title: '{title}'
              }}
            }};
            """;

        return new StudioPayload(
            "SEO и OpenGraph Next.js",
            "Метаданные для поисковых систем, превью соцсетей и robots.txt",
            new List<StudioView>
            {
                new("Next.js Metadata Config", ts)
            }
        );
    }

    private static StudioPayload BuildInteractiveMockup(string title, string concept)
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="ru">
            <head>
              <meta charset="utf-8">
              <title>{title} — Prototype</title>
              <style>
                body {{
                  margin: 0;
                  padding: 24px;
                  background: #0f172a;
                  color: #f8fafc;
                  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
                }}
                .card {{
                  background: #1e293b;
                  border: 1px solid #334155;
                  border-radius: 8px;
                  padding: 24px;
                  max-width: 600px;
                  margin: 0 auto;
                }}
                button {{
                  background: #38bdf8;
                  color: #0f172a;
                  border: none;
                  padding: 10px 20px;
                  border-radius: 6px;
                  font-weight: 600;
                  cursor: pointer;
                }}
              </style>
            </head>
            <body>
              <div class="card">
                <h1>{title}</h1>
                <p>{concept}</p>
                <button onclick="alert('Action triggered')">Запустить процесс</button>
              </div>
            </body>
            </html>
            """;

        return new StudioPayload(
            "Интерактивный HTML-макет",
            "Автономный HTML5/CSS3 прототип с нулевыми внешними зависимостями",
            new List<StudioView>
            {
                new("HTML5 Макет", html)
            }
        );
    }

    private static StudioPayload BuildErrorDoctor(string title)
    {
        var md = """
            # Диагностический отчет: Terminal Error Doctor

            ## 1. Сигнатура ошибки
            `ModuleNotFoundError: No module named 'sqlite3'` или `Error: Cannot find module '@prisma/client'`

            ## 2. Причина возникновения
            Отсутствие сгенерированного клиента ORM или незавершенный шаг инициализации окружения.

            ## 3. Точный промпт устранения
            `Запусти 'npx prisma generate' и верифицируй локальное подключение к БД перед стартом сервера.`
            """;

        return new StudioPayload(
            "Доктор сбоев терминала (Error Doctor)",
            "Автоматический разбор трассировки стека и хирургический промпт исправления",
            new List<StudioView>
            {
                new("Анализ сбоев", md)
            }
        );
    }

    private static StudioPayload BuildThreatModeler(string title)
    {
        var md = $"""
            # Моделирование угроз OWASP: {title}

            ## 1. Матрица угроз STRIDE
            - **Spoofing (Подделка)**: JWT-токены с алгоритмом RS256 и коротким временем жизни (15 минут).
            - **Tampering (Искажение)**: Подпись полезной нагрузки webhook через HMAC-SHA256.
            - **Information Disclosure (Утечка)**: Маскирование PII в логах и исключение секретов из ответов API.
            - **Denial of Service (Отказ)**: Глобальный Rate Limiter на базе Redis.

            ## 2. Директивы безопасности для Antigravity
            - Все внешние входящие запросы должны проходить через Zod/Pydantic валидаторы.
            - Доступ к БД разрешен только через подготовленные выражения (Prepared Statements).
            """;

        return new StudioPayload(
            "Моделирование угроз (OWASP)",
            "Анализ векторов атак, матрица STRIDE и правила изоляции данных",
            new List<StudioView>
            {
                new("Модель угроз (THREATS.md)", md)
            }
        );
    }

    private static StudioPayload BuildSecretShield(string title)
    {
        var md = """
            # Защита от утечки секретов: Secret Shield

            ## 1. Паттерны поиска приватных ключей
            - AWS Access Key: `AKIA[0-9A-Z]{16}`
            - OpenAI API Key: `sk-[a-zA-Z0-9]{48}`
            - GitHub Personal Token: `ghp_[a-zA-Z0-9]{36}`
            - Private RSA Key: `-----BEGIN RSA PRIVATE KEY-----`

            ## 2. Действие при обнаружении
            - Немедленная блокировка коммита локальным хуком.
            - Ротация утекшего ключа через Vault / Cloud Provider API.
            """;

        return new StudioPayload(
            "Защита от утечки секретов (Secret Shield)",
            "Предотвращение попадания API-ключей и паролей в git-репозиторий",
            new List<StudioView>
            {
                new("Правила сканирования", md)
            }
        );
    }

    private static StudioPayload BuildEvalHarness(string title)
    {
        var ts = """
            import { describe, it, expect } from 'vitest';

            describe('Architecture Regression Eval Suite', () => {
              it('should enforce p95 response time under 200ms', async () => {
                const start = performance.now();
                const res = await fetch('http://localhost:3000/api/health');
                const duration = performance.now() - start;
                expect(res.status).toBe(200);
                expect(duration).toBeLessThan(200);
              });

              it('should reject unauthenticated requests to protected endpoints', async () => {
                const res = await fetch('http://localhost:3000/api/admin/metrics');
                expect(res.status).toBe(401);
              });
            });
            """;

        return new StudioPayload(
            "Регрессионные тесты и эвалы",
            "Набор автоматических тестов производительности и контрактов",
            new List<StudioView>
            {
                new("Vitest Test Suite", ts)
            }
        );
    }

    private static StudioPayload BuildGitStory(string title)
    {
        var commit = $"""
            feat: implement core topology architecture for {title}

            - Configure relation schemas and indexed database entities
            - Establish typed RPC contracts and event bus integration
            - Setup zero-downtime deployment blueprints and health probes
            """;

        var pr = $"""
            ## Описание PR
            Данный пулреквест реализует базовый каркас приложения {title} в соответствии с канонами Antigravity.

            ### Что изменено:
            1. Внедрена схема данных с UUID v4 и B-Tree индексами.
            2. Добавлены типизированные контракты взаимодействия.
            3. Настроена защита от утечки секретов и аудит доступности.

            ### План тестирования:
            - [x] Автоматические тесты проходят успешно
            - [x] Отсутствуют предупреждения компилятора
            """;

        return new StudioPayload(
            "Conventional Commits и PR",
            "Генератор структурированных сообщений коммитов и описаний пулреквестов",
            new List<StudioView>
            {
                new("Сообщение коммита", commit),
                new("Описание Pull Request", pr)
            }
        );
    }

    private static StudioPayload BuildDeployBlueprint(string title)
    {
        var dockerfile = $"""
            FROM node:20-alpine AS builder
            WORKDIR /app
            COPY package*.json ./
            RUN npm ci
            COPY . .
            RUN npm run build

            FROM node:20-alpine AS runner
            WORKDIR /app
            ENV NODE_ENV=production
            COPY --from=builder /app/dist ./dist
            COPY --from=builder /app/node_modules ./node_modules
            EXPOSE 3000
            CMD ["node", "dist/index.js"]
            """;

        var vercelJson = """
            {
              "framework": "nextjs",
              "buildCommand": "npm run build",
              "outputDirectory": ".next",
              "regions": ["fra1"]
            }
            """;

        return new StudioPayload(
            "Blueprints деплоя Vercel/Docker",
            "Оптимизированные конфигурации Docker и манифесты деплоя",
            new List<StudioView>
            {
                new("Dockerfile", dockerfile),
                new("vercel.json", vercelJson)
            }
        );
    }

    private static StudioPayload BuildIncidentCommander(string title)
    {
        var md = $"""
            # План аварийного восстановления: {title}

            ## 1. Сценарий: Отказ основного узла БД
            1. Проверить статус реплики `SELECT * FROM pg_stat_replication;`.
            2. Переключить DNS/роутер на резервный инстанс (Failover trigger).
            3. Запустить скрипт проверки целостности данных.

            ## 2. Сценарий: Всплеск нагрузки (Spike Traffic)
            1. Активировать аварийный режим кэширования L1 (Stale-While-Revalidate).
            2. Масштабировать контейнеры до 8 реплик.
            """;

        return new StudioPayload(
            "План аварийного восстановления (Incident Commander)",
            "Runbook восстановления при сбоях и протоколы эскалации инцидентов",
            new List<StudioView>
            {
                new("Disaster Recovery Plan", md)
            }
        );
    }

    private static StudioPayload BuildStressTest(string title)
    {
        var k6 = """
            import http from 'k6/http';
            import { check, sleep } from 'k6';

            export const options = {
              stages: [
                { duration: '30s', target: 50 },  // Разгон до 50 пользователей
                { duration: '1m', target: 50 },   // Полка нагрузки
                { duration: '30s', target: 0 }    // Снижение нагрузки
              ],
              thresholds: {
                http_req_duration: ['p(95)<250'], // 95% запросов быстрее 250 мс
                http_req_failed: ['rate<0.01']    // Ошибок менее 1%
              }
            };

            export default function () {
              const res = http.get('http://localhost:3000/api/health');
              check(res, { 'status is 200': (r) => r.status === 200 });
              sleep(1);
            }
            """;

        return new StudioPayload(
            "Нагрузочные тесты k6 (Stress Test)",
            "Сценарии нагрузочного тестирования k6 с проверкой порогов p95",
            new List<StudioView>
            {
                new("k6 Скрипт нагрузки", k6)
            }
        );
    }

    private static StudioPayload BuildRunbook(string title)
    {
        var md = $"""
            # Runbook разработчика: {title}

            ## 1. Локальный запуск
            ```bash
            # Клонирование и установка зависимостей
            git clone <repo-url>
            npm install

            # Запуск базы данных
            docker compose up -d

            # Запуск в режиме разработки
            npm run dev
            ```

            ## 2. Проверка качества перед коммитом
            ```bash
            npm run lint
            npm run test
            ```
            """;

        return new StudioPayload(
            "Runbook разработчика",
            "Инструкция по развертыванию, отладке и локальному тестированию",
            new List<StudioView>
            {
                new("Developer Runbook", md)
            }
        );
    }
}
