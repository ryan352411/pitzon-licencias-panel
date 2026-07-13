const state = {
  isLoggedIn: false,
  appCode: "ALL",
  search: "",
  installations: []
};

const elements = {
  username: document.querySelector("#username"),
  password: document.querySelector("#password"),
  btnLogin: document.querySelector("#btnLogin"),
  btnLogout: document.querySelector("#btnLogout"),
  btnRefresh: document.querySelector("#btnRefresh"),
  appFilter: document.querySelector("#appFilter"),
  searchBox: document.querySelector("#searchBox"),
  statusText: document.querySelector("#statusText"),
  metrics: document.querySelector("#metrics"),
  tableCount: document.querySelector("#tableCount"),
  installationsBody: document.querySelector("#installationsBody"),
  eventsList: document.querySelector("#eventsList")
};

elements.username.value = localStorage.getItem("licensingUsername") || "admin";
elements.btnLogin.addEventListener("click", login);
elements.btnLogout.addEventListener("click", logout);
elements.password.addEventListener("keydown", event => {
  if (event.key === "Enter") {
    login();
  }
});

elements.username.addEventListener("keydown", event => {
  if (event.key === "Enter") {
    elements.password.focus();
  }
});

elements.btnRefresh.addEventListener("click", loadDashboard);
elements.appFilter.addEventListener("change", () => {
  state.appCode = elements.appFilter.value;
  loadDashboard();
});

let searchTimer;
elements.searchBox.addEventListener("input", () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    state.search = elements.searchBox.value.trim();
    loadDashboard();
  }, 250);
});

async function init() {
  try {
    await apiGet("/api/me");
    setLoggedIn(true);
    await loadDashboard();
  } catch {
    setLoggedIn(false);
    renderEmpty("Inicia sesión para cargar instalaciones.");
  }
}

async function login() {
  const username = elements.username.value.trim();
  const password = elements.password.value;

  if (!username || !password) {
    setStatus("Captura usuario y contraseña", false);
    return;
  }

  try {
    setStatus("Iniciando sesión...", true);
    await apiPost("/api/login", { username, password });
    localStorage.setItem("licensingUsername", username);
    elements.password.value = "";
    setLoggedIn(true);
    await loadDashboard();
  } catch (error) {
    setLoggedIn(false);
    setStatus(error.message, false);
  }
}

async function logout() {
  await apiPost("/api/logout", {});
  setLoggedIn(false);
  renderEmpty("Sesión cerrada.");
}

async function loadDashboard() {
  if (!state.isLoggedIn) {
    renderEmpty("Inicia sesión para cargar instalaciones.");
    return;
  }

  try {
    setStatus("Conectando...", true);
    const [summary, installations, events] = await Promise.all([
      apiGet("/api/summary"),
      apiGet(`/api/installations?appCode=${encodeURIComponent(state.appCode)}&search=${encodeURIComponent(state.search)}`),
      apiGet(`/api/events?appCode=${encodeURIComponent(state.appCode)}`)
    ]);

    state.installations = normalizeArray(installations);
    renderMetrics(normalizeArray(summary));
    renderInstallations(state.installations);
    renderEvents(normalizeArray(events));
    setStatus("Conectado", true);
  } catch (error) {
    if (error.status === 401) {
      setLoggedIn(false);
      renderEmpty("La sesión venció. Vuelve a iniciar sesión.");
    }

    setStatus(error.message, false);
  }
}

async function apiGet(url) {
  const response = await fetch(url, { credentials: "same-origin" });
  return readResponse(response);
}

