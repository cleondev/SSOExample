// SSOExample jQuery SPA
// - Hash-router: signin, dashboard, admin, audit-history
// - Sign in with Microsoft: Authorization Code + PKCE thật với Microsoft Entra ID
// - Password login local + login-as cho admin (issue local-signed JWT)
// - Token trong sessionStorage; banner đỏ khi impersonate

const STORAGE_KEY = 'ssoexample.session.v2';
const PKCE_KEY = 'ssoexample.pkce.v2';

const config = {
  apiBaseUrl: 'https://localhost:5001',
  localDemoClientId: 'ssoexample-web',
  redirectUri: window.location.origin + '/auth/callback',
  authority: null,
  entraClientId: null,
  scopes: []
};

function hasPlaceholder(value) {
  return typeof value !== 'string' || !value.length || value.includes('<') || value.includes('>');
}

function useEntraId() {
  return !hasPlaceholder(config.authority) && !hasPlaceholder(config.entraClientId);
}

const state = {
  accessToken: null,
  refreshToken: null,
  expiresAt: null,
  user: null,
  impersonation: null
};

// ---------- session ----------
function loadSession() {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return;
    Object.assign(state, JSON.parse(raw));
  } catch { /* ignore */ }
}
function persistSession() {
  if (!state.accessToken) {
    sessionStorage.removeItem(STORAGE_KEY);
    return;
  }
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}
function clearSession() {
  state.accessToken = null;
  state.refreshToken = null;
  state.expiresAt = null;
  state.user = null;
  state.impersonation = null;
  sessionStorage.removeItem(STORAGE_KEY);
}
function applyTokenResponse(response) {
  state.accessToken = response.accessToken;
  state.refreshToken = response.refreshToken;
  state.expiresAt = response.expiresAt;
  state.user = response.user;
  state.impersonation = response.impersonation || null;
  persistSession();
}

// ---------- API client ----------
function api(path, options = {}) {
  return $.ajax({
    url: `${config.apiBaseUrl}${path}`,
    method: options.method || 'GET',
    contentType: 'application/json',
    data: options.body && JSON.stringify(options.body),
    headers: state.accessToken ? { Authorization: `Bearer ${state.accessToken}` } : {}
  });
}
function apiError(xhr) {
  const msg = xhr.responseJSON?.error || xhr.statusText || 'Lỗi không xác định';
  return `${xhr.status} · ${msg}`;
}

// ---------- PKCE ----------
function base64UrlEncode(buf) {
  return btoa(String.fromCharCode(...new Uint8Array(buf)))
    .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
async function createPkce() {
  const verifier = base64UrlEncode(crypto.getRandomValues(new Uint8Array(32)));
  const hash = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
  return { verifier, challenge: base64UrlEncode(hash), state: base64UrlEncode(crypto.getRandomValues(new Uint8Array(16))) };
}

// ---------- UI helpers ----------
function toast(message, type = 'info') {
  $('.toast').remove();
  const $el = $('<div class="toast"></div>').addClass(type).text(message).appendTo('body');
  requestAnimationFrame(() => $el.addClass('show'));
  setTimeout(() => $el.removeClass('show'), 3000);
  setTimeout(() => $el.remove(), 3400);
}
function escapeHtml(value) {
  return $('<div>').text(value ?? '').html();
}
function rolesHtml(roles) {
  return (roles || []).map(r => `<span class="badge role-${escapeHtml(r)}">${escapeHtml(r)}</span>`).join('');
}
function statusBadge(status) {
  return `<span class="badge status-${escapeHtml(status)}">${escapeHtml(status)}</span>`;
}
function decodeJwt(token) {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    const padded = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(decodeURIComponent(escape(atob(padded.padEnd(padded.length + (4 - padded.length % 4) % 4, '=')))));
  } catch { return null; }
}

// ---------- Router ----------
const routes = {
  '/signin': renderSignin,
  '/dashboard': renderDashboard,
  '/admin': renderAdmin,
  '/audit-history': renderAuditHistory
};

function defaultRoute() {
  return state.accessToken ? '/dashboard' : '/signin';
}

