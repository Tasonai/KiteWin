namespace KiteWin.Core.Services;

using System;
using System.Collections.Generic;
using KiteWin.Core.Models;

public sealed class AutoArchitectureResult
{
    public IReadOnlyList<CanvasFrame> Frames { get; }
    public IReadOnlyList<CanvasSticky> Stickies { get; }
    public IReadOnlyList<CanvasConnection> Connections { get; }

    public AutoArchitectureResult(
        IReadOnlyList<CanvasFrame> frames,
        IReadOnlyList<CanvasSticky> stickies,
        IReadOnlyList<CanvasConnection> connections)
    {
        Frames = frames;
        Stickies = stickies;
        Connections = connections;
    }
}

public static class CanvasAutoArchitect
{
    public static AutoArchitectureResult Synthesize(
        string title,
        string concept,
        string platform = "Windows 11",
        string style = "Fluent Design")
    {
        var cleanTitle = string.IsNullOrWhiteSpace(title) ? "Архитектура" : title.Trim();
        var cleanConcept = string.IsNullOrWhiteSpace(concept) ? "Сервис бизнес-логики" : concept.Trim();

        // 1. Frames (Layered Subsystems)
        var fClient = new CanvasFrame
        {
            Title = $"1. Клиентский уровень ({platform})",
            ColorHex = "#3B82F6",
            X = 80,
            Y = 80,
            Width = 340,
            Height = 480
        };

        var fGateway = new CanvasFrame
        {
            Title = "2. Edge Gateway & Безопасность",
            ColorHex = "#8B5CF6",
            X = 480,
            Y = 80,
            Width = 340,
            Height = 480
        };

        var fDomain = new CanvasFrame
        {
            Title = "3. Сервисы предметной области",
            ColorHex = "#10B981",
            X = 880,
            Y = 80,
            Width = 360,
            Height = 480
        };

        var fData = new CanvasFrame
        {
            Title = "4. Персистентность и Кэш",
            ColorHex = "#64748B",
            X = 1300,
            Y = 80,
            Width = 340,
            Height = 480
        };

        var fAsync = new CanvasFrame
        {
            Title = "5. Фоновые очереди и EventBus",
            ColorHex = "#F59E0B",
            X = 880,
            Y = 620,
            Width = 360,
            Height = 400
        };

        // 2. Component Stickies
        var sUI = new CanvasSticky
        {
            Text = $"{cleanTitle} UI ({platform})\nЭстетика: {style}",
            ColorHex = "#BAE6FD",
            X = 250,
            Y = 180
        };

        var sState = new CanvasSticky
        {
            Text = "Клиентский менеджер состояния и кэш форм",
            ColorHex = "#E0F2FE",
            X = 250,
            Y = 380
        };

        var sGateway = new CanvasSticky
        {
            Text = "Edge API Gateway\nRate Limiter (100 req/min) & WAF",
            ColorHex = "#DDD6FE",
            X = 650,
            Y = 180
        };

        var sAuth = new CanvasSticky
        {
            Text = "JWT Auth / Session Guard\nПроверка прав и RBAC",
            ColorHex = "#EDE9FE",
            X = 650,
            Y = 380
        };

        var sCoreService = new CanvasSticky
        {
            Text = $"{cleanTitle} Core Domain Service\n{cleanConcept}",
            ColorHex = "#BBF7D0",
            X = 1060,
            Y = 180
        };

        var sValidator = new CanvasSticky
        {
            Text = "Zod Schema Validator & Бизнес-правила",
            ColorHex = "#DCFCE7",
            X = 1060,
            Y = 380
        };

        var sDB = new CanvasSticky
        {
            Text = "PostgreSQL Database\nРеляционная схема с B-Tree индексами",
            ColorHex = "#E2E8F0",
            X = 1470,
            Y = 180
        };

        var sRedis = new CanvasSticky
        {
            Text = "Redis In-Memory Cache\nLRU буфер горячих запросов (TTL 300s)",
            ColorHex = "#F1F5F9",
            X = 1470,
            Y = 380
        };

        var sEventBus = new CanvasSticky
        {
            Text = "Domain EventBus\nПубликация доменных событий",
            ColorHex = "#FEF08A",
            X = 1060,
            Y = 720
        };

        var sWorker = new CanvasSticky
        {
            Text = "Async Job Queue (Inngest/Temporal)\nТяжелые задачи, письма и вебхуки",
            ColorHex = "#FEF9C3",
            X = 1060,
            Y = 880
        };

        // 3. Typified Protocol Connections
        var c1 = new CanvasConnection
        {
            FromId = $"sticky-{sUI.Id}",
            ToId = $"sticky-{sGateway.Id}",
            Label = CanvasConnectionProtocol.Trpc,
            ColorHex = CanvasConnectionProtocol.Color(CanvasConnectionProtocol.Trpc)
        };

        var c2 = new CanvasConnection
        {
            FromId = $"sticky-{sGateway.Id}",
            ToId = $"sticky-{sAuth.Id}",
            Label = CanvasConnectionProtocol.Grpc,
            ColorHex = CanvasConnectionProtocol.Color(CanvasConnectionProtocol.Grpc)
        };

        var c3 = new CanvasConnection
        {
            FromId = $"sticky-{sGateway.Id}",
            ToId = $"sticky-{sCoreService.Id}",
            Label = CanvasConnectionProtocol.Grpc,
            ColorHex = CanvasConnectionProtocol.Color(CanvasConnectionProtocol.Grpc)
        };

        var c4 = new CanvasConnection
        {
            FromId = $"sticky-{sCoreService.Id}",
            ToId = $"sticky-{sValidator.Id}",
            Label = "Calls",
            ColorHex = CanvasConnectionProtocol.Color(CanvasConnectionProtocol.Rest)
        };

        var c5 = new CanvasConnection
        {
            FromId = $"sticky-{sCoreService.Id}",
            ToId = $"sticky-{sDB.Id}",
            Label = CanvasConnectionProtocol.Sql,
            ColorHex = CanvasConnectionProtocol.Color(CanvasConnectionProtocol.Sql)
        };

        var c6 = new CanvasConnection
        {
            FromId = $"sticky-{sCoreService.Id}",
            ToId = $"sticky-{sRedis.Id}",
            Label = CanvasConnectionProtocol.Rest,
            ColorHex = CanvasConnectionProtocol.Color(CanvasConnectionProtocol.Rest)
        };

        var c7 = new CanvasConnection
        {
            FromId = $"sticky-{sCoreService.Id}",
            ToId = $"sticky-{sEventBus.Id}",
            Label = CanvasConnectionProtocol.EventBus,
            ColorHex = CanvasConnectionProtocol.Color(CanvasConnectionProtocol.EventBus)
        };

        var c8 = new CanvasConnection
        {
            FromId = $"sticky-{sEventBus.Id}",
            ToId = $"sticky-{sWorker.Id}",
            Label = CanvasConnectionProtocol.Queue,
            ColorHex = CanvasConnectionProtocol.Color(CanvasConnectionProtocol.Queue)
        };

        var c9 = new CanvasConnection
        {
            FromId = $"sticky-{sUI.Id}",
            ToId = $"sticky-{sState.Id}",
            Label = "Sync",
            ColorHex = CanvasConnectionProtocol.Color(CanvasConnectionProtocol.WebSocket)
        };

        return new AutoArchitectureResult(
            new[] { fClient, fGateway, fDomain, fData, fAsync },
            new[] { sUI, sState, sGateway, sAuth, sCoreService, sValidator, sDB, sRedis, sEventBus, sWorker },
            new[] { c1, c2, c3, c4, c5, c6, c7, c8, c9 }
        );
    }
}