async function apiPost(url, body) {
  const response = await fetch(url, {
    method: "POST",
    credentials: "same-origin",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  return readResponse(response);
}

async function readResponse(response) {
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(payload.error || "No se pudo completar la solicitud.");
    error.status = response.status;
    throw error;
  }

  return payload;
}

function renderMetrics(summary) {
  const totals = summary.reduce((acc, item) => {
    acc.totalInstallations += item.totalInstallations;
    acc.activeInstallations += item.activeInstallations;
    acc.inactiveInstallations += item.inactiveInstallations;
    acc.totalLaunches += item.totalLaunches;
    return acc;
  }, { totalInstallations: 0, activeInstallations: 0, inactiveInstallations: 0, totalLaunches: 0 });

  const pitzon = summary.find(item => item.appCode === "PITZON") || emptySummary("PITZON");
  const torqo = summary.find(item => item.appCode === "TORQO") || emptySummary("TORQO");

  elements.metrics.innerHTML = [
    metricCard("Total instalaciones", totals.totalInstallations, `${totals.activeInstallations} activas`, "total"),
    metricCard("PITZON", pitzon.totalInstallations, `${pitzon.inactiveInstallations} desactivadas`, "pitzon"),
    metricCard("TORQO", torqo.totalInstallations, `${torqo.inactiveInstallations} desactivadas`, "torqo"),
    metricCard("Arranques registrados", totals.totalLaunches, "Desde el primer uso", "total")
  ].join("");
}

function metricCard(label, value, detail, tone) {
  return `
    <article class="metric ${tone}">
      <p class="eyebrow">${escapeHtml(label)}</p>
      <strong>${value}</strong>
      <p class="muted">${escapeHtml(detail)}</p>
    </article>`;
}

function emptySummary(appCode) {
  return {
    appCode,
    totalInstallations: 0,
    activeInstallations: 0,
    inactiveInstallations: 0,
    totalLaunches: 0
  };
}

function renderInstallations(installations) {
  elements.tableCount.textContent = `${installations.length} registros`;

  if (installations.length === 0) {
    elements.installationsBody.innerHTML = `<tr><td colspan="9">No hay instalaciones registradas con estos filtros.</td></tr>`;
    return;
  }

  elements.installationsBody.innerHTML = installations.map(item => `
    <tr>
      <td><span class="badge app">${escapeHtml(item.appCode)}</span></td>
      <td>
        <strong>${escapeHtml(item.machineName)}</strong>
        <p class="muted">${escapeHtml(item.installationId)}</p>
      </td>
      <td>${escapeHtml(item.windowsUser)}</td>
      <td>${formatDate(item.firstSeenAt)}</td>
      <td>${formatDate(item.lastSeenAt)}</td>
      <td>${item.launchCount}</td>
      <td><span class="badge ${item.isActive ? "active" : "inactive"}">${item.isActive ? "Activa" : "Inactiva"}</span></td>
      <td>
        <div class="notes-cell">
          <textarea data-notes="${item.installationId}" rows="2">${escapeHtml(item.notes || "")}</textarea>
          <button class="secondary" data-save-notes="${item.installationId}" type="button">Guardar</button>
        </div>
      </td>
      <td>
        <button class="${item.isActive ? "danger" : "success"}" data-toggle="${item.installationId}" type="button">
          ${item.isActive ? "Desactivar" : "Activar"}
        </button>
      </td>
    </tr>
  `).join("");

  document.querySelectorAll("[data-toggle]").forEach(button => {
    button.addEventListener("click", () => toggleActivation(button.dataset.toggle));
  });

  document.querySelectorAll("[data-save-notes]").forEach(button => {
    button.addEventListener("click", () => saveNotes(button.dataset.saveNotes));
  });
}

function renderEvents(events) {
  if (events.length === 0) {
    elements.eventsList.innerHTML = `<p class="muted">Aun no hay eventos registrados.</p>`;
    return;
  }

  elements.eventsList.innerHTML = events.map(event => `
    <article class="event">
      <div>
        <strong>${escapeHtml(event.appCode)} abierto en ${escapeHtml(event.machineName)}</strong>
        <p class="muted">${escapeHtml(event.windowsUser)} · ${escapeHtml(event.installationId)}</p>
      </div>
      <span class="muted">${formatDate(event.eventAt)}</span>
    </article>
  `).join("");
}

async function toggleActivation(installationId) {
  const installation = state.installations.find(item => item.installationId === installationId);
  if (!installation) {
    return;
  }

  await apiPost(`/api/installations/${installationId}/activation`, { isActive: !installation.isActive });
  await loadDashboard();
}

async function saveNotes(installationId) {
  const input = document.querySelector(`[data-notes="${installationId}"]`);
  await apiPost(`/api/installations/${installationId}/notes`, { notes: input.value });
  await loadDashboard();
}

function setLoggedIn(isLoggedIn) {
  state.isLoggedIn = isLoggedIn;
  elements.btnLogin.classList.toggle("hidden", isLoggedIn);
  elements.btnLogout.classList.toggle("hidden", !isLoggedIn);
  elements.username.disabled = isLoggedIn;
  elements.password.disabled = isLoggedIn;
}

function setStatus(text, isOk) {
  elements.statusText.textContent = text;
  elements.statusText.style.color = isOk ? "#178f5a" : "#c73636";
}

function renderEmpty(message) {
  elements.metrics.innerHTML = "";
  elements.tableCount.textContent = "0 registros";
  elements.installationsBody.innerHTML = `<tr><td colspan="9">${escapeHtml(message)}</td></tr>`;
  elements.eventsList.innerHTML = "";
}

function normalizeArray(value) {
  if (Array.isArray(value)) {
    return value;
  }

  return value ? [value] : [];
}

function formatDate(value) {
  if (!value) {
    return "-";
  }

  return new Intl.DateTimeFormat("es-MX", {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(new Date(value));
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

init();
