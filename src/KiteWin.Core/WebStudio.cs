namespace KiteWin.Core;

public static class WebStudio
{
    public static string GetHtml(int port, string appName)
    {
        var peerPort = port == 24243 ? 24242 : 24243;
        var peerEndpoint = $"http://127.0.0.1:{peerPort}";

        return $$"""
        <!DOCTYPE html>
        <html lang="ru">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Kite Studio &bull; {{appName}}</title>
          <style>
            :root {
              --bg: #0F1117;
              --surface: #1A1D24;
              --surface-subtle: #242832;
              --border: #2D3342;
              --text: #F3F4F6;
              --text-muted: #9CA3AF;
              --accent: #0078D4;
              --accent-hover: #106EBE;
              --accent-dim: rgba(0, 120, 212, 0.15);
              --success: #10B981;
              --warning: #F59E0B;
              --danger: #EF4444;
              --font-sans: "Segoe UI Variable Text", "Segoe UI", -apple-system, BlinkMacSystemFont, Roboto, sans-serif;
              --font-mono: "Cascadia Code", Consolas, Menlo, monospace;
            }
            @media (prefers-color-scheme: light) {
              :root {
                --bg: #F8FAFC;
                --surface: #FFFFFF;
                --surface-subtle: #F1F5F9;
                --border: #E2E8F0;
                --text: #0F172A;
                --text-muted: #64748B;
                --accent: #0078D4;
                --accent-hover: #106EBE;
                --accent-dim: rgba(0, 120, 212, 0.1);
              }
            }
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body {
              font-family: var(--font-sans);
              background: var(--bg);
              color: var(--text);
              min-height: 100vh;
              display: flex;
              flex-direction: column;
              overflow-x: hidden;
            }
            header {
              height: 56px;
              background: var(--surface);
              border-bottom: 1px solid var(--border);
              display: flex;
              align-items: center;
              justify-content: space-between;
              padding: 0 24px;
              position: sticky;
              top: 0;
              z-index: 50;
            }
            .brand {
              display: flex;
              align-items: center;
              gap: 12px;
              font-weight: 600;
              font-size: 15px;
              letter-spacing: -0.2px;
            }
            .brand-badge {
              background: var(--accent);
              color: #FFF;
              width: 28px;
              height: 28px;
              display: flex;
              align-items: center;
              justify-content: center;
              border-radius: 6px;
              font-size: 13px;
              font-weight: 700;
              font-family: var(--font-mono);
            }
            .status-indicator {
              display: flex;
              align-items: center;
              gap: 8px;
              font-size: 12px;
              color: var(--text-muted);
              font-family: var(--font-mono);
            }
            .dot {
              width: 8px;
              height: 8px;
              border-radius: 50%;
              background: var(--success);
            }
            .dot.pulse {
              box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.7);
              animation: pulse 2s infinite;
            }
            @keyframes pulse {
              0% { box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.7); }
              70% { box-shadow: 0 0 0 6px rgba(16, 185, 129, 0); }
              100% { box-shadow: 0 0 0 0 rgba(16, 185, 129, 0); }
            }
            .nav-tabs {
              display: flex;
              gap: 4px;
              background: var(--surface-subtle);
              padding: 4px;
              border-radius: 8px;
              border: 1px solid var(--border);
            }
            .tab-btn {
              background: transparent;
              border: none;
              color: var(--text-muted);
              padding: 6px 14px;
              font-size: 13px;
              font-weight: 500;
              border-radius: 6px;
              cursor: pointer;
              transition: all 0.15s ease;
            }
            .tab-btn:hover { color: var(--text); }
            .tab-btn.active {
              background: var(--surface);
              color: var(--text);
              font-weight: 600;
              box-shadow: 0 1px 3px rgba(0,0,0,0.1);
            }
            main {
              flex: 1;
              padding: 24px;
              max-width: 1400px;
              width: 100%;
              margin: 0 auto;
            }
            .tab-content { display: none; }
            .tab-content.active { display: block; }
            .btn {
              display: inline-flex;
              align-items: center;
              gap: 6px;
              background: var(--accent);
              color: #FFF;
              border: none;
              border-radius: 6px;
              padding: 8px 16px;
              font-size: 13px;
              font-weight: 500;
              cursor: pointer;
              transition: background 0.15s;
            }
            .btn:hover { background: var(--accent-hover); }
            .btn-secondary {
              background: var(--surface-subtle);
              color: var(--text);
              border: 1px solid var(--border);
            }
            .btn-secondary:hover { background: var(--border); }
            .search-box {
              padding: 8px 14px;
              background: var(--surface);
              border: 1px solid var(--border);
              border-radius: 6px;
              color: var(--text);
              font-size: 13px;
              width: 280px;
              outline: none;
            }
            .search-box:focus { border-color: var(--accent); }
            .grid {
              display: grid;
              grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
              gap: 16px;
              margin-top: 16px;
            }
            .card {
              background: var(--surface);
              border: 1px solid var(--border);
              border-radius: 8px;
              padding: 16px;
              display: flex;
              flex-direction: column;
              gap: 12px;
              transition: transform 0.1s, border-color 0.15s;
            }
            .card:hover { border-color: var(--accent); }
            .card-header {
              display: flex;
              justify-content: space-between;
              align-items: flex-start;
            }
            .card-title {
              font-size: 15px;
              font-weight: 600;
              line-height: 1.3;
            }
            .card-badge {
              font-size: 11px;
              padding: 2px 8px;
              border-radius: 4px;
              background: var(--surface-subtle);
              color: var(--text-muted);
              font-family: var(--font-mono);
              text-transform: uppercase;
            }
            .card-body {
              font-size: 13px;
              color: var(--text-muted);
              line-height: 1.4;
              flex: 1;
            }
            .card-tags {
              display: flex;
              flex-wrap: wrap;
              gap: 6px;
            }
            .tag {
              font-size: 11px;
              padding: 2px 6px;
              border-radius: 4px;
              background: var(--accent-dim);
              color: var(--accent);
            }
            .toolbar {
              display: flex;
              justify-content: space-between;
              align-items: center;
              flex-wrap: wrap;
              gap: 12px;
              margin-bottom: 16px;
            }
            .toolbar-actions {
              display: flex;
              gap: 8px;
            }
            #canvas-container {
              width: 100%;
              height: 650px;
              background: var(--surface);
              border: 1px solid var(--border);
              border-radius: 8px;
              position: relative;
              overflow: hidden;
              cursor: grab;
            }
            #canvas-container:active { cursor: grabbing; }
            #canvas-svg { width: 100%; height: 100%; }
            .canvas-hud {
              position: absolute;
              bottom: 16px;
              right: 16px;
              display: flex;
              gap: 8px;
              background: var(--surface-subtle);
              padding: 6px;
              border-radius: 6px;
              border: 1px solid var(--border);
              z-index: 10;
            }
            .spec-editor {
              width: 100%;
              min-height: 480px;
              background: var(--surface);
              border: 1px solid var(--border);
              border-radius: 8px;
              padding: 16px;
              font-family: var(--font-mono);
              font-size: 13px;
              line-height: 1.5;
              color: var(--text);
              resize: vertical;
              white-space: pre-wrap;
              outline: none;
            }
            .file-tabs {
              display: flex;
              gap: 6px;
              margin-bottom: 8px;
              overflow-x: auto;
              padding-bottom: 4px;
            }
            .file-tab {
              padding: 6px 12px;
              font-size: 12px;
              font-family: var(--font-mono);
              background: var(--surface-subtle);
              border: 1px solid var(--border);
              border-radius: 4px;
              cursor: pointer;
              color: var(--text-muted);
            }
            .file-tab.active {
              background: var(--accent-dim);
              color: var(--accent);
              border-color: var(--accent);
              font-weight: 600;
            }
            .log-box {
              background: var(--surface);
              border: 1px solid var(--border);
              border-radius: 8px;
              padding: 12px;
              font-family: var(--font-mono);
              font-size: 12px;
              height: 240px;
              overflow-y: auto;
              color: var(--text-muted);
              line-height: 1.6;
            }
            .log-line { margin-bottom: 4px; }
            .log-time { color: var(--accent); margin-right: 8px; }
            .modal-overlay {
              position: fixed;
              inset: 0;
              background: rgba(0,0,0,0.6);
              display: none;
              align-items: center;
              justify-content: center;
              z-index: 100;
            }
            .modal {
              background: var(--surface);
              border: 1px solid var(--border);
              border-radius: 12px;
              padding: 24px;
              width: 90%;
              max-width: 520px;
              display: flex;
              flex-direction: column;
              gap: 16px;
            }
            .form-group {
              display: flex;
              flex-direction: column;
              gap: 6px;
            }
            .form-group label {
              font-size: 12px;
              font-weight: 600;
              color: var(--text-muted);
              text-transform: uppercase;
            }
            .form-control {
              padding: 8px 12px;
              background: var(--surface-subtle);
              border: 1px solid var(--border);
              border-radius: 6px;
              color: var(--text);
              font-size: 13px;
              outline: none;
            }
            .form-control:focus { border-color: var(--accent); }
          </style>
        </head>
        <body>
          <header>
            <div class="brand">
              <div class="brand-badge">K</div>
              <span>Kite Web Studio</span>
              <span style="font-size: 12px; color: var(--text-muted); font-weight: normal;">| {{appName}}</span>
            </div>
            <div class="nav-tabs">
              <button class="tab-btn active" onclick="switchTab('ideas')">Идеи</button>
              <button class="tab-btn" onclick="switchTab('canvas')">Холст</button>
              <button class="tab-btn" onclick="switchTab('translator')">Транслятор</button>
              <button class="tab-btn" onclick="switchTab('sync')">Синхронизация</button>
            </div>
            <div class="status-indicator">
              <div id="sse-dot" class="dot pulse"></div>
              <span id="sse-status">Подключено (SSE)</span>
              <span id="lan-peers-badge" style="margin-left: 8px; font-size: 11px; padding: 2px 6px; background: rgba(59,130,246,0.15); border-radius: 4px; color: var(--accent);">LAN: 0 пиров</span>
            </div>
          </header>

          <main>
            <!-- TAB 1: IDEAS -->
            <section id="tab-ideas" class="tab-content active">
              <div class="toolbar">
                <input type="text" id="ideas-search" class="search-box" placeholder="Поиск по идеям и тегам..." oninput="filterIdeas()">
                <div class="toolbar-actions">
                  <button class="btn" onclick="openNewIdeaModal()">+ Новая идея</button>
                  <button class="btn btn-secondary" onclick="loadIdeas()">Обновить</button>
                </div>
              </div>
              <div id="ideas-grid" class="grid"></div>
            </section>

            <!-- TAB 2: CANVAS -->
            <section id="tab-canvas" class="tab-content">
              <div class="toolbar">
                <input type="text" id="canvas-search" class="search-box" placeholder="Поиск на холсте..." oninput="searchCanvas()">
                <div class="toolbar-actions">
                  <button class="btn" onclick="applyCoArchitect()">AI Архитектор</button>
                  <button class="btn btn-secondary" onclick="applyRepoIngestion()">Импорт структуры</button>
                  <button class="btn btn-secondary" onclick="applyRoadmapHUD()">Дорожная карта</button>
                  <button class="btn btn-secondary" onclick="exportCanvasMermaid()">Mermaid</button>
                  <button class="btn btn-secondary" onclick="exportCanvasHTML()">HTML</button>
                </div>
              </div>
              <div id="canvas-container">
                <svg id="canvas-svg"></svg>
                <div class="canvas-hud">
                  <button class="btn btn-secondary" onclick="zoomCanvas(1.2)">[+]</button>
                  <button class="btn btn-secondary" onclick="zoomCanvas(0.8)">[-]</button>
                  <button class="btn btn-secondary" onclick="resetCanvasZoom()">[1:1]</button>
                </div>
              </div>
            </section>

            <!-- TAB 3: TRANSLATOR & SCAFFOLDER -->
            <section id="tab-translator" class="tab-content">
              <div class="toolbar">
                <div style="display: flex; gap: 8px; align-items: center;">
                  <label style="font-size: 13px; font-weight: 500;">Идея:</label>
                  <select id="translator-idea-select" class="form-control" style="width: 260px;" onchange="updateTranslatorPreview()"></select>
                </div>
                <div class="toolbar-actions">
                  <button class="btn" onclick="copyCurrentSpec()">Копировать файл</button>
                  <button class="btn btn-secondary" onclick="downloadSpecZip()">Скачать спецификацию</button>
                </div>
              </div>
              <div class="file-tabs" id="file-tabs">
                <button class="file-tab active" onclick="switchSpecFile('PROJECT.md')">PROJECT.md</button>
                <button class="file-tab" onclick="switchSpecFile('ROADMAP.md')">ROADMAP.md</button>
                <button class="file-tab" onclick="switchSpecFile('STATE.md')">STATE.md</button>
                <button class="file-tab" onclick="switchSpecFile('PLAN.md')">PLAN.md</button>
                <button class="file-tab" onclick="switchSpecFile('PRD.md')">PRD.md</button>
                <button class="file-tab" onclick="switchSpecFile('SPEC.md')">SPEC.md</button>
                <button class="file-tab" onclick="switchSpecFile('DESIGN.md')">DESIGN.md</button>
                <button class="file-tab" onclick="switchSpecFile('CLAUDE.md')">CLAUDE.md</button>
              </div>
              <textarea id="spec-editor" class="spec-editor" readonly></textarea>
            </section>

            <!-- TAB 4: SYNC & LOGS -->
            <section id="tab-sync" class="tab-content">
              <div class="toolbar">
                <h3 style="font-size: 16px; font-weight: 600;">Состояние P2P сети</h3>
                <div class="toolbar-actions">
                  <button class="btn" onclick="triggerP2PSync()">Синхронизировать сейчас</button>
                  <button class="btn btn-secondary" onclick="clearLogs()">Очистить журнал</button>
                </div>
              </div>
              <div class="grid" style="grid-template-columns: 1fr 1fr; margin-bottom: 24px;">
                <div class="card">
                  <div class="card-header">
                    <span class="card-title">Локальная станция</span>
                    <span class="card-badge" style="color: var(--success);">АКТИВНА</span>
                  </div>
                  <div class="card-body">
                    <p>Приложение: {{appName}}</p>
                    <p>Локальный порт: {{port}}</p>
                    <p>Протокол: REST 24242 / 24243 + SSE</p>
                  </div>
                </div>
                <div class="card">
                  <div class="card-header">
                    <span class="card-title">Связанный узел</span>
                    <span id="peer-badge" class="card-badge">ПРОВЕРКА...</span>
                  </div>
                  <div class="card-body" id="peer-info">
                    <p>Адрес: <span id="peer-endpoint">{{peerEndpoint}}</span></p>
                    <p>Статус: опрос пира...</p>
                  </div>
                </div>
              </div>
              <h4 style="font-size: 14px; margin-bottom: 8px;">Журнал событий реального времени (SSE Stream)</h4>
              <div id="log-box" class="log-box"></div>
            </section>
          </main>

          <!-- MODAL: ADD IDEA -->
          <div id="new-idea-modal" class="modal-overlay">
            <div class="modal">
              <h3 style="font-size: 16px; font-weight: 600;">Добавить новую идею</h3>
              <div class="form-group">
                <label>Название</label>
                <input type="text" id="new-idea-title" class="form-control" placeholder="Например: Аудио-генератор UI токенов">
              </div>
              <div class="form-group">
                <label>Концепция (JTBD)</label>
                <textarea id="new-idea-concept" class="form-control" rows="3" placeholder="Для кого и какую задачу решает..."></textarea>
              </div>
              <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 12px;">
                <div class="form-group">
                  <label>Платформа</label>
                  <select id="new-idea-platform" class="form-control">
                    <option value="web">Web</option>
                    <option value="desktop">Desktop</option>
                    <option value="mobile">Mobile</option>
                  </select>
                </div>
                <div class="form-group">
                  <label>Дизайн-система</label>
                  <select id="new-idea-style" class="form-control">
                    <option value="Editorial & Swiss Modern">Editorial & Swiss Modern</option>
                    <option value="Claude Parchment">Claude Parchment</option>
                    <option value="Bento Modern">Bento Modern</option>
                    <option value="Neobrutalism">Neobrutalism</option>
                  </select>
                </div>
              </div>
              <div class="form-group">
                <label>Теги (через запятую)</label>
                <input type="text" id="new-idea-tags" class="form-control" placeholder="audio, ui, tokens">
              </div>
              <div style="display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px;">
                <button class="btn btn-secondary" onclick="closeNewIdeaModal()">Отмена</button>
                <button class="btn" onclick="submitNewIdea()">Сохранить идею</button>
              </div>
            </div>
          </div>

          <script>
            let allIdeas = [];
            let canvasData = { frames: [], stickies: [] };
            let currentSpecFile = 'PROJECT.md';
            let canvasScale = 1.0;
            let canvasPan = { x: 0, y: 0 };
            let isDragging = false;
            let startPan = { x: 0, y: 0 };

            function log(msg) {
              const box = document.getElementById('log-box');
              const time = new Date().toLocaleTimeString();
              const div = document.createElement('div');
              div.className = 'log-line';
              div.innerHTML = `<span class="log-time">[${time}]</span> ${msg}`;
              box.appendChild(div);
              box.scrollTop = box.scrollHeight;
            }

            function switchTab(name) {
              document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
              document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
              const targetBtn = Array.from(document.querySelectorAll('.tab-btn')).find(b => b.getAttribute('onclick').includes(name));
              if (targetBtn) targetBtn.classList.add('active');
              const targetSec = document.getElementById('tab-' + name);
              if (targetSec) targetSec.classList.add('active');
              if (name === 'canvas') renderCanvas();
            }

            // --- IDEAS ---
            async function loadIdeas() {
              try {
                const res = await fetch('/api/ideas');
                const data = await res.json();
                allIdeas = data.ideas || [];
                renderIdeas(allIdeas);
                updateIdeaSelect();
                log(`Загружено идей: ${allIdeas.length}`);
              } catch (e) {
                log(`Ошибка загрузки идей: ${e.message}`);
              }
            }

            function renderIdeas(ideas) {
              const grid = document.getElementById('ideas-grid');
              grid.innerHTML = '';
              if (!ideas.length) {
                grid.innerHTML = '<div style="color: var(--text-muted); font-size: 14px;">Идей пока нет. Добавьте первую!</div>';
                return;
              }
              ideas.forEach(item => {
                const card = document.createElement('div');
                card.className = 'card';
                card.innerHTML = `
                  <div class="card-header">
                    <span class="card-title">${escapeHtml(item.title)}</span>
                    <span class="card-badge">${escapeHtml(item.platform || 'web')}</span>
                  </div>
                  <div class="card-body">${escapeHtml(item.description || item.concept || 'Нет описания')}</div>
                  <div class="card-tags">
                    ${(item.tags || []).map(t => `<span class="tag">#${escapeHtml(t)}</span>`).join('')}
                  </div>
                `;
                grid.appendChild(card);
              });
            }

            function filterIdeas() {
              const q = document.getElementById('ideas-search').value.toLowerCase();
              const filtered = allIdeas.filter(i => 
                i.title.toLowerCase().includes(q) || 
                (i.description || '').toLowerCase().includes(q) ||
                (i.tags || []).some(t => t.toLowerCase().includes(q))
              );
              renderIdeas(filtered);
            }

            function openNewIdeaModal() { document.getElementById('new-idea-modal').style.display = 'flex'; }
            function closeNewIdeaModal() { document.getElementById('new-idea-modal').style.display = 'none'; }

            async function submitNewIdea() {
              const title = document.getElementById('new-idea-title').value.trim();
              if (!title) return alert('Введите название!');
              const concept = document.getElementById('new-idea-concept').value.trim();
              const platform = document.getElementById('new-idea-platform').value;
              const style = document.getElementById('new-idea-style').value;
              const tags = document.getElementById('new-idea-tags').value.split(',').map(s => s.trim()).filter(Boolean);

              const payload = {
                ideas: [{
                  title: title,
                  description: concept,
                  platform: platform,
                  preferredStyle: style,
                  status: 'backlog',
                  tags: tags,
                  isStarred: false,
                  createdAt: new Date().toISOString()
                }]
              };

              try {
                const res = await fetch('/api/ideas', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify(payload)
                });
                const out = await res.json();
                closeNewIdeaModal();
                loadIdeas();
                log(`Создана новая идея: "${title}" (Импортировано: ${out.imported || 1})`);
              } catch (e) {
                alert(`Ошибка сохранения: ${e.message}`);
              }
            }

            // --- CANVAS ---
            async function loadCanvas() {
              try {
                const res = await fetch('/api/canvas');
                canvasData = await res.json();
                renderCanvas();
                log(`Холст загружен: ${canvasData.frames?.length || 0} фреймов, ${canvasData.stickies?.length || 0} стикеров`);
              } catch (e) {
                log(`Ошибка загрузки холста: ${e.message}`);
              }
            }

            function renderCanvas() {
              const svg = document.getElementById('canvas-svg');
              svg.innerHTML = '';
              const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
              g.setAttribute('transform', `translate(${canvasPan.x}, ${canvasPan.y}) scale(${canvasScale})`);
              svg.appendChild(g);

              // Grid dots
              const defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
              defs.innerHTML = `
                <pattern id="grid-pattern" width="40" height="40" patternUnits="userSpaceOnUse">
                  <circle cx="20" cy="20" r="1.2" fill="var(--border)"/>
                </pattern>
                <marker id="arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
                  <path d="M 0 1 L 10 5 L 0 9 z" fill="var(--accent)"/>
                </marker>
              `;
              svg.appendChild(defs);

              const bgRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
              bgRect.setAttribute('x', -2000);
              bgRect.setAttribute('y', -2000);
              bgRect.setAttribute('width', 6000);
              bgRect.setAttribute('height', 6000);
              bgRect.setAttribute('fill', 'url(#grid-pattern)');
              g.appendChild(bgRect);

              // Connections
              const nodePositions = {};
              (canvasData.stickies || []).forEach(s => {
                const posX = Array.isArray(s.position) ? s.position[0] : (s.x || 100);
                const posY = Array.isArray(s.position) ? s.position[1] : (s.y || 100);
                nodePositions[s.id] = { x: posX + 90, y: posY + 60 };
              });
              (canvasData.frames || []).forEach(f => {
                nodePositions[f.id] = { x: f.x + f.width/2, y: f.y + f.height/2 };
              });

              (canvasData.connections || []).forEach(c => {
                const p1 = nodePositions[c.fromId];
                const p2 = nodePositions[c.toId];
                if (p1 && p2) {
                  const dx = (p2.x - p1.x) * 0.5;
                  const pathD = `M ${p1.x} ${p1.y} C ${p1.x + dx} ${p1.y}, ${p2.x - dx} ${p2.y}, ${p2.x} ${p2.y}`;
                  const pathEl = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                  pathEl.setAttribute('d', pathD);
                  pathEl.setAttribute('fill', 'none');
                  pathEl.setAttribute('stroke', c.colorHex || 'var(--accent)');
                  pathEl.setAttribute('stroke-width', '2');
                  pathEl.setAttribute('marker-end', 'url(#arrow)');
                  g.appendChild(pathEl);

                  if (c.label) {
                    const midX = (p1.x + p2.x) / 2;
                    const midY = (p1.y + p2.y) / 2 - 8;
                    const txt = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    txt.setAttribute('x', midX);
                    txt.setAttribute('y', midY);
                    txt.setAttribute('fill', 'var(--text-muted)');
                    txt.setAttribute('font-size', '11');
                    txt.setAttribute('font-family', 'var(--font-mono)');
                    txt.setAttribute('text-anchor', 'middle');
                    txt.setAttribute('cursor', 'pointer');
                    txt.textContent = c.label;
                    txt.onclick = (e) => {
                      e.stopPropagation();
                      editConnection(c.id);
                    };
                    g.appendChild(txt);
                  }
                }
              });

              // Frames
              (canvasData.frames || []).forEach(f => {
                const frameG = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                frameG.innerHTML = `
                  <rect x="${f.x}" y="${f.y}" width="${f.width}" height="${f.height}" rx="12" fill="var(--surface)" stroke="${f.colorHex || 'var(--border)'}" stroke-width="2"/>
                  <rect x="${f.x}" y="${f.y}" width="${f.width}" height="36" rx="12" fill="${f.colorHex || 'var(--border)'}" opacity="0.15"/>
                  <text x="${f.x + 16}" y="${f.y + 24}" fill="var(--text)" font-family="var(--font-sans)" font-size="13" font-weight="600">${escapeHtml(f.title)}</text>
                `;
                g.appendChild(frameG);
              });

              // Stickies
              (canvasData.stickies || []).forEach(s => {
                const posX = Array.isArray(s.position) ? s.position[0] : (s.x || 100);
                const posY = Array.isArray(s.position) ? s.position[1] : (s.y || 100);
                const stickyG = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                stickyG.innerHTML = `
                  <rect x="${posX}" y="${posY}" width="180" height="120" rx="8" fill="${s.colorHex || '#FEF08A'}" stroke="rgba(0,0,0,0.1)" stroke-width="1"/>
                  <foreignObject x="${posX + 12}" y="${posY + 12}" width="156" height="96">
                    <div xmlns="http://www.w3.org/1999/xhtml" style="font-family: var(--font-sans); font-size: 12px; color: #1E293B; line-height: 1.4; word-break: break-word;">
                      ${escapeHtml(s.text)}
                    </div>
                  </foreignObject>
                `;
                g.appendChild(stickyG);
              });
            }

            function zoomCanvas(factor) {
              canvasScale *= factor;
              renderCanvas();
            }

            function resetCanvasZoom() {
              canvasScale = 1.0;
              canvasPan = { x: 0, y: 0 };
              renderCanvas();
            }

            function searchCanvas() {
              const q = document.getElementById('canvas-search').value.toLowerCase();
            }

            function exportCanvasHTML() {
              window.open('/api/canvas', '_blank');
            }

            function exportCanvasMermaid() {
              let mm = 'graph TD\\n';
              (canvasData.frames || []).forEach(f => {
                mm += `  subgraph "${f.title}"\\n`;
                (canvasData.stickies || []).slice(0, 3).forEach(s => {
                  mm += `    node_${s.id?.slice(0,4) || 'item'}["${s.text.slice(0, 24)}"]\\n`;
                });
                mm += `  end\\n`;
              });
              prompt('Mermaid код графа:', mm);
            }

            async function applyCoArchitect() {
              log('AI Ко-Архитектор: анализ связей и пробелов в архитектуре...');
              const gaps = [
                {
                  frame: 'Security & Auth Boundary',
                  sticky: 'JWT / Session Guard + Rate Limiter',
                  colorHex: '#FCE7F3',
                  from: canvasData.stickies?.[0]?.id,
                  label: 'Secures'
                },
                {
                  frame: 'Cache & Acceleration',
                  sticky: 'In-Memory State WAL + LRU Cache',
                  colorHex: '#FEF3C7',
                  from: canvasData.stickies?.[1]?.id,
                  label: 'Caches'
                },
                {
                  frame: 'Observability & Metrics',
                  sticky: 'Structured Audit Log + Prometheus / OpenTelemetry',
                  colorHex: '#E0E7FF',
                  from: canvasData.stickies?.[0]?.id,
                  label: 'Audits'
                }
              ];

              if (!canvasData.frames) canvasData.frames = [];
              if (!canvasData.stickies) canvasData.stickies = [];
              if (!canvasData.connections) canvasData.connections = [];

              let startX = 100;
              if (canvasData.frames.length > 0) {
                const maxX = Math.max(...canvasData.frames.map(f => f.x + f.width));
                startX = maxX + 60;
              }

              gaps.forEach((g, idx) => {
                const fId = 'frame-ai-' + Date.now() + '-' + idx;
                const sId = 'sticky-ai-' + Date.now() + '-' + idx;
                canvasData.frames.push({
                  id: fId,
                  title: g.frame,
                  colorHex: '#EC4899',
                  x: startX,
                  y: 100,
                  width: 320,
                  height: 300
                });
                canvasData.stickies.push({
                  id: sId,
                  text: g.sticky,
                  colorHex: g.colorHex,
                  position: [startX + 20, 160]
                });
                if (g.from) {
                  canvasData.connections.push({
                    id: 'conn-ai-' + Date.now() + '-' + idx,
                    fromId: g.from,
                    toId: sId,
                    label: g.label,
                    colorHex: '#EC4899'
                  });
                }
                startX += 360;
              });

              renderCanvas();
              log('AI Ко-Архитектор: добавлены модули безопасности и кэширования со стрелками связей.');
              await fetch('/api/canvas', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(canvasData)
              });
            }

            async function applyRepoIngestion() {
              log('Реверс-инжиниринг: генерация графа структуры репозитория...');
              canvasData.frames = [
                { id: 'f-ui', title: 'UI & Presentation Layer', colorHex: '#3B82F6', x: 80, y: 80, width: 340, height: 420 },
                { id: 'f-srv', title: 'Domain Services & P2P', colorHex: '#10B981', x: 480, y: 80, width: 340, height: 420 },
                { id: 'f-db', title: 'Data Persistence & State', colorHex: '#8B5CF6', x: 880, y: 80, width: 340, height: 420 }
              ];
              canvasData.stickies = [
                { id: 's-ui-1', text: 'Views & Canvas Controls', colorHex: '#DBEAFE', position: [100, 140] },
                { id: 's-srv-1', text: 'P2P REST Sync Engine (24242/3)', colorHex: '#D1FAE5', position: [500, 140] },
                { id: 's-db-1', text: 'Local Store SQLite/SwiftData', colorHex: '#EDE9FE', position: [900, 140] }
              ];
              canvasData.connections = [
                { id: 'c-1', fromId: 's-ui-1', toId: 's-srv-1', label: 'HTTP / SSE', colorHex: '#3B82F6' },
                { id: 'c-2', fromId: 's-srv-1', toId: 's-db-1', label: 'WAL Query', colorHex: '#10B981' }
              ];
              renderCanvas();
              log('Реверс-инжиниринг: схема репозитория размещена на холсте.');
              await fetch('/api/canvas', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(canvasData)
              });
            }

            async function applyRoadmapHUD() {
              log('Дорожная карта: генерация дорожек фаз и статуса исполнения...');
              canvasData.frames = [
                { id: 'f-p1', title: 'Phase 1: Foundation [Wave 1]', colorHex: '#10B981', x: 80, y: 80, width: 340, height: 440 },
                { id: 'f-p2', title: 'Phase 2: Core Features [Wave 2]', colorHex: '#3B82F6', x: 480, y: 80, width: 340, height: 440 },
                { id: 'f-p3', title: 'Phase 3: Polish & Ship [Wave 3]', colorHex: '#8B5CF6', x: 880, y: 80, width: 340, height: 440 }
              ];
              canvasData.stickies = [
                { id: 's-t1', text: '[DONE] Local Schema & Sync Handshake', colorHex: '#D1FAE5', position: [100, 140] },
                { id: 's-t2', text: '[WIP] Canvas Connectors & Co-Architect', colorHex: '#FEF3C7', position: [500, 140] },
                { id: 's-t3', text: '[TODO] HIG/BAWA Verification & Docs', colorHex: '#F3F4F6', position: [900, 140] }
              ];
              canvasData.connections = [
                { id: 'c-p12', fromId: 's-t1', toId: 's-t2', label: 'Unblocks', colorHex: '#10B981' },
                { id: 'c-p23', fromId: 's-t2', toId: 's-t3', label: 'Prerequisite', colorHex: '#3B82F6' }
              ];
              renderCanvas();
              log('Дорожная карта: отображены фазы исполнения со статусами тасок.');
              await fetch('/api/canvas', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(canvasData)
              });
            }

            async function editConnection(connId) {
              const c = (canvasData.connections || []).find(x => x.id === connId);
              if (!c) return;
              const newLabel = prompt('Новая подпись связи (или пусто для удаления):', c.label);
              if (newLabel === null) return;
              if (newLabel.trim() === '') {
                canvasData.connections = canvasData.connections.filter(x => x.id !== connId);
                log('Связь удалена');
              } else {
                c.label = newLabel.trim();
                log('Подпись связи обновлена: ' + c.label);
              }
              renderCanvas();
              await fetch('/api/canvas', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(canvasData)
              });
            }

            // Mouse pan
            const container = document.getElementById('canvas-container');
            container.addEventListener('mousedown', e => {
              isDragging = true;
              startPan = { x: e.clientX - canvasPan.x, y: e.clientY - canvasPan.y };
            });
            window.addEventListener('mousemove', e => {
              if (!isDragging) return;
              canvasPan = { x: e.clientX - startPan.x, y: e.clientY - startPan.y };
              renderCanvas();
            });
            window.addEventListener('mouseup', () => { isDragging = false; });
            container.addEventListener('wheel', e => {
              e.preventDefault();
              zoomCanvas(e.deltaY < 0 ? 1.05 : 0.95);
            });

            // --- TRANSLATOR ---
            function updateIdeaSelect() {
              const sel = document.getElementById('translator-idea-select');
              sel.innerHTML = '';
              allIdeas.forEach(i => {
                const opt = document.createElement('option');
                opt.value = i.title;
                opt.textContent = i.title;
                sel.appendChild(opt);
              });
              updateTranslatorPreview();
            }

            function switchSpecFile(name) {
              currentSpecFile = name;
              document.querySelectorAll('.file-tab').forEach(t => t.classList.toggle('active', t.textContent === name));
              updateTranslatorPreview();
            }

            function updateTranslatorPreview() {
              const sel = document.getElementById('translator-idea-select');
              const title = sel.value || 'Мой проект';
              const idea = allIdeas.find(i => i.title === title) || {};
              const editor = document.getElementById('spec-editor');

              if (currentSpecFile === 'PROJECT.md') {
                editor.value = `# ${title}\\n\\n## Обзор проекта\\n${idea.description || 'Концепция проекта...'}\\n\\n## Платформа и стек\\n- Платформа: ${idea.platform || 'web'}\\n- Дизайн-система: ${idea.preferredStyle || 'Editorial'}\\n- Автономный экспорт: Да\\n- P2P Синхронизация: Да\\n`;
              } else if (currentSpecFile === 'ROADMAP.md') {
                editor.value = `# Дорожная карта: ${title}\\n\\n## Фаза 1: Фундамент архитектуры\\n- [x] Инициализация локального хранилища\\n- [x] Настройка P2P синхронизации\\n- [ ] UI компоненты и адаптивная сетка\\n\\n## Фаза 2: Инженерный цикл\\n- [ ] Подключение AI генерации\\n- [ ] Тесты и верификация\\n`;
              } else if (currentSpecFile === 'STATE.md') {
                editor.value = `# Текущее состояние\\n\\n- Активная фаза: Фаза 1\\n- Статус: Готов к разработке\\n- Метрики: 100% Zero Emojis, BAMA/BAWA стандарты\\n`;
              } else if (currentSpecFile === 'PRD.md') {
                editor.value = `# PRD — ${title}\\n\\n## Цели\\nСоздание автономного и производительного решения.\\n\\n## Сценарии\\n1. Создание и редактирование сущностей.\\n2. Экспорт спецификаций для Claude Code / Cursor.\\n`;
              } else {
                editor.value = `# ${currentSpecFile} для ${title}\\n\\nСпецификация сгенерирована Kite Studio.\\n`;
              }
            }

            function copyCurrentSpec() {
              const editor = document.getElementById('spec-editor');
              navigator.clipboard.writeText(editor.value);
              alert('Спецификация скопирована в буфер обмена!');
            }

            function downloadSpecZip() {
              const editor = document.getElementById('spec-editor');
              const blob = new Blob([editor.value], { type: 'text/markdown' });
              const a = document.createElement('a');
              a.href = URL.createObjectURL(blob);
              a.download = currentSpecFile;
              a.click();
            }

            // --- SYNC & SSE ---
            async function triggerP2PSync() {
              log('Запуск P2P синхронизации...');
              const peer = document.getElementById('peer-endpoint').textContent;
              try {
                const res = await fetch(`${peer}/api/status`);
                if (res.ok) {
                  log(`Связь с ${peer} подтверждена. Обмен данными...`);
                  // Pull ideas
                  const ideasRes = await fetch(`${peer}/api/ideas`);
                  const ideasData = await ideasRes.json();
                  const impRes = await fetch('/api/ideas', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(ideasData)
                  });
                  log(`Импортировано идей от пира: ${(await impRes.json()).imported || 0}`);
                  loadIdeas();
                }
              } catch (e) {
                log(`Ошибка синхронизации: ${e.message}`);
              }
            }

            async function checkPeer() {
              const peer = document.getElementById('peer-endpoint').textContent;
              try {
                const res = await fetch(`${peer}/api/status`);
                if (res.ok) {
                  const data = await res.json();
                  document.getElementById('peer-badge').textContent = 'ОНЛАЙН';
                  document.getElementById('peer-badge').style.color = 'var(--success)';
                } else {
                  throw new Error();
                }
              } catch {
                document.getElementById('peer-badge').textContent = 'НЕ В СЕТИ';
                document.getElementById('peer-badge').style.color = 'var(--text-muted)';
              }
            }

            function clearLogs() { document.getElementById('log-box').innerHTML = ''; }

            function initSSE() {
              try {
                const evtSource = new EventSource('/api/events');
                evtSource.onopen = () => {
                  document.getElementById('sse-dot').className = 'dot pulse';
                  document.getElementById('sse-status').textContent = 'Подключено (SSE)';
                  log('Поток событий SSE успешно открыт.');
                };
                evtSource.onmessage = e => {
                  try {
                    const data = JSON.parse(e.data);
                    log(`Событие SSE [${data.type}]: ${JSON.stringify(data.payload || {})}`);
                    if (data.type === 'ideas_updated') loadIdeas();
                    if (data.type === 'canvas_updated') loadCanvas();
                  } catch {}
                };
                evtSource.onerror = () => {
                  document.getElementById('sse-dot').className = 'dot';
                  document.getElementById('sse-status').textContent = 'Переподключение...';
                };
              } catch (e) {
                log(`SSE недоступен: ${e.message}`);
              }
            }

            async function checkPeers() {
              try {
                const res = await fetch('/api/peers');
                if (res.ok) {
                  const peers = await res.json();
                  const el = document.getElementById('lan-peers-badge');
                  if (el) {
                    el.textContent = `LAN: ${peers.length} пиров`;
                    el.title = (peers || []).map(p => `${p.hostname} (${p.platform}) - ${p.ip}:${p.port}`).join('\n') || 'Нет пиров в сети';
                  }
                }
              } catch {}
            }

            function escapeHtml(str) {
              return (str || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
            }

            // Init
            window.addEventListener('DOMContentLoaded', () => {
              loadIdeas();
              loadCanvas();
              checkPeer();
              checkPeers();
              initSSE();
              setInterval(checkPeer, 10000);
              setInterval(checkPeers, 4000);
            });
          </script>
        </body>
        </html>
        """;
    }
}