function currentRoute() {
  const hash = window.location.hash.replace(/^#/, '');
  const path = hash.split('?')[0];
  return path || defaultRoute();
}

function navigate(path) {
  if (window.location.hash === `#${path}`) {
    render();
  } else {
    window.location.hash = `#${path}`;
  }
}

function render() {
  const path = currentRoute();
  const handler = routes[path];

  if (!handler) {
    navigate(defaultRoute());
    return;
  }

  $('.nav a').removeClass('active');
  const route = path.replace(/^\//, '');
  $(`.nav a[data-route="${route}"]`).addClass('active');

  renderUserPill();
  renderImpersonationBanner();
  handler();
}

window.addEventListener('hashchange', render);

// ---------- Header / banner ----------
function renderUserPill() {
  const $pill = $('#userPill');
  const $sign = $('#signInButton');
  if (!state.user) {
    $pill.attr('hidden', true);
    $sign.removeAttr('hidden');
    return;
  }
  $sign.attr('hidden', true);
  $pill.removeAttr('hidden');
  const initials = (state.user.displayName || state.user.userName || '?').split(/\s+/).map(p => p[0]).join('').slice(0, 2).toUpperCase();
  $pill.find('.avatar').text(initials);
  $pill.find('.user-name').text(state.user.displayName || state.user.userName);
  $pill.find('.user-role').text((state.user.roles || []).join(', ') || 'user');

  $('.nav a[data-role="Admin"]').toggle((state.user.roles || []).includes('Admin'));
}
function renderImpersonationBanner() {
  const $banner = $('#impersonationBanner');
  if (!state.impersonation) {
    $banner.attr('hidden', true);
    return;
  }
  const expires = new Date(state.impersonation.expiresAt).toLocaleTimeString();
  $banner.find('.text').text(
    `Bạn đang login as ${state.user?.displayName || state.user?.userName} · actor: ${state.impersonation.actorUserName} · hết hạn ${expires}`
  );
  $banner.removeAttr('hidden');
}

// ---------- Views ----------
function mount(templateId) {
  const tpl = document.getElementById(templateId);
  $('#view').empty().append(tpl.content.cloneNode(true));
}

function renderSignin() {
  mount('tpl-signin');

  if (useEntraId()) {
    $('#ssoModeHint').html(`Redirect tới <code>${escapeHtml(config.authority)}</code> với client <code>${escapeHtml(config.entraClientId)}</code>. Đăng nhập bằng user thật của tenant Entra ID.`);
  } else {
    $('#ssoModeHint').html(`<strong>Chưa cấu hình.</strong> Cập nhật <code>MicrosoftEntraId.Authority</code> + <code>MicrosoftEntraId.ClientId</code> trong <code>SsoExample.Web/appsettings.Required.json</code> rồi restart Web.`);
    $('#ssoStart').prop('disabled', true);
  }

  $('#ssoStart').on('click', startSsoFlow);

  $('#passwordForm').on('submit', e => {
    e.preventDefault();
    $('#signinError').attr('hidden', true);
    api('/api/auth/login/password', {
      method: 'POST',
      body: {
        userNameOrEmail: $('#username').val(),
        password: $('#password').val(),
        clientId: config.localDemoClientId
      }
    }).then(response => {
      applyTokenResponse(response);
      toast('Đăng nhập thành công');
      navigate('/dashboard');
    }).catch(xhr => {
      $('#signinError').text(`Đăng nhập thất bại: ${apiError(xhr)}`).removeAttr('hidden');
    });
  });
}

function entraEndpoint(suffix) {
  // Authority dạng `https://login.microsoftonline.com/{tenant}/v2.0` — endpoint nằm ở `{base}/oauth2/v2.0/{suffix}`
  const base = config.authority.replace(/\/v2\.0\/?$/, '').replace(/\/$/, '');
  return `${base}/oauth2/v2.0/${suffix}`;
}

function buildScopeList() {
  if (!config.apiScope || config.apiScope.includes('<')) {
    throw new Error('Api.RequiredScope chưa được cấu hình trong appsettings.Required.json.');
  }
  return ['openid', 'profile', 'email', config.apiScope];
}

async function startSsoFlow() {
  if (!useEntraId()) {
    toast('Microsoft Entra ID chưa được cấu hình — kiểm tra appsettings.Required.json (Authority, ClientId).', 'error');
    return;
  }

  try {
    const pkce = await createPkce();
    sessionStorage.setItem(PKCE_KEY, JSON.stringify(pkce));

    const params = new URLSearchParams({
      client_id: config.entraClientId,
      response_type: 'code',
      redirect_uri: config.redirectUri,
      response_mode: 'query',
      scope: buildScopeList().join(' '),
      state: pkce.state,
      code_challenge: pkce.challenge,
      code_challenge_method: 'S256',
      prompt: 'select_account'
    });
    window.location.href = `${entraEndpoint('authorize')}?${params.toString()}`;
  } catch (err) {
    toast('Không khởi tạo được PKCE: ' + err.message, 'error');
  }
}

async function handleSsoCallback(search) {
  const params = new URLSearchParams(search);
  const code = params.get('code');
  const stateParam = params.get('state');
  const errorParam = params.get('error');
  const stored = JSON.parse(sessionStorage.getItem(PKCE_KEY) || 'null');

  history.replaceState(null, '', '/');

  if (errorParam) {
    toast(`Entra ID báo lỗi: ${errorParam} · ${params.get('error_description') || ''}`, 'error');
    navigate('/signin');
    return;
  }

  if (!code || !stored || stored.state !== stateParam) {
    toast('Callback không hợp lệ (state mismatch hoặc thiếu code)', 'error');
    navigate('/signin');
    return;
  }

  try {
    await exchangeEntraCode(code, stored.verifier);
    sessionStorage.removeItem(PKCE_KEY);
    toast('Sign in with Microsoft thành công');
    navigate('/dashboard');
  } catch (err) {
    toast('Đổi code thất bại: ' + err.message, 'error');
    navigate('/signin');
  }
}

async function exchangeEntraCode(code, verifier) {
  const tokenUrl = entraEndpoint('token');
  const body = new URLSearchParams({
    client_id: config.entraClientId,
    grant_type: 'authorization_code',
    code,
    redirect_uri: config.redirectUri,
    code_verifier: verifier,
    scope: buildScopeList().join(' ')
  });

  const res = await fetch(tokenUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Microsoft /token ${res.status} · ${text}`);
  }
  const tokens = await res.json();
  const idClaims = tokens.id_token ? decodeJwt(tokens.id_token) || {} : {};
  const accessClaims = decodeJwt(tokens.access_token) || {};

  applyTokenResponse({
    accessToken: tokens.access_token,
    refreshToken: tokens.refresh_token || null,
    expiresAt: new Date(Date.now() + (tokens.expires_in || 0) * 1000).toISOString(),
    user: {
      id: idClaims.oid || accessClaims.oid || idClaims.sub || '',
      userName: idClaims.preferred_username || accessClaims.preferred_username || idClaims.email || '',
      email: idClaims.email || idClaims.preferred_username || '',
      displayName: idClaims.name || accessClaims.name || idClaims.preferred_username || 'user',
      roles: (accessClaims.roles || idClaims.roles || [])
    },
    impersonation: null
  });
}

function renderDashboard() {
  if (!state.accessToken) {
    mount('tpl-401');
    return;
  }
  mount('tpl-dashboard');
  $('#greetName').text(state.user?.displayName || state.user?.userName || 'bạn');
  $('#refreshDashboard').on('click', loadDashboardData);
  $('#copyToken').on('click', () => {
    navigator.clipboard.writeText(state.accessToken).then(() => toast('Đã copy token'));
  });
  loadDashboardData();
}

function loadDashboardData() {
  api('/api/me')
    .then(me => {
      const decoded = decodeJwt(state.accessToken) || {};
      const rows = [
        ['User ID', me.userId],
        ['Username', me.userName],
        ['Email', me.email || '—'],
        ['Display name', me.displayName || '—'],
        ['Roles', (me.roles || []).join(', ') || '(none)'],
        ['Issuer (iss)', decoded.iss || '—'],
        ['Audience (aud)', decoded.aud || '—'],
        ['Expires (exp)', decoded.exp ? new Date(decoded.exp * 1000).toLocaleString() : '—']
      ];
      $('#meProps').html(rows.map(([k, v]) => `<dt>${escapeHtml(k)}</dt><dd>${escapeHtml(v)}</dd>`).join(''));
      $('#tokenView').text(JSON.stringify(decoded, null, 2));
    })
    .catch(xhr => $('#meProps').html(`<dt>Lỗi</dt><dd>${escapeHtml(apiError(xhr))}</dd>`));

  api('/api/orders')
    .then(orders => {
      const body = orders.length
        ? orders.map(o => `<tr><td>${o.id}</td><td>${escapeHtml(o.owner)}</td><td>${o.total.toFixed(2)}</td><td>${statusBadge(o.status)}</td></tr>`).join('')
        : '<tr><td colspan="4" class="muted">Không có đơn hàng nào</td></tr>';
      $('#ordersTable tbody').html(body);
    })
    .catch(xhr => $('#ordersTable tbody').html(`<tr><td colspan="4" class="muted">Lỗi: ${escapeHtml(apiError(xhr))}</td></tr>`));
}

function renderAdmin() {
  if (!state.accessToken) { mount('tpl-401'); return; }
  if (!(state.user?.roles || []).includes('Admin')) { mount('tpl-403'); return; }
  mount('tpl-admin');

  $('#refreshAdmin').on('click', loadAdminData);

  loadAdminData();
}

function loadAdminData() {
  api('/api/admin/users')
    .then(users => {
      const rows = users.map(u => {
        const isAdmin = (u.roles || []).includes('Admin');
        const action = isAdmin
          ? '<span class="muted small">Không impersonate admin</span>'
          : `<button class="danger sm js-login-as" data-user-id="${escapeHtml(u.id)}" data-user-name="${escapeHtml(u.displayName)} (${escapeHtml(u.userName)})">Login as</button>`;
        return `<tr>
          <td><strong>${escapeHtml(u.displayName)}</strong><br><span class="muted small">${escapeHtml(u.userName)}</span></td>
          <td>${escapeHtml(u.email)}</td>
          <td>${rolesHtml(u.roles)}</td>
          <td class="col-action">${action}</td>
        </tr>`;
      }).join('');
      $('#usersTable tbody').html(rows || '<tr><td colspan="4" class="muted">Không có user</td></tr>');
      $('#usersTable .js-login-as').on('click', function () {
        openLoginAsDialog($(this).data('user-id'), $(this).data('user-name'));
      });
    })
    .catch(xhr => $('#usersTable tbody').html(`<tr><td colspan="4" class="muted">Lỗi: ${escapeHtml(apiError(xhr))}</td></tr>`));

  api('/api/admin/audit-logs')
    .then(logs => {
      const body = logs.length
        ? logs.map(l => `<tr>
            <td>${new Date(l.createdAt).toLocaleString()}</td>
            <td><span class="badge">${escapeHtml(l.action)}</span></td>
            <td>${escapeHtml(l.actorUserId)}</td>
            <td>${escapeHtml(l.subjectUserId || '—')}</td>
            <td>${escapeHtml(l.reason || '—')}</td>
            <td>${escapeHtml(l.ipAddress)}</td>
          </tr>`).join('')
        : '<tr><td colspan="6" class="muted">Chưa có audit log</td></tr>';
      $('#auditTable tbody').html(body);
    })
    .catch(xhr => $('#auditTable tbody').html(`<tr><td colspan="6" class="muted">Lỗi: ${escapeHtml(apiError(xhr))}</td></tr>`));
}

function openLoginAsDialog(userId, userLabel) {
  const dialog = document.getElementById('loginAsDialog');
  if (!dialog) return;
  $('#loginAsTargetLabel').text(userLabel);
  $('#loginAsForm').data('targetUserId', userId);
  $('#loginAsReason').val('Hỗ trợ kiểm tra lỗi đơn hàng theo ticket #12345');
  if (typeof dialog.showModal === 'function') {
    dialog.showModal();
  } else {
    dialog.setAttribute('open', '');
  }
}

function closeLoginAsDialog() {
  const dialog = document.getElementById('loginAsDialog');
  if (!dialog) return;
  if (typeof dialog.close === 'function') {
    dialog.close();
  } else {
    dialog.removeAttribute('open');
  }
}

function bindLoginAsDialog() {
  const $form = $('#loginAsForm');
  if (!$form.length || $form.data('bound')) return;
  $form.data('bound', true);

  $('#loginAsCancel, #loginAsCancel2').on('click', closeLoginAsDialog);

  $form.on('submit', e => {
    e.preventDefault();
    const reason = $('#loginAsReason').val().trim();
    const targetUserId = $form.data('targetUserId');
    if (!targetUserId) {
      toast('Chưa chọn user để impersonate', 'error');
      return;
    }
    if (reason.length < 10) {
      toast('Lý do tối thiểu 10 ký tự', 'error');
      return;
    }
    api('/api/auth/login-as', {
      method: 'POST',
      body: { targetUserId, reason, clientId: config.localDemoClientId }
    }).then(response => {
      applyTokenResponse(response);
      closeLoginAsDialog();
      toast(`Đang login as ${response.user.displayName}`);
      navigate('/dashboard');
    }).catch(xhr => toast('Login-as thất bại: ' + apiError(xhr), 'error'));
  });
}

function renderAuditHistory() {
  if (!state.accessToken) { mount('tpl-401'); return; }
  if (!(state.user?.roles || []).includes('Admin')) { mount('tpl-403'); return; }
  mount('tpl-audit-history');

  $('#refreshAuditHistory').on('click', loadAuditHistory);
  loadAuditHistory();
}

function loadAuditHistory() {
  api('/api/admin/request-logs')
    .then(logs => {
      const body = logs.length
        ? logs.map(l => {
            const statusClass = l.statusCode >= 500 ? 'danger'
              : l.statusCode >= 400 ? 'warn'
              : l.statusCode >= 300 ? 'info'
              : 'ok';
            const actor = l.actorUserName
              ? `${escapeHtml(l.actorUserName)}<br><span class="muted small">${escapeHtml(l.actorUserId || '')}</span>`
              : '<span class="muted">anonymous</span>';
            const impersonation = l.impersonated
              ? `<span class="badge role-Admin">via ${escapeHtml(l.impersonationActorUserName || '')}</span>`
              : '<span class="muted small">—</span>';
            return `<tr>
              <td>${new Date(l.createdAt).toLocaleString()}</td>
              <td>${actor}</td>
              <td><span class="badge method-${escapeHtml(l.method.toLowerCase())}">${escapeHtml(l.method)}</span></td>
              <td><code>${escapeHtml(l.path)}</code></td>
              <td><span class="badge status-${statusClass}">${l.statusCode}</span></td>
              <td>${impersonation}</td>
              <td>${escapeHtml(l.ipAddress)}</td>
            </tr>`;
          }).join('')
        : '<tr><td colspan="7" class="muted">Chưa có request nào được ghi nhận.</td></tr>';
      $('#requestLogTable tbody').html(body);
    })
    .catch(xhr => $('#requestLogTable tbody').html(`<tr><td colspan="7" class="muted">Lỗi: ${escapeHtml(apiError(xhr))}</td></tr>`));
}

// ---------- Bootstrap ----------
function bindGlobalEvents() {
  $('#logout').on('click', () => {
    clearSession();
    toast('Đã đăng xuất');
    navigate('/signin');
  });
  $('#endImpersonation').on('click', () => {
    clearSession();
    toast('Đã kết thúc phiên login-as');
    navigate('/signin');
  });
}

$(async function () {
  loadSession();

  try {
    const appConfig = await $.getJSON('/config');
    config.apiBaseUrl = appConfig.api?.baseUrl || config.apiBaseUrl;
    config.localDemoClientId = appConfig.api?.localDemoClientId || config.localDemoClientId;
    config.apiScope = appConfig.api?.requiredScope;
    config.authority = appConfig.microsoftEntraId?.authority;
    config.entraClientId = appConfig.microsoftEntraId?.clientId;
    config.scopes = appConfig.microsoftEntraId?.scopes || [];
    if (appConfig.microsoftEntraId?.redirectUris?.[0]) {
      config.redirectUri = appConfig.microsoftEntraId.redirectUris[0];
    }
  } catch { /* config endpoint optional */ }

  bindGlobalEvents();
  bindLoginAsDialog();

  if (window.location.pathname === '/auth/callback' && new URLSearchParams(window.location.search).has('code')) {
    handleSsoCallback(window.location.search);
    return;
  }

  render();
});
